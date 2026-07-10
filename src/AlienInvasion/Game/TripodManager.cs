using System.Collections.Generic;
using AlienInvasion.Core;
using UnityEngine;

namespace AlienInvasion.Game
{
    /// <summary>
    /// トライポッド群(3体)の召喚/移動/消滅/レーザー破壊/軌跡汚染を統括する静的マネージャ。
    /// Mothership/InvasionManager と同じスレッド境界規律に従う:
    /// Spawn/UpdateVisual/DespawnAll/ResetForNewLevel は全てメインスレッド専用
    /// (GameObject操作・_tripods配列とその要素の Position の書き込み元・Effects.*呼び出し)。
    /// UpdateSimulation はシミュレーションスレッド専用(DisasterHelpers/汚染書込のみ。
    /// GameObject/Transform/Effects/_tripods配列そのものへの書き込みは一切行わない)。
    /// SnapshotPositions() は読み取り専用アクセサで、UpdateSimulation から読まれる。書き込み元は
    /// 常にメインスレッドのみであり、これは既存の InvasionManager._target と同じ「良性レース」の
    /// 許容範囲に従う(Tripod.Position 自体は不変な Vector3 の差し替えなので、読み取り側が
    /// 半端な値を見ることはない)。
    /// </summary>
    public static class TripodManager
    {
        private static Tripod[] _tripods;
        private static float _activeElapsed;
        private static float _turnTimer;

        // --- ビーム描画(メインスレッド専用) ---
        private static float _beamTimer;

        // --- ビーム破壊/軌跡汚染(シミュレーションスレッド専用) ---
        // OnAfterSimulationTick の呼び出し実時間間隔には正確な保証がない(環境やゲーム速度倍率に
        // よって変動しうる)ため、tickカウンタで間隔を近似する。Cities: Skylines modding で広く
        // 目安とされる概算レート(秒間15tick程度、1倍速時)を用いて ModConfig の *Seconds 定数を
        // 近似tick数に変換する。既存 InvasionThreadingExtension.RandomCheckIntervalSeconds と
        // 同じ割り切りで、正確な実時間一致より「周期的に発火すること」を優先する設計判断。
        // ビーム間隔の厳密なバランス調整は実機で行う(計画書スコープ外)。
        private const float ApproxSimTicksPerSecond = 15f;
        private static int _trailTickCounter;

        // ビーム着弾点の破壊要求キュー。メインスレッド(FireBeam時)が積み、simスレッド(UpdateSimulation)が
        // 消化して DisasterHelpers.DestroyStuff を呼ぶ。両スレッドから触るためロックで保護する。
        private static readonly List<Vector3> _destroyQueue = new List<Vector3>();
        private static readonly object _queueLock = new object();

        public static bool IsFinished
        {
            get { return _activeElapsed >= ModConfig.TripodActiveSeconds; }
        }

        /// <summary>craterCenter 周辺に TripodCount 体を散布生成する。メインスレッド専用。</summary>
        public static void Spawn(Vector3 craterCenter)
        {
            try
            {
                _tripods = new Tripod[ModConfig.TripodCount];
                for (int i = 0; i < _tripods.Length; i++)
                {
                    Vector3 pos = craterCenter + new Vector3(
                        Random.Range(-ModConfig.TripodSpawnScatter, ModConfig.TripodSpawnScatter),
                        0f,
                        Random.Range(-ModConfig.TripodSpawnScatter, ModConfig.TripodSpawnScatter));
                    _tripods[i] = new Tripod(pos);
                }
                _activeElapsed = 0f;
                _turnTimer = 0f;
                _beamTimer = 0f;
                _trailTickCounter = 0;
                lock (_queueLock) { _destroyQueue.Clear(); }
                ModConfig.Log("Tripods spawned near " + craterCenter);
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("TripodManager.Spawn error: " + e);
            }
        }

        /// <summary>全トライポッドの移動+定期方向転換。メインスレッド専用。</summary>
        public static void UpdateVisual(float realTimeDelta)
        {
            if (_tripods == null) return;
            try
            {
                for (int i = 0; i < _tripods.Length; i++)
                {
                    if (_tripods[i] != null) _tripods[i].Advance(realTimeDelta);
                }

                _turnTimer += realTimeDelta;
                if (_turnTimer >= ModConfig.TripodTurnIntervalSeconds)
                {
                    _turnTimer = 0f;
                    for (int i = 0; i < _tripods.Length; i++)
                    {
                        if (_tripods[i] == null) continue;
                        float deg = Random.Range(-ModConfig.TripodTurnMaxDeg, ModConfig.TripodTurnMaxDeg);
                        _tripods[i].Turn(deg * Mathf.Deg2Rad);
                    }
                }

                _beamTimer += realTimeDelta;
                if (_beamTimer >= ModConfig.BeamIntervalSeconds)
                {
                    _beamTimer = 0f;
                    for (int i = 0; i < _tripods.Length; i++)
                    {
                        if (_tripods[i] == null) continue;
                        // 進行方向斜め下へ発射(ビーム描画+着弾爆発はメインスレッド)。
                        // 着弾点を破壊キューへ積み、simスレッドで建物破壊する。
                        Vector3 impact = _tripods[i].FireBeam();
                        EnqueueDestroy(impact);
                    }
                }

                _activeElapsed += realTimeDelta;
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("TripodManager.UpdateVisual error: " + e);
            }
        }

