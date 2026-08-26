namespace SynergyUI
{
    /// <summary>
    /// 主菜单 —— 进入四个子界面的入口。验证 UIManager 导航栈。
    /// </summary>
    public sealed class MainMenuScreen : UIScreen
    {
        public override string UxmlResourcePath => "UXML/MainMenu";

        public override void OnEnter()
        {
            UIBinder.BindButton(Root, "btn-effect", () => Manager.Show<EffectComposerScreen>());
            UIBinder.BindButton(Root, "btn-card", () => Manager.Show<CardComposerScreen>());
            UIBinder.BindButton(Root, "btn-deck", () => Manager.Show<DeckBuilderScreen>());
            UIBinder.BindButton(Root, "btn-battle", () => Manager.Show<BattleScreen>());
        }
    }
}
