using AlienInvasion.Core;
using ColossalFramework;
using UnityEngine;

namespace AlienInvasion.Game
{
    /// <summary>
    /// トライポッド1体のGameObjectと位置/向き。母船(Mothership)と同じく、GameObject操作は
    /// 全てメインスレッド(InvasionManager.UpdateVisual経由)から呼ぶこと。
    /// prefab が未生成(AssetBundle無し)でも Advance/Turn による移動計算自体は継続する
    /// (Position は GameObject の有無に関わらず更新される)。
    /// </summary>
    public class Tripod
    {
        private GameObject _gameObject;
        private float _dirX;
        private float _dirZ;

        public Vector3 Position { get; private set; }

        public Tripod(Vector3 groundPos)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            _dirX = Mathf.Cos(angle);
            _dirZ = Mathf.Sin(angle);

            Position = ClampToGround(groundPos);

            try
            {
                GameObject prefab = AssetLoader.GetPrefab(ModConfig.TripodPrefabName);
                if (prefab != null)
                {
                    _gameObject = Object.Instantiate(prefab);
                    _gameObject.transform.position = Position;
                    _gameObject.transform.localScale = Vector3.one * ModConfig.TripodScale;
                }
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("Tripod ctor error: " + e);
            }
        }

        /// <summary>方向転換。メインスレッド専用。</summary>
        public void Turn(float angleRad)
        {
            float ndx, ndz;
            TripodWalk.Rotate(_dirX, _dirZ, angleRad, out ndx, out ndz);
            _dirX = ndx;
            _dirZ = ndz;
        }

        /// <summary>移動+境界反射+地表追従。メインスレッド専用。</summary>
        public void Advance(float dt)
        {
            try
            {
                float half = ModConfig.TripodMapHalfExtent;

                float nx = TripodWalk.StepComponent(Position.x, _dirX, ModConfig.TripodSpeed, dt);
                float nz = TripodWalk.StepComponent(Position.z, _dirZ, ModConfig.TripodSpeed, dt);

                float bx, bdx, bz, bdz;
                TripodWalk.BounceAxis(nx, _dirX, half, out bx, out bdx);
                TripodWalk.BounceAxis(nz, _dirZ, half, out bz, out bdz);
                _dirX = bdx;
                _dirZ = bdz;

                Position = ClampToGround(new Vector3(bx, Position.y, bz));

                if (_gameObject != null)
                {
                    _gameObject.transform.position = Position;
                }
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("Tripod.Advance error: " + e);
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
