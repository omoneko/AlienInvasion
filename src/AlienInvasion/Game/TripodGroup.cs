using System.Collections.Generic;
using AlienInvasion.Core;
using UnityEngine;

namespace AlienInvasion.Game
{
    /// <summary>
    /// The group of tripods belonging to one invasion: deploying them, moving them, removing
    /// them, and their laser destruction and contaminated trail. This was once a static
    /// TripodManager; it became an instance so that several invasions can run at once, and
    /// each Invasion now owns one TripodGroup.
    ///
    /// Thread discipline, the same as Mothership and Invasion:
    /// Spawn, UpdateVisual, DespawnAll and ResetForNewLevel are all main thread only - they
    /// work with GameObjects, they are what writes the _tripods array and the Position of its
    /// elements, and they call into Effects.
    /// UpdateSimulation is simulation thread only, doing nothing but DisasterHelpers and
    /// writing contamination.
    /// SnapshotPositions() is a read-only accessor. Writes only ever come from the main thread,
    /// and Tripod.Position is replaced as an immutable Vector3 rather than mutated, so a reader
    /// can never see a half-written value - the race is benign. The queue of destruction
    /// requests is touched from both threads and is protected by a per-instance lock.
    /// </summary>
    public class TripodGroup
    {
        private Tripod[] _tripods;
        private long _spawnGameTicks;   // in-game time (ticks) when they were deployed, used to decide how long they have been active
        private float _turnTimer;

        // The newest impact ID already handled from the Missile mod's beacon. It is rebased at
        // deployment so only impacts after that count as direct hits, which stops a crater that
        // was already there from toppling them the instant they appear.
        private long _nuclearLastId;

        // --- Drawing the beam (main thread only) ---
        private float _beamTimer;

        // --- Beam destruction and the contaminated trail (simulation thread only) ---
        private const float ApproxSimTicksPerSecond = 15f;
        private int _trailTickCounter;

        // Queue of destruction requests at beam impacts. FireBeam fills it on the main thread
        // and UpdateSimulation drains it on the simulation thread.
        private readonly List<Vector3> _destroyQueue = new List<Vector3>();
        private readonly object _queueLock = new object();

        /// <summary>
        /// Whether TripodActiveDays of in-game time have passed since they were deployed.
        /// Because it is measured against the game clock rather than in real seconds, it
        /// stretches with the game speed and does not advance while paused.
        /// </summary>
        public bool IsFinished
        {
            get
            {
                if (_tripods == null) return false;
                long nowTicks = SimulationManager.instance.m_currentGameTime.Ticks;
                return ExpiryClock.HasElapsedDays(_spawnGameTicks, nowTicks, ModConfig.TripodActiveDays);
            }
        }

        /// <summary>Deploys TripodCount tripods scattered around craterCenter. Main thread only.</summary>
        public void Spawn(Vector3 craterCenter)
        {
            try
            {
                _tripods = new Tripod[ModConfig.TripodCount];
                for (int i = 0; i < _tripods.Length; i++)
                {
                    Vector3 pos = craterCenter + new Vector3(
                        Random.Range(-ModConfig.TripodSpawnScatter, ModConfig.TripodSpawnScatter),
                        0f,
                        Random.Range(-ModConfig.TripodSpawnScatter, ModConfig.TripodSpawnScatter));
                    _tripods[i] = new Tripod(pos);
                }
                _spawnGameTicks = SimulationManager.instance.m_currentGameTime.Ticks;
                _turnTimer = 0f;
                _beamTimer = 0f;
                _trailTickCounter = 0;
                // Rebase on the newest nuclear impact ID at deployment, so earlier impacts are
                // ignored. This is 0 when the Missile mod is not installed.
                _nuclearLastId = NuclearImpactReader.CurrentId();
                lock (_queueLock) { _destroyQueue.Clear(); }
                ModConfig.Log("Tripods spawned near " + craterCenter + " (active " + ModConfig.TripodActiveDays + " game-days)");
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("TripodGroup.Spawn error: " + e);
            }
        }

