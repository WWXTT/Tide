using System.Collections.Generic;

namespace CardCore
{
    /// <summary>声明期交互参数（2026-11-15 v0.3 移动/宣言规格；P1 2026-11-19 落地）：随发动入栈
    ///（GameActions.PlayCard/PlayCardInResponse/ActivateEffect/ActivateHeroSkill 的 trailing optional
    /// 参数 → PendingEffect/EffectInstance.Interaction → 执行上下文），结算期由 handler 消费
    ///（MoveUnitHandler 选格 / ProphecyHandlerUtil.AskCardNameAsync 卡名）。
    /// null=无（行为与旧版完全一致，UI 桥/预置照旧）。</summary>
    public sealed class InteractionParams
    {
        /// <summary>宣言卡名（原始卡名，不含 "Name:" 前缀——编码由消费口按 handler 契约补）。</summary>
        public string DeclaredName;

        /// <summary>移动落格（-1=未指定）。</summary>
        public int MoveX = -1;

        /// <summary>移动落格（-1=未指定）。</summary>
        public int MoveZ = -1;

        /// <summary>是否携带有效落格。</summary>
        public bool HasMoveCell => MoveX >= 0 && MoveZ >= 0;

        /// <summary>宣言候选序号（动作特征 [16]，P1 v0.3）：DeclaredName 在该效果候选表的下标
        ///（0..15 钳位；-1=未设置）。由 LAE 展开时写入，仅随行特征消费（引擎不读）。</summary>
        public int DeclaredIndex = -1;
    }

    /// <summary>AI/训练可用的宣言候选名单（2026-11-15 v0.3 用户口径：**只使用已读到的卡名**）：
    /// SearchDeck（检索）→ 己方牌库去重名（己方信息天然全可读）；
    /// DeclareHand（强夺）→ 对手手牌中带 ExposedCounter（展示指示物）卡的去重名；
    /// DeclareDeck（窃取）→ 对手牌库中带 ExposedCounter 卡的去重名。
    /// 按区间隔不混（换区即清展示——RevealRules 既有生命周期）；候选为空=不发动（非空转）。
    /// 禁止枚举对手未展示卡名/全池卡名（v0.3 §7.4）。</summary>
    public static class DeclarationCandidates
    {
        /// <summary>该原子的宣言候选名（distinct 保序；空名跳过）。其他原子/无候选=空表。</summary>
        public static List<string> For(GameCore core, Player me, AtomicEffectInstance atom)
        {
            var result = new List<string>();
            if (core?.ZoneManager == null || atom == null || me == null) return result;
            IEnumerable<Card> source = null;
            switch (atom.Type)
            {
                case AtomicEffectType.SearchDeck: // 检索：己方牌库（己方信息，天然可读）
                    source = core.ZoneManager.GetCards(me, Zone.Deck);
                    break;
                case AtomicEffectType.DeclareHand: // 强夺：对手手牌已展示
                    source = Attribute.RevealRules.GetExposedCards(core.ZoneManager, me.Opponent, Zone.Hand);
                    break;
                case AtomicEffectType.DeclareDeck: // 窃取：对手牌库已展示
                    source = Attribute.RevealRules.GetExposedCards(core.ZoneManager, me.Opponent, Zone.Deck);
                    break;
                default:
                    return result;
            }
            if (source == null) return result;
            var seen = new HashSet<string>();
            foreach (var c in source)
            {
                var name = (c as IHasName)?.CardName;
                if (string.IsNullOrEmpty(name) || !seen.Add(name)) continue;
                result.Add(name);
            }
            return result;
        }
    }
}
