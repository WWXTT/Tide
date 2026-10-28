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
        /// 卡面结构（2026-10-28 新卡 360×648 分数锚替换 ACard.prefab 定案）：type/name 分数锚 +
        /// form-scroll 行区（cost/effect/passives/keys 行=text 标题+des 内容；des 定号 16+Overflow
        /// 超高滚动，行 prefab 默认 inactive、Apply 按内容点亮）；attribute/stats/效果发动费暂无
        /// 节点=空转。放大=localScale 整卡同比（2026-10-28 晚回退定案——文字随变换缩放）。
        /// 用户 btn 悬浮层（地牌/使用按钮）不参与卡面结构。
        /// prefab 保持无脚本（CardOverlayController/HandFanView 加载后 AddComponent）。
    /// 非 Full 形态=保底显示 type/name/费用（战场实体卡面另案，不做设计投入）。
    /// </summary>
    public sealed class ACardView : MonoBehaviour
    {
        // ---- 节点缓存（Bind 一次解析） ----
        private Image _typeBg, _mask, _art;
        private RectTransform _typeBlock;
        private CostSquaresView _typeCost;
        private TMP_Text _nameText, _effectText, _passivesText, _attributeText, _costText, _keysText;
        private TMP_Text _costTitle, _effectTitle, _passivesTitle, _keysTitle; // 新卡行标题（行=text 标题 + des 内容）
        private RectTransform _attrIconRow;             // B6 攻/血图标行（attribute 块内运行时建，仿 FillChips 行构建）
        private TMP_Text _attrPowerText, _attrLifeText; // 图标行数字（字体/色取 attribute/text 同源）
        private RectTransform _effectBlock, _effectCostRow, _passivesBlock, _keysBlock, _statsBlock, _attributeBlock, _costBlock;
        private CostSquaresView _effectCost;
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
        private static GameObject _backPrefab;   // ACardBack prefab 静态缓存（EnsureBackPrefab 单次 Load）
        private static bool _backPrefabTried;
        private static string SpritePath(string name) => $"Assets/Art/Icons/{name}.png";
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
            _typeBlock = Rt("type");
            _typeBg = FindImage("type/bg");
            _mask = FindImage("type/mask");
            _art = FindImage("type/mask/art");
            _typeCost = FindComponent<CostSquaresView>("type");
            // 新卡（2026-10-28 360×648 分数锚）：name 即 TMP；四行区嵌在 form-scroll/viewport/content
            // 下（行=text 标题 + des 内容，des 定号 16+滚动）；attribute/stats/效果发动费暂无节点空转
            _nameText = FindText("name/text") ?? FindText("name");
            _costBlock = ZoneBlock("cost", out _costTitle, out _costText);
            _effectBlock = ZoneBlock("effect", out _effectTitle, out _effectText);
            _effectCost = FindComponent<CostSquaresView>("effect");
            // cost 行=预留 effect/cost 容器，缺省回落 CostSquares 实例根本体（隐藏即藏费用行）
            _effectCostRow = Rt("effect/cost") ?? (_effectCost != null ? (RectTransform)_effectCost.transform : null);
            _passivesBlock = ZoneBlock("passives", out _passivesTitle, out _passivesText);
            _keysBlock = ZoneBlock("keys", out _keysTitle, out _keysText);
            _statsBlock = Rt("stats");
            _attributeBlock = Rt("attribute");
            _attributeText = FindText("attribute/text");
            SetText(_costTitle, "代价：");
            SetText(_effectTitle, "效果：");
            SetText(_passivesTitle, "被动：");
            SetText(_keysTitle, "关键词：");
            _back = transform.Find("ACardBack")?.gameObject ?? transform.Find("back")?.gameObject;
            // 用户 btn 悬浮层（放置/发动按钮，2026-10-14）：默认全实例隐藏——只有手牌扇宿主
            //（HandFanCard）按悬停条件显式开启；预览卡/合成器卡面/对手扇一律不可见
            _actionLayer = transform.Find("btn")?.gameObject;
            if (_actionLayer != null && _actionLayer.activeSelf) _actionLayer.SetActive(false);
            _group = gameObject.GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();

            // 字号口径（2026-10-28 新卡）：三档全由 prefab 定——名称 25（18-25 自动缩号）/
            // 关键词行 18/效果文本 16；放大=localScale 同比渲染，View 不改字号
            _bound = true;
        }

        /// <summary>全量重绘（幂等）：Full=六区全开；其余形态保底 type/name/费用。</summary>
        public void Apply(CardOverlayItem item, CardOverlayLayout layout)
        {
            if (!_bound) Bind();
            if (item == null) return;
            bool full = layout == CardOverlayLayout.Full;

            // type 区：底图+遮罩随类型换（Sprite 有缓存，重绑幂等）；卡费方格。
            // Hero=角色占位卡（选目标面板的 Player 候选，2026-10-16）：非卡实体无底图/遮罩/立绘
            // ——整区隐藏（根 VLG 收拢，只剩卡名【己方/对方角色】）
            bool rolePlaceholder = item.Supertype == Cardtype.Hero;
            SetActive(_typeBlock, !rolePlaceholder);
            if (rolePlaceholder)
            {
                if (_art != null && _art.sprite != null) { _art.sprite = null; _artLoaded = null; }
            }
            else
            {
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
            }

            SetText(_nameText, item.Name);

            // cost 区：代价行（PayloadCost=产黑白元素等；空=藏块）
            bool hasPayload = full && !string.IsNullOrEmpty(item.PayloadText);
            SetActive(_costBlock, hasPayload);
            if (hasPayload) SetText(_costText, item.PayloadText);

            // attribute 区（2026-10-22 B6 图标化）：生物=剑/心图标+数字（FightIcon/HeartIcon 双就绪才走
            // 图标档——缺素材回落全词文本，AttributeText 恒为回落源）；结界=耐久纯文本；空=藏块
            var atkIcon = LoadSprite("FightIcon");
            var hpIcon = LoadSprite("HeartIcon");
            bool iconMode = full && item.AttrPower.HasValue && item.AttrLife.HasValue
                            && atkIcon != null && hpIcon != null;
            bool hasAttr = full && (iconMode || !string.IsNullOrEmpty(item.AttributeText));
            SetActive(_attributeBlock, hasAttr);
            if (hasAttr)
            {
                if (iconMode) FillAttributeIcons(item, atkIcon, hpIcon);
                else
                {
                    if (_attrIconRow != null && _attrIconRow.gameObject.activeSelf)
                        _attrIconRow.gameObject.SetActive(false); // 池复用回落文本档
                    SetActive(_attributeText, true);               // 还显全词文本（图标档曾藏）
                    SetText(_attributeText, item.AttributeText);
                }
            }

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

            // keys 区（2026-10-28 新卡行式文本）：关键词并一行入 des；stats 新卡无节点=空转
            //（指示物层数暂无宿主行，待卡面补节点后恢复）
            bool hasKeys = full && item.KeywordChips != null && item.KeywordChips.Count > 0;
            SetActive(_keysBlock, hasKeys);
            if (hasKeys) SetText(_keysText, string.Join("　", item.KeywordChips));
            SetActive(_statsBlock, false);

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
            // 静态缓存（2026-10-18 性能）：对手手牌每张伏面卡首次落位都 Load 一次 prefab——
            // HotUpdateAssets 句柄永生累积（长局泄漏）；同 SpriteCache 口径缓存引用
            if (_backPrefab == null && !_backPrefabTried)
            {
                _backPrefab = Tide.HotUpdate.HotUpdateAssets.Load<GameObject>("ACardBack", UiKit.PrefabPath("ACardBack"));
                _backPrefabTried = true; // 失败也只试一次（错误日志下方单发）
                if (_backPrefab == null)
                    Debug.LogError("[ACardView] ACardBack.prefab 缺失（" + UiKit.PrefabPath("ACardBack") + "）——伏面卡背不可用");
            }
            var prefab = _backPrefab;
            if (prefab == null)
                return;
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

        // ==================== attribute 图标行（2026-10-22 B6 玩家反馈：攻/血一行数字不醒目） ====================

        /// <summary>攻/血图标+数字行：剑图标+攻数字+心图标+血数字（玩家建议 ❤:3 🗡:5 式）。
        /// 行在 attribute 块内运行时构建（块高 20 保持不动——根 VLG 下游零位移）；数字字号 12
        /// 固定（高于文本档 max 10，突出主数值）；字体/颜色取 attribute/text 同源。
        /// 图标档激活期藏全词文本；池复用回落文本档由 Apply 侧负责还显。</summary>
        private void FillAttributeIcons(CardOverlayItem item, Sprite atkIcon, Sprite hpIcon)
        {
            if (_attributeBlock == null || _attributeText == null) return;
            if (_attrIconRow == null) BuildAttrIconRow(atkIcon, hpIcon);
            if (_attrIconRow == null) return; // 构建异常兜底：保持上一帧（极端缺组件不崩）
            _attributeText.gameObject.SetActive(false); // 图标档藏全词文本
            _attrIconRow.gameObject.SetActive(true);
            _attrPowerText.text = item.AttrPower.Value.ToString();
            _attrLifeText.text = item.AttrLife.Value.ToString();
        }

        private void BuildAttrIconRow(Sprite atkIcon, Sprite hpIcon)
        {
            var rowGo = new GameObject("icon-row", typeof(RectTransform));
            var row = (RectTransform)rowGo.transform;
            row.SetParent(_attributeBlock, false);
            row.anchorMin = Vector2.zero;
            row.anchorMax = Vector2.one;
            row.offsetMin = row.offsetMax = Vector2.zero;
            var hlg = rowGo.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 5f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.padding = new RectOffset(8, 4, 0, 0);
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            Image IconNode(string name, Sprite sprite)
            {
                var go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(row, false);
                var img = go.AddComponent<Image>();
                img.sprite = sprite;
                img.preserveAspect = true;
                img.raycastTarget = false;
                var le = go.AddComponent<LayoutElement>();
                le.preferredWidth = 13f;
                le.preferredHeight = 13f;
                return img;
            }

            TMP_Text NumNode(string name)
            {
                var go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(row, false);
                var t = go.AddComponent<TextMeshProUGUI>();
                if (_attributeText != null)
                {
                    t.font = _attributeText.font;
                    t.color = _attributeText.color;
                }
                t.fontSize = 12f;
                t.fontStyle = FontStyles.Bold;
                t.alignment = TextAlignmentOptions.MidlineLeft;
                t.enableWordWrapping = false;
                t.raycastTarget = false;
                return t;
            }

            IconNode("atk-icon", atkIcon);
            _attrPowerText = NumNode("atk-num");
            IconNode("hp-icon", hpIcon);
            _attrLifeText = NumNode("hp-num");
            _attrIconRow = row;
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

        /// <summary>行区解析（2026-10-28 新卡口径）：块=form-scroll/viewport/content/<zone>（回落根级
        /// 旧行）；title=行 text 节点、content=行 des 节点（旧行无 des 时内容写 text 本体）。</summary>
        private RectTransform ZoneBlock(string zone, out TMP_Text title, out TMP_Text content)
        {
            var block = Rt("form-scroll/viewport/content/" + zone) ?? Rt(zone);
            title = TextOf(block, "text");
            content = TextOf(block, "des") ?? TextOf(block, "text");
            return block;
        }

        private static TMP_Text TextOf(RectTransform block, string child)
        {
            if (block == null) return null;
            var t = block.Find(child);
            return t != null ? t.GetComponent<TMP_Text>() : null;
        }

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
