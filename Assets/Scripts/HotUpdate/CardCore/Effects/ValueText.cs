using System.Text.RegularExpressions;

namespace CardCore.Effects
{
    // =====================================================================================
    // 值文本色（2026-11-13《战场数值表现统一修改计划》P4·D7 §4.7）：战报行内数值 token 的
    // 染色单源——与浮字（BattleFieldView.StatUp/DownColor）同调色板（hex 换算已对账：
    // #6BFF73=(0.42,1,0.45)、#FF6152=(1,0.38,0.32)）。
    //
    // 消费口径：MatchLogRenderer.Render(richValues:true) 出口统一套色（渲染单源机械替换，
    // 不在 UI 层猜极性）；richValues=false（导出/控制台/验证器）零标签纯文本。
    // 行极性由渲染 case 标注（Line.Polarity）：+1 增益/−1 减益/0 中性（骨架行不染）。
    // =====================================================================================
    public static class ValueText
    {
        /// <summary>增益绿（与 StatUpColor 同值）。</summary>
        public const string UpHex = "#6BFF73";
        /// <summary>减益红（与 StatDownColor 同值）。</summary>
        public const string DownHex = "#FF6152";
        /// <summary>中性灰（§4.7——骨架/中性行数值不上色时的兜底，现主要留锚用）。</summary>
        public const string NeutralHex = "#E6E6E6";

        /// <summary>极性 → 色标签（0 中性=不加色，返回原文）。</summary>
        public static string Span(string text, int polarity)
        {
            if (string.IsNullOrEmpty(text) || polarity == 0) return text;
            return $"<color={(polarity > 0 ? UpHex : DownHex)}>{text}</color>";
        }

        // 数值 token 模式（渲染器产出行文的全集，一次扫描避免嵌套重复染色）：
        //  1) 旧→新对：只染新值（旧值是"从哪来"，染色误导）
        //  2) 带符号数（P +2 / +2/+2 / （+2）内 / 吸收 +3）
        //  3) 裸数（回复 4 生命 / 受到 5 点伤害 / 失去 3 层）——按行极性
        private static readonly Regex ValueTokens = new Regex(
            @"(?<old>\d+)(?<arrow>→)(?<new>\d+)|(?<signed>[＋+−-]\d+)|(?<bare>\d+)",
            RegexOptions.Compiled);

        /// <summary>行内数值 token 染色（polarity=0 原文返回）。机械替换不猜语义——
        /// 行极性由渲染 case 定（§4.7「渲染期染色，不做 UI 正则猜极性」）。</summary>
        public static string Colorize(string text, int polarity)
        {
            if (string.IsNullOrEmpty(text) || polarity == 0) return text;
            return ValueTokens.Replace(text, m =>
            {
                if (m.Groups["arrow"].Success)
                    return m.Groups["old"].Value + m.Groups["arrow"].Value + Span(m.Groups["new"].Value, polarity);
                if (m.Groups["signed"].Success)
                    return Span(m.Groups["signed"].Value, polarity);
                return Span(m.Groups["bare"].Value, polarity);
            });
        }
    }
}
