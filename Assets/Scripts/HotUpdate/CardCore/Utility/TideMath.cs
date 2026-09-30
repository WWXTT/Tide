using System;

namespace CardCore
{
    /// <summary>
    /// Mathf 等价数学（2026-09-27 去 Unity 化）：共享源码里的取整辅助收口于此。
    /// 其余 Clamp/Max/Min/Pow/Log 直接用 System.Math（行为一致，无需包装）。
    /// RoundToInt 口径 = 四舍五入远离零（Unity Mathf 同款，区别于银行家舍入）。
    /// </summary>
    public static class TideMath
    {
        public static int RoundToInt(float f) => (int)Math.Round(f, MidpointRounding.AwayFromZero);
        public static int RoundToInt(double d) => (int)Math.Round(d, MidpointRounding.AwayFromZero);
        public static int CeilToInt(float f) => (int)Math.Ceiling(f);
        public static int FloorToInt(float f) => (int)Math.Floor(f);
    }
}
