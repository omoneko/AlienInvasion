using AlienInvasion.Core;
using UnityEngine;

namespace AlienInvasion.Game
{
    /// <summary>
    /// 1回の襲来イベント(UFO1体分)の全ライフサイクル。以前は静的 InvasionManager が単一の襲来を
    /// 保持していたが、複数UFOの同時進行を可能にするためインスタンス化した。
    /// InvasionManager が最大 MaxConcurrentInvasions 個の Invasion をスロットで並走させる。
    ///
    /// スレッド境界規律:
    /// ctor / UpdateVisual / ForceCleanup: メインスレッド専用(Mothership/TripodGroupのGameObject操作・
    ///   フェーズタイマー・状態遷移の書き込み元)。
    /// UpdateSimulation: シミュレーションスレッド専用(DisasterHelpers/汚染書込)。
    /// _state は UpdateVisual(メイン)からのみ書き込み、UpdateSimulation は読むのみ(良性レース)。
    /// </summary>
    public class Invasion
    {
        private InvasionState _state;
        private Mothership _ship;
        private readonly Vector3 _target;
        private float _phaseElapsed;
        private float _strikeTimer;
        private bool _bombardResolved;  // Bombarding終了時の陥没/建物破壊/汚染登録が完了したか
        private readonly TripodGroup _tripods = new TripodGroup();

        /// <summary>メインスレッド専用。Mothership を生成し Descending から開始する。</summary>
        public Invasion(Vector3 target)
        {
            _target = target;
            _ship = new Mothership(target);
            _state = InvasionState.Descending;
            _phaseElapsed = 0f;
            _strikeTimer = 0f;
            _bombardResolved = false;
            SoundManager.PlayUfoArrival(target); // UFO飛来音
        }

        /// <summary>
        /// メインスレッド専用。襲来演出を1フレーム進める。戻り値 false は「完了(母船・トライポッド共に
        /// 破棄済み)なので呼び出し側スロットから除去してよい」を意味する。simTimeDelta はゲーム速度連動の
        /// シミュレーションデルタ(2倍/3倍速で降下・移動・回転・ビーム間隔が伸縮し、一時停止中は 0)。
        /// </summary>
        public bool UpdateVisual(float simTimeDelta)
        {
            if (_state == InvasionState.Idle || _state == InvasionState.Done) return false;
            try
            {
                _phaseElapsed += simTimeDelta;

                switch (_state)
                {
                    case InvasionState.Descending:
                        UpdateDescending(simTimeDelta);
                        break;
                    case InvasionState.Bombarding:
                        UpdateBombarding(simTimeDelta);
                        break;
                    case InvasionState.Ascending:
                        UpdateAscending(simTimeDelta);
                        break;
                    case InvasionState.TripodDeploy:
                        UpdateTripodDeploy();
                        break;
                    case InvasionState.TripodsActive:
                        UpdateTripodsActive(simTimeDelta);
                        break;
                    case InvasionState.Departing:
                        UpdateDeparting(simTimeDelta);
                        break;
                }
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("Invasion.UpdateVisual error: " + e);
            }
            return _state != InvasionState.Done;
        }

        /// <summary>シミュレーションスレッド専用。DisasterHelpers/汚染書込はここでのみ行う。</summary>
        public void UpdateSimulation()
        {
            try
            {
                if (_state == InvasionState.Ascending && !_bombardResolved)
                {
                    _bombardResolved = true;
                    ResolveBombardDamage();
                }
                else if (_state == InvasionState.TripodsActive)
                {
                    _tripods.UpdateSimulation();
                }
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("Invasion.UpdateSimulation error: " + e);
            }
        }

        /// <summary>この襲来がトライポッド活動中なら、生存トライポッドの代表位置を返す。メインスレッド専用。</summary>
        public bool TryGetTripodPosition(out Vector3 pos)
        {
            if (_state == InvasionState.TripodsActive)
            {
                return _tripods.TryGetAnyPosition(out pos);
            }
            pos = default(Vector3);
            return false;
        }

        /// <summary>レベル再読込等での強制破棄。メインスレッド専用。</summary>
        public void ForceCleanup()
        {
            try
            {
                _tripods.DespawnAll();
                if (_ship != null)
                {
                    _ship.Destroy();
                    _ship = null;
                }
                _state = InvasionState.Done;
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("Invasion.ForceCleanup error: " + e);
            }
        }

        private void UpdateDescending(float simTimeDelta)
        {
            float t = _phaseElapsed / ModConfig.DescendSeconds;
            float eased = MovementMath.EaseInOut(t);
            float altitude = MovementMath.Lerp(ModConfig.MothershipStartAltitude, ModConfig.MothershipHoverAltitude, eased);
            if (_ship != null)
            {
                _ship.SetAltitude(altitude);
                _ship.Spin(simTimeDelta);
            }
            if (t >= 1f)
            {
                _state = InvasionStateMachine.Next(_state);
                _phaseElapsed = 0f;
            }
        }

