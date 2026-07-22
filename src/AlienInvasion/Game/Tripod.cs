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
        private float _bobTime;
        private readonly float _bobPhase;

        // --- 核直撃による転倒(倒れ→横たわり→消滅) ---
        private bool _toppling;
        private float _toppleElapsed;
        private Quaternion _toppleFromRot;
        private Quaternion _toppleToRot;
        private Vector3 _toppleBasePos;

        public Vector3 Position { get; private set; }

        /// <summary>核直撃を受けて転倒中(倒れ込み〜横たわり)か。true の間は歩行・ビーム発射をしない。</summary>
        public bool Toppling { get { return _toppling; } }

        /// <summary>転倒＋横たわりを終え、TripodGroup が破棄してよい状態か。</summary>
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

        /// <summary>方向転換。メインスレッド専用。</summary>
        public void Turn(float angleRad)
        {
            float ndx, ndz;
            TripodWalk.Rotate(_dirX, _dirZ, angleRad, out ndx, out ndz);
            _dirX = ndx;
            _dirZ = ndz;
        }

        /// <summary>
        /// 核直撃を受けて転倒を開始する。メインスレッド専用。既に転倒中/転倒済みなら何もしない。
        /// 現在の向きから、進行方向に対して横向きの水平軸まわりに TripodToppleFallAngleDeg 度倒す。
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
                // 進行方向 (dirX,0,dirZ) に直交する水平軸まわりに前へ倒す。
                Vector3 axis = new Vector3(-_dirZ, 0f, _dirX);
                if (axis.sqrMagnitude < 1e-6f) axis = Vector3.right;
                _toppleToRot = Quaternion.AngleAxis(ModConfig.TripodToppleFallAngleDeg, axis.normalized) * _toppleFromRot;
            }
            ModConfig.Log("Tripod hit by nuclear strike -> toppling at " + Position);
        }

        /// <summary>転倒アニメを1フレーム進める。メインスレッド専用。</summary>
        private void UpdateTopple(float dt)
        {
            _toppleElapsed += dt;

            if (_gameObject != null)
            {
                float f = ToppleAnimation.FallFraction(_toppleElapsed, ModConfig.TripodToppleDurationSeconds);
                _gameObject.transform.rotation = Quaternion.Slerp(_toppleFromRot, _toppleToRot, f);
                // 倒れ込むほど少し沈めて、横倒しの脚が宙に浮くのを目立たなくする(見た目のみ)。
                _gameObject.transform.position = _toppleBasePos + Vector3.down * (ModConfig.TripodToppleSink * f);
            }

            if (ToppleAnimation.IsFinished(_toppleElapsed, ModConfig.TripodToppleDurationSeconds, ModConfig.TripodToppleDwellSeconds))
            {
                ToppleFinished = true;
            }
        }

        /// <summary>移動+境界反射+水際反射+地表追従+向き+浮遊上下動。メインスレッド専用。</summary>
        public void Advance(float dt)
        {
            // 転倒中は歩行せず、倒れ込み演出だけを進める。
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
                    // 水際: 次の位置が水上なら引き返す(向き反転)。今フレームは踏みとどまる。
                    // 陸地(道路含む)は水でないため自由に通過できる。
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

        /// <summary>見た目の反映(進行方向への向き＋浮遊上下動)。Positionは地表のまま(sim読取用)。</summary>
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
                // モデル前方(=Blenderの-Y側)を進行方向へ。ズレはTripodYawOffsetDegで微調整。
                _gameObject.transform.rotation =
                    Quaternion.LookRotation(heading) * Quaternion.Euler(0f, ModConfig.TripodYawOffsetDeg, 0f);
            }
        }

        /// <summary>
        /// 進行方向・斜め下(俯角20〜60°ランダム)へレーザーを発射する。頭から着弾点までビームを描画し、
        /// 着弾点で爆発エフェクトを再生する。着弾点(ワールド座標)を返す(呼び出し側がsimスレッドでの
        /// 建物破壊に用いる)。メインスレッド専用(エフェクト再生のため)。
        /// </summary>
        public Vector3 FireBeam()
        {
            try
            {
                Vector3 hd = new Vector3(_dirX, 0f, _dirZ);
                if (hd.sqrMagnitude < 1e-6f) return Position;
                hd.Normalize();

                float angleRad = Random.Range(ModConfig.BeamMinAngleDeg, ModConfig.BeamMaxAngleDeg) * Mathf.Deg2Rad;
                // 頭(接地点からTripodHeadHeight上)から俯角angleで発射。接地までの水平距離 d = 高さ / tan(俯角)。
                float d = ModConfig.TripodHeadHeight / Mathf.Tan(angleRad);
                if (d > ModConfig.BeamMaxRange) d = ModConfig.BeamMaxRange;

                Vector3 head = Position + Vector3.up * ModConfig.TripodHeadHeight;
                Vector3 flat = new Vector3(Position.x + hd.x * d, 0f, Position.z + hd.z * d);
                Vector3 impact = ClampToGround(flat); // 着弾点(前方地表)

                Effects.PlayBeam(impact, head);   // head -> impact のビーム描画
                Effects.PlayExplosion(impact);    // 着弾点で爆発
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
                return false; // 判定不能時は陸扱い(移動を止めない)
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