        /// <summary>
        /// ビーム破壊(DisasterHelpers.DestroyStuff)と軌跡汚染(ContaminationManager.AddZone)。
        /// シミュレーションスレッド専用(InvasionManager.UpdateSimulation の TripodsActive 分岐から呼ぶ)。
        /// GameObject/Transform/Effects/_tripods配列そのものへの書き込みは一切行わない。
        /// 座標は SnapshotPositions() 経由でメインスレッドが書いた値を読むだけ(良性レース、クラス冒頭コメント参照)。
        /// </summary>
        public static void UpdateSimulation()
        {
            try
            {
                // 1) ビーム着弾点の破壊要求を消化(メインスレッドが積んだもの)。
                DrainDestroyQueue();

                // 2) 軌跡汚染: トライポッド現在地(接地点)に一定間隔で赤い汚染を残す。
                Vector3[] positions = SnapshotPositions();
                if (positions.Length == 0) return;

                _trailTickCounter++;
                if (_trailTickCounter >= ToApproxTicks(ModConfig.TripodTrailContamIntervalSeconds))
                {
                    _trailTickCounter = 0;
                    long nowTicks = SimulationManager.instance.m_currentGameTime.Ticks;
                    for (int i = 0; i < positions.Length; i++)
                    {
                        var zone = new ContaminationZone(positions[i].x, positions[i].z, ModConfig.TripodTrailContamRadius, nowTicks);
                        ContaminationManager.AddZone(zone);
                    }
                }
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("TripodManager.UpdateSimulation error: " + e);
            }
        }

        /// <summary>メインスレッドが積んだ着弾点の破壊要求をロック下で取り出し、建物を破壊する。simスレッド専用。</summary>
        private static void DrainDestroyQueue()
        {
            Vector3[] impacts;
            lock (_queueLock)
            {
                if (_destroyQueue.Count == 0) return;
                impacts = _destroyQueue.ToArray();
                _destroyQueue.Clear();
            }
            for (int i = 0; i < impacts.Length; i++)
            {
                DestroyAt(impacts[i]);
            }
        }

        /// <summary>着弾点を破壊要求キューへ積む。メインスレッド(FireBeam直後)から呼ぶ。</summary>
        private static void EnqueueDestroy(Vector3 impact)
        {
            lock (_queueLock)
            {
                _destroyQueue.Add(impact);
            }
        }

        private static void DestroyAt(Vector3 pos)
        {
            int seed = (int)SimulationManager.instance.m_randomizer.Int32(1000000u);
            // preRadius は totalRadius と同じ値にする(0だと何も破壊されないという既知の罠を回避。
            // InvasionManager.ResolveBombardDamage と同じ規律)。
            DisasterHelpers.DestroyStuff(seed, null, pos, ModConfig.BeamDestroyRadius, ModConfig.BeamDestroyRadius, 0f,
                ModConfig.BeamDestroyRadius * 0.5f, ModConfig.BeamDestroyRadius, ModConfig.BeamDestroyRadius * 0.3f, ModConfig.BeamDestroyRadius * 0.6f);
        }

        private static int ToApproxTicks(float seconds)
        {
            int ticks = Mathf.RoundToInt(seconds * ApproxSimTicksPerSecond);
            return ticks < 1 ? 1 : ticks;
        }

        /// <summary>現在の各トライポッド座標のスナップショット(UpdateSimulation のsim読取用)。メイン/sim どちらからでも読める。</summary>
        public static Vector3[] SnapshotPositions()
        {
            // ローカルに配列参照を退避してから読む。メインスレッドの DespawnAll が
            // ガード判定と要素アクセスの間に _tripods を null 化しても、退避済み参照を
            // 走査するため NRE にならない(TOCTOU 回避)。
            Tripod[] tripods = _tripods;
            if (tripods == null) return new Vector3[0];
            var result = new Vector3[tripods.Length];
            for (int i = 0; i < tripods.Length; i++)
            {
                result[i] = tripods[i] != null ? tripods[i].Position : default(Vector3);
            }
            return result;
        }

        /// <summary>全トライポッドを破棄する。メインスレッド専用。</summary>
        public static void DespawnAll()
        {
            try
            {
                if (_tripods != null)
                {
                    for (int i = 0; i < _tripods.Length; i++)
                    {
                        if (_tripods[i] != null) _tripods[i].Destroy();
                    }
                    _tripods = null;
                }
                _activeElapsed = 0f;
                _turnTimer = 0f;
                _beamTimer = 0f;
                _trailTickCounter = 0;
                lock (_queueLock) { _destroyQueue.Clear(); }
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("TripodManager.DespawnAll error: " + e);
            }
        }

        /// <summary>レベル再読込時の強制リセット。InvasionManager.ResetForNewLevel から呼ぶ。メインスレッド専用。</summary>
        public static void ResetForNewLevel()
        {
            DespawnAll();
        }
    }
}
