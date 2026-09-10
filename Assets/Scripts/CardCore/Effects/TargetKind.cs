using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCore
{
    /// <summary>
    /// 目标种类（M1 目标域模型 2026-09-10 定案）：作用区域的显式编号taxonomy，
    /// 取代旧 EffectTargetType(10值)+TargetFilter 分区 token+EffectTargetScope(死)。
    ///
    /// 两种根本划分：
    /// - 战场单位（0-3）：生命性（可摧毁=有生命 / 可消灭=无生命）× 归属（己方/对方）。
    ///   角色=有生命单位（世界观定案），不单列。生物/结界在最早发动时点已身在战场——
    ///   "指向自己"=从己方单位域选源卡，无"自身"种类（自身是组合层 SelectionMode）。
    ///   瞬间在发动区=非场上单位：不可被单位域指向，只接卡指向效果（14/15）——由域本身保证。
    /// - 功能区域卡（4-15）：作用对象是卡不是单位。地牌在元素池（12/13），
    ///   无生命域（2/3）只含战场非生物。
    ///
    /// 组合语义：原子列可指向目标全集（TargetKinds），组合效果的可作用范围=成员原子域的
    /// **交集**（不是并集）；交集空 → 构筑期拦截；运行时候选空 → 不可发动。
    /// 属性细化过滤（Untapped/Power&gt;N 等）保留为域内 filter，不进序号。
    ///
    /// 值一经分配只许尾部追加、永不重排（JSON/表列存裸 int）。
    /// </summary>
    public enum TargetKind : int
    {
        /// <summary>己方有生命单位（战场生物 + 己方角色）</summary>
        OwnLivingUnit = 0,
        /// <summary>对方有生命单位（对方生物 + 对方角色）</summary>
        EnemyLivingUnit = 1,
        /// <summary>己方无生命单位（战场结界等非生物持久物）</summary>
        OwnNonLivingUnit = 2,
        /// <summary>对方无生命单位</summary>
        EnemyNonLivingUnit = 3,

        /// <summary>己方手牌（卡）</summary>
        OwnHand = 4,
        /// <summary>对方手牌（卡）</summary>
        EnemyHand = 5,
        /// <summary>己方牌库（卡）</summary>
        OwnDeck = 6,
        /// <summary>对方牌库（卡）</summary>
        EnemyDeck = 7,
        /// <summary>己方坟场（卡）</summary>
        OwnGraveyard = 8,
        /// <summary>对方坟场（卡）</summary>
        EnemyGraveyard = 9,
        /// <summary>己方除外区（卡）</summary>
        OwnExile = 10,
        /// <summary>对方除外区（卡）</summary>
        EnemyExile = 11,
        /// <summary>己方元素池（地牌卡）</summary>
        OwnElementPool = 12,
        /// <summary>对方元素池（地牌卡）</summary>
        EnemyElementPool = 13,
        /// <summary>己方发动区（被使用的卡——反制指向落点）</summary>
        OwnActivation = 14,
        /// <summary>对方发动区（被使用的卡）</summary>
        EnemyActivation = 15,
    }

    /// <summary>组合效果的目标选择模式（域=能指什么；模式=怎么选）。</summary>
    public enum SelectionMode : int
    {
        /// <summary>无目标（DrawCard 等）：域为空集，不参与交集约束。</summary>
        None = -1,
        /// <summary>自身：源卡在组合域内校验，取源卡为唯一目标。</summary>
        Self = 0,
        /// <summary>手动：TargetSelectionService 交互选取（TargetCount/DynamicTargetCount 管数量）。</summary>
        Manual = 1,
        /// <summary>全域：候选全取（旧 All/AllAllies/AllEnemies/Random 的运行时行为）。</summary>
        Full = 2,
    }

    /// <summary>TargetKind 静态规则：区域映射、类别判定、解析与格式化（表列/JSON 存逗号分隔 int 串）。</summary>
    public static class TargetKindRules
    {
        /// <summary>是否为卡种类（功能区域）；false = 战场单位种类。</summary>
        public static bool IsCardKind(int kind)
            => kind >= (int)TargetKind.OwnHand && kind <= (int)TargetKind.EnemyActivation;

        /// <summary>是否为战场单位种类。</summary>
        public static bool IsUnitKind(int kind)
            => kind >= (int)TargetKind.OwnLivingUnit && kind <= (int)TargetKind.EnemyNonLivingUnit;

        /// <summary>是否归属对方（序号奇偶：单位 0/2=己方 1/3=对方；卡域成对 odd=对方）。</summary>
        public static bool IsEnemySide(int kind)
            => kind == (int)TargetKind.EnemyLivingUnit
            || kind == (int)TargetKind.EnemyNonLivingUnit
            || (IsCardKind(kind) && kind % 2 == 1);

        /// <summary>卡种类 → (Zone, 是否己方)。单位种类返回 (None, false) 无意义值。</summary>
        public static (Zone zone, bool own) ZoneOf(int kind)
        {
            bool own = !IsEnemySide(kind);
            switch (kind)
            {
                case (int)TargetKind.OwnHand:
                case (int)TargetKind.EnemyHand: return (Zone.Hand, own);
                case (int)TargetKind.OwnDeck:
                case (int)TargetKind.EnemyDeck: return (Zone.Deck, own);
                case (int)TargetKind.OwnGraveyard:
                case (int)TargetKind.EnemyGraveyard: return (Zone.Graveyard, own);
                case (int)TargetKind.OwnExile:
                case (int)TargetKind.EnemyExile: return (Zone.Exile, own);
                case (int)TargetKind.OwnElementPool:
                case (int)TargetKind.EnemyElementPool: return (Zone.ElementPool, own);
                case (int)TargetKind.OwnActivation:
                case (int)TargetKind.EnemyActivation: return (Zone.Activation, own);
                default: return (Zone.None, false);
            }
        }

        /// <summary>某功能区域的两侧种类（己方, 对方）。单位区域返回单位四种类。</summary>
        public static int[] BothSides(Zone zone)
        {
            switch (zone)
            {
                case Zone.Hand: return new[] { (int)TargetKind.OwnHand, (int)TargetKind.EnemyHand };
                case Zone.Deck: return new[] { (int)TargetKind.OwnDeck, (int)TargetKind.EnemyDeck };
                case Zone.Graveyard: return new[] { (int)TargetKind.OwnGraveyard, (int)TargetKind.EnemyGraveyard };
                case Zone.Exile: return new[] { (int)TargetKind.OwnExile, (int)TargetKind.EnemyExile };
                case Zone.ElementPool: return new[] { (int)TargetKind.OwnElementPool, (int)TargetKind.EnemyElementPool };
                case Zone.Activation: return new[] { (int)TargetKind.OwnActivation, (int)TargetKind.EnemyActivation };
                default: return new[]
                {
                    (int)TargetKind.OwnLivingUnit, (int)TargetKind.EnemyLivingUnit,
                    (int)TargetKind.OwnNonLivingUnit, (int)TargetKind.EnemyNonLivingUnit,
                };
            }
        }

        /// <summary>单位域（生命性 × 归属，战场）。All=true 取全部四类。</summary>
        public static int[] UnitKinds(bool livingOnly = false, bool enemyOnly = false, bool ownOnly = false)
        {
            var all = new[]
            {
                (int)TargetKind.OwnLivingUnit, (int)TargetKind.EnemyLivingUnit,
                (int)TargetKind.OwnNonLivingUnit, (int)TargetKind.EnemyNonLivingUnit,
            };
            IEnumerable<int> kinds = all;
            if (livingOnly) kinds = kinds.Where(k => k == (int)TargetKind.OwnLivingUnit || k == (int)TargetKind.EnemyLivingUnit);
            if (enemyOnly) kinds = kinds.Where(IsEnemySide);
            if (ownOnly) kinds = kinds.Where(k => !IsEnemySide(k));
            return kinds.ToArray();
        }

        /// <summary>逗号分隔 int 串解析（表列/UI 用）；null/空 = 空 集。</summary>
        public static List<int> Parse(string csv)
        {
            var result = new List<int>();
            if (string.IsNullOrEmpty(csv)) return result;
            foreach (var token in csv.Split(','))
            {
                if (int.TryParse(token.Trim(), out var kind) && Enum.IsDefined(typeof(TargetKind), kind))
                    result.Add(kind);
            }
            return result;
        }

        /// <summary>格式化为逗号分隔 int 串（升序去重——字节稳定，身份哈希同款口径）。</summary>
        public static string Format(IEnumerable<int> kinds)
            => kinds == null ? "" : string.Join(",", kinds.Distinct().OrderBy(k => k));

        /// <summary>域交集。null 或空集（无目标原子，如 DrawCard）= 不参与约束原样透传另一侧；
        /// 双侧非空取与——结果为空即"组合域为空"，构筑期拦截（返回空 List 供调用方判定）。</summary>
        public static List<int> Intersect(List<int> a, List<int> b)
        {
            bool unconstrainedA = a == null || a.Count == 0;
            bool unconstrainedB = b == null || b.Count == 0;
            if (unconstrainedA && unconstrainedB) return new List<int>();
            if (unconstrainedA) return new List<int>(b);
            if (unconstrainedB) return new List<int>(a);
            return a.Where(b.Contains).OrderBy(k => k).ToList();
        }

        /// <summary>全部有生命单位（双方角色+生物）——旧 Creature/Creature,Player/Player 域的迁移落点。</summary>
        public static int[] AllLivingUnits()
            => new[] { (int)TargetKind.OwnLivingUnit, (int)TargetKind.EnemyLivingUnit };
    }
}
