using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CardCore;
using CardCore.Attribute;

namespace SynergyUI
{
    /// <summary>
    /// ACard.prefab 手牌全信息卡面 View（2026-10-13，取代 CardOverlayCard 的 Full 形态）。
    /// MVC 口径：只消费 CardOverlayItem 显示模型，运行态刷新=只读重查（FromLiveCard 重快照），
    /// 绝不改游戏数据；事件面经 EventManager 订阅（按 RuntimeId 过滤命中才重绘，幂等全量）。
        /// 节点约定（与 Assets/Art/Prefab/ACard.prefab 同名，缺节点各区空转不崩）：
        /// type/(bg|mask|mask/art) + type 下 CostSquaresView（卡费）、attribute/text（属性区=根级
        /// 独立区，2026-10-15 接线：曾短暂挪入 type 区又改回根级——勿再写 type/attribute 旧路径）、
        /// name/text、cost/text（代价行=PayloadCost 产黑白元素等）、
        /// effect/(text|cost 下 CostSquaresView=发动消耗)、passives/text、keys/text（芯片模板）、
        /// stats/text（芯片模板）、back（伏面，节点已退役=空转）。
        /// 固定尺寸卡面（2026-10-14 定案，晚间用户改版 200×360/type80、各区定高以 prefab 为准）：
        /// effect/passives 换行+自动缩号+省略号兜底，keys/stats 芯片最多 2 行（超出并「+N」芯片、
        /// 块高随行数回写）；用户 btn 悬浮层（地牌/使用按钮）ignoreLayout 不参与竖排。
        /// prefab 保持无脚本（CardOverlayController/HandFanView 加载后 AddComponent）。
    /// 非 Full 形态=保底显示 type/name/费用（战场实体卡面另案，不做设计投入）。
    /// </summary>
    public sealed class ACardView : MonoBehaviour
    {
        // ---- 节点缓存（Bind 一次解析） ----
        private Image _typeBg, _mask, _art;
        private CostSquaresView _typeCost;
        private TMP_Text _nameText, _effectText, _passivesText, _attributeText, _costText;
        private RectTransform _effectBlock, _effectCostRow, _passivesBlock, _keysBlock, _statsBlock, _attributeBlock, _costBlock;
        private CostSquaresView _effectCost;
        private RectTransform _keysTemplate, _statsTemplate;
        private GameObject _back;
        private GameObject _actionLayer;
        private CanvasGroup _group;
        private Transform _faceRoot; // 六区卡面宿主（AHandCard 嵌套时=子实例 ACard；独立 ACard=卡根）
        private bool _bound, _faceUp = true;

        // ---- 运行态刷新 ----
        private Card _runtimeCard;
        private bool _subscribed;
        private Action<CounterChangedEvent> _onCounter;
        private Action<KeywordEvent> _onKeyword;
        private Action<EffectUsageChangedEvent> _onUsage;
        private Action<TurnStartEvent> _onTurn;

        // ---- 类型 → 底图/遮罩形状（2026-10-13 用户定案：生物=方·法术=三角·结界=圆） ----
        private static readonly Dictionary<Cardtype, (string bg, string mask)> TypeAssets = new()
        {
            { Cardtype.Creature, ("biology", "mask_square") },
            { Cardtype.Spell, ("spell", "mask_triangle") },
            { Cardtype.Enchantment, ("barrier", "mask_circle") },
        };
        private static readonly Dictionary<string, Sprite> SpriteCache = new();
        private static string SpritePath(string name) => $"Assets/Art/Battle3D/Icons/{name}.png";
        private string _artLoaded; // 已加载立绘名（防同图重复加载；空=保持预制体占位）
        // 立绘（type/mask/art）按名称两级回落：UI Sprites → YGOMonstersRig 模型预览图（CardArtLibrary，
        // 2026-10-13 立绘随卡类型定案——生物卡 Illustration=模型名；2026-10-15 法术/结界定案=无合适
        // 立绘，缺省空白不放图——Apply 清 sprite，不走找图链）

        /// <summary>手牌无移除角标（构筑区旧功能随 CardOverlayCard 退役；控制器接线自动跳过）。</summary>
        public GameObject RemoveButton => null;

        public void SetRemoveVisible(bool visible) { }

