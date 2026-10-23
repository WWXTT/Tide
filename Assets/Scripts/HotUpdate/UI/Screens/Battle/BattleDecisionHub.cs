using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using GameBoard;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SynergyUI
{
    /// <summary>
    /// 决策中枢（2026-10-18 第二轮 B3）：守卫窗口（棋盘箭头交互）+ 响应窗口（速度分档询问）
    /// + 不连锁/取消三义按钮 + 选择面取消键——「对手行动时问我什么」的全部形态收口一处。
    /// 共享状态协议原样随迁（本族是 BattleScreen 公认最险的耦合面）：
    /// - _notChainSerial 代次：收口即 +1 作废在途倒计时（ResolveGuardPrompt/OnNotChainClick/
    ///   FinishResponsePickUi/Shutdown 四口同源）；
    /// - _responsePassAll 整窗让过旗：不连锁/倒计时归零/切屏任一置位后，分档询问不再降档开窗；
    /// - _btnNotChain 三义（守卫让过/响应窗不连锁/选择面取消）：显隐收口单源在本类。
    /// OnExit 收口序（Shutdown）：整窗旗先置→守卫窗同步续跑收口→代次++→选择面 Cancel+ForceIdle
    /// →拦截箭头回收——顺序有逐条依据，勿动。
    /// 纯 C# 类：视图/引擎/横幅/悬停路由/选择器全经 ctor Func 注入；探针快照（2026-10-17
    /// 守卫点击失联取证）经 probeSnapshot 委托由屏侧供数。
    /// DecisionRequest 全面统一（模式抉择/选格/选色并入同管线）另立轮次——本步行为保持。
    /// </summary>
    internal sealed class BattleDecisionHub
    {
        private readonly Func<BattleViewData> _view;
        private readonly Func<GameCore> _core;
        private readonly Func<Player> _p1;
        private readonly Func<Player> _p2;
        private readonly Func<BattleFieldView> _field;
        private readonly BattleToastHub _toast;
        private readonly Action<string> _hoverRouter;         // 悬停路由（守卫期也要能看牌）
        private readonly Action<string> _banner;                // 横幅：null=复位教学文案；否则守卫提示覆盖
        private readonly Func<RegionTargetSelector> _selector;  // 结算期反问选择器（StartLocalFan 建前恒 null）
        private readonly Func<Card, string> _cardName;
        private readonly Func<string> _probeSnapshot;           // 取证快照（targeting/picker/overUI/view/cam）

        // ---- 不入连锁三义按钮 ----
        private Button _btnNotChain; // 不入连锁：守卫响应窗口让过口+选择/响应窗「取消/不连锁」
                                     //（2026-10-17 保持根级——守卫窗口 Region 关也要可见），仅窗口期可见
        private int _notChainSerial;                                       // 不连锁倒计时代次（收口即+1 作废旧循环）
        private bool _responsePassAll;                                     // 整窗让过旗（不连锁/倒计时归零/切屏——分档询问中两档同拒）
        private const int NotChainSeconds = 10;                            // 对手回合主动效果：连锁窗口秒数（超时=拒绝连锁）

        // ---- 守卫窗口状态（2026-10-14 棋盘箭头交互：点守卫生物出线→点攻击者确认）----
        // 2026-10-16 角色守卫：守卫者可以是角色（弹药>0 候选——点角色格选中）；攻击者也可以是
        // 对方角色（角色攻击宣言）——确认=点对方角色格。
        private UniTaskCompletionSource<ResponseOption> _guardTcs;
        private List<ResponseOption> _guardOptions;
        private Entity _guardAttacker;             // 本次被响应攻击的攻击者（引擎实体：单位卡或角色）
        private BattleCardView _guarder;           // 已选守卫者（null 且 _guarderIsHero=角色，双 null=未选）
        private bool _guarderIsHero;               // 守卫者=我方角色（2026-10-16 角色守卫）
        private readonly BattleAttackArrow _guardArrow = new BattleAttackArrow();
        private bool _guardProbeAlive;             // 取证（2026-10-17 守卫点击失联）：轮询存活心跳一次性标记

        private bool _selectCancelShown; // 「取消」键是否本屏选择面亮出（关窗收口判据——响应窗自管的 不连锁 不碰）

        private BattleViewData View => _view();
        private GameCore Core => _core();
        private Player P1 => _p1();
        private Player P2 => _p2();
        private BattleFieldView Field => _field();
        private RegionTargetSelector Selector() => _selector();

        public bool GuardWindowOpen => _guardTcs != null;

        /// <summary>守卫候选（三键对账读——守卫窗口期蓝键判定）。</summary>
        public List<ResponseOption> GuardCandidates => _guardOptions;

        public BattleDecisionHub(Func<BattleViewData> view, Func<GameCore> core,
            Func<Player> p1, Func<Player> p2, Func<BattleFieldView> field, BattleToastHub toast,
            Action<string> hoverRouter, Action<string> banner,
            Func<RegionTargetSelector> selector, Func<Card, string> cardName,
            Func<string> probeSnapshot)
        {
            _view = view;
            _core = core;
            _p1 = p1;
            _p2 = p2;
            _field = field;
            _toast = toast;
            _hoverRouter = hoverRouter;
            _banner = banner;
            _selector = selector;
            _cardName = cardName;
            _probeSnapshot = probeSnapshot;
        }

        /// <summary>按钮绑定（Build 期——btn-notchain 预制体根级按钮；初始隐藏）。</summary>
        public void Bind(Button notChain)
        {
            _btnNotChain = notChain;
            if (_btnNotChain != null) _btnNotChain.gameObject.SetActive(false);
        }

        /// <summary>取证心跳（PollFieldPointer 顶部转发）：守卫窗开启期一次性存活标记+左键分支快照。</summary>
        /// <summary>取证心跳（PollFieldPointer 顶部转发）：守卫窗开启期一次性存活标记+确认触发分支快照
        /// （2026-10-18 安卓滑动输入口径：BattlePointer 统一采样——快照供数经 ctor 委托由屏侧注入）。</summary>
        public void PollProbe()
        {
            if (!GuardWindowOpen) { _guardProbeAlive = false; return; }
            if (!_guardProbeAlive)
            {
                _guardProbeAlive = true;
                Debug.Log("[GuardProbe] 守卫窗口开启，棋盘指针轮询存活（此后每次确认触发打分支快照）");
            }
            if (BattlePointer.ConfirmPressed)
                Debug.Log($"[GuardProbe] 确认触发：{_probeSnapshot()}");
        }

        /// <summary>OnExit 收口协议（顺序有逐条依据——类注释；选择器经 ctor 注入取用）。</summary>
        public void Shutdown()
        {
            _responsePassAll = true;         // 整窗让过旗先置（ResolveGuardPrompt 同步续跑分档流程，防降档再开窗）
            if (_guardTcs != null) ResolveGuardPrompt(null, "界面退出——让过");
            _notChainSerial++;               // 作废在途响应窗倒计时（2026-10-16 弹窗退役后收口单源）
            var sel = Selector();
            sel?.Cancel();       // Region 选择面同步收口（空结果——防销毁后异步收尾触已毁节点）
            sel?.ForceIdle();    // 三态钉回空闲（2026-10-17）：Cancel 后 Run 收尾链可能孤悬——IsOpen/busy 残留会闭死新局点击路由
            _guardArrow.Dispose();
        }

        /// <summary>守卫候选走棋盘交互（用户定案：不弹列表）——点我方守卫生物/角色格=选中出拦截箭头
        /// （悬停攻击者绿、其余黄），再点攻击者=确认；点其他守卫者=换人；右键/ESC=让过；
        /// 点空地=取消选择（窗口仍在，防误触让过）。
        /// moreTiers（2026-10-16 分档询问）=让过守卫后同轮还有速度2+响应要问（文案提示；
        /// 「不连锁」按钮=整窗两档同拒）。</summary>
        private UniTask<ResponseOption> BeginGuardPrompt(Player holder, List<ResponseOption> guards,
            bool moreTiers = false)
        {
            _guardTcs = new UniTaskCompletionSource<ResponseOption>();
            _guardOptions = guards;
            _guardAttacker = guards[0].AttackInstance?.Source;
            _guarder = null;
            _guarderIsHero = false;
            string atkName = GuardAttackerName();
            string tgtName = EntityDisplay(AttackTargetOf(guards[0]));
            bool heroCanGuard = HasHeroGuardOption();
            _banner($"对手的 {atkName} 攻击 {tgtName}！点我方守卫生物{(heroCanGuard ? "/角色" : "")}拦截"
                + (moreTiers ? "（右键/ESC 让过——让过后还可发动速度2响应）" : "（右键/ESC 让过）"));
            if (_btnNotChain != null)
            {
                SetNotChainLabel("不连锁"); // 复位（上轮倒计时可能残留【N】文案）
                _btnNotChain.gameObject.SetActive(true);
            }
            Debug.Log($"[守卫窗口] {atkName} → {tgtName}：{guards.Count} 个守卫候选（含角色={(heroCanGuard ? "是" : "否")}"
                + $"{(moreTiers ? "；速度1档（让过后问速度2+）" : "")}；棋盘点选，右键让过）");
            return _guardTcs.Task;
        }

        /// <summary>守卫候选里是否有角色（SourcePlayer 匹配 P1——引擎侧资格=攻击弹药&gt;0）。</summary>
        private bool HasHeroGuardOption()
            => _guardOptions != null && P1 != null
               && _guardOptions.Exists(o => o != null && ReferenceEquals(o.SourcePlayer, P1));

        /// <summary>攻击者显示名（单位卡=卡名；角色=玩家名）。</summary>
        private string GuardAttackerName()
            => _guardAttacker is Card ac ? _cardName(ac) : (_guardAttacker as Player)?.Name ?? "角色";

        private static Entity AttackTargetOf(ResponseOption opt)
            => opt?.AttackInstance?.Targets is List<Entity> t && t.Count > 0 ? t[0] : null;

        public void HandlePointer(string hitName, Camera cam)
        {
            _hoverRouter(hitName); // 悬停卡面/信息保留（选守卫者时也要能看牌）

            int x, z;
            bool hasCell = BattleHoverInfoProvider.TryParseTile(hitName, out x, out z);
            var atkCell = GuardAttackerCell();
            bool onAttacker = hasCell && atkCell != null && atkCell.Value.x == x && atkCell.Value.z == z;

            // 拦截箭头（2026-10-15 定案：守卫=蓝）：守卫者→指针拉长；悬到攻击者=吸附目标格变绿（选对了）
            //（2026-10-18 触摸：无悬停——滑动中箭头才跟，终点=手指点位不偏置）
            if (_guarder != null || _guarderIsHero)
            {
                if (!BattlePointer.TouchTracked || BattlePointer.SlideActive)
                {
                    if (onAttacker)
                        _guardArrow.Show(GuarderAnchor(), GuardAttackerAnchor(), BattleAttackArrow.ValidSkin);
                    else if (CombatChoreographer.MouseGroundPoint(cam, BattlePointer.AimPoint, out Vector3 mousePt))
                        _guardArrow.Show(GuarderAnchor(), mousePt, BattleAttackArrow.GuardSkin);
                    else
                        _guardArrow.Hide();
                }
                else _guardArrow.Hide();
            }

            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
            {
                ResolveGuardPrompt(null, "让过（右键/ESC）");
                return;
            }
            if (!BattlePointer.ConfirmPressed) return;
            Debug.Log($"[GuardProbe] 守卫路由收到确认：hit={hitName} selected={(_guarder != null ? _guarder.Name : (_guarderIsHero ? "角色" : "无"))}");

            if (!hasCell) { SelectGuarder(null); return; }

            // 我方角色格=角色守卫入口（2026-10-16：点格体或【守卫】键均=选角色为守卫者）
            var myChar = BoardLayout.CharacterCell(0);
            if (x == myChar.x && z == myChar.z && HasHeroGuardOption())
            {
                SelectGuarder(null, hero: true);
                return;
            }

            var mine = (View.SelfUnits ?? new List<BattleCardView>())
                .FirstOrDefault(c => c.X == x && c.Z == z && !c.IsDead);
            if (mine != null)
            {
                if (_guardOptions.All(o => !ReferenceEquals(o.SourceCard, mine.CoreCard)))
                {
                    _toast.Show("该生物不能守卫（已横置或无守卫能力）");
                    return;
                }
                SelectGuarder(mine);
                return;
            }
            if (_guarder == null && !_guarderIsHero) return;
            if (onAttacker)
            {
                var opt = _guarderIsHero
                    ? _guardOptions.FirstOrDefault(o => ReferenceEquals(o.SourcePlayer, P1))
                    : _guardOptions.FirstOrDefault(o => ReferenceEquals(o.SourceCard, _guarder.CoreCard));
                ResolveGuardPrompt(opt, $"守卫确认：{(_guarderIsHero ? "角色" : _guarder.Name)} 拦截 {GuardAttackerName()}");
                return;
            }
            SelectGuarder(null); // 点空地/非攻击者=只取消选择（不让过）
        }

        /// <summary>弹窗守卫钮入口（2026-10-23 v4 §10.2）：格→守卫者选择——与点格选守卫者
        /// 完全同口（角色格=角色守卫；候选外单位=提示；窗口未开由调用方提示）。</summary>
        public void PickGuarderAt(int x, int z)
        {
            if (_guardTcs == null) return; // 窗口已关（弹窗残留点击竞态）
            var myChar = BoardLayout.CharacterCell(0);
            if (x == myChar.x && z == myChar.z && HasHeroGuardOption())
            {
                SelectGuarder(null, hero: true);
                return;
            }
            var mine = (View.SelfUnits ?? new List<BattleCardView>())
                .FirstOrDefault(c => c != null && c.X == x && c.Z == z && !c.IsDead);
            if (mine == null) return;
            if (_guardOptions.All(o => !ReferenceEquals(o.SourceCard, mine.CoreCard)))
            {
                _toast.Show("该生物不能守卫（已横置或无守卫能力）");
                return;
            }
            SelectGuarder(mine);
        }

        private void SelectGuarder(BattleCardView v, bool hero = false)
        {
            bool isHero = v == null && hero;
            if (_guarder == v && _guarderIsHero == isHero) return;
            _guarder = v;
            _guarderIsHero = isHero;
            if (v == null && !isHero) _guardArrow.Hide();
            else Debug.Log($"[守卫窗口] 选守卫者 {(isHero ? "角色" : v.Name)}——点攻击者确认，右键让过");
        }

        private void ResolveGuardPrompt(ResponseOption opt, string reason)
        {
            Debug.Log($"[守卫窗口] {reason}");
            _guardArrow.Hide();
            _guarder = null;
            _guarderIsHero = false;
            _guardOptions = null;
            _guardAttacker = null;
            var tcs = _guardTcs;
            _guardTcs = null;
            _notChainSerial++; // 作废在途倒计时（2026-10-16 混合窗口=守卫+Region 并行，收口单源）
            _banner(null); // 复位回教学步骤文案（无则隐藏）
            if (_btnNotChain != null) _btnNotChain.gameObject.SetActive(false);
            tcs?.TrySetResult(opt);
        }

        /// <summary>不入连锁按钮（2026-10-16 弹窗退役后收口单源）：守卫窗口/Region 响应面
        /// 任一在身=立即让过（Region 收口空表=Pass）；均未开时无操作（防误触）。
        /// 分档询问期=整窗两档同拒（_responsePassAll 防「速度1让过→速度2又被问」）。</summary>
        public void OnNotChainClick()
        {
            bool guardOpen = _guardTcs != null;
            bool regionPick = Selector() != null && Selector().IsOpen;
            if (!guardOpen && !regionPick) return;
            _notChainSerial++; // 作废在途倒计时
            _responsePassAll = true; // 整窗让过：不连锁=拒绝连锁，后续档不再问
            Debug.Log("[响应窗口] 不连锁按钮——立即让过（整窗）");
            if (guardOpen) ResolveGuardPrompt(null, "不入连锁（按钮让过）");
            Selector()?.Cancel();
        }

        private BattleCardView GuardAttackerView()
            => (View.OppUnits ?? new List<BattleCardView>())
                .FirstOrDefault(c => !c.IsDead && ReferenceEquals(c.CoreCard, _guardAttacker));

        /// <summary>守卫窗口攻击者格（单位=其落格；对方角色攻击=其角色格——2026-10-16）。</summary>
        private (int x, int z)? GuardAttackerCell()
        {
            if (_guardAttacker is Card) { var v = GuardAttackerView(); return v != null ? ((int x, int z)?)(v.X, v.Z) : null; }
            if (_guardAttacker is Player) { var c = BoardLayout.CharacterCell(1); return (c.x, c.z); }
            return null;
        }

        /// <summary>守卫窗口攻击者世界锚点（单位=模型 wrapper/格心；角色=角色格心上抬——立柱半高）。</summary>
        private Vector3 GuardAttackerAnchor()
        {
            var v = GuardAttackerView();
            if (v != null) return UnitAnchorOf(v);
            var cell = GuardAttackerCell();
            return cell != null
                ? BattleBoardGrid.WorldPositionOf(cell.Value.x, cell.Value.z) + new Vector3(0f, 0.6f, 0f)
                : Vector3.zero;
        }

        /// <summary>守卫者世界锚点（角色守卫=我方角色格心上抬，与立柱半高同口径）。</summary>
        private Vector3 GuarderAnchor()
        {
            if (_guarderIsHero)
            {
                var c = BoardLayout.CharacterCell(0);
                return BattleBoardGrid.WorldPositionOf(c.x, c.z) + new Vector3(0f, 0.6f, 0f);
            }
            return UnitAnchorOf(_guarder);
        }

        /// <summary>单位世界锚点：模型 wrapper 优先，回落格心（与编舞器 AttackerPos 同口径）。</summary>
        public Vector3 UnitAnchorOf(BattleCardView v)
        {
            if (v == null) return Vector3.zero;
            if (Field != null && Field.TryGetUnitBody(v.RuntimeId, out var wrapper, out _))
                return wrapper.position;
            return BattleBoardGrid.WorldPositionOf(v.X, v.Z);
        }

        /// <summary>人类响应窗口（2026-10-16 深夜定案：按速度分档顺序询问）。流程：
        /// ①速度1档（守卫=速度1响应）先问——棋盘箭头交互；若响应：守卫入栈、泵翻转优先权问对手
        /// 跟响应，记速器升档后后续窗口天然只剩速度2+（引擎既有口径：非回合方响应须严格高于
        /// 连锁当前最高速度，攻击=速度0上栈）。②速度1让过——同轮降档问速度2+（诱发/手牌响应卡
        /// =Region 面板卡牌列表，min=0：不选+确定=让过；对手回合 btn-notchain 10 秒倒计时）。
        /// 两档皆让=Pass。旧全屏列表弹窗已整删（遮罩拦棋盘=守卫卡死根因）。</summary>
        public async UniTask<ResponseOption> ShowResponseAsync(Player holder, List<ResponseOption> options)
        {
            var guards = options.Where(o => o.Kind == ResponseOption.ResponseKind.GuardAbility
                                            && (o.SourceCard != null || o.SourcePlayer != null)).ToList();
            var rest = options.Where(o => !guards.Contains(o)).ToList();
            _responsePassAll = false;
            // 响应窗提示（2026-10-17 label-num 并入后）：①档（守卫·Region 关）无面板——srcText 回落
            // Console（守卫指令另走 UpdatePromptBanner 同落日志）；②档 Region 面板 label-num 会以
            // 同源文本+择卡指引渲染（SelectIndicesAsync 的 hint），关窗/整窗收尾 finally 清空
            string srcText = DescribeResponseSource();
            _toast.Show(srcText);
            try
            {
            // ① 速度1档：守卫棋盘（无倒计时——同纯守卫窗口口径；不连锁按钮=整窗两档同拒）
            if (guards.Count > 0)
            {
                var guardOpt = await BeginGuardPrompt(holder, guards, moreTiers: rest.Count > 0);
                if (guardOpt != null) return guardOpt; // 守卫入栈→泵问对手跟响应→后续窗口自然只剩速度2+
                if (_responsePassAll || rest.Count == 0) return null;
                Debug.Log("[响应窗口] 速度1让过——降档询问速度2+（Region）");
            }
            else if (rest.Count == 0)
                return null;

            // ② 速度2+档：Region 卡牌列表（缺 Region=无交互让过；守卫档已在此之前保住棋盘）
            if (Selector() == null) return null;
            var labels = new List<string>(rest.Select(o => o.Label));
            if (holder == P1 && (View == null || !View.MyTurn)) // 仅对手回合计时（超时=拒绝连锁）
                NotChainCountdownAsync(++_notChainSerial).Forget();
            else if (_btnNotChain != null) // 我方回合：无倒计时，不连锁按钮=显式让过口（守卫档收口已隐藏）
            {
                SetNotChainLabel("不连锁");
                _btnNotChain.gameObject.SetActive(true);
            }
            var picked = await Selector().SelectIndicesAsync(labels, 0, 1, "响应窗口",
                srcText + "\n——选一张响应卡响应，或点「不连锁」让过", false, 60f);
            FinishResponsePickUi();
            return picked != null && picked.Count > 0 ? rest[picked[0]] : null;
            }
            finally
            {
                _toast.Show(""); // 整窗收口清 toast（②档关窗 prompt 已清，此处兜底守卫档早退路径）
            }
        }

        /// <summary>纯 Region 响应面收尾（守卫不在身时）：作废在途倒计时+按钮复位隐藏
        ///（混合态守卫在身由 ResolveGuardPrompt 统一收口）。</summary>
        private void FinishResponsePickUi()
        {
            if (_guardTcs != null) return;
            _notChainSerial++;
            if (_btnNotChain != null)
            {
                SetNotChainLabel("不连锁");
                _btnNotChain.gameObject.SetActive(false);
            }
        }

        /// <summary>不连锁倒计时（2026-10-15；2026-10-16 改挂窗口状态）：按钮文案「不连锁【N】」
        /// 逐秒倒数；代次不符（选卡/守卫定/让过/切屏已收口）即报废；归零=放弃选择（拒绝连锁）——
        /// Region 响应面收口空表（=Pass）+守卫窗口让过。</summary>
        private async UniTaskVoid NotChainCountdownAsync(int serial)
        {
            for (int s = NotChainSeconds; s >= 1; s--)
            {
                SetNotChainLabel($"不连锁【{s}】");
                if (_btnNotChain != null) _btnNotChain.gameObject.SetActive(true);
                await UniTask.Delay(1000);
                if (serial != _notChainSerial) return;
            }
            if (serial != _notChainSerial) return;
            Debug.Log($"[响应窗口] 倒计时归零——{NotChainSeconds} 秒未操作，放弃选择（拒绝连锁）");
            _responsePassAll = true; // 整窗让过：归零后不再降档询问
            Selector()?.Cancel();
            if (_guardTcs != null) ResolveGuardPrompt(null, "倒计时归零——守卫让过");
        }

        private void SetNotChainLabel(string text)
        {
            if (_btnNotChain == null) return;
            var lbl = _btnNotChain.GetComponentInChildren<TMP_Text>(true);
            if (lbl != null) lbl.text = text;
        }

        private string EntityDisplay(Entity e)
        {
            if (e is Player p)
            {
                string who = p == P1 ? "我方" : "对手";
                return $"{who}角色（生命 {p.Life}）";
            }
            if (e is Card c) return $"{_cardName(c)} [{c.GetPower()}/{c.GetLife()}]";
            return e.ToString();
        }

        /// <summary>选择面取消键驱动（RegionTargetSelector.onCancelAvailable 注入·2026-10-17
        /// 提示并入 label-num 后仅剩按键职责）：开窗且主动效果（allowCancel）亮 btn-notchain
        /// 「取消」——点击走既有 OnNotChain 收口=Cancel 选择（空表→AllowCancel 路径=不发动）；
        /// 关窗=收取消键（仅本屏亮出的才收——响应窗的不连锁自管，勿误关）。</summary>
        public void OnSelectCancel(bool allowCancel)
        {
            if (allowCancel && _btnNotChain != null)
            {
                _selectCancelShown = true;
                SetNotChainLabel("取消");
                _btnNotChain.gameObject.SetActive(true);
            }
            else if (_selectCancelShown)
            {
                _selectCancelShown = false;
                if (_btnNotChain != null) _btnNotChain.gameObject.SetActive(false);
            }
        }

        /// <summary>响应窗「在响应什么」（2026-10-16 定案）：栈顶未结算项→发动方+卡/效果名
        ///（MatchLogRenderer.StackAdd 行同款口径——2026-10-18 B1 收敛）——施放/攻击宣言/效果发动各有措辞。</summary>
        private string DescribeResponseSource()
        {
            var top = Core != null ? Core.StackEngine.PeekStack() : null;
            if (top == null) return "对方行动结算中——可响应或让过";
            string who = top.Controller == P1 ? "我方" : "对方";
            if (top.IsCardCast && top.CastCard != null)
                return $"{who}使用【{_cardName(top.CastCard)}】——可响应（选响应卡）或让过（不连锁）";
            if (top.IsAttackDeclaration)
            {
                string atk = top.Source is Card ac ? _cardName(ac) : "角色";
                string tgt = top.Targets != null && top.Targets.Count > 0 ? EntityDisplay(top.Targets[0]) : "";
                return $"{who}的{atk}攻击{tgt}——可响应（守卫/响应卡）或让过（不连锁）";
            }
            if (top.IsGuardDeclaration) return "守卫宣言结算中——可响应或让过（不连锁）";
            if (top.IsSBA) return "系统动作结算中——可响应或让过（不连锁）";
            string src = top.Source is Card sc ? _cardName(sc)
                : top.Source is Player sp ? (sp == P1 ? "我方角色" : "对方角色") : "?";
            string fxName = top.Definition?.DisplayName;
            bool voluntary = top.Definition != null
                && top.Definition.ActivationType == EffectActivationType.Voluntary;
            return voluntary
                ? $"{who}发动【{src}「{fxName}」】——可响应（选响应卡）或让过（不连锁）"
                : $"【{src}】触发「{fxName}」——可响应（选响应卡）或让过（不连锁）";
        }
    }
}
