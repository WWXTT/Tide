using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using CardCore;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SynergyUI
{
    /// <summary>
    /// Region 面板目标选择器（2026-10-15 用户定案，取代本地局 UiTargetSelector 列表弹窗）：
    /// BattleUI.prefab 的 Region 节点（ScrollRect 横滑）= 选择面——viewport/content/ACard 模板
    /// 逐候选实例化卡面（实例隐藏 cost 费用行与 btn 层；模板已无卡费方格）、label-num 写
    /// 「{选择标题}（x/y）」（标题空时回落「目标数量」——标题即"在为什么选目标"，如回合末
    /// 弃牌的「回合结束，手牌超出7张，请选择丢弃」）、btn-select（确定）在 x 满足 min..max
    /// 后显示，点击即确认收口。
    /// 实现 ITargetSelectorEx：SelectAsync 拿 Candidates 实体渲染 ACard 全卡面（FromLiveCard），
    /// 非卡实体/纯选项回落最小卡面（仅卡名）；超时自持（Ex 契约）——到点取当前有效选择，
    /// 不足 min 则 AutoPick 前 min 个。与 Region 浏览态（墓/除外/发动区列卡）互斥：
    /// 打开时压掉浏览三件（title/lines/cards）并复位 Region 活跃态；IsOpen 供
    /// BattleScreen.RefreshRegion/OpenRegion 闭流防串台。
    /// </summary>
    public sealed class RegionTargetSelector : ITargetSelectorEx
    {
        /// <summary>选择进行中（BattleScreen 浏览面板闭流判据）。</summary>
        public static bool IsOpen { get; private set; }

        private readonly RectTransform _region;
        private readonly TMP_Text _labelNum;
        private readonly Button _btnSelect;
        private readonly RectTransform _content;
        private readonly RectTransform _template;
        private readonly RectTransform _viewport;

        private readonly List<int> _picked = new List<int>();
        private readonly List<Image> _frames = new List<Image>();
        private int _min, _max;
        private string _title;
        private bool _busy;

        public RegionTargetSelector(RectTransform region)
        {
            _region = region;
            _labelNum = region.Find("label-num")?.GetComponent<TMP_Text>();
            _btnSelect = region.Find("btn-select")?.GetComponent<Button>();
            _viewport = region.Find("viewport") as RectTransform;
            _content = region.Find("viewport/content") as RectTransform;
            _template = region.Find("viewport/content/ACard") as RectTransform;
            if (_labelNum == null || _btnSelect == null || _viewport == null || _template == null)
                Debug.LogError("[RegionTargetSelector] Region 缺节点（label-num/btn-select/viewport/content/ACard）——目标选择将退化自动选");
        }

        // ==================== ITargetSelectorEx（本地局主入口：实体候选 → ACard 卡面） ====================

        public async UniTask<List<int>> SelectAsync(TargetSelectionRequest request,
            IReadOnlyList<string> labels, float timeoutSeconds)
        {
            var items = new List<CardOverlayItem>(labels.Count);
            for (int i = 0; i < labels.Count; i++)
            {
                var e = request.Candidates != null && i < request.Candidates.Count ? request.Candidates[i] : null;
                var it = e is Card card ? CardOverlayItem.FromLiveCard(card) : null;
                items.Add(it ?? new CardOverlayItem { Key = "L" + i, Name = labels[i] });
            }
            return await Run(items, request.MinCount, request.MaxCount, request.Title, timeoutSeconds);
        }

        // ==================== 基接口（纯选项/网络兜底路径：仅卡名最小卡面） ====================

        public async UniTask<List<int>> SelectIndicesAsync(IReadOnlyList<string> labels,
            int min, int max, string title, string hint, bool allowCancel, float timeoutSeconds)
        {
            var items = new List<CardOverlayItem>(labels.Count);
            for (int i = 0; i < labels.Count; i++)
                items.Add(new CardOverlayItem { Key = "L" + i, Name = labels[i] });
            return await Run(items, min, max, title, timeoutSeconds);
        }

        public async UniTask<int> SelectOneAsync(Player chooser, IReadOnlyList<string> options,
            string title, float timeoutSeconds)
        {
            var picked = await SelectIndicesAsync(options, 1, 1, title, "", false, timeoutSeconds);
            return picked != null && picked.Count > 0 ? picked[0] : 0;
        }

        // ==================== 选择面装配与驱动 ====================

        private async UniTask<List<int>> Run(IReadOnlyList<CardOverlayItem> items,
            int min, int max, string title, float timeoutSeconds)
        {
            bool usable = _region != null && _labelNum != null && _btnSelect != null
                && _content != null && _template != null && items.Count > 0 && max > 0;
            if (!usable || _busy) return AutoPick(items.Count, min); // 缺节点/重入：退化自动选

            _busy = true;
            IsOpen = true;
            _min = min;
            _max = max;
            _title = string.IsNullOrWhiteSpace(title) ? null : title;
            _picked.Clear();
            bool regionWasActive = _region.gameObject.activeSelf;

            // 压掉浏览态三件（BuildRegionPanel 的代码子物体；名字单源在 BattleScreen）
            var hidden = new List<KeyValuePair<GameObject, bool>>();
            foreach (var kid in new[] { "title", "lines", "cards" })
            {
                var t = _region.Find(kid);
                if (t != null)
                {
                    hidden.Add(new KeyValuePair<GameObject, bool>(t.gameObject, t.gameObject.activeSelf));
                    t.gameObject.SetActive(false);
                }
            }

            _labelNum.gameObject.SetActive(true);
            _viewport.gameObject.SetActive(true);
            _btnSelect.gameObject.SetActive(false);
            _region.gameObject.SetActive(true);
            if (_region.GetComponent<ScrollRect>() is { } scroll)
                scroll.horizontalNormalizedPosition = 0f; // 新一轮从最左起
            _btnSelect.onClick.RemoveAllListeners();

            ClearInstances();
            _frames.Clear();
            var tcs = new UniTaskCompletionSource<List<int>>();
            for (int i = 0; i < items.Count; i++) BuildCard(i, items[i], tcs);
            RefreshLabel();
            _btnSelect.onClick.AddListener(() =>
            {
                if (_picked.Count >= _min && _picked.Count <= _max) tcs.TrySetResult(Sorted());
            });

            // 超时自持（Ex 契约）：到点取当前有效选择，不足 min 则代选前 min 个
            float seconds = timeoutSeconds > 0f ? timeoutSeconds : 60f;
            var timeoutTask = UniTask.Delay(TimeSpan.FromSeconds(seconds))
                .ContinueWith(() => (List<int>)null);
            var (win, uiResult, timeoutResult) = await UniTask.WhenAny(tcs.Task, timeoutTask);
            List<int> result = win == 0 ? uiResult : null;
            if (result == null)
                result = _picked.Count >= _min && _picked.Count <= _max
                    ? Sorted() : AutoPick(items.Count, _min);

            // 收尾：清实例+还原浏览三件+Region 复位进入前活跃态
            _btnSelect.onClick.RemoveAllListeners();
            ClearInstances();
            _frames.Clear();
            foreach (var kv in hidden) kv.Key.SetActive(kv.Value);
            _region.gameObject.SetActive(regionWasActive);
            IsOpen = false;
            _busy = false;
            return result;
        }

        /// <summary>实例化一张候选卡：去 cost 费用行/btn 层，ACardView 全卡面 + 根 Button 点击切换。</summary>
        private void BuildCard(int idx, CardOverlayItem item, UniTaskCompletionSource<List<int>> tcs)
        {
            var rt = UnityEngine.Object.Instantiate(_template, _content, false);
            rt.gameObject.SetActive(true);
            rt.name = "pick-" + idx;
            var cost = rt.Find("cost");
            if (cost != null) cost.gameObject.SetActive(false);
            var btnLayer = rt.Find("btn");
            if (btnLayer != null) btnLayer.gameObject.SetActive(false);

            var face = rt.gameObject.AddComponent<ACardView>();
            face.Bind();
            face.Apply(item, CardOverlayLayout.Full);

            // 选中高亮框（首子物体外扩 6px；ignoreLayout 防被卡片根 VLG 排版挪位）
            var frameGo = new GameObject("sel-frame", typeof(RectTransform));
            frameGo.transform.SetParent(rt, false);
            frameGo.transform.SetAsFirstSibling();
            var frt = (RectTransform)frameGo.transform;
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = Vector2.one;
            frt.offsetMin = new Vector2(-6f, -6f);
            frt.offsetMax = new Vector2(6f, 6f);
            var fimg = frameGo.AddComponent<Image>();
            fimg.sprite = UiKit.RoundedSprite;
            fimg.type = Image.Type.Sliced;
            fimg.color = UiStyle.SelectedRowBg;
            frameGo.AddComponent<LayoutElement>().ignoreLayout = true;
            frameGo.SetActive(false);
            _frames.Add(fimg);

            var btn = rt.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() =>
            {
                Toggle(idx);
                RefreshLabel();
            });
        }

        private void Toggle(int idx)
        {
            if (_picked.Contains(idx))
            {
                _picked.Remove(idx);
                if (_frames[idx] != null) _frames[idx].gameObject.SetActive(false);
                return;
            }
            if (_picked.Count >= _max) return;
            if (_max == 1) // 单选互斥：清其它高亮
            {
                _picked.Clear();
                foreach (var f in _frames) if (f != null) f.gameObject.SetActive(false);
            }
            _picked.Add(idx);
            if (_frames[idx] != null) _frames[idx].gameObject.SetActive(true);
        }

        private void RefreshLabel()
        {
            // 标题说明本轮选择在选什么（如「回合结束，手牌超出7张，请选择丢弃」），空标题回落通用文案
            string head = string.IsNullOrWhiteSpace(_title) ? "目标数量" : _title;
            _labelNum.text = $"{head}（{_picked.Count}/{_max}）";
            bool ready = _picked.Count >= _min && _picked.Count <= _max;
            if (_btnSelect.gameObject.activeSelf != ready) _btnSelect.gameObject.SetActive(ready);
        }

        private List<int> Sorted()
        {
            var r = new List<int>(_picked);
            r.Sort();
            return r;
        }

        private static List<int> AutoPick(int count, int min)
        {
            var r = new List<int>();
            for (int i = 0; i < min && i < count; i++) r.Add(i);
            return r;
        }

        /// <summary>清 content 下全部实例（模板本体保留）。</summary>
        private void ClearInstances()
        {
            for (int i = _content.childCount - 1; i >= 0; i--)
                if (!ReferenceEquals(_content.GetChild(i), _template))
                    Destroy(_content.GetChild(i).gameObject);
        }

        private static void Destroy(GameObject go)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(go);
            else UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
