using AlienInvasion.Core;
using UnityEngine;

namespace AlienInvasion.Game
{
    /// <summary>
    /// 1回の襲来イベントの統括。
    /// StartInvasion / UpdateVisual: いずれもメインスレッド専用(GameObject操作・フェーズタイマー・状態遷移の書き込み元)。
    /// UpdateSimulation: シミュレーションスレッド専用(DisasterHelpers/汚染書込)。
    /// InvasionState/フェーズタイマーは StartInvasion と UpdateVisual からのみ書き込む(single-writer、書き込み元は常にメインスレッド)。
    /// UpdateSimulation は状態を読むのみで書き込まない。
    /// </summary>
    public static class InvasionManager
    {
        private static InvasionState _state = InvasionState.Idle;
        private static Mothership _ship;
        private static Vector3 _target;
        private static float _phaseElapsed;
        private static float _strikeTimer;
        private static float _craterProgress; // 0..1
        private static bool _bombardResolved;  // Bombarding終了時の建物破壊/汚染登録が完了したか

        public static bool IsActive
        {
            get { return _state != InvasionState.Idle; }
        }

        /// <summary>
        /// メインスレッド専用。Mothership の生成(Object.Instantiate/transform操作)と _state の書き込みを行うため、
        /// UpdateVisual と同じスレッド境界規律に従い、シミュレーションスレッドから呼び出してはならない。
        /// </summary>
        public static void StartInvasion(Vector3 targetPosition)
        {
            if (_state != InvasionState.Idle) return;
            _target = targetPosition;
            _ship = new Mothership(targetPosition);
            _state = InvasionState.Descending;
            _phaseElapsed = 0f;
            _strikeTimer = 0f;
            _craterProgress = 0f;
            _bombardResolved = false;
            ModConfig.Log("Invasion started at " + targetPosition);
        }

        public static void UpdateVisual(float realTimeDelta)
        {
            if (_state == InvasionState.Idle) return;
            try
            {
                if (_state == InvasionState.Done)
                {
                    _state = InvasionStateMachine.Next(_state);
                    return;
                }

                _phaseElapsed += realTimeDelta;

                switch (_state)
                {
                    case InvasionState.Descending:
                        UpdateDescending();
                        break;
                    case InvasionState.Bombarding:
                        UpdateBombarding(realTimeDelta);
                        break;
                    case InvasionState.Ascending:
                        UpdateAscending();
                        break;
                }
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("UpdateVisual error: " + e);
            }
        }

        private static void UpdateDescending()
        {
            float t = _phaseElapsed / ModConfig.DescendSeconds;
            float eased = MovementMath.EaseInOut(t);
            float altitude = MovementMath.Lerp(ModConfig.MothershipStartAltitude, ModConfig.MothershipHoverAltitude, eased);
            _ship.SetAltitude(altitude);
            if (t >= 1f)
            {
                _state = InvasionStateMachine.Next(_state);
                _phaseElapsed = 0f;
            }
        }

        private static void UpdateBombarding(float realTimeDelta)
        {
            _ship.SetAltitude(ModConfig.MothershipHoverAltitude);
            _strikeTimer += realTimeDelta;
            if (_strikeTimer >= ModConfig.StrikeIntervalSeconds)
            {
                _strikeTimer = 0f;
                Vector3 groundPoint = _target + new Vector3(
                    Random.Range(-ModConfig.StrikeScatterRadius, ModConfig.StrikeScatterRadius),
                    0f,
                    Random.Range(-ModConfig.StrikeScatterRadius, ModConfig.StrikeScatterRadius));
                Effects.PlayLightningStrike(groundPoint, _ship.SkyPointForBolt());
            }

            float t = _phaseElapsed / ModConfig.BombardSeconds;
            if (t > 1f) t = 1f;
            _craterProgress = t;

            if (_phaseElapsed >= ModConfig.BombardSeconds)
            {
                _state = InvasionStateMachine.Next(_state);
                _phaseElapsed = 0f;
            }
        }

        private static void UpdateAscending()
        {
            float t = _phaseElapsed / ModConfig.AscendSeconds;
            float eased = MovementMath.EaseInOut(t);
            float altitude = MovementMath.Lerp(ModConfig.MothershipHoverAltitude, ModConfig.MothershipStartAltitude, eased);
            _ship.SetAltitude(altitude);
            if (t >= 1f)
            {
                _ship.Destroy();
                _ship = null;
                _state = InvasionStateMachine.Next(_state); // -> Done
            }
        }

        /// <summary>シミュレーションスレッドから毎tick呼ぶ。DisasterHelpers/汚染書込はここでのみ行う。</summary>
        public static void UpdateSimulation()
        {
            try
            {
                if (_state == InvasionState.Bombarding && _craterProgress > 0f)
                {
                    float radius = ModConfig.CraterRadiusMax * _craterProgress;
                    float depth = ModConfig.CraterDepthMax * _craterProgress;
                    DisasterHelpers.MakeCrater(new Vector2(_target.x, _target.z), radius, depth, true);
                }
                else if (_state == InvasionState.Ascending && !_bombardResolved)
                {
                    _bombardResolved = true;
                    ResolveBombardDamage();
                }
                else if (_state == InvasionState.Idle)
                {
                    // 防御的な二重リセット: StartInvasion の一次リセットは既に行われているはずだが、
                    // 万一 UpdateSimulation が次サイクルの StartInvasion 実行前に Idle を観測した場合に備え、
                    // ここでも _bombardResolved を false に揃えておく(現状のロジックでは必須ではない安全策)。
                    _bombardResolved = false;
                }
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("UpdateSimulation error: " + e);
            }
        }

        private static void ResolveBombardDamage()
        {
            int seed = (int)SimulationManager.instance.m_randomizer.Int32(1000000u);
            // preRadius は totalRadius と同じ値にする(0だと何も破壊されないという既知の罠を回避)
            DisasterHelpers.DestroyStuff(seed, null, _target, ModConfig.DestructionRadius, ModConfig.DestructionRadius, 0f,
                ModConfig.DestructionRadius * 0.5f, ModConfig.DestructionRadius, ModConfig.DestructionRadius * 0.3f, ModConfig.DestructionRadius * 0.6f);

            long startTicks = SimulationManager.instance.m_currentGameTime.Ticks;
            var zone = new ContaminationZone(_target.x, _target.z, ModConfig.ContaminationRadius, startTicks);
            ContaminationManager.AddZone(zone);
            ModConfig.Log("Bombardment resolved: crater+destruction+contamination at " + _target);
        }
    }
}
