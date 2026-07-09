using ICities;

namespace AlienInvasion.Game.Loading
{
    /// <summary>
    /// レベルロード毎にUFO召喚ボタン(mod専用UI)を生成/破棄する。ゲームが自動検出する。
    /// OnLevelUnloading で必ず DestroyButton するのは、Task 11レビューで指摘された
    /// 「静的状態がレベルロードをまたいで残留する」問題と同種の不具合(UIコンポーネントの
    /// リーク・複数レベルにまたがる二重ボタン)を避けるため。
    /// </summary>
    public class InvasionLoadingExtension : LoadingExtensionBase
    {
        public override void OnLevelLoaded(LoadMode mode)
        {
            base.OnLevelLoaded(mode);
            try
            {
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
