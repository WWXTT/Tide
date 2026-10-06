using System.Collections.Generic;
using CardCore;
using CardCore.Attribute;

namespace SynergyUI
{
    /// <summary>
    /// 原子表工坊协调器（2026-10-06 UI 层）：编排「玩家改价 → 表侧落位 → 卡费刷新」全链。
    /// 分层理由：CardCore（AtomicTableOverlay/CardCostService/AtomicEffectTable）不依赖卡池
    /// （CardCatalog 属 UI 层），本类是两侧唯一的接线点。
    ///
    /// 三个入口面：
    /// · 工坊界面：ApplyRowEdit / RestoreRow / RestoreAll / SetOverlayEnabled（即时生效+落档）；
    /// · 对局开局：EnsureBattleCosts（BattleController.StartNewGame 装载卡池后调，幂等重放+重推）；
    /// · 教学免疫：EnterTutorialBaseline / ExitTutorialBaseline（教学局回基线，退出恢复改价）。
    /// 费用刷新语义：强制重推（ReforceSuggestedCosts）有意突破「声明优先」惯例——
    /// Cards.json 烘焙的声明费按当前表重算覆写，这正是「验证费用对卡组带来的变化」的机制本体。
    /// </summary>
    public static class AtomicTableWorkshop
    {
        private static bool _tutorialBaselineOn;

        /// <summary>教学基线在位（EnsureBattleCosts 的跳过判据；教学局禁推导覆写）。</summary>
        public static bool TutorialBaselineOn => _tutorialBaselineOn;

        // ======================================== 工坊界面入口 ========================================

        /// <summary>单行改价（编辑控件提交）：写表 + 落档 + 重推已装载卡费。返回重推变化清单（差异预览数据源）。</summary>
        public static List<CardCostService.ReforceLine> ApplyRowEdit(string hashId, float total)
        {
            var overlay = AtomicTableOverlay.Instance;
            if (!AtomicEffectTable.TrySetRowTotal(hashId, total))
            {
                TideLog.Warn($"[AtomicTableWorkshop] 改价被拒：{hashId} → {total}（不存在/不计价/越界）");
                return new List<CardCostService.ReforceLine>();
            }
            overlay.SetRow(hashId, total);
            CardIdentityService.InvalidateTableCache();
            return CardCostService.ReforceSuggestedCosts(CardCatalog.LoadAll());
        }

        /// <summary>恢复单行默认：基线值可能超出可编辑区间（如 12 费仪典行），无法「设回去」——
        /// 走整表重建（Reload 基线 + 重放其余偏离行）。</summary>
        public static List<CardCostService.ReforceLine> RestoreRow(string hashId)
        {
            AtomicTableOverlay.Instance.RemoveRow(hashId);
            RebuildTableFromOverlay();
            return CardCostService.ReforceSuggestedCosts(CardCatalog.LoadAll());
        }

        /// <summary>恢复默认（全部行）：清偏离行 + 整表回基线 + 重推。</summary>
        public static List<CardCostService.ReforceLine> RestoreAll()
        {
            AtomicTableOverlay.Instance.ClearRows();
            RebuildTableFromOverlay();
            return CardCostService.ReforceSuggestedCosts(CardCatalog.LoadAll());
        }

        /// <summary>总开关切换：关=纯基线（Reload 丢弃表侧改价），开=重放偏离行。</summary>
        public static List<CardCostService.ReforceLine> SetOverlayEnabled(bool enabled)
        {
            AtomicTableOverlay.Instance.SetEnabled(enabled);
            RebuildTableFromOverlay();
            return CardCostService.ReforceSuggestedCosts(CardCatalog.LoadAll());
        }

        /// <summary>按 overlay 现状重建表侧：基线 Reload →（启用时）重放偏离行 → 身份缓存失效 →
        /// 卡池失效（声明费还原，下次装载/对局按新表重推）。</summary>
        public static void RebuildTableFromOverlay()
        {
            var overlay = AtomicTableOverlay.Instance;
            AtomicEffectTable.Reload();
            if (overlay.Enabled)
                overlay.ApplyToTable();
            CardIdentityService.InvalidateTableCache();
            CardCatalog.Invalidate();
        }

        // ======================================== 对局开局生效点 ========================================

        /// <summary>BattleController.StartNewGame 装载卡池后调（幂等）：overlay 启用且非教学基线时，
        /// 确保偏离行已落表（EnsureApplied 按表代际去重）+ 全卡池强制重推（改价即时进对局）。
        /// 重推每局执行一次（黑洞过渡遮盖；幂等无账目）——同时兜底「重启后首次对局」「卡池重读盘」两条路径。</summary>
        public static void EnsureBattleCosts()
        {
            if (_tutorialBaselineOn) return;
            var overlay = AtomicTableOverlay.Instance;
            if (!overlay.Enabled) return;
            overlay.EnsureApplied();
            CardIdentityService.InvalidateTableCache();
            CardCostService.ReforceSuggestedCosts(CardCatalog.LoadAll());
        }

        // ======================================== 教学免疫 ========================================

        /// <summary>教学局前调（幂等；StartTutorialGame 两条成功路径）：原子表 Reload 回基线 +
        /// 卡池失效（下次装载=Cards.json 声明费 + 基线推导费）——教学钉死数学不受玩家改价影响。
        /// 只打标记不发还：退出教学由 ExitTutorialBaseline 恢复。</summary>
        public static void EnterTutorialBaseline()
        {
            if (_tutorialBaselineOn) return;
            _tutorialBaselineOn = true;
            if (!AtomicTableOverlay.Instance.Enabled) return; // 本就纯基线：只占标记（教学路径不重放不重推）
            AtomicEffectTable.Reload();
            CardIdentityService.InvalidateTableCache();
            CardCatalog.Invalidate();
        }

        /// <summary>教学局退出后调（幂等；BattleScreen.OnExit）：干净基线起底重放玩家改价 +
        /// 身份缓存失效 + 卡池失效（卡费交由下次装载/对局按改价表重推——不在此刻急推，避免切屏卡顿）。</summary>
        public static void ExitTutorialBaseline()
        {
            if (!_tutorialBaselineOn) return;
            _tutorialBaselineOn = false;
            if (!AtomicTableOverlay.Instance.Enabled) return;
            var overlay = AtomicTableOverlay.Instance;
            AtomicEffectTable.Reload(); // 干净基线起底（防教学过程中表状态被意外污染）
            overlay.ApplyToTable();
            CardIdentityService.InvalidateTableCache();
            CardCatalog.Invalidate();
        }
    }
}
