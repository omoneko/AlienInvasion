using ColossalFramework.UI;
using UnityEngine;

namespace AlienInvasion.Game.UI
{
    /// <summary>
    /// バニラ災害パネル(DisastersPanel)の「災害アイコン列」(UIScrollablePanel)の中に、
    /// UFO召喚アイコンをバニラ災害アイコンと並べて追加する。列の自動レイアウトに乗るため
    /// 他MOD(ミサイル等)や既存アイコンと重ならず、パネルにも見切れない。
    ///
    /// 注意: DisastersPanel はタブを開く度に列を再生成する(RefreshPanel)ため、追加したボタンが
    /// 消えることがある。EnsureAttached() を毎フレーム(メインスレッド)呼び、_button が消えていたら
    /// 貼り直す。列が見つからない環境(例: Natural Disasters DLC 未所持)は猶予後に右上フォールバック。
    /// </summary>
    public static class InvasionUI
    {
        private const string ButtonName = "AlienInvasionSummonButton";

        private static UIButton _button;
        private static Texture2D _iconTex;  // UFOアイコン
        private static bool _rowFound;
        private static bool _fallback;
        private static int _waitFrames;

        public static void CreateButton()
        {
            _button = null;
            _rowFound = false;
            _fallback = false;
            _waitFrames = 0;
            EnsureAttached();
        }

        /// <summary>OnUpdate(メインスレッド)から毎フレーム呼ぶ。列再生成で消えたら貼り直す。</summary>
        public static void EnsureAttached()
        {
            if (_fallback) return;
            if (_button != null) return;

            if (TryAttachToRow()) { _rowFound = true; return; }
            if (!_rowFound && ++_waitFrames >= ModConfig.SummonButtonFallbackFrames) CreateFallbackButton();
        }

        private static bool TryAttachToRow()
        {
            try
            {
                DisastersPanel panel = Object.FindObjectOfType<DisastersPanel>();
                if (panel == null || panel.component == null) return false;
                UIScrollablePanel row = panel.component.GetComponentInChildren<UIScrollablePanel>();
                if (row == null) return false;

                UIButton existing = row.Find<UIButton>(ButtonName);
                if (existing != null) { _button = existing; return true; }

                UIButton button = row.AddUIComponent<UIButton>();
                StyleTile(button, row);
                _button = button;
                ModConfig.Log("UFOアイコンを災害アイコン列に追加しました");
                return true;
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("InvasionUI.TryAttachToRow error: " + e);
                return false;
            }
        }

        private static void StyleTile(UIButton button, UIScrollablePanel row)
        {
            button.name = ButtonName;
            UIButton sample = FirstOtherButton(row);
            Vector2 sz = (sample != null && sample.size.x > 1f) ? sample.size : new Vector2(100f, 100f);
            button.size = sz;

            if (sample != null)
            {
                button.atlas = sample.atlas;
                button.normalBgSprite = sample.normalBgSprite;
                button.hoveredBgSprite = sample.hoveredBgSprite;
                button.pressedBgSprite = sample.pressedBgSprite;
                button.focusedBgSprite = sample.focusedBgSprite;
                button.disabledBgSprite = sample.disabledBgSprite;
            }
            else
            {
                button.normalBgSprite = "ButtonMenu";
                button.hoveredBgSprite = "ButtonMenuHovered";
                button.pressedBgSprite = "ButtonMenuPressed";
            }
            button.tooltip = "Select a location to summon the UFO mothership";
            button.eventClick += OnButtonClick;

            if (_iconTex == null) _iconTex = UfoIcon.Build(64);
            var icon = button.AddUIComponent<UITextureSprite>();
            float isz = Mathf.Min(sz.x, sz.y) * 0.62f;
            icon.texture = _iconTex;
            icon.size = new Vector2(isz, isz);
            icon.relativePosition = new Vector3((sz.x - isz) * 0.5f, (sz.y - isz) * 0.5f);
            icon.isInteractive = false;
        }

        private static UIButton FirstOtherButton(UIScrollablePanel row)
        {
            UIButton[] bs = row.GetComponentsInChildren<UIButton>();
            for (int i = 0; i < bs.Length; i++)
            {
                if (bs[i] != null && bs[i].name != ButtonName && bs[i].size.x > 1f) return bs[i];
            }
            return null;
        }

