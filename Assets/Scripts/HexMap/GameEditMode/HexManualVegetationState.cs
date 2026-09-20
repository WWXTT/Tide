using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace HexMap
{
    /// <summary>单格植被覆写方式：清除（该格无植被）或手动指定原型</summary>
    public enum HexVegOverrideMode
    {
        /// <summary>该格植被全部清除（压过散布规则——重散布后也不回填）</summary>
        Clear = 0,
        /// <summary>该格为手动放置（原型 + 每格株数；重散布后按记录重放）</summary>
        Manual = 1,
    }

    /// <summary>一格的手动植被覆写记录</summary>
    [Serializable]
    public struct HexVegOverride
    {
        public HexVegOverrideMode Mode;
        /// <summary>Manual：散布规则索引（HexMapFeatureSettings.scatterRules 下标）</summary>
        public int PrototypeIndex;
        /// <summary>Manual：每格株数（1..3）</summary>
        public int Count;
    }

    /// <summary>
    /// 手动植被覆写表（游戏内植被笔刷写 → 存档 Capture/Apply 与重散布后重放）。
    /// 仿 HexPoiRuntime 的静态运行时表：植被实例本体是 ECS 派生物（整批销毁重建），
    /// 只有本表能让手动植被活过 R 重生成 / V 重散布 / 存档往返。
    /// 重放方：HexVegetationSpawnSystem 每次散布完成后 ApplyManualOverrides。
    /// </summary>
    public static class HexManualVegetationState
    {
        /// <summary>offset → 覆写记录（无键 = 该格走散布规则）</summary>
        public static readonly Dictionary<int2, HexVegOverride> Overrides = new Dictionary<int2, HexVegOverride>();

        /// <summary>植被笔刷「清除」：销毁该格实例 + 记 Clear（压过之后的重散布）</summary>
        public static void RecordClear(int2 offset) =>
            Overrides[offset] = new HexVegOverride { Mode = HexVegOverrideMode.Clear, PrototypeIndex = -1, Count = 0 };

        /// <summary>植被笔刷「放置」：记 Manual（重散布后按记录重放）</summary>
        public static void RecordManual(int2 offset, int prototypeIndex, int count) =>
            Overrides[offset] = new HexVegOverride
            {
                Mode = HexVegOverrideMode.Manual,
                PrototypeIndex = prototypeIndex,
                Count = count,
            };

        /// <summary>存档 Apply：清空重灌（存档为唯一真相源）</summary>
        public static void ResetFrom(IEnumerable<KeyValuePair<int2, HexVegOverride>> source)
        {
            Overrides.Clear();
            if (source == null)
                return;
            foreach (var kv in source)
                Overrides[kv.Key] = kv.Value;
        }

        /// <summary>域重载关闭时防跨 Play 残留</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Overrides.Clear();
    }
}
