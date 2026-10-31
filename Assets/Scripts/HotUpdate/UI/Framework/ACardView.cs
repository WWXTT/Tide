using System;
using System.Collections.Generic;
using System.Linq;
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
        /// 节点约定（2026-10-30 美术定档：全部美术烘死在 prefab——底图/遮罩/徽章底/行样式代码不再
        /// 控制，只按类型点亮整块+填数据；缺节点各区空转不崩）：
        /// biology/barrier/spell 三个类型整块（prefab 默认 off，Apply 按卡类型单开：生物=biology、
        /// 结界=barrier、法术=spell，Hero=角色占位卡三块全关）；立绘=块内 mask/art（按 Illustration
        /// 换图，空/未命中回落预制体占位防池复用串图）；上半数字徽章=块内 Atc/Hea TMP（生物=攻/血
        /// 双徽章、结界=耐久 Hea、法术无徽章）；name（TMP 直挂）；行区 form-scroll/viewport/content/
        /// {effect,passives,keys,states}=烘焙标题 text + des 内容（按内容点亮；states=指示物行，
        /// 2026-10-30 prefab 补节点后接线）。
        /// 已随 2026-10-30 定档退役：type 区换底图（TypeAssets/SpriteCache）、type/effect 费用方格
        /// （CostSquaresView——卡费另觅宿主）、cost 代价行、attribute 区与 B6 运行时攻/血图标行、
        /// back 内嵌节点（伏面=ACardBack 整卡实例，保留）。
        /// 用户 btn 悬浮层（AHandCard 兄弟节点，非本 prefab）不参与卡面结构。
        /// prefab 保持无脚本（CardOverlayController/HandFanView 加载后 AddComponent）。
    /// 非 Full 形态=保底显示类型块/name（战场实体卡面另案，不做设计投入）。
    /// </summary>
    public sealed class ACardView : MonoBehaviour
    {
        // ---- 节点缓存（Bind 一次解析；美术定档后只解析数据宿主，无换图控件） ----
        private RectTransform _bioBlock, _barrierBlock, _spellBlock;   // 类型整块（美术全烘，默认 off）
        private Image _bioArt, _barrierArt, _spellArt;                 // 块内立绘（mask/art）
        private Sprite _bioArtBaked, _barrierArtBaked, _spellArtBaked; // 立绘预制体占位（未命中回落）
        private TMP_Text _bioAtc, _bioHea, _barrierHea;                // 上半数字徽章（生物攻/血·结界耐久）
        private TMP_Text _nameText, _effectText, _passivesText, _keysText, _statesText;
        private RectTransform _effectBlock, _passivesBlock, _keysBlock, _statesBlock;
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

        private static GameObject _backPrefab;   // ACardBack prefab 静态缓存（EnsureBackPrefab 单次 Load）
        private static bool _backPrefabTried;

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
            // 类型整块（2026-10-30 美术定档）：bg/mask/徽章底全烘死——代码只点亮对应块+填数；
            // 立绘占位按块缓存（Illustration 未命中时回落，防卡池复用串上一张的图）
            _bioBlock = Rt("biology");
            _barrierBlock = Rt("barrier");
            _spellBlock = Rt("spell");
            _bioArt = ArtOf(_bioBlock);
            _barrierArt = ArtOf(_barrierBlock);
            _spellArt = ArtOf(_spellBlock);
            _bioArtBaked = _bioArt != null ? _bioArt.sprite : null;
            _barrierArtBaked = _barrierArt != null ? _barrierArt.sprite : null;
            _spellArtBaked = _spellArt != null ? _spellArt.sprite : null;
            _bioAtc = DeepText(_bioBlock, "Atc");
            _bioHea = DeepText(_bioBlock, "Hea");
            _barrierHea = DeepText(_barrierBlock, "Hea");
            // name 直挂 TMP；四行区嵌在 form-scroll/viewport/content 下（text=烘焙标题、des=内容）
            _nameText = FindText("name/text") ?? FindText("name");
            _effectBlock = ZoneBlock("effect", out _, out _effectText);
            _passivesBlock = ZoneBlock("passives", out _, out _passivesText);
            _keysBlock = ZoneBlock("keys", out _, out _keysText);
            _statesBlock = ZoneBlock("states", out _, out _statesText);
            _back = transform.Find("ACardBack")?.gameObject ?? transform.Find("back")?.gameObject;
            // 用户 btn 悬浮层（放置/发动按钮，2026-10-14）：默认全实例隐藏——只有手牌扇宿主
            //（HandFanCard）按悬停条件显式开启；预览卡/合成器卡面/对手扇一律不可见
            _actionLayer = transform.Find("btn")?.gameObject;
            if (_actionLayer != null && _actionLayer.activeSelf) _actionLayer.SetActive(false);
            _group = gameObject.GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
            _bound = true;
        }

        /// <summary>全量重绘（幂等）：Full=全信息；其余形态保底类型块/name。</summary>
        public void Apply(CardOverlayItem item, CardOverlayLayout layout)
        {
            if (!_bound) Bind();
            if (item == null) return;
            bool full = layout == CardOverlayLayout.Full;

            // 类型整块：按卡类型单开（美术全烘，代码只管点亮/熄灭）；Hero=角色占位卡（选目标
            // 面板的 Player 候选）无类型块——三块全关，只剩卡名【己方/对方角色】
            bool rolePlaceholder = item.Supertype == Cardtype.Hero;
            bool isBio = !rolePlaceholder && item.Supertype == Cardtype.Creature;
            bool isBarrier = !rolePlaceholder && item.Supertype == Cardtype.Enchantment;
            bool isSpell = !rolePlaceholder && item.Supertype == Cardtype.Spell;
            SetActive(_bioBlock, isBio);
            SetActive(_barrierBlock, isBarrier);
            SetActive(_spellBlock, isSpell);

            // 立绘：按 Illustration 换图（两级找图 CardArtLibrary，miss 亦有缓存）；
            // 空/未命中回落预制体占位（三块各自缓存，池复用不串图）
            var art = isBio ? _bioArt : isBarrier ? _barrierArt : isSpell ? _spellArt : null;
            if (art != null)
            {
                var baked = isBio ? _bioArtBaked : isBarrier ? _barrierArtBaked : _spellArtBaked;
                var sprite = string.IsNullOrEmpty(item.Illustration) ? null : CardArtLibrary.Load(item.Illustration);
                art.sprite = sprite != null ? sprite : baked;
            }

            // 上半数字徽章（2026-10-30 定档）：生物=攻/血、结界=耐久、法术无徽章
            if (isBio)
            {
                SetText(_bioAtc, (item.AttrPower ?? 0).ToString());
                SetText(_bioHea, (item.AttrLife ?? 0).ToString());
            }
            else if (isBarrier)
                SetText(_barrierHea, (item.AttrDurability ?? 0).ToString());

            SetText(_nameText, item.Name);

            // effect 区：无主动效果整块隐藏（根 VLG 自动收拢）
            bool hasActive = full && !string.IsNullOrEmpty(item.ActiveEffectText);
            SetActive(_effectBlock, hasActive);
            if (hasActive) SetText(_effectText, item.ActiveEffectText);

            // passives 区
            bool hasPassives = full && item.PassiveLines != null && item.PassiveLines.Count > 0;
            SetActive(_passivesBlock, hasPassives);
            if (hasPassives) SetText(_passivesText, string.Join("\n", item.PassiveLines));

            // keys 区（行式文本）：关键词并一行入 des
            bool hasKeys = full && item.KeywordChips != null && item.KeywordChips.Count > 0;
            SetActive(_keysBlock, hasKeys);
            if (hasKeys) SetText(_keysText, string.Join("　", item.KeywordChips));

            // states 区（指示物行——2026-10-30 prefab 补节点后接线）：名称×层数并一行
            bool hasCounters = full && item.CounterChips != null && item.CounterChips.Count > 0;
            SetActive(_statesBlock, hasCounters);
            if (hasCounters) SetText(_statesText, string.Join("　", item.CounterChips.Select(c => c.Name + "×" + c.Layers)));

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

        // ==================== 类型块解析助手 ====================

        /// <summary>块内立绘节点（block/mask/art；缺块/缺 art=null 空转）。</summary>
        private static Image ArtOf(RectTransform block)
        {
            var t = block != null ? block.Find("mask/art") : null;
            return t != null ? t.GetComponent<Image>() : null;
        }

        /// <summary>块内按名深找 TMP（徽章 Atc/Hea 挂在同名徽章底 Image 下，路径不定长）。</summary>
        private static TMP_Text DeepText(RectTransform block, string nodeName)
        {
            if (block == null) return null;
            foreach (var tmp in block.GetComponentsInChildren<TMP_Text>(true))
                if (tmp.name == nodeName) return tmp;
            return null;
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

        private TMP_Text FindText(string path)
        {
            var t = _faceRoot.Find(path);
            return t != null ? t.GetComponent<TMP_Text>() : null;
        }

        private static void SetText(TMP_Text t, string value)
        {
            if (t != null) t.text = value ?? "";
        }

        private static void SetActive(Component c, bool active)
        {
            if (c != null && c.gameObject.activeSelf != active) c.gameObject.SetActive(active);
        }
    }
}
