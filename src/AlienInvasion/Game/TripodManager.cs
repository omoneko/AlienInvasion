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
        private static int _beamDestroyTickCounter;
        private static int _trailTickCounter;

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
                _beamDestroyTickCounter = 0;
                _trailTickCounter = 0;
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
                        Vector3 pos = _tripods[i].Position;
                        Effects.PlayBeam(pos, pos + Vector3.up * ModConfig.BeamSkyOffset);
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
                Vector3[] positions = SnapshotPositions();
                if (positions.Length == 0) return;

                _beamDestroyTickCounter++;
                if (_beamDestroyTickCounter >= ToApproxTicks(ModConfig.BeamIntervalSeconds))
                {
                    _beamDestroyTickCounter = 0;
                    for (int i = 0; i < positions.Length; i++)
                    {
                        DestroyAt(positions[i]);
                    }
                }

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
                _beamDestroyTickCounter = 0;
                _trailTickCounter = 0;
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
