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
        private bool PlayCardFinal(BattleCardView card, List<Entity> targets, int modeIndex,
            List<int> dynamicCounts = null)
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
                modeIndex, null, dynamicCounts, out reject);
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

        // LandFlow（手牌直点放地的旧选色弹窗链）已随 2026-10-16 弹窗退役整删；2026-11-15 地牌改版
        // 再删落格箭头/Region 选色：放地现役入口=OnFanPlaceLand（按钮直放）+ 拖拽整块落点，均不选色。

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
                var decl = await GameActions.CollectSpellDeclarationTargetsAsync(card, P1, modeIndex);

                if (decl == null)
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
                if (!PlayCardFinal(view, decl.Targets, modeIndex, decl.DynamicCounts))
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

        // PickLandColorAsync（放地选色）已随 2026-11-15 地牌改版整删：放置不再选色——
        // 入池即快照可产集合，产色在横置时选定（自动规划/手动弹窗）。

        /// <summary>放地终态（2026-11-15 地牌改版）：AddToElementPool 不传色——拒绝原因引擎直读
        ///（2026-11-03 失败提示细化沿用）。</summary>
        private void LandFinal(BattleCardView card)
        {
            string landReason = null; // 预置 null：Core 为空短路时 out 不赋值（CS0165）
            bool ok = Core != null && GameActions.AddToElementPool(Core, P1, card.CoreCard, out landReason);
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
        /// 法术多主动（2026-11-01 抉择上卡）：使用钮按卡面效果行选中模式直入施放
        ///（选中行不可付时点击 toast 拒——亮钮条件仍是任一模式可付）。</summary>
        private bool FanCanUse(HandFanCard fan)
        {
            if (!FanGateQuiet()) return false;
            var card = FanCardOf(fan);
            if (card == null) return false;
            return CardCastableNow(card);
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

        /// <summary>「放置」点击（2026-11-15 地牌改版）：**不再选色/选格**——直入 LandFinal
        ///（引擎快照可产集合，落格由 AssignLandCells 按展示色主色自动分配）；资格/余位闸前置同旧。</summary>
        private void OnFanPlaceLand(HandFanCard fan)
        {
            if (NetActive) { NetOnFanPlaceLand(fan); return; } // 网络局：intent 上行（ModeIndex=-1）
            if (!GateFanMyMain()) return;
            var card = FanCardOf(fan);
            if (card == null) return;
            if (Core?.ElementPool == null || !ElementPoolSystem.CanServeAsLand(card) || !Core.ElementPool.CanAddLand(P1))
                return;

            if (ElementPoolSystem.GetLandColorChoices(card).Count == 0)
            { _toast.Show("该卡无元素费用，不能作为地牌"); return; }

            var view = BattleView.FromRuntime(SerializableRuntimeCardState.FromCard(card), card, false);
            _selfFan?.SetVisible(false);
            LandFinal(view);
        }

        /// <summary>「使用」点击（2026-10-14 定案；2026-11-01 抉择上卡·SpUse 退役）：
        /// 法术按卡面效果行选中模式直入声明流（模式窗退役）、永久物=模式窗（多模式）→选格、
        /// 生物=直入召唤。</summary>
        private void OnFanUse(HandFanCard fan)
        {
            if (NetActive) { NetOnFanUse(fan); return; } // 网络局：intent 上行（目标结算期反问）
            if (!GateFanMyMain()) return;
            var card = FanCardOf(fan);
            if (card == null) return;
            var data = (card as CardWrapper)?.GetData();
            if (data == null) return;
            var view = BattleView.FromRuntime(SerializableRuntimeCardState.FromCard(card), card, false);

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

        /// <summary>点我方地牌（2026-10-11 现取现用 → 2026-11-15 地牌改版）：日志取证改**整区**
        /// 逐色「可用/可产」汇总（CanProduceColor 位掩码——多色地计入各色；不再按落格/展示色分组）。
        /// 付费时系统按账单自动横置（强制系统动作，无速度交互）。2026-10-16 lbl-toast 定案沿用：
        /// 点击不写 toast，详情=悬停 hover-info。</summary>
        private void OnClickMyLand(BattleCardView card)
        {
            if (card == null) return;
            var toks = new List<string>();
            var pv = _view?.Self;
            var lands = _view?.SelfLands ?? new List<BattleCardView>();
            foreach (var element in BoardLayout.LandCellElementOrder)
            {
                int total = lands.Count(c => c != null && c.Runtime != null && c.CanProduceColor(element));
                if (total == 0) continue;
                int untapped = lands.Count(c => c != null && c.Runtime != null && c.CanProduceColor(element) && !c.IsTapped);
                int wallet = pv != null && (int)element < pv.WalletByColor.Length ? pv.WalletByColor[(int)element] : 0;
                toks.Add($"{BattleView.ManaZh(element)}{untapped + wallet}/{total}");
            }
            Debug.Log($"[地牌区] {card.Name} 点击：{(toks.Count > 0 ? string.Join("  ", toks) : "暂无地牌")}"
                      + "（逐色可用/可产详情=悬停地牌长条 hover-info）");
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

        // ======================================== 手牌拖拽（2026-11-15 全拖拽改版·阶段H 视觉改版） ========================================
        // 卡不跟手（起手即隐藏）——引导改由 3D 攻击箭头（BattleAttackArrow，与生物攻击选目标同款）
        // + 可放置区域波标（BattleTileFx 波=候选期标志逐帧重应用，收口清波）承担。骨架照
        // BattleCellPicker.LoopAsync：每帧 HighlightCandidates + 箭头随指针 + 合法判定绿/黄。

        private readonly HandDragArrowView _dragArrow = new HandDragArrowView(); // 2D 箭头（阶段H 退役：拖拽路径不再调用，类与 prefab 节点保留）
        private readonly List<RaycastResult> _dragUiHits = new List<RaycastResult>();
        private BattleAttackArrow _dragArrow3D;     // 3D 拖拽箭头（BattleAttackArrow 同款虚线弧）
        private Vector3 _dragOrigin;                 // 箭头起点（起手时卡屏幕位投到地面）
        private int _dragWaveX = -1, _dragWaveZ = -1; // 当前点亮的波格（-1=无；换格/收口时清）

        private enum DragDropKind { Field, Land, Cancel }

        /// <summary>拖拽可操作闸（2026-11-15 用户实测①，静默口径）：己方主阶段 ∨ 持有优先权
        /// （获得连锁=可响应）。对手回合未持有优先权时拖拽起手即取消、拖拽中途失权自动取消。
        /// 点地牌产色同闸在 GateTapLand（toast 口径）——两处同源时点判定。</summary>
        private bool DragInteractionAllowed()
        {
            if (_gameEnded || _ctrl == null || Core == null || P1 == null) return false;
            if (FanGateQuiet()) return true; // 己方主阶段（静默版）
            return Core.StackEngine != null && Core.StackEngine.CurrentPriorityHolder == P1; // 持有优先权（连锁响应权）
        }

        /// <summary>拖拽起手（HandFanView.BeginDrag→OnDragBegin 钩子；2026-11-15 用户实测②）：
        /// 3D 箭头起点=**己方角色格**（与生物攻击选目标同锚——放地/召唤选格 picker 同源）。</summary>
        private void HandleFanDragBegin(HandFanCard fan, Vector2 screenPoint)
        {
            if (_dragArrow3D == null) _dragArrow3D = new BattleAttackArrow();
            var hero = BoardLayout.CharacterCell(0);
            _dragOrigin = BattleBoardGrid.WorldPositionOf(hero.x, hero.z); // 起点锚=角色格（picker 同款）
        }

        /// <summary>PC 拖拽选择态输入裁决（PollFieldPointer 拖拽分支逐帧调；2026-11-15 用户实测③）：
        /// PC=左键按下确认落点（走 HandleFanDragEnd 三分流）、右键/ESC 取消回手；触摸在轨时让位
        /// （触摸=抬起裁决，UGUI OnEndDrag 承担）。另查拖拽中途失权（回合切换）自动取消。</summary>
        private void PollDragConfirmOrCancel()
        {
            if (_selfFan == null || !_selfFan.Dragging) return;
            if (!DragInteractionAllowed()) { CancelFanDrag(); return; } // 失权（对手回合轮转）——自动取消
            if (BattlePointer.TouchTracked) return; // 触摸在轨：抬起裁决（OnEndDrag），不走鼠标口
            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
            {
                CancelFanDrag();
                return;
            }
            if (BattlePointer.ConfirmPressed) // 左键按下=确认（HandleFanDragEnd 自身首行吞同帧点击）
            {
                var fanCard = _selfFan.DraggingCard;
                if (fanCard != null) HandleFanDragEnd(fanCard, BattlePointer.AimPoint);
            }
        }

        /// <summary>取消拖拽（右键/ESC/失权）：收视觉+清态回手（不走向落点裁决）。</summary>
        private void CancelFanDrag()
        {
            HideDragVisual();
            _selfFan?.CancelDrag();
        }

        /// <summary>拖拽视觉逐帧（PollFieldPointer 拖拽让路分支调；阶段H=3D 箭头+波标）：
        /// 合法=ValidSkin 绿、非法=PlaceSkin 黄（与 BattleCellPicker 一致）。
        /// 波=悬停命中合法格点亮（单位空格=SetUnitGlow 蓝波；地牌色带=SetCellWave 元素色）；
        /// 法术不点候选格（自动落位，指针在战场即绿）；离开合法区清波。对账 ClearAll 会清波——
        /// 合法格每帧重应用（picker 同款）。带指针位口（UpdateDragVisualAt）供自动测试模拟悬停。</summary>
        private void UpdateDragVisual() => UpdateDragVisualAt(BattlePointer.AimPoint);

        internal void UpdateDragVisualAt(Vector2 sp)
        {
            var fanCard = _selfFan != null && _selfFan.Dragging ? _selfFan.DraggingCard : null;
            if (fanCard == null) { HideDragVisual(); return; }
            var card = FanCardOf(fanCard);
            var data = card != null ? (card as CardWrapper)?.GetData() : null;
            bool isSpell = data != null && data.Supertype == Cardtype.Spell;

            var kind = ClassifyDragDrop(sp, out int cx, out int cz);

            bool valid = false;
            int waveX = -1, waveZ = -1;
            if (kind == DragDropKind.Land)
            {
                valid = true;                       // 悬停地牌区=几何合法（资格闸在松手 DragPlaceLand）
                var any = BoardLayout.AnyElementCell(0); // 地牌行 tile 已随改版删除——放地候选波
                waveX = any.x; waveZ = any.z;            // 点亮临时元素格（元素色=该卡展示主色）
            }
            else if (kind == DragDropKind.Field && !isSpell)
            {
                var board = _ctrl?.Board;
                bool isUnitCell = false;
                if (cx >= 0)
                    foreach (var u in BoardLayout.UnitCells(0))
                        if (u.x == cx && u.z == cz) { isUnitCell = true; break; }
                valid = isUnitCell && board != null && board.CardAt(cx, cz) == null; // 己方空单位格=合法
                if (valid) { waveX = cx; waveZ = cz; } // 波=命中空格（蓝波 SetUnitGlow）
            }
            else if (kind == DragDropKind.Field) valid = true;    // 法术：指针在战场即合法（自动落位，无波）

            // 波标：换格先清旧（波=候选期标志，非法/离开=无波）
            if (waveX != _dragWaveX || waveZ != _dragWaveZ)
            {
                if (_dragWaveX >= 0) BattleTileFx.ClearUnitGlow(_dragWaveX, _dragWaveZ);
                _dragWaveX = waveX; _dragWaveZ = waveZ;
            }
            if (_dragWaveX >= 0)
            {
                if (kind == DragDropKind.Land)
                {
                    // 地牌改版：无色带可反推——波色=该卡可产集合首位主色（与入池展示色一致）
                    var choices = card != null ? ElementPoolSystem.GetLandColorChoices(card) : null;
                    var main = choices != null && choices.Count > 0 ? choices[0] : ManaType.Gray;
                    BattleTileFx.SetCellWave(_dragWaveX, _dragWaveZ, BattleTileFx.ElementColorOf((int)main));
                }
                else BattleTileFx.SetUnitGlow(_dragWaveX, _dragWaveZ);
            }

            // 箭头：地平面跟随指针；合法绿 / 非法黄（PlaceSkin）；地面取不到=收箭头
            var cam = BattleStageDirector.WorldCam;
            if (cam != null && CombatChoreographer.MouseGroundPoint(cam, sp, out var visPt))
                _dragArrow3D.Show(_dragOrigin, visPt, valid
                    ? BattleAttackArrow.ValidSkin : BattleAttackArrow.PlaceSkin);
            else if (_dragArrow3D != null) _dragArrow3D.Hide();
        }

        /// <summary>拖拽视觉收口：收 3D 箭头+清残留波格（松手/取消路径；HideDragVisual 恒幂等）。</summary>
        private void HideDragVisual()
        {
            if (_dragArrow3D != null) _dragArrow3D.Hide();
            if (_dragWaveX >= 0)
            {
                BattleTileFx.ClearUnitGlow(_dragWaveX, _dragWaveZ);
                _dragWaveX = _dragWaveZ = -1;
            }
        }

        /// <summary>拖拽落点分类（逐帧箭头与抬手裁决共用，指针屏幕位口径）：3D 射线——己方
        /// land-quad/land-strip/地牌行 tile=放地（2026-11-15 地牌改版：整块任意落点=放地，不再反推色带）；
        /// 其余 tile-*/hero-*/unit-*=战场（发动）；未命中/Env 装饰/对方面片长条/屏边 80px=取消。</summary>
        private DragDropKind ClassifyDragDrop(Vector2 sp, out int cx, out int cz)
        {
            cx = -1; cz = -1;
            if (sp.x < 80f || sp.y < 80f || sp.x > Screen.width - 80f || sp.y > Screen.height - 80f)
                return DragDropKind.Cancel; // 屏边 80px 取消带（定稿）
            var cam = BattleStageDirector.WorldCam;
            if (cam == null) return DragDropKind.Cancel;
            if (!Physics.Raycast(cam.ScreenPointToRay(sp), out var hit, 300f)) return DragDropKind.Cancel;
            var hn = hit.collider != null ? hit.collider.gameObject.name : "";
            if (LandStripFx.IsStripHit(hn, out int stripOwner))
                return stripOwner == 0 ? DragDropKind.Land : DragDropKind.Cancel; // 对方长条不可放地
            if (LandStripFx.TryParseHit(hn, out uint quadRid))
                return IsSelfLandQuad(quadRid) ? DragDropKind.Land : DragDropKind.Cancel; // 对方面片=取消
            if (hn.StartsWith("tile-", System.StringComparison.Ordinal)
                && BattleHoverInfoProvider.TryParseTile(hn, out cx, out cz))
            {
                if (BoardLayout.RoleOf(cx, cz) == CellRole.Land && BoardLayout.OwnerOf(cx, cz) == 0)
                    return DragDropKind.Land;
                return DragDropKind.Field;
            }
            if (hn.StartsWith("hero-", System.StringComparison.Ordinal)
                || hn.StartsWith("unit-", System.StringComparison.Ordinal))
                return DragDropKind.Field;
            return DragDropKind.Cancel; // Env 装饰（side-tree/crypt/pit/flat-ground 等）
        }

        /// <summary>面片 rid 是否己方地牌（放地色带反推用）。</summary>
        private bool IsSelfLandQuad(uint rid)
        {
            var pool = Core?.ElementPool;
            if (pool == null || P1 == null) return false;
            foreach (var pc in pool.GetPooledCards(P1))
                if (pc?.SourceCard != null && pc.SourceCard.RuntimeId == rid) return true;
            return false;
        }

        // LandColorAtWorldX（世界 x 反推色带）已随 2026-11-15 地牌改版整删：放地不再按色带锁色。

        /// <summary>拖拽抬手（HandFanView.EndDrag→OnDragEnd 钩子）：吞同帧棋盘点击→落点三分
        ///（战场=发动/地牌长条=放地/边缘装饰=取消）；拒绝=toast 真因+回手（绝不静默吞）；
        /// 选择器在身=取消。联机局沿 NetOnFanUse/NetOnFanPlaceLand intent（落点语义不进协议）。</summary>
        private void HandleFanDragEnd(HandFanCard fan, Vector2 screenPoint)
        {
            BattlePointer.ConsumeConfirm(); // 抬手帧吞棋盘点击（防同帧 Confirm 弹 Information/动作钮——§4.4）
            HideDragVisual(); // 阶段H：收 3D 箭头+清波（[HandDrag] drop 日志口径保留）
            if (fan == null) return;
            if (_gameEnded || IsTargetSelecting()
                || (_cellPicker != null && _cellPicker.Active)
                || (_choreo != null && _choreo.IsTargeting))
            {
                _toast.Show("选择进行中——拖拽已取消");
                _selfFan.RestoreDragCard(fan, false);
                return;
            }
            if (FanCardOf(fan) == null) { _selfFan.RestoreDragCard(fan, false); return; } // 已离手（对账竞态）

            // UGUI 命中：本扇子树=取消保持收扇；其它 UGUI 面板（Region/Information/HUD）=取消回常态
            //（§4.3.2 表）；零 UGUI 命中才进 3D 分类。**拖拽卡自身子树不计**——卡跟手，指针处必命中
            // 自己的 touch-shield，若计入则任何落点都误判「落回手牌区」（2026-11-15 走查实证修复：
            // 生物拖到战场恒 cancel、结界未跟手时反而成功，根因即此）。
            var es = EventSystem.current;
            if (es != null && _selfFan.Layer != null)
            {
                var ped = new PointerEventData(es) { position = screenPoint };
                es.RaycastAll(ped, _dragUiHits);
                bool overFan = false, anyUi = false;
                for (int i = 0; i < _dragUiHits.Count; i++)
                {
                    var go = _dragUiHits[i].gameObject;
                    if (go == null) continue;
                    if (fan != null && go.transform.IsChildOf(fan.transform)) continue; // 拖拽卡自身=跟手伪命中
                    anyUi = true;
                    if (go.transform.IsChildOf(_selfFan.Layer)) { overFan = true; break; }
                }
                _dragUiHits.Clear();
                if (overFan) { _selfFan.RestoreDragCard(fan, true); return; }   // 落在手牌区自身：取消并保持收扇
                if (anyUi) { _selfFan.RestoreDragCard(fan, false); return; }    // 落在其它面板：取消
            }

            var kind = ClassifyDragDrop(screenPoint, out int cx, out int cz);
            Debug.Log($"[HandDrag] drop kind={kind} cell=({cx},{cz}) screen=({screenPoint.x:F0},{screenPoint.y:F0})");

            if (NetActive)
            {
                if (kind == DragDropKind.Land) NetOnFanPlaceLand(fan); // 网络局：intent 上行（ModeIndex=-1，不选色）
                else if (kind == DragDropKind.Field) NetOnFanUse(fan);
                else _selfFan.RestoreDragCard(fan, false);
                return;
            }
            if (!GateFanMyMain()) { _selfFan.RestoreDragCard(fan, false); return; } // 闸自带 toast
            var card = FanCardOf(fan);
            if (card == null) { _selfFan.RestoreDragCard(fan, false); return; }
            if (kind == DragDropKind.Cancel) { _selfFan.RestoreDragCard(fan, false); return; }
            if (kind == DragDropKind.Land) { DragPlaceLand(fan, card); return; }
            DragUseOnField(fan, card, cx >= 0 && cz >= 0 ? ((int x, int z)?)(cx, cz) : null);
        }

        // LastDragDropWorldX（色带反推用）已随 2026-11-15 地牌改版整删（唯一调用点=色带反推）。

        /// <summary>拖拽放地（2026-11-15 地牌改版定稿）：整块任意落点=放地——不选色、不选格
        ///（引擎快照可产集合，AssignLandCells 按展示主色落格）；不可作地牌→toast 回手。</summary>
        private void DragPlaceLand(HandFanCard fan, Card card)
        {
            if (Core?.ElementPool == null || !ElementPoolSystem.CanServeAsLand(card) || !Core.ElementPool.CanAddLand(P1))
            {
                _toast.Show("不可作为地牌放置（资格/地牌上限）");
                _selfFan.RestoreDragCard(fan, false);
                return;
            }
            if (ElementPoolSystem.GetLandColorChoices(card).Count == 0)
            {
                _toast.Show("该卡无元素费用，不能作为地牌");
                _selfFan.RestoreDragCard(fan, false);
                return;
            }
            var view = BattleView.FromRuntime(SerializableRuntimeCardState.FromCard(card), card, false);
            _selfFan.RestoreDragCard(fan, false); // 卡先回手（成功路径随 LandFinal→RestoreFan 对账离场）
            LandFinal(view);
        }

        /// <summary>拖拽到战场（发动）：法术=自动 first-free（无箭头·不取落点·声明期目标照旧）；
        /// 永久物=命中空单位格优先否则 first-free（定稿——不弹选格器）；多模式先 Region 选模式。</summary>
        private void DragUseOnField(HandFanCard fan, Card card, (int x, int z)? hitCell)
        {
            var data = (card as CardWrapper)?.GetData();
            if (data == null) { _selfFan.RestoreDragCard(fan, false); return; }
            var view = BattleView.FromRuntime(SerializableRuntimeCardState.FromCard(card), card, false);

            if (data.Supertype == Cardtype.Spell)
            {
                int mode = GetModeCount(view) >= 2
                    ? Mathf.Clamp(fan.SelectedEffectMode, 0, GetModeCount(view) - 1) : 0;
                if (Core == null || !Core.ElementPool.CanPayCostWithAutoTap(
                        GameActions.GetCardCost(card, mode), P1))
                {
                    _toast.Show("费用不足——该作用不可发动");
                    _selfFan.RestoreDragCard(fan, false);
                    return;
                }
                _selfFan.RestoreDragCard(fan, false);
                BeginSpellCastAutoPlace(view, card, mode);
                return;
            }
            if (CostDerivationService.GetModeCount(data) >= 2)
            {
                PromptModeThenDragSummon(fan, view, card, hitCell).Forget();
                return;
            }
            DragSummonFlow(fan, view, card, 0, hitCell);
        }

        /// <summary>多模式永久物拖拽：Region 选模式（选完才回手；取消=放置作废卡在手）→钉格召唤。</summary>
        private async UniTaskVoid PromptModeThenDragSummon(HandFanCard fan, BattleCardView view, Card card, (int x, int z)? hitCell)
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
            _selfFan.RestoreDragCard(fan, false); // 选模式期卡回手（Region 面板展开扇可读）
            int? picked = await PickRegionOptionAsync("选择模式", options);
            if (picked == null) return;
            DragSummonFlow(fan, view, card, picked.Value, hitCell);
        }

        /// <summary>拖拽召唤（生物/结界）：落位=命中**空单位格**优先，否则自动 first-free（定稿）；
        /// 场地卡恒落场地格（顶替旧场地）；费用/满区拒绝=toast+回手。</summary>
        private void DragSummonFlow(HandFanCard fan, BattleCardView view, Card card, int modeIndex, (int x, int z)? hitCell)
        {
            if (Core == null || !GameActions.CanAffordCast(Core, P1, card, modeIndex))
            {
                _toast.Show("费用不足——该分支不可发动");
                _selfFan.RestoreDragCard(fan, false);
                return;
            }
            var board = _ctrl?.Board;
            if (board == null) { _selfFan.RestoreDragCard(fan, false); return; }
            var data = (card as CardWrapper)?.GetData();
            if (data != null && data.IsField)
            {
                var fc = BoardLayout.FieldCell(0);
                _choreo?.CancelTargeting();
                _pendingPins[card.RuntimeId] = fc;
                bool replacing = board.FieldOccupied(P1);
                _toast.Show(replacing ? "场地就位——将顶替己方现有场地（旧场地入墓）" : "场地就位（己方场地格）");
                _selfFan.RestoreDragCard(fan, false);
                PlayCardFinal(view, null, modeIndex);
                return;
            }
            (int x, int z) cell = (-1, -1);
            if (hitCell.HasValue)
            {
                var hc = hitCell.Value;
                bool isUnitCell = false;
                foreach (var u in BoardLayout.UnitCells(0))
                    if (u.x == hc.x && u.z == hc.z) { isUnitCell = true; break; }
                if (isUnitCell && board.CardAt(hc.x, hc.z) == null) cell = hc; // 指向的空单位格优先（定稿）
            }
            if (cell.x < 0) // 自动 first-free
                foreach (var c in BoardLayout.UnitCells(0))
                    if (board.CardAt(c.x, c.z) == null) { cell = c; break; }
            if (cell.x < 0)
            {
                _toast.Show("单位区已满——无法打出");
                _selfFan.RestoreDragCard(fan, false);
                return;
            }
            _choreo?.CancelTargeting();
            _pendingPins[card.RuntimeId] = cell;
            Debug.Log($"[HandDrag] drop field({cell.x},{cell.z})：{view.Name}（命中空格优先，否则 first-free）");
            _selfFan.RestoreDragCard(fan, false);
            PlayCardFinal(view, null, modeIndex);
        }

    }
}