        private void UpdateBombarding(float simTimeDelta)
        {
            if (_ship != null)
            {
                _ship.SetAltitude(ModConfig.MothershipHoverAltitude);
                _ship.Spin(simTimeDelta);
            }
            _strikeTimer += simTimeDelta;
            if (_strikeTimer >= ModConfig.StrikeIntervalSeconds)
            {
                _strikeTimer = 0f;
                Vector3 groundPoint = _target + new Vector3(
                    Random.Range(-ModConfig.StrikeScatterRadius, ModConfig.StrikeScatterRadius),
                    0f,
                    Random.Range(-ModConfig.StrikeScatterRadius, ModConfig.StrikeScatterRadius));
                Effects.PlayLightningStrike(groundPoint, _ship != null ? _ship.SkyPointForBolt() : groundPoint + Vector3.up * ModConfig.MothershipHoverAltitude);
            }

            // 陥没穴は Bombarding 終了時に ResolveBombardDamage(simスレッド)で1回だけ形成する
            // (MakeCrater を毎tick呼ぶと相対掘削が累積して異常に深くなるため。ModConfig 参照)。
            if (_phaseElapsed >= ModConfig.BombardSeconds)
            {
                _state = InvasionStateMachine.Next(_state);
                _phaseElapsed = 0f;
            }
        }

        /// <summary>
        /// 爆撃後、母船をホバリング高度から滞留高度まで上昇させる。ここでは母船を破棄せず、
        /// トライポッド活動中ずっと上空に滞留させ続ける(離脱は Departing フェーズで行う)。
        /// </summary>
        private void UpdateAscending(float simTimeDelta)
        {
            float t = _phaseElapsed / ModConfig.AscendSeconds;
            float eased = MovementMath.EaseInOut(t);
            float altitude = MovementMath.Lerp(ModConfig.MothershipHoverAltitude, ModConfig.MothershipLingerAltitude, eased);
            if (_ship != null)
            {
                _ship.SetAltitude(altitude);
                _ship.Spin(simTimeDelta);
            }
            if (t >= 1f)
            {
                _state = InvasionStateMachine.Next(_state); // -> TripodDeploy(母船は破棄せず滞留させ続ける)
                _phaseElapsed = 0f;
            }
        }

        private void UpdateTripodDeploy()
        {
            if (_ship != null) _ship.SetAltitude(ModConfig.MothershipLingerAltitude);
            _tripods.Spawn(_target);
            _state = InvasionStateMachine.Next(_state); // -> TripodsActive
            _phaseElapsed = 0f;
        }

        /// <summary>
        /// トライポッドの自由移動を進め、活動時間(TripodActiveDays 日・ゲーム内時間)を超えたら全体を消滅させて
        /// Departing へ進める。この間、母船は滞留高度で回転しながら上空に留まり続ける。
        /// </summary>
        private void UpdateTripodsActive(float simTimeDelta)
        {
            if (_ship != null)
            {
                _ship.SetAltitude(ModConfig.MothershipLingerAltitude);
                _ship.Spin(simTimeDelta);
            }
            _tripods.UpdateVisual(simTimeDelta);
            if (_tripods.IsFinished)
            {
                _tripods.DespawnAll();
                _state = InvasionStateMachine.Next(_state); // -> Departing
                _phaseElapsed = 0f;
            }
        }

        /// <summary>
        /// トライポッド消滅後、母船を滞留高度から出現高度まで上昇させ、上りきったら破棄して Done へ進める。
        /// Done になると UpdateVisual が false を返し、InvasionManager がスロットから除去する。
        /// </summary>
        private void UpdateDeparting(float simTimeDelta)
        {
            float t = _phaseElapsed / ModConfig.DepartSeconds;
            float eased = MovementMath.EaseInOut(t);
            float altitude = MovementMath.Lerp(ModConfig.MothershipLingerAltitude, ModConfig.MothershipStartAltitude, eased);
            if (_ship != null)
            {
                _ship.SetAltitude(altitude);
                _ship.Spin(simTimeDelta);
            }
            if (t >= 1f)
            {
                if (_ship != null)
                {
                    _ship.Destroy();
                    _ship = null;
                }
                _state = InvasionState.Done;
                _phaseElapsed = 0f;
            }
        }

        private void ResolveBombardDamage()
        {
            // 陥没穴を1回だけ形成する(バニラ災害規模5.5相当。SinkholeAI と同じ MakeCrater 呼び出し)。
            DisasterHelpers.MakeCrater(new Vector2(_target.x, _target.z), ModConfig.SinkholeRadius, ModConfig.SinkholeDepth, false);

            int seed = (int)SimulationManager.instance.m_randomizer.Int32(1000000u);
            // preRadius は totalRadius と同じ値にする(0だと何も破壊されないという既知の罠を回避)
            DisasterHelpers.DestroyStuff(seed, null, _target, ModConfig.DestructionRadius, ModConfig.DestructionRadius, 0f,
                ModConfig.DestructionRadius * 0.5f, ModConfig.DestructionRadius, ModConfig.DestructionRadius * 0.3f, ModConfig.DestructionRadius * 0.6f);

            long startTicks = SimulationManager.instance.m_currentGameTime.Ticks;
            var zone = new ContaminationZone(_target.x, _target.z, ModConfig.ContaminationRadius, startTicks);
            ContaminationManager.AddZone(zone);
            ModConfig.Log("Bombardment resolved: sinkhole+destruction+contamination at " + _target);
        }
    }
}
