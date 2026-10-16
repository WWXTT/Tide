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
    /// 红瞄准面片（2026-10-16 晚定案·选中才上盘）：开窗不铺候选面片，选中/取消选中在候选
    /// 地块 Show/Hide 红瞄准（与 sel-frame 同拍·全尺寸单档）。单位/施放立牌经注入的
    /// boardCellOf（BattleScreen→BattleFieldView.TryGetOnBoardCell）定位；角色候选经
    /// roleCellOf（BattleScreen→BoardState.CharacterCell）定位角色格——选中角色地块同样出面片。
    /// 关窗单口清（ClearAll）。
    /// 角色候选（2026-10-16）：Player 实体→卡名【己方/对方角色】的 Hero 型占位卡（注入
    /// isLocalPlayer 判侧；ACardView 对 Hero 藏 type 区=不带生物立绘槽）。
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
        private readonly Func<Card, (int x, int z)?> _boardCellOf; // 实体→场上格（null=宿主未注入，无面片）
        private readonly Func<Player, bool> _isLocalPlayer;        // 角色→己方/对方判别（null=一律按对方侧文案）
        private readonly Func<Player, (int x, int z)?> _roleCellOf; // 角色→角色格（null=宿主未注入，选中无面片）
        // lbl-toast 定案（2026-10-16）：开窗=toast 写「本次选择来干嘛」（效果发动的选择=完整效果文，
        // 由 BattleScreen 注入 buildPrompt 渲染；纯选项路径=hint 回落 title），关窗=清空；
        // allowCancel（主动效果可「取消不发动」）随提示一并给宿主亮 btn-notchain
        private readonly Func<TargetSelectionRequest, string> _buildPrompt;
        private readonly Action<string, bool> _onPrompt;

        private readonly List<int> _picked = new List<int>();
        private readonly List<Image> _frames = new List<Image>();
        private (uint id, int x, int z)?[] _reticles; // 本轮候选地块登记（选中才上盘；索引对齐候选；null=无实体/纯选项路径）
        private const uint RoleAimIdBase = uint.MaxValue; // 角色候选面片伪键基（角色无 RuntimeId；高位不与卡的低位顺序号冲突）
        private int _min, _max;
        private string _title;
        private bool _busy;
        private UniTaskCompletionSource<List<int>> _activeTcs; // 在途选择收口口（Cancel 用）

        /// <summary>外部收口在途选择（2026-10-16 响应窗口让过/守卫先定）：空表完成——
        /// Run 侧空表≠null 不触发超时代选，调用方按「未选」语义消费（响应窗=让过）。
        /// 未在途=无操作。</summary>
        public void Cancel() => _activeTcs?.TrySetResult(new List<int>());

        public RegionTargetSelector(RectTransform region, Func<Card, (int x, int z)?> boardCellOf = null,
            Func<Player, bool> isLocalPlayer = null, Func<Player, (int x, int z)?> roleCellOf = null,
            Func<TargetSelectionRequest, string> buildPrompt = null, Action<string, bool> onPrompt = null)
        {
            _region = region;
            _labelNum = region.Find("label-num")?.GetComponent<TMP_Text>();
            _btnSelect = region.Find("btn-select")?.GetComponent<Button>();
            _viewport = region.Find("viewport") as RectTransform;
            _content = region.Find("viewport/content") as RectTransform;
            _template = region.Find("viewport/content/ACard") as RectTransform;
            _boardCellOf = boardCellOf;
            _isLocalPlayer = isLocalPlayer;
            _roleCellOf = roleCellOf;
            _buildPrompt = buildPrompt;
            _onPrompt = onPrompt;
            if (_labelNum == null || _btnSelect == null || _viewport == null || _template == null)
                Debug.LogError("[RegionTargetSelector] Region 缺节点（label-num/btn-select/viewport/content/ACard）——目标选择将退化自动选");
        }

        // ==================== ITargetSelectorEx（本地局主入口：实体候选 → ACard 卡面） ====================

        public async UniTask<List<int>> SelectAsync(TargetSelectionRequest request,
            IReadOnlyList<string> labels, float timeoutSeconds)
        {
            var items = new List<CardOverlayItem>(labels.Count);
            var reticles = new (uint id, int x, int z)?[labels.Count]; // 场上候选→面片登记（索引对齐）
            for (int i = 0; i < labels.Count; i++)
            {
                var e = request.Candidates != null && i < request.Candidates.Count ? request.Candidates[i] : null;
                if (e is Player p)
                {
                    // 角色候选（2026-10-16 定案）：非卡实体——卡名只写【己方/对方角色】，
                    // Hero 型占位卡不带生物立绘槽（ACardView 藏 type 区）
                    items.Add(new CardOverlayItem
                    {
                        Key = "P" + i,
                        Name = _isLocalPlayer != null && _isLocalPlayer(p) ? "【己方角色】" : "【对方角色】",
                        Supertype = Cardtype.Hero,
                    });
                    if (_roleCellOf?.Invoke(p) is { } roleCell) // 选中角色→角色格出面片（2026-10-16 晚定案）
                        reticles[i] = (RoleAimIdBase - (uint)i, roleCell.x, roleCell.z);
                    continue;
                }
                var card = e as Card;
                var it = card != null ? CardOverlayItem.FromLiveCard(card) : null;
                items.Add(it ?? new CardOverlayItem { Key = "L" + i, Name = labels[i] });
                if (card != null && _boardCellOf?.Invoke(card) is { } cell) // 在场上（单位/施放立牌）
                    reticles[i] = (card.RuntimeId, cell.x, cell.z);
            }
            // lbl-toast（2026-10-16 定案）：效果发动的目标选择——buildPrompt 按 PromptSource/EffectId
            // 渲染完整效果文；取消口=AllowCancel（主动效果「取消不发动」）
            string prompt = _buildPrompt != null ? _buildPrompt(request) : request.Title;
            return await Run(items, reticles, request.MinCount, request.MaxCount, request.Title, timeoutSeconds,
                prompt, request.AllowCancel);
        }

        // ==================== 基接口（纯选项/网络兜底路径：仅卡名最小卡面） ====================

        public async UniTask<List<int>> SelectIndicesAsync(IReadOnlyList<string> labels,
            int min, int max, string title, string hint, bool allowCancel, float timeoutSeconds)
        {
            var items = new List<CardOverlayItem>(labels.Count);
            for (int i = 0; i < labels.Count; i++)
                items.Add(new CardOverlayItem { Key = "L" + i, Name = labels[i] });
            // 纯选项路径（响应窗/抉择）：提示=hint 优先（调用方写明「在响应什么」），回落 title
            string prompt = string.IsNullOrWhiteSpace(hint) ? title : hint;
            return await Run(items, null, min, max, title, timeoutSeconds, prompt, allowCancel); // 纯选项路径无实体→无面片
        }

        public async UniTask<int> SelectOneAsync(Player chooser, IReadOnlyList<string> options,
            string title, float timeoutSeconds)
        {
            var picked = await SelectIndicesAsync(options, 1, 1, title, "", false, timeoutSeconds);
            return picked != null && picked.Count > 0 ? picked[0] : 0;
        }

        // ==================== 选择面装配与驱动 ====================

        private async UniTask<List<int>> Run(IReadOnlyList<CardOverlayItem> items,
            IReadOnlyList<(uint id, int x, int z)?> reticles,
            int min, int max, string title, float timeoutSeconds,
            string prompt = null, bool allowCancel = false)
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
            _onPrompt?.Invoke(string.IsNullOrWhiteSpace(prompt) ? "请选择目标" : prompt, allowCancel); // toast=选择目的（2026-10-16 定案）
            if (_region.GetComponent<ScrollRect>() is { } scroll)
                scroll.horizontalNormalizedPosition = 0f; // 新一轮从最左起
            _btnSelect.onClick.RemoveAllListeners();

            ClearInstances();
            _frames.Clear();

            // 红瞄准面片（2026-10-16 晚定案·选中才上盘）：开窗只登记不上盘，选中/取消选中随 Toggle 对拍 Show/Hide
            _reticles = reticles != null ? CopyReticles(reticles, items.Count) : null;
            if (_reticles != null)
            {
                int onBoard = 0;
                for (int i = 0; i < _reticles.Length; i++) if (_reticles[i].HasValue) onBoard++;
                Debug.Log($"[RegionTarget] 选择窗开：候选{items.Count} 其中带地块{onBoard}——瞄准面片选中才上盘（现 {BattleTargetReticle.Count}）");
            }

            var tcs = new UniTaskCompletionSource<List<int>>();
            _activeTcs = tcs; // Cancel 外部收口挂点（2026-10-16）
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

            // 收尾：清实例+清面片+还原浏览三件+Region 复位进入前活跃态
            _btnSelect.onClick.RemoveAllListeners();
            ClearInstances();
            _frames.Clear();
            BattleTargetReticle.ClearAll();
            _reticles = null;
            foreach (var kv in hidden) kv.Key.SetActive(kv.Value);
            _region.gameObject.SetActive(regionWasActive);
            IsOpen = false;
            _busy = false;
            _activeTcs = null;
            _onPrompt?.Invoke(null, false); // 关窗清 toast/取消键（2026-10-16 定案）
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
                SyncReticles();
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
            SyncReticles();
        }

        /// <summary>面片与 _picked 全量对拍（选中=上盘/取消=下盘；含单选互斥同帧生效——被清的也下盘）。</summary>
        private void SyncReticles()
        {
            if (_reticles == null) return;
            for (int i = 0; i < _reticles.Length; i++)
                if (_reticles[i].HasValue)
                {
                    var r = _reticles[i].Value;
                    if (_picked.Contains(i)) BattleTargetReticle.Show(r.id, r.x, r.z);
                    else BattleTargetReticle.Hide(r.id);
                }
        }

        /// <summary>按候选数定长拷贝（防外部数组复用跨窗串号；越界截断）。</summary>
        private static (uint id, int x, int z)?[] CopyReticles(IReadOnlyList<(uint id, int x, int z)?> src, int count)
        {
            var r = new (uint id, int x, int z)?[count];
            for (int i = 0; i < count && i < src.Count; i++) r[i] = src[i];
            return r;
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
