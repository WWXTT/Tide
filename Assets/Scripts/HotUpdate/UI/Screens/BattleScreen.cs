using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.AI;
using CardCore.Attribute;
using CardCore.Network;
using CardCore.Serialization;
using GameBoard;
using Tide.HotUpdate;
    using Cysharp.Threading.Tasks;
    using UnityEngine;
    using UnityEngine.EventSystems;
    using UnityEngine.UI;
    using TMPro;

namespace SynergyUI
{
    /// <summary>对战模式。</summary>
    public enum BattleMode
    {
        /// <summary>本地 vs 极简 AI（GameCore 直连）。</summary>
        LocalAI,
        /// <summary>网络对局（快照渲染 + intent 上行）。</summary>
        Network,
        /// <summary>教学固定局（2026-10-02）：双方卡组顺序=摸牌序列（锁牌库序）、机器人逐回合剧本演出
        /// （ScriptedAi）、响应窗口全让过——渲染/交互与本地 AI 局完全同轨。</summary>
        Tutorial,
        /// <summary>挑战模式（2026-10-06）：玩家自选卡组，电脑按难度 N 获得非对称优势——
        /// 地牌槽上限 7+N（N≥7 触顶 14）、起手 6+N（InitGame 加成参数）。渲染/交互与本地 AI 局同轨。</summary>
        Challenge,
    }

    /// <summary>
    /// 对战入口参数（UIManager.Show 无参构造——静态传参）。
    /// 连接配置属匹配界面（阶段三）职责；当前注入方=主菜单调试按钮（默认本机）与
    /// 匹配界面（Client 接续：已进房/已提交卡组的现成连接，本屏消费后接管生命周期）。
    /// </summary>
    public static class BattleEntry
    {
        public static BattleMode Mode = BattleMode.LocalAI;
        public static string Host = "127.0.0.1";
        public static int Port = 8090;
        public static string Nickname = "player";

        /// <summary>匹配界面移交的现成连接（非空=已进房+已提交卡组，跳过连接与 JoinRoom，
        /// 一次性消费防跨局残留）。null=调试直连路径（本屏自建连接）。</summary>
        public static NetGameClient Client;

        /// <summary>教学局关卡 id（TutorialLibrary 条目；缺省 "basics" 基础教学）。
        /// 入口方接线：BattleEntry.Mode = BattleMode.Tutorial 后 Show&lt;BattleScreen&gt;。</summary>
        public static string TutorialId = "basics";

        /// <summary>镜像局卡组（2026-10-06 最后一课/考核局）：非空=双方同用这份卡组开局
        /// （AI 操纵玩家卡组的镜像）。一次性消费防跨局残留（StartLocalFan 开局即置空）。
        /// 注入方=TutorialCreationFlow/TutorialFlow（Mode=LocalAI + 本字段）。</summary>
        public static List<CardData> MirrorDeck;

        /// <summary>镜像局技能标记卡 ID（2026-10-07 卡牌化）：与 MirrorDeck 成对一次性消费——
        /// 双方同用（镜像口径）；null=镜像局无技能。</summary>
        public static string MirrorSkillCardId;

        /// <summary>挑战模式难度（2026-10-06，1..9 单值联动）：电脑地牌槽上限 7+N（N≥7 触顶 14）、起手 6+N。
        /// 与 TutorialId 同口径：注入方设值后保持到终局（ReportBattleResult 读取落档），
        /// 每次进挑战模式都会重设，无跨局残留问题。</summary>
        public static int ChallengeDifficulty = 1;

        /// <summary>挑战模式玩家卡组（2026-10-06）：null=随机卡组（RandomDeckFrom 兜底）。
        /// 一次性消费防跨局残留（StartLocalFan 开局即置空）。</summary>
        public static List<CardData> ChallengeDeck;

        /// <summary>挑战模式玩家技能标记卡 ID（2026-10-07 卡牌化）：与 ChallengeDeck 成对
        /// 一次性消费；null=无技能。AI 侧随机卡组在 StartLocalFan 自动挑合格结界。</summary>
        public static string ChallengeSkillCardId;
    }

    /// <summary>
    /// 对战界面（2026-10-02 战场定案 3D + 部分透明 UI 重做：旧 BattleUI.prefab 已删除；
    /// 2026-10-03 本地模式先以「双扇手牌 + 结束回合」过渡面运行——HandFanView 自原型屏移植接入，
    /// 出牌交互与原绑定/对局逻辑封存待 3D 层重接；同日过渡面预制体化：静态层级回归
    /// Assets/Art/UI/BattleUI.prefab（Tools/验证/对战UI/BattleUI预制体装配 幂等重建），
    /// Build=按名绑定（2026-10-07 兜底退役：缺节点 LogError 跳过，不代码补建）。
    /// 历史口径（2026-10-01 预制体化，封存代码对照用）：静态层级来自 BattleUI.prefab，Build 深度按名
    /// 绑定；战场格子/手牌槽复用烘焙节点（按序对应棋盘 x 位），战报行/弹窗运行时构建。
    /// 本地 AI（GameCore/GameActions 直连）与网络（MsgGameStateSync 快照全量渲染 +
    /// NetEventBatch 中文战报 + intent 上行 + SelectRequest 反问弹窗 + PassPriority 让行）。
    /// 战场格子化：9×6 棋盘的双方 2×7 单位区 + 1×7 地牌行按 z 序纵向铺开、奇数行
    /// 半格偏移（odd-r 六角视觉）；本地格位=BoardState 权威，网络=ZoneCards 列表序
    /// 按 BoardLayout 同规重建（零协议改动）。卡面=ACard.prefab 挂进槽位
    /// （CardOverlayController 池化管理）。
    /// </summary>
    public sealed class BattleScreen : UIScreen
    {
        private BattleController _ctrl;                 // 本地模式
        private NetGameClient _net;                     // 网络模式
        private BattleViewData _view;                   // 当前视图（渲染唯一输入）
        private readonly Queue<MsgSelectRequest> _pendingSelects = new Queue<MsgSelectRequest>();
        private bool _selectBusy;
        private int _netRoomPhase = -1;
        private bool _netDeckSent;
        private bool _netGameOver;
        private bool _running;
        private bool _gameEnded;

        // UGUI 卡牌层绑定（卡面挂屏内槽位；RefreshView 全量重建收集）
        private readonly List<CardOverlayBinding> _cardBindings = new List<CardOverlayBinding>();

        // ---- 控件引用（Build 填充） ----
        private TMP_Text _lblMode, _lblToast;
        private TMP_Text _lblPhase, _lblStack, _lblPriority, _lblActivation;
        private Button _btnGrave, _btnSkill, _btnPass, _btnEnd, _btnConcede;
        // 英雄区（2026-10-13 区域变化定案：英雄技能与角色同区）：左键=发动技能（无技能局回落角色攻击），
        // 右键=角色攻击（2026-10-10 角色参战入口保留；目标弹窗待 overlay 节点后可用）。
        private Button _btnHero;
        private TMP_Text _heroLabel;                 // 英雄区双行文案（角色 生命/弹药 + 技能名/费用/横置）
        private RectTransform _tempChip;             // 临时元素指示条（同区腾位显示：本回合钱包余额，空则隐藏）
        private TMP_Text _tempChipLabel;
        private RectTransform _oppLands, _oppUnitsNear, _oppUnitsFar;
        private RectTransform _selfUnitsFar, _selfUnitsNear, _selfLands;
        private RectTransform _selfHand;
        private UiKit.Scroll _logList;
        private RectTransform _overlay, _selectorLayer;
        private UiKit.Modal _modal;                     // 当前通用弹窗（选择/模式/响应/胜负）
        private Button _overlayCancel;

        // ---- 手牌扇形过渡面（2026-10-03 本地对战接入：原型屏 HandFanScreen 移植为本屏本地呈现） ----
        private HandFanView _selfFan, _oppFan;          // 双方手牌扇区（对账键=引擎卡 RuntimeId）
        private Button _btnEndFan;                      // 结束回合（过渡面唯一操作；AI 随后跑完对手回合）

        // ---- 战场 3D 摆位（2026-10-13 战场重做·原型）：消费 BattleViewData 幂等对账（本地/网络同轨） ----
        private BattleFieldView _field;
        private CombatChoreographer _choreo;         // 战斗编舞（2026-10-13 攻击演出：3D 寻路箭头选目标+走位/反击/死亡漩涡）

        // ---- 棋盘指针/区域面板/日志开关（2026-10-13 基础教学三问题：悬停信息+卡面预览、Region 列卡、btn-log） ----
        private ACardView _previewCard;            // 悬浮完整卡面（prefab 内 ACard 实例，默认隐藏）
        private RectTransform _hoverInfo;          // 悬停信息位（与卡面预览互斥）
        private TMP_Text _hoverInfoLabel;
        private string _hoverKey;                  // 当前悬停内容键（防逐帧重绘）
        private RectTransform _logListRect;        // log-list 高度动画目标（0↔700）
        private float _logListTarget;

        // 信息栏动态文本（FillBar 刷新）
        private TMP_Text _oppName, _oppLife, _oppDeck, _oppHand, _oppGrave, _oppFatigue, _oppBank;
        private TMP_Text _selfName, _selfLife, _selfDeck, _selfHandCount, _selfGrave, _selfFatigue, _selfBank;
        private TMP_Text _oppSkillLabel, _selfSkillLabel;
        private CanvasGroup _oppSkillGroup, _selfSkillGroup;

        private bool IsNetwork => BattleEntry.Mode == BattleMode.Network;
        private GameCore Core => _ctrl?.Core;
        private Player P1 => _ctrl?.P1;
        private Player P2 => _ctrl?.P2;

        // ======================================== 构建 ========================================

        // 2026-10-02 定案：战场改为 3D + 部分透明 UI 重做，旧 uGUI 预制体已删除（不具指导意义）。
        // 2026-10-03 过渡面预制体化：静态层级回归 Assets/Art/UI/BattleUI.prefab
        // （Tools/验证/对战UI/BattleUI预制体装配 幂等重建），Build=按名绑定（2026-10-07 起缺节点不补建）；
        // 原预制体绑定逻辑封存于 BindLegacyPrefabNodes、对局启动/刷新链全部保留，待 3D 层重接。
        protected override string PrefabName => "BattleUI";
        protected override string RootName => "battle";

        protected override void Build()
        {
            // 战场=3D 棋盘可见（2026-10-03 定案：UI 不挡世界相机）——底图只留视觉透明；
            // raycastTarget 必须关（2026-10-13 3D 点击修复）：全屏透明 Image 会令
            // EventSystem.IsPointerOverGameObject 恒真，棋盘点击（HandleFieldClick）全被它挡死
            var bg = Root.GetComponent<Image>();
            if (bg != null)
            {
                bg.color = new Color(bg.color.r, bg.color.g, bg.color.b, 0f);
                bg.raycastTarget = false;
            }

            // 返回主菜单（2026-10-13 按钮上盘：prefab 无此节点时由棋盘 hud-tile-back 承担，静默跳过）
            if (UiKit.FindDeep(Root, "btn-back") != null)
                BindBattleButton("btn-back", "← 返回主菜单", OnBack);

            // 终局弹窗层（2026-10-07 兜底退役：prefab 烘焙 overlay；缺失 LogError——各弹窗直接跳过）
            _overlay = Find("overlay");

            // 结束回合：右下角独立锚点，逃逸屏根纵向布局（不占顶部）
            _btnEndFan = BindBattleButton("btn-end-turn", "结束回合", OnEndTurnFan);
            if (_btnEndFan != null)
            {
                var endRt = (RectTransform)_btnEndFan.transform;
                var endLe = endRt.GetComponent<LayoutElement>() ?? endRt.gameObject.AddComponent<LayoutElement>();
                endLe.ignoreLayout = true;
                endRt.anchorMin = endRt.anchorMax = new Vector2(1f, 0f);
                endRt.anchoredPosition = new Vector2(-24f, 240f); // 手牌扇侧翼上方
                var endLbl = _btnEndFan.GetComponentInChildren<TMP_Text>();
                endRt.sizeDelta = new Vector2((endLbl != null ? endLbl.preferredWidth : 0f) + 28f, 36f);
                _btnEndFan.gameObject.SetActive(false);           // 仅本地对局显示（OnEnter 开）
            }

            // 英雄区（2026-10-13 区域变化定案：英雄技能与角色同区——技能并入角色入口，左键=发动技能、
            // 右键=角色攻击）：与结束回合对称的左下角锚点——本地对局专用；文案随 RefreshHeroZone 动态刷新
            _btnHero = BindBattleButton("btn-self-hero", "角色\n技能", OnClickHeroZone);
            if (_btnHero != null)
            {
                var heroRt = (RectTransform)_btnHero.transform;
                var heroLe = heroRt.GetComponent<LayoutElement>() ?? heroRt.gameObject.AddComponent<LayoutElement>();
                heroLe.ignoreLayout = true;
                heroRt.anchorMin = heroRt.anchorMax = new Vector2(0f, 0f);
                heroRt.pivot = new Vector2(0f, 0.5f); // 双行文案变宽——左缘对齐锚点，防中心 pivot 左溢出屏
                heroRt.anchoredPosition = new Vector2(24f, 240f); // 手牌扇左翼上方（与结束回合右翼对称）
                _heroLabel = _btnHero.GetComponentInChildren<TMP_Text>(true);
                heroRt.sizeDelta = new Vector2(160f, 54f); // 临时初值（RefreshHeroZone 按文案实宽覆写）

                // 右键=角色攻击（Button.onClick 只响应左键——右键经 EventTrigger 分流）
                var trigger = _btnHero.gameObject.AddComponent<EventTrigger>();
                var rightClick = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
                rightClick.callback.AddListener(data =>
                {
                    if (((PointerEventData)data).button == PointerEventData.InputButton.Right)
                    {
                        Debug.Log("[UI点击] btn-self-hero(右键=角色攻击)");
                        OnClickSelfHero();
                    }
                });
                trigger.triggers.Add(rightClick);
                _btnHero.gameObject.SetActive(false);             // 仅本地对局显示（OnEnter 开）
            }

            // 临时元素指示条（2026-10-13 区域变化定案：技能并入英雄区腾出的位置，显示本回合获得的
            // 临时元素余额——任一回合结束即逝）。运行时挂载件（同手牌扇口径：prefab 承骨架、动态件挂
            // Root），钱包为空整条隐藏；位置=英雄区正上方（RefreshHeroZone 对齐）。
            var chipGo = new GameObject("temp-elements", typeof(RectTransform), typeof(Image));
            _tempChip = (RectTransform)chipGo.transform;
            _tempChip.SetParent(Root, false);
            _tempChip.anchorMin = _tempChip.anchorMax = _tempChip.pivot = new Vector2(0f, 0f);
            var chipImg = chipGo.GetComponent<Image>();
            chipImg.color = new Color(0.06f, 0.07f, 0.10f, 0.72f); // 半透明底衬（3D 战场上可读）
            chipImg.raycastTarget = false;
            _tempChipLabel = UiKit.Label("lbl", _tempChip, "", UiStyle.SmallSize, UiStyle.TextBody);
            _tempChipLabel.richText = true;                        // 逐色 token（UiKit.Label 默认关富文本）
            var chipLblRt = _tempChipLabel.rectTransform;
            chipLblRt.anchorMin = chipLblRt.anchorMax = new Vector2(0.5f, 0.5f);
            chipLblRt.anchoredPosition = Vector2.zero;
            _tempChip.gameObject.SetActive(false);

            // 悬停信息位（2026-10-13 基础教学定案：toast 节点不要——区域/功能格说明与操作拒绝
            // 原因统一悬停就地展示）：左上 ACard 预览位正下方，与卡面预览互斥显示
            var infoGo = new GameObject("hover-info", typeof(RectTransform));
            _hoverInfo = (RectTransform)infoGo.transform;
            _hoverInfo.SetParent(Root, false);
            _hoverInfo.anchorMin = _hoverInfo.anchorMax = _hoverInfo.pivot = new Vector2(0f, 1f);
            _hoverInfo.anchoredPosition = new Vector2(29f, -402f); // ACard 实例（约 x∈[-931,-675]，顶缘-40）正下方
            _hoverInfo.sizeDelta = new Vector2(256f, 190f);
            _hoverInfoLabel = UiKit.Label("lbl", _hoverInfo, "", UiStyle.SmallSize, UiStyle.TextBody,
                TextAnchor.UpperLeft, wrap: true);
            UiKit.Stretch(_hoverInfoLabel.rectTransform);
            _hoverInfoLabel.richText = true;
            _hoverInfo.gameObject.SetActive(false);

            // 悬浮完整卡面（prefab 内用户摆位的 ACard 实例，默认隐藏）：悬停场上生物/结界/角色格技能点亮
            var previewNode = FindOptional("ACard");
            if (previewNode != null)
            {
                _previewCard = previewNode.gameObject.AddComponent<ACardView>();
                _previewCard.Bind();
            }

            // 手牌扇挂载层代码补建（2026-10-13 基础教学定案：prefab 极简化后按名绑定缺节点；
            // 扇层=纯画布无样式，代码建等价——2026-10-07「缺节点不补建」口径对本对例外）
            EnsureFanLayer("self-fan-layer");
            EnsureFanLayer("opp-fan-layer");

            // 双方手牌扇区（配置承自原型屏：底部己方全正面拱形弧、顶部对手缩小卡背扇）
            var selfLayer = FanLayer("self-fan-layer");
            if (selfLayer != null)
                _selfFan = new HandFanView(selfLayer, new HandFanView.Config
                {
                    Center = new Vector2(0f, -375f), // 卡心离屏底 ~165px（悬停抬起后不越界）
                });
            var oppLayer = FanLayer("opp-fan-layer");
            if (oppLayer != null)
                _oppFan = new HandFanView(oppLayer, new HandFanView.Config
                {
                    TopSide = true,
                    Center = new Vector2(0f, 375f),
                    BaseScale = 0.72f,
                    MaxFanWidth = 830f,
                });

            // 旧版战场 UI 绑定复活（2026-10-13 补回未 3D 化部分——信息条/中栏/战报/overlay 弹层；
            // 重绑冲突已在函数内剔除，board 六行等 3D 化部分缺失=null 静默）
            BindLegacyPrefabNodes();

            // 战斗日志开关（2026-10-13 基础教学：日志改喂 log-list——btn-log 切换高度 0↔700；
            // ClickSwallow 截断面板内点击冒泡，滚日志不误触收起）
            BindBattleButton("btn-log", "对局日志", ToggleLogList);
            _logListRect = FindOptional("log-list");
            if (_logListRect != null) _logListRect.gameObject.AddComponent<ClickSwallow>();

            // Region 区域面板骨架（prefab 用户节点：点棋盘墓地/除外/发动区/卡组/临时元素格打开）
            BuildRegionPanel();

            UiKit.Updater.Attach(Root, () =>
            {
                _selfFan?.Tick();
                _oppFan?.Tick();
                TickLogList();      // 日志面板高度动画
                PollFieldPointer(); // 3D 棋盘悬停/点击（区域格/功能格/单位地牌统一入口——2026-10-13）
            });
        }

