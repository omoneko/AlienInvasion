using UnityEngine;

namespace AlienInvasion.Game
{
    /// <summary>The mothership's GameObject and position. Every GameObject operation must be called from the main thread, via OnUpdate.</summary>
    public class Mothership
    {
        private GameObject _gameObject;
        private readonly Vector3 _targetGround;

        public Vector3 Position { get; private set; }

        public Mothership(Vector3 targetGround)
        {
            _targetGround = targetGround;
            Position = targetGround + new Vector3(0f, ModConfig.MothershipStartAltitude, 0f);
            GameObject go = ModelProvider.CreateInstance(ModConfig.MothershipPrefabName);
            if (go != null)
            {
                _gameObject = go;
                _gameObject.transform.position = Position;
                _gameObject.transform.localScale = Vector3.one * ModConfig.MothershipScale;
            }
        }

        /// <summary>Spins the mothership slowly about the world Y axis. Main thread only, and null-safe.</summary>
        public void Spin(float dt)
        {
            if (_gameObject != null)
            {
                _gameObject.transform.Rotate(0f, ModConfig.MothershipSpinDegPerSec * dt, 0f, Space.World);
            }
        }

        public void SetAltitude(float altitudeAboveTarget)
        {
            Position = _targetGround + new Vector3(0f, altitudeAboveTarget, 0f);
            if (_gameObject != null) _gameObject.transform.position = Position;
        }

        public Vector3 SkyPointForBolt()
        {
            return Position;
        }

        public void Destroy()
        {
            if (_gameObject != null)
            {
                Object.Destroy(_gameObject);
                _gameObject = null;
            }
        }
    }
}
