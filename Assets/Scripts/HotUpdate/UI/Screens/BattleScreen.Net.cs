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
        // ======================================== 网络模式（2026-10-21 P2P 复活） ========================================
        // 本屏无 BattleController/本地引擎（房主侧引擎也只归服务器泵——UI 一律客户端路径）：
        // 快照 → BuildNet → 投影器 ApplySnapshot（复用全套 3D 对账/信息栏/连锁链渲染）；
        // 交互收口 intent 上行（目标不预收——结算期 SelectRequest 反问回弹，服务器权威候选集）；
        // 反问 = Region 选项列表（实体候选按 CardId 查表渲染卡名）；
        // 表现 MVP = 战报轻日志+浮字级演出（伤害/治疗粒度的 3D 演出接 NetEvent 载荷属后续里程碑）。

        /// <summary>网络对局接入（LobbyScreen 移交的连接一次性消费）。</summary>
        private void StartNetFan()
        {
            _netClient = BattleEntry.Client;
            BattleEntry.Client = null;
            if (_netClient == null)
            {
                _toast.Show("网络对局连接缺失——返回主菜单");
                Manager.Back();
                return;
            }

            // 反问选择器（Region 选项列表）。不挂 TargetSelectionService/ResponseWindowService：
            // 网络局本端无引擎请求在途；房主侧更不得覆盖 NetRoom 已挂的 NetworkTargetSelector
            //（覆盖=引擎反问改走本地 Region 选择器→读全信息引擎→泄漏+流程错乱）。
            _regionSelector = _regionPanelCtl != null
                ? new RegionTargetSelector(_regionPanelCtl.PanelNode, BoardCellOf, p => false, RoleCellOf,
                    BuildSelectPrompt, _decision.OnSelectCancel, CloseInformation)
                : null;

            // 手牌扇交互（网络版：可发动性按视图近似评估，真校验交服务器 Error 反馈；
            // SpUse 钩子随 2026-11-01 上卡退役——特殊行可用性并入 CanUse）
            _selfFan?.SetInteractions(new FanInteractions
            {
                CanUse = NetFanCanUse,
                CanPlaceLand = NetFanCanPlaceLand,
                OnUse = NetOnFanUse,
                OnPlaceLand = NetOnFanPlaceLand,
            });

            _netClient.OnSnapshot += NetOnSnapshot;
            _netClient.OnEventBatch += NetOnEvents;
            _netClient.OnSelectRequest += NetOnSelectRequest;
            _netClient.OnTextRequest += NetOnTextRequest;
            _netClient.OnRoomState += NetOnRoomState;
            _netClient.OnManifest += NetOnManifest;
            _netClient.OnError += e => _toast.Show($"被拒（{e?.Context}）：{e?.Reason}");
            _netClient.OnDisconnected += NetOnDisconnected;

            // 网络五段式（2026-10-28 开局流程改版）：就绪开局（DeckSubmit 相位）即战场演出——
            // 地块/角色就位→掷币（服务器权威结果注入/固定模式快进）→网络选卡组面板（30s 服务器
            // 权威倒计时）→确认发 DeckSubmit 握手；对局开始=manifest 校验通过+首快照（既有链路）。
            if (StagedOpeningActive())
            {
                bool coinAnim = BattleEntry.NetCoinToss; // 一次性消费（防跨局残留，同 Client 口径）
                bool playerFirst = BattleEntry.NetPlayerFirst;
                _opening = new BattleOpeningDirector(() => _running, _field, lunzhuan: false,
                    engage: null, overlayParent: Root,
                    network: true, netPlayerFirst: playerFirst, netCoinAnimation: coinAnim,
                    netEngage: NetEngageStaged);
                _opening.RunAsync().Forget();
            }

            Debug.Log($"[BattleScreen] 网络对局已接入（{_netClient.Remote}）——{(StagedOpeningActive() ? "五段式开局（掷币+选卡组）" : "等待首快照")}");
        }

        /// <summary>网络五段式段五（确认出战）：UI 已关、对局开始前——发 DeckSubmit 握手（摘要主线程算）。
        /// tokenCardIds/tokenBindings（P9b 2026-10-29）：衍生物区/槽绑定随提交上送并计入摘要口径。</summary>
        private void NetEngageStaged(string[] cardIds, string skillCardId,
            string[] tokenCardIds, List<DeckTokenBinding> tokenBindings)
        {
            if (_netClient == null || cardIds == null || cardIds.Length == 0) return;
            _netSubmittedDeckIds = cardIds;
            _netSubmittedTokenIds = tokenCardIds;
            _netSubmittedTokenBindings = tokenBindings;
            var digest = NetMatchHandshake.ComputeDeckDigest(cardIds, tokenCardIds, tokenBindings); // 主线程（确认点击）——服务器泵同帧串行，天然安全
            _netClient.Send(NetworkMessageType.DeckSubmit, new MsgDeckSubmit
            {
                DeckName = "net",
                CardIds = cardIds,
                Digest = digest,
                SkillCardId = skillCardId,
                TokenCardIds = tokenCardIds ?? Array.Empty<string>(),
                TokenBindings = NetMatchHandshake.ToWireBindings(tokenBindings),
            });
            Debug.Log($"[BattleScreen] 已确认卡组（{cardIds.Length}张）——等待对手确认与开局…");
            _toast.Show("已确认卡组——等待对手与开局…");
        }

        /// <summary>开局清单到达（双方确认完毕）：校验回显→收口段四面板→对局开始（等首快照）。</summary>
        private void NetOnManifest(MsgMatchManifest manifest)
        {
            if (manifest == null) return;
            if (_netSubmittedDeckIds != null
                && !NetMatchHandshake.VerifyMatchManifest(manifest, _netSubmittedDeckIds,
                    _netSubmittedTokenIds, _netSubmittedTokenBindings, out var reason))
            {
                TideLog.Error($"[BattleScreen] MatchManifest 校验失败：{reason}——退回主菜单");
                _toast.Show($"开局清单校验失败：{reason}");
                Manager.Back();
                return;
            }
            _opening?.NetDeckPanel?.ForceClose(); // 服务器代提/双方已齐——面板收口（不再本地提交）
            Debug.Log($"[BattleScreen] 网络开局清单校验通过（OwnSeat={manifest.OwnSeat}）——对局开始");
        }

        /// <summary>快照到达：BuildNet → 投影器 ApplySnapshot（渲染扇出与本地同轨）+ 双扇对账。</summary>
        private void NetOnSnapshot(MsgGameStateSync snap)
        {
            if (snap == null) return;
            _netSnapshot = snap;
            _projector.ApplySnapshot(BattleView.BuildNet(snap));
            RefreshFansNet();
        }

        /// <summary>网络局双扇对账：己方手牌正面（快照 OwnRuntimeIds）；对手=数量背面+展示卡翻正面。</summary>
        private void RefreshFansNet()
        {
            if (BattleRevealDirector.SuppressHandFan) return;
            var d = _view;
            if (d == null) return;

            var self = new List<HandFanCardData>();
            foreach (var card in d.SelfHand)
                self.Add(new HandFanCardData
                {
                    RuntimeId = card.RuntimeId,
                    Item = CardOverlayItem.FromHand(card),
                    FaceUp = true,
                });
            _selfFan?.SetCards(self);

            // 对手手牌：背面按数量（RuntimeId 隐藏——合成键）；被展示（Exposed）的经 RevealedZoneCards 翻正面
            var exposed = new List<SerializableRuntimeCardState>();
            if (_netSnapshot?.RevealedZoneCards != null)
                foreach (var z in _netSnapshot.RevealedZoneCards)
                    if (z != null && (Zone)z.Zone == Zone.Hand && z.Seat == 1 - d.NetViewerSeat)
                        exposed.AddRange(z.Cards ?? Array.Empty<SerializableRuntimeCardState>());
            var opp = new List<HandFanCardData>();
            int backs = Math.Max(0, d.Opp.HandCount - exposed.Count);
            for (int i = 0; i < backs; i++)
                opp.Add(new HandFanCardData { RuntimeId = 0x80000000u + (uint)i, Item = BlankBack(0), FaceUp = false });
            foreach (var s in exposed)
            {
                var v = BattleView.FromRuntime(s, null, ownerIsOpponent: true);
                opp.Add(new HandFanCardData { RuntimeId = s.RuntimeId, Item = CardOverlayItem.FromHand(v), FaceUp = true });
            }
            _oppFan?.SetCards(opp);

            // 可发动性精确化（2026-10-28 取代「主阶段全亮」近似）：逐卡费用可付/永久物空格
            //（放地上限在 NetFanCanPlaceLand）——置灰口径与本地一致（SetPlayable 同源）
            if (_selfFan != null)
                foreach (var card in d.SelfHand)
                {
                    bool castable = NetCardCastableNet(card);
                    _selfFan.SetPlayable(card.RuntimeId, castable, false);
                }
        }

        // ---- 网络局交互（intent 上行） ----

        private bool NetFanGate()
        {
            var d = _view;
            return !_gameEnded && d != null && d.MyTurn && d.Phase == PhaseType.Main && !IsTargetSelecting();
        }

        /// <summary>网络「使用」条件（2026-11-01 SpUse 退役·合并口径）：普通出牌=NetCardCastableNet；
        /// 特殊行=充能（卡面声明 chargeAbility 才有——颜色余量服务器 CanCharge 终审，客户端不预闸）。</summary>
        private bool NetFanCanUse(HandFanCard fan)
        {
            if (!NetFanGate()) return false;
            var card = NetHandViewOf(fan);
            if (card == null) return false;
            if (NetCardCastableNet(card)) return true;
            // 充能（2026-11-05 系统动作化；2026-11-06 opt-in）：卡面声明充能资格的手牌卡一回合一次
            // 可宣（协议 114 改义承载）——客户端近似闸（服务器真校验兜底）。
            var data = DataOf(card);
            if (data == null || !data.ChargeAbility) return false;
            CardCostService.GetPrePayloadCost(data, 0, out var preCost, out _);
            foreach (var c in preCost.NonzeroColors())
                if (c != ManaType.Any && preCost[c] > 0f) return true;
            return false;
        }

        /// <summary>网络逐卡可发动（2026-10-28 可用性精确化，取代「主阶段全亮」近似）：
        /// 费用任一模式可付（快照每色可用量）+ 永久物另需单位区空格——同本地 CardCastableNow
        /// 口径；数据源=快照（WalletByColor/单位占用），服务器仍真校验（Error+toast 兜底）。
        /// 已知近似：法术锚价（SpellCastAnchor 绑引擎卡实例）未并入，法术显示偏亮由服务器拒兜底。</summary>
        private bool NetCardCastableNet(BattleCardView card)
        {
            var data = DataOf(card);
            if (data == null) return false;
            int modes = Math.Max(1, CostDerivationService.GetModeCount(data));
            bool ok = false;
            for (int i = 0; i < modes && !ok; i++)
                ok = NetCanAfford(data, i);
            if (!ok) return false;
            if (data.Supertype != Cardtype.Spell)
            {
                if (data.IsField) return true; // 场地卡（P8）：场地格每方一张恒可落，不占单位区空格
                return NetEmptyUnitCells().Count > 0; // 永久物=单位区空格（法术恒自动落位）
            }
            return true;
        }

        /// <summary>网络可付判定：恒推导费用账单 vs 快照每色可用量（WalletByColor=
        /// GetAvailableManaCount：钱包+未横置地牌现取现用合计）。Any（万能货币）由付完
        /// 本色后的盈余垫付（链尾垫付同口径）。</summary>
        private bool NetCanAfford(CardData data, int modeIndex)
        {
            var d = _view;
            if (d == null) return false;
            var bill = ElementPaymentValidator.NormalizeBill(CardCostService.GetRuntimeCost(data, modeIndex));
            if (bill.Count == 0) return true;
            var avail = d.Self.WalletByColor;
            int surplus = 0;
            foreach (var kv in bill)
            {
                if (kv.Key == ManaType.Any) continue;
                int have = (int)kv.Key >= 0 && (int)kv.Key < avail.Length ? avail[(int)kv.Key] : 0;
                if (kv.Value > have) return false;
                surplus += have - kv.Value;
            }
            return !bill.TryGetValue(ManaType.Any, out var anyNeed) || anyNeed <= surplus;
        }

        private bool NetFanCanPlaceLand(HandFanCard fan)
        {
            if (!NetFanGate()) return false;
            var data = DataOf(NetHandViewOf(fan));
            if (data == null) return false;
            // 锁色资格=可锁产色候选非空（GetLandColorChoices 单源：卡面/各模式费 ∪ 主动效果锚价，去黑白）
            if (NetLandColorChoices(data).Count == 0) return false;
            // 地牌池上限（快照 LandCap vs 我方已放地牌数——同本地 CanAddLand 口径）
            var d = _view;
            return (d?.SelfLands?.Count ?? 0) < (d?.Self.LandCap ?? 0);
        }

        /// <summary>可锁产色（GetLandColorChoices 网络侧口径——CardData 单源重载直转，锚价开格 2026-10-23）。</summary>
        private static List<ManaType> NetLandColorChoices(CardData data)
            => ElementPoolSystem.GetLandColorChoices(data);

        /// <summary>扇区卡 → 视图手牌卡（RuntimeId 对账；不在手=已过态返回 null）。</summary>
        private BattleCardView NetHandViewOf(HandFanCard fan)
        {
            if (fan == null) return null;
            foreach (var c in _view?.SelfHand ?? new List<BattleCardView>())
                if (c != null && c.RuntimeId == fan.RuntimeId) return c;
            return null;
        }

        /// <summary>网络「使用」点击（2026-11-01 上卡·SpUse 退役）：
        /// 特殊行选中=充能（IntentBuildCard 114）上行；普通=多主动法术按卡面效果行
        /// 选中模式（selectedMode 上行）。服务器终审一切。</summary>
        private void NetOnFanUse(HandFanCard fan)
        {
            var card = NetHandViewOf(fan);
            if (card == null || !NetFanGate())
            {
                // 2026-11-03 失败提示细化：非回合/非主阶段给可读反馈（与本地 GateFanMyMain 对称；
                // 目标选择期不弹——其自有选择 UX）
                if (card != null && !_gameEnded && _view != null && !IsTargetSelecting())
                    _toast.Show(!_view.MyTurn ? "对手回合，暂不可操作" : "当前不是主要阶段");
                return;
            }
            var spKind = fan.SelectedSpKind;
            if (spKind == SpEffectKind.Charge) { NetChargeAsync(card); return; }
            NetUseCardAsync(card, fan.SelectedEffectMode).Forget();
        }

        /// <summary>网络充能（2026-11-05 系统动作化改版）：**不选色直发** IntentBuildCard（协议 114，
        /// Color 恒 −1——颜色改由服务器结算期反问下发 SelectRequest）。服务器 DeclareCharge 终审。</summary>
        private void NetChargeAsync(BattleCardView card)
        {
            if (DataOf(card) == null || _netClient == null) return;

            _netClient.Send(NetworkMessageType.IntentBuildCard, new MsgIntentBuildCard
            {
                CardRuntimeId = card.RuntimeId,
                Color = -1, // 2026-11-05：充能色在服务器结算期反问（字段保留协议兼容，恒 −1）
            });
            _toast.Show("充能已宣入（服务器终审——结算期选择元素类别）");
        }

        /// <summary>网络出牌：模式抉择（Region）→ 永久物（生物/结界）
        /// 棋盘选格（光圈候选，同本地交互——钉 NetCellPins 随快照生效）→ IntentPlayCard
        ///（目标 null=结算期反问回弹）。法术恒自动落位（B4 定案）；选格钉=客户端表现层权威
        ///（快照不承载坐标）。2026-11-01 抉择上卡：多主动法术不再弹
        /// 「选择要发动的效果」Region 窗——模式=卡面效果行直选（selectedMode，同本地口径）；
        /// 永久物多模式照旧弹窗。）</summary>
        private async UniTaskVoid NetUseCardAsync(BattleCardView card, int selectedMode = 0)
        {
            var data = DataOf(card);
            if (data == null) return;

            int modeIndex = 0;
            if (GetModeCount(card) >= 2)
            {
                if (data.Supertype == Cardtype.Spell)
                {
                    modeIndex = Mathf.Clamp(selectedMode, 0, CostDerivationService.GetModeCount(data) - 1);
                }
                else
                {
                    var labels = FindChoiceLabels(data);
                    int modeCount = Math.Max(2, CostDerivationService.GetModeCount(data)); // P12：非充能主动效果数
                    var options = new List<string>();
                    for (int i = 0; i < modeCount; i++)
                    {
                        string name = i < labels.Count && !string.IsNullOrEmpty(labels[i]) ? labels[i] : $"模式 {i + 1}";
                        options.Add(name);
                    }
                    int? picked = await PickRegionOptionAsync("选择模式", options);
                    if (picked == null) return; // 取消/缺 Region：本次不出牌
                    modeIndex = picked.Value;
                }
            }

            // 场地卡（2026-10-29 P8）：落位恒为己方场地格——视图确定性映射（FillUnitsNet），
            // 无需选格/钉；旧场地在位=顶替入墓（服务器 ReplaceExistingField 终局裁决）
            if (data.Supertype != Cardtype.Spell && data.IsField)
            {
                bool replacing = (_view?.SelfUnits ?? new List<BattleCardView>())
                    .Any(u => u != null && DataOf(u)?.IsField == true);
                _toast.Show(replacing ? "场地就位——将顶替己方现有场地（旧场地入墓）" : "场地就位（己方场地格）");
            }
            // 永久物（2026-10-28 用户反馈：结界/生物不再 first-free 自动落位）——棋盘选格后发
            else if (data.Supertype != Cardtype.Spell && !await NetPickUnitCellAsync(card)) return;

            _netClient.Send(NetworkMessageType.IntentPlayCard, new MsgIntentPlayCard
            {
                CardRuntimeId = card.RuntimeId,
                FromZone = (int)Zone.Hand,
                ModeIndex = modeIndex,
                Targets = null, // 声明期目标不预收：结算期经 SelectRequest 反问（服务器候选集权威）
            });
        }

        /// <summary>网络永久物选格（光圈候选=己方单位区空格，同本地 BeginUseCellPicking 交互）：
        /// 点格=写 NetCellPins 钉并返回 true（随发 intent，快照落位即所选格）；取消返回 false。
        /// 满区 toast 拦截。</summary>
        private async UniTask<bool> NetPickUnitCellAsync(BattleCardView card)
        {
            var options = NetEmptyUnitCells();
            if (options.Count == 0) { _toast.Show("单位区已满——无法打出"); return false; }

            _choreo?.CancelTargeting();
            _selfFan?.SetVisible(false);
            var origin = BoardLayout.CharacterCell(0);
            var picked = await PickCellAsync(BattleBoardGrid.WorldPositionOf(origin.x, origin.z), options);
            if (picked == null)
            {
                RestoreFanNet();
                return false;
            }
            NetCellPins.Pin(card.RuntimeId, picked.Opt.X, picked.Opt.Z);
            RestoreFanNet();
            Debug.Log($"[HandPlay] 网络永久物选格：{card.Name} → ({picked.Opt.X},{picked.Opt.Z})（钉随快照生效）");
            return true;
        }

        /// <summary>网络局空格候选（己方单位区未被单位占用的格——视图坐标权威；
        /// 施放卡不占格——S3b 收口 2026-11-05 起 CastingCards 恒 X=-1 不参与占用）。</summary>
        private List<BattleCellPicker.CellOption> NetEmptyUnitCells()
        {
            var occupied = new HashSet<int>();
            foreach (var u in _view?.SelfUnits ?? new List<BattleCardView>())
                if (u != null && u.X >= 0) occupied.Add(u.X * 100 + u.Z);
            var options = new List<BattleCellPicker.CellOption>();
            foreach (var cell in BoardLayout.UnitCells(0))
                if (!occupied.Contains(cell.x * 100 + cell.z))
                    options.Add(new BattleCellPicker.CellOption { X = cell.x, Z = cell.z, ColorIndex = -1 });
            return options;
        }

        private void NetOnFanPlaceLand(HandFanCard fan)
        {
            var card = NetHandViewOf(fan);
            if (card == null || !NetFanGate()) return;
            NetPlaceLandAsync(card).Forget();
        }

        /// <summary>网络放地（2026-10-28 改版：棋盘选格取代 Region 弹窗——同本地交互）：
        /// 候选格=可锁色 → BoardLayout 固定元素格（与 FillLandsNet/AssignLandCells 同构映射）；
        /// 点格→单色直发 IntentAddToElementPool（ModeIndex=锁色）；同格多候选色=Region 列表消歧
        ///（本地同款）；取消恢复手牌扇。</summary>
        private async UniTaskVoid NetPlaceLandAsync(BattleCardView card)
        {
            var colors = NetLandColorChoices(DataOf(card));
            if (colors == null || colors.Count == 0) return;

            // 候选格（cell → 候选色集）：颜色→固定元素格映射（观察者视角己方 owner=0）
            var cellColors = new Dictionary<(int x, int z), List<ManaType>>();
            var options = new List<BattleCellPicker.CellOption>();
            foreach (var color in colors)
            {
                var cell = BoardLayout.LandCellOfElement(0, color);
                if (cell.x < 0) continue;
                if (!cellColors.TryGetValue((cell.x, cell.z), out var list))
                {
                    list = new List<ManaType>();
                    cellColors[(cell.x, cell.z)] = list;
                    options.Add(new BattleCellPicker.CellOption { X = cell.x, Z = cell.z, ColorIndex = (int)color });
                }
                list.Add(color);
            }
            if (options.Count == 0) { _toast.Show("地牌行无空位（颜色数超格）"); return; }

            _choreo?.CancelTargeting();
            _selfFan?.SetVisible(false);
            var origin = BoardLayout.CharacterCell(0);
            var picked = await PickCellAsync(BattleBoardGrid.WorldPositionOf(origin.x, origin.z), options);
            if (picked == null) { RestoreFanNet(); return; } // 取消
            RestoreFanNet();

            var candidates = cellColors[(picked.Opt.X, picked.Opt.Z)];
            if (candidates.Count == 1)
            {
                NetLandFinal(card, candidates[0]);
                return;
            }
            // 同格多候选色：Region 列表消歧（本地 PickLandColorAsync 同款）
            var labels = candidates.Select(c => $"锁定 {BattleView.ManaZh(c)} 产色").ToList();
            int? idx = await PickRegionOptionAsync("选择地牌产色", labels);
            if (idx == null) return;
            NetLandFinal(card, candidates[idx.Value]);
        }

        private void NetLandFinal(BattleCardView card, ManaType color)
        {
            Debug.Log($"[HandPlay] 网络放地：{card.Name} → 锁定 {BattleView.ManaZh(color)}（棋盘选格确认）");
            _netClient.Send(NetworkMessageType.IntentAddToElementPool, new MsgIntentAddToElementPool
            {
                CardRuntimeId = card.RuntimeId,
                ModeIndex = (int)color, // 2026-10-11 锁色定案：ModeIndex 复用为 ManaType 枚举值
            });
        }

        /// <summary>网络局手牌扇恢复（选格取消/确认后——同本地 RestoreFan 口径，走网络刷新链）。</summary>
        private void RestoreFanNet()
        {
            _selfFan?.SetVisible(true);
            RefreshFansNet();
        }

        /// <summary>选格结果包装（AOT 约束：CellOption 是热更 struct——UniTask 泛型参禁热更
        /// struct，引用类型包装走已锚 &lt;object&gt; 共享形态；null=取消）。</summary>
        private sealed class NetCellPick
        {
            public BattleCellPicker.CellOption Opt;
        }

        /// <summary>选格异步包装（2026-10-28 网络放地/永久物选格共用：光圈候选+点格回调）。
        /// 取消=返回 null。</summary>
        private async UniTask<NetCellPick> PickCellAsync(
            Vector3 origin, List<BattleCellPicker.CellOption> options)
        {
            var tcs = new Cysharp.Threading.Tasks.UniTaskCompletionSource<NetCellPick>();
            _cellPicker.Begin(origin, options,
                opt => tcs.TrySetResult(new NetCellPick { Opt = opt }),
                () => tcs.TrySetResult(null));
            return await tcs.Task;
        }

        /// <summary>网络局点己方单位：响应窗口在身=守卫候选；我方主阶段=攻击宣言（Region 选目标——
        /// 3D 寻路箭头选择器依赖本地引擎路径，网络局列表选择）。</summary>
        private void NetUnitClick(BattleCardView card)
        {
            var d = _view;
            if (_gameEnded || d == null) return;
            if (d.StackNotEmpty && d.MyPriority)
            {
                NetGuardFlowAsync(card).Forget();
                return;
            }
            if (!d.MyTurn || d.Phase != PhaseType.Main)
            {
                _toast.Show(!d.MyTurn ? "对手回合，暂不可操作" : "当前不是主要阶段");
                return;
            }
            if (card.IsTapped) { _toast.Show("该单位已横置"); return; }
            if (card.CoreCard is Card netUnitCard && !netUnitCard.HasAttackAbility())
            { _toast.Show("该单位不具备攻击能力"); return; } // 无攻资格预检（2026-11-06 opt-in）：点击即提示，不进选目标
            if (card.Runtime != null && card.Runtime.Power <= 0) { _toast.Show("该单位攻击力为 0"); return; }
            NetAttackFlowAsync(card).Forget();
        }

        private async UniTaskVoid NetAttackFlowAsync(BattleCardView card)
        {
            var d = _view;
            var options = new List<string> { "对方角色" };
            var targets = new List<NetEntityRef>
            {
                new NetEntityRef { Seat = 1 - d.NetViewerSeat, IsPlayer = true },
            };
            foreach (var u in d.OppUnits)
            {
                if (u == null) continue;
                options.Add(u.Name);
                targets.Add(new NetEntityRef
                {
                    RuntimeId = u.RuntimeId,
                    CardId = u.CardId,
                    Seat = 1 - d.NetViewerSeat,
                    IsPlayer = false,
                });
            }
            int? picked = await PickRegionOptionAsync("选择攻击目标", options);
            if (picked == null) return;
            _netClient.Send(NetworkMessageType.IntentDeclareAttack, new MsgIntentDeclareAttack
            {
                Attacker = new NetEntityRef
                {
                    RuntimeId = card.RuntimeId,
                    CardId = card.CardId,
                    Seat = d.NetViewerSeat,
                    IsPlayer = false,
                },
                Target = targets[picked.Value],
            });
        }

        /// <summary>网络守卫：守卫对象=栈上的攻击宣言（StackObjectType==AttackDeclaration，Source=攻击者）。</summary>
        private async UniTaskVoid NetGuardFlowAsync(BattleCardView blocker)
        {
            if (blocker.IsTapped) { _toast.Show("该单位已横置（守卫须未横置）"); return; }
            var snap = _netSnapshot;
            if (snap == null) return;
            var options = new List<string>();
            var attackers = new List<NetEntityRef>();
            foreach (var item in snap.StackV2 ?? Array.Empty<StackItemDTO>())
            {
                if (item == null || item.StackObjectType != (int)StackObjectType.AttackDeclaration) continue;
                if (item.Source == null || item.Source.IsPlayer) continue;
                options.Add($"拦截 {BattleView.EntityTextOf(item.Source, snap.ViewerSeat)} 的攻击");
                attackers.Add(item.Source);
            }
            if (options.Count == 0) { _toast.Show("没有可守卫的攻击"); return; }
            int? picked = await PickRegionOptionAsync("选择要守卫的攻击", options);
            if (picked == null) return;
            _netClient.Send(NetworkMessageType.IntentDeclareBlock, new MsgIntentDeclareBlock
            {
                Blocker = new NetEntityRef
                {
                    RuntimeId = blocker.RuntimeId,
                    CardId = blocker.CardId,
                    Seat = snap.ViewerSeat,
                    IsPlayer = false,
                },
                Attacker = attackers[picked.Value],
            });
        }

        /// <summary>网络局英雄技能发动（角色格【发动】键）：多效果 Region 抉择 → intent（服务器横置闸校验）。</summary>
        private async UniTaskVoid NetActivateSkillAsync()
        {
            var d = _view;
            if (_gameEnded || d == null || !d.MyTurn || d.Phase != PhaseType.Main) return;
            var skill = d.Self?.Skill;
            if (skill == null) { _toast.Show("本局无英雄技能"); return; }
            if (skill.IsTapped) { _toast.Show("技能本回合已发动"); return; }

            // 主动效果计数（CardData 口径——与 HeroSkillSystem.SkillEffectsOf 同谓词）；
            // 转换后 def 同序对齐，供抉择文案显示各效果锚价（与引擎现付同源）
            var data = DataOf(skill);
            var defs = data != null ? CardEffectConverter.ConvertAll(data.Effects, data.ID) : null;
            int count = 0;
            var names = new List<string>();
            var fees = new List<string>();
            if (data?.Effects != null)
                foreach (var e in data.Effects)
                {
                    int timing = e?.TriggerTiming ?? -1;
                    if (timing != (int)TriggerTiming.Activate_Active
                        && timing != (int)TriggerTiming.Activate_Instant) continue;
                    string name = string.IsNullOrEmpty(e.DisplayName) ? $"效果 {count + 1}" : e.DisplayName;
                    var def = defs != null && count < defs.Count ? defs[count] : null;
                    names.Add(name);
                    fees.Add(def != null ? SkillFeeZh(def) : "付费");
                    count++;
                }
            if (count == 0) { _toast.Show("技能无可发动效果"); return; }

            int effectIndex = 0;
            if (count > 1)
            {
                var options = new List<string>();
                for (int i = 0; i < names.Count; i++)
                    options.Add($"{names[i]}（{fees[i]}·速度0）");
                int? picked = await PickRegionOptionAsync("选择要发动的效果", options);
                if (picked == null) return;
                effectIndex = picked.Value;
            }
            _netClient.Send(NetworkMessageType.IntentActivateHeroSkill,
                new MsgIntentActivateHeroSkill { EffectIndex = effectIndex });
        }

        /// <summary>网络局结束回合（hud 结束格）。</summary>
        private void NetEndTurn()
        {
            var d = _view;
            if (_gameEnded || d == null) return;
            if (!d.MyTurn) { _toast.Show("对手回合进行中，暂不可结束回合"); return; }
            _netClient.Send<object>(NetworkMessageType.IntentEndTurn, null);
        }

        /// <summary>网络局让行（btn-notchain：响应窗口/优先权在我时让过=PassPriority）。</summary>
        private void NetPassPriority()
            => _netClient.Send<object>(NetworkMessageType.IntentPassPriority, null);

        /// <summary>btn-notchain 分发（2026-10-21 网络复活）：网络局=让过 intent；本地局=决策中枢三义键。</summary>
        private void OnNotChainClick()
        {
            if (NetActive)
            {
                NetPassPriority();
                return;
            }
            _decision.OnNotChainClick();
        }

        // ---- 网络局反问（SelectRequest → Region 选项列表 → SelectResponse） ----

        private void NetOnSelectRequest(MsgSelectRequest req)
        {
            if (req == null || !NetActive) return;
            NetSelectAsync(req).Forget();
        }

        private async UniTaskVoid NetSelectAsync(MsgSelectRequest req)
        {
            int me = _view?.NetViewerSeat ?? _netSnapshot?.ViewerSeat ?? 0;
            List<string> options;
            if (req.Candidates != null && req.Candidates.Length > 0)
            {
                // 实体反问：候选按 CardId 查表渲染卡名（Labels 恒空——线上去文本口径）
                options = new List<string>(req.Candidates.Length);
                foreach (var r in req.Candidates)
                    options.Add(BattleView.EntityTextOf(r, me));
            }
            else
            {
                options = req.Labels?.ToList() ?? new List<string>();
            }

            string title = string.IsNullOrWhiteSpace(req.Title) ? "请选择" : req.Title;
            if (options.Count == 0)
            {
                // 空候选（无目标可用等）：直接空应答（引擎 AutoSelect 兜底同口径）
                _netClient.Send(NetworkMessageType.SelectResponse, new MsgSelectResponse
                {
                    RequestId = req.RequestId,
                    Indices = Array.Empty<int>(),
                });
                return;
            }

            int[] indices;
            if (req.Max > 1 && _regionSelector != null)
            {
                var picked = await _regionSelector.SelectIndicesAsync(
                    options, Math.Max(1, Math.Min(req.Min, options.Count)),
                    Math.Min(Math.Max(req.Max, 1), options.Count), title, "", false, 60f);
                indices = picked != null ? picked.ToArray() : Array.Empty<int>();
            }
            else
            {
                int? picked = await PickRegionOptionAsync(title, options);
                indices = picked == null ? Array.Empty<int>() : new[] { picked.Value };
            }
            if (!NetActive) return; // 断线后不再应答
            _netClient.Send(NetworkMessageType.SelectResponse, new MsgSelectResponse
            {
                RequestId = req.RequestId,
                Indices = indices,
            });
        }

        // ---- 网络局文本反问（2026-10-31 阶段C：TextRequest → 模态输入窗 → TextResponse）----

        private void NetOnTextRequest(MsgTextRequest req)
        {
            if (req == null || !NetActive) return;
            NetTextAsync(req).Forget();
        }

        private async UniTaskVoid NetTextAsync(MsgTextRequest req)
        {
            // 本地局同款模态输入窗（RegionTargetSelector.PromptTextAsync——中文输入法随 TMP）；
            // null=取消（Text=null 应答），服务器侧再按长度/座位校验收口
            string text = _regionSelector != null
                ? await _regionSelector.PromptTextAsync(
                    req.Title, req.Default, req.MaxLen, req.TimeoutSeconds > 0f ? req.TimeoutSeconds : 120f)
                : null;
            if (!NetActive) return; // 断线后不再应答
            _netClient.Send(NetworkMessageType.TextResponse, new MsgTextResponse
            {
                RequestId = req.RequestId,
                Text = text,
            });
        }

        // ---- 网络局下行事件（表现 MVP：终局+战报轻日志） ----

        private void NetOnEvents(NetEvent[] events)
        {
            if (events == null) return;
            foreach (var e in events)
            {
                if (e == null) continue;
                if (e.EventType == nameof(GameOverEvent))
                {
                    var winner = NetParamEntity(e, nameof(GameOverEvent.Winner));
                    string reason = NetParamString(e, nameof(GameOverEvent.Reason));
                    int me = _view?.NetViewerSeat ?? -1;
                    bool win = me >= 0 && winner != null && winner.Seat == me;
                    if (!_gameEnded)
                    {
                        _gameEnded = true;
                        MarkGameOverUi($"{(win ? "胜利" : "失败")}——{(win ? "我方" : "对手")}获胜（{reason}）");
                    }
                    continue;
                }
                NetAppendLogLight(e);
            }
        }

        /// <summary>轻量战报行（schema-less 参数直取；完整战报中间层随演出里程碑接入）。</summary>
        private void NetAppendLogLight(NetEvent e)
        {
            int me = _view?.NetViewerSeat ?? -1;
            string line = null;
            switch (e.EventType)
            {
                case nameof(CardPlayEvent):
                    line = $"{WhoText(NetParamEntity(e, nameof(CardPlayEvent.Player)), me)} 打出 " +
                           $"{BattleView.EntityTextOf(NetParamEntity(e, nameof(CardPlayEvent.PlayedCard)), me)}";
                    break;
                case nameof(DamageEvent):
                    line = $"{BattleView.EntityTextOf(NetParamEntity(e, nameof(DamageEvent.Target)), me)} " +
                           $"受到 {NetParamInt(e, nameof(DamageEvent.Amount))} 点伤害";
                    break;
                case nameof(CardDrawEvent):
                    line = $"{WhoText(NetParamEntity(e, nameof(CardDrawEvent.Player)), me)} 抽了一张牌";
                    break;
            }
            if (line != null) _log.Append(line, "log-line--net");
        }

        private static string WhoText(NetEntityRef r, int viewerSeat)
            => r != null && r.IsPlayer ? (r.Seat == viewerSeat ? "我方" : "对手") : "?";

        private static NetParam NetParamOf(NetEvent e, string field)
            => e.Params?.FirstOrDefault(p => p != null && p.FieldName == field);

        private static NetEntityRef NetParamEntity(NetEvent e, string field)
            => NetParamOf(e, field)?.EntityRefs?.FirstOrDefault();

        private static long NetParamInt(NetEvent e, string field)
            => NetParamOf(e, field)?.IntValue ?? 0;

        private static string NetParamString(NetEvent e, string field)
            => NetParamOf(e, field)?.StringValue;

        private void NetOnRoomState(MsgRoomState room)
        {
            if (room == null) return;
            // 选卡组相位：服务器权威倒计时整秒校准转发（面板本地走字+此处校准）
            if (room.Phase == (int)NetRoomPhase.DeckSubmit)
                _opening?.NetDeckPanel?.OnServerSeconds(room.DeckPickSecondsLeft);
            // 对局中断线作废（M2 策略）：服务器广播 Error+Finished——留守侧收口为终局态
            if (room.Phase == (int)NetRoomPhase.Finished && !_gameEnded)
            {
                _gameEnded = true;
                MarkGameOverUi("对局作废（对手断线/房间关闭）——返回主菜单");
            }
        }

        private void NetOnDisconnected()
        {
            if (_gameEnded) return;
            _gameEnded = true;
            MarkGameOverUi("连接断开——对局作废（断线重连属后续里程碑）");
        }

        /// <summary>网络局收口（OnExit）：拆连接；房主侧会话服务器一并停机（重开=回房间屏重建）。</summary>
        private void NetShutdown()
        {
            if (_netClient != null)
            {
                _netClient.OnSnapshot -= NetOnSnapshot;
                _netClient.OnEventBatch -= NetOnEvents;
                _netClient.OnSelectRequest -= NetOnSelectRequest;
                _netClient.OnTextRequest -= NetOnTextRequest;
                _netClient.OnRoomState -= NetOnRoomState;
                _netClient.OnManifest -= NetOnManifest;
                _netClient.OnDisconnected -= NetOnDisconnected;
                NetClientRuntime.Unbind(_netClient);
                _netClient.Close();
                _netClient = null;
            }
            NetSessionServer.Current?.Stop(); // 房主侧：非 host 为 no-op
            _netSnapshot = null;
        }

    }
}
