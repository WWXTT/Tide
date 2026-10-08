using System;
using System.Linq;

namespace CardCore
{
    /// <summary>
    /// 可装载范围（2026-09-11 定案；2026-10-05 两槽改版全量重排）：
    /// 原子表 MountKinds 列（CSV 多选，同 TargetKinds 惯例）。
    ///
    /// 2026-10-07 晚「可以→仅可」全面转换（语义由可并集白名单转为分类位）：
    /// - 删值 ActiveEffect（入效果栏）：主干资格改派生（ComposerCatalog.CanBeTrunkRow——
    ///   非规则光环∪非仅连接光环；引擎行走 EngineTrunk 位；系统/攻守行有资格但被库过滤隐藏）。
    /// - 删值 BranchReward（分支奖励）：奖励资格改派生（CanBeRewardRow=派生主干资格∩非引擎行，
    ///   预算/可逆转过滤照旧——84 行声明全删，行为等价）。
    /// - RandomMount（随机幅度）语义翻转为 NoRandom（不可随机）黑名单：
    ///   含 {value} 的原子默认可随机（数值随机滑条），标此位禁随机；无 {value} 模板天然无滑条。
    /// - 守护/坚韧曾收为仅连接光环——2026-10-08 双双退出：坚韧指示物化（ToughnessCounter）、
    ///   守护改配对制关键词（位 8 拉黑）；关键词行=印刷/效果栏授予与光环条目两条通道。
    /// 余位按序重排为 0..6；关键词默认可作连接光环（位 8 黑名单拉黑，名单在表）/
    /// 位 3 可以作为连接光环=一般效果行光环化源（属性增加/减少，属性条目表驱动来源）；
    /// 位 7 仅可已删（枚举留空缺防旧数字数据误读）；
    /// 指示物与一般效果（未标位 3）不可（CanMountAsAura 统一判定）。
    /// </summary>
    public enum MountKind
    {
        /// <summary>关键词行（仅可）：卡面印刷+授予双通道，目标侧仅生物（表行 TargetFilter=NoRole 表达）；
        /// 授予形态入效果栏由派生主干资格承载，落槽域由 GrantTargetKinds 覆写为双方生物。
        /// 2026-10-07 晚定案：关键词**默认可作连接光环**（ComposerCatalog.CanMountAsAura），
        /// 黑名单走位 8（名单在表：守护/法术护盾/再生/禁魔石——与光环 live-query 持续语义冲突）。</summary>
        Keyword = 0,
        /// <summary>指示物行（2026-10-07 与关键词同型定案）：效果作用自身（表行 TargetKinds 存储态="自己"），
        /// 赋予语义可赋予任意卡（合成器落槽域统一=双方单位/手牌/牌库 {1..8}）；
        /// 持续与计价由指示物规格（CounterSpec）承载，效果级持续档不生效。</summary>
        Counter = 1,
        /// <summary>不可随机（黑名单）：标此位的行禁数值随机；未标且描述含 {value} 的行默认可随机。</summary>
        NoRandom = 2,
        /// <summary>可以作为连接光环（一般效果行的**光环化源**标记，2026-10-07 深夜定案）：属性增加/
        /// 属性减少两行声明此位（自指示物类重分类）——光环模式属性条目的来源（RefreshAuraLibrary 表驱动）。
        /// 设置攻击力/设置生命值/设置费用=纯系统行不标；关键词行不声明此位（默认可，拉黑走位 8）。</summary>
        LinkAura = 3,
        /// <summary>系统内部用：声明该位的行**移出玩家合成器全部选择面**
        /// （效果合成器原子库/分类、卡合成器代价候选）——引擎执行/计价代表原子/装载照常运转。
        /// 首批：修改攻击力/修改生命值/修改费用（数值修改原语，系统侧计价与运行时消费）。</summary>
        SystemInternal = 4,
        /// <summary>引擎主干行：六引擎行（倒计时/运势/拼点/死亡计数/元素充盈/手牌序位）的填充资格位
        /// ——填入槽即自由分支（无 gate-row，直接引擎参数+奖励槽）；行为条件载体：
        /// 零锚价、零域、不入主序列执行、无处理器。与效果栏主干互斥由派生谓词保证（引擎行不作 Then 奖励）。</summary>
        EngineTrunk = 5,
        /// <summary>规则光环族：仪典行统一标记（仅可作为规则光环——光环模式库与形态识别 IsRuleAuraRow 读此位）；
        /// 走 steps 原子步（ModifyGameRule），不占连接位、无箭头。</summary>
        RuleAura = 6,
        /// <summary>不可作为连接光环（黑名单，2026-10-07 晚定案；2026-10-08 名单随指示物化刷新）：
        /// 声明此位 = 永不可作光环条目（CanMountAsAura 一票否决）。名单在表不在代码——
        /// 现值：守护（配对制弹选目标）/法术护盾（真消耗型——生效后移除，live-query 不物化
        /// → RemoveKeyword 空操作 → 等效永久持有）/再生/禁魔石。
        /// 潜行/圣盾/复生已指示物化（2026-10-08）退出关键词族，不再经本位拉黑；
        /// 隐密无移除口=持续型，不拉黑（默认可）。
        /// 历史注：位 7「仅可以作为连接光环」已删（2026-10-07 深夜，枚举留空缺防旧数字数据误读）。</summary>
        NoLinkAura = 8,
    }