        private static void CreateFallbackButton()
        {
            try
            {
                UIView view = UIView.GetAView();
                if (view == null) { _fallback = true; return; }
                if (view.FindUIComponent<UIButton>(ButtonName) != null) { _fallback = true; return; }

                UIButton button = view.AddUIComponent(typeof(UIButton)) as UIButton;
                if (button == null) { _fallback = true; return; }
                StyleFallback(button);
                button.relativePosition = FindTopRightSlot(view, button);
                _button = button;
                _fallback = true;
                ModConfig.Log("災害列が見つからないため、UFO召喚ボタンを画面右上に生成しました(フォールバック)");
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("InvasionUI.CreateFallbackButton error: " + e);
                _fallback = true;
            }
        }

        private static void StyleFallback(UIButton button)
        {
            button.name = ButtonName;
            button.size = new Vector2(ModConfig.SummonButtonWidth, ModConfig.SummonButtonHeight);
            button.normalBgSprite = "ButtonMenu";
            button.hoveredBgSprite = "ButtonMenuHovered";
            button.pressedBgSprite = "ButtonMenuPressed";
            button.tooltip = "Select a location to summon the UFO mothership";
            button.eventClick += OnButtonClick;

            if (_iconTex == null) _iconTex = UfoIcon.Build(30);
            var icon = button.AddUIComponent<UITextureSprite>();
            icon.texture = _iconTex;
            icon.size = new Vector2(30f, 30f);
            icon.relativePosition = new Vector3((ModConfig.SummonButtonWidth - 30f) * 0.5f, (ModConfig.SummonButtonHeight - 30f) * 0.5f);
            icon.isInteractive = false;
        }

        private static Vector3 FindTopRightSlot(UIView view, UIComponent self)
        {
            Vector2 res = view.GetScreenResolution();
            float w = ModConfig.SummonButtonWidth, h = ModConfig.SummonButtonHeight;
            const float margin = 8f, gap = 6f;
            float x = Mathf.Max(0f, res.x - w - margin);
            UIButton[] buttons = view.GetComponentsInChildren<UIButton>();
            for (float y = margin; y <= res.y * 0.5f; y += h + gap)
            {
                Rect slot = new Rect(x, y, w, h);
                if (!OverlapsAnyButton(buttons, slot, self, res)) return new Vector3(x, y);
            }
            return new Vector3(x, margin);
        }

        private static bool OverlapsAnyButton(UIButton[] buttons, Rect slot, UIComponent self, Vector2 res)
        {
            if (buttons == null) return false;
            for (int i = 0; i < buttons.Length; i++)
            {
                UIButton b = buttons[i];
                if (b == null || b == self || !b.isVisible) continue;
                Vector2 sz = b.size;
                if (sz.x <= 1f || sz.y <= 1f) continue;
                if (sz.x > res.x * 0.5f || sz.y > res.y * 0.5f) continue;
                Vector3 ap = b.absolutePosition;
                Rect br = new Rect(ap.x, ap.y, sz.x, sz.y);
                if (br.Overlaps(slot)) return true;
            }
            return false;
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
                if (_iconTex != null)
                {
                    Object.Destroy(_iconTex);
                    _iconTex = null;
                }
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("InvasionUI.DestroyButton error: " + e);
            }
            finally
            {
                _rowFound = false;
                _fallback = false;
                _waitFrames = 0;
            }
        }

        private static void OnButtonClick(UIComponent component, UIMouseEventParameter eventParam)
        {
            try
            {
                // 災害列のクリック処理にバニラ災害を選ばせない：イベント消費＋選択解除してから母船設置ツールを起動。
                try { if (eventParam != null) eventParam.Use(); } catch { }
                ClearDisasterSelection();
                ToolsModifierControl.SetTool<MothershipPlacementTool>();
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("InvasionUI.OnButtonClick error: " + e);
            }
        }

        /// <summary>災害パネルの選択(ハイライト/武装)を解除する。</summary>
        private static void ClearDisasterSelection()
        {
            try
            {
                DisastersPanel panel = Object.FindObjectOfType<DisastersPanel>();
                if (panel == null) return;
                System.Reflection.PropertyInfo prop = typeof(GeneratedScrollPanel).GetProperty(
                    "selectedIndex",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (prop != null && prop.CanWrite) prop.SetValue(panel, -1, null);
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("InvasionUI.ClearDisasterSelection error: " + e);
            }
        }
    }
}