        /// <summary>按当前预制体层级解析节点（幂等；卡实例池复用只调一次）。</summary>
        public void Bind()
        {
            // 卡面六区宿主解析（2026-10-15 AHandCard 手牌改版）：手牌扇预制体 AHandCard 把
            // ACard 整卡嵌为名为 "ACard" 的子实例（btn/Flowing 等外层件是它的兄弟）；
            // 独立 ACard prefab（选目标 Region 面板/合成器预览/悬停卡）六区直挂卡根。
            _faceRoot = transform.Find("ACard");
            if (_faceRoot == null) _faceRoot = transform;
            _typeBg = FindImage("type/bg");
            _mask = FindImage("type/mask");
            _art = FindImage("type/mask/art");
            _typeCost = FindComponent<CostSquaresView>("type");
            _nameText = FindText("name/text");
            _effectBlock = Rt("effect");
            _effectText = FindText("effect/text");
            _effectCost = FindComponent<CostSquaresView>("effect");
            // cost 行=预留 effect/cost 容器，缺省回落 CostSquares 实例根本体（隐藏即藏费用行）
            _effectCostRow = Rt("effect/cost") ?? (_effectCost != null ? (RectTransform)_effectCost.transform : null);
            _passivesBlock = Rt("passives");
            _passivesText = FindText("passives/text");
            _keysBlock = Rt("keys");
            _keysTemplate = Rt("keys/text");
            _statsBlock = Rt("stats");
            _statsTemplate = Rt("stats/text");
            _attributeBlock = Rt("attribute");
            _attributeText = FindText("attribute/text");
            _costBlock = Rt("cost");
            _costText = FindText("cost/text");
            _back = transform.Find("ACardBack")?.gameObject ?? transform.Find("back")?.gameObject;
            // 用户 btn 悬浮层（放置/发动按钮，2026-10-14）：默认全实例隐藏——只有手牌扇宿主
            //（HandFanCard）按悬停条件显式开启；预览卡/合成器卡面/对手扇一律不可见
            _actionLayer = transform.Find("btn")?.gameObject;
            if (_actionLayer != null && _actionLayer.activeSelf) _actionLayer.SetActive(false);
            _group = gameObject.GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();

            // keys/stats 的占位文本退役为芯片模板（Clone 源，本体隐藏）
            if (_keysTemplate != null) _keysTemplate.gameObject.SetActive(false);
            if (_statsTemplate != null) _statsTemplate.gameObject.SetActive(false);

            // TMP 兜底（防实例属性漂移；prefab 已同值）：固定格内缩号+省略号
            ConfigureText(_nameText, 8, 12);
            ConfigureText(_costText, 8, 12);
            ConfigureText(_attributeText, 8, 10);
            ConfigureText(_effectText, 8, 11);
            ConfigureText(_passivesText, 8, 11);
            _bound = true;
        }