        /// <summary>按钮按名绑定（2026-10-07 兜底退役）：预制体节点接线点击（含 [UI点击] 日志）并回写文案
        ///（防烘焙文案漂移）；缺节点/缺 Button 组件 LogError 返回 null——不再代码补建。</summary>
        private Button BindBattleButton(string name, string text, Action onClick)
        {
            var node = Find(name);
            var btn = node != null ? node.GetComponentInChildren<Button>(true) : null;
            if (btn == null)
            {
                if (node != null) Debug.LogError($"[{GetType().Name}] 预制体节点无 Button 组件：{name}");
                return null; // 缺节点已由 Find 另报
            }
            var lbl = btn.GetComponentInChildren<TMP_Text>(true);
            if (lbl != null) lbl.text = text;
            btn.onClick.AddListener(() => { Debug.Log($"[UI点击] {name}"); onClick(); });
            return btn;
        }

        /// <summary>扇区挂载层单名绑定（2026-10-07 兜底退役）：全屏铺开做卡牌画布（烘焙态已定）；
        /// 缺层 LogError 返回 null——对应手牌扇不渲染（Tick/SetCards 已空引用安全）。
        /// 2026-10-13 基础教学：Build 先 EnsureFanLayer 代码补建，本查找恒命中。</summary>
        private RectTransform FanLayer(string name) => Find(name);

        /// <summary>手牌扇挂载层代码补建（全屏纯画布，无样式——prefab 极简版的等价替代）。</summary>
        private void EnsureFanLayer(string name)
        {
            if (UiKit.FindDeep(Root, name) != null) return;
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(Root, false);
            UiKit.Stretch(rt);
        }

        /// <summary>静默版 FindText（可选控件——极简 prefab 缺失不 LogError，null 安全）。</summary>
        private TMP_Text FindTextOptional(string name)
        {
            var rt = FindOptional(name);
            return rt != null ? rt.GetComponentInChildren<TMP_Text>(true) : null;
        }

        /// <summary>旧版战场 UI 深度按名绑定（2026-10-13 复活：prefab 补回未 3D 化控件——用户定案
        /// "只管加，布局自调"）。与现役 Build 共存口径：btn-back/btn-end-turn/btn-activate-skill
        /// 不在此重绑（现役已绑/已并入英雄区，重复 AddListener 会双触发）；board 六行/self-hand/
        /// self-skill 不补（3D/扇面/英雄区已替代，FindOptional 缺失=null 静默）。</summary>
        private void BindLegacyPrefabNodes()
        {
            _overlay = FindOptional("overlay") ?? UiKit.Overlay("overlay", Root);
            _selectorLayer = FindOptional("selector-overlay") ?? UiKit.Overlay("selector-overlay", Root);

            // ---- 工具栏（2026-10-13 基础教学：极简 prefab 大半控件已上盘/退役——全部改静默查找） ----
            _lblMode = FindTextOptional("lbl-mode");
            _lblToast = FindTextOptional("lbl-toast");

            // ---- 对手信息栏（上）----
            _oppName = FindTextOptional("lbl-opp-name");
            _oppLife = FindTextOptional("lbl-opp-life");
            _oppDeck = FindTextOptional("lbl-opp-deck");
            _oppHand = FindTextOptional("lbl-opp-hand");
            _oppGrave = FindTextOptional("lbl-opp-grave");
            _oppFatigue = FindTextOptional("lbl-opp-fatigue");
            (_oppSkillLabel, _oppSkillGroup) = BindSkillChip("opp-skill");
            _oppBank = FindTextOptional("lbl-opp-bank");

            // ---- 中区：回合/阶段/栈/操作 ----
            _lblPhase = FindTextOptional("lbl-phase");
            _lblStack = FindTextOptional("lbl-stack");
            _lblPriority = FindTextOptional("lbl-priority");
            _lblActivation = FindTextOptional("lbl-activation");
            if (UiKit.FindDeep(Root, "btn-grave-play") != null)
                _btnGrave = BindButton("btn-grave-play", OnGraveyardPlay);
            if (UiKit.FindDeep(Root, "btn-pass-priority") != null)
                _btnPass = BindButton("btn-pass-priority", OnPassPriority);
            // btn-end-turn 仅取引用（UpdateButtons 管可用态）——结束回合现役入口=棋盘 hud-tile-end-turn
            var endNode = FindOptional("btn-end-turn");
            _btnEnd = endNode != null ? endNode.GetComponentInChildren<Button>(true) : null;
            _btnConcede = BindButton("btn-concede", OnConcede);

            // ---- 我方信息栏（下）----
            _selfBank = FindTextOptional("lbl-self-bank");
            _selfFatigue = FindTextOptional("lbl-self-fatigue");
            _selfGrave = FindTextOptional("lbl-self-grave");
            _selfDeck = FindTextOptional("lbl-self-deck");
            _selfHandCount = FindTextOptional("lbl-self-hand");
            _selfLife = FindTextOptional("lbl-self-life");
            _selfName = FindTextOptional("lbl-self-name");

            // ---- 战报栏（右）----
            _logList = FindScroll("log-list");
        }

        /// <summary>技能 chip 绑定（文本在子 label；CanvasGroup 缺失则补挂）。</summary>
        private (TMP_Text label, CanvasGroup group) BindSkillChip(string name)
        {
            var rt = Find(name);
            if (rt == null) return (null, null);
            var lbl = rt.GetComponentInChildren<TMP_Text>(true);
            var group = rt.GetComponent<CanvasGroup>();
            if (group == null) group = rt.gameObject.AddComponent<CanvasGroup>();
            return (lbl, group);
        }

        // ======================================== 生命周期 ========================================

        public override void OnEnter()
        {
            _gameEnded = false;
            _netGameOver = false;
            _selectBusy = false;
            _pendingSelects.Clear();
            _netDeckSent = false;
            _netRoomPhase = -1;
            _view = null;
            _running = true;
            _modal = null;

            // 比赛禁联机（NetGate）：网络模式入口已全部屏蔽，此处防御性回落——
            // 万一 Mode 被残留置为 Network（如旧状态/外部注入），也按本地 AI 开局，绝不外连
            if (IsNetwork && !NetGate.OnlineEnabled)
                BattleEntry.Mode = BattleMode.LocalAI;

            if (_lblMode != null)
                _lblMode.text = IsNetwork ? $"网络 · {BattleEntry.Host}:{BattleEntry.Port}"
                    : BattleEntry.Mode == BattleMode.Tutorial ? $"教学 · {BattleEntry.TutorialId}"
                    : BattleEntry.Mode == BattleMode.Challenge ? $"挑战 · 难度{BattleEntry.ChallengeDifficulty}"
                    : "本地 · AI";
            if (_btnPass != null)
                _btnPass.gameObject.SetActive(IsNetwork);

            _btnEndFan?.gameObject.SetActive(!IsNetwork);
            _btnHero?.gameObject.SetActive(!IsNetwork);

            // 战场 3D 摆位 + 装台（2026-10-13 战场重做：本地/网络两路统一——
            // 网络快照经 OnNetSnapshot→_field.Apply 同轨渲染，交互桩维持 NetGate 现状）
            _field = new BattleFieldView();
            // 战斗编舞（2026-10-13）：3D 寻路箭头选攻击目标 + 战斗演出（走位/受击/死亡漩涡吸走）；
            // 事件订阅须先于本屏刷新订阅（宣言即 hold 位姿），OnEnter 建立即满足
            _choreo = new CombatChoreographer(_field, () => _view, RefreshField, listenEvents: !IsNetwork, selfPlayer: () => P1);
            BattleStageDirector.MountBoardAndDawnAsync().Forget(); // 转场③④：装载棋盘（分区表注入）→ 日出
            RefreshFieldWhenBoardReady().Forget();                 // 棋盘就绪后首刷（英雄柱/初始摆位）

            if (IsNetwork)
            {
                Debug.Log("[BattleScreen] 网络空屏桩（对局交互暂缺，战场渲染已接快照）——联机入口按 NetGate 现状禁用");
                return;
            }

            if (_btnEndFan != null) _btnEndFan.interactable = true;
            StartLocalFan();
        }

        public override void OnExit()
        {
            _running = false;
            _net?.Close();
            _net = null;
            CloseActionPopup(); // 3D 点击操作弹层（2026-10-13）
            TutorialFlow.ClearAssessment(); // 考核标记防跨局残留（未终局退出路径）
            TutorialGuide.End();            // 引导闸卸载防跨局残留

            // 手牌扇区实例回收（自持实例池，随挂载层销毁亦安全——显式清防重入）
            _selfFan?.Dispose();
            _oppFan?.Dispose();
            _selfFan = _oppFan = null;

            // 战场 3D 摆位回收（与棋盘拆台 DismountBoard 各自独立清根）；编舞器先于视图销毁
            // （epoch 代际使在途演出自弃，事件退订防跨局残留）
            _choreo?.Dispose();
            _choreo = null;
            _field?.Dispose();
            _field = null;

            // 战场拆台：卸棋盘格子+销毁棋盘根（黑洞/光源/相机由菜单舞台钩子恢复）
            BattleStageDirector.DismountBoard();

            // 本地局棋盘接线归零（静态扩展点不清理会跨界面残留——验证器 S5 锁定此口径）
            _ctrl?.Shutdown();
            _ctrl = null;

            // 仅在仍是本界面注册时解除，避免覆盖其它界面的注册
            if (TargetSelectionService.Current is UiTargetSelector)
                TargetSelectionService.Current = null;
            if (ResponseWindowService.HumanResponder == ShowResponsePopupAsync)
                ResponseWindowService.HumanResponder = null;

            // UGUI 卡牌层回收（卡面独立于界面树池化，切屏必须显式清）
            CardOverlayController.ClearActive();
        }

        private void OnBack() => Manager.Back();

        // ======================================== 本地模式 ========================================

