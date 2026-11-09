using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SynergyUI
{
    /// <summary>
    /// 对局记录屏（2026-11-09 协议 v11 对局记录定案）：联机对局终局摘要（EncounterStore）的
    /// 查看界面——列表（时间/对手/胜负）→ 详情（对手卡组摘要 + 战报全文）+「导入对手卡组」
    ///（OpponentDeckImporter——对手自建内容默认不进玩家库，导入是显式动作）。
    /// 纯代码构建（同 LobbyScreen 模式，prefab 化留待美术换皮）；空态=「暂无对局记录」。
    /// 主菜单入口 MainMenuScreen（btn-history）。
    /// </summary>
    public sealed class MatchHistoryScreen : UIScreen
    {
        protected override string RootName => "history-screen";

        private RectTransform _panelList, _panelDetail;
        private RectTransform _listContent;
        private TMP_Text _emptyLabel;
        private TMP_Text _detailTitle, _detailMeta, _detailLog;
        private RectTransform _logContent;
        private EncounterRecord _selected;
        private readonly List<EncounterRecord> _records = new List<EncounterRecord>();

        protected override void Build()
        {
            Root = UiKit.Screen(RootName, Parent);

            UiKit.Label("lbl-title", Root, "对局记录", 20, UiStyle.TextPrimary,
                TextAnchor.MiddleCenter, FontStyle.Bold);

            // ---- 列表面板 ----
            _panelList = UiKit.Column("panel-list", Root);
            _emptyLabel = UiKit.Label("lbl-empty", _panelList,
                "暂无对局记录——联机对局（含局域网）结束后自动入账", 14, UiStyle.TextFaint, wrap: true);
            var listScroll = UiKit.ScrollColumn("list-records", _panelList);
            _listContent = listScroll.Content;
            UiKit.Size(listScroll.Rect.transform as RectTransform, fw: 1f, fh: 1f);
            UiKit.Button("btn-history-back", _panelList, "返回主菜单", () => Manager.Back());

            // ---- 详情面板 ----
            _panelDetail = UiKit.Column("panel-detail", Root);
            _detailTitle = UiKit.Label("lbl-detail-title", _panelDetail, "", 16, UiStyle.TextPrimary,
                TextAnchor.MiddleLeft, FontStyle.Bold, wrap: true);
            _detailMeta = UiKit.Label("lbl-detail-meta", _panelDetail, "", 13, UiStyle.TextDim, wrap: true);
            var logScroll = UiKit.ScrollColumn("scroll-log", _panelDetail, bg: UiStyle.ListBg);
            _logContent = logScroll.Content;
            UiKit.Size(logScroll.Rect.transform as RectTransform, fw: 1f, fh: 1f);
            _detailLog = UiKit.Label("lbl-log", _logContent, "", 13, UiStyle.LogBase, wrap: true);
            UiKit.Size(_detailLog.rectTransform, fw: 1f);
            var detailBtns = UiKit.Row("row-detail-btns", _panelDetail);
            UiKit.Button("btn-import", detailBtns, "导入对手卡组", ImportSelected, UiStyle.BtnPrimary);
            UiKit.Button("btn-detail-back", detailBtns, "返回列表", () => ShowPanel(_panelList));
        }

        public override void OnEnter()
        {
            _records.Clear();
            _records.AddRange(EncounterStore.LoadAll());
            RebuildList();
            ShowPanel(_panelList);
        }

        private void RebuildList()
        {
            for (int i = _listContent.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(_listContent.GetChild(i).gameObject);
            if (_emptyLabel != null) _emptyLabel.gameObject.SetActive(_records.Count == 0);

            for (int index = 0; index < _records.Count; index++)
            {
                var record = _records[index];
                var row = UiKit.Row($"row-{index}", _listContent, spacing: 8f);
                UiKit.BgRow(row, UiStyle.RowBg);
                UiKit.Dot("dot-result", row, record.win ? UiStyle.ToastGreen : UiStyle.ErrorRed);
                UiKit.Label("lbl-time", row, FormatTime(record.finishedAtUnix), 13, UiStyle.TextDim);
                UiKit.Label("lbl-opponent", row, $"vs {record.opponentName}", 14, UiStyle.TextBody);
                UiKit.Label("lbl-deck", row, $"「{record.opponentDeckName}」（{record.opponentCardIds?.Length ?? 0}张）",
                    13, UiStyle.TextDim);
                UiKit.Label("lbl-result", row, record.win ? "胜" : "负", 14,
                    record.win ? UiStyle.ToastGreen : UiStyle.ErrorRed, TextAnchor.MiddleCenter, FontStyle.Bold);
                UiKit.Size(row, h: 34f);

                var button = row.gameObject.GetComponent<Button>() ?? row.gameObject.AddComponent<Button>();
                button.targetGraphic = row.GetComponent<Image>();
                int captured = index; // 闭包捕获
                button.onClick.AddListener(() => { Debug.Log($"[UI点击] row-{captured}"); OpenDetail(captured); });
            }
        }

        private void OpenDetail(int index)
        {
            if (index < 0 || index >= _records.Count) return;
            _selected = _records[index];
            var r = _selected;
            if (_detailTitle != null)
                _detailTitle.text = $"{(r.win ? "胜利" : "失败")}——vs {r.opponentName}「{r.opponentDeckName}」";
            if (_detailMeta != null)
            {
                string skill = string.IsNullOrEmpty(r.opponentSkillCardId) ? "无" : "有";
                string tokens = r.opponentTokenCardIds != null && r.opponentTokenCardIds.Length > 0
                    ? $"，衍生物 {r.opponentTokenCardIds.Length}" : "";
                _detailMeta.text = $"时间：{FormatTime(r.finishedAtUnix)} · 我方卡组：{r.ownDeckName}"
                    + $" · 对手 {r.opponentCardIds?.Length ?? 0} 张（技能 {skill}{tokens}）"
                    + $" · 自建内容：卡 {r.cards?.Length ?? 0}/效果 {r.effects?.Length ?? 0}/词条 {r.atomRows?.Length ?? 0}";
            }
            if (_detailLog != null)
                _detailLog.text = string.IsNullOrEmpty(r.log) ? "（战报未随记录保存——云打包超限时最旧的战报正文会被裁剪）" : r.log;
            ShowPanel(_panelDetail);
        }

        private void ImportSelected()
        {
            if (_selected == null) return;
            var r = _selected;
            UiKit.ConfirmBox(Root, "导入对手卡组",
                $"将把对手 {r.opponentName} 的卡组「{r.opponentDeckName}」及其自建卡/效果导入本地"
                + "（本地已有同 ID 内容保留本地版本）。确认导入？",
                "导入", () => ImportSelectedCore(),
                "取消", null, danger: false);
        }

        private void ImportSelectedCore()
        {
            string error = OpponentDeckImporter.Import(_selected, out var summary);
            if (error != null)
            {
                UiKit.ConfirmBox(Root, "导入失败", error, "知道了", null, "关闭", null, danger: true);
                return;
            }
            UiKit.ConfirmBox(Root, "导入对手卡组", summary ?? "导入完成。", "好的", null, "关闭", null, danger: false);
        }

        private void ShowPanel(RectTransform panel)
        {
            if (_panelList != null) _panelList.gameObject.SetActive(panel == _panelList);
            if (_panelDetail != null) _panelDetail.gameObject.SetActive(panel == _panelDetail);
        }

        private static string FormatTime(long unixSeconds)
            => unixSeconds <= 0 ? "时间未知"
                : DateTimeOffset.FromUnixTimeSeconds(unixSeconds).ToLocalTime().ToString("MM-dd HH:mm");
    }
}