        /// <summary>全量重绘（幂等）：Full=六区全开；其余形态保底 type/name/费用。</summary>
        public void Apply(CardOverlayItem item, CardOverlayLayout layout)
        {
            if (!_bound) Bind();
            if (item == null) return;
            bool full = layout == CardOverlayLayout.Full;

            // type 区：底图+遮罩随类型换（Sprite 有缓存，重绑幂等）；卡费方格
            SetTypeVisuals(item.Supertype);
            // 立绘（2026-10-15 定案）：法术/结界无合适立绘——缺省空白不放图（清 sprite，
            // 卡实例池复用防上一张生物的图串位）；生物=按名两级找图，空/未命中保持预制体占位
            bool artless = item.Supertype == Cardtype.Spell || item.Supertype == Cardtype.Enchantment;
            if (_art != null)
            {
                if (artless)
                {
                    if (_art.sprite != null) { _art.sprite = null; _artLoaded = null; }
                }
                else if (!string.IsNullOrEmpty(item.Illustration) && item.Illustration != _artLoaded)
                    TrySetArt(item.Illustration);
            }
            if (_typeCost != null)
            {
                if (item.Costs != null && !item.Costs.IsZero) _typeCost.SetCosts(item.Costs);
                else _typeCost.Clear();
            }

            SetText(_nameText, item.Name);

            // cost 区：代价行（PayloadCost=产黑白元素等；空=藏块）
            bool hasPayload = full && !string.IsNullOrEmpty(item.PayloadText);
            SetActive(_costBlock, hasPayload);
            if (hasPayload) SetText(_costText, item.PayloadText);

            // attribute 区：攻击/生命/耐久全词直书（生物=攻击+生命、结界=耐久；空=藏块）——
            // 文本由 CardOverlayItem.AttributeTextOf 单源拼装；发动速度不在卡面属性显示
            //（2026-10-15 晚定案：速度归每个主动效果描述句首，见 AtomText.ActivationPrefix）
            bool hasAttr = full && !string.IsNullOrEmpty(item.AttributeText);
            SetActive(_attributeBlock, hasAttr);
            if (hasAttr) SetText(_attributeText, item.AttributeText);

            // effect 区：无主动效果整块隐藏（根 VLG 自动收拢）
            bool hasActive = full && !string.IsNullOrEmpty(item.ActiveEffectText);
            SetActive(_effectBlock, hasActive);
            if (hasActive)
            {
                SetText(_effectText, item.ActiveEffectText);
                bool hasCost = item.ActiveEffectCost != null && !item.ActiveEffectCost.IsZero;
                SetActive(_effectCostRow, hasCost);
                if (_effectCost != null)
                {
                    if (hasCost) _effectCost.SetCosts(item.ActiveEffectCost);
                    else _effectCost.Clear();
                }
            }

            // passives 区
            bool hasPassives = full && item.PassiveLines != null && item.PassiveLines.Count > 0;
            SetActive(_passivesBlock, hasPassives);
            if (hasPassives) SetText(_passivesText, string.Join("\n", item.PassiveLines));

            // keys / stats 芯片区（无内容整块隐藏）
            if (full)
            {
                FillChips(_keysBlock, _keysTemplate, ChipLabels(item.KeywordChips));
                FillChips(_statsBlock, _statsTemplate, StatLabels(item.CounterChips));
            }
            else
            {
                SetActive(_keysBlock, false);
                SetActive(_statsBlock, false);
            }

            if (_group != null) _group.alpha = item.IsTapped ? 0.62f : 1f;

            // 运行态刷新锚（本地模式才有引擎对象；null=静态渲染不订阅）
            _runtimeCard = full ? item.RuntimeCard : null;
            if (isActiveAndEnabled) SyncSubscriptions();
            UpdateBack();
        }

        /// <summary>正/背面：false=卡背盖住正面（对手手牌伏面）。</summary>
        public void SetFaceUp(bool faceUp)
        {
            _faceUp = faceUp;
            UpdateBack();
        }

        /// <summary>放置/发动按钮层显隐（null 安全幂等）：仅手牌扇宿主调用；逐钮显隐由宿主对子 Button 控制。</summary>
        public void SetActionLayerVisible(bool visible)
        {
            if (_actionLayer != null && _actionLayer.activeSelf != visible)
                _actionLayer.SetActive(visible);
        }

        private void UpdateBack()
        {
            if (!_faceUp && _back == null) EnsureBackPrefab(); // 伏面首次落位（惰性建卡背实例）
            if (_back != null && _back.activeSelf == _faceUp)
                _back.SetActive(!_faceUp);
        }

        /// <summary>伏面=ACardBack.prefab 实例（2026-10-14 晚恢复：ACard 内 back 节点已随卡面改版退役，
        /// FaceUp=false 时改挂整卡背 prefab 拉伸盖面；只建一次，翻正面即隐藏复用）。
        /// 对手手牌卡背/被展示（Exposed）才见详细信息——宿主屏 RefreshFans 口径。</summary>
        private void EnsureBackPrefab()
        {
            var prefab = Tide.HotUpdate.HotUpdateAssets.Load<GameObject>("ACardBack", UiKit.PrefabPath("ACardBack"));
            if (prefab == null)
            {
                Debug.LogError("[ACardView] ACardBack.prefab 缺失（" + UiKit.PrefabPath("ACardBack") + "）——伏面卡背不可用");
                return;
            }
            _back = Instantiate(prefab, transform, false);
            _back.name = "ACardBack"; // 层级直书预制体名（Bind 按名找回，重绑幂等）
            var rt = (RectTransform)_back.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero; // 拉伸盖满卡根（132×288）
            // 卡根挂竖排布局（VLG）：未逃逸的子节点会被当排版项挪位（btn 悬浮层同坑）——
            // 卡背要整格盖面必须 ignoreLayout（2026-10-14 修：对手手牌伏面被排到卡外=显示成正面）
            var le = _back.AddComponent<LayoutElement>();
            le.ignoreLayout = true;
            _back.transform.SetAsLastSibling();         // 盖住六区内容
        }

