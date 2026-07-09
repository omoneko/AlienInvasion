using ColossalFramework.UI;
using UnityEngine;

namespace AlienInvasion.Game.UI
{
    /// <summary>
    /// UFO召喚ボタンの生成/破棄。
    ///
    /// 配置方針(ユーザ選択: 「災害タブ横に独立ボタン」):
    /// バニラの災害パネル DisastersPanel のルート(component)直下に独立した子ボタンとして貼る。
    /// - DisastersPanel は GeneratedScrollPanel 派生で、タブを開く度に RefreshPanel が
    ///   内部の m_ScrollablePanel(スクロール可能な災害アイコン列)だけを再生成する。
    ///   本ボタンはその m_ScrollablePanel の中には入れず、パネルのルート直下に置くため
    ///   再生成に巻き込まれず安定する。
    /// - パネルのルートの子なので、災害グループを開くと自動的に一緒に表示され、
    ///   別タブに切り替えると自動的に隠れる(手動の表示制御が不要)。
    ///
    /// DisastersPanel は災害グループを初めて開くまで生成されない場合があるため、
    /// EnsureAttached() を毎フレーム(メインスレッド)呼んでパネル出現を待って取り付ける。
    /// 一定フレーム経っても見つからない場合(例: Natural Disasters DLC 未所持)は、
    /// フォールバックとして画面左上に常時ボタンを出す(機能を失わせない)。
    ///
    /// 静的状態はレベルロード毎に InvasionLoadingExtension が CreateButton/DestroyButton を
    /// 呼んで張り直す前提。Task 11レビューの「静的状態がレベルをまたいで残留する」不具合を
    /// 避けるため、DestroyButton で必ず参照を破棄・null化・フラグをリセットすること。
    /// </summary>
    public static class InvasionUI
    {
        private const string ButtonName = "AlienInvasionSummonButton";

        private static UIButton _button;
        private static bool _attached;      // 災害パネルへの取り付け(またはフォールバック生成)が完了したか
        private static int _waitFrames;     // 災害パネルを待っているフレーム数

        /// <summary>レベルロード時に呼ぶ。まだ災害パネルが無ければ EnsureAttached が後で拾う。</summary>
        public static void CreateButton()
        {
            _attached = false;
            _waitFrames = 0;
            TryAttachToDisasterPanel();
        }

        /// <summary>
        /// OnUpdate(メインスレッド)から毎フレーム呼ぶ。未取り付けの間だけ動作し、
        /// 災害パネルの出現を待って取り付ける。猶予フレームを超えたらフォールバックボタンを出す。
        /// </summary>
        public static void EnsureAttached()
        {
            if (_attached) return;

            TryAttachToDisasterPanel();
            if (_attached) return;

            if (++_waitFrames >= ModConfig.SummonButtonFallbackFrames)
            {
                CreateFallbackButton();
            }
        }

        private static void TryAttachToDisasterPanel()
        {
            try
            {
                DisastersPanel panel = Object.FindObjectOfType<DisastersPanel>();
                if (panel == null || panel.component == null) return;

                UIComponent host = panel.component;

                // 既に同名ボタンがあれば二重生成しない
                if (host.Find<UIButton>(ButtonName) != null)
                {
                    _attached = true;
                    return;
                }

                UIButton button = host.AddUIComponent<UIButton>();
                StyleButton(button);

                // パネル右端に右寄せ、上端の少し上(災害アイコン列の上)に配置
                float x = Mathf.Max(0f, host.width - button.width - ModConfig.SummonButtonOffsetX);
                button.relativePosition = new Vector3(x, ModConfig.SummonButtonOffsetY);

                _button = button;
                _attached = true;
                ModConfig.Log("UFO召喚ボタンを災害パネルに取り付けました "
                    + "(panel size=" + host.width + "x" + host.height
                    + ", button pos=" + button.relativePosition + ")");
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("InvasionUI.TryAttachToDisasterPanel error: " + e);
            }
        }

        private static void CreateFallbackButton()
        {
            try
            {
                UIView view = UIView.GetAView();
                if (view == null)
                {
                    ModConfig.LogError("InvasionUI.CreateFallbackButton: UIView.GetAView() が null");
                    _attached = true; // これ以上リトライしない
                    return;
                }

                if (view.FindUIComponent<UIButton>(ButtonName) != null)
                {
                    _attached = true;
                    return;
                }

                UIButton button = view.AddUIComponent(typeof(UIButton)) as UIButton;
                if (button == null)
                {
                    ModConfig.LogError("InvasionUI.CreateFallbackButton: UIButton 生成失敗");
                    _attached = true;
                    return;
                }

                StyleButton(button);
                button.relativePosition = new Vector3(20f, 60f);

                _button = button;
                _attached = true;
                ModConfig.Log("災害パネルが見つからなかったため、UFO召喚ボタンを画面左上に生成しました(フォールバック)");
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("InvasionUI.CreateFallbackButton error: " + e);
                _attached = true;
            }
        }

        private static void StyleButton(UIButton button)
        {
            button.name = ButtonName;
            button.text = "UFO召喚";
            button.textScale = 0.85f;
            button.size = new Vector2(ModConfig.SummonButtonWidth, ModConfig.SummonButtonHeight);
            button.normalBgSprite = "ButtonMenu";
            button.hoveredBgSprite = "ButtonMenuHovered";
            button.pressedBgSprite = "ButtonMenuPressed";
            button.tooltip = "UFO母船を召喚する地点を選択します";
            button.eventClick += OnButtonClick;
        }

        public static void DestroyButton()
        {
            try
            {
                if (_button != null)
                {
                    _button.eventClick -= OnButtonClick;
                    Object.Destroy(_button.gameObject);
                    _button = null;
                }
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("InvasionUI.DestroyButton error: " + e);
            }
            finally
            {
                _attached = false;
                _waitFrames = 0;
            }
        }

        private static void OnButtonClick(UIComponent component, UIMouseEventParameter eventParam)
        {
            try
            {
                ToolsModifierControl.SetTool<MothershipPlacementTool>();
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("InvasionUI.OnButtonClick error: " + e);
            }
        }
    }
}
