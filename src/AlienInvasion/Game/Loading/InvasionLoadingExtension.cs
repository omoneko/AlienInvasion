using ICities;

namespace AlienInvasion.Game.Loading
{
    /// <summary>
    /// Creates and destroys the summon button - this mod's own UI - on every level load. The
    /// game discovers this class on its own.
    /// DestroyButton always runs in OnLevelUnloading to avoid the same class of bug as static
    /// state surviving across a level load: leaked UI components, and a second button appearing
    /// once more than one level has been loaded.
    /// </summary>
    public class InvasionLoadingExtension : LoadingExtensionBase
    {
        public override void OnLevelLoaded(LoadMode mode)
        {
            base.OnLevelLoaded(mode);
            try
            {
                // Register the custom placement tool with ToolController first, so SetTool<T>()
                // can open it. Without this, SetTool finds nothing in the dictionary, returns
                // null, and the button and the hotkey silently do nothing.
                UI.ToolRegistration.Register<UI.MothershipPlacementTool>();
                UI.InvasionUI.CreateButton();
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("InvasionLoadingExtension.OnLevelLoaded error: " + e);
            }
        }

        public override void OnLevelUnloading()
        {
            base.OnLevelUnloading();
            try
            {
                UI.InvasionUI.DestroyButton();
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("InvasionLoadingExtension.OnLevelUnloading error: " + e);
            }
        }
    }
}
