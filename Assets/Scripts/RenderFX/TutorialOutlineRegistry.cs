using System.Collections.Generic;
using UnityEngine;

namespace Tide.RenderFX
{
    /// <summary>
    /// 教学高亮目标登记表（2026-11-21 遮罩/高亮/暂停计划 v0.3 §4）：
    /// 热更侧（TutorialFocusController）把解析好的 renderer 集合+样式写入本表，
    /// 渲染侧 TutorialOutlineFeature 每帧读取渲染（登记表空=feature 零开销不挂 pass）。
    /// 跨程序集单源：Tide.RenderFX（AOT，供 RendererFeature 用）← Tide.HotUpdate（热更）写入。
    /// 域重载关闭防跨 Play 残留（BattleTileFx.ResetStatics 同口径）。
    /// </summary>
    public static class TutorialOutlineRegistry
    {
        /// <summary>一条高亮：目标 renderer 集合 + 样式（色/HDR/宽/脉动）。</summary>
        public struct Entry
        {
            public Renderer[] Renderers;
            public Color Color;
            public float WidthPx;    // 屏幕像素（1080 参考，C# 侧已按屏比换算）
            public float Hdr;        // >0.8 才能被 bloom 晕开
            public float PulseSpeed; // Hz（0=恒亮）
            public float PulseAmp;
        }

        private static readonly List<Entry> _entries = new List<Entry>();

        /// <summary>当前帧要描边的条目（渲染线程只读快照）。</summary>
        public static IReadOnlyList<Entry> Entries => _entries;

        /// <summary>是否有目标在册（feature 挂 pass 判据——平时 false 零开销）。</summary>
        public static bool HasTargets { get; private set; }

        /// <summary>整批替换（教学步进=全清全设；renderer 引用由热更侧持活）。</summary>
        public static void Set(IEnumerable<Entry> entries)
        {
            _entries.Clear();
            if (entries != null)
                foreach (var e in entries)
                    if (e.Renderers != null && e.Renderers.Length > 0)
                        _entries.Add(e);
            HasTargets = _entries.Count > 0;
        }

        /// <summary>清空（步进无高亮/引导退场/切屏兜底）。</summary>
        public static void Clear()
        {
            _entries.Clear();
            HasTargets = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _entries.Clear();
            HasTargets = false;
        }
    }
}