        /// <summary>
        /// Moves every tripod, turns them periodically and fires their beams. Main thread only.
        /// simTimeDelta is the simulation delta that follows the game speed, so the movement,
        /// turning, bobbing and beam interval all stretch with the speed multiplier and stop
        /// entirely while paused.
        /// </summary>
        public void UpdateVisual(float simTimeDelta)
        {
            if (_tripods == null) return;
            try
            {
                // Start the fall for any tripod taking a direct nuclear hit, reading the
                // Missile mod. Main thread.
                ApplyNuclearTopple();

                for (int i = 0; i < _tripods.Length; i++)
                {
                    if (_tripods[i] != null) _tripods[i].Advance(simTimeDelta);
                }

                _turnTimer += simTimeDelta;
                if (_turnTimer >= ModConfig.TripodTurnIntervalSeconds)
                {
                    _turnTimer = 0f;
                    for (int i = 0; i < _tripods.Length; i++)
                    {
                        if (_tripods[i] == null || _tripods[i].Toppling) continue;
                        float deg = Random.Range(-ModConfig.TripodTurnMaxDeg, ModConfig.TripodTurnMaxDeg);
                        _tripods[i].Turn(deg * Mathf.Deg2Rad);
                    }
                }

                _beamTimer += simTimeDelta;
                if (_beamTimer >= ModConfig.BeamIntervalSeconds)
                {
                    _beamTimer = 0f;
                    for (int i = 0; i < _tripods.Length; i++)
                    {
                        if (_tripods[i] == null || _tripods[i].Toppling) continue; // no firing while falling
                        // Fired forward and down. Drawing the beam and the impact explosion
                        // happen on the main thread; the impact point is queued so the
                        // simulation thread destroys the buildings.
                        Vector3 impact = _tripods[i].FireBeam();
                        EnqueueDestroy(impact);
                    }
                }

                // Destroy any tripod that has finished falling and lying there, freeing its slot.
                for (int i = 0; i < _tripods.Length; i++)
                {
                    if (_tripods[i] != null && _tripods[i].ToppleFinished)
                    {
                        _tripods[i].Destroy();
                        _tripods[i] = null;
                    }
                }
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("TripodGroup.UpdateVisual error: " + e);
            }
        }

        /// <summary>
        /// Beam destruction through DisasterHelpers.DestroyStuff and the contaminated trail
        /// through ContaminationManager.AddZone. Simulation thread only: it never writes a
        /// GameObject, a Transform, anything in Effects, or the _tripods array itself.
        /// </summary>
        public void UpdateSimulation()
        {
            try
            {
                DrainDestroyQueue();

                Vector3[] positions = SnapshotPositions();
                if (positions.Length == 0) return;

                _trailTickCounter++;
                if (_trailTickCounter >= ToApproxTicks(ModConfig.TripodTrailContamIntervalSeconds))
                {
                    _trailTickCounter = 0;
                    long nowTicks = SimulationManager.instance.m_currentGameTime.Ticks;
                    for (int i = 0; i < positions.Length; i++)
                    {
                        var zone = new ContaminationZone(positions[i].x, positions[i].z, ModConfig.TripodTrailContamRadius, nowTicks);
                        ContaminationManager.AddZone(zone);
                    }
                }
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("TripodGroup.UpdateSimulation error: " + e);
            }
        }

        /// <summary>
        /// Reads the Missile mod's nuclear impact beacon and starts the fall for every tripod
        /// standing within the direct-hit radius of an impact that happened after they were
        /// deployed. Main thread only, since it rotates GameObjects. Returns immediately when
        /// the Missile mod is not installed.
        /// </summary>
        private void ApplyNuclearTopple()
        {
            if (_tripods == null) return;
            if (!NuclearImpactReader.Available) return;

            // Return before calling Snapshot() when there is nothing new, which avoids
            // allocating an array every frame.
            long current = NuclearImpactReader.CurrentId();
            if (current <= _nuclearLastId) return;

            float[] snap = NuclearImpactReader.Snapshot(); // {id, x, z} triples, newest first
            long maxId = _nuclearLastId;
            for (int s = 0; s + 2 < snap.Length; s += 3)
            {
                long id = (long)snap[s];
                if (id <= _nuclearLastId) break; // newest first, so everything past a handled ID is handled too
                if (id > maxId) maxId = id;

                float ix = snap[s + 1];
                float iz = snap[s + 2];
                for (int t = 0; t < _tripods.Length; t++)
                {
                    Tripod tp = _tripods[t];
                    if (tp == null || tp.Toppling) continue;
                    Vector3 p = tp.Position;
                    if (NuclearHitTest.IsDirectHit(p.x - ix, p.z - iz, ModConfig.NuclearToppleRadius))
                        tp.BeginTopple();
                }
            }
            _nuclearLastId = maxId;
        }

