using System.Collections.Generic;
using AlienInvasion.Core;
using ICities;
using UnityEngine;

namespace AlienInvasion.Game.Simulation
{
    /// <summary>
    /// Drives triggering an invasion, advancing it and maintaining the contamination.
    ///
    /// Threading, and why it is arranged this way:
    /// The obvious place for the random trigger roll is OnAfterSimulationTick, calling
    /// InvasionManager.StartInvasion straight from there. That is not safe. StartInvasion
    /// constructs a Mothership, and that constructor calls UnityEngine.Object.Instantiate and
    /// writes transform.position - real GameObject and Transform work, which risks undefined
    /// behaviour and corruption when called from anywhere but Unity's main/render thread.
    /// InvasionManager documents StartInvasion as main thread only for exactly this reason.
    /// A Cities: Skylines simulation tick runs on a background thread separate from Unity's
    /// render thread, so calling StartInvasion from OnAfterSimulationTick breaks that contract
    /// and can cause corruption or crashes that are very hard to diagnose.
    ///
    /// So this class splits the work:
    /// - OnUpdate (main thread) handles both the manual hotkey and the random trigger roll.
    ///   The roll uses UnityEngine.Random, which is main-thread safe, rather than
    ///   SimulationManager.instance.m_randomizer, the deterministic RNG meant for the
    ///   simulation thread. All this roll decides is whether and when to start a one-off set
    ///   piece; nothing about it is persisted to the save or has to reproduce on reload, so a
    ///   frame-based Unity RNG is fine. That is unlike, say, the contamination severity roll,
    ///   which does have to agree between the save and the replay.
    ///   The hotkey does not call StartInvasion directly either. To match the vanilla
    ///   disasters - aim, then left click to confirm - it only opens
    ///   AlienInvasion.Game.UI.MothershipPlacementTool through ToolsModifierControl.SetTool,
    ///   and the tool itself calls StartInvasion from OnToolGUI, also on the main thread.
    ///   The summon button in InvasionUI opens the same tool, so the hotkey and the button
    ///   behave identically.
    /// - OnAfterSimulationTick (simulation thread) does only InvasionManager.UpdateSimulation
    ///   and the upkeep and expiry of the contamination zones. It never touches a GameObject
    ///   and never calls StartInvasion, UpdateVisual or RedContaminationVisual.Sync.
    /// </summary>
    public class InvasionThreadingExtension : ThreadingExtensionBase
    {
        private int _pollutionTickCounter;
        private const int PollutionProcessInterval = 16;

        // The game time of the last random-invasion check. The schedule runs on the game clock,
        // not real time, so it stops while paused and follows the game speed - the same basis as
        // TripodActiveDays. 0 means "not primed yet"; see MaybeRollRandomInvasion.
        private long _lastRandomCheckTicks;

        public override void OnUpdate(float realTimeDelta, float simulationTimeDelta)
        {
            try
            {
                // Everything an invasion does - descending, spinning, moving, bobbing, firing -
                // follows the game speed. The simulationTimeDelta CS passes to OnUpdate is the
                // smooth interpolation delta the game itself uses to interpolate vehicles and
                // citizens: it stretches with the speed multiplier and is 0 while paused.
                // Passing that to UpdateVisual instead of realTimeDelta means:
                //   - at 2x and 3x speed the descent, movement, spin and beam interval all
                //     speed up to match
                //   - it freezes naturally while paused, since the delta is 0, which doubles up
                //     with the !paused gate below
                //   - it stretches in the same direction as how long the tripods stay, which is
                //     measured in in-game days
                // At 1x, simulationTimeDelta is about equal to realTimeDelta, so the constants
                // written in seconds still mean what they say.
                bool paused = SimulationManager.instance.SimulationPaused;

                if (Input.GetKeyDown(ModSettings.Hotkey) && InvasionManager.CanStartMore)
                {
                    // The hotkey does not start an invasion outright: it opens the same
                    // placement tool the UI button does, where you aim and left click to
                    // confirm. StartInvasion is called from
                    // MothershipPlacementTool.OnToolGUI, a tool UI event on the main thread.
                    // Placing is allowed while paused; the invasion then stays frozen until
                    // the game is resumed.
                    ToolsModifierControl.SetTool<AlienInvasion.Game.UI.MothershipPlacementTool>();
                }

                // The disasters panel is sometimes created late, so keep trying every frame
                // until the button is attached.
                UI.InvasionUI.EnsureAttached();

                if (!paused)
                {
                    MaybeRollRandomInvasion();
                    // The invasion's visuals advance on simulationTimeDelta, following the game speed.
                    InvasionManager.UpdateVisual(simulationTimeDelta);
                }

                RedContaminationVisual.Sync(ContaminationManager.Zones);

                // Update the night-time glow, lighting the coloured parts of the mothership and
                // the tripods according to the time of day. This keeps working while paused.
                EmissionController.Update(realTimeDelta);

                // The tripod movement sound: one source, never overlapping, silenced while
                // paused. Called every frame even when paused, so that it can stop.
                Vector3 tripodPos;
                bool hasTripod = InvasionManager.TryGetAnyTripodPosition(out tripodPos);
                SoundManager.UpdateTripodAmbience(hasTripod, tripodPos, paused, realTimeDelta);
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("OnUpdate error: " + e);
            }
        }

