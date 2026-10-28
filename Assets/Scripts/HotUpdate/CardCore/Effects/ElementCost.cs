using System;
using System.Collections.Generic;
using System.Text;

namespace CardCore
{
    /// <summary>
    /// 费用的全链唯一表示（2026-10-04 定案：**位置数组**，下标 = ManaType 枚举序号）。
    ///
    /// 下标序 = 现行枚举序 [灰,红,蓝,绿,任意]（Gray=0 … Any=4，共 5 位）。2026-10-28 P2 席位收缩：
    /// White（旧 4）/ Black（旧 5）退役，Any 6→4；旧六/七长数组读入走**重排**迁移
    ///（new[0..3]=old[0..3]、new[4]=old[6]，白/黑位非零记 Error 后丢弃——直接截断会把旧
    /// 「白」错当新「任意」，禁止）。例：碾压红3 = [0,3,0,0,0]。
    ///
    /// 落盘形态统一为 float 位置数组（原子表 ManaList / Cards.json costList / Effects.json cost 同款）；
    /// 边界适配（FromLegacyDict/ToLegacyDict）供网络快照、事件 DTO 等仍持 Dictionary&lt;int,float&gt;
    /// 的内部传输结构过渡使用——核心链路（原子表→推导→卡 Cost→支付入口→UI 数据源）一律本类型。
    /// </summary>
    public sealed class ElementCost
    {
        /// <summary>数组长度 = ManaType 成员数（扩缩色只改枚举，这里自动跟随；现行 = 5）。</summary>
        public static readonly int Length;

        /// <summary>旧枚举序位（重排迁移用）：白/黑退役前的席位号，4/5 永久留空不回收。</summary>
        public const int RetiredWhiteSlot = 4;
        public const int RetiredBlackSlot = 5;

        static ElementCost()
        {
            Length = Enum.GetValues(typeof(ManaType)).Length;
        }

        /// <summary>位置数组本体（只读引用；内容可变——累加/缩放原地改）。</summary>
        public readonly float[] v;

        public ElementCost()
        {
            v = new float[Length];
        }

        /// <summary>从位置数组构建（null → 空费用）。2026-10-28 P2 席位收缩迁移：
        /// 长度 4（原子表新格式）→ 补零到 5；长度 5 → 原样；长度 6/7（旧档）→ **重排**
        /// new[0..3]=old[0..3]、new[4]=old[6]（六长无 Any 位则补 0）——old[4]/old[5]（白/黑）
        /// 非零记 Error 并按 0 处理（无兼容层，白黑已无处可去）；其它长度 → Error + 置零。</summary>
        public ElementCost(float[] source)
        {
            v = new float[Length];
            if (source == null) return;
            switch (source.Length)
            {
                case int n when n == Length:
                    for (int i = 0; i < n; i++) v[i] = source[i];
                    break;
                case int n when n >= 0 && n < Length:
                    // 短数组（4=原子表新格式等）补零
                    for (int i = 0; i < n; i++) v[i] = source[i];
                    break;
                case 6:
                case 7:
                    // 旧档 [灰,红,蓝,绿,白,黑,(任意)] → 重排，禁止截断（截断会把旧白位错当新任意位）
                    WarnRetiredSlots(source);
                    for (int i = 0; i < 4; i++) v[i] = source[i];
                    v[4] = source.Length > 6 ? source[6] : 0f;
                    break;
                default:
                    TideLog.Error($"[ElementCost] 位置数组长度 {source.Length} 非法（合法 0~7），已置零");
                    break;
            }
        }

        private static void WarnRetiredSlots(float[] source)
        {
            if (source[RetiredWhiteSlot] != 0f || source[RetiredBlackSlot] != 0f)
                TideLog.Error($"[ElementCost] 旧 {source.Length} 长位置数组含白/黑份额 " +
                              $"[白={source[RetiredWhiteSlot]},黑={source[RetiredBlackSlot]}]——席位已退役（2026-10-28），" +
                              "按 0 处理丢弃（无兼容层）");
        }

        /// <summary>单色便捷构造（如兜底行 Gray1）。</summary>
        public static ElementCost FromValue(ManaType color, float amount)
        {
            var c = new ElementCost();
            c.v[(int)color] = amount;
            return c;
        }

        public float this[ManaType m]
        {
            get => v[(int)m];
            set => v[(int)m] = value;
        }

        /// <summary>各色份额合计（原 TotalUnitCost 口径）。</summary>
        public float Total
        {
            get
            {
                float sum = 0f;
                for (int i = 0; i < Length; i++) sum += v[i];
                return sum;
            }
        }

        public bool IsZero
        {
            get
            {
                for (int i = 0; i < Length; i++)
                    if (v[i] != 0f) return false;
                return true;
            }
        }

