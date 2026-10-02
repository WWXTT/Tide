using UnityEngine;
using CardCore.Network;

namespace SynergyUI
{
    /// <summary>
    /// 主菜单 —— 子界面入口（2026-10-01 预制体化：层级来自 Assets/Art/UI/MainUI.prefab，
    /// Build 只做按名绑定+闭包接线；title/subtitle 等静态文本由预制体烘焙）。
    /// </summary>
    public sealed class MainMenuScreen : UIScreen
    {
        protected override string PrefabAddress => "MainUI";
        protected override string PrefabAssetPath => "Assets/Art/UI/MainUI.prefab";
        protected override string RootName => "main-menu";

        protected override void Build()
        {
            BindButton("btn-battle", () =>
            {
                BattleEntry.Mode = BattleMode.LocalAI; // 本地 vs AI（随机卡组开局）
                Manager.Show<BattleScreen>();
            });

            // 大厅入口（比赛禁联机——随 NetGate 总闸屏蔽；恢复=NetGate.OnlineEnabled 置 true）
            BindButton("btn-match", () => Manager.Show<MatchScreen>());
            var match = Find("btn-match");
            if (match != null) match.gameObject.SetActive(NetGate.OnlineEnabled);

            BindButton("btn-deck", () => Manager.Show<DeckBuilderScreen>());
            BindButton("btn-card", () => Manager.Show<CardComposerScreen>());
            BindButton("btn-effect", () => Manager.Show<EffectComposerScreen>());
        }
    }
}
