using System;
using System.Collections.Generic;
using System.Linq;
using CardCore.Attribute;
using Cysharp.Threading.Tasks;

namespace CardCore.Attribute.Handlers
{
    // ================================================================
    // 信息族（蓝色）：宣言（当前猜当前验证）/ 预言（隐藏押注，延迟验证）
    // 宣言对象两类（2026-10-29 阶段3c 定案）：类别=有限明确枚举域（生物/法术/结界 3 选 1）；
    // 卡名=开放集（Name 维度，奖励预算随之放大——类别 ≤1 费、卡名 ≤3 费）。
    //（旧「卡名等开放集不可作为宣言对象」定案随之退役。）
    // 2026-10-30 族改版：箭头宣言（DeclareArrow）整体退役（表行、handler、枚举位同批删除）；
    // DeclareHand 改「强夺」——宣言卡名，从对手手牌获得该卡；DeclareDeckTop 改「预言」——
    // 隐秘宣言类别，对手下回合首张同类卡发动无效（复用 ProphecySystem 延迟验证，
    // 命中奖励=内置发动无效原子）。两枚举名承袭旧槽位，语义以表行/handler 为准。
    // StringValue 编码 "维度:值"，如 Type:Creature / Name:青眼白龙 / Color:Red /
    // CostParity:Odd / CostExact:3 / LinkArrow:NE。
    // ================================================================

    /// <summary>
    /// 宣言维度注册表：维度名 + 域校验 + 对卡匹配置值。
    /// 新增维度 = 加一段 case（类别等有限域必须有限可枚举；Name 为开放集——任意非空卡名）。
    /// </summary>
    public static class ProphecyDimension
    {
        public const string Type = "Type";                 // Cardtype 三类（2026-10-29 收紧：Creature/Spell/Enchantment）
        public const string Name = "Name";                 // 卡名（开放集，2026-10-29 阶段3c 解禁——比 CardName/ID）
        public const string Color = "Color";               // ManaType（Red/Blue/Green/Gray）
        public const string CostParity = "CostParity";     // Odd / Even
        public const string CostExact = "CostExact";       // 0..7（费用受曲线封顶，天然有限）
        public const string LinkArrow = "LinkArrow";       // CardCore.HexDirection 单方向

        private const int CostExactMax = 7; // 与地牌槽上限 7 同源的封顶值（2026-10-13 缩盘 9→7）

        /// <summary>解析 "维度:值" 编码（无冒号或空串返回 false）</summary>
        public static bool TryParse(string encoded, out string dimension, out string value)
        {
            dimension = value = null;
            if (string.IsNullOrEmpty(encoded)) return false;
            int idx = encoded.IndexOf(':');
            if (idx <= 0 || idx >= encoded.Length - 1) return false;
            dimension = encoded.Substring(0, idx).Trim();
            value = encoded.Substring(idx + 1).Trim();
            return dimension.Length > 0 && value.Length > 0;
        }

        /// <summary>值是否属于该维度的有限域（目录/UI 下拉与运行时共用；Name=开放集恒真）</summary>
        public static bool IsValidValue(string dimension, string value)
        {
            switch (dimension)
            {
                case Type:
                    // 2026-10-29 阶段3c 收紧：类别猜法=生物/法术/结界 3 选 1
                    //（Artifact/Land 等超类型不入宣言域——猜中概率与奖励预算按 3 类校准）
                    return value.Equals(nameof(Cardtype.Creature), StringComparison.OrdinalIgnoreCase)
                           || value.Equals(nameof(Cardtype.Spell), StringComparison.OrdinalIgnoreCase)
                           || value.Equals(nameof(Cardtype.Enchantment), StringComparison.OrdinalIgnoreCase);
                case Name:
                    return !string.IsNullOrWhiteSpace(value); // 开放集：任意非空卡名
                case Color:
                    return Enum.TryParse<ManaType>(value, true, out _);
                case CostParity:
                    return value.Equals("Odd", StringComparison.OrdinalIgnoreCase)
                           || value.Equals("Even", StringComparison.OrdinalIgnoreCase);
                case CostExact:
                    return int.TryParse(value, out int n) && n >= 0 && n <= CostExactMax;
                case LinkArrow:
                    return Enum.TryParse<HexDirection>(value, true, out var dir)
                           && dir != HexDirection.None && dir != HexDirection.All
                           && (dir & (dir - 1)) == 0; // 单方向（Flags 枚举取单 bit）
                default:
                    return false;
            }
        }

