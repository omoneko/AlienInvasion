using System.IO;
using UnityEngine;

namespace AlienInvasion.Game
{
    /// <summary>
    /// 効果音の初期化・再生の単一窓口。Mod.OnEnabled から Initialize を呼ぶ。
    /// 実体のロード/再生は SoundLoaderBehaviour(常駐GameObject)が担う。メインスレッド専用。
    /// </summary>
    public static class SoundManager
    {
        private static SoundLoaderBehaviour _behaviour;

        public static void Initialize(string modDirectory)
        {
            try
            {
                if (_behaviour != null) return;
                string dir = Path.Combine(modDirectory, ModConfig.SoundsFolderName);

                var go = new GameObject("AlienInvasion_SoundManager");
                Object.DontDestroyOnLoad(go);
                _behaviour = go.AddComponent<SoundLoaderBehaviour>();
                _behaviour.BeginLoad(dir);
                ModConfig.Log("SoundManager initialized: " + dir);
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("SoundManager.Initialize error: " + e);
            }
        }

        /// <summary>UFO飛来(襲来開始)時。告知向きに2Dで1回再生。</summary>
        public static void PlayUfoArrival(Vector3 pos)
        {
            if (_behaviour != null) _behaviour.PlayUfoArrival();
        }

        /// <summary>
        /// トライポッド移動音を毎フレーム駆動する(メインスレッド専用)。単一の常設AudioSourceで再生するため
        /// 重ならず、一時停止中は再生も止まる。hasTripod=活動中トライポッドの有無、pos=代表位置、
        /// paused=ゲーム一時停止中か、dt=実時間デルタ。
        /// </summary>
        public static void UpdateTripodAmbience(bool hasTripod, Vector3 pos, bool paused, float dt)
        {
            if (_behaviour != null) _behaviour.UpdateTripodAmbience(hasTripod, pos, paused, dt);
        }
    }
}