    /// <summary>MountKinds CSV 解析/判定辅助（AtomicEffectTable 与装载校验共用）。</summary>
    public static class MountKindExtensions
    {
        /// <summary>成员中文名（2026-10-06 表列中文化定案：原子表开放给玩家，MountKinds 列写中文 CSV；
        /// 下标=枚举序，与枚举成员一一对应，新增成员须同步补名）。</summary>
        private static readonly string[] ZhNames =
        {
            "关键词",   // 0 Keyword
            "指示物",   // 1 Counter
            "不可随机", // 2 NoRandom（2026-10-07 自「随机幅度」翻转）
            "可以作为连接光环", // 3 LinkAura（2026-10-07 晚自「连接光环」改名——多通道之一）
            "系统",     // 4 SystemInternal
            "引擎主干", // 5 EngineTrunk
            "规则光环", // 6 RuleAura
            "仅可以作为连接光环", // 7 LinkAuraOnly（已删——坚韧/守护回归关键词族，占位保索引对齐）
            "不可作为连接光环", // 8 NoLinkAura（2026-10-07 晚：消耗型关键词黑名单）
        };

        /// <summary>成员中文名（越界回退枚举名）。</summary>
        public static string ZhNameOf(MountKind kind)
        {
            int i = (int)kind;
            return i >= 0 && i < ZhNames.Length ? ZhNames[i] : kind.ToString();
        }

        /// <summary>枚举集 → 中文 CSV（升序去重，兜底字面量/展示用；持久哈希口径仍走数字）。</summary>
        public static string ZhCsv(System.Collections.Generic.IEnumerable<MountKind> kinds)
            => kinds == null ? "" : string.Join(",",
                kinds.Distinct().OrderBy(k => k).Select(ZhNameOf));

        /// <summary>解析 CSV（双轨：中文优先，序号数字回退——表数据为中文，数字轨供
        /// 验证器夹具/过渡数据兼容）。"关键词,连接光环" 或 "0,3" → {Keyword, LinkAura}；
        /// 空串=空集=普通行（无特殊位，主干/奖励资格走派生），消费方自行兜底。</summary>
        public static System.Collections.Generic.HashSet<MountKind> ParseCsv(string csv)
        {
            var set = new System.Collections.Generic.HashSet<MountKind>();
            if (string.IsNullOrEmpty(csv)) return set;
            foreach (var tok in csv.Split(','))
            {
                var t = tok.Trim();
                if (t.Length == 0) continue;
                int zh = System.Array.IndexOf(ZhNames, t);
                if (zh >= 0 && Enum.IsDefined(typeof(MountKind), zh))
                {
                    set.Add((MountKind)zh);
                    continue;
                }
                if (int.TryParse(t, out int v) && Enum.IsDefined(typeof(MountKind), v))
                    set.Add((MountKind)v);
            }
            return set;
        }
    }
}