        private void StartLocal()
        {
            _ctrl = new BattleController();
            if (BattleEntry.Mode == BattleMode.Tutorial && !StartTutorialGame(_ctrl))
                _ctrl.StartNewGame(); // 教学配置缺失等异常回落普通局（不炸界面）

            // 引擎事件多但刷新幂等：统一全量重建；战报行同时追加。
            Subscribe<TurnStartEvent>(e => { AppendLocal(e); RefreshLocal(); });
            Subscribe<PhaseStartEvent>(_ => RefreshLocal());
            Subscribe<CardDrawEvent>(e => AppendLocal(e));
            Subscribe<CardPlayEvent>(e => { AppendLocal(e); RefreshLocal(); });
            Subscribe<CardZoneChangeEvent>(_ => RefreshLocal());
            Subscribe<CardPutToBattlefieldEvent>(_ => RefreshLocal());
            Subscribe<CardLeaveBattlefieldEvent>(_ => RefreshLocal());
            Subscribe<LifeChangeEvent>(e => { AppendLocal(e); RefreshLocal(); });
            Subscribe<CombatDamageEvent>(e => AppendLocal(e));
            Subscribe<CardDestroyEvent>(e => { AppendLocal(e); RefreshLocal(); });
            Subscribe<FatigueEvent>(e => AppendLocal(e));
            Subscribe<ElementPoolAddEvent>(_ => RefreshLocal());
            Subscribe<ElementPoolPayEvent>(_ => RefreshLocal());
            Subscribe<StackEmptyEvent>(_ => RefreshLocal());
            Subscribe<GameOverEvent>(OnGameOver);

            // 通用目标选择器（效果引擎结算期反问）
            TargetSelectionService.Current = new UiTargetSelector(_selectorLayer);
            // 响应窗口（发动弹窗）：人类候选→简版选择弹窗
            ResponseWindowService.HumanResponder = ShowResponsePopupAsync;

            RefreshLocal();
        }

        /// <summary>教学固定局开局（2026-10-02）：TutorialConfig 指定双方卡组顺序（=摸牌序列，锁牌库序）
        /// 与机器人逐回合剧本（ScriptedAi 按条演出），rngSeed 钉效果随机；机器人响应窗口全让过
        /// （EnablePassiveAiResponder——永不反制玩家）。返回 false=配置缺失回落普通局。
        /// 预设场面（2026-10-06 教学直入）：scenario 非 null=组卡取双方场面并集、
        /// 不走起手/摸牌序列断言，TutorialScenarioSeeder 直入起跳回合。</summary>
        private bool StartTutorialGame(BattleController ctrl)
        {
            var tutorial = TutorialLibrary.Get(BattleEntry.TutorialId);
            if (tutorial == null)
            {
                ShowToast($"教学配置缺失：{BattleEntry.TutorialId}（回落本地 AI 局）");
                return false;
            }
            if (tutorial.scenario != null)
            {
                var playerCards = TutorialLibrary.BuildDeck(TutorialScenario.DeckIdsOf(tutorial.scenario.player));
                var aiCards = TutorialLibrary.BuildDeck(TutorialScenario.DeckIdsOf(tutorial.scenario.ai));
                ctrl.StartNewGame(playerCards, aiCards, new ScriptedAi(tutorial),
                    rngSeed: tutorial.rngSeed, scenario: tutorial.scenario);
                ctrl.EnablePassiveAiResponder();
                TutorialGuide.Begin(tutorial); // 暂停引导（2026-10-06）：guide 为空时内部不激活
                return true;
            }
            var playerDeck = TutorialLibrary.BuildDeck(tutorial.playerDeck);
            var aiDeck = TutorialLibrary.BuildDeck(tutorial.aiDeck);
            if (playerDeck.Count < GameCore.OpeningHandSize || aiDeck.Count < GameCore.OpeningHandSize)
            {
                ShowToast("教学卡组配置不足（回落本地 AI 局）");
                return false;
            }
            ctrl.StartNewGame(playerDeck, aiDeck, new ScriptedAi(tutorial),
                lockDeckOrder: true, rngSeed: tutorial.rngSeed);
            ctrl.EnablePassiveAiResponder();
            TutorialGuide.Begin(tutorial);
            return true;
        }

        // ======================================== 本地对局（扇形手牌过渡面，2026-10-03 接入） ========================================

        /// <summary>本地对局启动（原型屏 HandFanScreen 移植接入）：开局即建双扇手牌，引擎手牌事件
        /// 幂等对账刷新。战场 3D 重做前的最小对战面——出牌交互暂缺，回合由「结束回合」推进。</summary>
        private void StartLocalFan()
        {
            _ctrl = new BattleController();
            var mirrorDeck = BattleEntry.MirrorDeck; // 一次性消费（防跨局残留，同 Client 口径）
            BattleEntry.MirrorDeck = null;
            var mirrorSkill = BattleEntry.MirrorSkillCardId;
            BattleEntry.MirrorSkillCardId = null;
            var challengeDeck = BattleEntry.ChallengeDeck; // 一次性消费（挑战模式玩家卡组）
            BattleEntry.ChallengeDeck = null;
            var challengeSkill = BattleEntry.ChallengeSkillCardId;
            BattleEntry.ChallengeSkillCardId = null;
            if (mirrorDeck != null)
                // 最后一课/考核镜像局：AI 打同卡组（技能标记双方同用——镜像口径）
                _ctrl.StartNewGame(mirrorDeck, mirrorDeck,
                    mySkillCardId: mirrorSkill, aiSkillCardId: mirrorSkill);
            else if (BattleEntry.Mode == BattleMode.Challenge)
            {
                // 挑战模式（2026-10-06 单值联动）：难度 N = 电脑地牌槽 9+N、起手 6+N；
                // AI 卡组缺省随机 30 张，策略按卡组主色自适配（AutoMatch：标签优先回落费用主色）
                // 技能（2026-10-07 卡牌化）：玩家=卡组标记；AI 随机卡组自动挑一张合格结界
                var difficulty = Mathf.Clamp(BattleEntry.ChallengeDifficulty, 1, ChallengeProgressManager.MaxDifficulty);
                BattleEntry.ChallengeDifficulty = difficulty;
                var aiDeck = BattleController.RandomDeckFrom(CardCatalog.LoadAll());
                _ctrl.StartNewGame(challengeDeck, aiDeck, new SimpleAI(AiStrategy.AutoMatch(aiDeck)),
                    aiLandCapBonus: difficulty, aiExtraOpeningDraws: difficulty,
                    mySkillCardId: challengeSkill,
                    aiSkillCardId: HeroSkillSystem.AutoPickSkillCard(aiDeck));
            }
            else if (BattleEntry.Mode == BattleMode.Tutorial)
            {
                if (!StartTutorialGame(_ctrl))
                    _ctrl.StartNewGame(); // 教学配置缺失回落普通局
            }
            else
                // 普通本地局兜底（2026-10-13 补）：LocalAI 无镜像时原链无分支命中=空战场不 InitGame
                _ctrl.StartNewGame();

            // 引擎事件多但刷新幂等：统一对账重建（口径同原原型屏）；
            // RefreshField（2026-10-13 战场重做）=BattleViewData 构建→BattleFieldView 3D 摆位
            Subscribe<CardEnterHandEvent>(_ => RefreshFans());
            Subscribe<CardPlayEvent>(_ => { RefreshFans(); RefreshField(); });            // 出牌→单位/地牌落格
            Subscribe<CardZoneChangeEvent>(_ => { RefreshFans(); RefreshField(); });
            Subscribe<CardMoveEvent>(_ => { RefreshFans(); RefreshField(); });
            Subscribe<CardDestroyEvent>(_ => { RefreshFans(); RefreshField(); });
            Subscribe<CounterChangedEvent>(_ => { RefreshFans(); RefreshHeroZone(); RefreshField(); }); // 指示物→对手卡翻面/弹药→英雄区/攻生→头顶数值
            Subscribe<RevealCardsEvent>(_ => RefreshFans());
            // 英雄区/临时元素条（2026-10-13 区域变化）：技能横置与钱包跨回合清零（TurnStart）、
            // 角色生命（LifeChange）、临时钱包入账/支付（ElementPool 两事件）；
            // RefreshField 同步：横置恢复/角色生命/地牌横置色
            Subscribe<TurnStartEvent>(_ => { RefreshHeroZone(); RefreshField(); });
            Subscribe<LifeChangeEvent>(_ => { RefreshHeroZone(); RefreshField(); });
            Subscribe<ElementPoolGainEvent>(_ => { RefreshHeroZone(); RefreshField(); });
            Subscribe<ElementPoolPayEvent>(_ => { RefreshHeroZone(); RefreshField(); });
            Subscribe<GameOverEvent>(OnGameOverFan);

            RefreshFans();
            RefreshHeroZone();
            RefreshField(); // 补偿刷（2026-10-13）：订阅注册前已发生的静默变化（教学场景注入零事件）兜底
            Debug.Log("[BattleScreen] 本地对局已启动（扇形手牌过渡面；战场 3D 重做中）");
        }

        /// <summary>结束己方回合并让 AI 跑完对手回合（手牌变化经事件链刷新扇区）。</summary>
        private async void OnEndTurnFan()
        {
            if (_gameEnded || Core == null || _ctrl.TurnPlayer != P1) return;
            GameActions.EndTurn(Core, P1);
            if (!_gameEnded && _ctrl.TurnPlayer == P2)
                await _ctrl.RunAiTurnAsync();
        }

        /// <summary>终局弹窗（扇形过渡面口径；旧链 OnGameOver 的 RefreshLocal 依赖已删预制体字段，不走）。</summary>
        private void OnGameOverFan(GameOverEvent e)
        {
            _gameEnded = true;
            if (_btnEndFan != null) _btnEndFan.interactable = false;
            bool win = e.Winner == P1;
            TutorialFlow.ReportBattleResult(BattleEntry.Mode, win); // 教学逻辑层：考核局/教学局结果落档
            ShowOverlay(win ? "胜利" : "失败", $"{(win ? "我方" : "对手")}获胜（{e.Reason}）。");
            SetCancelText("返回主菜单");
        }

        /// <summary>幂等全量对账：己方手牌全正面；对手手牌默认背面，带 Exposed 指示物的翻正面。</summary>
        private void RefreshFans()
        {
            if (Core?.ZoneManager == null || P1 == null || P2 == null) return;

            var self = new List<HandFanCardData>();
            foreach (var card in Core.ZoneManager.GetCards(P1, Zone.Hand))
            {
                self.Add(new HandFanCardData
                {
                    RuntimeId = card.RuntimeId,
                    Item = CardOverlayItem.FromBattle(BattleView.FromRuntime(
                        SerializableRuntimeCardState.FromCard(card), card, ownerIsOpponent: false),
                        CardOverlayLayout.Full),
                    FaceUp = true,
                });
            }

            var opp = new List<HandFanCardData>();
            foreach (var card in Core.ZoneManager.GetCards(P2, Zone.Hand))
            {
                bool exposed = RevealRules.IsExposed(card); // 信息轴「展示」：对双方公开
                opp.Add(new HandFanCardData
                {
                    RuntimeId = card.RuntimeId,
                    Item = exposed
                        ? CardOverlayItem.FromBattle(BattleView.FromRuntime(
                            SerializableRuntimeCardState.FromCard(card), card, ownerIsOpponent: true),
                            CardOverlayLayout.Full)
                        : BlankBack(card.RuntimeId),
                    FaceUp = exposed,
                });
            }

            _selfFan?.SetCards(self);
            _oppFan?.SetCards(opp);
        }

        /// <summary>背面卡的空内容（Key 仅为口径完整；文字被 back 覆盖不可见）。</summary>
        private static CardOverlayItem BlankBack(uint runtimeId)
            => new CardOverlayItem { Key = "R" + runtimeId };

        // ======================================== 战场 3D 摆位（2026-10-13 战场重做） ========================================

        private int _lastCoreNullLogFrame = -999; // RefreshField 核心未就绪跳过的节流日志帧

        /// <summary>摆位刷新（渲染唯一输入=BattleViewData）：本地=BuildLocal 直连构建；
        /// 网络=_view 已由 OnNetSnapshot 构建（此处只 Apply）。</summary>
        private void RefreshField()
        {
            if (!IsNetwork)
            {
                if (Core == null || P1 == null || P2 == null)
                {
                    if (Time.frameCount - _lastCoreNullLogFrame > 120)
                    {
                        _lastCoreNullLogFrame = Time.frameCount;
                        Debug.Log($"[BattleField] 刷新跳过：对局核心未就绪（core={Core != null}, p1={P1 != null}, p2={P2 != null}）");
                    }
                    return;
                }
                _view = BattleView.BuildLocal(Core, _ctrl.Board, P1, P2);
            }
            _field?.Apply(_view);
            RefreshBars(); // 2D 信息栏（2026-10-13 补回的旧控件）随 3D 对账同频刷新
            RefreshRegion(); // Region 区域面板开着则同频重列（区域变化走本刷新链）
        }

        /// <summary>2D 信息栏刷新（2026-10-13 补回）：双方数据条+中栏文本+操作按钮态。
        /// 控件缺失全静默（?. / null 判空——prefab 未补齐时零炸）。</summary>
        private void RefreshBars()
        {
            var d = _view;
            if (d == null) return;
            FillBar(d.Opp, opp: true);
            FillBar(d.Self, opp: false);
            if (_lblPhase != null) _lblPhase.text = d.PhaseText;
            if (_lblStack != null) _lblStack.text = string.Join(" ｜ ", d.StackLines);
            if (_lblPriority != null)
                _lblPriority.text = d.StackNotEmpty && d.MyPriority ? "◇ 优先权在我（可响应/让行）" : "";
            if (_lblActivation != null)
                _lblActivation.text = d.ActivationTexts.Count > 0 ? string.Join("；", d.ActivationTexts) : "";
            UpdateButtons(d);
        }

        /// <summary>首刷就绪门（2026-10-13 教学局首刷竞速修复）：本地局=棋盘+对局核心齐备——
        /// 首刷曾只等棋盘就绪，会跑赢 StartNewGame（教学场景注入零事件、TurnStart 早于订阅注册），
        /// Core 未就位静默白刷且此后无补偿，预设单位永不渲染；网络局维持只等棋盘（_view 走快照不经 Core）。</summary>
        private bool BoardReadyForFirstPaint()
            => BattleBoardGrid.Ready && (IsNetwork || (Core != null && P1 != null && P2 != null));

        /// <summary>装台完成后首刷：就绪前 Apply 会自跳过，这里只负责"就绪即补一刷"
        /// （空场英雄柱/初始摆位）；之后一切变化走事件订阅路径。</summary>
        private async UniTaskVoid RefreshFieldWhenBoardReady()
        {
            float deadline = Time.realtimeSinceStartup + 45f;
            while (_running && !BoardReadyForFirstPaint() && Time.realtimeSinceStartup < deadline)
                await UniTask.Yield();
            Debug.Log($"[BattleField] 首刷触发（grid={BattleBoardGrid.Ready}, core={Core != null}, running={_running}）");
            try
            {
                RefreshField();
            }
            catch (Exception ex)
            {
                Debug.LogError("[BattleField] 首刷异常：" + ex);
            }
        }

        private void AppendLocal(IGameEvent e)
        {
            var line = BattleEventText.ForLocal(e, p => p == P1);
            AppendLog(line.Text, line.Class);
        }

        private void RefreshLocal()
        {
            if (Core == null || P1 == null || P2 == null) return;
            _view = BattleView.BuildLocal(Core, _ctrl.Board, P1, P2);
            RefreshView();
        }

