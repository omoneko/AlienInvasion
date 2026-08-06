using AlienInvasion.Core;
using ColossalFramework;
using UnityEngine;

namespace AlienInvasion.Game
{
    /// <summary>
    /// One tripod: its GameObject, position and heading. As with the mothership, every
    /// GameObject operation must be called from the main thread, via
    /// InvasionManager.UpdateVisual.
    /// The movement maths in Advance and Turn keeps running even when no prefab was created -
    /// without the AssetBundle, say - because Position is updated whether or not a GameObject
    /// exists.
    /// </summary>
    public class Tripod
    {
        private GameObject _gameObject;
        private float _dirX;
        private float _dirZ;
        private float _bobTime;
        private readonly float _bobPhase;

        // --- Toppling from a direct nuclear hit: it falls, lies there, then disappears ---
        private bool _toppling;
        private float _toppleElapsed;
        private Quaternion _toppleFromRot;
        private Quaternion _toppleToRot;
        private Vector3 _toppleBasePos;

        public Vector3 Position { get; private set; }

        /// <summary>Whether it is falling or lying there after a direct nuclear hit. While true it neither walks nor fires.</summary>
        public bool Toppling { get { return _toppling; } }

        /// <summary>Whether the fall and the time lying there are both over, so TripodGroup can destroy it.</summary>
        public bool ToppleFinished { get; private set; }

        public Tripod(Vector3 groundPos)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            _dirX = Mathf.Cos(angle);
            _dirZ = Mathf.Sin(angle);
            _bobPhase = Random.Range(0f, Mathf.PI * 2f);

            Position = ClampToGround(groundPos);

            try
            {
                GameObject go = ModelProvider.CreateInstance(ModConfig.TripodPrefabName);
                if (go != null)
                {
                    _gameObject = go;
                    _gameObject.transform.position = Position;
                    _gameObject.transform.localScale = Vector3.one * ModConfig.TripodScale;
                }
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("Tripod ctor error: " + e);
            }
        }

        /// <summary>Changes direction. Main thread only.</summary>
        public void Turn(float angleRad)
        {
            float ndx, ndz;
            TripodWalk.Rotate(_dirX, _dirZ, angleRad, out ndx, out ndz);
            _dirX = ndx;
            _dirZ = ndz;
        }

        /// <summary>
        /// Starts the fall after a direct nuclear hit. Main thread only, and a no-op if it is
        /// already falling or has fallen. From its current facing it topples
        /// TripodToppleFallAngleDeg degrees about the horizontal axis across its heading.
        /// </summary>
        public void BeginTopple()
        {
            if (_toppling || ToppleFinished) return;
            _toppling = true;
            _toppleElapsed = 0f;
            _toppleBasePos = Position;

            if (_gameObject != null)
            {
                _toppleFromRot = _gameObject.transform.rotation;
                // Fall forward, about the horizontal axis perpendicular to the heading.
                Vector3 axis = new Vector3(-_dirZ, 0f, _dirX);
                if (axis.sqrMagnitude < 1e-6f) axis = Vector3.right;
                _toppleToRot = Quaternion.AngleAxis(ModConfig.TripodToppleFallAngleDeg, axis.normalized) * _toppleFromRot;
            }
            ModConfig.Log("Tripod hit by nuclear strike -> toppling at " + Position);
        }

        /// <summary>Advances the toppling animation by one frame. Main thread only.</summary>
        private void UpdateTopple(float dt)
        {
            _toppleElapsed += dt;

            if (_gameObject != null)
            {
                float f = ToppleAnimation.FallFraction(_toppleElapsed, ModConfig.TripodToppleDurationSeconds);
                _gameObject.transform.rotation = Quaternion.Slerp(_toppleFromRot, _toppleToRot, f);
                // Sink it a little as it falls, so the legs of a fallen tripod are less
                // obviously hanging in the air. Visual only.
                _gameObject.transform.position = _toppleBasePos + Vector3.down * (ModConfig.TripodToppleSink * f);
            }

            if (ToppleAnimation.IsFinished(_toppleElapsed, ModConfig.TripodToppleDurationSeconds, ModConfig.TripodToppleDwellSeconds))
            {
                ToppleFinished = true;
            }
        }

