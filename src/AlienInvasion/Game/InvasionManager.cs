using UnityEngine;

namespace AlienInvasion.Game
{
    /// <summary>
    /// 複数の襲来(UFO)を同時進行させる静的コーディネータ。最大 MaxConcurrentInvasions 個の
    /// Invasion を固定長スロット配列で並走させる。
    ///
    /// スレッド境界規律(既存の単一襲来版と同じ思想を配列に拡張):
    /// - スロット配列 _slots の「書き込み(生成・除去・全消去)」は全てメインスレッドのみ(single-writer)。
    ///   StartInvasion / UpdateVisual(完了スロットのnull化) / ResetForNewLevel は全てメインスレッド。
    /// - UpdateSimulation(シミュレーションスレッド)は各スロット参照をローカルに退避してから読むだけ
    ///   (参照代入はアトモ―ミック。メインが同一スロットを null 化しても、退避済み参照を使うため NRE に
    ///   ならず、既に巻き取り済みの Invasion をもう1tick処理するだけの良性レースに収まる)。
    /// したがってロックは不要(書き手が常にメインスレッド1本)。
    /// </summary>
    public static class InvasionManager
    {
        private static readonly Invasion[] _slots = new Invasion[ModConfig.MaxConcurrentInvasions];

        /// <summary>いずれかの襲来が進行中か(ランダム発動の抑制などに使う)。</summary>
        public static bool IsActive
        {
            get
            {
                for (int i = 0; i < _slots.Length; i++)
                {
                    if (_slots[i] != null) return true;
                }
                return false;
            }
        }

        /// <summary>現在進行中の襲来数。</summary>
        public static int ActiveCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _slots.Length; i++)
                {
                    if (_slots[i] != null) n++;
                }
                return n;
            }
        }

        /// <summary>まだ新しい襲来を開始できるか(上限未満か)。</summary>
        public static bool CanStartMore
        {
            get { return ActiveCount < _slots.Length; }
        }

        /// <summary>
        /// メインスレッド専用。空きスロットがあれば新しい襲来を開始する。上限に達している場合は何もしない。
        /// Mothership の生成(Object.Instantiate/transform操作)を伴うため、シミュレーションスレッドから呼ばないこと。
        /// </summary>
        public static void StartInvasion(Vector3 targetPosition)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] == null)
                {
                    _slots[i] = new Invasion(targetPosition);
                    ModConfig.Log("Invasion started at " + targetPosition + " (" + ActiveCount + "/" + _slots.Length + ")");
                    return;
                }
            }
            ModConfig.Log("Invasion request ignored: already at max concurrent (" + _slots.Length + ")");
        }

        /// <summary>メインスレッド専用。全スロットの演出を1フレーム進め、完了したものをスロットから除去する。</summary>
        public static void UpdateVisual(float simTimeDelta)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                Invasion inv = _slots[i];
                if (inv == null) continue;
                bool stillActive = inv.UpdateVisual(simTimeDelta);
                if (!stillActive) _slots[i] = null;
            }
        }

        /// <summary>シミュレーションスレッド専用。各スロットの破壊/汚染書込を進める。</summary>
        public static void UpdateSimulation()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                Invasion inv = _slots[i]; // ローカルに退避(良性レース: 下記クラスコメント参照)
                if (inv == null) continue;
                inv.UpdateSimulation();
            }
        }

        /// <summary>
        /// レベルロード時(InvasionDataExtension.OnLoadData)専用。メインスレッドで呼ばれる。
        /// 別セーブへ切り替える際、旧レベルの静的状態が残留して新レベルへ誤作用するのを防ぐため、
        /// 進行中の全襲来を強制破棄しスロットを空にする。フェーズ1では襲来状態はセーブに永続化されないため、
        /// 再開ではなくリセットが正しい挙動。
        /// </summary>
        public static void ResetForNewLevel()
        {
            try
            {
                for (int i = 0; i < _slots.Length; i++)
                {
                    if (_slots[i] != null)
                    {
                        _slots[i].ForceCleanup();
                        _slots[i] = null;
                    }
                }
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("InvasionManager.ResetForNewLevel error: " + e);
            }
        }
    }
}
