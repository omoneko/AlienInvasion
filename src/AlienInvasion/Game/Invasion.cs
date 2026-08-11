using AlienInvasion.Core;
using UnityEngine;

namespace AlienInvasion.Game
{
    /// <summary>
    /// The whole lifecycle of one invasion - one mothership. This used to be held as a single
    /// invasion by the static InvasionManager; it became an instance so several can run at
    /// once, and InvasionManager now keeps up to MaxConcurrentInvasions of them in slots.
    ///
    /// Thread discipline:
    /// The constructor, UpdateVisual and ForceCleanup are main thread only - they do the
    ///   GameObject work for the Mothership and the TripodGroup, and they are what writes the
    ///   phase timers and the state transitions.
    /// UpdateSimulation is simulation thread only, doing DisasterHelpers and writing
    ///   contamination.
    /// _state is written only by UpdateVisual on the main thread and merely read by
    /// UpdateSimulation, which is a benign race.
    /// </summary>
    public class Invasion
    {
        private InvasionState _state;
        private Mothership _ship;
        private readonly Vector3 _target;
        private float _phaseElapsed;
        private float _strikeTimer;
        private bool _bombardResolved;  // whether the sinkhole, the destruction and the contamination at the end of the bombardment have been applied
        private readonly TripodGroup _tripods = new TripodGroup();

        /// <summary>Main thread only. Creates the Mothership and starts in the Descending state.</summary>
        public Invasion(Vector3 target)
        {
            _target = target;
            _ship = new Mothership(target);
            _state = InvasionState.Descending;
            _phaseElapsed = 0f;
            _strikeTimer = 0f;
            _bombardResolved = false;
            SoundManager.PlayUfoArrival(target); // the arrival sound
        }

        /// <summary>
        /// Main thread only. Advances the invasion by one frame. Returning false means it has
        /// finished - the mothership and the tripods are all destroyed - and the caller can
        /// clear its slot. simTimeDelta is the delta that follows the game speed, so at 2x and
        /// 3x the descent, movement, spin and beam interval all stretch to match, and it is 0
        /// while paused.
        /// </summary>
        public bool UpdateVisual(float simTimeDelta)
        {
            if (_state == InvasionState.Idle || _state == InvasionState.Done) return false;
            try
            {
                _phaseElapsed += simTimeDelta;

                switch (_state)
                {
                    case InvasionState.Descending:
                        UpdateDescending(simTimeDelta);
                        break;
                    case InvasionState.Bombarding:
                        UpdateBombarding(simTimeDelta);
                        break;
                    case InvasionState.Ascending:
                        UpdateAscending(simTimeDelta);
                        break;
                    case InvasionState.TripodDeploy:
                        UpdateTripodDeploy();
                        break;
                    case InvasionState.TripodsActive:
                        UpdateTripodsActive(simTimeDelta);
                        break;
                    case InvasionState.Departing:
                        UpdateDeparting(simTimeDelta);
                        break;
                }
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("Invasion.UpdateVisual error: " + e);
            }
            return _state != InvasionState.Done;
        }

        /// <summary>Simulation thread only. DisasterHelpers and writing contamination happen here and nowhere else.</summary>
        public void UpdateSimulation()
        {
            try
            {
                if (_state == InvasionState.Ascending && !_bombardResolved)
                {
                    _bombardResolved = true;
                    ResolveBombardDamage();
                }
                else if (_state == InvasionState.TripodsActive)
                {
                    _tripods.UpdateSimulation();
                }
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("Invasion.UpdateSimulation error: " + e);
            }
        }

        /// <summary>A representative position of the surviving tripods, if this invasion has them out. Main thread only.</summary>
        public bool TryGetTripodPosition(out Vector3 pos)
        {
            if (_state == InvasionState.TripodsActive)
            {
                return _tripods.TryGetAnyPosition(out pos);
            }
            pos = default(Vector3);
            return false;
        }

        /// <summary>Forced teardown, for a level reload and the like. Main thread only.</summary>
        public void ForceCleanup()
        {
            try
            {
                _tripods.DespawnAll();
                if (_ship != null)
                {
                    _ship.Destroy();
                    _ship = null;
                }
                _state = InvasionState.Done;
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("Invasion.ForceCleanup error: " + e);
            }
        }

        private void UpdateDescending(float simTimeDelta)
        {
            float t = _phaseElapsed / ModConfig.DescendSeconds;
            float eased = MovementMath.EaseInOut(t);
            float altitude = MovementMath.Lerp(ModConfig.MothershipStartAltitude, ModConfig.MothershipHoverAltitude, eased);
            if (_ship != null)
            {
                _ship.SetAltitude(altitude);
                _ship.Spin(simTimeDelta);
            }
            if (t >= 1f)
            {
                _state = InvasionStateMachine.Next(_state);
                _phaseElapsed = 0f;
            }
        }