        private void OnGameOver(GameOverEvent e)
        {
            _gameEnded = true;
            bool win = e.Winner == P1;
            ShowOverlay(win ? "胜利" : "失败", $"{(win ? "我方" : "对手")}获胜（{e.Reason}）。");
            SetCancelText("返回主菜单");
            RefreshLocal();
        }

        // ======================================== 网络模式 ========================================

        private async UniTaskVoid StartNetAsync()
        {
            _ctrl = null;
            bool injected = BattleEntry.Client != null; // 匹配界面接续（阶段三）：已进房+已提交卡组
            _net = BattleEntry.Client ?? new NetGameClient();
            BattleEntry.Client = null; // 一次性消费
            _net.OnRoomState += OnNetRoomState;
            _net.OnManifest += OnNetManifest;
            _net.OnSnapshot += OnNetSnapshot;
            _net.OnEventBatch += OnNetEventBatch;
            _net.OnSelectRequest += OnNetSelectRequest;
            _net.OnError += OnNetError;
            _net.OnDisconnected += () => AppendLog("与服务器的连接已断开", "log-line--combat");

            if (injected)
            {
                _netDeckSent = true; // 卡组已在匹配界面提交
                AppendLog($"接续匹配连接 {_net.Remote}", "log-line--system");
            }
            else
            {
                try
                {
                    _net.Connect(BattleEntry.Host, BattleEntry.Port);
                }
                catch (Exception ex)
                {
                    ShowOverlay("连接失败", $"{BattleEntry.Host}:{BattleEntry.Port} — {ex.Message}");
                    return;
                }

                AppendLog($"已连接 {_net.Remote}", "log-line--system");
                _net.Send(NetworkMessageType.JoinRoom, new MsgJoinRoom
                {
                    RoomId = "",
                    WantSeat = -1, // 任意空位（双客户端先后就座 0/1）
                    Nickname = string.IsNullOrEmpty(BattleEntry.Nickname)
                        ? "player" + UnityEngine.Random.Range(100, 999)
                        : BattleEntry.Nickname,
                });
            }

            // 主循环：每帧泵收发 + 房间状态机（卡组提交）；终局后继续泵收尾战报，退出由用户返回触发
            while (_running && !_net.Disconnected)
            {
                _net.Pump();

                if (_netRoomPhase == (int)NetRoomPhase.DeckSubmit && !_netDeckSent)
                    SendNetDeck();

                await UniTask.Yield();
            }
        }

        private void SendNetDeck()
        {
            _netDeckSent = true;
            // 测试期随机卡组（卡组选定属匹配阶段职责）
            var deckIds = BattleController.RandomDeckFrom(CardCatalog.LoadAll())
                .Select(c => c.ID).ToArray();
            _net.Send(NetworkMessageType.DeckSubmit, new MsgDeckSubmit
            {
                DeckName = BattleEntry.Nickname,
                CardIds = deckIds,
                Digest = NetMatchHandshake.ComputeDeckDigest(deckIds),
            });
            AppendLog($"已提交随机卡组（{deckIds.Length} 张）", "log-line--system");
        }

        private void OnNetRoomState(MsgRoomState room)
        {
            _netRoomPhase = room?.Phase ?? -1;
            if (_netRoomPhase == (int)NetRoomPhase.Playing && !_netGameOver)
                AppendLog("对局进行中", "log-line--system");
            if (_netRoomPhase == (int)NetRoomPhase.Finished)
                AppendLog("房间已结束（可返回）", "log-line--system");
        }

        private void OnNetManifest(MsgMatchManifest manifest)
        {
            if (manifest == null) return;
            AppendLog(manifest.OwnSeat == 0
                ? "对局开始：我方先手"
                : "对局开始：我方后手", "log-line--turn");
        }

        private void OnNetSnapshot(MsgGameStateSync snap)
        {
            _view = BattleView.BuildNet(snap);
            _field?.Apply(_view); // 战场 3D 摆位（2026-10-13：网络快照与本地同轨渲染）
            RefreshView();
        }

        private int _lastViewerSeat;

        private void OnNetEventBatch(NetEvent[] events)
        {
            int viewer = _lastViewerSeat; // viewerSeat 以最新快照为准
            foreach (var e in events)
            {
                var line = BattleEventText.ForNet(e, viewer);
                AppendLog(line.Text, line.Class);
                if (e.EventType == "GameOverEvent")
                {
                    _netGameOver = true;
                    _gameEnded = true;

                    // 终局弹窗（Winner 实体引用按座位判胜负）
                    var winnerParam = e.Params != null
                        ? e.Params.FirstOrDefault(p => p.FieldName == "Winner")
                        : null;
                    var winnerRef = winnerParam != null && winnerParam.EntityRefs != null && winnerParam.EntityRefs.Length > 0
                        ? winnerParam.EntityRefs[0] : null;
                    bool win = winnerRef != null && winnerRef.IsPlayer && winnerRef.Seat == viewer;
                    ShowOverlay(win ? "胜利" : "失败", "对局结束。");
                    SetCancelText("返回主菜单");
                }
            }
        }

        private void OnNetSelectRequest(MsgSelectRequest req)
        {
            _pendingSelects.Enqueue(req);
            DrainNetSelects().Forget();
        }

        /// <summary>反问串行处理（一次一弹窗；候选=实体反问按 CardId 查表名，空候选=纯选项 Labels）。</summary>
        private async UniTaskVoid DrainNetSelects()
        {
            if (_selectBusy) return;
            _selectBusy = true;
            try
            {
                while (_pendingSelects.Count > 0 && _running)
                {
                    var req = _pendingSelects.Dequeue();
                    var labels = req.Candidates != null && req.Candidates.Length > 0
                        ? req.Candidates.Select(r => BattleView.EntityTextOf(r, _lastViewerSeat)).ToList()
                        : (req.Labels ?? Array.Empty<string>()).ToList();
                    int min = Math.Max(1, req.Min);
                    int max = Math.Max(min, req.Max);

                    List<int> picked;
                    if (labels.Count == 0)
                        picked = new List<int>();
                    else
                        picked = await new UiTargetSelector(_selectorLayer).SelectIndicesAsync(
                            labels, min, max,
                            string.IsNullOrEmpty(req.Title) ? "请选择" : req.Title,
                            req.Hint ?? "", req.AllowCancel,
                            req.TimeoutSeconds > 0 ? req.TimeoutSeconds : 60f);

                    if (_net != null)
                        _net.Send(NetworkMessageType.SelectResponse, new MsgSelectResponse
                        {
                            RequestId = req.RequestId,
                            Indices = (picked ?? new List<int>()).ToArray(),
                        });
                }
            }
            finally { _selectBusy = false; }
        }

        private void OnNetError(MsgError err)
        {
            ShowToast($"被拒（{err?.Context}）：{err?.Reason}");
            AppendLog($"操作被拒（{err?.Context}）：{err?.Reason}", "log-line--combat");
        }

        // ======================================== 全量渲染（两种模式统一输入 BattleViewData） ========================================

        private void RefreshView()
        {
            var d = _view;
            if (d == null) return;

            _cardBindings.Clear(); // 卡牌层绑定随全量重建收集（BuildHexRow/BuildHand 填充）

            // 观众席记录（网络事件文本用）
            if (IsNetwork && _view.NetViewerSeat >= 0) _lastViewerSeat = _view.NetViewerSeat;

            // 信息栏
            FillBar(d.Opp, opp: true);
            FillBar(d.Self, opp: false);

            // 战场（行序=棋盘 z 序；对手在上）——缩盘后 z0..z5（休眠链：节点缺失时 null 早退不渲染）
            BuildHexRow(_oppLands, d.OppLands, z: 0, mine: false, isLand: true);
            BuildHexRow(_oppUnitsNear, d.OppUnits, z: 1, mine: false, isLand: false);
            BuildHexRow(_oppUnitsFar, d.OppUnits, z: 2, mine: false, isLand: false);
            BuildHexRow(_selfUnitsFar, d.SelfUnits, z: 3, mine: true, isLand: false);
            BuildHexRow(_selfUnitsNear, d.SelfUnits, z: 4, mine: true, isLand: false);
            BuildHexRow(_selfLands, d.SelfLands, z: 5, mine: true, isLand: true);

            // 中栏（2026-10-13 基础教学：极简 prefab 无中栏节点——null 判空，缺件零炸）
            if (_lblPhase != null) _lblPhase.text = d.PhaseText;
            if (_lblStack != null) _lblStack.text = string.Join(" ｜ ", d.StackLines);
            if (_lblPriority != null)
                _lblPriority.text = d.StackNotEmpty && d.MyPriority ? "◇ 优先权在我（可响应/让行）" : "";
            if (_lblActivation != null)
                _lblActivation.text = d.ActivationTexts.Count > 0 ? string.Join("；", d.ActivationTexts) : "";

            // 我方手牌
            BuildHand(d);

            UpdateButtons(d);

            // 卡牌层挂载：卡实例直接挂进本视图刚建好的槽位
            CardOverlayController.Instance.Bind(_cardBindings);
        }

        private void FillBar(BattlePlayerView p, bool opp)
        {
            string aiTag = p.IsAI ? "（AI）" : "";
            if (opp)
            {
                if (_oppName != null) _oppName.text = $"对手{aiTag}";
                if (_oppLife != null) _oppLife.text = $"生命 {p.Life}/{p.MaxHealth}";
                if (_oppDeck != null) _oppDeck.text = $"牌库 {p.DeckCount}";
                if (_oppHand != null) _oppHand.text = $"手牌 {p.HandCount}";
                if (_oppGrave != null) _oppGrave.text = $"墓地 {p.GraveyardCount}";
                if (_oppFatigue != null) _oppFatigue.text = p.FatigueCount > 0 ? $"疲劳 {p.FatigueCount}" : "";
                if (_oppBank != null) _oppBank.text = $"地牌上限 {p.LandCap} · 临时元素 {p.BankText}";
                if (_oppSkillLabel != null)
                    _oppSkillLabel.text = p.Skill != null
                        ? $"技能：{p.Skill.Name}{(p.Skill.IsTapped ? "（已横置）" : "")} {p.Skill.CostText}"
                        : "技能：—";
                if (_oppSkillGroup != null)
                    _oppSkillGroup.alpha = p.Skill != null && p.Skill.IsTapped ? 0.45f : 1f;
            }
            else
            {
                if (_selfName != null) _selfName.text = "我方";
                if (_selfLife != null) _selfLife.text = $"生命 {p.Life}/{p.MaxHealth}";
                if (_selfDeck != null) _selfDeck.text = $"牌库 {p.DeckCount}";
                if (_selfHandCount != null) _selfHandCount.text = $"手牌 {p.HandCount}";
                if (_selfGrave != null) _selfGrave.text = $"墓地 {p.GraveyardCount}";
                if (_selfFatigue != null) _selfFatigue.text = p.FatigueCount > 0 ? $"疲劳 {p.FatigueCount}" : "";
                if (_selfBank != null) _selfBank.text = $"地牌上限 {p.LandCap} · 临时元素 {p.BankText}";
            }
        }

        /// <summary>一行 7 格（x=1..7）：复用预制体烘焙 cell（按序对应 x，保留用户样式调整）；
        /// 不足补建、多余销毁。空格=底座，有卡则卡实例挂满该格。
        /// 地牌叠层（2026-10-09 地牌槽提升·每格两张）：第二张（Stacked）经内嵌 wrapper 错位小卡
        /// 渲染（CardOverlay 对 Slot stretch 填满——偏移与缩放由 wrapper 承载），两层各可点击产元素。</summary>
        private void BuildHexRow(RectTransform row, List<BattleCardView> cards, int z, bool mine, bool isLand)
        {
            if (row == null) return;
            float w = isLand ? 76f : 96f;
            float h = isLand ? 62f : 74f;
            EnsureChildCount(row, 7, "cell", w, h);

            for (int i = 0; i < row.childCount; i++)
            {
                var cell = (RectTransform)row.GetChild(i);
                ClearChildren(cell); // 清烘焙残留（含上帧 stack wrapper）；卡面实例随后由 CardOverlayController 挂入

                var cellCards = cards.Where(c => c.X == i + 1 && c.Z == z).ToList();
                if (cellCards.Count == 0) continue;

                var primary = cellCards.FirstOrDefault(c => !c.Stacked) ?? cellCards[0];
                var stacked = cellCards.FirstOrDefault(c => c.Stacked && c != primary);

                // 点击语义不变（我方单位=攻击开窗、我方地牌=产元素；对方卡不可点）
                var captured = primary;
                _cardBindings.Add(new CardOverlayBinding
                {
                    Slot = cell,
                    Item = CardOverlayItem.FromBattle(captured,
                        isLand ? CardOverlayLayout.Land : CardOverlayLayout.Compact),
                    Layout = isLand ? CardOverlayLayout.Land : CardOverlayLayout.Compact,
                    OnClick = mine
                        ? (isLand ? (Action)(() => OnClickMyLand(captured)) : () => OnClickMyUnit(captured))
                        : null,
                });

                // 叠层第二张（仅地牌行出现——BoardState 地牌格容量 2）
                if (stacked != null)
                {
                    var wrap = new GameObject("stack", typeof(RectTransform)).GetComponent<RectTransform>();
                    wrap.SetParent(cell, false);
                    wrap.anchorMin = wrap.anchorMax = new Vector2(0.5f, 0.5f);
                    wrap.sizeDelta = new Vector2(w * 0.78f, h * 0.78f);
                    wrap.anchoredPosition = new Vector2(w * 0.18f, h * 0.18f);
                    var s = stacked;
                    _cardBindings.Add(new CardOverlayBinding
                    {
                        Slot = wrap,
                        Item = CardOverlayItem.FromBattle(s, CardOverlayLayout.Land),
                        Layout = CardOverlayLayout.Land,
                        OnClick = mine ? (Action)(() => OnClickMyLand(s)) : null,
                    });
                }
            }
        }

        /// <summary>手牌条：复用预制体烘焙 hand-slot（数量随手牌增删——不足补建、多余销毁）。</summary>
        private void BuildHand(BattleViewData d)
        {
            if (_selfHand == null) return;
            int count = d.SelfHand?.Count ?? 0;
            EnsureChildCount(_selfHand, count, "hand-slot", 132f, 184f);
            int slots = Mathf.Min(count, _selfHand.childCount); // 缺额不补建——按现有槽渲染

            for (int i = 0; i < slots; i++)
            {
                var slot = (RectTransform)_selfHand.GetChild(i);
                ClearChildren(slot);
                var captured = d.SelfHand[i];
                _cardBindings.Add(new CardOverlayBinding
                {
                    Slot = slot,
                    Item = CardOverlayItem.FromBattle(captured, CardOverlayLayout.Full),
                    Layout = CardOverlayLayout.Full,
                    OnClick = () => OnClickHandCard(captured),
                });
            }
        }

