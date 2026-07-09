using ColossalFramework.UI;
using UnityEngine;

namespace AlienInvasion.Game.UI
{
    /// <summary>
    /// UFO召喚ボタンの生成/破棄。ボタンは UIView 直下に生成される単純なmod専用UIで、
    /// バニラの災害パネルには一切統合しない(意図的な設計判断)。
    ///
    /// 静的参照(_button)はレベルロード毎に InvasionLoadingExtension から
    /// CreateButton/DestroyButton が呼ばれることで正しく張り直される前提。
    /// Task 11レビューで指摘された「静的状態がレベルロードをまたいで残留する」問題の
    /// 再発を避けるため、DestroyButton で必ず参照を破棄・null化すること。
    /// </summary>
    public static class InvasionUI
    {
        private const string ButtonName = "AlienInvasionSummonButton";
        private static UIButton _button;

        public static void CreateButton()
        {
            try
            {
                UIView view = UIView.GetAView();
                if (view == null)
                {
                    ModConfig.LogError("InvasionUI.CreateButton: UIView.GetAView() が null を返しました");
                    return;
                }

                // 二重生成防止: 静的参照が残っていなくても、同名コンポーネントが既に
                // View 下に存在する場合はスキップする(念のための防御)。
                if (_button != null || view.FindUIComponent<UIButton>(ButtonName) != null)
                {
                    return;
                }

                UIButton button = view.AddUIComponent(typeof(UIButton)) as UIButton;
                if (button == null)
                {
                    ModConfig.LogError("InvasionUI.CreateButton: UIButton の生成に失敗しました");
                    return;
                }

                button.name = ButtonName;
                button.text = "UFO召喚";
                button.size = new Vector2(90f, 30f);
                button.relativePosition = new Vector3(20f, 60f);
                button.normalBgSprite = "ButtonMenu";
                button.eventClick += OnButtonClick;

                _button = button;
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("InvasionUI.CreateButton error: " + e);
            }
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