        public override void OnAfterSimulationTick()
        {
            try
            {
                InvasionManager.UpdateSimulation();
                ProcessContaminationZones();
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("OnAfterSimulationTick error: " + e);
            }
        }

        /// <summary>
        /// The random trigger roll. Main thread only: it uses UnityEngine.Random, which is
        /// main-thread safe, and never touches SimulationManager.instance.m_randomizer, the
        /// RNG belonging to the simulation thread.
        /// </summary>
        private void MaybeRollRandomInvasion()
        {
            long nowTicks = SimulationManager.instance.m_currentGameTime.Ticks;

            // Establish the baseline instead of rolling when there is none (a fresh extension
            // instance, i.e. a level just loaded), or when the clock has moved backwards - which
            // is what loading an earlier save looks like. Rolling in either case would measure
            // against a date that has nothing to do with this city.
            if (_lastRandomCheckTicks == 0 || nowTicks < _lastRandomCheckTicks)
            {
                _lastRandomCheckTicks = nowTicks;
                return;
            }

            if (!RandomInvasionSchedule.IsCheckDue(_lastRandomCheckTicks, nowTicks)) return;
            _lastRandomCheckTicks = nowTicks;

            // Checked after the clock is advanced, so switching the setting on does not inherit a
            // backlog of days that passed while it was off.
            if (!ModSettings.RandomEnabled) return;
            if (InvasionManager.IsActive) return;

            int roll = Mathf.FloorToInt(Random.Range(0f, RandomInvasionSchedule.RollRange));
            if (!RandomInvasionSchedule.ShouldFire(ModSettings.RandomAverageDays, roll)) return;

            const float half = 8500f; // roughly the extent of the map
            float x = Random.Range(-half, half);
            float z = Random.Range(-half, half);
            InvasionManager.StartInvasion(new Vector3(x, 0f, z));
            ModConfig.Log("Random invasion triggered at (" + x + ", " + z + ")");
        }

        /// <summary>
        /// Simulation thread only. ContaminationManager and PollutionField touch nothing but
        /// NaturalResourceManager's plain struct arrays, so calling them here is safe.
        /// Anything that touches a GameObject - RedContaminationVisual and the like - must not
        /// go in here.
        /// </summary>
        private void ProcessContaminationZones()
        {
            if (++_pollutionTickCounter < PollutionProcessInterval) return;
            _pollutionTickCounter = 0;

            List<ContaminationZone> zones = ContaminationManager.Zones;
            if (zones.Count == 0) return;

            long nowTicks = SimulationManager.instance.m_currentGameTime.Ticks;
            for (int i = zones.Count - 1; i >= 0; i--)
            {
                ContaminationZone zone = zones[i];
                if (ExpiryClock.HasExpired(zone.StartTicks, nowTicks, ModConfig.ExpiryMonths))
                {
                    ContaminationManager.ClearZone(zone);
                    ContaminationManager.RemoveZoneAt(i);
                    ModConfig.Log("contamination zone expired (" + ModConfig.ExpiryMonths + "mo) and cleared");
                    continue;
                }
                ContaminationManager.ReassertZone(zone);
            }
        }
    }
}
