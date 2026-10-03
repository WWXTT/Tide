using UnityEngine;
using CardCore.Network;
using Cysharp.Threading.Tasks;

namespace SynergyUI
{
    /// <summary>
    /// 主菜单 —— 子界面入口（2026-10-01 预制体化：层级来自 Assets/Art/UI/MainUI.prefab，
    /// Build 只做按名绑定+闭包接线；title/subtitle 等静态文本由预制体烘焙）。
    /// </summary>
    public sealed class MainMenuScreen : UIScreen
    {
        protected override string PrefabName => "MainUI";
        protected override string RootName => "main-menu";

        protected override void Build()
        {
            BindButton("btn-battle", () =>
            {
                BattleEntry.Mode = BattleMode.LocalAI; // 本地 vs AI（随机卡组开局）
                // 进战场过渡（2026-10-03 定案）：黑洞吞屏（光压 -20°→视界膨胀 0.5s）完成后再切屏，
                // 屏内 OnEnter 继续装棋盘+日出（BattleStageDirector 四步链）
                BattleStageDirector.SwallowToBattleAsync(() => Manager.Show<BattleScreen>()).Forget();
            });

            // 大厅入口
            BindButton("btn-match", () => Manager.Show<MatchScreen>());
            BindButton("btn-deck", () => Manager.Show<DeckBuilderScreen>());
            BindButton("btn-card", () => Manager.Show<CardComposerScreen>());
            BindButton("btn-effect", () => Manager.Show<EffectComposerScreen>());
        }
    }
}
