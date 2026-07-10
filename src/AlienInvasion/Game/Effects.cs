using ColossalFramework;
using UnityEngine;

namespace AlienInvasion.Game
{
    /// <summary>雷ボルト・着弾閃光・雷鳴の再生。全てメインスレッド(OnUpdate)から呼ぶこと。</summary>
    public static class Effects
    {
        private const float BoltLifetime = 0.15f;
        private static Material _boltMaterial;
        private static RainProperties _rainProperties;
        private static bool _rainPropertiesSearched;

        private const float BeamLifetime = 0.12f;
        private static Material _beamMaterial;

        /// <summary>
        /// LineRenderer 用の発光ライン素材を、CSで実際に使えるシェーダーで生成する。
        /// "Particles/Additive" は CS のランタイムでは除去されていて null になりがちで、その場合
        /// マテリアルが付かず「マゼンタ(ピンク紫)」になる。そこで加算系→スプライト→Unlit の順で
        /// 実在するシェーダーを探し、色は _TintColor / _Color / _EmissionColor / material.color の
        /// どれが有効でも狙った色(青白)になるよう全てに設定する。全滅時のみ Standard(発光しないが
        /// マゼンタは回避)へフォールバックする。メインスレッド専用。
        /// </summary>
        private static Material CreateLineMaterial(Color tint)
        {
            try
            {
                RenderAssets.DumpAvailableShadersOnce();
                Shader shader = RenderAssets.FindFirst(
                    "Particles/Additive", "Particles/Alpha Blended", "Sprites/Default",
                    "Unlit/Transparent", "Unlit/Color");
                if (shader == null) shader = RenderAssets.FindLoadedContaining("additive", "particle", "sprite", "unlit");
                if (shader == null) shader = Shader.Find("Standard"); // 最後の砦(発光しないがマゼンタ回避)
                if (shader == null) return null;

                var mat = new Material(shader);
                if (mat.HasProperty("_TintColor")) mat.SetColor("_TintColor", tint);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", tint);
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", tint);
                mat.color = tint;
                ModConfig.Log("Effects: line material shader = " + shader.name);
                return mat;
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("Effects.CreateLineMaterial error: " + e);
                return null;
            }
        }

        public static void PlayLightningStrike(Vector3 groundPoint, Vector3 skyPoint)
        {
            try
            {
                SpawnBolt(groundPoint, skyPoint);
                PlayImpactBurst(groundPoint);
                PlayThunderSound(groundPoint);
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("PlayLightningStrike error: " + e);
            }
        }

        private static void SpawnBolt(Vector3 groundPoint, Vector3 skyPoint)
        {
            if (_boltMaterial == null)
            {
                _boltMaterial = CreateLineMaterial(ModConfig.BoltColor);
            }

            var go = new GameObject("AlienInvasion_LightningBolt");
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            if (_boltMaterial != null) line.material = _boltMaterial;
            line.startWidth = 4f;
            line.endWidth = 1.5f;
            Color bolt = ModConfig.BoltColor;
            line.startColor = new Color(bolt.r, bolt.g, bolt.b, 1f);
            line.endColor = new Color(bolt.r, bolt.g, bolt.b, 0.6f);

            const int segments = 6;
            line.positionCount = segments + 1;
            Vector3 dir = (groundPoint - skyPoint);
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                Vector3 basePos = skyPoint + dir * t;
                if (i != 0 && i != segments)
                {
                    basePos.x += Random.Range(-8f, 8f);
                    basePos.z += Random.Range(-8f, 8f);
                }
                line.SetPosition(i, basePos);
            }

            Object.Destroy(go, BoltLifetime);
        }

        /// <summary>
        /// トライポッドのレーザービームを一瞬描画する(赤・細身)。既存 PlayLightningStrike の
        /// LineRenderer/マテリアルキャッシュ手法を流用。メインスレッド専用(GameObject操作のため)。
        /// </summary>
        public static void PlayBeam(Vector3 groundPoint, Vector3 from)
        {
            try
            {
                SpawnBeam(groundPoint, from);
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("PlayBeam error: " + e);
            }
        }

        private static void SpawnBeam(Vector3 groundPoint, Vector3 from)
        {
            if (_beamMaterial == null)
            {
                _beamMaterial = CreateLineMaterial(ModConfig.BeamColor);
            }

            var go = new GameObject("AlienInvasion_TripodBeam");
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            if (_beamMaterial != null) line.material = _beamMaterial;
            line.startWidth = 1.5f;
            line.endWidth = 0.5f;

            Color beamColor = ModConfig.BeamColor;
            line.startColor = new Color(beamColor.r, beamColor.g, beamColor.b, 1f);
            line.endColor = new Color(beamColor.r, beamColor.g, beamColor.b, 0.6f);

            line.positionCount = 2;
            line.SetPosition(0, from);
            line.SetPosition(1, groundPoint);

            Object.Destroy(go, BeamLifetime);
        }

        /// <summary>着弾点で爆発(隕石着弾エフェクト)を再生する。メインスレッド専用。</summary>
        public static void PlayExplosion(Vector3 position)
        {
            try
            {
                PlayImpactBurst(position);
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("PlayExplosion error: " + e);
            }
        }

        private static void PlayImpactBurst(Vector3 position)
        {
            EffectInfo effect = ResolveImpactEffect();
            if (effect == null) return;
            var spawnArea = new EffectInfo.SpawnArea(position, Vector3.up, 0f);
            Singleton<EffectManager>.instance.DispatchEffect(
                effect, default(InstanceID), spawnArea, Vector3.zero, 0f, ModConfig.ImpactEffectMagnitude,
                Singleton<VehicleManager>.instance.m_audioGroup);
        }

        /// <summary>
        /// 着弾爆発に使うエフェクトを解決する。既定はゲーム標準の中規模爆発(DisasterProperties.m_mediumExplosion。
        /// 隕石着弾より遥かに小さく着弾向き)。取得できない場合は隕石着弾エフェクトへフォールバック。
        /// </summary>
        private static EffectInfo ResolveImpactEffect()
        {
            try
            {
                DisasterProperties dp = Singleton<DisasterManager>.instance.m_properties;
                if (dp != null && dp.m_mediumExplosion != null) return dp.m_mediumExplosion;
            }
            catch (System.Exception)
            {
                // フォールバックへ
            }
            return ResolveMeteorImpactEffect();
        }

        private static EffectInfo ResolveMeteorImpactEffect()
        {
            int count = PrefabCollection<VehicleInfo>.LoadedCount();
            for (int i = 0; i < count; i++)
            {
                VehicleInfo info = PrefabCollection<VehicleInfo>.GetLoaded((uint)i);
                if (info == null) continue;
                MeteorAI ai = info.m_vehicleAI as MeteorAI;
                if (ai != null && ai.m_impactEffect != null) return ai.m_impactEffect;
            }
            return null;
        }

        private static void PlayThunderSound(Vector3 position)
        {
            if (!_rainPropertiesSearched)
            {
                _rainPropertiesSearched = true;
                _rainProperties = Object.FindObjectOfType<RainProperties>();
            }
            if (_rainProperties == null || _rainProperties.m_ThunderSound == null) return;
            Singleton<AudioManager>.instance.AddEvent(
                Singleton<AudioManager>.instance.AmbientGroup, _rainProperties.m_ThunderSound,
                position, Vector3.zero, 200f, 1f, 1f);
        }
    }
}
