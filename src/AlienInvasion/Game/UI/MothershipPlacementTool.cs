using ColossalFramework;
using UnityEngine;

namespace AlienInvasion.Game.UI
{
    /// <summary>
    /// The tool for placing an invasion by hand. It derives from ToolBase to give the same
    /// feel as the other disasters - meteors, tornadoes and so on - where you aim and left
    /// click to confirm. The raycast against the terrain and the overlay drawing follow the
    /// same pattern the real DisasterTool uses.
    ///
    /// Threading: every part of a ToolBase lifecycle - OnEnable, OnDisable, OnToolLateUpdate,
    /// SimulationStep, RenderOverlay and OnToolGUI - is called from Unity's main/render thread,
    /// which is not the simulation thread that drives
    /// ThreadingExtensionBase.OnBeforeSimulationTick and OnAfterSimulationTick. Calling
    /// InvasionManager.StartInvasion directly from here therefore honours its main-thread-only
    /// contract.
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
                // One shot: a single click places it and returns to the normal camera and
                // selection mode.
                ToolsModifierControl.SetTool<DefaultTool>();
            }
        }
    }
}
