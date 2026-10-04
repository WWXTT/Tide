using System;
using System.Collections.Generic;
using System.Text;

namespace CardCore
{
    /// <summary>
    /// 费用的全链唯一表示（2026-10-04 定案：**位置数组**，下标 = ManaType 枚举序号）。
    ///
    /// 下标序 = 现行枚举序 [灰,红,蓝,绿,白,黑]（Gray=0 … Black=5，不重排——已序列化的 int 色键
    /// 全部保序）。例：碾压红3 = [0,3,0,0,0,0]。扩色 = 枚举加成员，Length 随枚举自动加长，
    /// 表示层零改动；新色默认份额 0，旧数据语义不变。
    ///
    /// 落盘形态统一为 float 位置数组（原子表 ManaList / Cards.json costList / Effects.json cost 同款）；
    /// 边界适配（FromLegacyDict/ToLegacyDict）供网络快照、事件 DTO 等仍持 Dictionary&lt;int,float&gt;
    /// 的内部传输结构过渡使用——核心链路（原子表→推导→卡 Cost→支付入口→UI 数据源）一律本类型。
    /// </summary>
    public sealed class ElementCost
    {
        /// <summary>数组长度 = ManaType 成员数（扩色只改枚举，这里自动跟随）。</summary>
        public static readonly int Length;

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

        /// <summary>从位置数组构建（null → 空费用；短数组补零，长数组截断——解析容错在调用侧告警）。</summary>
        public ElementCost(float[] source)
        {
            v = new float[Length];
            if (source == null) return;
            int n = Math.Min(Length, source.Length);
            for (int i = 0; i < n; i++) v[i] = source[i];
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
}
