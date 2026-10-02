using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using CardCore;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SynergyUI
{
    /// <summary>
    /// ITargetSelector 的 UGUI 实现（2026-10-01 UITK→UGUI 移植，语义不变）：
    /// 多选模态 + 倒计时 + 超时自动确定。基于索引，实体/选项弹窗共用。
    /// 弹窗挂在屏的 Overlay 层（模态遮罩挡下层点击；关窗即销毁）。
    /// </summary>
    public sealed class UiTargetSelector : ITargetSelector
    {
        private readonly RectTransform _overlayLayer;
        private readonly HashSet<int> _selected = new HashSet<int>();

        public UiTargetSelector(RectTransform overlayLayer)
        {
            _overlayLayer = overlayLayer;
        }

        public async UniTask<List<int>> SelectIndicesAsync(IReadOnlyList<string> labels,
            int min, int max, string title, string hint, bool allowCancel, float timeoutSeconds)
        {
            if (_overlayLayer == null || labels == null || labels.Count == 0)
                return AutoPick(labels?.Count ?? 0, min);

            _selected.Clear();
            var modal = UiKit.ModalBox(_overlayLayer, title, width: 480f, height: 500f);

            UiKit.Label("hint", modal.Panel, string.IsNullOrEmpty(hint) ? " " : hint,
                UiStyle.SmallSize, UiStyle.TextDim, wrap: true);

            var list = UiKit.ScrollColumn("list", modal.Panel, spacing: 4f);
            UiKit.Size(list.Rect.transform, fw: 1f, fh: 1f);

            var bottom = UiKit.Row("bottom", modal.Panel, spacing: 8f);
            var countdown = UiKit.Label("countdown", bottom, "", UiStyle.SmallSize, UiStyle.TextDim);
            UiKit.Size(countdown, w: 48f, h: 28f);
            var spacer = UiKit.Label("spacer", bottom, "");
            UiKit.Size(spacer, fw: 1f);

            var tcs = new UniTaskCompletionSource<List<int>>();

            var confirm = UiKit.Button("confirm", bottom, "确认", null, UiStyle.BtnPrimary, height: 32f);
            var cancel = UiKit.Button("cancel", bottom, "取消", null, height: 32f);
            cancel.gameObject.SetActive(allowCancel);

            var rows = new Image[labels.Count];
            for (int i = 0; i < labels.Count; i++)
                rows[i] = BuildRow(list.Content, i, labels[i]);

            confirm.onClick.AddListener(() =>
            {
                if (_selected.Count < min || _selected.Count > max) return;
                var result = _selected.ToList();
                result.Sort();
                modal.Close();
                tcs.TrySetResult(result);
            });
            if (allowCancel)
            {
                cancel.onClick.AddListener(() =>
                {
                    modal.Close();
                    tcs.TrySetResult(new List<int>());
                });
            }

            UpdateConfirmEnabled(confirm, min, max);

            // 倒计时 + 超时自动确定（先头 min 个）
            RunCountdown(countdown, timeoutSeconds, tcs, () =>
            {
                modal.Close();
                return AutoPick(labels.Count, min);
            }).Forget();

            return await tcs.Task;

            // 行构建：点击切换选中态（单选互斥；选中底色=SelectedRowBg）
            Image BuildRow(RectTransform content, int idx, string text)
            {
                var row = UiKit.Node("row-" + idx, content);
                var img = row.gameObject.AddComponent<Image>();
                img.sprite = UiKit.RoundedSprite;
                img.type = Image.Type.Sliced;
                img.color = UiStyle.RowBg;
                var lbl = UiKit.Label("label", row, text, UiStyle.BodySize, UiStyle.TextBody,
                    TextAnchor.MiddleLeft);
                UiKit.StretchInset(lbl.rectTransform, 10f, 4f);
                var btn = row.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.targetGraphic = img;
                btn.onClick.AddListener(() =>
                {
                    ToggleRow(idx, img, min, max, rows);
                    UpdateConfirmEnabled(confirm, min, max);
                });
                UiKit.Size(row, h: 36f);
                return img;
            }
        }

        private void ToggleRow(int idx, Image row, int min, int max, Image[] all)
        {
            if (_selected.Contains(idx))
            {
                _selected.Remove(idx);
                if (row != null) row.color = UiStyle.RowBg;
            }
            else
            {
                // 单选：先清除其它高亮
                if (max == 1)
                {
                    _selected.Clear();
                    foreach (var sibling in all)
                        if (sibling != null) sibling.color = UiStyle.RowBg;
                }
                if (_selected.Count >= max) return;
                _selected.Add(idx);
                if (row != null) row.color = UiStyle.SelectedRowBg;
            }
        }

        private void UpdateConfirmEnabled(Button confirm, int min, int max)
        {
            confirm.interactable = _selected.Count >= min && _selected.Count <= max;
        }

        private static async UniTask RunCountdown(TMP_Text countdownLbl, float seconds,
            UniTaskCompletionSource<List<int>> tcs, Func<List<int>> onTimeout)
        {
            float remaining = seconds;
            while (remaining > 0f)
            {
                if (tcs.Task.Status.IsCompleted()) return;
                if (countdownLbl != null)
                    countdownLbl.text = $"{Math.Ceiling(remaining)}s";
                await UniTask.Delay(TimeSpan.FromSeconds(1));
                remaining -= 1f;
            }
            if (!tcs.Task.Status.IsCompleted())
                tcs.TrySetResult(onTimeout());
        }

        private static List<int> AutoPick(int count, int min)
        {
            var r = new List<int>();
            for (int i = 0; i < min && i < count; i++)
                r.Add(i);
            return r;
        }
    }
}
