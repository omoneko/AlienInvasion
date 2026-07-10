using System.Collections.Generic;
using AlienInvasion.Core;
using ICities;
using UnityEngine;

namespace AlienInvasion.Game.Simulation
{
    /// <summary>
    /// 襲来の発動・進行・汚染維持を駆動する。
    ///
    /// スレッド設計(Task 11レビューで指摘された危険を踏まえた意図的な変更点):
    /// ブリーフのサンプルコードは MaybeRollRandomInvasion (ランダム発動抽選) を
    /// OnAfterSimulationTick (シミュレーションスレッド) に置き、そこから直接
    /// InvasionManager.StartInvasion を呼ぶ形になっていた。しかし StartInvasion は
    /// 内部で Mothership を new し、Mothership のコンストラクタは
    /// UnityEngine.Object.Instantiate と transform.position の書き込みを行う ---
    /// これらは Unity のメイン/レンダースレッド以外から呼ぶと未定義動作/破損の危険がある
    /// 本物の GameObject/Transform 操作であり、Task 11 の InvasionManager.cs では
    /// StartInvasion は明示的に「メインスレッド専用」とドキュメント化されている。
    /// Cities: Skylines のシミュレーションtickはUnityのレンダースレッドとは別の
    /// バックグラウンドスレッドで走るため、OnAfterSimulationTick から StartInvasion を
    /// 呼ぶことはこの契約に違反し、実際に診断困難な破損/クラッシュを招きうる。
    ///
    /// そのため、このクラスでは:
    /// - OnUpdate (メインスレッド) : 手動キー(F7)判定 と ランダム発動抽選 の両方を行う。
    ///   ランダム抽選はここから直接 InvasionManager.StartInvasion を呼ぶ(メインスレッド)。
    ///   ランダム抽選には SimulationManager.instance.m_randomizer (シミュレーションスレッド用の
    ///   決定論的RNG) ではなく、UnityEngine.Random (メインスレッドセーフ) を用いる。
    ///   この抽選は「どの/いつ 1回限りの演出イベントを開始するか」を決めるだけで、
    ///   セーブに永続化されリプレイ時に再現される必要のある値ではないため、
    ///   フレームベースのUnity RNGで問題ない(他の汚染深刻度ロールのような
    ///   セーブ/リプレイ一致が必要なケースとは異なる)。
    ///   手動キー(F7)は Task 16 でバニラ災害と同じ「狙って左クリックで確定」の操作感に
    ///   統一するため、直接 StartInvasion を呼ばず ToolsModifierControl.SetTool
    ///   で AlienInvasion.Game.UI.MothershipPlacementTool を起動するだけに変更した
    ///   (実際の StartInvasion 呼び出しはそのツールの OnToolGUI 側、これもメインスレッド)。
    ///   UIの「UFO召喚」ボタン(InvasionUI)も同じツールを起動するため、F7とボタンは
    ///   完全に同じ体験になる。
    /// - OnAfterSimulationTick (シミュレーションスレッド) : InvasionManager.UpdateSimulation
    ///   と 汚染ゾーンの維持/期限処理 のみを行う。GameObjectに触れる処理や
    ///   StartInvasion/UpdateVisual/RedContaminationVisual.Sync の呼び出しは一切含まない。
    /// </summary>
    public class InvasionThreadingExtension : ThreadingExtensionBase
    {
        private int _pollutionTickCounter;
        private const int PollutionProcessInterval = 16;

        // ランダム発動チェックの実時間間隔(秒)。
        // ModConfig.RandomCheckIntervalTicks はブリーフ上「シミュレーションtick数」の目安値だが、
        // OnUpdate にはtickインデックスが無いため、簡易な変換として
        // 「RandomCheckIntervalTicks / 100 秒ごとに1回チェックする」という実時間換算で扱う。
        // 正確なタイミングは重要ではなく、周期的にメインスレッド上でチェックされることのみが目的。
        private const float RandomCheckIntervalSeconds = ModConfig.RandomCheckIntervalTicks / 100f;
        private float _randomCheckTimer;