        /// <summary>子节点数量对齐（2026-10-07 兜底退役）：多余摘父销毁；不足只 LogError 不补建——
        /// 预制体须烘焙满额（六行 9 cell/手牌条满手牌数），缺额部分本帧不渲染。</summary>
        private static void EnsureChildCount(RectTransform parent, int count, string childName, float w, float h)
        {
            while (parent.childCount > count)
            {
                var last = parent.GetChild(parent.childCount - 1);
                last.SetParent(null);
                UnityEngine.Object.Destroy(last.gameObject);
            }
            if (parent.childCount < count)
                Debug.LogError($"[BattleScreen] 预制体烘焙 {childName} 不足（{parent.childCount}/{count}）——缺额不补建；检查 BattleUI.prefab");
        }

        private void UpdateButtons(BattleViewData d)
        {
            bool myMain = d.MyTurn && d.Phase == PhaseType.Main && !_gameEnded;

            if (_btnGrave != null) _btnGrave.interactable = myMain && d.Self.GraveyardCount > 0;
            // 网络协议无技能 intent 通道（2026-10-07 卡牌化后技能卡已挂真实效果定义，
            // 理论可走 IntentActivateEffect 寻址，但通道/验签未做——本地可用，网络暂禁）
            if (_btnSkill != null) _btnSkill.interactable = !IsNetwork && myMain && !_gameEnded;
            if (_btnHero != null) _btnHero.interactable = myMain && !_gameEnded; // 弹药门禁在点击时 toast（无弹药常态可见）
            if (_btnPass != null) _btnPass.interactable = IsNetwork && d.StackNotEmpty && d.MyPriority && !_gameEnded;
            if (_btnEnd != null) _btnEnd.interactable = d.MyTurn && !_gameEnded;
            if (_btnConcede != null) _btnConcede.interactable = !_gameEnded;
        }

        // ======================================== 操作派发（本地直调 GameActions / 网络转 intent） ========================================

        private bool GateMyMain()
        {
            if (_gameEnded || _view == null) return false;
            if (!_view.MyTurn || _view.Phase != PhaseType.Main)
            {
                // 静默 return 会让点击毫无反馈（联机反馈"使用糊"的主因之一）——给一句可读提示
                ShowToast(!_view.MyTurn ? "对手回合，暂不可操作" : "当前不是主要阶段");
                return false;
            }
            return true;
        }

        /// <summary>点手牌：抉择卡先模式窗；生物弹「打出/放地」二选一；其余直接打出（网络 Targets=null 交服务器反问）。</summary>
        private void OnClickHandCard(BattleCardView card)
        {
            if (!GateMyMain()) return;

            bool isModal = GetModeCount(card) >= 2;
            bool isCreature = IsCreatureCard(card);

            if (isModal)
            {
                PromptModeThenPlay(card, fromGrave: false);
                return;
            }

            if (isCreature)
            {
                ShowChoiceOverlay("使用生物", $"{card.Name}：作为单位打出，或放入元素池当地牌（入池自选锁定产色）？",
                    new List<string> { "作为单位打出", "放入元素池（地牌）" }, idx =>
                    {
                        CloseOverlay();
                        if (idx == 0) PlayCardFinal(card, null, 0, fromGrave: false);
                        else LandFlow(card);
                    });
                return;
            }

            PlayCardFinal(card, null, 0, fromGrave: false);
        }

        private static bool IsCreatureCard(BattleCardView card)
        {
            return DataOf(card)?.Supertype == Cardtype.Creature;
        }

        private static int GetModeCount(BattleCardView card)
        {
            var data = DataOf(card);
            return data == null ? 1 : CostDerivationService.GetModeCount(data);
        }

        /// <summary>卡的静态数据：本地=引擎对象直读；网络=CardId 查本地卡表（引擎对象缺失）。</summary>
        private static CardData DataOf(BattleCardView card)
        {
            if (card.CoreCard != null) return (card.CoreCard as CardWrapper)?.GetData();
            return string.IsNullOrEmpty(card.CardId) ? null : CardCatalog.GetById(card.CardId);
        }

        /// <summary>抉择模式窗（定案时序：先选模式→验费用→目标→出牌）。网络侧费用校验交服务器（Error 反馈）。</summary>
        private void PromptModeThenPlay(BattleCardView card, bool fromGrave)
        {
            var data = DataOf(card);
            var labels = FindChoiceLabels(data);
            int modeCount = Math.Max(2, CostDerivationService.GetModeCount(data));

            var options = new List<string>();
            for (int i = 0; i < modeCount; i++)
            {
                string name = i < labels.Count && !string.IsNullOrEmpty(labels[i]) ? labels[i] : $"模式 {i + 1}";
                // 本地=运行口径费（GetCardCost）；网络=声明费（无引擎对象，服务器校验权威）
                string cost = IsNetwork
                    ? (data != null ? BattleView.CostTextOf(data.Cost) : "?")
                    : CostText(LocalCostOf(card, i));
                options.Add($"{name}（费用 {cost}）");
            }

            ShowChoiceOverlay("选择模式", $"为 {card.Name} 选择要发动的分支：", options, idx =>
            {
                CloseOverlay();

                if (!IsNetwork)
                {
                    var core = Core;
                    // 自动横置口径（2026-09-30）：bank 不足但地牌可产所需元素亦放行——付费步自动横置
                    if (core != null && !core.ElementPool.CanPayCostWithAutoTap(
                            GameActions.GetCardCost(card.CoreCard, idx), P1))
                    {
                        ShowToast("费用不足——该分支不可发动");
                        RefreshLocal();
                        return;
                    }
                }

                PlayCardFinal(card, null, idx, fromGrave);
            });
        }

        private static ElementCost LocalCostOf(BattleCardView card, int modeIndex)
        {
            if (card.CoreCard == null) return null;
            return GameActions.GetCardCost(card.CoreCard, modeIndex);
        }

        private static string CostText(ElementCost cost)
            => cost == null || cost.IsZero ? "0" : BattleView.CostTextOf(cost);

        /// <summary>出牌终态：本地=声明期目标预选+PlayCard+响应窗口泵；网络=intent 上行（Targets=null 服务器反问）。</summary>
        private void PlayCardFinal(BattleCardView card, List<Entity> targets, int modeIndex, bool fromGrave)
        {
            if (IsNetwork)
            {
                _net?.Send(NetworkMessageType.IntentPlayCard, new MsgIntentPlayCard
                {
                    CardRuntimeId = card.RuntimeId,
                    Targets = null, // 声明期目标交引擎反问（SelectRequest 弹窗）——NetClientBrain 同款
                    FromZone = fromGrave ? (int)Zone.Graveyard : (int)Zone.Hand,
                    ModeIndex = modeIndex,
                });
                return;
            }

            var core = Core;
            if (core == null || P1 == null) return;

            // 指向性法术：声明期目标弹窗（本地候选）
            if (targets == null)
            {
                var atomic = FindTargetingAtomic(card.CoreCard, modeIndex);
                if (atomic != null)
                {
                    PromptLocalTargetThenPlay(card, atomic, modeIndex, fromGrave);
                    return;
                }
            }

            bool ok = fromGrave
                ? GameActions.PlayCardFromGraveyard(core, P1, card.CoreCard, targets, modeIndex)
                : GameActions.PlayCard(core, P1, card.CoreCard, targets, Zone.Hand, modeIndex);
            if (ok)
                _ctrl.SettleResponseWindow().Forget();
            else
                ShowToast(fromGrave ? "无法使用（配额已用或不可支付）" : "无法打出（费用/条件不满足）");
            RefreshLocal();
        }

        /// <summary>本地声明期目标弹窗（候选=TargetResolver 引擎筛选）。</summary>
        private void PromptLocalTargetThenPlay(BattleCardView card, AtomicEffectConfig cfg, int modeIndex, bool fromGrave)
        {
            var core = Core;
            var ctx = new EffectExecutionContext
            {
                Source = card.CoreCard,
                Controller = P1,
                ZoneManager = core.ZoneManager,
                ElementPool = core.ElementPool,
            };
            var resolver = new TargetResolver(core.ZoneManager);
            var candidates = resolver.GetCandidates(cfg.GetTargetKindList(), cfg.TargetFilter, ctx);
            if (!string.IsNullOrEmpty(cfg.TargetFilter))
                candidates = resolver.ApplyFilters(candidates, resolver.ParseFilters(cfg.TargetFilter), ctx);

            if (candidates.Count == 0)
            {
                // 无候选：按无目标直接结算（引擎自动解析兜底）
                PlayCardDirect(card, null, modeIndex, fromGrave);
                return;
            }

            var options = new List<Tuple<string, Action>>();
            foreach (var entity in candidates)
            {
                var captured = entity;
                options.Add(Tuple.Create<string, Action>(EntityDisplay(captured), () =>
                {
                    CloseOverlay();
                    PlayCardDirect(card, new List<Entity> { captured }, modeIndex, fromGrave);
                }));
            }
            ShowPickOverlay("选择目标", $"为 {card.Name} 选择目标：", options);
        }

        private void PlayCardDirect(BattleCardView card, List<Entity> targets, int modeIndex, bool fromGrave)
        {
            var core = Core;
            bool ok = fromGrave
                ? GameActions.PlayCardFromGraveyard(core, P1, card.CoreCard, targets, modeIndex)
                : GameActions.PlayCard(core, P1, card.CoreCard, targets, Zone.Hand, modeIndex);
            if (ok)
                _ctrl.SettleResponseWindow().Forget();
            else
                ShowToast("无法打出（费用/条件不满足）");
            RefreshLocal();
        }

        /// <summary>放地入元素池（2026-10-11 锁色定案）：入池时从卡费颜色（红/蓝/绿/灰）自选锁定产色——
        /// 单色卡直接落，多色卡弹选色窗。后续生成固定；付费时系统按账单自动横置（无手动产色）。</summary>
        private void LandFlow(BattleCardView card)
        {
            List<ManaType> colors;
            if (card.CoreCard != null)
                colors = ElementPoolSystem.GetLandColorChoices(card.CoreCard);
            else
            {
                // 网络侧：卡数据直取（CardData 同 IHasCost 接口）
                var data = DataOf(card) as CardCore.IHasCost;
                colors = data != null ? ElementPoolSystem.GetLandColorChoices(data) : new List<ManaType>();
            }
            if (colors.Count == 0)
            {
                ShowToast("该卡无费用颜色可锁定（0费/纯黑白不可作地牌）");
                return;
            }
            if (colors.Count == 1)
            {
                LandFinal(card, colors[0]);
                return;
            }

            var options = new List<Tuple<string, Action>>();
            foreach (var c in colors)
            {
                var captured = c;
                options.Add(Tuple.Create<string, Action>($"锁定 {BattleView.ManaZh(captured)} 产色", () =>
                {
                    CloseOverlay();
                    LandFinal(card, captured);
                }));
            }
            ShowPickOverlay("选择地牌产色", $"{card.Name}：入池后固定产出此色（不可更改）：", options);
        }

        /// <summary>放地终态：本地=AddToElementPool(锁色)；网络=intent 上行（ModeIndex 复用为锁色枚举值）。</summary>
        private void LandFinal(BattleCardView card, ManaType color)
        {
            if (IsNetwork)
            {
                _net?.Send(NetworkMessageType.IntentAddToElementPool, new MsgIntentAddToElementPool
                {
                    CardRuntimeId = card.RuntimeId,
                    ModeIndex = (int)color,
                });
                return;
            }
            bool ok = Core != null && GameActions.AddToElementPool(Core, P1, card.CoreCard, color);
            if (!ok) ShowToast("无法放入元素池（上限/资格）");
            RefreshLocal();
        }

        /// <summary>点我方地牌（2026-10-11 现取现用）：手动横置产元素已退役——地牌由付费步系统
        /// 按账单自动横置（强制系统动作，无速度交互）。点击仅作信息提示。</summary>
        private void OnClickMyLand(BattleCardView card)
        {
            if (!GateMyMain()) return;
            ShowToast(card.IsTapped
                ? $"{card.Name}：已横置（本回合已为支付贡献过元素，回合开始恢复）"
                : $"{card.Name}：付费时自动横置产元素，无需手动操作");
        }

        /// <summary>点我方单位：3D 寻路箭头选攻击目标（2026-10-13 攻击宣言改版）→ 攻击宣言。
        /// 悬停对方单位/角色格实时画寻路路径（BFS 最短路），点目标=路径固定并回传目标；
        /// 确认后下游宣言链路不变（本地 DeclareAttack+结算 / 网络 intent）。
        /// 取代旧 ShowPickOverlay 列表弹窗（overlay 节点缺失时空转的依赖一并解除）。</summary>
        private void OnClickMyUnit(BattleCardView card)
        {
            if (!GateMyMain()) return;
            if (card.IsTapped) { ShowToast("该单位已横置"); return; }
            if (card.Runtime != null && card.Runtime.Power <= 0) { ShowToast("该单位攻击力为 0"); return; }
            if (card.X < 0 || card.Z < 0 || _choreo == null)
            {
                ShowToast("单位位置未知，无法攻击");
                return;
            }

            _choreo.BeginTargeting(card, target =>
            {
                if (IsNetwork)
                {
                    int oppSeat = _view != null && _view.NetViewerSeat >= 0 ? 1 - _view.NetViewerSeat : 1;
                    SendAttackIntent(card, target == null
                        ? new NetEntityRef { Seat = oppSeat, IsPlayer = true }
                        : NetRefOf(target, oppSeat));
                    return;
                }

                Entity captured = target != null && target.CoreCard != null ? (Entity)target.CoreCard : P2;
                if (captured == null || !_ctrl.DeclareAttack(P1, card.CoreCard, captured))
                {
                    ShowToast("无法对该目标攻击");
                    RefreshLocal();
                    return;
                }
                _ctrl.SettleResponseWindow().Forget();
                RefreshLocal();
            });
        }

