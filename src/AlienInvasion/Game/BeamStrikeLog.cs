using System;
using System.Collections.Generic;
using UnityEngine;

namespace AlienInvasion.Game
{
    /// <summary>
    /// トライポッドのレーザー発射記録。他MOD（CSWarfront等）がリフレクションで読むための公開API。
    /// GodzillaDisasterのRayStrikeLogと同じ「単調増加ID + float[]スナップショット」パターン。
    ///
    /// レコード形式: 新しい順に {id, startX, startZ, endX, endZ} の5要素×N件。
    /// startはトライポッドの接地点、endは着弾点（どちらもワールドX/Z）。idはプロセス中単調増加
    /// （レベル再読込でもリセットしない＝読む側は「既読ID以下は無視」するだけで済む）。
    ///
    /// スレッド注記: RecordはTripod.FireBeam（メインスレッド）から、CurrentId/Snapshotは
    /// 他MODのsimスレッドから呼ばれうるため、全公開メンバをロックで保護する。
    /// </summary>
    public static class BeamStrikeLog
    {
        private const int MaxKept = 16; // トライポッドは複数体が一定間隔で撃つため、ゴジラより多めに保持

        private static readonly object _lock = new object();
        private static readonly List<float[]> _strikes = new List<float[]>(); // 新しい順
        private static long _currentId;

        /// <summary>最新の発射ID。0は「まだ一度も発射していない」。</summary>
        public static long CurrentId()
        {
            lock (_lock) { return _currentId; }
        }

        /// <summary>発射記録のスナップショット。新しい順に {id, startX, startZ, endX, endZ} ×N。</summary>
        public static float[] Snapshot()
        {
            lock (_lock)
            {
                float[] arr = new float[_strikes.Count * 5];
                for (int i = 0; i < _strikes.Count; i++)
                    Array.Copy(_strikes[i], 0, arr, i * 5, 5);
                return arr;
            }
        }

        /// <summary>レーザー発射を記録する（Tripod.FireBeamから呼ぶ。メインスレッド）。</summary>
        public static void Record(Vector3 from, Vector3 to)
        {
            lock (_lock)
            {
                _currentId++;
                _strikes.Insert(0, new float[] { _currentId, from.x, from.z, to.x, to.z });
                if (_strikes.Count > MaxKept) _strikes.RemoveAt(_strikes.Count - 1);
            }
        }

        /// <summary>レベルロード時（InvasionManager.ResetForNewLevel）。記録は消すがIDは進めたままにする。</summary>
        public static void ResetForNewLevel()
        {
            lock (_lock) { _strikes.Clear(); }
        }
    }
}