        /// <summary>卡是否命中宣言（维度:值）。未知维度/非法值恒 false。</summary>
        public static bool Matches(Card card, string dimension, string value)
        {
            if (card == null || !IsValidValue(dimension, value)) return false;

            switch (dimension)
            {
                case Type:
                    return card is IHasSupertype st && st.Supertype == ParseEnum<Cardtype>(value);

                case Name:
                    // 卡名精确匹配（CardName 或卡 ID——合成器引用卡双写口径同源）
                    return card is IHasName named
                           && (string.Equals(named.CardName, value, StringComparison.Ordinal)
                               || string.Equals(card.ID, value, StringComparison.Ordinal));

                case Color:
                    if (!(card is IHasCost hasCost) || hasCost.Cost == null) return false;
                    var color = ParseEnum<ManaType>(value);
                    return hasCost.Cost[color] > 0f;

                case CostParity:
                {
                    int total = TotalCost(card);
                    bool odd = total % 2 == 1;
                    return value.Equals("Odd", StringComparison.OrdinalIgnoreCase) ? odd : !odd;
                }

                case CostExact:
                    return TotalCost(card) == int.Parse(value);

                case LinkArrow:
                    return card is CardWrapper wrapper
                           && wrapper.GetData().ArrowDirections.HasFlag(ParseEnum<HexDirection>(value));

                default:
                    return false;
            }
        }

        /// <summary>卡总费用（与 GameActions.GetCardCost 同默认：无费用按灰 1）</summary>
        private static int TotalCost(Card card)
        {
            if (card is IHasCost hasCost && hasCost.Cost != null)
                return (int)hasCost.Cost.Total;
            return 1;
        }

        private static T ParseEnum<T>(string value) where T : struct
        {
            Enum.TryParse<T>(value, true, out var result);
            return result;
        }
    }

    /// <summary>
    /// 强夺（2026-10-30 改版，承旧「宣言·确认手牌」槽位）：宣言一个卡名（StringValue 构筑期
    /// 预置，ProphecyDimension.Name 编码——卡名或卡 ID 双写口径同源），从对手手牌获得该卡。
    /// 命中 = 匹配卡跨玩家迁移进己方手牌并**永久改写持有者**（ChangeOwner 同款定案：此后
    /// 弹回/洗回进新主卡组手牌、死亡去新主墓地）；未命中（对手手牌无该卡/空手/非法编码）=
    /// 空手而归，DeclareHit=false 供紧邻分支判定。宣言与夺取结果公开（DeclareResolvedEvent
    /// 全量回执）。旧「系统随机翻牌验证/逐张递进揭示/Exposed 展示态」链路随改版整体退役。
    /// </summary>
    public class DeclareHandHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.DeclareHand;

        /// <summary>结算期宣言反问（2026-10-31 阶段C）：弹卡名输入窗替换构筑期预置；
        /// 取消/超时=本次效果不结算（已付费用不退）。AI/无头=预置即结果（无预置空转）。</summary>
        public override async UniTask ExecuteAsync(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            if (!await ProphecyHandlerUtil.AskCardNameAsync(effect, context,
                    "强夺：宣言一个卡名（命中则从对手手牌获得该卡）", nameEncoded: true))
                return;
            Execute(effect, context);
        }

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            context.LastOutcome.Declaration = effect.StringValue;
            var me = context.Controller;
            var opponent = me?.Opponent;
            var hand = ProphecyHandlerUtil.OpponentHand(context);

            // 宣言域校验：强夺只收卡名档（Name:开放集）；其余编码按未命中处理
            Card found = null;
            if (!ProphecyDimension.TryParse(effect.StringValue, out var dim, out var val)
                || dim != ProphecyDimension.Name || !ProphecyDimension.IsValidValue(dim, val))
            {
                TideLog.Warn(
                    $"[强夺] 非法宣言编码 '{effect.StringValue}'（强夺只收卡名 Name:卡名），作未命中处理");
            }
            else if (hand != null)
            {
                foreach (var c in hand)
                    if (ProphecyDimension.Matches(c, dim, val)) { found = c; break; }
            }