        // ==================== 运行态刷新（事件→只读重查→重绘） ====================

        private void OnEnable() => SyncSubscriptions();

        private void OnDisable()
        {
            if (_subscribed) UnsubscribeAll();
        }

        private void SyncSubscriptions()
        {
            bool want = _runtimeCard != null;
            if (want == _subscribed) return;
            if (want) SubscribeAll();
            else if (_subscribed) UnsubscribeAll();
        }

        private void SubscribeAll()
        {
            var bus = EventManager.Instance;
            if (bus == null) return;
            _onCounter = e => { if (IsFor(e.Target)) Rebuild(); };
            _onKeyword = e => { if (IsFor(e.Target)) Rebuild(); };
            _onUsage = e => { if (e.EffectId == null || IsFor(e.Source)) Rebuild(); }; // null=整账重置播报
            _onTurn = e => Rebuild();                                              // x 随回合归零
            bus.Subscribe(_onCounter);
            bus.Subscribe(_onKeyword);
            bus.Subscribe(_onUsage);
            bus.Subscribe(_onTurn);
            _subscribed = true;
        }

        private void UnsubscribeAll()
        {
            var bus = EventManager.Instance;
            if (bus != null)
            {
                if (_onCounter != null) bus.Unsubscribe(_onCounter);
                if (_onKeyword != null) bus.Unsubscribe(_onKeyword);
                if (_onUsage != null) bus.Unsubscribe(_onUsage);
                if (_onTurn != null) bus.Unsubscribe(_onTurn);
            }
            _subscribed = false;
        }

        private bool IsFor(Entity t) => t != null && _runtimeCard != null && ReferenceEquals(t, _runtimeCard);

        /// <summary>从引擎活卡重快照重建显示模型（只读查询，FromLiveCard→FromHand 同链）。</summary>
        private void Rebuild()
        {
            if (_runtimeCard == null) return;
            Apply(CardOverlayItem.FromLiveCard(_runtimeCard), CardOverlayLayout.Full);
        }

        // ==================== type 区 ====================

        private void SetTypeVisuals(Cardtype type)
        {
            if (!TypeAssets.TryGetValue(type, out var assets))
                assets = ("biology", "mask_square"); // 其他类型兜底生物底（手牌不常见）
            var bg = LoadSprite(assets.bg);
            if (_typeBg != null && bg != null) _typeBg.sprite = bg;
            var mask = LoadSprite(assets.mask);
            if (_mask != null && mask != null) _mask.sprite = mask;
        }

        private void TrySetArt(string illustration)
        {
            var sp = CardArtLibrary.Load(illustration);
            if (sp == null) return;
            if (_art != null) _art.sprite = sp;
            _artLoaded = illustration;
        }

        private static Sprite LoadSprite(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (SpriteCache.TryGetValue(name, out var s)) return s;
            s = Tide.HotUpdate.HotUpdateAssets.Load<Sprite>(name, SpritePath(name));
            SpriteCache[name] = s; // 加载失败也缓存 null，避免逐卡重试
            return s;
        }

        // ==================== keys / stats 芯片区 ====================

        private static List<string> ChipLabels(List<string> chips) => chips;

        private static List<string> StatLabels(List<(string Name, int Layers)> counters)
        {
            if (counters == null || counters.Count == 0) return null;
            var labels = new List<string>(counters.Count);
            foreach (var c in counters) labels.Add($"{c.Name}×{c.Layers}");
            return labels;
        }

