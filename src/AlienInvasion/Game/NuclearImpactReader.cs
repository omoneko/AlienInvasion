using System;
using System.Reflection;

namespace AlienInvasion.Game
{
    /// <summary>
    /// Missile Disaster Mod の核着弾ビーコン（MissileDisaster.Game.NuclearImpactBeacon）を
    /// リフレクションで読む疎結合ブリッジ。両Modは相互にDLL参照しないため、型を AppDomain から
    /// 名前で探し、公開契約の CurrentId プロパティと Snapshot() メソッドだけを呼ぶ。
    /// Missile MOD が未導入なら Available=false となり、核直撃転倒の機能はまるごと無効化される。
    ///
    /// 解決は初回アクセス時に一度だけ行う（トライポッド活動中に呼ばれる頃には全Modの
    /// アセンブリはロード済みのため、片方向きの検出で十分）。全メソッドはメインスレッドから呼ぶ。
    /// </summary>
    public static class NuclearImpactReader
    {
        private const string BeaconTypeName = "MissileDisaster.Game.NuclearImpactBeacon";
        private static readonly float[] Empty = new float[0];

        private static bool _resolved;
        private static bool _available;
        private static MethodInfo _snapshot;
        private static PropertyInfo _currentId;

        /// <summary>Missile MOD の核着弾ビーコンが利用可能か（未導入なら false）。</summary>
        public static bool Available
        {
            get { Resolve(); return _available; }
        }

        /// <summary>直近に発行された核着弾ID（0=まだ無し／未導入）。安価な新着有無チェック用。</summary>
        public static long CurrentId()
        {
            Resolve();
            if (!_available) return 0L;
            try
            {
                object v = _currentId.GetValue(null, null);
                return v is long ? (long)v : Convert.ToInt64(v);
            }
            catch (Exception e)
            {
                ModConfig.LogError("NuclearImpactReader.CurrentId error: " + e);
                _available = false;
                return 0L;
            }
        }

        /// <summary>直近の核着弾を新しい順に {id, x, z} の三つ組で返す（未導入/無ければ空配列）。</summary>
        public static float[] Snapshot()
        {
            Resolve();
            if (!_available) return Empty;
            try
            {
                object v = _snapshot.Invoke(null, null);
                float[] arr = v as float[];
                return arr ?? Empty;
            }
            catch (Exception e)
            {
                ModConfig.LogError("NuclearImpactReader.Snapshot error: " + e);
                _available = false;
                return Empty;
            }
        }

        private static void Resolve()
        {
            if (_resolved) return;
            _resolved = true;
            try
            {
                Type t = FindType(BeaconTypeName);
                if (t == null) { _available = false; return; }

                _snapshot = t.GetMethod("Snapshot", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
                _currentId = t.GetProperty("CurrentId", BindingFlags.Public | BindingFlags.Static);
                _available = _snapshot != null && _currentId != null;

                if (_available)
                    ModConfig.Log("Missile Disaster Mod を検出: 核直撃によるトライポッド転倒を有効化");
                else
                    ModConfig.Log("NuclearImpactBeacon は見つかったが契約メンバが不一致のため無効化");
            }
            catch (Exception e)
            {
                _available = false;
                ModConfig.LogError("NuclearImpactReader.Resolve error: " + e);
            }
        }

        private static Type FindType(string fullName)
        {
            Assembly[] asms = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < asms.Length; i++)
            {
                try
                {
                    Type t = asms[i].GetType(fullName, false);
                    if (t != null) return t;
                }
                catch { /* 動的アセンブリ等は無視 */ }
            }
            return null;
        }
    }
}
