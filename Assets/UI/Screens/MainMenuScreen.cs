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
            UIBinder.BindButton(Root, "btn-battle", () =>
            {
                BattleEntry.Mode = BattleMode.LocalAI; // 本地 vs AI（随机卡组开局）
                Manager.Show<BattleScreen>();
            });
            // 联机匹配入口（阶段三，2026-09-24）：大厅（房间列表/自动匹配/AI 填位）→ 接续对战界面
            UIBinder.BindButton(Root, "btn-match", () => Manager.Show<MatchScreen>());
            // 网络对战调试入口（2026-09-24 阶段四）：连接配置写死本机——ip/port 表单属
            // 阶段三匹配界面职责，此按钮仅打通网络模式全链冒烟（MatchScreen 已就绪，保留作快速直连）。
            UIBinder.BindButton(Root, "btn-battle-net", () =>
            {
                BattleEntry.Mode = BattleMode.Network;
                BattleEntry.Host = "127.0.0.1";
                BattleEntry.Port = 7777;
                Manager.Show<BattleScreen>();
            });
        }
    }
}
