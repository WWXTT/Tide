using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCore.Attribute
{
    /// <summary>
    /// 变形快照——记录变形前形态，离开战场时恢复（术语定案：变形不触发死亡；
    /// 变形后的单位死亡进墓，移动触发解除，进墓后变回原随从）。
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
    }

    /// <summary>
    /// 变形机制：把一张在场卡完全复制为目标形态（除解除钩子外），
    /// 并在其离开战场（ZoneContainer.Move 的 from==Battlefield 分支）时恢复原形态。
    /// 封印（翻面覆盖、不移动）不会触发解除——解除只挂在「移动」上。
    /// </summary>
    public static class MorphSystem
    {
        /// <summary>
        /// 目标形态解析器：变形原子的 StringValue（目标卡 ID）→ CardData。
        /// 由组合根注入（CardCatalog.GetById）；null 时变形静默跳过。
        /// （与 CombatSystem.AdjacentResolver 同款「静态扩展点 + 组合根接线」模式。）
        /// </summary>
        public static Func<string, CardData> ResolveMorphTarget;

        /// <summary>是否处于变形状态</summary>
        public static bool IsMorphed(this Card card) => card != null && card._morphSnapshot.HasValue;

        /// <summary>
        /// 变形：把卡完全复制为目标形态（ID/名称/类别/身材/费用/关键词），
        /// 同时快照原形态。已变形的不叠加。运行时状态（指示物/横置/控制器/区域）保留。
        /// </summary>
        public static bool MorphInto(this Card card, CardData target)
        {
            if (card == null || target == null || card._morphSnapshot.HasValue) return false;

            // 快照原形态
            card._morphSnapshot = new MorphSnapshot
            {
                Id = card.ID,
                CardName = (card as IHasName)?.CardName,
                Supertype = (card as IHasSupertype)?.Supertype ?? Cardtype.Creature,
                Power = card._power,
                Life = card._life,
                MaxLife = card._maxLife,
                BaseCost = card._baseCost,
                Keywords = new List<string>(card._keywords),
            };

            // 完全复制目标形态
            card.ID = target.ID;
            if (card is IHasName named) named.CardName = target.CardName;
            if (card is IHasSupertype st) st.Supertype = target.Supertype;

            CardCostService.EnsureCost(target); // 直构 CardData 缺 costList 时补建议档位
            card._power = target.Power ?? 0;
            card._life = target.Life ?? 1;
            card._maxLife = card._life;
            card._baseCost = target.Cost != null ? (int)target.Cost.Values.Sum() : 0;

            card._keywords.Clear();
            if (target.Keywords != null)
                card._keywords.AddRange(target.Keywords);

            return true;
        }

        /// <summary>
        /// 解除变形：恢复原形态并清空钩子。未变形返回 false。
        /// 由离开战场（移动）路径调用——死亡进墓后变回原随从。
        /// </summary>
        public static bool TryEndMorph(this Card card)
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
            card._keywords.Clear();
            card._keywords.AddRange(snap.Keywords);

            return true;
        }
    }
}

namespace CardCore
{
    /// <summary>Card 变形钩子字段（partial 与原 Card 声明同命名空间 CardCore）</summary>
    public partial class Card
    {
        /// <summary>变形钩子：null = 未变形；非 null = 处于变形状态（存原形态）</summary>
        internal Attribute.MorphSnapshot? _morphSnapshot = null;
    }
}
