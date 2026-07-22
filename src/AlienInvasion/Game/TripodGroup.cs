using System.Collections.Generic;
using AlienInvasion.Core;
using UnityEngine;

namespace AlienInvasion.Game
{
    /// <summary>
    /// 1回の襲来に属するトライポッド群(TripodCount体)の召喚/移動/消滅/レーザー破壊/軌跡汚染。
    /// 複数の襲来(UFO)を同時進行させるため、以前の静的 TripodManager をインスタンス化したもの。
    /// 各 Invasion が自分専用の TripodGroup を1つ保持する。
    ///
    /// スレッド境界規律(Mothership/Invasion と同じ):
    /// Spawn/UpdateVisual/DespawnAll/ResetForNewLevel は全てメインスレッド専用
    /// (GameObject操作・_tripods配列とその要素の Position の書き込み元・Effects.*呼び出し)。
    /// UpdateSimulation はシミュレーションスレッド専用(DisasterHelpers/汚染書込のみ)。
    /// SnapshotPositions() は読み取り専用アクセサ。書き込み元は常にメインスレッドのみで、
    /// Tripod.Position 自体は不変な Vector3 の差し替えなので読み取り側が半端な値を見ることはない
    /// (良性レース)。破壊要求キューは両スレッドから触るためインスタンス毎のロックで保護する。
    /// </summary>
    public class TripodGroup
    {
        private Tripod[] _tripods;
        private long _spawnGameTicks;   // 召喚時のゲーム内時刻(Ticks)。活動時間の判定に使う。
        private float _turnTimer;

        // 核着弾ビーコン(Missile MOD)から処理済みの最新着弾ID。召喚時点の値を基準にし、
        // それ以降の核着弾だけを直撃判定の対象にする(召喚前のクレーターで即転倒しないように)。
        private long _nuclearLastId;

        // --- ビーム描画(メインスレッド専用) ---
        private float _beamTimer;

        // --- ビーム破壊/軌跡汚染(シミュレーションスレッド専用) ---
        private const float ApproxSimTicksPerSecond = 15f;
        private int _trailTickCounter;

        // ビーム着弾点の破壊要求キュー。メイン(FireBeam時)が積み、sim(UpdateSimulation)が消化する。
        private readonly List<Vector3> _destroyQueue = new List<Vector3>();
        private readonly object _queueLock = new object();

        /// <summary>
        /// 召喚から TripodActiveDays 日(ゲーム内時間)が経過したか。実時間の秒ではなくゲーム時刻で
        /// 判定するため、ゲーム速度倍率で伸縮し、一時停止中は進まない(呼び出しは!paused時のメインスレッド)。
        /// </summary>
        public bool IsFinished
        {
            get
            {
                if (_tripods == null) return false;
                long nowTicks = SimulationManager.instance.m_currentGameTime.Ticks;
                return ExpiryClock.HasElapsedDays(_spawnGameTicks, nowTicks, ModConfig.TripodActiveDays);
            }
        }

