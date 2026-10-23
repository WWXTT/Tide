using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCore.Attribute
{
    /// <summary>
    /// 变形快照（2026-10-13 临摹/变形赋予重做）——记录变形前形态与状态，变形指示物解除时
    /// **纯回滚不刷新**：不发入场事件、不做入场关键词刷新，只回写快照字段（含横置态/生命），
    /// 在场时重注册原触发式（幂等注册，不触发任何事件）。变形期间受到的伤害随回滚丢弃
    /// （回到变形前数值——「保持变形前全部状态」的字面语义）。
    /// </summary>
    internal struct MorphSnapshot
    {
        public string Id;
        public string CardName;
        public Cardtype Supertype;
        public int Power;
        public int Life;
        public int MaxLife;
        public int BaseCost;
        public List<string> Keywords;
        /// <summary>轨别台账快照（三轨制定案：与 _keywords 同快照同恢复，清除口径不丢轨别）</summary>
        public List<KeywordGrant> Grants;
        /// <summary>原文本效果列表引用（临摹/变形以目标文本效果取代自身——解除时换回）</summary>
        public List<CardEffectData> Effects;
        /// <summary>原连接光环与箭头（2026-10-13 定案：临摹/变形复制箭头类光环——非规则光环全量复制）</summary>
        public List<LinkAuraData> LinkAuras;
        public HexDirection ArrowDirections;
        /// <summary>变形前横置态（变为结界形态自动非横置——解除时还原变形前的真实横置态）</summary>
        public bool WasTapped;
        /// <summary>发动补价（2026-10-23 开放元素限制——任意元素可补差）：max(0, 变形后声明费总点 − 变形前声明费总点)——
        /// 被变形卡发动主动效果时并入该次元素账单的**灰档**（灰档支付链=灰→黑→白→红→蓝→绿，六色皆可垫付），
        /// 叠加于横置/耐久经济之上；触发式不补价。</summary>
        public int Surcharge;
    }

    /// <summary>
    /// 变形机制（2026-10-13 临摹/变形赋予重做）：
    /// - 统一复制核 ApplyForm：把一张在场卡完全复制为目标形态（身份/属性/印刷关键词/文本效果
    ///   ——跳过其规则光环步），挂 1 层变形指示物（MorphStateCounter，中性 Resident）；
    /// - 临摹（Morph 原子）：自身复制场上目标（生物/结界）；变形赋予（GrantMorph 原子）：
    ///   目标变成引用卡（SummonTokenHandler.ResolveTemplate 解析，生物或结界）；
    /// - 解除=变形指示物被清（换区 ClearAll / 净化 PurgeAll——钩子 CounterRules.ClearCounters）
    ///   → RestoreIfMorphed 纯回滚原形态；解减益（ClearNegative）不清中性；
    /// - 触发式随形态重挂（UnregisterEntityEffects 首个消费者 + RegisterCardTriggeredEffects）；
    ///   主动/AI/声明期每帧现转 GetData().Effects，替换即生效；
    /// - 变为结界形态：自动非横置（结界无横置态）+ 按入场同口径初始化耐久层
    ///  （否则结界形态耐久空=永不能发动）；耐久层随解除同清（Resident 与变形指示物同 sweep）。
    /// </summary>
    public static class MorphSystem
    {
        /// <summary>是否处于变形状态（快照在场=变形中；解除清快照）。</summary>
        public static bool IsMorphed(this Card card) => card != null && card._morphSnapshot.HasValue;

        /// <summary>临摹：自身（须在场）复制场上目标（生物/结界）的当前形态。</summary>
        public static bool ApplyLinmo(Card self, Card target, Entity source)
        {
            var data = (target as CardWrapper)?.GetData();
            if (data == null) return false;
            return ApplyForm(self, data, source);
        }

        /// <summary>
        /// 统一复制核：把在场卡完全复制为目标形态（深克隆目标——不动目标本体），
        /// 跳过其规则光环步（ModifyGameRule 原子——运行时唯一同名互斥源），挂变形指示物并重挂触发式。
        /// 已变形的不叠加；非在场卡/裸卡拒绝。
        /// </summary>
        public static bool ApplyForm(Card self, CardData form, Entity source)
        {
            if (self == null || form == null || self._morphSnapshot.HasValue) return false;
            var data = (self as CardWrapper)?.GetData();
            if (data == null || self.GetZone() != Zone.Battlefield) return false;

            // 深克隆目标形态（TideJson 往返——回响复制同款先例；不与目标共享任何可变引用）
            var clone = TideJson.FromJson<CardData>(TideJson.ToJson(form));
            if (clone == null) return false;
            CardCostService.EnsureCost(clone); // 直构克隆走统一计价口：刷新推导费显示缓存（恒推导定案）
            int surcharge = Math.Max(0, (int)(clone.Cost?.Total ?? 0f) - (int)((self as IHasCost)?.Cost?.Total ?? 0f));

            // 快照原形态与状态（纯回滚数据源——含横置态与原文本效果）
            self._morphSnapshot = new MorphSnapshot
            {
                Id = self.ID,
                CardName = (self as IHasName)?.CardName,
                Supertype = (self as IHasSupertype)?.Supertype ?? Cardtype.Creature,
                Power = self._power,
                Life = self._life,
                MaxLife = self._maxLife,
                BaseCost = self._baseCost,
                Keywords = new List<string>(self._keywords),
                Grants = self._keywordGrants.Select(g => new KeywordGrant
                {
                    Keyword = g.Keyword,
                    Lane = g.Lane,
                    Source = g.Source,
                    Value = g.Value,
                    Limit = g.Limit,
                }).ToList(),
                Effects = data.Effects,
                LinkAuras = data.LinkAuras,
                ArrowDirections = data.ArrowDirections,
                WasTapped = self._isTapped,
                Surcharge = surcharge,
            };

            // 复制身份/属性/关键词 + 文本效果取代
            self.ID = clone.ID;
            if (self is IHasName named) named.CardName = clone.CardName;
            if (self is IHasSupertype st) st.Supertype = clone.Supertype;
            self._power = clone.Power ?? 0;
            self._life = clone.Life ?? 1;
            self._maxLife = self._life;
            self._baseCost = (int)(clone.Cost?.Total ?? 0f);
            self._keywords.Clear();
            self._keywordGrants.Clear();
            if (clone.Keywords != null)
            {
                // 目标形态关键词落 Printed 轨（形态=本体）；不叠加定案：Distinct 单份+台账唯一
                foreach (var kw in clone.Keywords.Distinct())
                {
                    if (string.IsNullOrEmpty(kw) || self._keywords.Contains(kw)) continue;
                    self._keywords.Add(kw);
                    self._keywordGrants.Add(new KeywordGrant { Keyword = kw, Lane = KeywordLane.Printed });
                }
            }
            data.Effects = FilterRuleAuras(clone.Effects); // wrapper 的 _data 只读但 Effects 属性可写——逐卡实例，不污染目录

            // 连接光环（箭头类，2026-10-13 定案：非规则光环全量复制——属性/关键词光环随形态走；
            // 规则光环不在 CardData.LinkAuras，而在 Effects 的 ModifyGameRule 步，已在上方被滤除）
            data.LinkAuras = clone.LinkAuras != null
                ? new List<LinkAuraData>(clone.LinkAuras.Select(a => new LinkAuraData { stat = a.stat, value = a.value, keyword = a.keyword }))
                : new List<LinkAuraData>();
            data.ArrowDirections = clone.ArrowDirections;
            GameBoard.LinkAuraSystem.InvalidateCache(); // 光环 live-query 缓存失效重算（TransferEquipment 同惯例）

            // 变为结界形态：自动非横置（结界无横置态）+ 按入场同口径初始化耐久层
            //（2026-10-17 定案：结界一律走耐久经济——TotalDurabilityOf 单源，不以印刷>0 为闸）
            if (clone.Supertype == Cardtype.Enchantment)
            {
                self._isTapped = false;
                if (self.GetCounterCount(CounterRules.DurabilityCounter) <= 0)
                    self.AddCounters(CounterRules.DurabilityCounter,
                        CardCompositionCost.TotalDurabilityOf(clone));
            }

            // 变形指示物（中性 Resident，层恒 1）：解除口=换区/净化清（ClearCounters 钩子）
            self.AddCounters(CounterRules.MorphStateCounter, 1, source);
            EventManager.Instance.Publish(new CounterChangedEvent
            {
                Target = self,
                CounterType = CounterRules.MorphStateCounter,
                Amount = 1,
                Source = source,
            });
            EventManager.Instance.Publish(new KeywordAppliedEvent
            {
                Target = self,
                Keyword = "临摹",
                Detail = $"临摹为 {clone.CardName}（跳过其规则光环；发动主动效果按变形前后费差以任意元素补差 {surcharge}）",
                Source = source,
            });

            // 触发式随形态重挂：旧注册整卡注销（首个消费者）+ 新效果现转注册（幂等）
            ReregisterTriggers(self);
            return true;
        }

        /// <summary>
        /// 解除变形（由 CounterRules.ClearCounters 在变形指示物被清时调用）：纯回滚原形态——
        /// 不发入场事件、不做入场关键词刷新、不重置横置（还原变形前横置态）；
        /// 在场时重注册原触发式（幂等）；计数清理由发起本次清除的 sweep 完成（含变形期间挂的耐久层）。
        /// 未变形返回 false。
        /// </summary>
        public static bool RestoreIfMorphed(Card card)
        {
            if (card == null || !card._morphSnapshot.HasValue) return false;

            var snap = card._morphSnapshot.Value;
            card._morphSnapshot = null;

            card.ID = snap.Id;
            if (card is IHasName named) named.CardName = snap.CardName;
            if (card is IHasSupertype st) st.Supertype = snap.Supertype;
            card._power = snap.Power;
            card._life = snap.Life;
            card._maxLife = snap.MaxLife;
            card._baseCost = snap.BaseCost;
            card._isTapped = snap.WasTapped; // 变形前横置态还原（结界形态的非横置不带走）
            card._keywords.Clear();
            card._keywords.AddRange(snap.Keywords);
            card._keywordGrants.Clear();
            if (snap.Grants != null)
                card._keywordGrants.AddRange(snap.Grants.Select(g => new KeywordGrant
                {
                    Keyword = g.Keyword,
                    Lane = g.Lane,
                    Source = g.Source,
                    Value = g.Value,
                    Limit = g.Limit,
                }));
            var data = (card as CardWrapper)?.GetData();
            if (data != null)
            {
                data.Effects = snap.Effects;
                data.LinkAuras = snap.LinkAuras;
                data.ArrowDirections = snap.ArrowDirections;
                GameBoard.LinkAuraSystem.InvalidateCache();
            }

            EventManager.Instance.Publish(new KeywordAppliedEvent
            {
                Target = card,
                Keyword = "临摹",
                Detail = $"变形解除——还原原形态 {snap.CardName}（纯回滚：状态/横置/文本效果恢复，无入场刷新）",
                Source = null,
            });

            // 在场（净化解除）才需要重挂原触发式；换区解除时新区域不需要注册，
            // 再入场由入场统一出口按还原后的 data 重注册
            if (card.GetZone() == Zone.Battlefield)
                ReregisterTriggers(card);
            return true;
        }

        /// <summary>发动补价（2026-10-23 开放元素限制——任意元素可补差）：被变形卡当前应补的差额点数；未变形=0。
        /// 消费口=EffectExecutor.CanActivate 费用预检与 ExecuteAsync 现付段（并入灰档——六色支付链皆可垫付；
        /// 仅主动——触发式预付不经此）。</summary>
        public static int ActivationSurcharge(Entity source)
            => source is Card c && c._morphSnapshot.HasValue ? c._morphSnapshot.Value.Surcharge : 0;

        /// <summary>触发式重挂：整卡注销旧注册 + 按当前 data 现转注册（主动每帧现转，无需此处）。</summary>
        private static void ReregisterTriggers(Card card)
        {
            var core = GameCore.Instance;
            var controller = card.GetController();
            if (core == null || controller == null) return;
            core.TriggerEngine.UnregisterEntityEffects(card);
            GameActions.RegisterCardTriggeredEffects(core, card, controller);
        }

        // ==================== 规则光环过滤（跳过唯一效果——2026-10-13 口径：仅规则光环） ====================

        /// <summary>效果列表深拷贝件上滤除规则光环（ModifyGameRule 原子步——运行时唯一同名互斥源）；
        /// 滤后空效果整条丢弃。唯一门分支只是构筑约束+结算条件，照常保留。</summary>
        private static List<CardEffectData> FilterRuleAuras(List<CardEffectData> effects)
        {
            if (effects == null) return new List<CardEffectData>();
            var kept = new List<CardEffectData>();
            foreach (var fx in effects)
            {
                if (fx == null) continue;
                if (fx.Steps != null) FilterSteps(fx.Steps);
                if (fx.AtomicEffects != null) FilterEntries(fx.AtomicEffects);
                // 滤后既无 Steps 也无扁平原子 = 空效果，丢弃
                if ((fx.Steps == null || fx.Steps.Count == 0)
                    && (fx.AtomicEffects == null || fx.AtomicEffects.Count == 0)) continue;
                kept.Add(fx);
            }
            return kept;
        }

        /// <summary>步骤序列过滤：kind==0 原子步查表剔规则光环；分支步剔 then/else 内规则光环；抉择步递归各模式。</summary>
        private static void FilterSteps(List<EffectStepData> steps)
        {
            if (steps == null) return;
            steps.RemoveAll(s => s != null && s.kind == 0 && IsRuleAuraEntry(s.atomic));
            foreach (var s in steps)
            {
                if (s == null) continue;
                if (s.kind == 1)
                {
                    FilterEntries(s.thenSteps);
                    FilterEntries(s.elseSteps);
                }
                else if (s.kind == 2 && s.choices != null)
                    foreach (var c in s.choices)
                        FilterSteps(c?.steps);
            }
        }

        /// <summary>扁平原子条目过滤（含槽级分支载荷 then 奖励内的规则光环）。</summary>
        private static void FilterEntries(List<AtomicEffectEntry> entries)
        {
            if (entries == null) return;
            entries.RemoveAll(e => e != null && IsRuleAuraEntry(e));
            foreach (var e in entries)
                if (e?.branch?.then != null)
                    e.branch.then.RemoveAll(a => a != null && IsRuleAuraEntry(a));
        }

        /// <summary>原子条目是否规则光环步：refId → 表行 EnumName（英文 EffectType）== ModifyGameRule
        /// （合成器 IsRitualStepRow 同口径——EffectType 单一来源）。</summary>
        private static bool IsRuleAuraEntry(AtomicEffectEntry entry)
        {
            var enumName = AtomicEffectTable.GetByHashId(entry?.refId)?.EnumName;
            return enumName == nameof(AtomicEffectType.ModifyGameRule);
        }
    }
}

namespace CardCore
{
    /// <summary>Card 变形钩子字段（partial 与原 Card 声明同命名空间 CardCore）</summary>
    public partial class Card
    {
        /// <summary>变形钩子：null = 未变形；非 null = 处于变形状态（存原形态+纯回滚快照）</summary>
        internal Attribute.MorphSnapshot? _morphSnapshot = null;
    }
}