        /// <summary>首个非零色（序数序，确定性——原 ManaList[0] 首项语义的替身）；全零 = Gray。</summary>
        public ManaType PrimaryColor
        {
            get
            {
                for (int i = 0; i < Length; i++)
                    if (v[i] != 0f) return (ManaType)i;
                return ManaType.Gray;
            }
        }

        /// <summary>序数序非零色枚举——显示/遍历的统一口径（取代字典插入序的旧不稳定口径）。</summary>
        public IEnumerable<ManaType> NonzeroColors()
        {
            for (int i = 0; i < Length; i++)
                if (v[i] != 0f) yield return (ManaType)i;
        }

        public ElementCost Clone()
        {
            var c = new ElementCost();
            Array.Copy(v, c.v, Length);
            return c;
        }

        /// <summary>原地累加另一份费用（桶聚合用）。</summary>
        public void Add(ElementCost other)
        {
            if (other == null) return;
            for (int i = 0; i < Length && i < other.v.Length; i++) v[i] += other.v[i];
        }

        /// <summary>原地缩放（比例拆分用）。</summary>
        public void Scale(float factor)
        {
            for (int i = 0; i < Length; i++) v[i] *= factor;
        }

        // ---- 边界适配（过渡）：网络快照/事件等仍持字典的内部结构 ----

        /// <summary>旧字典表示 → 位置数组（键=枚举 int；越界键忽略）。</summary>
        public static ElementCost FromLegacyDict(Dictionary<int, float> dict)
        {
            var c = new ElementCost();
            if (dict == null) return c;
            foreach (var kv in dict)
                if (kv.Key >= 0 && kv.Key < Length)
                    c.v[kv.Key] = kv.Value;
            return c;
        }

        /// <summary>位置数组 → 旧字典表示（只含非零项——装载形态口径）。</summary>
        public Dictionary<int, float> ToLegacyDict()
        {
            var dict = new Dictionary<int, float>();
            for (int i = 0; i < Length; i++)
                if (v[i] != 0f)
                    dict[i] = v[i];
            return dict;
        }

        /// <summary>计价内部桶（枚举键字典）→ 位置数组（越界键忽略）。</summary>
        public static ElementCost FromColorDict(Dictionary<ManaType, float> dict)
        {
            var c = new ElementCost();
            if (dict == null) return c;
            foreach (var kv in dict)
                c.v[(int)kv.Key] = kv.Value;
            return c;
        }

        /// <summary>位置数组 → 枚举键 int 字典（只含非零项；黑白获得/退款等 int 消费口）。</summary>
        public Dictionary<ManaType, int> ToColorDictInt()
        {
            var dict = new Dictionary<ManaType, int>();
            for (int i = 0; i < Length; i++)
                if (v[i] != 0f)
                    dict[(ManaType)i] = (int)v[i];
            return dict;
        }

        /// <summary>序数序无损文本（R 往返格式，invariant）——日志/断言/**指纹哈希**共用口径。</summary>
        public string DebugText()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < Length; i++)
            {
                if (v[i] == 0f) continue;
                if (sb.Length > 0) sb.Append(' ');
                sb.Append((ManaType)i).Append(':').Append(v[i].ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            }
            return sb.Length > 0 ? sb.ToString() : "0";
        }

        public override string ToString() => DebugText();
    }

    /// <summary>ManaType 中文名与反查（2026-10-06 单一来源定案）：原子表 Tags 表色追加/
    /// ElementAffinity 色名扫描/UI ColorFilter 共用。既有四处展示映射（ElementAffinity.GetColorName
    /// 的 Gray=「通用」、ColorFilter/GameActions/HeroSkillSystem 的「灰」）属各自展示语境不强制归一——
    /// **表列词表统一用本表的「灰」**。</summary>
    public static class ManaTypeNames
    {
        /// <summary>中文名（下标=枚举序 [灰,红,蓝,绿,任意]；白/黑已随 2026-10-28 P2 席位收缩退役，
        /// 词表同步删除——TryParseZh 反查「白」「黑」将返回 false，不再静默落旧席位）。
        /// 「任意」=Any 万能货币（2026-10-23）——表色追加/反查场景不应出现（费用不使用 Any），
        /// 进词表仅为展示完备。</summary>
        private static readonly string[] ZhNames =
        {
            "灰", // Gray
            "红", // Red
            "蓝", // Blue
            "绿", // Green
            "任意", // Any（万能货币，非地牌锁色色）
        };

        /// <summary>中文名（越界回退枚举名）。</summary>
        public static string ZhNameOf(ManaType m)
        {
            int i = (int)m;
            return i >= 0 && i < ZhNames.Length ? ZhNames[i] : m.ToString();
        }

        /// <summary>中文反查（词表外返回 false——不静默落灰）。</summary>
        public static bool TryParseZh(string name, out ManaType m)
        {
            m = default;
            if (string.IsNullOrEmpty(name)) return false;
            int i = Array.IndexOf(ZhNames, name.Trim());
            if (i < 0) return false;
            m = (ManaType)i;
            return true;
        }
    }
}