        /// <summary>点角色攻击（2026-10-10 角色参战遗留收口；2026-10-13 区域变化后入口=英雄区右键）：
        /// 弹药>0 弹目标选择（对手角色+对方单位）→角色攻击宣言——引擎侧 Player 攻击者已全面支持
        /// （CanDeclareAttack 弹药同门、结算后烧光弹药）。目标弹窗依赖 overlay 节点（过渡面暂缺，
        /// ShowPickOverlay 空转）——3D 层重接时随出牌交互一并恢复。
        /// 网络模式 intent 已支持 IsPlayer 攻击者，但网络 UI 是空屏桩——同技能按钮先例 toast 拒绝。</summary>
        private void OnClickSelfHero()
        {
            if (IsTargetSelecting()) return; // 目标选择期不开新选择（两语义裁定·见 OnClickHeroZone）
            if (!GateFanMyMain()) return;
            if (IsNetwork) { ShowToast("网络模式角色攻击暂未开放"); return; }
            var core = Core;
            if (core == null || P1 == null) return;
            int ammo = P1.GetPower(); // Player 分支=HeroAttackCounter 弹药层数钳非负
            if (ammo <= 0) { ShowToast("角色无弹药（攻击力 0）"); return; }

            var targets = new List<Entity> { P2 };
            targets.AddRange(core.ZoneManager.GetCards(P2, Zone.Battlefield).Cast<Entity>());
            var options = new List<Tuple<string, Action>>();
            foreach (var entity in targets)
            {
                var captured = entity;
                options.Add(Tuple.Create<string, Action>(EntityDisplay(captured), () =>
                {
                    CloseOverlay();
                    if (!_ctrl.DeclareAttack(P1, P1, captured))
                    {
                        ShowToast("无法对该目标攻击");
                        RefreshHeroZone();
                        return;
                    }
                    _ctrl.SettleResponseWindow().Forget();
                    RefreshHeroZone();
                }));
            }
            ShowPickOverlay("选择攻击目标", $"用 角色（弹药 {ammo}）攻击：", options);
        }

        // ======================================== 3D 棋盘指针（2026-10-13 悬停/点击统一入口） ========================================

        private RectTransform _actionPopup; // 单位操作按钮组（点空白/操作完成关闭；随屏销毁）

        /// <summary>3D 棋盘指针轮询（UiKit.Updater.Tick）：每帧一次共享射线——
        /// 悬停路由（单位格=完整卡面/特殊格与功能地块=hover-info 文本），左键点击路由
        /// （功能地块/特殊格 Region 面板/角色格技能详情/我方单位操作/地牌信息）。
        /// UI 命中让路且不关弹窗（销毁按钮会吞掉 onClick，原口径）；单位模型 collider 已拆，
        /// 打格反查是既定通路。</summary>
        private void PollFieldPointer()
        {
            if (_choreo != null && _choreo.IsTargeting)
            {
                ClearHover(); // 攻击目标选择中：悬停/点击由编舞器消费（寻路箭头跟随），本屏路由让路
                return;
            }
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                ClearHover();
                return;
            }
            var cam = BattleStageDirector.WorldCam;
            if (cam == null || _view == null)
            {
                ClearHover();
                if (Input.GetMouseButtonDown(0)) { CloseActionPopup(); CloseRegion(); }
                return;
            }
            RaycastHit hit;
            bool hasHit = Physics.Raycast(cam.ScreenPointToRay(Input.mousePosition), out hit, 300f);
            var hitName = hasHit && hit.collider != null ? hit.collider.gameObject.name : "";
            HandleFieldHover(hitName);
            if (Input.GetMouseButtonDown(0)) HandleFieldClick(hitName);
        }

        // ---- 悬停路由 ----

        /// <summary>悬停展示：功能地块/特殊格=信息文本；单位格（生物+结界，双方）与角色格（技能卡）=完整卡面。</summary>
        private void HandleFieldHover(string hitName)
        {
            if (hitName == "hud-tile-end-turn") { SetHoverInfo(EndTurnTileInfo()); return; }
            if (hitName == "hud-tile-back") { SetHoverInfo("返回主菜单\n（离开当前对局回到主菜单）"); return; }
            int hx, hz;
            if (!TryParseTile(hitName, out hx, out hz)) { ClearHover(); return; }

            var role = BoardLayout.RoleOf(hx, hz);
            int ownerIdx = BoardLayout.OwnerOf(hx, hz);
            if (role == CellRole.Character)
            {
                // 角色格：技能卡面即详情（技能是公开信息，双方可看）
                var skill = ownerIdx == 0 ? _view?.Self?.Skill : _view?.Opp?.Skill;
                if (skill != null) ShowPreview(skill);
                else SetHoverInfo((ownerIdx == 0 ? "我方" : "对手") + "角色格（本局无技能卡）");
                return;
            }
            if (role != CellRole.Unit && role != CellRole.Land)
            {
                SetHoverInfo(ZoneCellInfo(role, ownerIdx));
                return;
            }

            var unit = UnitAt(hx, hz);
            if (unit != null && IsPreviewableUnit(unit)) { ShowPreview(unit); return; }
            ClearHover(); // 地牌/空格不出卡面（2026-10-13 用户定案范围=生物+结界）
        }

        private void ClearHover()
        {
            if (_hoverKey == null) return;
            _hoverKey = null;
            if (_previewCard != null && _previewCard.gameObject.activeSelf) _previewCard.gameObject.SetActive(false);
            if (_hoverInfo != null && _hoverInfo.gameObject.activeSelf) _hoverInfo.gameObject.SetActive(false);
        }

        /// <summary>卡面预览（幂等：同卡不重绘；与信息位互斥）。</summary>
        private void ShowPreview(BattleCardView card)
        {
            if (card == null) return;
            string key = "R" + card.RuntimeId;
            if (_hoverKey == key) return;
            _hoverKey = key;
            if (_hoverInfo != null && _hoverInfo.gameObject.activeSelf) _hoverInfo.gameObject.SetActive(false);
            if (_previewCard == null) return;
            _previewCard.Apply(CardOverlayItem.FromBattle(card, CardOverlayLayout.Full), CardOverlayLayout.Full);
            _previewCard.gameObject.SetActive(true);
        }

        /// <summary>悬停信息文本（与卡面预览互斥；文本未变不重写）。</summary>
        private void SetHoverInfo(string text)
        {
            if (string.IsNullOrEmpty(text)) { ClearHover(); return; }
            if (_hoverKey == "info:" + text) return;
            _hoverKey = "info:" + text;
            if (_previewCard != null && _previewCard.gameObject.activeSelf) _previewCard.gameObject.SetActive(false);
            if (_hoverInfo == null) return;
            if (_hoverInfoLabel.text != text) _hoverInfoLabel.text = text;
            if (!_hoverInfo.gameObject.activeSelf) _hoverInfo.gameObject.SetActive(true);
        }

        private static bool TryParseTile(string hitName, out int x, out int z)
        {
            x = z = -1;
            if (string.IsNullOrEmpty(hitName) || !hitName.StartsWith("tile-")) return false;
            var parts = hitName.Split('-');
            return parts.Length == 3 && int.TryParse(parts[1], out x) && int.TryParse(parts[2], out z);
        }

        /// <summary>格位反查战场单位（双方）。</summary>
        private BattleCardView UnitAt(int x, int z)
        {
            return (_view.SelfUnits ?? new List<BattleCardView>()).FirstOrDefault(c => c.X == x && c.Z == z && !c.IsDead)
                   ?? (_view.OppUnits ?? new List<BattleCardView>()).FirstOrDefault(c => c.X == x && c.Z == z && !c.IsDead);
        }

        /// <summary>悬停卡面范围（2026-10-13 用户定案）：生物+结界；查表 miss（无类型数据）保守放行。</summary>
        private static bool IsPreviewableUnit(BattleCardView unit)
        {
            var data = DataOf(unit);
            return data == null || data.Supertype == Cardtype.Creature || data.Supertype == Cardtype.Enchantment;
        }

        // ---- 悬停信息文案 ----

        private string EndTurnTileInfo()
        {
            if (_view == null) return "结束回合";
            string state = _view.MyTurn
                ? (_view.Phase == PhaseType.Main ? "当前可结束——点击推进回合" : $"当前为{BattleView.PhaseZh(_view.Phase)}阶段（主要阶段才可操作）")
                : "对手回合进行中…";
            return "结束回合\n" + _view.PhaseText + "\n" + state;
        }

        /// <summary>特殊格悬停信息（数量+点击引导；临时元素=钱包快照）。</summary>
        private string ZoneCellInfo(CellRole role, int ownerIdx)
        {
            var owner = ownerIdx == 0 ? P1 : P2;
            string who = owner == P1 ? "我方" : "对手";
            var pv = owner == P1 ? _view?.Self : _view?.Opp;
            switch (role)
            {
                case CellRole.Graveyard:
                    return $"{who}墓地 · {pv?.GraveyardCount ?? 0} 张\n点击查看";
                case CellRole.Exile:
                    return $"{who}除外区 · {ZoneCount(owner, Zone.Exile)} 张\n点击查看";
                case CellRole.Deck:
                    return $"{who}卡组 · 剩 {pv?.DeckCount ?? ZoneCount(owner, Zone.Deck)} 张（隐藏）";
                case CellRole.Activation:
                    return $"{who}发动区 · {ZoneCount(owner, Zone.Activation)} 张\n点击查看";
                case CellRole.TempElement:
                    return TempWalletText(owner, pv);
                default:
                    return null;
            }
        }

        private int ZoneCount(Player owner, Zone zone)
            => Core != null ? (Core.ZoneManager.GetCards(owner, zone)?.Count ?? 0) : 0;

        private string TempWalletText(Player owner, BattlePlayerView pv)
        {
            var toks = new List<string>();
            var pool = Core?.ElementPool?.GetPool(owner);
            if (pool != null && pool.TempElements != null)
                foreach (ManaType m in Enum.GetValues(typeof(ManaType)))
                    if (pool.TempElements.TryGetValue(m, out int n) && n > 0)
                        toks.Add($"{BattleView.ManaZh(m)}{n}");
            return $"{(owner == P1 ? "我方" : "对手")}临时元素 · 回合末清空\n"
                   + "本回合余额：" + (toks.Count > 0 ? string.Join(" ", toks) : "空") + "\n"
                   + $"可支配钱包：{pv?.BankText ?? "—"}\n点击查看";
        }

        // ---- 点击路由 ----

        private void HandleFieldClick(string hitName)
        {
            // 功能地块（BattleHudRow：2026-10-13 按钮上盘——返回/结束回合）
            if (hitName == "hud-tile-end-turn" || hitName == "hud-tile-back")
            {
                bool endTurn = hitName == "hud-tile-end-turn";
                Debug.Log("[HUD地块] 点击：" + (endTurn ? "结束回合" : "返回主菜单"));
                CloseActionPopup();
                CloseRegion();
                if (endTurn) OnEndTurnFan();
                else OnBack();
                return;
            }

            int x, z;
            if (!TryParseTile(hitName, out x, out z)) { CloseActionPopup(); CloseRegion(); return; }

            // 特殊格分流（两侧列 x0/x8 不承载单位/地牌——BoardLayout 保证）
            var role = BoardLayout.RoleOf(x, z);
            if (role != CellRole.Unit && role != CellRole.Land && role != CellRole.Edge)
            {
                CloseActionPopup();
                HandleZoneCellClick(role, BoardLayout.OwnerOf(x, z));
                return;
            }

            var unit = (_view.SelfUnits ?? new List<BattleCardView>()).FirstOrDefault(c => c.X == x && c.Z == z && !c.IsDead);
            if (unit != null) { ShowUnitActions(unit, x, z); return; }

            var land = (_view.SelfLands ?? new List<BattleCardView>()).FirstOrDefault(c => c.X == x && c.Z == z);
            if (land != null) { CloseActionPopup(); OnClickMyLand(land); return; }
            CloseActionPopup();
            CloseRegion();
        }

        /// <summary>特殊格点击：墓地/除外/发动区/卡组/临时元素→Region 面板；角色格→技能详情+操作组。</summary>
        private void HandleZoneCellClick(CellRole role, int ownerIdx)
        {
            var owner = ownerIdx == 0 ? P1 : P2;
            if (owner == null) return;
            switch (role)
            {
                case CellRole.Graveyard: OpenRegion(RegionKind.Graveyard, owner); break;
                case CellRole.Exile: OpenRegion(RegionKind.Exile, owner); break;
                case CellRole.Activation: OpenRegion(RegionKind.Activation, owner); break;
                case CellRole.Deck: OpenRegion(RegionKind.Deck, owner); break;
                case CellRole.TempElement: OpenRegion(RegionKind.Temp, owner); break;
                case CellRole.Character: OnClickCharacterCell(owner); break;
            }
        }

        /// <summary>点角色格（2026-10-13 基础教学定案：点击角色显示技能详情——卡面悬停已在展示）：
        /// 我方额外弹 [发动技能/角色攻击]（门禁沿用旧 btn-self-hero 口径；目标选择期不弹）。</summary>
        private void OnClickCharacterCell(Player owner)
        {
            if (IsTargetSelecting()) return; // 选择期点击=目标语义（两语义裁定）
            CloseRegion();
            CloseActionPopup();
            if (owner != P1) { Debug.Log("[角色格] 查看对方技能卡面"); return; }
            if (!GateFanMyMain()) return; // 拒绝原因走悬停信息/日志（无 toast 节点，2026-10-13 定案）
            if (OpenActionPopup(BoardLayout.P1CharX, BoardLayout.P1CharZ) == null) return;
            if (HeroSkillSystem.ResolveSkillCard(Core, P1) != null)
                AddActionButton("发动技能", () => { CloseActionPopup(); OnClickHeroZone(); });
            AddActionButton("角色攻击", () => { CloseActionPopup(); OnClickSelfHero(); });
        }

        /// <summary>单位操作按钮组：可行动判定（攻击=我方主阶段+未横置+攻>0；产元素=带地牌特性+未横置）
        /// → 头顶屏幕投影处弹按钮。不可行动不弹（toast 说明——用户定案"可以行动才弹"）。</summary>
        private void ShowUnitActions(BattleCardView card, int x, int z)
        {
            CloseActionPopup();
            if (IsTargetSelecting()) return; // 选择期点击=目标语义（两语义裁定）
            if (!GateMyMain()) return;       // 非我方主阶段（GateMyMain 自带 toast）

            bool canAttack = !card.IsTapped && (card.Runtime == null || card.Runtime.Power > 0);
            bool canTapElement = !card.IsTapped && !IsNetwork && Core != null && P1 != null
                && card.CoreCard != null && card.CoreCard.IsAlive && card.CoreCard.HasKeyword(KeywordRules.LandTrait);
            if (!canAttack && !canTapElement)
            {
                ShowToast(card.IsTapped ? "该单位已横置" : "该单位无法行动");
                return;
            }

            if (OpenActionPopup(x, z) == null) return;
            if (canAttack) AddActionButton("攻击", () => { CloseActionPopup(); OnClickMyUnit(card); });
            if (canTapElement) AddActionButton("产元素", () => { CloseActionPopup(); TapCreatureElement(card); });
        }