        public override void OnUpdate(float realTimeDelta, float simulationTimeDelta)
        {
            try
            {
                // ゲームが一時停止中は襲来の進行(降下・回転・移動・上下動・ビーム)を凍結する。
                // OnUpdate はレンダースレッドのため一時停止中も realTimeDelta が進み続けるが、
                // それをそのまま使うと停止中でもUFO/トライポッドが動いてしまうため。
                bool paused = SimulationManager.instance.SimulationPaused;

                if (Input.GetKeyDown(ModConfig.ManualTriggerKey) && !InvasionManager.IsActive)
                {
                    // Task 16: F7は即時発動ではなく、UIボタンと同じ配置ツール(狙って左クリックで確定)を
                    // 起動するだけに変更。実際の StartInvasion 呼び出しは
                    // MothershipPlacementTool.OnToolGUI (メインスレッド上のツールUIイベント)側で行う。
                    // (配置は一時停止中でも可能。発動後の進行は解除まで凍結される。)
                    ToolsModifierControl.SetTool<AlienInvasion.Game.UI.MothershipPlacementTool>();
                }

                // 災害パネルは遅延生成されることがあるため、取り付け完了まで毎フレーム試行する。
                UI.InvasionUI.EnsureAttached();

                if (!paused)
                {
                    MaybeRollRandomInvasion(realTimeDelta);
                    InvasionManager.UpdateVisual(realTimeDelta);
                }

                RedContaminationVisual.Sync(ContaminationManager.Zones);
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("OnUpdate error: " + e);
            }
        }

        public override void OnAfterSimulationTick()
        {
            try
            {
                InvasionManager.UpdateSimulation();
                ProcessContaminationZones();
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("OnAfterSimulationTick error: " + e);
            }
        }

        /// <summary>
        /// メインスレッド専用のランダム発動抽選。UnityEngine.Random (メインスレッドセーフ)のみを使用し、
        /// SimulationManager.instance.m_randomizer (シミュレーションスレッド用RNG)には触れない。
        /// </summary>
        private void MaybeRollRandomInvasion(float realTimeDelta)
        {
            if (InvasionManager.IsActive) return;

            _randomCheckTimer += realTimeDelta;
            if (_randomCheckTimer < RandomCheckIntervalSeconds) return;
            _randomCheckTimer = 0f;

            int roll = Mathf.FloorToInt(Random.Range(0f, 10000f));
            if (roll >= ModConfig.RandomChancePer10000) return;

            const float half = 8500f; // マップ範囲の目安
            float x = Random.Range(-half, half);
            float z = Random.Range(-half, half);
            InvasionManager.StartInvasion(new Vector3(x, 0f, z));
            ModConfig.Log("Random invasion triggered at (" + x + ", " + z + ")");
        }

        /// <summary>
        /// シミュレーションスレッド専用。ContaminationManager/PollutionField は
        /// NaturalResourceManager の素の構造体配列のみを触るため、ここでの呼び出しは安全。
        /// GameObjectに触れる処理(RedContaminationVisual等)はここに置かないこと。
        /// </summary>
        private void ProcessContaminationZones()
        {
            if (++_pollutionTickCounter < PollutionProcessInterval) return;
            _pollutionTickCounter = 0;

            List<ContaminationZone> zones = ContaminationManager.Zones;
            if (zones.Count == 0) return;

            long nowTicks = SimulationManager.instance.m_currentGameTime.Ticks;
            for (int i = zones.Count - 1; i >= 0; i--)
            {
                ContaminationZone zone = zones[i];
                if (ExpiryClock.HasExpired(zone.StartTicks, nowTicks, ModConfig.ExpiryMonths))
                {
                    ContaminationManager.ClearZone(zone);
                    ContaminationManager.RemoveZoneAt(i);
                    ModConfig.Log("contamination zone expired (" + ModConfig.ExpiryMonths + "mo) and cleared");
                    continue;
                }
                ContaminationManager.ReassertZone(zone);
            }
        }
    }
}
