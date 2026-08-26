using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using CardCore;
using UnityEngine.UIElements;

namespace SynergyUI
{
    /// <summary>
    /// ITargetSelector 的 UI Toolkit 实现：多选遮罩 + 倒计时 + 超时自动确定。
    /// 操作 Battle.uxml 中的 selector-overlay 子树。基于索引，实体/选项弹窗共用。
    /// </summary>
    public sealed class UiTargetSelector : ITargetSelector
    {
        private readonly VisualElement _root;
        private readonly HashSet<int> _selected = new HashSet<int>();

        public UiTargetSelector(VisualElement root)
        {
            _root = root;
        }

        public async UniTask<List<int>> SelectIndicesAsync(IReadOnlyList<string> labels,
            int min, int max, string title, string hint, bool allowCancel, float timeoutSeconds)
        {
            var overlay = _root.Q<VisualElement>("selector-overlay");
            if (overlay == null || labels == null || labels.Count == 0)
                return AutoPick(labels?.Count ?? 0, min);

            var titleLbl = _root.Q<Label>("selector-title");
            var hintLbl = _root.Q<Label>("selector-hint");
            var listView = _root.Q<ScrollView>("selector-list");
            var countdownLbl = _root.Q<Label>("selector-countdown");
            var confirmBtn = _root.Q<Button>("selector-confirm");
            var cancelBtn = _root.Q<Button>("selector-cancel");

            titleLbl.text = title;
            hintLbl.text = hint;
            _selected.Clear();
            listView.Clear();

            for (int i = 0; i < labels.Count; i++)
            {
                int idx = i;
                var row = new VisualElement();
                row.AddToClassList("list-row");
                var lbl = new Label(labels[i]);
                lbl.AddToClassList("list-row__name");
                row.Add(lbl);
                row.RegisterCallback<ClickEvent>(_ => ToggleRow(idx, row, min, max, confirmBtn));
                listView.Add(row);
            }

            var tcs = new UniTaskCompletionSource<List<int>>();

            EventCallback<ClickEvent> onConfirm = null;
            EventCallback<ClickEvent> onCancel = null;

            void Cleanup()
            {
                confirmBtn.UnregisterCallback(onConfirm);
                if (onCancel != null) cancelBtn.UnregisterCallback(onCancel);
                overlay.style.display = DisplayStyle.None;
            }

            onConfirm = _ =>
            {
                if (_selected.Count < min || _selected.Count > max) return;
                var result = _selected.ToList();
                result.Sort();
                Cleanup();
                tcs.TrySetResult(result);
            };
            confirmBtn.RegisterCallback(onConfirm);

            cancelBtn.style.display = allowCancel ? DisplayStyle.Flex : DisplayStyle.None;
            if (allowCancel)
            {
                onCancel = _ =>
                {
                    Cleanup();
                    tcs.TrySetResult(new List<int>());
                };
                cancelBtn.RegisterCallback(onCancel);
            }

            UpdateConfirmEnabled(confirmBtn, min, max);
            overlay.style.display = DisplayStyle.Flex;

            // 倒计时 + 超时自动确定（先头 min 个）
            RunCountdown(countdownLbl, timeoutSeconds, tcs, () =>
            {
                Cleanup();
                return AutoPick(labels.Count, min);
            }).Forget();

            return await tcs.Task;
        }

        private void ToggleRow(int idx, VisualElement row, int min, int max, Button confirmBtn)
        {
            if (_selected.Contains(idx))
            {
                _selected.Remove(idx);
                row.RemoveFromClassList("list-row--selected");
            }
            else
            {
                // 单选：先清除其它高亮
                if (max == 1)
                {
                    _selected.Clear();
                    foreach (var sibling in row.parent.Children())
                        sibling.RemoveFromClassList("list-row--selected");
                }
                if (_selected.Count >= max) return;
                _selected.Add(idx);
                row.AddToClassList("list-row--selected");
            }
            UpdateConfirmEnabled(confirmBtn, min, max);
        }

        private void UpdateConfirmEnabled(Button confirmBtn, int min, int max)
        {
            confirmBtn.SetEnabled(_selected.Count >= min && _selected.Count <= max);
        }

        private static async UniTask RunCountdown(Label countdownLbl, float seconds,
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