        /// <summary>芯片流式排布：按 preferredWidth 累计分行（行=HLG，块内 VLG 纵排）——uGUI 无现成 wrap 布局。
        /// 固定尺寸卡面口径（2026-10-14）：最多 2 行，丢弃的芯片并成末行「+N」；行宽预算取 sizeDelta（定宽，
        /// 不依赖布局时机）；块高=6+行数×15 回写（1 行=21、2 行=36）。labels 空=整块隐藏。
        /// template=芯片克隆源（字体随预制体），本体隐藏。</summary>
        private void FillChips(RectTransform block, RectTransform template, List<string> labels)
        {
            if (block == null) return;
            if (labels == null || labels.Count == 0)
            {
                block.gameObject.SetActive(false);
                return;
            }
            block.gameObject.SetActive(true);

            // 清旧芯片行（模板之外全毁；编辑态探针走 DestroyImmediate）
            for (int i = block.childCount - 1; i >= 0; i--)
                if (!ReferenceEquals(block.GetChild(i), template))
                    DestroyObj(block.GetChild(i).gameObject);

            var vlg = block.GetComponent<VerticalLayoutGroup>();
            float budget = block.sizeDelta.x - (vlg != null ? vlg.padding.horizontal : 0) - 1f;
            HorizontalLayoutGroup row = null;
            float used = 0f;
            int rows = 0, dropped = 0;
            const int maxRows = 2;
            foreach (var label in labels)
            {
                var chip = Instantiate(template, block);
                chip.gameObject.SetActive(true);
                var t = chip.GetComponent<TMP_Text>();
                if (t != null)
                {
                    t.text = label;
                    t.fontSize = 11;
                }
                float w = t != null ? t.preferredWidth : 30f;
                if (row == null || used + w > budget)
                {
                    if (rows == maxRows) { dropped++; DestroyObj(chip.gameObject); continue; } // 超行数上限：丢弃计数
                    var rowGo = new GameObject("row", typeof(RectTransform));
                    rowGo.transform.SetParent(block, false);
                    row = rowGo.AddComponent<HorizontalLayoutGroup>();
                    row.spacing = 2f;
                    row.childAlignment = TextAnchor.UpperLeft;
                    row.childControlWidth = true;
                    row.childControlHeight = true;
                    row.childForceExpandWidth = false;
                    row.childForceExpandHeight = false;
                    used = 0f;
                    rows++;
                }
                chip.SetParent(row.transform, false);
                used += w + row.spacing;
            }
            if (dropped > 0 && row != null)
            {
                var overflowChip = Instantiate(template, row.transform);
                overflowChip.gameObject.SetActive(true);
                var ot = overflowChip.GetComponent<TMP_Text>();
                if (ot != null)
                {
                    ot.text = "+" + dropped;
                    ot.fontSize = 11;
                }
            }

            // 块高随行数（固定卡内的局部弹性；根 VLG 重排下方区块）
            block.sizeDelta = new Vector2(block.sizeDelta.x, 6f + rows * 15f);
        }

        // ==================== 小工具 ====================

        private RectTransform Rt(string path) => _faceRoot.Find(path) as RectTransform;

        private Image FindImage(string path)
        {
            var t = _faceRoot.Find(path);
            return t != null ? t.GetComponent<Image>() : null;
        }

        private TMP_Text FindText(string path)
        {
            var t = _faceRoot.Find(path);
            return t != null ? t.GetComponent<TMP_Text>() : null;
        }

        private T FindComponent<T>(string path) where T : Component
        {
            var t = _faceRoot.Find(path);
            return t != null ? t.GetComponentInChildren<T>(true) : null;
        }

        private static void SetText(TMP_Text t, string value)
        {
            if (t != null) t.text = value ?? "";
        }

        /// <summary>固定格文本兜底：自动缩号 + 省略号（值与 prefab 一致，防实例漂移）。</summary>
        private static void ConfigureText(TMP_Text t, int min, int max)
        {
            if (t == null) return;
            t.enableAutoSizing = true;
            t.fontSizeMin = min;
            t.fontSizeMax = max;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.enableWordWrapping = true;
        }

        /// <summary>编辑态（探针/预制体预览）Destroy 分流，防 edit-mode 报错。</summary>
        private static void DestroyObj(GameObject go)
        {
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }

        private static void SetActive(Component c, bool active)
        {
            if (c != null && c.gameObject.activeSelf != active) c.gameObject.SetActive(active);
        }
    }
}
