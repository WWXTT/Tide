using System.Collections.Generic;
using System.Linq;
using GameBoard;
using Tide.RenderFX;
using UnityEngine;

namespace SynergyUI
{
    /// <summary>
    /// 教学表现层总装（2026-11-21 遮罩/高亮/暂停计划 v0.4 §6 阶段4/5 胶水）：引导步的
    /// mask/highlight/pause 三块落到对应子系统——
    /// · mask → TutorialMaskController（成片缓存→模糊链→全屏遮罩图单窗；相机照常渲屏——
    ///   成片由 Tide.RenderFX.TutorialMaskFeature 拷入 TutorialMaskSceneCache；
    ///   目标解析失败=本步无窗只警告）；
    /// · highlight → TutorialOutlineRegistry（后处理选择描边；renderer 集合=解析器输出，
    ///   隐藏物体过滤——DrawRenderer 直绘不吃 culling，藏着的 tile 描了会出幽灵环）；
    /// · pause → BattlePauseBus（reason=Tutorial；自由步/aiTurnOnConfirm 强制无效——审计+此处双保险；
    ///   死锁防线：resumeBy=confirm 须有解说卡【确定】、含 window 须有遮罩窗，都不在=跳过暂停只警告）。
    /// 调用方（BattleTutorialDirector）：步进时 ApplyStep（非 intro 步）；解说卡摆出时 ApplyStep
    ///（intro 步——解说摆出来→暂停 同拍）；引导退场/切屏 Clear（BattleScreen.OnExit）。
    /// 旧档（无三块）与教学二/三/四：ApplyStep 全部空操作，行为零变化（计划 §7-5）。
    /// </summary>
    internal static class TutorialFocusController
    {
    /// <summary>1080 参考分辨率（与遮罩同源：px 值按屏比换算）。</summary>
    private const float RefHeight = 1080f;

    /// <summary>本步点亮的格环（Clear 时逐格熄——ClearGlow 只熄本格不动对账态）。</summary>
    private static readonly List<(int x, int z)> _cellRings = new List<(int, int)>();

    /// <summary>池环动态模式（2026-11-23 验收：basics_07 highlight style="pool"）——元素池行
    /// 每格环=各自元素色，有可用元素（未横置地牌+临时余额）亮原色、无=暗灰；Tick 每帧刷新。</summary>
    private static readonly List<(int x, int z, int element)> _poolRingCells = new List<(int, int, int)>();

    private static readonly Color PoolUnlitGray = new Color(0.27f, 0.27f, 0.27f); // 无可用元素=暗灰
    private static readonly Color PoolAnyColor = new Color(0.95f, 0.95f, 0.88f);  // 彩池环=米白（区别四主色）

    private static Color PoolRingColor(int element)
    {
        switch ((CardCore.ManaType)element)
        {
            case CardCore.ManaType.Red: return new Color(0.92f, 0.28f, 0.20f);
            case CardCore.ManaType.Blue: return new Color(0.25f, 0.50f, 1.00f);
            case CardCore.ManaType.Green: return new Color(0.30f, 0.78f, 0.40f);
            case CardCore.ManaType.Gray: return new Color(0.62f, 0.64f, 0.70f);
            default: return PoolAnyColor; // Any/彩
        }
    }

    /// <summary>该色可用元素（未横置地牌+临时余额——与地牌行「可用/上限」分子同口径；
    /// 地牌真源=ElementPool 池卡列表 PooledCard，Zone.ElementPool 卡对象不带横置/锁色态）。</summary>
    private static int AvailableElements(CardCore.ManaType type)
    {
        var core = CardCore.GameCore.Instance;
        if (core?.ElementPool == null || core.Player1 == null) return 0;
        int remaining = 0;
        foreach (var pc in core.ElementPool.GetPooledCards(core.Player1))
            if (pc != null && (int)pc.LockedColor == (int)type && !pc.IsTapped) remaining++;
        remaining += core.ElementPool.TempWalletOf(core.Player1, type);
        return remaining;
    }

    /// <summary>池环每帧刷新（Tick 驱动；模式外零成本直返）。</summary>
    private static void RefreshPoolRings()
    {
        if (_poolRingCells.Count == 0) return;
        foreach (var (x, z, element) in _poolRingCells)
        {
            bool lit = AvailableElements((CardCore.ManaType)element) > 0;
            BattleTileFx.SetCellRing(x, z, lit ? PoolRingColor(element) : PoolUnlitGray);
        }
    }

        /// <summary>应用一步的表现三块（step=null/无块=清空上一部残留）。</summary>
        public static void ApplyStep(RectTransform root, TutorialGuideStepSpec step)
        {
            // 遮罩：有 spec 且解析得过才启用；否则清（上一部的窗不得跨步残留）
            if (step?.mask != null && !string.IsNullOrEmpty(step.mask.target))
                TutorialMaskController.Apply(root, step.mask);
            else
                TutorialMaskController.Clear();

            ApplyHighlight(step?.highlight);
            ApplyPause(step);
        }

        /// <summary>只清视觉（引导退场/切屏兜底；暂停走 BattlePauseBus.ForceResume——BattleScreen.OnExit 同拍调）。</summary>
        public static void Clear()
        {
            TutorialMaskController.Clear();
            TutorialOutlineRegistry.Clear();
            ClearCellRings();
        }

        private static void ClearCellRings()
        {
            foreach (var (x, z) in _cellRings) BattleTileFx.ClearGlow(x, z); // 只熄本格环不动对账态
            _cellRings.Clear();
            _poolRingCells.Clear(); // 池环动态模式随步清（2026-11-23）
        }