            bool hit = found != null;
            context.LastOutcome.DeclareHit = hit;

            if (!hit)
            {
                PublishEvent(new DeclareResolvedEvent
                {
                    Declaration = effect.StringValue,
                    Hit = false,
                    Controller = me,
                    RevealedCards = null,
                    Sampled = false,
                });
                return;
            }

            // 夺取：跨玩家手牌直移（ChangeControl 同款容器 Remove/Add 范式，不经 OnCardMoved）
            if (context.ZoneManager != null && opponent != null)
            {
                context.ZoneManager.GetZoneContainer(opponent)?.Remove(found, Zone.Hand);
                found.SetController(me);
                found._owner = me;             // 永久获得：归属随迁（弹回/洗回/死亡归新主）
                found._isRevealed = false;     // 入手重置宣言已展示标记（OnCardMoved 同款）
                context.ZoneManager.GetZoneContainer(me)?.Add(found, Zone.Hand);
                PublishEvent(new CardEnterHandEvent
                {
                    Player = me,
                    Card = found,
                    FromZone = Zone.Hand,      // 跨玩家手牌迁移——区域枚举无侧别，源=手牌
                    IsDraw = false,
                });
            }

            PublishEvent(new DeclareResolvedEvent
            {
                Declaration = effect.StringValue,
                Hit = true,
                Controller = me,
                RevealedCards = new List<Card> { found }, // 夺取即公开（卡离开对手手牌）
                Sampled = false,
            });
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect)
            => $"宣言{effect.StringValue}，从对手手牌获得该卡";
    }

    /// <summary>
    /// 窃取（2026-10-31 新增）：宣言一个卡名（StringValue 构筑期预置，ProphecyDimension.Name
    /// 编码——卡名或卡 ID 双写口径同源），从对手**牌库**获得该卡。与强夺（DeclareHand）同构、
    /// 源区换牌库：牌库为隐蔽有序区，按宣言卡名全库检索（顺序无意义），命中即跨玩家迁移进
    /// 己方手牌并永久改写持有者（_owner 随迁，同强夺定案）；未命中（牌库无该卡/空库/非法编码）
    /// = 空手而归，DeclareHit=false 供紧邻分支判定。宣言与窃取结果公开（DeclareResolvedEvent
    /// 全量回执——离开牌库即公开，同强夺口径）。
    /// </summary>
    public class DeclareDeckHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.DeclareDeck;

        /// <summary>结算期宣言反问（2026-10-31 阶段C，同强夺）：弹卡名输入窗替换构筑期预置；
        /// 取消/超时=本次效果不结算（费用不退）。AI/无头=预置即结果。</summary>
        public override async UniTask ExecuteAsync(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            if (!await ProphecyHandlerUtil.AskCardNameAsync(effect, context,
                    "窃取：宣言一个卡名（命中则从对手牌库获得该卡）", nameEncoded: true))
                return;
            Execute(effect, context);
        }

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            context.LastOutcome.Declaration = effect.StringValue;
            var me = context.Controller;
            var opponent = me?.Opponent;
            var deck = ProphecyHandlerUtil.OpponentDeck(context);

            // 宣言域校验：窃取只收卡名档（Name:开放集）；其余编码按未命中处理
            Card found = null;
            if (!ProphecyDimension.TryParse(effect.StringValue, out var dim, out var val)
                || dim != ProphecyDimension.Name || !ProphecyDimension.IsValidValue(dim, val))
            {
                TideLog.Warn(
                    $"[窃取] 非法宣言编码 '{effect.StringValue}'（窃取只收卡名 Name:卡名），作未命中处理");
            }
            else if (deck != null)
            {
                foreach (var c in deck)
                    if (ProphecyDimension.Matches(c, dim, val)) { found = c; break; }
            }

            bool hit = found != null;
            context.LastOutcome.DeclareHit = hit;

            if (!hit)
            {
                PublishEvent(new DeclareResolvedEvent
                {
                    Declaration = effect.StringValue,
                    Hit = false,
                    Controller = me,
                    RevealedCards = null,
                    Sampled = false,
                });
                return;
            }

            // 窃取：跨玩家牌库→手牌直移（强夺同款容器 Remove/Add 范式，不经 OnCardMoved）
            if (context.ZoneManager != null && opponent != null)
            {
                context.ZoneManager.GetZoneContainer(opponent)?.Remove(found, Zone.Deck);
                found.SetController(me);
                found._owner = me;             // 永久获得：归属随迁（弹回/洗回/死亡归新主）
                found._isRevealed = false;     // 牌库隐蔽无展示态，入手归零（强夺同款）
                context.ZoneManager.GetZoneContainer(me)?.Add(found, Zone.Hand);
                PublishEvent(new CardEnterHandEvent
                {
                    Player = me,
                    Card = found,
                    FromZone = Zone.Deck,      // 跨玩家牌库迁移——区域枚举无侧别，源=牌库
                    IsDraw = false,
                });
            }

            PublishEvent(new DeclareResolvedEvent
            {
                Declaration = effect.StringValue,
                Hit = true,
                Controller = me,
                RevealedCards = new List<Card> { found }, // 窃取即公开（卡离开对手牌库）
                Sampled = false,
            });
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect)
            => $"宣言{effect.StringValue}，从对手牌库获得该卡";
    }

    /// <summary>
    /// 预言（2026-10-30 改版，承旧「宣言·验牌库顶」槽位）：隐秘宣言一个类别（StringValue 构筑期
    /// 预置，ProphecyDimension.Type 编码——生物/法术/结界 3 选 1），押注经 ProphecySystem 延迟
    /// 验证：对手下回合**首张**出牌与宣言类别一致 → 该卡发动无效；不一致或整回合未出牌 = 落空。
    /// 命中奖励=内置发动无效原子（CardPlayEvent 时点被验证卡尚在发动区——施放路径送墓即
    /// cast 消费点中止，不付费不结算）。押注隐藏（ProphecyRegisteredEvent 不含宣言内容，
    /// 对手只知道"预言已立"；验证时刻 ProphecyResolvedEvent 公开）。非法编码照常登记，
    /// 验证时刻按未命中结算（与卡名预言同口径——组装期垃圾不卡死结算）。
    /// </summary>
    public class DeclareDeckTopHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.DeclareDeckTop;

        /// <summary>结算期宣言反问（2026-10-31 阶段C）：弹类别三选一（生物/法术/结界——定案只收
        /// 类别档）替换构筑期预置。隐秘性：押注内容只进 ProphecySystem（ProphecyRegisteredEvent
        /// 不含宣言）+ 请求只下发宣告方座位（MsgSelectRequest 定向），对手不可见。</summary>
        public override async UniTask ExecuteAsync(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            var encoded = await ProphecyHandlerUtil.AskDimensionAsync(effect, context,
                "预言：隐秘押注对手下回合首张出牌的类别", typeOnly: true);
            if (encoded == null) return;
            effect.StringValue = encoded;
            Execute(effect, context);
        }

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            context.LastOutcome.Declaration = effect.StringValue;

            if (!ProphecyDimension.TryParse(effect.StringValue, out var dim, out var val)
                || dim != ProphecyDimension.Type || !ProphecyDimension.IsValidValue(dim, val))
            {
                TideLog.Warn(
                    $"[预言] 非法宣言编码 '{effect.StringValue}'（预言只收类别 Type:Creature/Spell/Enchantment），将按未命中结算");
            }

            // 押注登记（复用 ProphecySystem 延迟验证）：命中 → 内置发动无效（Targets=被验证首张卡）；
            // 未命中 → Else 空 = 落空。紧邻的 ProphecyHit/Miss 分支若存在则由执行引擎另行打包登记。
            ProphecySystem.Register(new PendingProphecy
            {
                Declarer = context.Controller,
                Source = context.Source,
                Declaration = effect.StringValue,
                ConditionId = "ProphecyHit",
                Then = new List<AtomicEffectInstance>
                {
                    new AtomicEffectInstance { Type = AtomicEffectType.NegateActivation },
                },
                ZoneManager = context.ZoneManager,
                ElementPool = context.ElementPool,
            });
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect)
            => $"隐秘宣言{effect.StringValue}，对手下回合首张同类卡发动无效";
    }

    /// <summary>
    /// 预言·对手下回合首张（隐藏押注）：本 handler 只登记押注（校验并暂存宣言），
    /// 不写 DeclareHit。紧随其后的分支步骤由执行引擎识别为延迟验证，打包成
    /// PendingProphecy 注册到 ProphecySystem——对手下回合首张出牌时验证，
    /// 整回合未出牌作未命中走 else（用户定案）。
    /// </summary>
    public class ProphecyNextCardHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.ProphecyNextCard;

        /// <summary>结算期宣言反问（2026-10-31 阶段C）：弹维度选择（全量维度——类别/颜色/费用/
        /// 箭头/卡名，卡名项链文本输入）替换构筑期预置；押注隐秘（ProphecyRegisteredEvent 不含
        /// 宣言内容）。维度行无取消键（封闭选项），卡名输入取消=效果不结算。</summary>
        public override async UniTask ExecuteAsync(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            var encoded = await ProphecyHandlerUtil.AskDimensionAsync(effect, context,
                "卡名预言：押注对手下回合首张出牌的类别或卡名", typeOnly: false);
            if (encoded == null) return;
            effect.StringValue = encoded;
            Execute(effect, context);
        }

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            // 校验宣言域；非法宣言也照常登记（验证时刻按未命中结算，避免组装期垃圾卡死结算）
            if (!ProphecyDimension.TryParse(effect.StringValue, out var dim, out var val)
                || !ProphecyDimension.IsValidValue(dim, val))
            {
                TideLog.Warn(
                    $"[Prophecy] 非法宣言编码 '{effect.StringValue}'（应为 维度:值，且值在有限域内），预言将按未命中结算");
            }

            context.LastOutcome.Declaration = effect.StringValue;
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect) => $"预言对手下回合首张卡为{effect.StringValue}";
    }

    // ---------------- 共用小工具 ----------------

    /// <summary>宣言族共用小工具（PositionPicker 注入点已随 2026-10-30 强夺改版删除——
    /// 强夺按宣言卡名定位，无选位语义）。
    /// 2026-10-31 阶段C：新增结算期宣言反问两口（原子表「参数」列接 UI 的引擎侧载体）——
    /// AskCardNameAsync（CardName 行：检索/强夺/窃取）与 AskDimensionAsync（Dimension 行：
    /// 筛选展示/预言/卡名预言）。取消语义=效果不结算、已付费用不退（TargetSelectionService 定案）。</summary>
    public static class ProphecyHandlerUtil
    {
        /// <summary>卡名输入长度上限（输入窗 characterLimit 与网络 MaxLen 同源）。</summary>
        internal const int MaxCardNameLength = 32;

        internal static List<Card> OpponentHand(EffectExecutionContext context)
        {
            var opponent = context.Controller?.Opponent;
            if (opponent == null || context.ZoneManager == null) return null;
            try { return context.ZoneManager.GetCards(opponent, Zone.Hand); }
            catch (KeyNotFoundException) { return null; }
        }

        internal static List<Card> OpponentDeck(EffectExecutionContext context)
        {
            var opponent = context.Controller?.Opponent;
            if (opponent == null || context.ZoneManager == null) return null;
            try { return context.ZoneManager.GetCards(opponent, Zone.Deck); }
            catch (KeyNotFoundException) { return null; }
        }

        internal static bool MatchesDeclaration(string encoded, Card card)
        {
            return ProphecyDimension.TryParse(encoded, out var dim, out var val)
                   && ProphecyDimension.Matches(card, dim, val);
        }

        /// <summary>卡名宣言反问（CardName 行共用，2026-10-31 阶段C）：结算期弹文本输入窗，
        /// 玩家输入替换 StringValue（不再读构筑期预置）。默认值=旧预置（Name: 前缀按需解码/
        /// 裸名原样——检索旧口径），玩家清空=取消。AI/无头=默认值即结果（无默认 null 空转=旧行为）。
        /// 返回 true=已写回可继续结算；false=取消/超时（效果不结算、费用不退）。</summary>
        internal static async UniTask<bool> AskCardNameAsync(AtomicEffectInstance effect,
            EffectExecutionContext context, string title, bool nameEncoded)
        {
            if (effect == null || context == null || context.Controller == null) return false;

            string preset = effect.StringValue;
            string def = preset;
            if (nameEncoded && ProphecyDimension.TryParse(preset, out var dim, out var val)
                && dim == ProphecyDimension.Name)
                def = val; // 旧预置 "Name:卡名" → 输入窗只回填卡名本体

            string input = await TargetSelectionService.RequestTextAsync(
                context.Controller, title, def, MaxCardNameLength);
            if (input == null) return false;

            effect.StringValue = nameEncoded ? ProphecyDimension.Name + ":" + input : input;
            return true;
        }

        /// <summary>维度宣言反问（Dimension 行共用，2026-10-31 阶段C）：封闭维度走既有纯选项
        /// 反问（RequestOneIndexAsync——本地 Region 卡牌列表/网络 SelectRequest/无头 AI 自动取 0）；
        /// Name 开放集选项链文本反问（取消=整体取消）。合法预置 str 排位 0（默认/AI/超时兜底恒取）。
        /// typeOnly=true 只给类别三项（预言定案：Creature/Spell/Enchantment 三选一）。
        /// 返回 "维度:值" 编码；null=取消（效果不结算）。</summary>
        internal static async UniTask<string> AskDimensionAsync(AtomicEffectInstance effect,
            EffectExecutionContext context, string title, bool typeOnly)
        {
            if (effect == null || context == null || context.Controller == null) return null;

            var options = DimensionOptions(typeOnly);
            string preset = effect.StringValue;
            string presetName = null;
            if (ProphecyDimension.TryParse(preset, out var dim, out var val)
                && ProphecyDimension.IsValidValue(dim, val))
            {
                if (dim == ProphecyDimension.Name) presetName = val;
                else
                {
                    // 预置合法 → 挪到首位（默认值位；非法编码不排——按第一个合法值起）
                    string encoded = dim + ":" + val;
                    int idx = options.FindIndex(o => o.encoded == encoded);
                    if (idx > 0) { var hit = options[idx]; options.RemoveAt(idx); options.Insert(0, hit); }
                }
            }

            int pick = await TargetSelectionService.RequestOneIndexAsync(
                context.Controller, options.Select(o => o.label).ToList(), title);
            if (pick < 0 || pick >= options.Count) pick = 0; // 越界兜底（Service 已钳，双保险）

            if (options[pick].isNameEntry)
            {
                string name = await TargetSelectionService.RequestTextAsync(
                    context.Controller, title + "——输入卡名", presetName, MaxCardNameLength);
                return name == null ? null : ProphecyDimension.Name + ":" + name;
            }
            return options[pick].encoded;
        }

        /// <summary>封闭维度选项目录（编码+显示名；Name 以特殊项出现——开放集链文本输入）。</summary>
        private static List<(string encoded, string label, bool isNameEntry)> DimensionOptions(bool typeOnly)
        {
            var list = new List<(string, string, bool)>();
            if (typeOnly)
            {
                list.Add(("Type:Creature", "类别：生物", false));
                list.Add(("Type:Spell", "类别：法术", false));
                list.Add(("Type:Enchantment", "类别：结界", false));
                return list;
            }
            list.Add(("Type:Creature", "类别：生物", false));
            list.Add(("Type:Spell", "类别：法术", false));
            list.Add(("Type:Enchantment", "类别：结界", false));
            list.Add(("Color:Red", "颜色：红", false));
            list.Add(("Color:Blue", "颜色：蓝", false));
            list.Add(("Color:Green", "颜色：绿", false));
            list.Add(("Color:Gray", "颜色：灰", false));
            list.Add(("CostParity:Odd", "费用：奇数", false));
            list.Add(("CostParity:Even", "费用：偶数", false));
            for (int n = 0; n <= 7; n++)
                list.Add(("CostExact:" + n, $"费用：恰为 {n}", false));
            list.Add(("LinkArrow:Up", "箭头：上", false));
            list.Add(("LinkArrow:Down", "箭头：下", false));
            list.Add(("LinkArrow:UpperLeft", "箭头：左上", false));
            list.Add(("LinkArrow:UpperRight", "箭头：右上", false));
            list.Add(("LinkArrow:LowerLeft", "箭头：左下", false));
            list.Add(("LinkArrow:LowerRight", "箭头：右下", false));
            list.Add((ProphecyDimension.Name + ":", "卡名：手动输入…", true));
            return list;
        }
    }
}
