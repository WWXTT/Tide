using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.Attribute;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SynergyUI
{
    /// <summary>
    /// 原子表工坊界面（2026-10-06）：开放原子表总价锚价给玩家修改——每行 [0.5, 8.5] 0.5 步进
    /// （0 与 9 永远设不进，AtomicEffectTable.TrySetRowTotal 权威校验），按行内原六色占比分摊。
    /// 改价即时生效（AtomicTableWorkshop.ApplyRowEdit → 全卡池强制重推），差异面板显示
    /// 所选卡组的「卡名 旧费→新费」；「镜像验证对局」用所选卡组双方同开（BattleEntry.MirrorDeck），
    /// AI 操作镜像卡组与玩家对打，验证费用改动对卡组的影响。
    /// 纯代码屏（PrefabName=null，UiKit 构建）；节点按名稳定（row-{hashId}、lbl-total-{hashId}、
    /// btn-plus-{hashId} 等），验证走层级/字符串断言（截图验收禁令口径）。
    /// 教学免疫：本屏改价不进教学局（BattleScreen.StartTutorialGame 前回基线，非本屏职责）。
    /// </summary>
    public sealed class WorkshopScreen : UIScreen
    {
        protected override string RootName => "workshop-screen";

        private UiKit.Scroll _rowList;
        private UiKit.Scroll _diffList;
        private TMP_Text _toast;
        private UiKit.Dropdown _deckDropdown;

        // 行节点引用（定向刷新单行，免整表重建）
        private readonly Dictionary<string, TMP_Text> _totalLabels = new Dictionary<string, TMP_Text>();
        private readonly Dictionary<string, TMP_Text> _costLabels = new Dictionary<string, TMP_Text>();

        // 卡组下拉映射（index → 卡组名；index 0=默认镜像口径）
        private List<string> _deckNames = new List<string>();
        private const string DefaultDeckOption = "默认（主体卡组）";

        protected override void Build()
        {
            Root = UiKit.Screen(RootName, Parent);

            // ---- 顶栏 ----
            var top = UiKit.Row("topbar", Root, spacing: 8f);
            UiKit.Button("btn-back", top, "返回", () => Manager.Back(), width: 72f);
            var title = UiKit.Label("title", top, "原子表工坊", UiStyle.HeaderSize, UiStyle.TextPrimary);
            UiKit.Size(title, w: 180f);
            UiKit.Label("subtitle", top, "改原子效果的总价锚价（0.5–8.5，禁 0 与 9），按原色占比分摊——改价即时生效于本地对战",
                UiStyle.SmallSize, UiStyle.TextFaint);

            // ---- 操作栏 ----
            var actions = UiKit.Row("actionbar", Root, spacing: 8f);
            UiKit.Toggle("toggle-enabled", actions, "启用改价", AtomicTableOverlay.Instance.Enabled, OnToggleEnabled);
            UiKit.Button("btn-restore", actions, "恢复默认", OnRestoreAll);
            var deckLabel = UiKit.Label("lbl-deck", actions, "验证卡组：", UiStyle.BodySize, UiStyle.TextBody);
            UiKit.Size(deckLabel, w: 80f);
            _deckDropdown = new UiKit.Dropdown("dropdown-deck", actions, Root,
                new List<string> { DefaultDeckOption }, 0, width: 180f);
            UiKit.Button("btn-mirror", actions, "镜像验证对局", OnMirrorBattle, bg: UiStyle.BtnPrimary);

            // ---- 主体：左行列表 + 右差异面板 ----
            var body = UiKit.Row("body", Root, spacing: 8f);
            _rowList = UiKit.ScrollColumn("list-rows", body, spacing: 2f, pad: 4f);
            UiKit.Size(_rowList.Rect, fw: 1f);

            var diffPanel = UiKit.Column("diff-panel", body, spacing: 4f);
            UiKit.Size(diffPanel, w: 360f, fh: 1f);
            UiKit.Label("lbl-diff-title", diffPanel, "费用变化（所选卡组）", UiStyle.BodySize, UiStyle.TextPrimary);
            _diffList = UiKit.ScrollColumn("list-diff", diffPanel, spacing: 2f, pad: 4f);
            UiKit.Size(_diffList.Rect, fw: 1f, fh: 1f);

            _toast = UiKit.Toast("lbl-toast", Root);
        }

        public override void OnEnter()
        {
            // overlay 启用但未落表（如启动后直进本屏）→ 先落表，保证行显示与对局一致
            if (AtomicTableOverlay.Instance.Enabled)
            {
                AtomicTableOverlay.Instance.EnsureApplied();
                CardIdentityService.InvalidateTableCache();
            }
            RefreshDeckDropdown();
            RebuildRows();
            ClearDiff();
        }

        // ======================================== 行列表 ========================================

        /// <summary>整表重建（进屏/恢复默认/开关切换后）。</summary>
        private void RebuildRows()
        {
            ClearChildren(_rowList.Content);
            _totalLabels.Clear();
            _costLabels.Clear();
            foreach (var cfg in AtomicEffectTable.OrderedRows)
            {
                if (cfg == null || string.IsNullOrEmpty(cfg.HashId)) continue;
                MakeRow(cfg.HashId, cfg.DisplayName ?? cfg.EnumName, cfg);
            }
        }

        private void MakeRow(string hashId, string displayName, AtomicEffectConfig cfg)
        {
            var row = UiKit.Row($"row-{hashId}", _rowList.Content, spacing: 6f, pad: 4f);
            UiKit.BgRow(row);
            UiKit.Size(row, h: 30f);

            var name = UiKit.Label("name", row, displayName, UiStyle.SmallSize, UiStyle.TextBody);
            UiKit.Size(name, w: 170f);

            var cost = UiKit.Label("cost", row, CostText(cfg), UiStyle.SmallSize, UiStyle.TextFaint);
            UiKit.Size(cost, w: 130f);
            _costLabels[hashId] = cost;

            var total = UiKit.Label("total", row, TotalText(cfg), UiStyle.SmallSize, UiStyle.TextPrimary);
            UiKit.Size(total, w: 44f);
            _totalLabels[hashId] = total;

            if (cfg.ManaList == null || cfg.ManaList.IsZero)
            {
                UiKit.Label("hint", row, "不计价", UiStyle.SmallSize, UiStyle.TextHint);
                return; // 不计价行：无编辑控件
            }

            var id = hashId;
            UiKit.MiniButton($"btn-minus-{id}", row, "−", () => OnStep(id, -1), width: 24f);
            UiKit.MiniButton($"btn-plus-{id}", row, "＋", () => OnStep(id, +1), width: 24f);
            UiKit.MiniButton($"btn-reset-{id}", row, "还原", () => OnResetRow(id), width: 40f);
        }

        /// <summary>步进提交：吸附 0.5 网格后 ±0.5，钳到 [0.5, 8.5]——超界基线值（如 12）一步拉回可编辑区间。</summary>
        private void OnStep(string hashId, int dir)
        {
            var cfg = AtomicEffectTable.GetByHashId(hashId);
            if (cfg?.ManaList == null) return;
            var current = cfg.ManaList.Total;
            var snapped = Mathf.Clamp(Mathf.Round(current * 2f) / 2f,
                AtomicEffectTable.MinEditableTotal, AtomicEffectTable.MaxEditableTotal);
            var target = Mathf.Clamp(snapped + dir * 0.5f,
                AtomicEffectTable.MinEditableTotal, AtomicEffectTable.MaxEditableTotal);
            if (Math.Abs(target - current) < 1e-4f)
            {
                ShowToast(dir > 0 ? "已到上限 8.5" : "已到下限 0.5");
                return;
            }
            var lines = AtomicTableWorkshop.ApplyRowEdit(hashId, target);
            RefreshRow(hashId);
            RefreshDiff(lines);
            ShowToast($"{cfg.DisplayName ?? hashId}：{TotalText(cfg)}（{lines.Count} 卡费用变化）");
        }

        /// <summary>单行还原默认：基线值可能超可编辑区间（如 12），走整表重建路径。</summary>
        private void OnResetRow(string hashId)
        {
            if (!AtomicTableOverlay.Instance.Rows.ContainsKey(hashId))
            {
                ShowToast("该行未修改");
                return;
            }
            var lines = AtomicTableWorkshop.RestoreRow(hashId);
            RebuildRows();
            RefreshDiff(lines);
            ShowToast("已还原该行（基线价）");
        }

        private void OnRestoreAll()
        {
            var lines = AtomicTableWorkshop.RestoreAll();
            RebuildRows();
            RefreshDiff(lines);
            ShowToast(lines.Count > 0 ? $"已恢复默认（{lines.Count} 卡费用回基线）" : "已恢复默认");
        }

        private void OnToggleEnabled(bool enabled)
        {
            var lines = AtomicTableWorkshop.SetOverlayEnabled(enabled);
            RebuildRows();
            RefreshDiff(lines);
            ShowToast(enabled ? "改价已启用" : "改价已停用（回基线）");
        }

        /// <summary>定向刷新单行显示（免整表重建）。</summary>
        private void RefreshRow(string hashId)
        {
            var cfg = AtomicEffectTable.GetByHashId(hashId);
            if (cfg == null) return;
            if (_totalLabels.TryGetValue(hashId, out var total)) total.text = TotalText(cfg);
            if (_costLabels.TryGetValue(hashId, out var cost)) cost.text = CostText(cfg);
        }

        private static string TotalText(AtomicEffectConfig cfg)
            => cfg.ManaList == null ? "—" : cfg.ManaList.Total.ToString("0.##");

        /// <summary>六色费用文本（序数序 [灰,红,蓝,绿,白,黑]，只列非零色，中文色名）。</summary>
        private static string CostText(AtomicEffectConfig cfg)
        {
            if (cfg.ManaList == null || cfg.ManaList.IsZero) return "不计价";
            var parts = new List<string>();
            foreach (ManaType m in Enum.GetValues(typeof(ManaType)))
            {
                var v = cfg.ManaList[m];
                if (v > 0f) parts.Add($"{ManaTypeNames.ZhNameOf(m)}{v:0.##}");
            }
            return string.Join(" ", parts);
        }

        // ======================================== 差异面板 ========================================

        private void ClearDiff()
        {
            ClearChildren(_diffList.Content);
            UiKit.Label("diff-empty", _diffList.Content, "（编辑后显示所选卡组的费用变化）",
                UiStyle.SmallSize, UiStyle.TextHint);
        }

        /// <summary>最近一次操作的卡费变化清单，过滤到所选卡组（镜像验证的即时读数）。</summary>
        private void RefreshDiff(List<CardCostService.ReforceLine> lines)
        {
            ClearChildren(_diffList.Content);
            var deckIds = SelectedDeckCardIds();
            var shown = lines.Where(l => deckIds == null || deckIds.Contains(l.CardId)).ToList();
            if (shown.Count == 0)
            {
                UiKit.Label("diff-none", _diffList.Content, "（所选卡组无费用变化）",
                    UiStyle.SmallSize, UiStyle.TextHint);
                return;
            }
            foreach (var line in shown)
                UiKit.Label($"diff-{line.CardId}", _diffList.Content,
                    $"{line.CardName ?? line.CardId}：{line.OldText} → {line.NewText}",
                    UiStyle.SmallSize, UiStyle.TextBody);
        }

        // ======================================== 卡组选择与镜像验证 ========================================

        private void RefreshDeckDropdown()
        {
            _deckNames = new List<string> { DefaultDeckOption };
            _deckNames.AddRange(DeckSerializer.LoadAll()
                .Where(d => d != null && !string.IsNullOrEmpty(d.name))
                .Select(d => d.name));
            _deckDropdown.SetOptions(_deckNames, 0);
        }

        /// <summary>所选卡组的卡 id 集（index 0=默认镜像口径 → null=不过滤，显示全部变化）。</summary>
        private HashSet<string> SelectedDeckCardIds()
        {
            var name = SelectedDeckName();
            if (name == null) return null;
            var data = DeckSerializer.Load(name);
            return data?.cardIds != null ? new HashSet<string>(data.cardIds) : null;
        }

        private string SelectedDeckName()
        {
            var idx = _deckDropdown != null ? _deckDropdown.Index : 0;
            return idx > 0 && idx < _deckNames.Count ? _deckNames[idx] : null;
        }

        /// <summary>镜像验证对局：双方同用所选卡组（默认=主体卡组，缺失回落随机镜像——
        /// 复用最后一课口径 ResolveDefaultMirrorDeck），AI 操作镜像卡组与玩家对打。</summary>
        private void OnMirrorBattle()
        {
            List<CardData> deck = null;
            var name = SelectedDeckName();
            if (name != null)
            {
                var data = DeckSerializer.Load(name);
                if (data?.cardIds != null)
                    deck = TutorialLibrary.BuildDeck(data.cardIds);
                if (deck == null || deck.Count < GameCore.OpeningHandSize)
                {
                    ShowToast($"卡组 {name} 不可用（缺卡/张数不足）——回落默认镜像");
                    deck = null;
                }
            }
            if (deck == null)
                deck = TutorialCreationFlow.ResolveDefaultMirrorDeck();

            BattleEntry.Mode = BattleMode.LocalAI;
            BattleEntry.MirrorDeck = deck;
            BattleStageDirector.SwallowToBattleAsync(() => Manager.Show<BattleScreen>()).Forget();
        }

        // ======================================== 小件 ========================================

        private void ShowToast(string message)
        {
            if (_toast != null) _toast.text = message;
            TideLog.Info($"[WorkshopScreen] {message}");
        }

        /// <summary>清空容器直系子节点（DeckBuilderScreen 同款幂等清理）。</summary>
        private static void ClearChildren(RectTransform container)
        {
            if (container == null) return;
            for (int i = container.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(container.GetChild(i).gameObject);
        }
    }
}
