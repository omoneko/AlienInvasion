using System.Collections.Generic;
using UnityEngine;

namespace AlienInvasion.Game
{
    /// <summary>
    /// モデルの色付きマテリアル(ベースのメタリックグレー以外)を夜間に発光させる。
    ///
    /// 仕組み: モデル構築時(ObjMeshBuilder)に、ベース以外のマテリアルを自身の色付きで
    /// 発光対象として登録する。毎フレーム(メインスレッド)、現在が夜か(SimulationManager の
    /// m_enableDayNight && m_isNightTime)に応じて発光係数を 0(昼)⇔1(夜) へ滑らかに補間し、
    /// 各マテリアルの _EmissionColor を「登録色 × 係数 × 強度」に設定する。
    ///
    /// マテリアルは ModelProvider がモデル単位でキャッシュ・共有するため、登録はモデル種別ごとに
    /// 1回だけ行われ(インスタンス数に依らない)、ここでの更新で同種の全UFO/トライポッドが一斉に光る。
    /// GameObject/Material に触れるため全てメインスレッド専用。
    /// </summary>
    public static class EmissionController
    {
        private struct Entry
        {
            public Material Mat;
            public Color Color;
        }

        private static readonly List<Entry> _entries = new List<Entry>();
        private static float _current; // 0=昼(消灯) .. 1=夜(発光)

        /// <summary>発光対象マテリアルを登録する(モデル構築時に1回)。メインスレッド専用。</summary>
        public static void Register(Material mat, Color emissionColor)
        {
            if (mat == null) return;
            try
            {
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    mat.SetColor("_EmissionColor", Color.black); // 初期は消灯(昼)
                }
                _entries.Add(new Entry { Mat = mat, Color = emissionColor });
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("EmissionController.Register error: " + e);
            }
        }

        /// <summary>毎フレーム、昼夜に応じて発光を更新する。一時停止中も夜なら光らせたいので pause は無視。メインスレッド専用。</summary>
        public static void Update(float realTimeDelta)
        {
            if (_entries.Count == 0) return;
            try
            {
                float target = IsNight() ? 1f : 0f;
                _current = Mathf.MoveTowards(_current, target, ModConfig.EmissionFadePerSecond * realTimeDelta);
                float k = _current * ModConfig.NightEmissionIntensity;
                for (int i = 0; i < _entries.Count; i++)
                {
                    Material m = _entries[i].Mat;
                    if (m == null) continue;
                    if (m.HasProperty("_EmissionColor"))
                    {
                        Color c = _entries[i].Color;
                        m.SetColor("_EmissionColor", new Color(c.r * k, c.g * k, c.b * k, 1f));
                    }
                }
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("EmissionController.Update error: " + e);
            }
        }

        private static bool IsNight()
        {
            try
            {
                SimulationManager sm = SimulationManager.instance;
                return sm != null && sm.m_enableDayNight && sm.m_isNightTime;
            }
            catch (System.Exception)
            {
                return false;
            }
        }
    }
}