        /// <summary>craterCenter 周辺に TripodCount 体を散布生成する。メインスレッド専用。</summary>
        public void Spawn(Vector3 craterCenter)
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
                _spawnGameTicks = SimulationManager.instance.m_currentGameTime.Ticks;
                _turnTimer = 0f;
                _beamTimer = 0f;
                _trailTickCounter = 0;
                // 召喚時点の最新核着弾IDを基準化(これ以前の着弾は無視)。Missile MOD未導入なら 0。
                _nuclearLastId = NuclearImpactReader.CurrentId();
                lock (_queueLock) { _destroyQueue.Clear(); }
                ModConfig.Log("Tripods spawned near " + craterCenter + " (active " + ModConfig.TripodActiveDays + " game-days)");
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("TripodGroup.Spawn error: " + e);
            }
        }

        /// <summary>
        /// 全トライポッドの移動+定期方向転換+ビーム発射。メインスレッド専用。
        /// simTimeDelta はゲーム速度連動のシミュレーションデルタ(移動・旋回・上下動・ビーム間隔が
        /// ゲーム速度倍率で伸縮し、一時停止中は 0)。
        /// </summary>
        public void UpdateVisual(float simTimeDelta)
        {
            if (_tripods == null) return;
            try
            {
                // 核直撃を受けたトライポッドを転倒開始させる(Missile MOD連携・メインスレッド)。
                ApplyNuclearTopple();

                for (int i = 0; i < _tripods.Length; i++)
                {
                    if (_tripods[i] != null) _tripods[i].Advance(simTimeDelta);
                }

                _turnTimer += simTimeDelta;
                if (_turnTimer >= ModConfig.TripodTurnIntervalSeconds)
                {
                    _turnTimer = 0f;
                    for (int i = 0; i < _tripods.Length; i++)
                    {
                        if (_tripods[i] == null || _tripods[i].Toppling) continue;
                        float deg = Random.Range(-ModConfig.TripodTurnMaxDeg, ModConfig.TripodTurnMaxDeg);
                        _tripods[i].Turn(deg * Mathf.Deg2Rad);
                    }
                }

                _beamTimer += simTimeDelta;
                if (_beamTimer >= ModConfig.BeamIntervalSeconds)
                {
                    _beamTimer = 0f;
                    for (int i = 0; i < _tripods.Length; i++)
                    {
                        if (_tripods[i] == null || _tripods[i].Toppling) continue; // 転倒中は発射しない
                        // 進行方向斜め下へ発射(ビーム描画+着弾爆発はメインスレッド)。
                        // 着弾点を破壊キューへ積み、simスレッドで建物破壊する。
                        Vector3 impact = _tripods[i].FireBeam();
                        EnqueueDestroy(impact);
                    }
                }

                // 転倒＋横たわりを終えたトライポッドを破棄してスロットを空ける。
                for (int i = 0; i < _tripods.Length; i++)
                {
                    if (_tripods[i] != null && _tripods[i].ToppleFinished)
                    {
                        _tripods[i].Destroy();
                        _tripods[i] = null;
                    }
                }
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("TripodGroup.UpdateVisual error: " + e);
            }
        }

        /// <summary>
        /// ビーム破壊(DisasterHelpers.DestroyStuff)と軌跡汚染(ContaminationManager.AddZone)。
        /// シミュレーションスレッド専用。GameObject/Transform/Effects/_tripods配列そのものへの書き込みは一切行わない。
        /// </summary>
        public void UpdateSimulation()
        {
            try
            {
                DrainDestroyQueue();

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
                ModConfig.LogError("TripodGroup.UpdateSimulation error: " + e);
            }
        }

        /// <summary>
        /// Missile MOD の核着弾ビーコンを読み、召喚後に発生した核着弾の直撃半径内にいる各トライポッドの
        /// 転倒を開始させる。メインスレッド専用(GameObject回転を伴うため)。Missile MOD未導入なら即return。
        /// </summary>
        private void ApplyNuclearTopple()
        {
            if (_tripods == null) return;
            if (!NuclearImpactReader.Available) return;

            // 新着が無ければ Snapshot() を呼ばず即return(毎フレームの配列確保を避ける)。
            long current = NuclearImpactReader.CurrentId();
            if (current <= _nuclearLastId) return;

            float[] snap = NuclearImpactReader.Snapshot(); // 新しい順に {id, x, z} の三つ組
            long maxId = _nuclearLastId;
            for (int s = 0; s + 2 < snap.Length; s += 3)
            {
                long id = (long)snap[s];
                if (id <= _nuclearLastId) break; // 新しい順なので、既処理IDに達したら以降も既処理
                if (id > maxId) maxId = id;

                float ix = snap[s + 1];
                float iz = snap[s + 2];
                for (int t = 0; t < _tripods.Length; t++)
                {
                    Tripod tp = _tripods[t];
                    if (tp == null || tp.Toppling) continue;
                    Vector3 p = tp.Position;
                    if (NuclearHitTest.IsDirectHit(p.x - ix, p.z - iz, ModConfig.NuclearToppleRadius))
                        tp.BeginTopple();
                }
            }
            _nuclearLastId = maxId;
        }

        private void DrainDestroyQueue()
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

        private void EnqueueDestroy(Vector3 impact)
        {
            lock (_queueLock)
            {
                _destroyQueue.Add(impact);
            }
        }

        private void DestroyAt(Vector3 pos)
        {
            int seed = (int)SimulationManager.instance.m_randomizer.Int32(1000000u);
            // preRadius は totalRadius と同じ値にする(0だと何も破壊されないという既知の罠を回避)。
            DisasterHelpers.DestroyStuff(seed, null, pos, ModConfig.BeamDestroyRadius, ModConfig.BeamDestroyRadius, 0f,
                ModConfig.BeamDestroyRadius * 0.5f, ModConfig.BeamDestroyRadius, ModConfig.BeamDestroyRadius * 0.3f, ModConfig.BeamDestroyRadius * 0.6f);
        }

        private static int ToApproxTicks(float seconds)
        {
            int ticks = Mathf.RoundToInt(seconds * ApproxSimTicksPerSecond);
            return ticks < 1 ? 1 : ticks;
        }

        /// <summary>
        /// 生存中の各トライポッド座標のスナップショット(sim読取用)。メイン/sim どちらからでも読める。
        /// 核直撃で個別に破棄(null化)された枠は除外する(除外しないと原点(0,0,0)に汚染を撒いてしまう)。
        /// </summary>
        public Vector3[] SnapshotPositions()
        {
            // ローカルに配列参照を退避してから読む(TOCTOU回避)。メインスレッドの DespawnAll が
            // ガード判定と要素アクセスの間に _tripods を null 化しても、退避済み参照を走査するため NRE にならない。
            Tripod[] tripods = _tripods;
            if (tripods == null) return new Vector3[0];
            var result = new List<Vector3>(tripods.Length);
            for (int i = 0; i < tripods.Length; i++)
            {
                Tripod t = tripods[i];
                if (t != null) result.Add(t.Position);
            }
            return result.ToArray();
        }

        /// <summary>生存している先頭トライポッドの位置を返す(移動音の発生源に使う)。1体もいなければ false。</summary>
        public bool TryGetAnyPosition(out Vector3 pos)
        {
            Tripod[] tripods = _tripods;
            if (tripods != null)
            {
                for (int i = 0; i < tripods.Length; i++)
                {
                    if (tripods[i] != null) { pos = tripods[i].Position; return true; }
                }
            }
            pos = default(Vector3);
            return false;
        }

        /// <summary>全トライポッドを破棄する。メインスレッド専用。</summary>
        public void DespawnAll()
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
                _spawnGameTicks = 0L;
                _turnTimer = 0f;
                _beamTimer = 0f;
                _trailTickCounter = 0;
                _nuclearLastId = 0L;
                lock (_queueLock) { _destroyQueue.Clear(); }
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("TripodGroup.DespawnAll error: " + e);
            }
        }
    }
}