        private void UpdateBombarding(float simTimeDelta)
        {
            if (_ship != null)
            {
                _ship.SetAltitude(ModConfig.MothershipHoverAltitude);
                _ship.Spin(simTimeDelta);
            }
            _strikeTimer += simTimeDelta;
            if (_strikeTimer >= ModConfig.StrikeIntervalSeconds)
            {
                _strikeTimer = 0f;
                Vector3 groundPoint = _target + new Vector3(
                    Random.Range(-ModConfig.StrikeScatterRadius, ModConfig.StrikeScatterRadius),
                    0f,
                    Random.Range(-ModConfig.StrikeScatterRadius, ModConfig.StrikeScatterRadius));
                Effects.PlayLightningStrike(groundPoint, _ship != null ? _ship.SkyPointForBolt() : groundPoint + Vector3.up * ModConfig.MothershipHoverAltitude);
            }

            // The sinkhole is dug exactly once, by ResolveBombardDamage on the simulation
            // thread when the bombardment ends. Calling MakeCrater every tick would accumulate,
            // because it digs relative to the current ground, and the hole would end up
            // absurdly deep. See ModConfig.
            if (_phaseElapsed >= ModConfig.BombardSeconds)
            {
                _state = InvasionStateMachine.Next(_state);
                _phaseElapsed = 0f;
            }
        }

        /// <summary>
        /// After the bombardment, climbs the mothership from its hovering altitude to the
        /// loitering altitude. It is not destroyed here: it stays overhead for as long as the
        /// tripods are active, and only leaves during the Departing phase.
        /// </summary>
        private void UpdateAscending(float simTimeDelta)
        {
            float t = _phaseElapsed / ModConfig.AscendSeconds;
            float eased = MovementMath.EaseInOut(t);
            float altitude = MovementMath.Lerp(ModConfig.MothershipHoverAltitude, ModConfig.MothershipLingerAltitude, eased);
            if (_ship != null)
            {
                _ship.SetAltitude(altitude);
                _ship.Spin(simTimeDelta);
            }
            if (t >= 1f)
            {
                _state = InvasionStateMachine.Next(_state); // to TripodDeploy; the mothership stays and loiters
                _phaseElapsed = 0f;
            }
        }

        private void UpdateTripodDeploy()
        {
            if (_ship != null) _ship.SetAltitude(ModConfig.MothershipLingerAltitude);
            _tripods.Spawn(_target);
            _state = InvasionStateMachine.Next(_state); // -> TripodsActive
            _phaseElapsed = 0f;
        }

        /// <summary>
        /// Advances the tripods roaming freely, and once TripodActiveDays of in-game time have
        /// passed, removes them all and moves on to Departing. Throughout, the mothership stays
        /// overhead at the loitering altitude, spinning.
        /// </summary>
        private void UpdateTripodsActive(float simTimeDelta)
        {
            if (_ship != null)
            {
                _ship.SetAltitude(ModConfig.MothershipLingerAltitude);
                _ship.Spin(simTimeDelta);
            }
            _tripods.UpdateVisual(simTimeDelta);
            if (_tripods.IsFinished)
            {
                _tripods.DespawnAll();
                _state = InvasionStateMachine.Next(_state); // -> Departing
                _phaseElapsed = 0f;
            }
        }

        /// <summary>
        /// Once the tripods are gone, climbs the mothership from the loitering altitude back to
        /// the spawn altitude, then destroys it and moves on to Done. At Done, UpdateVisual
        /// returns false and InvasionManager clears the slot.
        /// </summary>
        private void UpdateDeparting(float simTimeDelta)
        {
            float t = _phaseElapsed / ModConfig.DepartSeconds;
            float eased = MovementMath.EaseInOut(t);
            float altitude = MovementMath.Lerp(ModConfig.MothershipLingerAltitude, ModConfig.MothershipStartAltitude, eased);
            if (_ship != null)
            {
                _ship.SetAltitude(altitude);
                _ship.Spin(simTimeDelta);
            }
            if (t >= 1f)
            {
                if (_ship != null)
                {
                    _ship.Destroy();
                    _ship = null;
                }
                _state = InvasionState.Done;
                _phaseElapsed = 0f;
            }
        }

        private void ResolveBombardDamage()
        {
            // Dig the sinkhole exactly once, equivalent to a vanilla disaster at scale 5.5,
            // using the same MakeCrater call SinkholeAI makes.
            DisasterHelpers.MakeCrater(new Vector2(_target.x, _target.z), ModConfig.SinkholeRadius, ModConfig.SinkholeDepth, false);

            int seed = (int)SimulationManager.instance.m_randomizer.Int32(1000000u);
            // preRadius has to equal totalRadius; passing 0 is the known trap where nothing is destroyed
            DisasterHelpers.DestroyStuff(seed, null, _target, ModConfig.DestructionRadius, ModConfig.DestructionRadius, 0f,
                ModConfig.DestructionRadius * 0.5f, ModConfig.DestructionRadius, ModConfig.DestructionRadius * 0.3f, ModConfig.DestructionRadius * 0.6f);

            // The crater and the destruction always happen; only the red weed is optional.
            if (ModSettings.ContaminationEnabled)
            {
                long startTicks = SimulationManager.instance.m_currentGameTime.Ticks;
                var zone = new ContaminationZone(_target.x, _target.z, ModConfig.ContaminationRadius, startTicks);
                ContaminationManager.AddZone(zone);
                ModConfig.Log("Bombardment resolved: sinkhole+destruction+contamination at " + _target);
            }
            else
            {
                ModConfig.Log("Bombardment resolved: sinkhole+destruction at " + _target + " (contamination off)");
            }
        }
    }
}
