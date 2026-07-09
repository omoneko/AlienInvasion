using UnityEngine;

namespace AlienInvasion.Game
{
    /// <summary>
    /// トライポッド群(3体)の召喚/移動/消滅を統括する静的マネージャ。
    /// Mothership/InvasionManager と同じスレッド境界規律に従う:
    /// Spawn/UpdateVisual/DespawnAll/ResetForNewLevel は全てメインスレッド専用
    /// (GameObject操作・_tripods配列とその要素の Position の書き込み元)。
    /// SnapshotPositions() は読み取り専用アクセサで、Task 5 でシミュレーションスレッドの
    /// UpdateSimulation(ビーム破壊/軌跡汚染)から読まれる想定。書き込み元は常にメインスレッドのみであり、
    /// これは既存の _target/_craterProgress と同じ「良性レース」の許容範囲に従う
    /// (Tripod.Position 自体は不変な Vector3 の差し替えなので、読み取り側が半端な値を見ることはない)。
    /// </summary>
    public static class TripodManager
    {
        private static Tripod[] _tripods;
        private static float _activeElapsed;
        private static float _turnTimer;

        // --- Task 5 seam --------------------------------------------------
        // ビーム破壊(Effects.PlayBeam の描画トリガー用タイマー、およびシミュレーションスレッド側の
        // UpdateSimulation(DisasterHelpers.DestroyStuff / ContaminationManager.AddZone))は
        // Task 5 でここに追加する。本タスクでは移動のみを実装し、破壊/汚染には一切触れない。
        // private static float _beamTimer;           // Task 5: メイン側、Effects.PlayBeam トリガー
        // private static float _beamDestroyTimer;    // Task 5: sim側、DisasterHelpers.DestroyStuff トリガー
        // private static float _trailTimer;          // Task 5: sim側、ContaminationManager.AddZone トリガー
        // public static void UpdateSimulation() { ... } // Task 5: sim スレッド専用、ここでは未実装
        // --------------------------------------------------------------------

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

                _activeElapsed += realTimeDelta;
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("TripodManager.UpdateVisual error: " + e);
            }
        }

        /// <summary>現在の各トライポッド座標のスナップショット(Task 5 のsim読取用)。メイン/sim どちらからでも読める。</summary>
        public static Vector3[] SnapshotPositions()
        {
            if (_tripods == null) return new Vector3[0];
            var result = new Vector3[_tripods.Length];
            for (int i = 0; i < _tripods.Length; i++)
            {
                result[i] = _tripods[i] != null ? _tripods[i].Position : default(Vector3);
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
