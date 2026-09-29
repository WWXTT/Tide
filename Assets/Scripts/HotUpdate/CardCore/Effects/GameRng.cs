using System;
using System.Collections.Generic;

namespace CardCore
{
    /// <summary>
    /// 对局随机服务（2026-09-13「两个随机」定案）：全场随机统一经此口——
    /// 随机目标抽取（RandomTarget 标志，2026-09-16 自 SelectionMode 移出）与数值随机（Value 幅度）都走这里，
    /// 种子可复播（M3 网络同种子对拍/回放的前提；验证器可钉种子做确定性断言）。
    /// 与 Zones 洗牌 _rng 同款哲学、各管各的（合流留到网络对拍统一收口）。
    /// 新增随机一律走 GameRng，不再散用 UnityEngine.Random（确定性地雷）。
    /// </summary>
    public static class GameRng
    {
        private static Random _rng = new Random();

        /// <summary>重播种子（同种子 → 同随机序列）。</summary>
        public static void Reseed(int seed) => _rng = new Random(seed);

        /// <summary>[min, max) 均匀整数。</summary>
        public static int Next(int minInclusive, int maxExclusive) => _rng.Next(minInclusive, maxExclusive);

        /// <summary>
        /// 从候选中不重复抽取 n 个（部分 Fisher-Yates）：n ≥ 候选数时全取（顺序打乱）。
        /// 返回新列表，不改入参顺序。n ≤ 0 返回空。
        /// </summary>
        public static List<T> PickN<T>(List<T> candidates, int n)
        {
            var result = new List<T>();
            if (candidates == null || candidates.Count == 0 || n <= 0) return result;

            var pool = new List<T>(candidates);
            int take = Math.Min(n, pool.Count);
            for (int i = 0; i < take; i++)
            {
                int j = _rng.Next(i, pool.Count);
                (pool[i], pool[j]) = (pool[j], pool[i]);
                result.Add(pool[i]);
            }
            return result;
        }

        /// <summary>
        /// 数值随机（2026-09-13 定案）：名义 value ± 幅度——span = round(|value| × amplitude)，
        /// 返回 [value − span, value + span] 均匀整数（3 伤 ±100% → 0..6）。
        /// amplitude ≤ 0 恒返回名义值（计价锚点不漂移：构筑/计价/描述读名义 Value，只有结算读掷值）。
        /// </summary>
        public static int RollValue(int value, float amplitude)
        {
            if (amplitude <= 0f) return value;
            int span = (int)Math.Round(Math.Abs(value) * amplitude, MidpointRounding.AwayFromZero);
            if (span <= 0) return value;
            return value + _rng.Next(-span, span + 1);
        }
    }
}
