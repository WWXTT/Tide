using System.Collections.Generic;
using System.Linq;
using CardCore.Attribute;

namespace CardCore
{
    /// <summary>
    /// 组合目标域服务（2026-09-10 目标域模型）：
    /// 运行时「候选空 → 不可发动」的引擎侧预检（PlayCard / CanActivate 两链路）。
    /// 域来自 converter 预计算（def.TargetDomain / ChoiceDomains）；
    /// 候选解析复用 EffectHandlerRegistry.ResolveCandidates（同结算口径）。
    /// 注：SimpleAI / BattleScreen / LegalActionEnumerator 的同口径接线属 AI/UI 后续任务。
    /// </summary>
    public static class TargetDomainService
    {
        /// <summary>卡以 modeIndex 模式打出时，全部非无目标效果是否都有可作用候选。</summary>
        public static bool HasPlayableTargets(GameCore core, Player player, Card card, int modeIndex = 0)
        {
            if (core == null || player == null || card == null) return true;
            var data = card is CardWrapper wrapper ? wrapper.GetData() : null;
            if (data?.Effects == null || data.Effects.Count == 0) return true;

            var ctx = new EffectExecutionContext
            {
                Source = card.IsSpellCard() ? (Entity)player : card,
                Controller = player,
                ZoneManager = core.ZoneManager,
                ElementPool = core.ElementPool,
                ModeIndex = modeIndex,
            };

            foreach (var effectData in data.Effects)
            {
                if (effectData == null) continue;
                var def = CardEffectConverter.ConvertOne(effectData, data.ID);
                if (!HasCandidates(def, ctx)) return false;
            }
            return true;
        }

        /// <summary>激活式能力（def 已转换）：组合域候选非空预检——CanActivate 第 5 步。</summary>
        public static bool HasCandidates(EffectDefinition def, EffectExecutionContext ctx)
        {
            if (def == null) return true;
            // 区域自结算类（SelectionMode=None，2026-09-11）：无候选要求——
            // 空牌库抽卡走疲劳、空库磨牌空转等由 handler 自裁，不在预检拦
            if (def.SelectionMode == SelectionMode.None) return true;

            var domain = def.TargetDomain;
            if (def.ChoiceDomains != null && ctx.ModeIndex >= 0
                && ctx.ModeIndex < def.ChoiceDomains.Length
                && def.ChoiceDomains[ctx.ModeIndex] != null
                && def.ChoiceDomains[ctx.ModeIndex].Count > 0)
            {
                domain = def.ChoiceDomains[ctx.ModeIndex];
            }
            if (domain == null || domain.Count == 0) return true; // 无目标效果不拦

            return EffectHandlerRegistry.ResolveCandidates(domain, def.TargetFilter, ctx).Count > 0;
        }
    }
}