        private void DrainDestroyQueue()
        {
            Vector3[] impacts;
            lock (_queueLock)
            {
                if (_destroyQueue.Count == 0) return;
                impacts = _destroyQueue.ToArray();
                _destroyQueue.Clear();
            }
            for (int i = 0; i < impacts.Length; i++)
            {
                DestroyAt(impacts[i]);
            }
        }

        private void EnqueueDestroy(Vector3 impact)
        {
            lock (_queueLock)
            {
                _destroyQueue.Add(impact);
            }
        }

        private void DestroyAt(Vector3 pos)
        {
            int seed = (int)SimulationManager.instance.m_randomizer.Int32(1000000u);
            // preRadius has to equal totalRadius; passing 0 is the known trap where nothing is
            // destroyed at all.
            DisasterHelpers.DestroyStuff(seed, null, pos, ModConfig.BeamDestroyRadius, ModConfig.BeamDestroyRadius, 0f,
                ModConfig.BeamDestroyRadius * 0.5f, ModConfig.BeamDestroyRadius, ModConfig.BeamDestroyRadius * 0.3f, ModConfig.BeamDestroyRadius * 0.6f);
        }

        private static int ToApproxTicks(float seconds)
        {
            int ticks = Mathf.RoundToInt(seconds * ApproxSimTicksPerSecond);
            return ticks < 1 ? 1 : ticks;
        }

        /// <summary>
        /// A snapshot of the positions of the surviving tripods, for the simulation thread to
        /// read; it is safe to call from either thread. Slots emptied by a direct nuclear hit
        /// are skipped - including them would stamp contamination at the origin.
        /// </summary>
        public Vector3[] SnapshotPositions()
        {
            // Copy the array reference to a local before reading it, to avoid a
            // time-of-check-to-time-of-use race: if DespawnAll on the main thread nulls
            // _tripods between the guard and the element access, this still walks the copied
            // reference and cannot throw a NullReferenceException.
            Tripod[] tripods = _tripods;
            if (tripods == null) return new Vector3[0];
            var result = new List<Vector3>(tripods.Length);
            for (int i = 0; i < tripods.Length; i++)
            {
                Tripod t = tripods[i];
                if (t != null) result.Add(t.Position);
            }
            return result.ToArray();
        }

        /// <summary>The position of the first surviving tripod, used to place the movement sound. False if none are left.</summary>
        public bool TryGetAnyPosition(out Vector3 pos)
        {
            Tripod[] tripods = _tripods;
            if (tripods != null)
            {
                for (int i = 0; i < tripods.Length; i++)
                {
                    if (tripods[i] != null) { pos = tripods[i].Position; return true; }
                }
            }
            pos = default(Vector3);
            return false;
        }

        /// <summary>Destroys every tripod. Main thread only.</summary>
        public void DespawnAll()
        {
            try
            {
                if (_tripods != null)
                {
                    for (int i = 0; i < _tripods.Length; i++)
                    {
                        if (_tripods[i] != null) _tripods[i].Destroy();
                    }
                    _tripods = null;
                }
                _spawnGameTicks = 0L;
                _turnTimer = 0f;
                _beamTimer = 0f;
                _trailTickCounter = 0;
                _nuclearLastId = 0L;
                lock (_queueLock) { _destroyQueue.Clear(); }
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("TripodGroup.DespawnAll error: " + e);
            }
        }
    }
}
