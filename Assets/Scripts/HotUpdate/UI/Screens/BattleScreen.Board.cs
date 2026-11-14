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
    public sealed partial class BattleScreen : UIScreen
    {
        // ======================================== 守卫窗口（2026-10-14：对手攻击时点守卫生物出箭头拦截） ========================================



        /// <summary>点【攻击】键/角色攻击（2026-10-16 与生物同款箭头选目标）：箭头自角色格拉出，
        /// 悬停对方单位/角色格（目标=角色回传 null→P2），点击确认→角色攻击宣言——引擎侧 Player
        /// 攻击者已全面支持（CanDeclareAttack 弹药同门、结算后烧光弹药）。
        /// 伪视图钉角色格、RuntimeId 保持 0（=编舞器 IdOf 的角色哨兵，与 AI 角色攻击宣言同口径）。</summary>
        private void OnClickSelfHero()
        {
            if (IsTargetSelecting()) return; // 目标选择期不开新选择（两语义裁定·见 OnClickHeroZone）
            if (!GateFanMyMain()) return;
            var core = Core;
            if (core == null || P1 == null) return;
            int ammo = P1.GetPower(); // Player 分支=HeroAttackCounter 弹药层数钳非负
            if (ammo <= 0) { _toast.Show("角色无弹药（攻击力 0）"); return; }
            if (_choreo == null) { _toast.Show("战场编舞未就绪，无法发起角色攻击"); return; }

            var cell = BoardLayout.CharacterCell(0);
            var heroView = new BattleCardView { Name = "角色", X = cell.x, Z = cell.z };
            _choreo.BeginTargeting(heroView, target =>
            {
                Entity captured = target != null && target.CoreCard != null ? (Entity)target.CoreCard : P2;
                string heroAttackReason = null; // 预置 null：captured 短路时 out 不赋值（CS0165）
                if (captured == null || !_ctrl.DeclareAttack(P1, P1, captured, out heroAttackReason))
                {
                    // 引擎真因（2026-11-03 失败提示细化）：潜行/隐密等目标侧原因对角色攻击同样生效
                    _toast.Show(heroAttackReason ?? "无法对该目标攻击");
                    RefreshField();
                    return;
                }
                _ctrl.SettleResponseWindow().Forget();
                RefreshField();
            });
        }

        // ======================================== 3D 棋盘指针（2026-10-13 悬停/点击统一入口） ========================================



        /// <summary>3D 棋盘指针轮询（UiKit.Updater.Tick）：每帧一次共享射线——
        /// 悬停路由（单位格=四段卡信息/特殊格与功能地块=hover-info 文本），左键点击路由
        /// （功能地块/特殊格 Region 面板/角色格角色与技能详情/地牌信息）。
        /// UI 命中让路且不收钮（销毁按钮会吞掉 onClick，原口径）；单位模型 collider 已拆，
        /// 打格反查是既定通路。2026-10-26 头顶 3D 钮定案：点有可行动作的己方单位/
        /// 角色格在头顶弹 BattleUnitActionButtons（位图=ReconcileTileButtons；钮名 unit-act-* 前置路由）。
        /// 2026-10-18 安卓滑动输入：鼠标/触摸统一走 BattlePointer（触摸点击=抬起裁决、
        /// 落点=手指原始点位；PC 口径零变化）。</summary>
        private void PollFieldPointer()
        {
            BattlePointer.Sample(); // 鼠标/触摸统一采样（同帧幂等）——本屏与选择循环共源
            _decision?.PollProbe(); // 取证心跳（2026-10-17 口径，2026-10-18 B3 收口进决策中枢；快照供数在装配处）

            // 牌库预览条让路（2026-10-26 定案：真实的目标选择触发时自动关闭渲染目标选择）：
            // 列表型（Region 选择）+棋盘型（守卫窗/选格/攻击选目标）任一在身即收
            if (_deckPreviewOwner >= 0 && ((_regionSelector != null && _regionSelector.IsOpen)
                || (_cellPicker != null && _cellPicker.Active)
                || (_choreo != null && _choreo.IsTargeting)
                || (_decision != null && _decision.GuardWindowOpen)))
                CloseDeckPreview();

            // Information 让路（2026-11-03 需求4）：3D 接管态（守卫窗/选格/攻击箭头）Information
            // 关闭让位；Region 选择在身不关——候选条收起（btn-hide）时允许查阅（需求6，与预览条口径相反）
            if (_infoCtl != null && _infoCtl.IsOpen && ((_cellPicker != null && _cellPicker.Active)
                || (_choreo != null && _choreo.IsTargeting)
                || (_decision != null && _decision.GuardWindowOpen)))
                CloseInformation();

            if (_opening != null && _opening.Active)
            {
                // 五段式开场闸（2026-10-19 掷币演出挪战场）：段一~四无 Core，游戏点击分支全拦——
                // 指针交开场导演（deckpick- 选卡组分发/其余点击=演出段快进），悬停层清空防残文案。
                _hoverProv.Clear();
                _opening.PollPointer();
                return;
            }

            ReconcileTileButtons(); // 可用动作位图每帧对账先行（3D 动作钮触发/内容/关闭单源；让路路径同样收钮）
            if (_choreo != null && _choreo.IsTargeting)
            {
                _hoverProv.Clear(); // 攻击目标选择中：悬停/点击由编舞器消费（寻路箭头跟随），本屏路由让路
                return;
            }
            if (_cellPicker != null && _cellPicker.Active)
            {
                _hoverProv.Clear(); // 放地/出牌选格中：点击由选格器消费（箭头跟随+候选格高亮），本屏路由让路
                return;
            }
            if (BattlePointer.OverUI) // UI 让路口径统一（2026-10-22 B2）：无参 IsPointerOverGameObject
                // 触摸下基本恒 false（不采手指位），与选择循环（BattlePointer）口径分裂——曾致触摸经
                // UGUI 区漏判为棋盘点击；OverUI 在本方法开头 Sample() 已刷新（鼠标/触摸同源）。
            {
                _hoverProv.Clear();
                return;
            }
            var cam = BattleStageDirector.WorldCam;
            if (cam == null || _view == null)
            {
                _hoverProv.Clear();
                if (BattlePointer.ConfirmPressed) CloseRegion();
                return;
            }
            RaycastHit hit;
            // 裁决点（触摸=手指原始点位；确认帧=确认落点）——悬停与点击同射线
            var aim = BattlePointer.ConfirmPressed ? BattlePointer.ConfirmPoint : BattlePointer.AimPoint;
            bool hasHit = Physics.Raycast(cam.ScreenPointToRay(aim), out hit, 300f);
            var hitCollider = hasHit ? hit.collider : null;
            var hitName = hitCollider != null ? hitCollider.gameObject.name : "";
            // 守卫窗口：点守卫者/角色/攻击者全归决策中枢——可守卫的己方格先弹头顶 3D 动作钮（守卫钮=选守卫者，
            // v4 §10.2 两段式），攻击者/无动作格原口直进（悬停帧 btnKind 空照常路由）。
            // 按钮组开着时其他 3D 点击（点攻击者确认等）同口径先收钮（2026-10-24 关闭钮退役定案随迁）
            if (_decision != null && _decision.GuardWindowOpen)
            {
                if (BattlePointer.ConfirmPressed && TryUnitActionButton(hitName)) return; // 3D 动作钮前置路由（先于格点击）
                if (BattlePointer.ConfirmPressed && TryOpenUnitActions(hitName)) return;
                if (BattlePointer.ConfirmPressed) _unitButtons.Close();
                _decision.HandlePointer(hitName, cam);
                return;
            }
            _hoverProv.Handle(hitName);
            if (BattlePointer.ConfirmPressed) HandleFieldClick(hitName);
        }

        // ---- 悬停路由 ----

        // ---- 悬停信息文案 ----

        /// <summary>阶段显示格文字（【己方主要阶段】式——BattleHudRow.SetPhase 喂文，8 字 2.6 号贴格宽）。</summary>
        private static string PhaseTileText(BattleViewData d)
            => PhaseTileText(d.Phase, d.MyTurn);

        /// <summary>阶段显示格文字（相位+行方直组——2026-10-26 事件序喂数：OnPhaseAnnounced 用事件载荷组字，
        /// 与对账版同源单口）。</summary>
        private static string PhaseTileText(PhaseType phase, bool myTurn)
            => $"【{(myTurn ? "己方" : "对手")}{BattleView.PhaseZh(phase)}阶段】";

        // 相位播报/牌库标记/赋予粒子四钩已随二期 A 数据化退役（2026-11-11）：投影器只构造
        // BattleFxOp（PhaseAnnounce/DeckMark/KeywordBurst/CounterBurst）经 LocalFilmSource.Present
        // 路由——轮内挂帧随播/轮外即时执行；执行侧=BattleFxOpExecutor（PlaybackFx ref 口同实现）。

        /// <summary>角色【发动】键高亮门禁（2026-10-16 三键改版）：有技能卡且挂主动效果；
        /// 深判定（费用/每回合一次/阶段）留引擎在发动时裁决，拒绝走点击 toast。</summary>
        private bool HeroHasActivatableSkill()
            => HeroSkillSystem.SkillEffectsOf(HeroSkillSystem.ResolveSkillCard(Core, P1)).Count > 0;

        // ---- 悬停族转发桩（2026-10-18 拆分——单源在 BattleHoverInfoProvider，屏内调用点零改动） ----
        private static bool TryParseTile(string hitName, out int x, out int z)
            => BattleHoverInfoProvider.TryParseTile(hitName, out x, out z);

        private int ZoneCount(Player owner, Zone zone)
            => BattleHoverInfoProvider.ZoneCountOf(Core, owner, zone);

        private List<Card> ExposedDeckCards(Player owner)
            => BattleHoverInfoProvider.ExposedDeckCardsOf(Core, owner);

        // ---- 点击路由 ----

        /// <summary>左键点击路由（2026-10-26 定案：格前/弹窗两代入口退役——点有可行动作的己方单位/
        /// 角色格在头顶弹 3D 动作按钮组 BattleUnitActionButtons，动作处理器经 OnUnitActionButton
        /// 沿用既有入口；其余落点=收钮后走原信息展示语义）。功能地块/特殊格/地牌/施放图标路由不变。</summary>
        private void HandleFieldClick(string hitName)
        {
            CloseDeckPreview(); // 旧牌库预览条随 2026-11-03 牌组进 Information 退役——留收口兜底（恒空转）

            // 3D 动作钮命中前置（2026-10-26）：执行该动作即收钮——先于「关钮」防点击被吞
            if (TryUnitActionButton(hitName)) return;
            // 点单位弹可用动作（§10.2 语义随迁）：命中格在位图中且有任一可用项=开钮组（换目标即重开）；
            // 其余任何落点（含点空白/无动作格/无命中）=收钮
            if (TryOpenUnitActions(hitName)) return;
            _unitButtons.Close();

            // 功能地块（BattleHudRow 中线状态行：结束回合格可点；两时间/连锁速度格纯展示——
            // 未认领名走 TryParseTile 失败分支收弹窗，无需另接点击）
                if (hitName == "hud-tile-end-turn")
                {
                    Debug.Log("[HUD地块] 点击：结束回合");
                    // 按压反馈同棋盘格（2026-11-12）：改名认领格走 PlayHud 直喂 Transform——
                    // 纯视觉先于各闸（对手回合被拦也照压=点击有响应），不改路由不进结算链
                    BattleTilePress.PlayHud(BattleHudRow.EndTurnTile);
                    CloseRegion();
                    if (IsTargetSelecting()) return; // Region 抉择/反问在身：先完成选择再推进（非模态须自防重入）
                    if (NetActive) { OnEndTurnFan(); return; } // 网络局：视图闸在 NetEndTurn（Core 侧门不适用）
                    // 仅己方回合可点（2026-10-14 定案）：对手回合就地提示，不再静默吞（引擎闸仍在 OnEndTurnFan 兜底）
                if (Core == null || _ctrl == null || _ctrl.TurnPlayer != P1)
                {
                    _toast.Show("对手回合进行中，暂不可结束回合");
                    return;
                }
                OnEndTurnFan();
                return;
            }

            // 施放占格类型图标（2026-10-15 发动格拆除）：点击=该方发动区列卡（Region 面板——反制指向/查看入口，
            // 原发动格点击入口随格拆除迁移至此）
            var casting = _hoverProv.CastingCardAt(hitName);
            if (casting != null)
            {
                OpenRegion(RegionKind.Activation, BoardLayout.OwnerOf(casting.X, casting.Z) == 0 ? P1 : P2);
                return;
            }

            int x, z;
            if (!TryParseTile(hitName, out x, out z)) { CloseRegion(); return; }

            // 格子按压反馈（2026-11-11 木桩测试需求）：命中任意格（单位/地牌/特殊/角色/空格）
            // 整格按下-回弹、挂件随格同动——纯视觉 fire-and-forget，在 role 分发前触发，
            // 不改任何路由分支、不进结算链
            BattleTilePress.Play(_field, x, z);

            // 特殊格分流（两侧列 x0/x7 不承载单位/地牌——BoardLayout 保证）
            var role = BoardLayout.RoleOf(x, z);
            if (role != CellRole.Unit && role != CellRole.Land && role != CellRole.Edge)
            {
                HandleZoneCellClick(role, BoardLayout.OwnerOf(x, z));
                return;
            }

            // 单位格体（有动作的已在方法头弹窗返回；此处=无动作单位）=展示信息（悬停已出四段卡信息）
            var unit = (_view.SelfUnits ?? new List<BattleCardView>()).FirstOrDefault(c => c.X == x && c.Z == z && !c.IsDead);
            if (unit != null) { CloseRegion(); return; }

            // 地牌格（2026-11-03 Information 改版）：双方地牌格点击列出该格地牌（原=纯日志取证、
            // 对方地牌格无响应）；己方保留旧逐色日志。无地牌的空格照旧收面板。
            int landOwnerIdx = BoardLayout.OwnerOf(x, z);
            var landList = landOwnerIdx == 0 ? _view.SelfLands : _view.OppLands;
            var land = (landList ?? new List<BattleCardView>()).FirstOrDefault(c => c.X == x && c.Z == z);
            if (land != null)
            {
                if (landOwnerIdx == 0) OnClickMyLand(land); // 旧取证日志保留
                OpenInformationLands(landOwnerIdx, x, z);
                return;
            }
            CloseRegion();
        }

        /// <summary>特殊格点击：墓地/除外/发动区/卡组/额外区→Region 面板；角色格→技能详情+操作组。
        /// 2026-10-17 点击门槛（用户定案）：墓地/除外须有卡才开列表面（无卡=点击无反馈，信息走悬停）；
        /// 卡组是隐藏区，只有存在被展示的卡（RevealRules 信息轴）才开——列表只列被展示的卡。
        /// 额外区（2026-10-22 衍生物模板区·原技能格位）：有模板卡才开（同墓地/除外门槛）。
        /// 卡组有诅咒/祝福标记卡时优先开标记预览条（2026-10-26——复用目标选中栏，OpenDeckPreview）。</summary>
        private void HandleZoneCellClick(CellRole role, int ownerIdx)
        {
            var owner = ownerIdx == 0 ? P1 : P2;
            if (owner == null) return;
            switch (role)
            {
                case CellRole.Graveyard:
                    if (ZoneCount(owner, Zone.Graveyard) > 0) OpenInformation(Zone.Graveyard, owner); // 2026-11-03：改 Information 窗（原 Region 浏览）
                    break;
                case CellRole.Exile:
                    if (ZoneCount(owner, Zone.Exile) > 0) OpenInformation(Zone.Exile, owner);
                    break;
                // CellRole.Activation 已随发动格拆除（2026-10-15）退役：Region 发动区入口迁至
                // 点击施放类型图标（cast-{rid} 路由——见 HandleFieldClick）
                case CellRole.Deck:
                    // 2026-11-03 定案：牌组统一进 Information（全卡背+被展示卡正面含状态）
                    //——旧标记卡预览条（诅咒/祝福列表）与被展示卡浏览随之退役
                    if (ZoneCount(owner, Zone.Deck) > 0) OpenInformation(Zone.Deck, owner);
                    break;
                // CellRole.Skill 已随技能格撤销（2026-10-16）退役：技能发动入口=角色格【发动】键
                //（HandleFieldClick btnKind 路由——OnClickHeroZone 链路），介绍=角色格悬停。
                // CellRole.Extra 已随额外区格整删（2026-10-29 P11）：额外区无盘面入口
                //（Zone.Extra 本体保留——衍生物模板区，P9b 域）；万能格 Any 只做展示无点击。
                case CellRole.Character: OnClickCharacterCell(owner); break;
            }
        }

        /// <summary>点角色格格体（2026-10-16 三键改版定案）：格体=展示信息语义（与单位格同款——
        /// 悬停出角色+技能介绍），攻击/守卫/发动技能拆到格前三键；旧【发动技能/角色攻击】
        /// 操作弹窗退役（OpenActionPopup/AddActionButton 一并拆除）。</summary>
        private void OnClickCharacterCell(Player owner)
        {
            CloseRegion();
            Debug.Log(owner == P1
                ? "[角色格] 我方角色格——操作走格前三键（悬停可看角色与技能介绍）"
                : "[角色格] 查看对方角色（悬停出信息）");
        }

        /// <summary>格前【攻击】键入口（2026-10-15 三键改版；原「点单位格直达」链路原样沿用）：
        /// 主阶段直达 3D 箭头选目标（横置/攻 0 门禁在 OnClickMyUnit 内 toast）；
        /// 结界等非生物单位无攻击宣言。教学期门禁照旧。</summary>
        private void ShowUnitActions(BattleCardView card, int x, int z)
        {
            if (IsTargetSelecting()) return; // 选择期点击=目标语义（两语义裁定）
            if (!GateMyMain()) return;       // 非我方主阶段（GateMyMain 自带 toast）
            // 教学期前置提示（引擎闸仍兜底——闸一直都在，这里把拒绝原因提前到点击时可见）
            var step = TutorialGuide.Active ? TutorialGuide.Current : null;
            if (step != null)
            {
                // attacker 空=宽松步（2026-10-17 教学两段式：任意己方生物可攻击——与引擎闸 GateAttack 同口径）
                if (step.type == "attack" && !string.IsNullOrEmpty(step.attacker) && step.attacker != card.CardId)
                {
                    _toast.Show($"引导要求用 {TutorialGuide.NameOf(step.attacker)} 攻击（{card.Name} 暂不可操作）");
                    return;
                }
                if (step.type == "guard") { _toast.Show(step.hint ?? "生物保留待守卫"); return; }
                if (step.type == "end") { _toast.Show(step.hint ?? "引导要求结束回合"); return; }
            }
            if (!IsCreatureCard(card)) { _toast.Show("该单位无法攻击"); return; }
            Debug.Log($"[BattleScreen] 攻击键点击：{card.Name}（{x},{z}）→ 攻击目标选择");
            OnClickMyUnit(card);
        }

        // ---- 单位可用动作位图 + 头顶 3D 动作按钮组（2026-10-26：格前/弹窗两代入口退役，位图单源喂按钮组） ----

        private struct CellActions { public int X, Z; public bool Attack, Guard, Activate; }

        private readonly List<CellActions> _cellActions = new List<CellActions>();
        private readonly BattleUnitActionButtons _unitButtons = new BattleUnitActionButtons();
        private bool _actBtnMyTurn;         // 开按钮组时的回合/阶段印章（变化=关，§10.2 关闭条件随迁）
        private PhaseType _actBtnPhase;
        private int _actBtnTurnNo;

        // 三键效果缓存（2026-10-18 性能）：UnitActivatedEffects/HeroHasActivatableSkill 每次跑完整效果
        // 定义转换（ConvertAll+LINQ），改事件级重算、帧间复用；SelfUnits 每次 RefreshField 重建，
        // 引用比对兜底 + RefreshField 尾置脏（StackEmpty 订阅兜住变形/临摹等改写效果的结算路径）
        private readonly Dictionary<BattleCardView, bool> _unitActivateFlags = new Dictionary<BattleCardView, bool>();
        private readonly Dictionary<BattleCardView, bool> _unitPendingFlags = new Dictionary<BattleCardView, bool>();
        private bool _activateFlagsDirty = true;
        private List<BattleCardView> _activateFlagsFor;
        private bool _heroActivateFlag;

        /// <summary>可用动作位图每帧对账（2026-10-26 头顶 3D 钮改版：位图落 _cellActions 单源——
        /// 点单位弹按钮组的触发/内容/关闭都吃这里）：编舞选目标/放格选格/
        /// 网络局/反问在身=清空+收钮；守卫窗口=守卫候选亮（窗口独占）；常态=己方单位格——
        /// 攻击=生物未横置攻&gt;0 且我方主阶段、发动=有主动效果且我方主阶段、守卫=仅窗口内。
        /// 角色格：攻击=有弹药且我方主阶段、发动=技能挂主动效果、守卫=窗口内且角色在候选。
        /// 深判定（费用/横置代价/耐久/每回合一次）留引擎裁决，位图不预判，拒绝走点击 toast。</summary>
        private void ReconcileTileButtons()
        {
            if (_view == null || !BattleBoardGrid.Ready
                || (_choreo != null && _choreo.IsTargeting)
                || (_cellPicker != null && _cellPicker.Active)
                || NetActive || IsTargetSelecting())
            {
                _cellActions.Clear();
                _unitButtons.Close();
                return;
            }

            bool myMain = !_gameEnded && _view.MyTurn && _view.Phase == PhaseType.Main && !IsTargetSelecting();
            var units = _view.SelfUnits;
            if (!ReferenceEquals(_activateFlagsFor, units) || _activateFlagsDirty)
            {
                _activateFlagsFor = units;
                _activateFlagsDirty = false;
                _unitActivateFlags.Clear();
                _unitPendingFlags.Clear();
                int withPending = 0; // TEMP 诊断（定位后删）
                if (units != null)
                {
                    foreach (var c in units)
                    {
                        if (c == null || c.CoreCard == null) continue;
                        // 发动键点亮两源分记（2026-10-20 一步点击修订）：自身主动效果（发动即横置=
                        // 每回合一发闸——点亮须未横置，镜像攻击键，横置判定在对账现场取活值）**或
                        // 待发主动奖励**（自由分支 tactivate=2 条件达成入队——入队随引擎事件、发动
                        // 无横置闸，点亮不吃横置）。深判定（速度门）留引擎，亮了被拒走 toast
                        bool hasPending = PendingRewardsOf(c.CoreCard).Count > 0;
                        if (hasPending) withPending++;
                        _unitActivateFlags[c] = UnitActivatedEffects(c).Count > 0;
                        _unitPendingFlags[c] = hasPending;
                    }
                }
                Debug.Log($"[BR-Flags] 三键旗重算：单位数={units?.Count ?? 0} 含待发奖励={withPending} myTurn={_view?.MyTurn} phase={_view?.Phase}"); // TEMP 诊断（定位后删）
                _heroActivateFlag = P1 != null && HeroHasActivatableSkill();
            }
            _cellActions.Clear();
            if (units != null)
            {
                foreach (var c in units)
                {
                    if (c == null || c.IsDead || c.CoreCard == null || c.X < 0 || c.Z < 0) continue;
                    var e = new CellActions { X = c.X, Z = c.Z };
                    if (_decision != null && _decision.GuardWindowOpen)
                        e.Guard = _decision.GuardCandidates != null
                                  && _decision.GuardCandidates.Exists(o => o != null && ReferenceEquals(o.SourceCard, c.CoreCard));
                    else
                    {
                        e.Attack = myMain && IsCreatureCard(c) && !c.IsTapped
                                   && (c.Runtime == null || c.Runtime.Power > 0);
                        // 发动键：主动效果须未横置（引擎 ActivateEffect 横置闸的前置显示——一步点击
                        // 定案后发动即横置，亮死键=点两次案残留）；待发奖励不受横置影响
                        e.Activate = myMain && ((_unitActivateFlags.TryGetValue(c, out var hasDef) && hasDef && !c.IsTapped)
                                                || (_unitPendingFlags.TryGetValue(c, out var hasPend) && hasPend));
                    }
                    _cellActions.Add(e);
                }
            }

            // 角色格位图（2026-10-16）：Attack=弹药>0、Activate=技能挂主动效果、
            // Guard=守卫窗口内且角色在候选（ReferenceEquals 匹配 SourcePlayer——引擎侧弹药门同源）
            if (P1 != null)
            {
                var hc = BoardLayout.CharacterCell(0);
                var hero = new CellActions { X = hc.x, Z = hc.z };
                if (_decision != null && _decision.GuardWindowOpen)
                    hero.Guard = _decision.GuardCandidates != null
                                 && _decision.GuardCandidates.Exists(o => o != null && ReferenceEquals(o.SourcePlayer, P1));
                else
                {
                    hero.Attack = myMain && P1.GetPower() > 0;
                    hero.Activate = myMain && _heroActivateFlag;
                }
                _cellActions.Add(hero);
            }

            // 按钮组随行对账（§10.2 关闭条件随迁）：阶段或回合变化（印章对拍）——选目标/网络局在方法头已收
            if (_unitButtons.IsOpen
                && (_view.MyTurn != _actBtnMyTurn || _view.Phase != _actBtnPhase || _view.TurnNumber != _actBtnTurnNo))
                _unitButtons.Close();
            // 绑定格位图对账（2026-10-26 新增）：仍在位图=刷新可用项（守卫窗翻转/横置降档自适应）；
            // 失位（单位死亡/离场/格位变动）=收钮——位图单源裁决，钮组不自持可用性
            if (_unitButtons.IsOpen)
            {
                bool found = false, a = false, g = false, c = false;
                for (int i = 0; i < _cellActions.Count; i++)
                    if (_cellActions[i].X == _unitButtons.BoundX && _cellActions[i].Z == _unitButtons.BoundZ)
                    {
                        found = true;
                        a = _cellActions[i].Attack; g = _cellActions[i].Guard; c = _cellActions[i].Activate;
                        break;
                    }
                if (found) _unitButtons.SetBits(a, g, c);
                else _unitButtons.Close();
            }
        }

        /// <summary>§10.2（随迁 2026-10-26）：命中的格有可用动作（位图单源=_cellActions）→ 头顶弹
        /// 3D 动作按钮组。网络/无动作=false（调用方走原路由=收钮）。守卫窗口内同样先弹（守卫钮→PickGuarderAt）。</summary>
        private bool TryOpenUnitActions(string hitName)
        {
            if (NetActive) return false; // 网络局不弹（§10.2 关闭条件）
            int x, z;
            if (!TryParseTile(hitName, out x, out z)) return false;
            for (int i = 0; i < _cellActions.Count; i++)
            {
                if (_cellActions[i].X != x || _cellActions[i].Z != z) continue;
                if (!_cellActions[i].Attack && !_cellActions[i].Guard && !_cellActions[i].Activate) return false;
                OpenUnitActions3D(x, z, _cellActions[i]);
                return true;
            }
            return false;
        }

        /// <summary>3D 动作钮点击路由（2026-10-26）：命中名=本钮（unit-act-*）且钮组在开=执行动作
        /// （Fire 先关再回调）；其余=false（调用方走原格点击路由）。</summary>
        private bool TryUnitActionButton(string hitName)
        {
            if (!_unitButtons.IsOpen || !BattleUnitActionButtons.IsActionButton(hitName)) return false;
            _unitButtons.Fire(hitName);
            return true;
        }

        /// <summary>开/换钮组：锚=单位头顶位（2026-10-26 迁格后 BattleFieldView 头顶锚=模型头顶高
        /// +0.25——按钮组落旧读数正上方；角色=柱顶读数位；未建件回落格上 2.0），动作为零分配转发到既有入口。</summary>
        private void OpenUnitActions3D(int x, int z, CellActions e)
        {
            bool isHero = x == BoardLayout.P1CharX && z == BoardLayout.P1CharZ;
            BattleCardView unit = null;
            if (!isHero)
                unit = (_view.SelfUnits ?? new List<BattleCardView>())
                    .FirstOrDefault(c => c != null && c.X == x && c.Z == z && !c.IsDead);
            var statsAnchor = _field != null
                ? (isHero ? _field.HeroStatsAnchor(0)
                          : (unit != null ? _field.StatsAnchorOf(unit.RuntimeId) : null))
                : null;
            var anchor = statsAnchor ?? (BattleBoardGrid.WorldPositionOf(x, z) + Vector3.up * 2.0f);
            _actBtnMyTurn = _view.MyTurn; // 关闭印章（ReconcileTileButtons 对拍）
            _actBtnPhase = _view.Phase;
            _actBtnTurnNo = _view.TurnNumber;
            _unitButtons.Show(anchor, x, z, e.Attack, e.Guard, e.Activate,
                              kind => OnUnitActionButton(kind, isHero, unit, x, z));
        }

        /// <summary>钮组动作分流（§10.2：处理器全沿用三键/弹窗时代的现有入口）：
        /// 攻击=单位 ShowUnitActions/角色 OnClickSelfHero；发动=单位主动效果/角色技能；
        /// 守卫=守卫窗口专属（PickGuarderAt=原点格选守卫者同口）。</summary>
        private void OnUnitActionButton(BattleUnitActionButtons.Kind kind, bool isHero, BattleCardView unit, int x, int z)
        {
            if (kind == BattleUnitActionButtons.Kind.Guard)
            {
                if (_decision != null && _decision.GuardWindowOpen) _decision.PickGuarderAt(x, z);
                else _toast.Show("守卫在对手攻击宣言后的守卫窗口内可用");
                return;
            }
            CloseRegion();
            if (isHero)
            {
                if (kind == BattleUnitActionButtons.Kind.Attack) OnClickSelfHero();
                else OnClickHeroZone();
                return;
            }
            if (unit == null) return; // 单位已离场（对账竞态）
            if (kind == BattleUnitActionButtons.Kind.Attack) ShowUnitActions(unit, x, z);
            else OnClickUnitEffectButton(unit).Forget();
        }

        /// <summary>单位可发动效果清单（三键高亮/点击单源）：引擎效果定义中时机=主动两档
        ///（Activate_Active/Activate_Instant——响应式 Activate_Response 归响应窗口不在此口）。
        /// CoreCard 缺（网络模式/异常数据）=空。</summary>
        private static List<EffectDefinition> UnitActivatedEffects(BattleCardView card)
        {
            if (card?.CoreCard == null) return new List<EffectDefinition>();
            // 结界禁主动（2026-10-15 改版）：来源闸镜像——防御旧数据/运行时改写，不亮死按钮
            if (CardCore.Attribute.CounterRules.UsesDurabilityEconomy(card.CoreCard))
                return new List<EffectDefinition>();
            return GameActions.GetCardEffectDefinitions(card.CoreCard)
                .Where(d => d != null
                    && (d.TriggerTiming == TriggerTiming.Activate_Active
                        || d.TriggerTiming == TriggerTiming.Activate_Instant))
                .ToList();
        }

        /// <summary>格前【发动效果】键点击（2026-10-15 三键改版：单位由参数直给，原头顶按钮路由退役）：
        /// 待发优先（2026-10-20 主动运势两段式：第一次点击=掷骰入待发，第二次点击=发动待发）——
        /// 有待发奖励时不再与主动效果混选：单个直达发动、多个只在待发里择一；无待发才走主动效果
        /// （掷骰后生物已横置，本回合再掷被 ActivateEffect 横置闸拒，(本回合0/1) 闭环）。
        /// 目标不在此弹——引擎结算期反问经 TargetSelectionService（Current=RegionTargetSelector）
        /// 弹 Region 选卡面板。门禁时点/优先权/费用/横置/耐久全在 GameActions.ActivateEffect 裁决，
        /// 拒绝 toast。</summary>
        private async UniTaskVoid OnClickUnitEffectButton(BattleCardView card)
        {
            if (card?.CoreCard == null) return;
            if (IsTargetSelecting()) return; // 选择期点击=目标语义（两语义裁定）
            if (!GateMyMain()) return;
            var defs = UnitActivatedEffects(card);
            var pendings = PendingRewardsOf(card.CoreCard); // 待发主动奖励（自由分支·2026-10-20）
            if (pendings.Count > 0)
            {
                if (pendings.Count == 1)
                {
                    ActivatePendingReward(pendings[0]);
                    return;
                }
                var popts = new List<string>(pendings.Count);
                foreach (var pe in pendings)
                    popts.Add((string.IsNullOrEmpty(pe.Effect?.DisplayName) ? "待发奖励" : pe.Effect.DisplayName)
                              + "（奖励待发）");
                int? pp = await PickRegionOptionAsync("选择待发奖励", popts);
                if (pp == null || pp.Value < 0 || pp.Value >= pendings.Count) return;
                ActivatePendingReward(pendings[pp.Value]);
                return;
            }
            if (defs.Count == 0) return;
            if (defs.Count == 1)
            {
                await ActivateUnitEffectAsync(card, defs[0]);
                return;
            }

            var options = new List<string>(defs.Count);
            for (int i = 0; i < defs.Count; i++)
                options.Add(string.IsNullOrEmpty(defs[i].DisplayName) ? $"主动效果 {i + 1}" : defs[i].DisplayName);
            int? picked = await PickRegionOptionAsync("选择效果", options);
            if (picked == null) return;
            if (picked.Value < 0 || picked.Value >= defs.Count) return;
            await ActivateUnitEffectAsync(card, defs[picked.Value]);
        }

        /// <summary>该卡的待发主动奖励（BRANCH_REWARD·自由分支 tactivate=2 条件达成入队）：
        /// 发动键点亮与点击分流共用。速度门深判定留引擎（PlayerActivateVoluntary 裁决）。</summary>
        private List<PendingEffect> PendingRewardsOf(Card card)
        {
            var list = new List<PendingEffect>();
            if (card == null || Core?.StackEngine == null || P1 == null)
            {
                Debug.Log($"[BR-Pending] 查询跳过：card={(card as CardCore.IHasName)?.CardName} coreNull={Core?.StackEngine == null} p1Null={P1 == null}"); // TEMP 诊断（定位后删）
                return list;
            }
            var activatable = Core.StackEngine.GetActivatableVoluntaryEffects(P1);
            foreach (var pe in activatable)
                if (pe != null && ReferenceEquals(pe.Source, card)) list.Add(pe);
            // TEMP 诊断日志（2026-10-20 倒计时按钮不亮排查——定位后删）：只在有队列项或匹配时刷
            if (activatable.Count > 0 || list.Count > 0)
                Debug.Log($"[BR-Pending] 卡={(card as CardCore.IHasName)?.CardName} 队列可发动={activatable.Count}"
                    + $"（各源：{string.Join("|", activatable.Where(pe => pe != null).Select(pe => (pe.Source as CardCore.IHasName)?.CardName ?? pe.Source?.ToString()))}）"
                    + $" 本卡匹配={list.Count} 栈大小={Core.StackEngine.StackSize}");
            return list;
        }

        /// <summary>发动待发主动奖励（2026-10-20 自由分支定案）：入栈后走响应窗泵（对手可连锁；
        /// 无候选自动双 Pass 结算——技能发动同款）。速度门被拒回插队列不丢。</summary>
        private void ActivatePendingReward(PendingEffect pe)
        {
            var name = pe?.Effect?.DisplayName ?? "待发奖励";
            Debug.Log($"[BattleScreen] 待发奖励发动：{name}");
            bool ok = Core != null && Core.StackEngine.PlayerActivateVoluntary(pe);
            if (!ok) _toast.Show("发动入栈被拒（速度门/结算中）"); // 2026-11-03 失败提示细化：待发奖励唯一拒绝口=速度门
            else _ctrl.SettleResponseWindow().Forget();
            RefreshField();
        }

        /// <summary>发动单位主动效果（本地局）：入栈后无响应候选自动双 Pass（技能发动同款）；
        /// 生物横置/结界耐久消耗随 RefreshField 对账刷新。
        /// 2026-11-14 动态数量：含动态原子的效果发动前先选目标（1..候选数，可取消=不发动），
        /// 选中数量随 ActivateEffect 落原子——结算扣费 base×N、执行按 N 消费预选。</summary>
        private async UniTask ActivateUnitEffectAsync(BattleCardView card, EffectDefinition def)
        {
            List<Entity> targets = null;
            List<int> dynamicCounts = null;
            if (def != null && CardCore.CostDerivationService.HasDynamicAtom(def))
            {
                var decl = await GameActions.CollectEffectDeclarationTargetsAsync(def, card.CoreCard, P1);
                if (decl == null)
                {
                    _toast.Show("已取消发动（目标选择取消）");
                    RefreshField();
                    return;
                }
                targets = decl.Targets;
                dynamicCounts = decl.DynamicCounts;
                CardCore.CostDerivationService.ApplyDynamicCounts(def, dynamicCounts);
            }
            Debug.Log($"[BattleScreen] 单位效果发动：{card.Name}（{card.CardId}）→ {def.DisplayName ?? def.Id}");
            bool ok = GameActions.ActivateEffect(Core, P1, def, card.CoreCard, targets, dynamicCounts, 0, out var activateReason);
            if (!ok) _toast.Show(activateReason ?? "无法发动（费用/横置/耐久/阶段门禁）"); // 引擎真因（2026-11-03 失败提示细化）
            else _ctrl.SettleResponseWindow().Forget();
            RefreshField();
        }

        private RegionTargetSelector _regionSelector; // 结算期反问选择器（null=自动代选）
        private BattleRegionPanel _regionPanelCtl;    // Region 浏览面板（2026-10-18 拆分·Battle/BattleRegionPanel.cs）
        private BattleInformationPanel _infoCtl;      // Information 区域卡查阅窗（2026-11-03·Battle/BattleInformationPanel.cs）
        private int _deckPreviewOwner = -1;           // 牌库预览条开着的属主（-1=关·2026-10-26 诅咒/祝福标记预览）

        // ---- Region 面板守卫转发（2026-10-18 拆分——本体在 BattleRegionPanel；选择面占用期间
        //      不开关面板=窗口蒸发教训 2026-10-15，守卫留在屏侧因 _regionSelector 是决策流中枢） ----
        private void OpenRegion(RegionKind kind, Player owner)
        {
            if (_regionSelector != null && _regionSelector.IsOpen) return; // 目标选择进行中：Region 被选择面占用
            CloseDeckPreview(); // 预览条与浏览面板互斥（2026-10-26）
            CloseInformation(); // Select 浏览与 Information 同位互斥（2026-11-03）
            _regionPanelCtl?.Open(kind, owner);
        }

        private void CloseRegion()
        {
            if (_regionSelector != null && _regionSelector.IsOpen) return; // 选择进行中（弃牌等反问占用选择面）
            CloseDeckPreview(); // 2026-10-26：预览条随浏览面板同收（棋盘点击路径已在 HandleFieldClick 头收过，此处兜其他调用口）
            _regionPanelCtl?.Close();
        }

        private void RefreshRegion()
        {
            if (_regionSelector != null && _regionSelector.IsOpen) return;
            _regionPanelCtl?.Refresh();
            _infoCtl?.Refresh(); // Information 同步对账（2026-11-03：查看期间区域变动重列）
        }

        // ---- Information 区域查阅窗（2026-11-03 战场UI改版：墓地/除外/有地牌的元素格列卡）----

        /// <summary>区域点击开 Information 的闸：选择面在身且候选条未收起（btn-hide）时不开——
        /// 收起期=玩家正在观察场面，允许查阅（需求6）；3D 接管态点击到不了这里（PollFieldPointer 让路）。</summary>
        private bool InformationAllowed()
            => !(_regionSelector != null && _regionSelector.IsOpen && !_regionSelector.ViewportHidden);

        private void OpenInformation(Zone zone, Player owner)
        {
            if (!InformationAllowed()) return;
            CloseRegion(); // Select 浏览面板与 Information 同位——先收（选择在身时本就开不了浏览）
            _infoCtl?.OpenZone(zone, owner, owner != P1);
        }

        private void OpenInformationLands(int ownerIdx, int x, int z)
        {
            if (!InformationAllowed()) return;
            CloseRegion();
            _infoCtl?.OpenLands(ownerIdx, x, z, ownerIdx != 0);
        }

        /// <summary>Select 占窗（选择/文本/下拉/预览/btn-hide 切回显示）让位回调收口（需求4/7）。</summary>
        private void CloseInformation() => _infoCtl?.Close();

        private List<BattleCardView> LandsOfOwnerIdx(int ownerIdx)
            => ownerIdx == 0 ? _view?.SelfLands : _view?.OppLands;

        // ---- 牌库标记预览（2026-10-26 诅咒/祝福显示层补充——效果描述不提，纯可见性增强） ----

        private List<Card> MarkedDeckCards(Player owner)
            => BattleHoverInfoProvider.MarkedDeckCardsOf(Core, owner);

        /// <summary>牌库标记常驻氛围对账（ApplyProjection 链）：牌库中存在诅咒/祝福标记卡=对应
        /// 氛围件常亮。幂等全量——事件丢拍自愈，与 SetDeckCount 同生命周期节拍。</summary>
        private void ReconcileDeckMarks()
        {
            if (Core == null) return;
            for (int owner = 0; owner <= 1; owner++)
            {
                var p = owner == 0 ? P1 : P2;
                if (p == null) continue;
                bool curse = false, bless = false;
                var cards = Core.ZoneManager.GetCards(p, Zone.Deck);
                if (cards != null)
                    foreach (var c in cards)
                    {
                        if (c == null) continue;
                        if (!curse && c.GetCounterCount(CounterRules.CurseCounter) > 0) curse = true;
                        else if (!bless && c.GetCounterCount(CounterRules.BlessingCounter) > 0) bless = true;
                    }
                BattleZoneProps.SetDeckMarks(owner, curse, bless);
            }
        }

        /// <summary>打开牌库标记预览条（复用目标选中栏的非交互显示面）：列出该牌库被诅咒/祝福
        /// 标记的卡（ACardView 全卡面+指示物芯片）。空库/无选择器静默不开。</summary>
        private void OpenDeckPreview(int ownerIdx)
        {
            if (_regionSelector != null && BuildDeckPreview(ownerIdx, out var title, out var items))
            {
                _deckPreviewOwner = ownerIdx;
                _regionSelector.ShowPreview(title, items);
            }
        }

        private void CloseDeckPreview()
        {
            if (_deckPreviewOwner < 0) return;
            _deckPreviewOwner = -1;
            _regionSelector?.ClosePreview();
        }

        /// <summary>预览内容装配（标题+卡面）：诅咒在前祝福在后；false=该库无标记卡。</summary>
        private bool BuildDeckPreview(int ownerIdx, out string title, out List<CardOverlayItem> items)
        {
            title = null;
            items = null;
            var owner = ownerIdx == 0 ? P1 : P2;
            var marked = MarkedDeckCards(owner);
            if (owner == null || marked.Count == 0) return false;
            int curse = marked.Count(c => c.GetCounterCount(CounterRules.CurseCounter) > 0);
            items = marked.Select(CardOverlayItem.FromLiveCard).ToList();
            title = $"{(ownerIdx == 0 ? "己方" : "对方")}牌库 · 诅咒 {curse} · 祝福 {marked.Count - curse}";
            return true;
        }

        // TempWalletLines（临时元素 Region 面板行）：2026-10-14 技能格定案随 RegionKind.Temp 一并退役
    }
}