        /// <summary>Movement, reflecting off the map bounds and the water's edge, following the terrain, facing, and the hovering bob. Main thread only.</summary>
        public void Advance(float dt)
        {
            // While falling it does not walk; only the toppling animation advances.
            if (_toppling)
            {
                UpdateTopple(dt);
                return;
            }
            try
            {
                float half = ModConfig.TripodMapHalfExtent;

                float nx = TripodWalk.StepComponent(Position.x, _dirX, ModConfig.TripodSpeed, dt);
                float nz = TripodWalk.StepComponent(Position.z, _dirZ, ModConfig.TripodSpeed, dt);

                if (IsWater(nx, nz))
                {
                    // At the water's edge: if the next position would be over water, turn
                    // back and stay put for this frame. Land, roads included, is not water and
                    // is crossed freely.
                    _dirX = -_dirX;
                    _dirZ = -_dirZ;
                }
                else
                {
                    float bx, bdx, bz, bdz;
                    TripodWalk.BounceAxis(nx, _dirX, half, out bx, out bdx);
                    TripodWalk.BounceAxis(nz, _dirZ, half, out bz, out bdz);
                    _dirX = bdx;
                    _dirZ = bdz;

                    Position = ClampToGround(new Vector3(bx, Position.y, bz));
                }

                UpdateTransform(dt);
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("Tripod.Advance error: " + e);
            }
        }

        /// <summary>Applies the visuals: facing along the heading, plus the hovering bob. Position itself stays on the ground, which is what the simulation thread reads.</summary>
        private void UpdateTransform(float dt)
        {
            if (_gameObject == null) return;

            _bobTime += dt;
            float bob = ModConfig.TripodBobAmplitude *
                        Mathf.Sin(2f * Mathf.PI * ModConfig.TripodBobFreqHz * _bobTime + _bobPhase);
            _gameObject.transform.position = Position + Vector3.up * bob;

            Vector3 heading = new Vector3(_dirX, 0f, _dirZ);
            if (heading.sqrMagnitude > 1e-6f)
            {
                // Point the model's front - Blender's -Y side - along the heading;
                // TripodYawOffsetDeg corrects any discrepancy.
                _gameObject.transform.rotation =
                    Quaternion.LookRotation(heading) * Quaternion.Euler(0f, ModConfig.TripodYawOffsetDeg, 0f);
            }
        }

        /// <summary>
        /// Fires the laser forward and down, at a random depression angle between 20 and 60
        /// degrees. The beam is drawn from the head to the impact point and an explosion plays
        /// there. Returns the impact point in world space, which the caller uses to destroy
        /// buildings on the simulation thread. Main thread only, because it plays effects.
        /// </summary>
        public Vector3 FireBeam()
        {
            try
            {
                Vector3 hd = new Vector3(_dirX, 0f, _dirZ);
                if (hd.sqrMagnitude < 1e-6f) return Position;
                hd.Normalize();

                float angleRad = Random.Range(ModConfig.BeamMinAngleDeg, ModConfig.BeamMaxAngleDeg) * Mathf.Deg2Rad;
                // Fired from the head, TripodHeadHeight above its footing, at the depression
                // angle. The horizontal distance to the ground is height / tan(angle).
                float d = ModConfig.TripodHeadHeight / Mathf.Tan(angleRad);
                if (d > ModConfig.BeamMaxRange) d = ModConfig.BeamMaxRange;

                Vector3 head = Position + Vector3.up * ModConfig.TripodHeadHeight;
                Vector3 flat = new Vector3(Position.x + hd.x * d, 0f, Position.z + hd.z * d);
                Vector3 impact = ClampToGround(flat); // the impact point, on the ground ahead

                Effects.PlayBeam(impact, head);   // draw the beam from the head to the impact
                Effects.PlayExplosion(impact);    // explode at the impact point
                BeamStrikeLog.Record(Position, impact); // published for other mods; CS:WARFRONT reads it to damage units
                return impact;
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("Tripod.FireBeam error: " + e);
                return Position;
            }
        }

        private static bool IsWater(float x, float z)
        {
            try
            {
                return Singleton<TerrainManager>.instance.HasWater(new Vector2(x, z));
            }
            catch (System.Exception)
            {
                return false; // when it cannot be determined, treat it as land so movement is not blocked
            }
        }

        public void Destroy()
        {
            if (_gameObject != null)
            {
                Object.Destroy(_gameObject);
                _gameObject = null;
            }
        }

        private static Vector3 ClampToGround(Vector3 pos)
        {
            try
            {
                float y = Singleton<TerrainManager>.instance.SampleRawHeightSmoothWithWater(pos, false, 0f);
                return new Vector3(pos.x, y, pos.z);
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("Tripod ClampToGround error: " + e);
                return pos;
            }
        }
    }
}
