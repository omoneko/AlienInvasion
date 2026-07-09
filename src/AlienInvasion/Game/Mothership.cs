using UnityEngine;

namespace AlienInvasion.Game
{
    /// <summary>母船のGameObjectと位置。GameObject操作は全てメインスレッド(OnUpdate)から呼ぶこと。</summary>
    public class Mothership
    {
        private GameObject _gameObject;
        private readonly Vector3 _targetGround;

        public Vector3 Position { get; private set; }

        public Mothership(Vector3 targetGround)
        {
            _targetGround = targetGround;
            Position = targetGround + new Vector3(0f, ModConfig.MothershipStartAltitude, 0f);
            GameObject prefab = AssetLoader.GetPrefab(ModConfig.MothershipPrefabName);
            if (prefab != null)
            {
                _gameObject = Object.Instantiate(prefab);
                _gameObject.transform.position = Position;
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
