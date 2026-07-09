using ColossalFramework;
using UnityEngine;

namespace AlienInvasion.Game.UI
{
    /// <summary>
    /// UFO召喚の手動配置ツール。他の災害(Meteor/Tornado等)と同じ「狙って左クリックで確定」の
    /// 操作感を提供する ToolBase 派生。地形へのレイキャストとオーバーレイ描画は本物の
    /// DisasterTool と同じパターンを踏襲している。
    ///
    /// スレッド境界: ToolBase のライフサイクル(OnEnable/OnDisable/OnToolLateUpdate/SimulationStep/
    /// RenderOverlay/OnToolGUI)はいずれもUnityのメイン/レンダースレッドから呼ばれる
    /// (シミュレーションスレッドの ThreadingExtensionBase.OnBeforeSimulationTick/OnAfterSimulationTick とは別)。
    /// そのためここから直接 InvasionManager.StartInvasion を呼んでも Task 11 の
    /// メインスレッド専用契約に違反しない。
    /// </summary>
    public class MothershipPlacementTool : ToolBase
    {
        private Vector3 m_cachedPosition;
        private bool m_placementValid;
        private Ray m_mouseRay;
        private float m_mouseRayLength;
        private bool m_mouseRayValid;

        protected override void OnEnable()
        {
            base.OnEnable();
            m_placementValid = false;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            m_placementValid = false;
        }

        protected override void OnToolLateUpdate()
        {
            Vector3 mousePosition = Input.mousePosition;
            m_mouseRay = Camera.main.ScreenPointToRay(mousePosition);
            m_mouseRayLength = Camera.main.farClipPlane;
            m_mouseRayValid = !m_toolController.IsInsideUI && Cursor.visible;
        }

        public override void SimulationStep()
        {
            if (m_mouseRayValid)
            {
                RaycastInput input = new RaycastInput(m_mouseRay, m_mouseRayLength);
                RaycastOutput output;
                if (RayCast(input, out output))
                {
                    output.m_hitPos.y = Singleton<TerrainManager>.instance.SampleRawHeightSmoothWithWater(output.m_hitPos, false, 0f);
                    m_cachedPosition = output.m_hitPos;
                    m_placementValid = true;
                }
                else
                {
                    m_placementValid = false;
                }
            }
            else
            {
                m_placementValid = false;
            }
        }

        public override void RenderOverlay(RenderManager.CameraInfo cameraInfo)
        {
            if (!m_placementValid) return;

            Color color = new Color(0.2f, 0.9f, 1f, 0.6f);
            Singleton<RenderManager>.instance.OverlayEffect.DrawCircle(
                cameraInfo,
                color,
                m_cachedPosition,
                100f,
                m_cachedPosition.y - 100f,
                m_cachedPosition.y + 100f,
                false,
                true);
        }

        protected override void OnToolGUI(Event e)
        {
            if (m_toolController.IsInsideUI) return;
            if (e.type != EventType.MouseDown || e.button != 0 || !m_placementValid) return;

            try
            {
                AlienInvasion.Game.InvasionManager.StartInvasion(m_cachedPosition);
            }
            catch (System.Exception ex)
            {
                AlienInvasion.Game.ModConfig.LogError("MothershipPlacementTool.OnToolGUI error: " + ex);
            }
            finally
            {
                // ワンショット: 1回クリックしたら配置終了、通常のカメラ/選択モードへ戻す。
                ToolsModifierControl.SetTool<DefaultTool>();
            }
        }
    }
}