        /// <summary>遮罩逐帧（窗跟新/模糊链/点窗恢复）+池环亮灰刷新——BattleScreen 更新器驱动。</summary>
        public static void Tick()
        {
            TutorialMaskController.Tick();
            RefreshPoolRings();
        }

        // ======================================== 高亮 ========================================

        private static void ApplyHighlight(List<TutorialHighlightSpec> highlights)
        {
            ClearCellRings();
            if (highlights == null || highlights.Count == 0)
            {
                TutorialOutlineRegistry.Clear();
                return;
            }
            float scale = Mathf.Max(0.01f, Screen.height / RefHeight);
            var entries = new List<TutorialOutlineRegistry.Entry>();
            foreach (var h in highlights)
            {
                if (h == null || string.IsNullOrEmpty(h.target)) continue;
                // 格群词（zone:*/cell:*/board/units）→ 格环（计划 §4：格级简单叠加，与描边可共存，
                // 不进蒙版描边——tile mesh 描边不如现成格环稳）
                var cells = TutorialTargetResolver.CellsOf(h.target);
                if (cells != null)
                {
                    // 池环动态模式（2026-11-23 验收 basics_07）：style="pool"=元素池行各格环随
                    // 各自元素色亮/灰（有可用元素才亮）；其余=静态色环（原口径）
                    if (h.style == "pool")
                    {
                        _poolRingCells.Clear();
                        var landCells = BoardLayout.LandCells(0);
                        int n = Mathf.Min(landCells.Count, BoardLayout.LandCellElementOrder.Length);
                        for (int i = 0; i < n; i++)
                            _poolRingCells.Add((landCells[i].x, landCells[i].z,
                                (int)BoardLayout.LandCellElementOrder[i]));
                        var any = BoardLayout.AnyElementCell(0);
                        _poolRingCells.Add((any.x, any.z, (int)CardCore.ManaType.Any));
                        foreach (var (x, z, _) in _poolRingCells) _cellRings.Add((x, z)); // Clear 统一熄
                        RefreshPoolRings(); // 首帧即按当前可用量落色
                        continue;
                    }
                    ColorUtility.TryParseHtmlString(
                        string.IsNullOrEmpty(h.color) ? "#FFD75A" : h.color, out var ringColor);
                    foreach (var (x, z) in cells)
                    {
                        BattleTileFx.SetCellRing(x, z, ringColor);
                        _cellRings.Add((x, z));
                    }
                    continue;
                }
                if (!TutorialTargetResolver.TryResolveRenderers(h.target, out var renderers, out var error))
                {
                    Debug.LogWarning($"[TutorialFocus] 高亮目标跳过：{error}");
                    continue;
                }
                // 隐藏物体过滤：DrawRenderer 直绘不吃 culling——被 reveal 藏住的格描边=幽灵环
                var visible = renderers.Where(r => r != null && r.gameObject.activeInHierarchy).ToArray();
                if (visible.Length == 0) continue;
                ColorUtility.TryParseHtmlString(
                    string.IsNullOrEmpty(h.color) ? "#FFD75A" : h.color, out var color);
                PulseParams(h.pulse, out float speed, out float amp);
                entries.Add(new TutorialOutlineRegistry.Entry
                {
                    Renderers = visible,
                    Color = color,
                    WidthPx = Mathf.Max(1f, h.width * scale),
                    Hdr = h.hdr,
                    PulseSpeed = speed,
                    PulseAmp = amp,
                });
            }
            TutorialOutlineRegistry.Set(entries.Count > 0 ? entries : null);
            if (entries.Count > 0)
                Debug.Log($"[TutorialFocus] 高亮启用：{entries.Count} 条");
        }

        private static void PulseParams(string pulse, out float speed, out float amp)
        {
            switch (pulse)
            {
                case "none": speed = 0f; amp = 0f; return;
                case "fast": speed = 2.5f; amp = 0.4f; return;
                default: speed = 1f; amp = 0.25f; return; // slow（缺省档）
            }
        }

        // ======================================== 暂停 ========================================

        private static void ApplyPause(TutorialGuideStepSpec step)
        {
            var p = step?.pause;
            if (p == null || !p.on) return;
            // 自由步/交回合链强制无效（计划 §5.3-1/2——TideSim 审计同口径拦截，此处双保险）
            if (step.type == "fieldclear" || step.type == "luckuse" || step.type == "deathtoll"
                || step.aiTurnOnConfirm)
            {
                Debug.LogWarning($"[TutorialFocus] pause.on 被忽略（{step.type}{(step.aiTurnOnConfirm ? "+aiTurnOnConfirm" : "")}——锁住=死锁）");
                return;
            }
            // 死锁防线：恢复口必须实际存在——confirm 要解说卡【确定】、window 要遮罩窗
            var resumeBy = ParseResumeBy(p.resumeBy);
            bool hasConfirm = step.type == "intro"; // 解说卡=TipPanel 确定钮（导演已接 ResumeByConfirm）
            bool hasWindow = TutorialMaskController.Active;
            if (resumeBy == BattlePauseResume.Confirm && !hasConfirm
                || resumeBy == BattlePauseResume.Window && !hasWindow
                || resumeBy == BattlePauseResume.WindowOrConfirm && !hasConfirm && !hasWindow)
            {
                Debug.LogWarning($"[TutorialFocus] pause 跳过（resumeBy={p.resumeBy} 但确定钮/遮罩窗均不在——挂了就死锁）");
                return;
            }
            BattlePauseBus.PauseForTutorial(step.hint, resumeBy, p.showHint);
        }

        private static BattlePauseResume ParseResumeBy(string resumeBy)
        {
            switch (resumeBy)
            {
                case "confirm": return BattlePauseResume.Confirm;
                case "window": return BattlePauseResume.Window;
                default: return BattlePauseResume.WindowOrConfirm;
            }
        }
    }
}
