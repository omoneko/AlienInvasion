using ColossalFramework.UI;
using UnityEngine;

namespace AlienInvasion.Game.UI
{
    /// <summary>
    /// Adds the summon icon into the row of disaster icons (a UIScrollablePanel) in the vanilla
    /// DisastersPanel, alongside the vanilla ones. Because it joins that row's automatic layout,
    /// it cannot overlap the existing icons or another mod's - the missile mod's, say - and it
    /// cannot end up clipped by the panel.
    ///
    /// Note that DisastersPanel rebuilds the row every time the tab is opened (RefreshPanel),
    /// which can take the button with it. EnsureAttached() therefore runs every frame on the
    /// main thread and re-attaches _button if it has gone. Where the row never appears at all -
    /// without the Natural Disasters DLC, for instance - it falls back after a grace period to
    /// a button in the top-right corner.
    /// </summary>
    public static class InvasionUI
    {
        private const string ButtonName = "AlienInvasionSummonButton";

        private static UIButton _button;
        private static Texture2D _iconTex;  // the mothership icon
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

        /// <summary>
        /// Call every frame from OnUpdate on the main thread; re-attaches the icon whenever the
        /// row is rebuilt.
        /// This also fixes the same bug reported against KAIJU, where the icon vanished from the
        /// disasters tab. The old code stopped trying the row for good as soon as it fell back
        /// to the top-right button, so if the row appeared later than
        /// SummonButtonFallbackFrames, the icon never came back. It now keeps trying the row
        /// even while the fallback is showing, and moves to the disasters tab - discarding the
        /// fallback - the moment the row exists.
        /// </summary>
        public static void EnsureAttached()
        {
            if (_rowFound && _button != null) return; // already in the row; a rebuild clears this and it retries

            UIButton fallbackButton = _fallback ? _button : null;
            if (TryAttachToRow())
            {
                _rowFound = true;
                if (fallbackButton != null && !ReferenceEquals(fallbackButton, _button))
                {
                    try { Object.Destroy(fallbackButton.gameObject); }
                    catch (System.Exception e) { ModConfig.LogError("InvasionUI: fallback cleanup error: " + e); }
                    ModConfig.Log("moved the summon button from the top-right fallback into the disaster icon row");
                }
                _fallback = false;
                return;
            }

            if (_button != null) return; // the fallback is showing and the row is still not there
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
                ModConfig.Log("added the mothership icon to the disaster icon row");
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

                // Part of the same fix: an existing button - left over from a previous level,
                // say - is adopted into _button and reused. The old code merely set
                // _fallback=true and returned, after which the icon was nowhere to be seen and
                // nothing was logged.
                UIButton stale = view.FindUIComponent<UIButton>(ButtonName);
                if (stale != null)
                {
                    _button = stale;
                    _fallback = true;
                    ModConfig.Log("reused the existing summon button (fallback)");
                    return;
                }

                UIButton button = view.AddUIComponent(typeof(UIButton)) as UIButton;
                if (button == null) { _fallback = true; return; }
                StyleFallback(button);
                button.relativePosition = FindTopRightSlot(view, button);
                _button = button;
                _fallback = true;
                ModConfig.Log("the disaster row was not found, so the summon button was created in the top-right corner (fallback)");
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
                // Stop the click from also selecting a vanilla disaster in the row: consume the
                // event and clear the selection before opening the placement tool.
                try { if (eventParam != null) eventParam.Use(); } catch { }
                ClearDisasterSelection();
                ToolsModifierControl.SetTool<MothershipPlacementTool>();
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("InvasionUI.OnButtonClick error: " + e);
            }
        }

        /// <summary>Clears the disasters panel's selection, both the highlight and the armed state.</summary>
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
