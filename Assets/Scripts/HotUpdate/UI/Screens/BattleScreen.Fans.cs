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
        // ======================================== 战场 3D 摆位（2026-10-13 战场重做） ========================================

        /// <summary>摆位刷新（转发投影器——构建/扇出/版本所有权单点在 BattleProjector）。</summary>
        private void RefreshField() => _projector.RefreshField();

        /// <summary>渲染扇出（投影器 onApplied 钩子）：3D 对账+连锁链+2D 信息栏+Region+三键缓存置脏。</summary>
        private void ApplyProjection(BattleViewData d)
        {
            _field?.Apply(d);
            ReconcileDeckMarks(); // 牌库标记常驻氛围（2026-10-26）：与卡堆高度同链对账——标记在库即亮、抽走即灭
            _chainStage?.Refresh(d); // 连锁链克隆对账（2026-10-19：结算期只增不删——保持到逆结算完成；
                                     // 施放卡面呈现归链，左上角 cast-stage 已退役）
            RefreshBars(); // 2D 信息栏随 3D 对账同频刷新
            RefreshRegion(); // Region 区域面板开着则同频重列（区域变化走本刷新链）
            _activateFlagsDirty = true; // 三键效果缓存置脏：单位清单/效果定义可能已变（2026-10-18 性能）
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
            if (NetActive) BattleHudRow.SetPhase(PhaseTileText(d), d.MyTurn); // 网络局维持快照喂数（联机时序另立任务）
            else BattleHudRow.ReconcilePhase(PhaseTileText(d), d.MyTurn); // 本地局正源=相位事件序（OnPhaseAnnounced 入队）；对账仅队列空闲时直写兜底（开局种子/教学换场自愈）
            _hudTicker.RefreshChainSpeed();                 // 连锁速度格（中线 x3）=引擎记速器实值（2026-10-15 接线）
            if (_lblStack != null) _lblStack.text = string.Join(" ｜ ", d.StackLines);
            if (_lblPriority != null)
                _lblPriority.text = d.StackNotEmpty && d.MyPriority ? "◇ 优先权在我（可响应/让行）" : "";
            UpdateButtons(d);
        }


        /// <summary>首刷就绪门（2026-10-13 教学局首刷竞速修复）：棋盘+对局核心齐备——
        /// 首刷曾只等棋盘就绪，会跑赢 StartNewGame（教学场景注入零事件、TurnStart 早于订阅注册），
        /// Core 未就位静默白刷且此后无补偿，预设单位永不渲染。</summary>
        private bool BoardReadyForFirstPaint()
            => BattleBoardGrid.Ready && Core != null && P1 != null && P2 != null;

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

        // ---- 出场粒子/扣费粒子（2026-11-11 P0-2 迁投影器；二期 A 起屏侧闭包退役）：
        //      LocalFilmSource.OnPaid/OnArrival 单源构造 op（Paid/Arrival）经 Present 路由
        //      ——轮内挂帧随播/轮外即时执行；实付账本/印刷费回落规则逐字同源。 ----

        /// <summary>轻刷（转发投影器——构建+仅 2D 信息栏，原口径见 BattleProjector.RefreshLocal）。</summary>
        private void RefreshLocal() => _projector.RefreshLocal();

        // ======================================== 全量渲染（两种模式统一输入 BattleViewData） ========================================

        private void RefreshView()
        {
            var d = _view;
            if (d == null) return;

            // 信息栏
            FillBar(d.Opp, opp: true);
            FillBar(d.Self, opp: false);

            // 战场=3D 层渲染（BattleFieldView 对账）；旧 2D 棋盘六行/手牌条/卡牌层挂载链已退役
            //（2026-10-14 晚拆除——ACard 实例只出自手牌扇区/Region 列卡/prefab 预览位）

            // 中栏（2026-10-13 基础教学：极简 prefab 无中栏节点——null 判空，缺件零炸）
            if (_lblPhase != null) _lblPhase.text = d.PhaseText;
            if (NetActive) BattleHudRow.SetPhase(PhaseTileText(d), d.MyTurn); // 网络局维持快照喂数（联机时序另立任务）
            else BattleHudRow.ReconcilePhase(PhaseTileText(d), d.MyTurn); // 本地局正源=相位事件序（OnPhaseAnnounced 入队）；对账仅队列空闲时直写兜底（开局种子/教学换场自愈）
            _hudTicker.RefreshChainSpeed();                 // 连锁速度格（中线 x3）=引擎记速器实值（2026-10-15 接线）
            if (_lblStack != null) _lblStack.text = string.Join(" ｜ ", d.StackLines);
            if (_lblPriority != null)
                _lblPriority.text = d.StackNotEmpty && d.MyPriority ? "◇ 优先权在我（可响应/让行）" : "";

            // 我方手牌=扇区（RefreshFans 对账），旧手牌条退役
            UpdateButtons(d);
        }

        private void FillBar(BattlePlayerView p, bool opp)
        {
            string aiTag = p.IsAI ? "（AI）" : "";
            // 生命标签走节拍展示值（2026-10-20 角色HP接入命中节拍）：攻击步冻结期角色柱缓冲终值，
            // 这里取当前展示值（旧值）防快照直出；未建柱/首写前回落数据值
            int lifeShown = p.Life;
            if (_field != null && _field.TryGetHeroLifeShown(opp ? 1 : 0, out var shown)) lifeShown = shown;
            if (opp)
            {
                if (_oppName != null) _oppName.text = $"对手{aiTag}";
                if (_oppLife != null) _oppLife.text = $"生命 {lifeShown}/{p.MaxHealth}";
                if (_oppDeck != null) _oppDeck.text = $"牌库 {p.DeckCount}";
                if (_oppHand != null) _oppHand.text = $"手牌 {p.HandCount}";
                if (_oppGrave != null) _oppGrave.text = $"墓地 {p.GraveyardCount}";
                if (_oppFatigue != null) _oppFatigue.text = p.FatigueCount > 0 ? $"疲劳 {p.FatigueCount}" : "";
                if (_oppBank != null) _oppBank.text = $"地牌上限 {p.LandCap} · 临时元素 {p.BankText}";
            }
            else
            {
                if (_selfName != null) _selfName.text = "我方";
                if (_selfLife != null) _selfLife.text = $"生命 {lifeShown}/{p.MaxHealth}";
                if (_selfDeck != null) _selfDeck.text = $"牌库 {p.DeckCount}";
                if (_selfHandCount != null) _selfHandCount.text = $"手牌 {p.HandCount}";
                if (_selfGrave != null) _selfGrave.text = $"墓地 {p.GraveyardCount}";
                if (_selfFatigue != null) _selfFatigue.text = p.FatigueCount > 0 ? $"疲劳 {p.FatigueCount}" : "";
                if (_selfBank != null) _selfBank.text = $"地牌上限 {p.LandCap} · 临时元素 {p.BankText}";
            }
        }

        /// <summary>角色柱HP读数落地回调（BattleFieldView 命中节拍释放时触发，2026-10-20）：
        /// 只同拍重写 2D 生命标签（FillBar 常规刷新走节拍展示值，这里补命中拍这一刻的即时跟进）。</summary>
        private void OnHeroLifeShownChanged(int owner, int life)
        {
            var d = _view;
            if (d == null) return;
            var p = owner == 1 ? d.Opp : d.Self;
            if (owner == 1) { if (_oppLife != null) _oppLife.text = $"生命 {life}/{p.MaxHealth}"; }
            else { if (_selfLife != null) _selfLife.text = $"生命 {life}/{p.MaxHealth}"; }
        }

        private void UpdateButtons(BattleViewData d)
        {
            bool myMain = d.MyTurn && d.Phase == PhaseType.Main && !_gameEnded;

            // _btnGrave 已随 2026-10-30 归土改版整删（墓地视手牌使用退役）
            // _btnSkill/_btnPass 已随 2026-10-18 网络簇整删退役（技能键早已无节点恒 null；让优先权=网络专属）
            // 认输键终局后仍可点=返回主菜单出口（2026-10-16 弹窗退役：旧弹窗取消键职责迁移至此）
            if (_btnConcede != null) _btnConcede.interactable = true;
        }

        // ======================================== 操作派发（本地直调 GameActions / 网络转 intent） ========================================

        private bool GateMyMain()
        {
            if (_gameEnded || _view == null) return false;
            if (IsTargetSelecting()) return false; // Region 抉择/反问在身不开新交互（非模态面板须自防重入，静默）
            if (!_view.MyTurn || _view.Phase != PhaseType.Main)
            {
                // 静默 return 会让点击毫无反馈（联机反馈"使用糊"的主因之一）——给一句可读提示
                _toast.Show(!_view.MyTurn ? "对手回合，暂不可操作" : "当前不是主要阶段");
                return false;
            }
            return true;
        }

        private static bool IsCreatureCard(BattleCardView card)
        {
            return DataOf(card)?.Supertype == Cardtype.Creature;
        }

        private static int GetModeCount(BattleCardView card)
        {
            var data = DataOf(card);
            if (data == null) return 1;
            // P12 抉择默认化：法术模式数=非充能主动效果数（充能不进模式）——CostDerivationService 单源；
            // 永久物模式已随内嵌 Choice 退役恒 1（多模式生物/结界不再存在）
            return CostDerivationService.GetModeCount(data);
        }

        /// <summary>卡的静态数据：本地=引擎对象直读；网络=CardId 查本地卡表（引擎对象缺失）。</summary>
        private static CardData DataOf(BattleCardView card)
        {
            if (card.CoreCard != null) return (card.CoreCard as CardWrapper)?.GetData();
            return string.IsNullOrEmpty(card.CardId) ? null : CardCatalog.GetByInstanceId(card.CardId);
        }

        /// <summary>永久物出牌费用读数（模式窗选项文案用；法术链已随 2026-11-01 抉择上卡改版退役）。</summary>
        private static ElementCost LocalCostOf(BattleCardView card, int modeIndex)
        {
            if (card.CoreCard == null) return null;
            return GameActions.GetCardCost(card.CoreCard, modeIndex);
        }

        private static string CostText(ElementCost cost)
            => cost == null || cost.IsZero ? "0" : BattleView.CostTextOf(cost);

        /// <summary>出牌终态：PlayCard+响应窗口泵（2026-10-18 网络簇整删——intent 上行分支随网络簇退役）。
        /// 2026-10-20 法术改版：法术经 SpellDeclarationFlow 进入（声明期预选目标+贴片预览+可取消撤销），
        /// 本口仍是终态单点（法术带 targets、生物/结界 targets=null 照旧）。
        /// 返回声明成败——法术流的乐观 UI（藏卡+转正贴片）由调用方按 false 清理（拒绝无引擎事件，
        /// 扇区/贴片不会随后续对账自愈）。</summary>
        private bool PlayCardFinal(BattleCardView card, List<Entity> targets, int modeIndex)
        {
            var core = Core;
            if (core == null || P1 == null) return false;

            // 2026-10-16 定案（法术目标口径已被 2026-10-20 改案取代）：指向性法术目标统一在
            // 声明期收集（SpellDeclarationFlow→CollectSpellDeclarationTargetsAsync——可取消=撤销发动）；
            // defer 原子（效果处理时选）结算期反问（必须选）；牌库等隐藏区/区域自结算类效果
            // 不弹选直接结算（引擎 None 模式早退单源）。
            string reject = null;
            // 出牌来源区（2026-10-29 阶段3b 牌库顶当手牌）：牌库顶虚拟手牌打出走 fromZone=Deck
            //（TopDeckPlaySource/IPlaySource 授权）；其余路径恒手牌（墓地施放已随 2026-10-30 归土改版退役）。
            bool ok = GameActions.PlayCard(core, P1, card.CoreCard, targets,
                card.CoreCard != null && card.CoreCard.GetZone() == Zone.Deck ? Zone.Deck : Zone.Hand,
                modeIndex, out reject);
            if (ok)
            {
                // 出牌选格钉提前到声明成功即转 BoardState（2026-10-15 发动格拆除）：施放类型图标=玩家所选格，
                // 入场经 AssignCells 钉优先原地转正——同钉单源（旧「入场事件补钉」机制随施放占格退役）
                if (_pendingPins.Remove(card.RuntimeId, out var pin))
                    _ctrl?.Board?.PinCard(card.CoreCard, pin.x, pin.z);
                _ctrl.SettleResponseWindow().Forget();
            }
            else
            {
                _pendingPins.Remove(card.RuntimeId); // 声明失败：选格单作废
                // 精确拒因直读（同网络 Error 帧口径——通用文案无法区分 费用/目标/代价，2026-09-26）
                _toast.Show($"无法打出（{reject ?? "费用/条件不满足"}）");
            }
            RefreshLocal();
            return ok;
        }

        // LandFlow（手牌直点放地的旧选色弹窗链）已随 2026-10-16 弹窗退役整删：放地现役入口=
        // OnFanPlaceLand 落格箭头（同格多候选色→PickLandColorAsync 走 Region 卡牌列表）。

        // ---- 法术发动声明流（2026-10-20 改案；2026-10-22 B4 反馈修订：恒自动落位→选目标（可取消）→连锁检测→扣费→效果） ----

        /// <summary>法术发动起手（2026-10-22 玩家反馈 B4 修订：手选发动位退役——法术发动位无战略
        /// 语义（施放占格临时），恒自动落位首空格，与墓地路径 PreviewCastingCell / 网络局服务器
        /// first-free 同口径；仅满区拦截。）：登记选格单（PlayCardFinal 声明成功即 PinCard——
        /// PlaceCasting 钉优先落同格）→SpellDeclarationFlow（贴片预览+声明期目标收集，可取消）。
        /// 教学闸预检前置（防落位后才被拒）。</summary>
        private void BeginSpellCastAutoPlace(BattleCardView view, Card card, int modeIndex)
        {
            var board = _ctrl?.Board;
            if (board == null) return;

            // 教学闸预检（同 SpellDeclarationFlow 口径——落位前先验）
            if (RuleHooks.TutorialPlayGate?.Invoke(P1, card) is string gateReject)
            {
                _toast.Show($"教学引导：{gateReject}");
                return;
            }

            (int x, int z) picked = (-1, -1);
            foreach (var cell in BoardLayout.UnitCells(0))
                if (board.CardAt(cell.x, cell.z) == null) { picked = (cell.x, cell.z); break; }
            if (picked.x < 0) { _toast.Show("单位区已满——无法发动（法术须先到发动区占位）"); return; }

            _choreo?.CancelTargeting();
            _pendingPins[card.RuntimeId] = picked;
            Debug.Log($"[HandPlay] 法术自动落位：{view.Name} → ({picked.x},{picked.z})（B4 免手选发动区）");
            _toast.Show("已自动落位发动区");
            SpellDeclarationFlow(view, modeIndex, pickedX: picked.x, pickedZ: picked.z).Forget();
        }

        /// <summary>法术发动声明流：发动位/模式/费用已过 → 预览态（贴片预览——手牌路径=玩家所选格，
        /// 墓地路径自动 first-free + 手牌扇藏卡=「卡已放发动区」观感）→ 声明期目标收集（逐原子问选，
        /// 可取消）→ 取消=撤销发动（PlayCard 未调：使用宣言触发等声明副作用零，选格单一并
        /// 作废，回退绝对干净）→ 确认=预览贴片无缝转正 + PlayCardFinal（真声明：进发动区+上栈+PinCard
        /// 钉所选格）→ SettleResponseWindow（连锁检测=引擎现状；无连锁结算期扣费+粒子+效果表现）。
        /// 无交互原子（全取/随机/全 defer）：收集器同步完成空表——预览同帧转正零可见闪烁
        ///（UniTask 同步前缀保证）。</summary>
        private async UniTaskVoid SpellDeclarationFlow(BattleCardView view, int modeIndex,
            int pickedX = -1, int pickedZ = -1)
        {
            var core = Core;
            var card = view?.CoreCard;
            if (core == null || P1 == null || card == null) return;

            // 教学闸预检（声明期先验——防选完目标才被引擎拒；PlayCard 内终审保留同口径）
            if (RuleHooks.TutorialPlayGate?.Invoke(P1, card) is string gateReject)
            {
                _pendingPins.Remove(card.RuntimeId); // 选位单作废（手选发动位路径）
                _toast.Show($"教学引导：{gateReject}");
                return;
            }

            // 落格：手牌路径=玩家所选格（选格单已登记）；无选格单自动 first-free 落位
            var (cellX, cellZ) = pickedX >= 0 ? (pickedX, pickedZ) : PreviewCastingCell();
            _field?.ShowCastingPreview(card, cellX, cellZ);
            _selfFan?.SetCardActive(card.RuntimeId, false);

            try
            {
                var targets = await GameActions.CollectSpellDeclarationTargetsAsync(card, P1, modeIndex);

                if (targets == null)
                {
                    // 玩家取消——撤销发动：拆预览+还原手牌扇+选格单作废（引擎零状态）
                    _field?.HideCastingPreview();
                    _selfFan?.SetCardActive(card.RuntimeId, true);
                    _pendingPins.Remove(card.RuntimeId);
                    RefreshLocal();
                    return;
                }

                // 确认：预览贴片转正（选格单经 PlayCardFinal 声明成功 PinCard——PlaceCasting 钉优先落同格）
                _field?.PromoteCastingPreview(card, cellX, cellZ);
                if (!PlayCardFinal(view, targets, modeIndex))
                {
                    // 声明被引擎终审拒（如弃牌代价·手牌无目标）：镜像取消路径清理。转正件已进
                    // _castings 正片表，HideCastingPreview 够不着——走 InterruptCasting 即清口
                    // （CardActivationFailedEvent 同款）；拒绝零引擎事件，贴片/藏卡不会随后续
                    // 对账自愈（2026-10-26 bug：失败后手牌空+发动标识滞留）。
                    _field?.InterruptCasting(card.RuntimeId);
                    _selfFan?.SetCardActive(card.RuntimeId, true);
                    RefreshLocal();
                }
            }
            catch (Exception ex)
            {
                // 异常兜底=按取消清理（2026-10-20 实测教训：续体异常打断声明流会滞留预览/藏卡不还=UI 卡死）
                Debug.LogException(ex);
                _field?.HideCastingPreview();
                _selfFan?.SetCardActive(card.RuntimeId, true);
                _pendingPins.Remove(card.RuntimeId);
                RefreshLocal();
            }
        }

        /// <summary>声明期贴片预览落格（墓地路径回落用——手牌路径已改玩家手选发动位 2026-10-20）：
        /// 镜像 BoardState.PlaceCasting 法术档（first-free——UnitCells(0) 首个无占用格；
        /// 预览期无状态变更，引擎届时落同格）。满格 (-1,-1)=无贴片预览。</summary>
        private (int x, int z) PreviewCastingCell()
        {
            var board = _ctrl?.Board;
            if (board == null) return (-1, -1);
            foreach (var c in BoardLayout.UnitCells(0))
                if (board.CardAt(c.x, c.z) == null)
                    return (c.x, c.z);
            return (-1, -1);
        }

        /// <summary>放地锁产色（2026-10-16 弹窗退役：旧选色弹窗→Region 卡牌列表，min=1 强制选；
        /// 单色直落）。null=缺 Region 静默取消（本次放置作废）。</summary>
        private async UniTaskVoid PickLandColorAsync(BattleCardView view, List<ManaType> colors)
        {
            if (colors == null || colors.Count == 0) return;
            if (colors.Count == 1)
            {
                LandFinal(view, colors[0]);
                return;
            }
            var options = new List<string>(colors.Count);
            foreach (var c in colors) options.Add($"锁定 {BattleView.ManaZh(c)} 产色");
            int? picked = await PickRegionOptionAsync("选择地牌产色", options);
            if (picked == null) return;
            LandFinal(view, colors[picked.Value]);
        }

        /// <summary>放地终态：AddToElementPool(锁色)——拒绝原因引擎直读（2026-11-03 失败提示细化）。</summary>
        private void LandFinal(BattleCardView card, ManaType color)
        {
            string landReason = null; // 预置 null：Core 为空短路时 out 不赋值（CS0165）
            bool ok = Core != null && GameActions.AddToElementPool(Core, P1, card.CoreCard, color, out landReason);
            if (!ok) _toast.Show(landReason ?? "无法放入元素池（上限/资格）");
            RestoreFan(); // 放地即时对账（2026-10-14）：手牌扇收卡+地格「可用/上限」上盘——
                          // RefreshLocal 只刷 2D 信息栏，扇区/3D 摆位原要等下一事件才补（入池只发 ElementPoolAddEvent）
        }

        // ======================================== 手牌扇出牌接线（2026-10-14 放置/发动按钮 + 箭头选格） ========================================

        /// <summary>扇区卡 → 引擎手牌卡（RuntimeId 对账；不在手=已过态返回 null）。
        /// 牌库顶虚拟手牌（2026-10-29 阶段3b）：手牌对账落空再查牌库顶（RuntimeId 同键——
        /// 「只可打出」的出牌链由此找到卡实例）。</summary>
        private Card FanCardOf(HandFanCard fan)
        {
            if (fan == null || Core == null || P1 == null) return null;
            foreach (var c in Core.ZoneManager.GetCards(P1, Zone.Hand))
                if (c != null && c.RuntimeId == fan.RuntimeId) return c;
            var top = TopDeckVirtualCard();
            return top != null && top.RuntimeId == fan.RuntimeId ? top : null;
        }

        /// <summary>GateFanMyMain 静默版（按钮/流光逐帧评估用——不弹 toast）。</summary>
        private bool FanGateQuiet()
        {
            if (_gameEnded || _ctrl == null || Core == null || P1 == null) return false;
            var phase = Core.TurnEngine?.CurrentPhase;
            return _ctrl.TurnPlayer == P1 && phase != null && phase.Phase == PhaseType.Main;
        }

        /// <summary>「放置」按钮条件：可作地牌（正式三型/非衍生物/非临时——锁定卡可放地，2026-11-02）+ 有可锁产色
        /// + 地牌池有余位（各自上限口径：PooledCards.Count &lt; GetLandCap）。
        /// 牌库顶虚拟手牌（2026-10-29 阶段3b）「只可打出」——限手牌区，不放地。</summary>
        private bool FanCanPlaceLand(HandFanCard fan)
        {
            if (!FanGateQuiet()) return false;
            var card = FanCardOf(fan);
            if (card == null || card.GetZone() != Zone.Hand || Core?.ElementPool == null) return false;
            return ElementPoolSystem.CanServeAsLand(card)
                && ElementPoolSystem.GetLandColorChoices(card).Count > 0
                && Core.ElementPool.CanAddLand(P1);
        }

        /// <summary>「使用」按钮条件（2026-11-01 SpUse 退役·合并口径）：
        /// 普通出牌=任一模式 CanAffordCast（含声明承诺 pending——代价补偿黑/白不预计算垫本卡，
        /// 2026-10-16 恢复旧序）+永久物另需单位区空格（满场不可出）；
        /// 特殊行=充能（2026-11-05 系统动作化；2026-11-06 opt-in：卡面声明资格、本回合未充过且有
        /// 可充颜色——点击时 CanCharge 终审；免费落层、颜色结算期反问）。
        /// 法术多主动（2026-11-01 抉择上卡）：使用钮按卡面效果行选中模式直入施放
        ///（选中行不可付时点击 toast 拒——亮钮条件仍是任一模式可付）。</summary>
        private bool FanCanUse(HandFanCard fan)
        {
            if (!FanGateQuiet()) return false;
            var card = FanCardOf(fan);
            if (card == null) return false;
            if (CardCastableNow(card)) return true;
            var data = (card as CardWrapper)?.GetData();
            if (data == null) return false;
            // 充能（2026-11-05 系统动作化；2026-11-06 opt-in）：卡面声明 chargeAbility 的手牌卡——
            // 本回合未充过（一张卡一回合一次）且还有可充颜色（卡具有且未充满）即亮；
            // 颜色在结算期经引擎反问选。
            if (card.GetZone() == Zone.Hand && data.ChargeAbility && !GameActions.ChargeUsedThisTurn(Core, card))
            {
                var info = GameActions.GetHandCardChargeInfo(Core, P1, card);
                if (info.WindowOpen && info.RemainingByColor.Count > 0) return true;
            }
            return false;
        }

        /// <summary>卡当前可发动（无主阶段闸——闸由调用方外置）；法术=可付即可，永久物另需空格。
        /// 场地卡（2026-10-29 P8）不占单位容量：落己方场地格恒可入（旧场地在位=顶替），免空格判定。</summary>
        private bool CardCastableNow(Card card)
        {
            if (card == null || Core == null || P1 == null) return false;
            var data = (card as CardWrapper)?.GetData();
            if (data == null) return false;
            int modes = Math.Max(1, CostDerivationService.GetModeCount(data));
            bool ok = false;
            for (int i = 0; i < modes && !ok; i++)
                ok = GameActions.CanAffordCast(Core, P1, card, i);
            if (!ok) return false;
            if (data.Supertype != Cardtype.Spell)
            {
                if (data.IsField) return true; // 场地卡：场地格每方一张恒可落
                int n = 0;
                foreach (var c in Core.ZoneManager.GetCards(P1, Zone.Battlefield))
                    if (c == null || !c.IsFieldCard()) n++;
                return n < BoardLayout.UnitCells(0).Count;
            }
            return true;
        }

        /// <summary>己方手牌可发动流光评估（随 RefreshFans 对账刷新；非主阶段全熄）。
        /// 2026-10-19 分支金圈：可付且卡上有限分支（Gate）条件已达成=金色光圈，
        /// 区别于常规「费用可用」绿（教学二·命运一抽定案）。</summary>
        private void EvaluateFanPlayability()
        {
            if (_selfFan == null || Core == null || P1 == null) return;
            bool gate = FanGateQuiet();
            foreach (var card in Core.ZoneManager.GetCards(P1, Zone.Hand))
            {
                bool castable = gate && CardCastableNow(card);
                _selfFan.SetPlayable(card.RuntimeId, castable, castable && GateArmedNow(card));
            }

            // 牌库顶虚拟手牌（2026-10-29 阶段3b）：发动流光随对账刷新（打出后顶牌换新即重评）
            var deckTop = TopDeckVirtualCard();
            if (deckTop != null)
            {
                bool castable = gate && CardCastableNow(deckTop);
                _selfFan.SetPlayable(deckTop.RuntimeId, castable, castable && GateArmedNow(deckTop));
            }
        }

        /// <summary>卡上有限分支（settle=1 局面门）当前是否达成（金圈判据，2026-10-19 教学二）：
        /// 扫主动效果原子的槽级 Gate 载荷，用局面评估器预评（CastCard=该卡，与结算期
        /// ApplySituationGateAsync 的 HostCard 同口径）；无门/未知条件恒 false。局面门族
        /// 条件只读 context 局面（outcome 不参与），传全新 EffectOutcome 即可。</summary>
        private bool GateArmedNow(Card card)
        {
            var data = (card as CardWrapper)?.GetData();
            if (data?.Effects == null) return false;
            foreach (var fx in data.Effects)
            {
                if (fx?.Steps == null) continue;
                foreach (var step in fx.Steps)
                {
                    var b = step?.atomic?.branch;
                    if (b == null || b.settle != 1 || string.IsNullOrEmpty(b.gateId)) continue;
                    var ctx = new EffectExecutionContext
                    {
                        Controller = P1,
                        Source = card,
                        CastCard = card,
                        ZoneManager = Core.ZoneManager,
                        ElementPool = Core.ElementPool,
                        SimulatedPreview = true, // 预评口径：按「这张打出后」评估（首卡计 0、手牌剩 1 等）
                    };
                    if (BranchConditionEvaluator.Evaluate(b.gateId, new EffectOutcome(),
                            b.condParam, b.condStr, ctx))
                        return true;
                }
            }
            return false;
        }

        /// <summary>常驻考验重置按钮逐帧门控（2026-10-19）：教学局非 Boss 课常显（终局隐藏）；
        /// 可点=自由操作期（我方回合主阶段+无选择在身+无介绍卡在屏）且表现播毕（VisualsBusy）。</summary>
        private void TickTutorialResetButton()
        {
            if (_tutorialReset == null) return;
            bool visible = BattleEntry.Mode == BattleMode.Tutorial
                && BattleEntry.TutorialId != TutorialProgressManager.LunzhuanId
                && !_gameEnded;
            if (_tutorialReset.gameObject.activeSelf != visible)
                _tutorialReset.gameObject.SetActive(visible);
            if (!visible) return;
            bool free = FanGateQuiet() && !IsTargetSelecting()
                && _tutorial != null && !_tutorial.IntroPanelOpen
                && (_field == null || !_field.VisualsBusy);
            if (_tutorialReset.interactable != free)
                _tutorialReset.interactable = free;
        }

        private void OnTutorialResetClicked()
        {
            _tutorial?.TryResetTrial();
        }

        /// <summary>T 键测试跳段（2026-10-21 用户要求·临时调试口）：教学局按 T=直接跳到最终
        /// 考验段（教学二=段 8 命运一抽考验）。门控=重置按钮口径（玩家主阶段+无选择在身+无介绍
        /// 卡在屏）另加栈收口+回放导演闲——防结算/回放中途强跳撕裂换场重铺；非教学局零开销直返。</summary>
        private void PollTutorialJumpKey()
        {
            if (BattleEntry.Mode != BattleMode.Tutorial || _gameEnded) return;
            if (!Input.GetKeyDown(KeyCode.T)) return;
            if (!FanGateQuiet() || IsTargetSelecting()) return;
            if (_tutorial == null || _tutorial.IntroPanelOpen) return;
            if (Core != null && Core.StackEngine != null
                && (!Core.StackEngine.IsEmpty || Core.StackEngine.IsResolving)) return;
            if (_field != null && _field.VisualsBusy) return;
            if (_director != null && _director.Busy) return;
            _tutorial.JumpToFinalTrial();
        }

        /// <summary>教学自由步轮询（2026-10-19 教学二段一改版）：fieldclear 步在位时逐帧查
        /// 对手场上生物——清零即完成推进（下一 intro 解说卡弹出）。luckuse/deathtoll 自由步同口
        /// 接入（2026-10-20）。栈收口（防结算中途瞬态空场）且表现播毕（死亡退场演完再弹卡）
        /// 才查；非自由步在位零开销直返。
        /// 终局放行（2026-10-20 段六元素充盈）：等待步的完成击杀恰是预设剧本（充盈兽 3 伤=AI 3 血
        /// 一发终结）——IsScriptedContinuation 在位时终局弹窗已跳过、全靠本轮询推进到换场步续接，
        /// 此时不可因 _gameEnded 冻结（否则死锁在终局画面）；非续接终局（真打完）照旧冻结。</summary>
        private void PollFieldClearStep()
        {
            if (BattleEntry.Mode != BattleMode.Tutorial || !TutorialGuide.Active) return;
            var stepType = TutorialGuide.Current?.type;
            if (stepType != "fieldclear" && stepType != "luckuse" && stepType != "deathtoll") return;
            if (_gameEnded && (_tutorial == null || !_tutorial.IsScriptedContinuation)) return;
            if (Core == null || Core.StackEngine == null
                || !Core.StackEngine.IsEmpty || Core.StackEngine.IsResolving) return;
            if (_field != null && _field.VisualsBusy) return;
            // 回放导演在途（2026-10-20 运势段被截修复）：步间停顿/末步等待期视觉门瞬空——
            // 演出载荷还在导演手里没放出，栈空+门空双双穿透，步进早于演出收口
            if (_director != null && _director.Busy) return;
            if (stepType == "fieldclear") TutorialGuide.PollFieldClear();
            else if (stepType == "deathtoll") TutorialGuide.PollDeathToll(); // 死亡计数段自由步（2026-10-20）：计数满 6 触发奖励即推进
            else TutorialGuide.PollLuckUse(); // 运势段等待步（2026-10-20；2026-11-08 奖励效果化改版）：六只运势单位 LuckRolledEvent 全掷过（准备阶段自动掷骰）才推进
        }

        /// <summary>选格/出牌流程收尾：恢复手牌扇+全量对账（按钮条件/流光随刷新重评估）。</summary>
        private void RestoreFan()
        {
            _selfFan?.SetVisible(true);
            RefreshFans();
            RefreshField();
        }

        /// <summary>「放置」点击（2026-10-14 定案）：藏手牌→放地箭头（候选=卡可锁色的落格：
        /// 已有色=色组现格、新色=BoardState 落格预览；高亮按元素色）→确认入池（LandFinal 同源）；
        /// 同格多候选新色=弹现选色窗消歧；取消（右键/ESC/无效格）恢复手牌。</summary>
        private void OnFanPlaceLand(HandFanCard fan)
        {
            if (NetActive) { NetOnFanPlaceLand(fan); return; } // 网络局：锁色选择+intent 上行
            if (!GateFanMyMain()) return;
            var card = FanCardOf(fan);
            if (card == null) return;
            if (Core?.ElementPool == null || !ElementPoolSystem.CanServeAsLand(card) || !Core.ElementPool.CanAddLand(P1))
                return;

            var choices = ElementPoolSystem.GetLandColorChoices(card);
            if (choices.Count == 0) { _toast.Show("该卡无可锁定产色（无费用元素/纯黑白不可作地牌）"); return; }

            var view = BattleView.FromRuntime(SerializableRuntimeCardState.FromCard(card), card, false);
            var board = _ctrl?.Board;
            if (board == null) return;

            // 候选格（cell → 候选色集）：确定性预览——与入池后 AssignLandCells 落格一致
            var cellColors = new Dictionary<(int x, int z), List<ManaType>>();
            var options = new List<BattleCellPicker.CellOption>();
            foreach (var color in choices)
            {
                var cell = ExistingLandCellOf(color) ?? board.PreviewLandCell(P1, color);
                if (cell == null) continue;
                if (!cellColors.TryGetValue(cell.Value, out var list))
                {
                    list = new List<ManaType>();
                    cellColors[cell.Value] = list;
                    options.Add(new BattleCellPicker.CellOption { X = cell.Value.x, Z = cell.Value.z, ColorIndex = (int)color });
                }
                list.Add(color);
            }
            if (options.Count == 0) { _toast.Show("地牌行无空位（颜色数超格）"); return; }

            _choreo?.CancelTargeting();
            _selfFan?.SetVisible(false);
            var hero = BoardLayout.CharacterCell(0);
            _cellPicker.Begin(BattleBoardGrid.WorldPositionOf(hero.x, hero.z), options, opt =>
            {
                RestoreFan();
                var colors = cellColors[(opt.X, opt.Z)];
                if (colors.Count == 1)
                {
                    LandFinal(view, colors[0]);
                    return;
                }
                PickLandColorAsync(view, colors).Forget(); // 同格多候选新色：Region 卡牌列表消歧（2026-10-16 弹窗退役）
            }, RestoreFan);
        }

        /// <summary>色 → 现有地牌色组落格（BoardState 反查；无该色组=null）。</summary>
        private (int x, int z)? ExistingLandCellOf(ManaType color)
        {
            var board = _ctrl?.Board;
            if (board == null || Core?.ElementPool == null) return null;
            foreach (var pc in Core.ElementPool.GetPooledCards(P1))
                if (pc?.SourceCard != null && pc.LockedColor == color && board.TryGetCell(pc.SourceCard, out int x, out int z))
                    return (x, z);
            return null;
        }

        /// <summary>「使用」点击（2026-10-14 定案；2026-11-01 抉择上卡+充能上卡·SpUse 退役）：
        /// 特殊行选中=充能（ChargeCardAsync——2026-11-05 系统动作化：宣言入栈，结算期反问选色）；
        /// 普通=法术按卡面效果行选中模式直入声明流（模式窗退役）、永久物=模式窗（多模式）→选格、
        /// 生物=直入召唤。再点选中特殊行取消即回本链。</summary>
        private void OnFanUse(HandFanCard fan)
        {
            if (NetActive) { NetOnFanUse(fan); return; } // 网络局：intent 上行（目标结算期反问）
            if (!GateFanMyMain()) return;
            var card = FanCardOf(fan);
            if (card == null) return;
            var data = (card as CardWrapper)?.GetData();
            if (data == null) return;
            var view = BattleView.FromRuntime(SerializableRuntimeCardState.FromCard(card), card, false);

            // 充能上卡（2026-11-01）：卡面特殊行选中时 Use 钮承载对应入口（SpUse 退役）
            var spKind = fan.SelectedSpKind;
            if (spKind == SpEffectKind.Charge)
            {
                ChargeCardAsync(card); // 充能=速度0系统动作宣入（结算期反问元素类别）
                return;
            }

            if (data.Supertype == Cardtype.Spell)
            {
                // 2026-11-01 抉择上卡改版：多主动=卡面效果行直选（HandFanCard.SelectedEffectMode，
                // 默认第一行），不再弹「选择要发动的效果」；单主动恒 0 同链。
                // 费用预检保持（防选完位置目标才被引擎拒），法术恒自动落位（B4）。
                int mode = GetModeCount(view) >= 2
                    ? Mathf.Clamp(fan.SelectedEffectMode, 0, GetModeCount(view) - 1)
                    : 0;
                if (Core == null || !Core.ElementPool.CanPayCostWithAutoTap(
                        GameActions.GetCardCost(card, mode), P1))
                {
                    _toast.Show("费用不足——该作用不可发动");
                    return;
                }
                BeginSpellCastAutoPlace(view, card, mode);
                return;
            }
            if (CostDerivationService.GetModeCount(data) >= 2)
            {
                PromptModeThenPickCell(view, card).Forget();
                return;
            }
            SummonFlow(view, card, 0);
        }

        /// <summary>召唤出牌（验费→选格→PlayCardFinal）。
        /// 按 CanAffordCast 全口径验费（含声明承诺）。</summary>
        private void SummonFlow(BattleCardView view, Card card, int modeIndex)
        {
            if (Core == null || !GameActions.CanAffordCast(Core, P1, card, modeIndex))
            {
                _toast.Show("费用不足——该分支不可发动");
                return;
            }
            BeginUseCellPicking(view, card, modeIndex);
        }

        /// <summary>充能（2026-11-05 系统动作化改版）：把充能作为速度0系统动作宣入栈（仿攻击宣言，
        /// 对手获响应窗口）——**发动期不选色**：颜色在结算期经引擎反问下拉选择（只列此卡具有
        /// 且未充满的颜色；本地=Select 窗下拉、网络=SelectRequest、AI/无头/超时自动取最大余量色）。
        /// 确定后免费 +1 层同色费用降低指示物并展示此卡（不支付元素）；卡留手牌——净费随 RefreshLocal 回写。</summary>
        private void ChargeCardAsync(Card card)
        {
            if (GameActions.DeclareCharge(Core, P1, card, out var reason))
                _toast.Show("充能已宣入（响应窗口后结算——届时选择元素类别）");
            else
                _toast.Show($"充能失败：{reason}");
            RefreshLocal();
        }

        /// <summary>模式窗变体（永久物出牌）：选完模式进入召唤出牌（恒普通召唤）→选格——文案口径同模式窗旧链，
        /// 验费用 CanAffordCast 全口径（含声明承诺；代价补偿不预计算——2026-10-16 恢复旧序）。
        /// 2026-10-16 弹窗退役：选项=Region 面板卡牌列表。</summary>
        private async UniTaskVoid PromptModeThenPickCell(BattleCardView view, Card card)
        {
            var data = DataOf(view);
            var labels = FindChoiceLabels(data);
            int modeCount = Math.Max(2, CostDerivationService.GetModeCount(data));
            var options = new List<string>();
            for (int i = 0; i < modeCount; i++)
            {
                string name = i < labels.Count && !string.IsNullOrEmpty(labels[i]) ? labels[i] : $"模式 {i + 1}";
                options.Add($"{name}（费用 {CostText(LocalCostOf(view, i))}）");
            }
            int? picked = await PickRegionOptionAsync("选择模式", options);
            if (picked == null) return;
            int idx = picked.Value;
            SummonFlow(view, card, idx);
        }

        /// <summary>永久物出牌选格：候选=己方单位区空格（占用=BoardState 现值，含施放占格卡）；
        /// 箭头自角色格拉出（原发动格锚随发动格拆除退役，2026-10-15）。
        /// 确认→登记选格单（PlayCardFinal 声明成功即转 BoardState 钉——施放类型图标与入场转正同钉单源）
        /// →PlayCardFinal（需目标的卡在链内弹目标窗）。取消恢复手牌。</summary>
        private void BeginUseCellPicking(BattleCardView view, Card card, int modeIndex)
        {
            var board = _ctrl?.Board;
            if (board == null) return;

            // 场地卡（2026-10-29 P8）：目标格恒为己方场地格 (0,2)——不弹选格；格上有旧场地=顶替入墓
            var playData = (card as CardWrapper)?.GetData();
            if (playData != null && playData.IsField)
            {
                var fc = BoardLayout.FieldCell(0);
                _choreo?.CancelTargeting();
                _pendingPins[card.RuntimeId] = fc;
                bool replacing = board.FieldOccupied(P1);
                Debug.Log($"[HandPlay] 场地卡直接落位：{view.Name} → ({fc.x},{fc.z}){(replacing ? "（顶替旧场地入墓）" : "")}");
                _toast.Show(replacing ? "场地就位——将顶替己方现有场地（旧场地入墓）" : "场地就位（己方场地格）");
                PlayCardFinal(view, null, modeIndex);
                return;
            }

            var options = new List<BattleCellPicker.CellOption>();
            foreach (var cell in BoardLayout.UnitCells(0))
                if (board.CardAt(cell.x, cell.z) == null)
                    options.Add(new BattleCellPicker.CellOption { X = cell.x, Z = cell.z, ColorIndex = -1 });
            if (options.Count == 0) { _toast.Show("单位区已满——无法打出"); return; }

            _choreo?.CancelTargeting();
            _selfFan?.SetVisible(false);
            var origin = BoardLayout.CharacterCell(0);
            _cellPicker.Begin(BattleBoardGrid.WorldPositionOf(origin.x, origin.z), options, opt =>
            {
                RestoreFan();
                _pendingPins[card.RuntimeId] = (opt.X, opt.Z);
                Debug.Log($"[HandPlay] 永久物出牌选格：{view.Name} → ({opt.X},{opt.Z})（声明成功即钉）");
                PlayCardFinal(view, null, modeIndex);
            }, RestoreFan);
        }

        /// <summary>移动原子选格（2026-10-31 场面轴3c MoveUnit）：CellSelectionService 反问桥实现——
        /// 与永久物出牌选格同交互（BattleCellPicker 箭头+候选空格），箭头自待移动单位当前格拉出
        ///（未落格回落角色格）。返回候选表索引（-1=取消）；候选/合法性由 MoveUnitRules 单源筛得。</summary>
        private async UniTask<int> PickMoveDestinationAsync(Card unit, IReadOnlyList<(int x, int z)> cells, string title)
        {
            var options = new List<BattleCellPicker.CellOption>();
            foreach (var c in cells)
                options.Add(new BattleCellPicker.CellOption { X = c.x, Z = c.z, ColorIndex = -1 });

            _choreo?.CancelTargeting();
            _selfFan?.SetVisible(false);
            var origin = BoardLayout.CharacterCell(0);
            if (unit != null && _ctrl?.Board != null && _ctrl.Board.TryGetCell(unit, out int ux, out int uz))
                origin = (ux, uz);
            if (!string.IsNullOrEmpty(title)) _toast.Show(title);

            var done = new UniTaskCompletionSource<int>();
            _cellPicker.Begin(BattleBoardGrid.WorldPositionOf(origin.x, origin.z), options, opt =>
            {
                RestoreFan();
                int idx = -1;
                for (int i = 0; i < cells.Count; i++)
                    if (cells[i].x == opt.X && cells[i].z == opt.Z) { idx = i; break; }
                done.TrySetResult(idx);
            }, () => { RestoreFan(); done.TrySetResult(-1); });
            return await done.Task;
        }

        /// <summary>点我方地牌（2026-10-11 现取现用 → 2026-10-14 按色叠格）：全自动结算口径不变——
        /// 付费时系统按账单自动横置（强制系统动作，无速度交互）。2026-10-16 lbl-toast 定案：
        /// 地牌格介绍属场面信息=hover-info 单源（LandCellInfo 悬停即有，含逐色可用/上限与地牌上限行），
        /// 点击不再写 toast（旧汇总提示会覆盖选择/响应提示位——牛头不对马嘴），留日志取证。</summary>
        private void OnClickMyLand(BattleCardView card)
        {
            if (card == null) return;
            var toks = new List<string>();
            var pv = _view?.Self;
            foreach (var g in (_view?.SelfLands ?? new List<BattleCardView>())
                     .Where(c => c.X == card.X && c.Z == card.Z && c.Runtime != null)
                     .GroupBy(c => c.Runtime.LockedColor))
            {
                int untapped = g.Count(c => !c.IsTapped);
                int wallet = pv != null && g.Key >= 0 && g.Key < pv.WalletByColor.Length ? pv.WalletByColor[g.Key] : 0;
                string color = g.Key >= 0 ? BattleView.ManaZh((ManaType)g.Key) : "?";
                toks.Add($"{color}{untapped + wallet}/{g.Count()}");
            }
            Debug.Log($"[地牌格] {card.Name} 点击：{(toks.Count > 0 ? string.Join("  ", toks) : "暂无地牌")}"
                      + "（逐色可用/上限详情=悬停地牌格 hover-info）");
        }

        /// <summary>点我方单位：3D 寻路箭头选攻击目标（2026-10-13 攻击宣言改版）→ 攻击宣言。
        /// 悬停对方单位/角色格实时画寻路路径（BFS 最短路），点目标=路径固定并回传目标；
        /// 确认后下游宣言链路不变（本地 DeclareAttack+结算 / 网络 intent）。
        /// 取代旧 ShowPickOverlay 列表弹窗（overlay 节点缺失时空转的依赖一并解除）。</summary>
        private void OnClickMyUnit(BattleCardView card)
        {
            if (NetActive) { NetUnitClick(card); return; } // 网络局：守卫/攻击 intent（列表选目标）
            if (!GateMyMain()) return;
            if (card.IsTapped) { _toast.Show("该单位已横置"); return; }
            if (card.CoreCard is Card unitCard && !unitCard.HasAttackAbility())
            { _toast.Show("该单位不具备攻击能力"); return; } // 无攻资格预检（2026-11-06 opt-in）：点击即提示，不进选目标
            if (card.Runtime != null && card.Runtime.Power <= 0) { _toast.Show("该单位攻击力为 0"); return; }
            if (card.X < 0 || card.Z < 0 || _choreo == null)
            {
                _toast.Show("单位位置未知，无法攻击");
                return;
            }

            _choreo.BeginTargeting(card, target =>
            {
                Entity captured = target != null && target.CoreCard != null ? (Entity)target.CoreCard : P2;
                string attackReason = null; // 预置 null：captured 短路时 out 不赋值（CS0165）
                if (captured == null || !_ctrl.DeclareAttack(P1, card.CoreCard, captured, out attackReason))
                {
                    // 引擎真因（2026-11-03 失败提示细化；教学闸文案在 reason 链首位——旧 AttackFailReason 复算退役）
                    _toast.Show(attackReason ?? "无法对该目标攻击");
                    RefreshLocal();
                    return;
                }
                _ctrl.SettleResponseWindow().Forget();
                RefreshLocal();
            });
        }

    }
}