        /// <summary>操作按钮组容器：格心头顶世界坐标 → Canvas 本地投影（单位/角色格共用）。</summary>
        private RectTransform OpenActionPopup(int x, int z)
        {
            CloseActionPopup();
            var popupGo = new GameObject("field-actions", typeof(RectTransform), typeof(VerticalLayoutGroup));
            _actionPopup = (RectTransform)popupGo.transform;
            _actionPopup.SetParent(Root, false);
            _actionPopup.sizeDelta = new Vector2(104f, 40f);
            var le = popupGo.AddComponent<LayoutElement>();
            le.ignoreLayout = true; // 逃逸屏根纵向布局（同 btn-end-turn 口径）
            var vg = popupGo.GetComponent<VerticalLayoutGroup>();
            vg.spacing = 4f;

            // 定位：单位头顶世界坐标 → Canvas 本地（ScreenSpaceOverlay → cam 参 null）
            var cam = BattleStageDirector.WorldCam;
            if (cam == null) return _actionPopup;
            var screen = cam.WorldToScreenPoint(BattleBoardGrid.WorldPositionOf(x, z) + new Vector3(0f, 2.2f, 0f));
            var canvasRt = Root.parent as RectTransform;
            if (canvasRt != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, screen, null, out Vector2 local))
                _actionPopup.localPosition = local;
            return _actionPopup;
        }

        /// <summary>操作按钮（field-actions 子项）。</summary>
        private void AddActionButton(string text, Action onClick)
        {
            if (_actionPopup == null) return;
            var go = new GameObject("act-" + text, typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_actionPopup, false);
            rt.sizeDelta = new Vector2(100f, 30f);
            var img = go.GetComponent<Image>();
            img.color = new Color(0.10f, 0.12f, 0.16f, 0.92f);
            var lbl = UiKit.Label("lbl", rt, text, UiStyle.SmallSize, UiStyle.TextBody);
            lbl.rectTransform.anchorMin = lbl.rectTransform.anchorMax = Vector2.one * 0.5f;
            lbl.rectTransform.anchoredPosition = Vector2.zero;
            go.GetComponent<Button>().onClick.AddListener(() => { Debug.Log("[UI点击] field-action:" + text); onClick(); });
        }

        /// <summary>带地牌特性生物横置产元素（教学闸/资格判定在 GameActions.TapCreatureForElement 引擎内）。</summary>
        private void TapCreatureElement(BattleCardView card)
        {
            if (Core == null || P1 == null || card.CoreCard == null) return;
            if (!GameActions.TapCreatureForElement(Core, P1, card.CoreCard))
            {
                ShowToast("无法产元素（引擎拒绝——详见日志）");
                return;
            }
            RefreshHeroZone(); // 临时钱包条
            RefreshField();
        }

        private void CloseActionPopup()
        {
            if (_actionPopup != null) { UnityEngine.Object.Destroy(_actionPopup.gameObject); _actionPopup = null; }
        }

        // ======================================== Region 区域面板（2026-10-13 基础教学：区域按钮上盘——点特殊格列卡） ========================================

        /// <summary>面板当前展示的区域。</summary>
        private enum RegionKind { None, Graveyard, Exile, Activation, Deck, Temp }

        private RegionKind _regionKind;
        private Player _regionOwner;
        private RectTransform _regionPanel;
        private TMP_Text _regionTitle;
        private UiKit.Scroll _regionGrid;   // 列卡网格（墓地/除外/发动区）
        private RectTransform _regionLines; // 文本行（卡组数量/临时元素钱包）
        private GameObject _regionCardPrefab;

        /// <summary>Region 面板骨架（prefab 用户节点：标题+关闭+卡片网格/文本行两态；默认隐藏）。</summary>
        private void BuildRegionPanel()
        {
            _regionPanel = FindOptional("Region");
            if (_regionPanel == null) return;

            var img = _regionPanel.GetComponent<Image>();
            if (img != null) img.color = new Color(0.07f, 0.08f, 0.12f, 0.92f); // 白底改深衬（文字可读）

            var vg = _regionPanel.GetComponent<VerticalLayoutGroup>()
                     ?? _regionPanel.gameObject.AddComponent<VerticalLayoutGroup>();
            vg.spacing = 8f;
            vg.padding = new RectOffset(12, 12, 10, 10);
            vg.childControlWidth = true;
            vg.childControlHeight = true;
            vg.childForceExpandWidth = false;
            vg.childForceExpandHeight = false;
            vg.childAlignment = TextAnchor.UpperLeft;

            var titleRow = UiKit.Row("title", _regionPanel, spacing: 8f);
            _regionTitle = UiKit.Label("lbl", titleRow, "", UiStyle.BodySize, UiStyle.TextPrimary, TextAnchor.MiddleLeft);
            UiKit.Size(_regionTitle, fw: 1f);
            UiKit.Button("btn-close", titleRow, "关闭", CloseRegion, height: 30f);

            _regionLines = UiKit.Column("lines", _regionPanel, spacing: 6f);
            UiKit.Size(_regionLines, fw: 1f, fh: 1f);
            _regionLines.gameObject.SetActive(false);

            _regionGrid = UiKit.ScrollGrid("cards", _regionPanel, cellW: 232f, cellH: 296f, spacing: 10f);
            UiKit.Size(_regionGrid.Rect.transform, fw: 1f, fh: 1f);

            _regionPanel.gameObject.SetActive(false);
        }

        private void OpenRegion(RegionKind kind, Player owner)
        {
            if (_regionPanel == null) { Debug.Log($"[Region] 面板节点缺失，无法展示 {kind}"); return; }
            _regionKind = kind;
            _regionOwner = owner;
            _regionPanel.gameObject.SetActive(true);
            RefreshRegion();
        }

        private void CloseRegion()
        {
            if (_regionPanel == null || !_regionPanel.gameObject.activeSelf) return;
            _regionPanel.gameObject.SetActive(false);
            _regionKind = RegionKind.None;
            _regionOwner = null;
            Debug.Log("[Region] 关闭");
        }

        /// <summary>面板内容重列（打开时/区域变化刷新链调用；未开着零开销）。</summary>
        private void RefreshRegion()
        {
            if (_regionPanel == null || !_regionPanel.gameObject.activeSelf || _regionOwner == null) return;
            string who = _regionOwner == P1 ? "我方" : "对手";
            bool ownerIsOpp = _regionOwner != P1;

            Zone? zone = _regionKind switch
            {
                RegionKind.Graveyard => Zone.Graveyard,
                RegionKind.Exile => Zone.Exile,
                RegionKind.Activation => Zone.Activation,
                _ => null,
            };
            if (zone.HasValue)
            {
                var cards = Core != null ? Core.ZoneManager.GetCards(_regionOwner, zone.Value) : null;
                string zoneName = _regionKind == RegionKind.Graveyard ? "墓地" : _regionKind == RegionKind.Exile ? "除外区" : "发动区";
                _regionTitle.text = $"{who}{zoneName} · {cards?.Count ?? 0} 张";
                FillRegionCards(cards, ownerIsOpp);
                Debug.Log($"[Region] {who}{zoneName} 列卡 {cards?.Count ?? 0} 张");
                return;
            }
            if (_regionKind == RegionKind.Deck)
            {
                int n = ZoneCount(_regionOwner, Zone.Deck);
                _regionTitle.text = $"{who}卡组";
                FillRegionLines(new List<string> { $"剩余 {n} 张", "（隐藏区域——不展示卡面）" });
                Debug.Log($"[Region] {who}卡组数量行：{n} 张");
                return;
            }
            if (_regionKind == RegionKind.Temp)
            {
                var pv = _regionOwner == P1 ? _view?.Self : _view?.Opp;
                _regionTitle.text = $"{who}临时元素";
                FillRegionLines(TempWalletLines(_regionOwner, pv));
                Debug.Log($"[Region] {who}临时元素钱包行");
            }
        }

        /// <summary>列卡：ACard 实例按次重建（手牌同口径 FromRuntime→FromBattle Full；自持实例不入全局池）。</summary>
        private void FillRegionCards(List<Card> cards, bool ownerIsOpp)
        {
            _regionLines.gameObject.SetActive(false);
            _regionGrid.Rect.gameObject.SetActive(true);
            ClearChildren(_regionGrid.Content);
            if (cards == null || cards.Count == 0) return;
            if (_regionCardPrefab == null)
                _regionCardPrefab = HotUpdateAssets.Load<GameObject>("ACard", UiKit.PrefabPath("ACard"));
            if (_regionCardPrefab == null)
            {
                Debug.LogError("[Region] ACard.prefab 缺失——无法列卡");
                return;
            }
            int i = 0;
            foreach (var c in cards)
            {
                var view = BattleView.FromRuntime(SerializableRuntimeCardState.FromCard(c), c, ownerIsOpp);
                var item = CardOverlayItem.FromBattle(view, CardOverlayLayout.Full);
                var go = UnityEngine.Object.Instantiate(_regionCardPrefab, _regionGrid.Content, false);
                go.name = "region-card-" + i++;
                var cv = go.AddComponent<ACardView>();
                cv.Bind();
                cv.Apply(item, CardOverlayLayout.Full);
            }
        }

        private void FillRegionLines(List<string> lines)
        {
            _regionGrid.Rect.gameObject.SetActive(false);
            _regionLines.gameObject.SetActive(true);
            ClearChildren(_regionLines);
            foreach (var line in lines)
            {
                var lbl = UiKit.Label("line", _regionLines, line, UiStyle.SmallSize, UiStyle.TextBody,
                    TextAnchor.MiddleLeft, wrap: true);
                UiKit.Size(lbl, fw: 1f);
            }
        }

        private List<string> TempWalletLines(Player owner, BattlePlayerView pv)
        {
            var lines = new List<string>();
            var pool = Core?.ElementPool?.GetPool(owner);
            if (pool != null && pool.TempElements != null)
            {
                var toks = new List<string>();
                foreach (ManaType m in Enum.GetValues(typeof(ManaType)))
                    if (pool.TempElements.TryGetValue(m, out int n) && n > 0)
                        toks.Add($"{BattleView.ManaZh(m)}{n}");
                lines.Add("本回合临时余额：" + (toks.Count > 0 ? string.Join(" ", toks) : "空"));
            }
            lines.Add($"可支配钱包：{pv?.BankText ?? "—"}");
            lines.Add($"地牌槽上限：{pv?.LandCap ?? 0}");
            lines.Add("（临时元素任一回合结束即清空）");
            return lines;
        }

        // ======================================== 战报面板开关（2026-10-13 基础教学：btn-log 切换 log-list 高度 0↔700） ========================================

        private const float LogListOpenHeight = 700f;
        private const float LogListSpeed = 3500f; // px/s（700px≈0.2s 展开）

        private void ToggleLogList()
        {
            _logListTarget = _logListTarget > 0f ? 0f : LogListOpenHeight;
            Debug.Log($"[BattleLog] btn-log 切换：目标高度 {_logListTarget:0}（展开={_logListTarget > 0f}）");
        }

        private void TickLogList()
        {
            if (_logListRect == null) return;
            float h = Mathf.MoveTowards(_logListRect.sizeDelta.y, _logListTarget, Time.deltaTime * LogListSpeed);
            if (Mathf.Approximately(h, _logListRect.sizeDelta.y)) return;
            _logListRect.sizeDelta = new Vector2(_logListRect.sizeDelta.x, h);
            if (h >= LogListOpenHeight - 0.5f) _logList?.ScrollToBottom();
        }

        /// <summary>吞掉点击（IPointerClickHandler 实现即截断向父级 Button 冒泡——展开的日志面板内
        /// 点击/拖拽不得触发 btn-log 收起）。</summary>
        private sealed class ClickSwallow : MonoBehaviour, IPointerClickHandler
        {
            public void OnPointerClick(PointerEventData e) { }
        }

        // ======================================== 英雄区（2026-10-13 区域变化：技能与角色同区） ========================================

        /// <summary>是否有进行中的目标选择/弹窗：本屏列表模态在身，或引擎结算期反问在途。
        /// <b>两语义裁定（2026-10-13 用户定案）：目标选择期点击角色=「把角色选为目标」；
        /// 主动阶段（我方主阶段·无待选）点击角色=「发动技能」。二者不得混流——选择期一切
        /// 角色点击先走目标语义，实体点选接线属 3D 层目标选择重做（当前列表模态自带遮罩挡点击）。</summary>
        private bool IsTargetSelecting()
        {
            if (_modal != null) return true;                             // 攻击目标/响应/模式选择弹窗在身
            if (TargetSelectionService.PendingRequests > 0) return true; // 引擎结算期反问在途
            if (_choreo != null && _choreo.IsTargeting) return true;     // 3D 寻路箭头选目标在途（2026-10-13）
            return false;
        }

        /// <summary>点英雄区（左键）：发动英雄技能（2026-10-13 区域变化定案——英雄技能与角色共用本区，
        /// 主动阶段选中英雄即发动技能）；未携带技能的局回落角色攻击。网络模式同旧技能按钮口径 toast 拒绝。
        /// 目标选择期点击不走技能（见 IsTargetSelecting 两语义裁定）。</summary>
        private void OnClickHeroZone()
        {
            if (IsTargetSelecting()) return; // 选择期点击=目标语义，由选择器接管（模态遮罩本就挡住；防御性双保险）
            if (!GateFanMyMain()) return;
            if (IsNetwork) { ShowToast("网络模式英雄技能暂未开放"); return; }
            var skill = HeroSkillSystem.ResolveSkillCard(Core, P1);
            if (skill == null) { OnClickSelfHero(); return; } // 无技能局：本区只承担角色攻击
            ActivateSkillAsync().Forget();
        }

        /// <summary>过渡面主阶段闸（活数据口径）：GateMyMain 依赖 _view（旧渲染链刷新产物，扇形过渡面
        /// 恒 null——纯扇形流程无人调 RefreshLocal，首点必然静默失败）。本地局直接读 TurnEngine 现值。</summary>
        private bool GateFanMyMain()
        {
            if (_gameEnded) return false;
            if (_ctrl == null || Core == null || P1 == null) return false;
            var phase = Core.TurnEngine?.CurrentPhase;
            if (_ctrl.TurnPlayer != P1 || phase == null || phase.Phase != PhaseType.Main)
            {
                ShowToast(_ctrl.TurnPlayer != P1 ? "对手回合，暂不可操作" : "当前不是主要阶段");
                return false;
            }
            return true;
        }

        /// <summary>英雄区+临时元素条刷新（幂等）：角色 生命/弹药、技能 名/派生发动价/横置态；
        /// 临时钱包逐色余额（ElementPool.TempElements——任一回合结束清空，空则整条隐藏）。</summary>
        private void RefreshHeroZone()
        {
            if (_btnHero == null || Core == null || P1 == null) return;

            var skill = HeroSkillSystem.ResolveSkillCard(Core, P1);
            string skillLine;
            if (skill == null)
            {
                skillLine = "技能：—";
            }
            else
            {
                // 发动价=唯一主动效果派生价（同旧技能栏口径，非卡面打出价）
                var fx = HeroSkillSystem.SkillEffectOf(skill);
                string cost = fx != null
                    ? BattleView.CostTextOf(CostDerivationService.DeriveElementCosts(fx)) : "0";
                bool tapped = SerializableRuntimeCardState.FromCard(skill).IsTapped;
                skillLine = $"技能：{CardDisplayName(skill)} {cost}{(tapped ? "（已横置）" : "")}";
            }

            var heroRt = (RectTransform)_btnHero.transform;
            if (_heroLabel != null)
            {
                _heroLabel.text = $"角色 生命{P1.Life}/{P1.MaxHealth} · 弹药{Math.Max(0, P1.GetPower())}\n{skillLine}";
                heroRt.sizeDelta = new Vector2(_heroLabel.preferredWidth + 28f, _heroLabel.preferredHeight + 14f);
            }

            if (_tempChip != null && _tempChipLabel != null)
            {
                var tokens = new List<string>();
                foreach (ManaType m in Enum.GetValues(typeof(ManaType)))
                {
                    int n = Core.ElementPool.GetAvailableManaCount(m, P1);
                    if (n > 0)
                        tokens.Add($"<color=#{ColorUtility.ToHtmlStringRGB(TempTokenColor(m))}>"
                                   + $"{BattleView.ManaZh(m)}{n}</color>");
                }
                bool any = tokens.Count > 0;
                _tempChip.gameObject.SetActive(any);
                if (!any) return;

                _tempChipLabel.text = $"<color=#B4BAC6>临时</color> {string.Join(" ", tokens)}";
                float w = _tempChipLabel.preferredWidth + 16f;
                float h = _tempChipLabel.preferredHeight + 8f;
                _tempChipLabel.rectTransform.sizeDelta = new Vector2(w - 10f, h - 4f);
                _tempChip.sizeDelta = new Vector2(w, h);
                // 英雄区正上方（英雄钮 pivot(0,0.5)：顶边 = pos.y + 高/2）
                _tempChip.anchoredPosition = new Vector2(24f, heroRt.anchoredPosition.y + heroRt.sizeDelta.y * 0.5f + 6f);
            }
        }

        /// <summary>临时元素 token 色（黑在深色底衬上不可辨——提亮为浅灰蓝近似；余同费用方格点色）。</summary>
        private static Color TempTokenColor(ManaType m)
        {
            switch (m)
            {
                case ManaType.Red: return UiStyle.DotRed;
                case ManaType.Blue: return UiStyle.DotBlue;
                case ManaType.Green: return UiStyle.DotGreen;
                case ManaType.White: return UiStyle.DotWhite;
                case ManaType.Black: return new Color(0.62f, 0.65f, 0.72f);
                default: return UiStyle.DotGray; // 灰
            }
        }

        private int MyNetSeat => _view != null && _view.NetViewerSeat >= 0 ? _view.NetViewerSeat : 0;

        private static NetEntityRef NetRefOf(BattleCardView card, int seat) => new NetEntityRef
        {
            RuntimeId = card.RuntimeId,
            Seat = seat,
            IsPlayer = false,
            CardId = card.CardId,
        };

        private void SendAttackIntent(BattleCardView attacker, NetEntityRef target)
        {
            _net?.Send(NetworkMessageType.IntentDeclareAttack, new MsgIntentDeclareAttack
            {
                Attacker = NetRefOf(attacker, MyNetSeat),
                Target = target,
            });
        }

        // ---- 中栏按钮 ----
        // （跳过准备阶段按钮已删：准备阶段纯自动推进——TurnEngine.StartNewTurn 内
        //   AdvanceFromStandby 直调，2026-09-24 定案；UI 不再有停等入口。）

        private void OnGraveyardPlay()
        {
            if (!GateMyMain()) return;

            if (IsNetwork)
            {
                // 网络侧：弹本地墓地候选（快照 ZoneCards 公开区）→ IntentPlayCard FromZone=Graveyard
                AppendLog("网络模式：点击手牌区操作，墓地使用=从墓地点选（快照公开区）", "log-line--system");
                ShowToast("网络模式墓地使用：待快照墓地明细面板（后续迭代）");
                return;
            }

            var grave = Core.ZoneManager.GetCards(P1, Zone.Graveyard);
            if (grave == null || grave.Count == 0) { ShowToast("墓地为空"); return; }

            var options = new List<Tuple<string, Action>>();
            foreach (var c in grave)
            {
                var captured = c;
                var view = new BattleCardView
                {
                    CardId = c.ID,
                    CoreCard = c,
                    Runtime = SerializableRuntimeCardState.FromCard(c),
                    Name = CardDisplayName(c),
                };
                var data = (c as CardWrapper)?.GetData();
                if (data != null)
                {
                    view.CostText = BattleView.CostTextOf(data.Cost);
                    view.SupertypeText = BattleView.SupertypeZh(data.Supertype);
                }
                options.Add(Tuple.Create<string, Action>(
                    $"{view.Name}（费 {view.CostText}）", () =>
                    {
                        CloseOverlay();
                        if (GetModeCount(view) >= 2) PromptModeThenPlay(view, fromGrave: true);
                        else PlayCardFinal(view, null, 0, fromGrave: true);
                    }));
            }
            ShowPickOverlay("墓地使用", "选一张牌，视为手牌中使用（每回合一次）：", options);
        }

        private void OnActivateSkill()
        {
            if (!GateMyMain()) return;
            if (IsNetwork)
            {
                ShowToast("网络模式技能发动：协议 intent 通道待增补");
                return;
            }
            ActivateSkillAsync().Forget();
        }

        private async UniTaskVoid ActivateSkillAsync()
        {
            bool ok = await GameActions.ActivateHeroSkill(Core, P1);
            if (!ok) ShowToast("无法发动技能（费用/横置/阶段）");
            else _ctrl.SettleResponseWindow().Forget(); // 无候选自动双 Pass，无害
            RefreshHeroZone(); // 扇形过渡面不走走 RefreshLocal（旧渲染链字段已不绑，会空引用）
        }

        private void OnPassPriority()
        {
            if (IsNetwork && _view != null && _view.StackNotEmpty && _view.MyPriority && !_gameEnded)
                _net?.Send<object>(NetworkMessageType.IntentPassPriority, null);
        }

        private async void OnEndTurn()
        {
            if (_gameEnded || _view == null || !_view.MyTurn) return;

            if (IsNetwork)
            {
                _net?.Send<object>(NetworkMessageType.IntentEndTurn, null);
                return;
            }

            GameActions.EndTurn(Core, P1);
            // 交给 AI 跑完 P2 的整个回合（战斗窗口处可暂停等人类响应弹窗），再统一刷新
            if (!_gameEnded && _ctrl.TurnPlayer == P2)
                await _ctrl.RunAiTurnAsync();
            RefreshLocal();
        }

        private void OnConcede()
        {
            if (_gameEnded) return;
            ShowChoiceOverlay("投降确认", "确定投降吗？本局将判负。", new List<string> { "确认投降", "取消" }, idx =>
            {
                CloseOverlay();
                if (idx != 0) return;
                if (IsNetwork)
                    _net?.Send<object>(NetworkMessageType.IntentConcede, null);
                else
                {
                    Core.EndGame(P2, GameOverReason.Concede);
                    RefreshLocal();
                }
            });
        }

        // ======================================== 本地响应窗口（发动弹窗·沿用旧语义） ========================================

        /// <summary>人类响应弹窗：候选列表 + 「跳过」行；返回所选或 null=Pass。</summary>
        private UniTask<ResponseOption> ShowResponsePopupAsync(Player holder, List<ResponseOption> options)
        {
            var tcs = new UniTaskCompletionSource<ResponseOption>();
            var labels = new List<string>(options.Select(o => o.Label)) { "跳过（让过优先权）" };
            ShowChoiceOverlay("响应窗口", $"{holder?.Name}：发动一个响应，或跳过", labels, idx =>
            {
                CloseOverlay();
                tcs.TrySetResult(idx >= options.Count ? null : options[idx]);
            });
            return tcs.Task;
        }

        // ======================================== 弹窗通用 ========================================

        private void ShowPickOverlay(string title, string hint, List<Tuple<string, Action>> options)
        {
            if (_overlay == null) return; // 预制体缺 overlay（Build 已报）——弹窗跳过（2026-10-07 兜底退役）
            _modal?.Close();
            _modal = UiKit.ModalBox(_overlay, title, width: 460f, height: 460f);
            UiKit.Label("hint", _modal.Panel, hint, UiStyle.SmallSize, UiStyle.TextDim, wrap: true);
            var list = UiKit.ScrollColumn("list", _modal.Panel, spacing: 4f);
            UiKit.Size(list.Rect.transform, fw: 1f, fh: 1f);
            foreach (var opt in options)
            {
                var captured = opt;
                AddOverlayRow(list.Content, opt.Item1, () => { captured.Item2(); });
            }
            AddCancelButton();
        }

        private void ShowChoiceOverlay(string title, string hint, List<string> options, Action<int> onPick)
        {
            if (_overlay == null) return; // 预制体缺 overlay（Build 已报）——弹窗跳过（2026-10-07 兜底退役）
            _modal?.Close();
            _modal = UiKit.ModalBox(_overlay, title, width: 460f, height: 460f);
            UiKit.Label("hint", _modal.Panel, hint, UiStyle.SmallSize, UiStyle.TextDim, wrap: true);
            var list = UiKit.ScrollColumn("list", _modal.Panel, spacing: 4f);
            UiKit.Size(list.Rect.transform, fw: 1f, fh: 1f);
            for (int i = 0; i < options.Count; i++)
            {
                var idx = i;
                AddOverlayRow(list.Content, options[idx], () => onPick(idx));
            }
            AddCancelButton();
        }

        private void ShowOverlay(string title, string hint)
        {
            if (_overlay == null) return; // 预制体缺 overlay（Build 已报）——弹窗跳过（2026-10-07 兜底退役）
            _modal?.Close();
            _modal = UiKit.ModalBox(_overlay, title, width: 460f, height: 260f);
            UiKit.Label("hint", _modal.Panel, hint, UiStyle.SmallSize, UiStyle.TextDim, wrap: true);
            AddCancelButton();
        }

        private void AddCancelButton()
        {
            var bar = UiKit.Row("bar", _modal.Panel, spacing: 8f);
            var spacer = UiKit.Label("spacer", bar, "");
            UiKit.Size(spacer, fw: 1f);
            _overlayCancel = UiKit.Button("overlay-cancel", bar, "取消", OnOverlayCancel, height: 34f);
        }

        private void SetCancelText(string text)
        {
            if (_overlayCancel != null)
            {
                var lbl = _overlayCancel.GetComponentInChildren<TMP_Text>();
                if (lbl != null) lbl.text = text;
                var le = _overlayCancel.GetComponent<LayoutElement>();
                if (le != null) le.preferredWidth = lbl.preferredWidth + 28f;
            }
        }

        private void AddOverlayRow(RectTransform content, string text, Action onClick)
        {
            var row = UiKit.Node("row", content);
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
            btn.onClick.AddListener(() => onClick());
            UiKit.Size(row, h: 38f);
        }

        private void CloseOverlay()
        {
            _modal?.Close();
            _modal = null;
            _overlayCancel = null;
        }

        private void OnOverlayCancel()
        {
            if (_gameEnded) Manager.Back();
            else CloseOverlay();
        }

        private string EntityDisplay(Entity e)
        {
            if (e is Player p)
            {
                string who = p == P1 ? "我方" : "对手";
                return $"{who}角色（生命 {p.Life}）";
            }
            if (e is Card c) return $"{CardDisplayName(c)} [{c.GetPower()}/{c.GetLife()}]";
            return e.ToString();
        }

        private static string CardDisplayName(Card c)
        {
            if (c is CardWrapper w)
            {
                var d = w.GetData();
                return string.IsNullOrEmpty(d?.CardName) ? d?.ID ?? "?" : d.CardName;
            }
            return c != null ? c.ToString() : "?";
        }

        private void ShowToast(string message)
        {
            if (_lblToast != null) _lblToast.text = message;
            else Debug.Log($"[BattleScreen] {message}"); // 过渡面无 toast 控件（教学配置缺失等回落提示走日志）
        }

        // ======================================== 战报 ========================================

        private void AppendLog(string text, string cls)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (_logList?.Content == null) return;

            var (color, style) = cls switch
            {
                "log-line--turn" => (UiStyle.LogTurn, FontStyle.Bold),
                "log-line--combat" => (UiStyle.LogCombat, FontStyle.Normal),
                "log-line--system" => (UiStyle.LogSystem, FontStyle.Normal),
                _ => (UiStyle.LogBase, FontStyle.Normal),
            };
            var line = UiKit.Label("line", _logList.Content, text, UiStyle.MiniSize, color, style: style, wrap: true);
            UiKit.Size(line, fw: 1f);

            while (_logList.Content.childCount > 300)
            {
                var first = _logList.Content.GetChild(0);
                first.SetParent(null);
                UnityEngine.Object.Destroy(first.gameObject);
            }
            _logList.ScrollToBottom();
        }

        // ======================================== 声明期目标原子（本地；沿用旧语义） ========================================

        /// <summary>返回该卡第一个需玩家选择目标的原子配置；无则 null。</summary>
        private static AtomicEffectConfig FindTargetingAtomic(Card card, int modeIndex = 0)
        {
            if (!(card is CardWrapper wrapper)) return null;
            var defs = CardEffectConverter.ConvertAll(wrapper.GetData().Effects, wrapper.GetData().ID);
            foreach (var def in defs)
            {
                if (def.IsActivatedEffect) continue; // 施放即结算的才会自动跑

                if (def.Steps != null && def.Steps.Count > 0)
                {
                    foreach (var atomic in CardEffectConverter.EnumerateMainSequenceAtoms(def.Steps, modeIndex))
                    {
                        var cfg = AtomicEffectTable.GetByType(atomic.Type);
                        if (cfg != null && cfg.GetTargetKindList().Count > 0)
                            return cfg;
                    }
                    continue;
                }

                foreach (var atomic in def.Effects)
                {
                    var cfg = AtomicEffectTable.GetByType(atomic.Type);
                    if (cfg != null && cfg.GetTargetKindList().Count > 0)
                        return cfg;
                }
            }
            return null;
        }

        /// <summary>抉择模式显示名（第一个 Choice 步骤的 label 列表；无则空表）。</summary>
        private static List<string> FindChoiceLabels(CardData data)
        {
            var labels = new List<string>();
            if (data?.Effects == null) return labels;
            foreach (var eff in data.Effects)
            {
                if (eff?.Steps == null) continue;
                foreach (var step in eff.Steps)
                {
                    if (step?.choices == null || step.choices.Count < 2) continue;
                    foreach (var c in step.choices)
                        labels.Add(c?.label);
                    return labels;
                }
            }
            return labels;
        }

        /// <summary>清空容器（先摘父再 Destroy，防 Destroy 延迟导致的同帧占位）。</summary>
        private static void ClearChildren(RectTransform container)
        {
            for (int i = container.childCount - 1; i >= 0; i--)
            {
                var child = container.GetChild(i);
                child.SetParent(null);
                UnityEngine.Object.Destroy(child.gameObject);
            }
        }
    }
}
