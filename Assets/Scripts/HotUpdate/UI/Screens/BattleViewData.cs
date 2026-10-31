using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.Network;
using CardCore.Serialization;
using GameBoard;

namespace SynergyUI
{
    // ============================================================
    // 对战视图模型（2026-09-24 阶段四重做）：
    // 界面渲染的唯一输入——本地（GameCore 直连）与网络（MsgGameStateSync 快照）
    // 两种数据源各自构建本模型，渲染层不接触引擎对象与网络 DTO。
    // 运行态统一走 SerializableRuntimeCardState 口径（本地 FromCard 导出），
    // 静态信息（名/费用/箭头）按 CardId 查本地卡表——不依赖 Cards.json 静态关键词字段，
    // 另一会话的关键词迁移（keywords→effectIds）对本层无感。
    // ============================================================

    /// <summary>对战卡视图。</summary>
    public sealed class BattleCardView
    {
        public uint RuntimeId;
        public string CardId;
        public Card CoreCard;                        // 本地模式引擎对象（交互回调用）；网络模式 null
        public SerializableRuntimeCardState Runtime; // 运行态（两种模式统一口径）

        // 静态信息（CardCatalog 查表填充；miss 时退化为 ID）
        public string Name;
        public string Illustration; // 立绘名（=生物模型目录名；战场 3D 按此优先解析，2026-10-13）
        public string CostText;
        public string SupertypeText;
        public int ArrowFlags; // 屏幕六向箭头位（对手侧已按 LinkAuraSystem 同规镜像）
        public int ArrowDirs; // 原始卡面箭头位（HexDirection，未镜像——战场 3D 六向三角消费，2026-10-14）
        public Cardtype Type;  // 卡类型（战场 3D 区分生物模型/结界模型，2026-10-14；查表 miss 时=default 保持生物路径）
        public bool HasAura;   // 有链接光环效果（聚合缓存 LinkAuras 非空——结界光环环显隐，2026-10-14）
        public int? BasePower, BaseLife; // 印刷基础攻/血（卡表原值；2026-10-15 战场头顶属性配色：
                                         // 持平=白/低于=红/高于=绿；查表 miss=null 恒白）
        public int Armor;                // 护甲指示物层数（KeywordRules.ArmorCounter；2026-10-16
                                         // 战场头顶「(+x)」后缀显示，双模式统一从 Counters 快照取）

        // 派生显示
        public int X = -1, Z = -1;  // 棋盘格位（-1=未落格；地牌=同色组共享格，2026-10-14 按色叠格）
        public string LandTokensText;
        public bool IsDead;         // 战场尸体（标死未收）

        public bool IsTapped => Runtime != null && Runtime.IsTapped;
        public bool IsFrozen => Runtime != null && Runtime.IsFrozen;
        public string StatsText => Runtime != null
            ? $"{Runtime.Power}/{Runtime.Life}" + (IsTapped ? " 横" : "") + (IsFrozen ? " 冻" : "") + (IsDead ? " †" : "")
            : "";
    }

    /// <summary>对战玩家视图（公开面；己方手牌挂在整局视图上）。</summary>
    public sealed class BattlePlayerView
    {
        public string Name = "";
        public int Life, MaxHealth, DeckCount, HandCount, GraveyardCount, FatigueCount, LandCap;
        public string BankText = "—";
        public int[] WalletByColor = new int[ElementCost.Length]; // 临时钱包逐色余额（2026-10-14 上盘：
                                                                  // 地牌格「剩余」并入钱包该色、技能格黑白数字源）
        public BattleCardView Skill; // 英雄技能卡（FieldZone）
        public bool IsAI;
        public int HeroAttack; // 角色攻击力=HeroAttackCounter 弹药层数（2026-10-10 角色参战收口：0=无攻击资格，3D HUD 消费）
        public int Armor;      // 角色护甲层数（ArmorCounter 指示物；2026-10-16 立柱生命「(+x)」后缀显示）
        public CounterEntryDTO[] Counters; // 角色层全量指示物（2026-10-17 角色状态展示补齐）：本地=Entity.EnumerateCounters
                                           // 投影、网络=PlayerState.Counters 回填（协议早已携带）；立柱摘要/悬停状态段消费
    }

    /// <summary>对战整局视图。</summary>
    public sealed class BattleViewData
    {
        public int TurnNumber;
        public PhaseType Phase;
        public bool MyTurn;       // 行动方=我（本地=P1 回合；网络=ActiveSeat==viewerSeat）
        public bool MyPriority;   // 优先权在我（网络响应窗口；本地=栈优先权持有者）
        public bool StackNotEmpty;
        public int StackTopOwnerSeat = -1; // 栈顶效果归属方（观察者相对 0=己方 1=对手；-1=空栈/网络快照未携带）。
                                          // 2026-10-26 扳机锚侧定案：半场光条亮**发动方**（对手触发的效果亮对手侧——
                                          // 人机 AI 同帧让过持有者恒翻己方，旧「可响应方」锚法恒误亮）；时间格角标与
                                          //「优先权在我」仍锚可响应方（MyPriority）。网络快照未携带恒 -1=回落旧口径
        public int ChainSpeed = -1; // 记速器当前值（2026-10-15 战场接线）：本地=StackEngine.SpeedCounter.CurrentSpeed；
                                    // -1=网络快照未承载（快照协议无此字段）→战场占位「—」
        public bool ChainResolving; // 记速器结算中（连锁结算中禁止新的速度发动入栈）
        public int NetViewerSeat = -1; // 网络模式=快照 ViewerSeat（本地 -1）

        public BattlePlayerView Self = new BattlePlayerView();
        public BattlePlayerView Opp = new BattlePlayerView();

        public List<BattleCardView> SelfUnits = new List<BattleCardView>();
        public List<BattleCardView> OppUnits = new List<BattleCardView>();
        public List<BattleCardView> SelfLands = new List<BattleCardView>();
        public List<BattleCardView> OppLands = new List<BattleCardView>();
        public List<BattleCardView> SelfHand = new List<BattleCardView>(); // 己方手牌（正面）
        public List<string> StackLines = new List<string>();               // 栈条目摘要
        public List<BattleCardView> CastingCards = new List<BattleCardView>(); // 施放占格卡
        //（2026-10-15 发动格拆除：发动区占位卡 × 单位区空格立牌——整个 2×6 兼任发动区；
        // 旧 ActivationTexts 文本摘要随立牌呈现退役，写入点 lbl-activation 本就静默 null）

        public List<BattleCardView> ChainCards = new List<BattleCardView>(); // 连锁链卡牌施放项
        //（2026-10-19 chain UI 定案：栈上 IsCardCast 项，底→顶序——攻击/守卫/触发效果/SBA 不占
        // 链位；随栈实时伸缩（结算出栈即短），链舞台在结算期按「只增不删」口径消费；网络快照
        // 未承载恒空）

        public bool GameOver;
        public string GameOverTitle, GameOverText;

        public string PhaseText
        {
            get
            {
                string who = MyTurn ? "我方" : "对手";
                return $"回合 {TurnNumber} · {who}的{BattleView.PhaseZh(Phase)}阶段";
            }
        }
    }

    /// <summary>视图构建与格位/文案映射（本地与网络两入口）。</summary>
    public static class BattleView
    {
        // ---- 中文映射（展示串中文；PhaseZh/ManaZh 已收敛 CardCore 单源——2026-10-18 B1 日志中间层） ----
        private static readonly Dictionary<ManaType, string> ManaNames = new Dictionary<ManaType, string>
        {
            { ManaType.Gray, "灰" }, { ManaType.Red, "红" }, { ManaType.Blue, "蓝" },
            { ManaType.Green, "绿" },
            { ManaType.Any, "任意" }, // 万能货币（2026-10-23）：钱包串显示「任意N」（白/黑已删 2026-10-28 P2）
        };

        public static string PhaseZh(PhaseType p) => CardCore.MatchLogRenderer.PhaseZh(p);
        public static string ManaZh(ManaType m) => ManaNames.TryGetValue(m, out var s) ? s : m.ToString();

        /// <summary>声明费用（位置数组）→ 中文（如「红2 灰1」；序数序=枚举序；空=0）。</summary>
        public static string CostTextOf(ElementCost cost)
        {
            if (cost == null || cost.IsZero) return "0";
            return string.Join(" ", cost.NonzeroColors()
                .Select(c => $"{ManaZh(c)}{cost[c]:0.#}"));
        }

        private static readonly Dictionary<Cardtype, string> SupertypeNames = new Dictionary<Cardtype, string>
        {
            { Cardtype.Creature, "生物" }, { Cardtype.Spell, "法术" }, { Cardtype.Enchantment, "结界" },
            { Cardtype.Artifact, "装备" }, { Cardtype.Land, "地" }, { Cardtype.Field, "场地" },
        };

        public static string SupertypeZh(Cardtype t) => SupertypeNames.TryGetValue(t, out var s) ? s : t.ToString();

        // 英雄技能卡中文名映射已删（2026-10-07 卡牌化）：技能=构筑标记的真实结界卡，
        // 名字/费用走 CardCatalog 正常解析（旧 HEROSKILL_* 合成 ID 随三色硬编码技能退役）。

        // ---- 箭头：卡面六向（HexDirection）→ 屏幕六向位 ----
        // 位序：NE=1 E=2 SE=4 SW=8 W=16 NW=32（我方视角=棋盘绝对方向）。
        // 对手侧镜像与 LinkAuraSystem.cs:253 同规（归属 1 的箭头取 Opposite）。
        public const int ArrowNE = 1, ArrowE = 2, ArrowSE = 4, ArrowSW = 8, ArrowW = 16, ArrowNW = 32;

        public static int ScreenArrowBits(HexDirection dirs, bool ownerIsOpponent)
        {
            var board = BoardMath.MapArrow(dirs & HexDirection.All);
            if (ownerIsOpponent) board = BoardMath.Opposite(board);
            switch (board)
            {
                case BoardDirection.NE: return ArrowNE;
                case BoardDirection.E: return ArrowE;
                case BoardDirection.SE: return ArrowSE;
                case BoardDirection.SW: return ArrowSW;
                case BoardDirection.W: return ArrowW;
                case BoardDirection.NW: return ArrowNW;
                default: return 0;
            }
        }

        /// <summary>运行态+静态信息合并构建卡视图。coreCard 本地引擎对象（网络传 null）。</summary>
        public static BattleCardView FromRuntime(SerializableRuntimeCardState s, Card coreCard, bool ownerIsOpponent)
        {
            var v = new BattleCardView
            {
                RuntimeId = s.RuntimeId,
                CardId = s.ID,
                CoreCard = coreCard,
                Runtime = s,
            };

            var data = CardCatalog.GetByInstanceId(s.ID);
            if (data != null)
            {
                v.Name = string.IsNullOrEmpty(data.CardName) ? data.ID : data.CardName;
                v.Illustration = data.Illustration;
                v.CostText = CostTextOf(data.Cost);
                v.SupertypeText = SupertypeZh(data.Supertype);
                v.ArrowFlags = ScreenArrowBits(data.ArrowDirections, ownerIsOpponent);
                v.ArrowDirs = (int)data.ArrowDirections;
                v.Type = data.Supertype;
                v.HasAura = data.LinkAuras != null && data.LinkAuras.Count > 0; // 加载侧已聚合（CardLoader.AggregateEffectAuras）
                v.BasePower = data.Power; // 印刷基础值（属性配色比较基准）
                v.BaseLife = data.Life;
                if (data.Supertype == Cardtype.Enchantment)
                    v.BaseLife = CardCompositionCost.TotalDurabilityOf(data); // 2026-10-17 恒耐久定案：头顶生命位=耐久，基准=派生总耐久
            }
            else
            {
                v.Name = s.ID; // 查表 miss（如临时卡/token）退化为 ID
            }

            // 2026-10-11 锁色定案：指示物余量→锁定产色（-1=非地牌）
            if (s.LockedColor >= 0)
                v.LandTokensText = $"锁:{ManaZh((ManaType)s.LockedColor)}";

            // 2026-10-16 修（结界悬停无信息根因）：非生物卡不再按 Life<=0 误判为战场尸体——
            // 无生命单位（结界/装备）_life 恒 0（耐久在指示物）、法术无生命字段，死亡由离场表达
            //（耐久归零 Smashed 直毁入墓/法术结算即走），在场即活；data==null（token/临时卡）保守沿用旧口径。
            v.IsDead = data != null && data.Supertype != Cardtype.Creature ? false : s.Life <= 0;
            v.Armor = Math.Max(0, s.Counters?
                .FirstOrDefault(c => c.Key == CardCore.Attribute.KeywordRules.ArmorCounter)?.Value ?? 0);
            return v;
        }

        // ============================================================
        // 本地模式构建（GameCore 直连；格位=BoardState 权威值）
        // ============================================================
        public static BattleViewData BuildLocal(GameCore core, BoardState board, Player self, Player opp)
        {
            var d = new BattleViewData
            {
                TurnNumber = core.TurnEngine.TurnNumber,
                Phase = core.TurnEngine.CurrentPhase?.Phase ?? PhaseType.Standby,
                MyTurn = core.TurnEngine.TurnPlayer == self,
                MyPriority = core.StackEngine?.CurrentPriorityHolder == self,
                StackNotEmpty = core.StackEngine != null && core.StackEngine.StackSize > 0,
            };

            // 栈顶归属（2026-10-26 扳机锚侧定案）：EffectInstance.Controller——各入栈路径（速度发动/触发/挂起）
            // 均赋值；空栈/取不到保持 -1（消费侧回落旧持有方口径）
            var stackTop = core.StackEngine?.PeekStack();
            if (stackTop != null && stackTop.Controller != null)
                d.StackTopOwnerSeat = stackTop.Controller == self ? 0 : 1;

            // 记速器（2026-10-15 战场接线）：连锁速度格的实值源——当前连锁已发动的最高速度
            var speedCounter = core.StackEngine?.SpeedCounter;
            if (speedCounter != null)
            {
                d.ChainSpeed = speedCounter.CurrentSpeed;
                d.ChainResolving = speedCounter.IsResolving;
            }

            FillPlayerLocal(d.Self, core, self);
            FillPlayerLocal(d.Opp, core, opp);

            foreach (var card in new List<Card>(core.ZoneManager.GetCards(self, Zone.Battlefield)))
                d.SelfUnits.Add(UnitViewLocal(core, board, card, ownerIsOpponent: false));
            foreach (var card in new List<Card>(core.ZoneManager.GetCards(opp, Zone.Battlefield)))
                d.OppUnits.Add(UnitViewLocal(core, board, card, ownerIsOpponent: true));

            FillLandsLocal(d.SelfLands, core, board, self, ownerIsOpponent: false);
            FillLandsLocal(d.OppLands, core, board, opp, ownerIsOpponent: true);

            foreach (var card in new List<Card>(core.ZoneManager.GetCards(self, Zone.Hand)))
                d.SelfHand.Add(FromRuntime(SerializableRuntimeCardState.FromCard(card), card, false));

            // 施放占格（2026-10-15 发动格拆除）：发动区占位卡 × BoardState 落格（钉优先→first-free）
            foreach (var p in new[] { self, opp })
            {
                foreach (var card in new List<Card>(core.ZoneManager.GetCards(p, Zone.Activation)))
                {
                    int cx, cz;
                    if (board == null || !board.TryGetCastingCell(card, out cx, out cz)) continue; // 无空格不立牌
                    var v = FromRuntime(SerializableRuntimeCardState.FromCard(card), card, p == opp);
                    v.X = cx;
                    v.Z = cz;
                    d.CastingCards.Add(v);
                }
            }

            // 连锁链（2026-10-19 chain UI）：栈上卡牌施放项，底→顶——SnapshotStack 为顶→底故倒序入列；
            // 归属按施放者（EffectInstance.Controller）判观侧
            var stackItems = core.StackEngine?.SnapshotStack();
            if (stackItems != null)
            {
                for (int i = stackItems.Count - 1; i >= 0; i--)
                {
                    var item = stackItems[i];
                    if (item == null || !item.IsCardCast || !(item.Source is Card chainCard)) continue;
                    d.ChainCards.Add(FromRuntime(SerializableRuntimeCardState.FromCard(chainCard),
                        chainCard, item.Controller == opp));
                }
            }

            d.StackLines.Add(d.StackNotEmpty ? $"栈：{core.StackEngine.StackSize} 个对象" : "栈：空");
            return d;
        }

        private static void FillPlayerLocal(BattlePlayerView v, GameCore core, Player p)
        {
            v.Name = p?.Name ?? "?";
            v.Life = p.Life;
            v.MaxHealth = p.MaxHealth;
            v.IsAI = p.IsAI;
            v.DeckCount = Count(core, p, Zone.Deck);
            v.HandCount = Count(core, p, Zone.Hand);
            v.GraveyardCount = Count(core, p, Zone.Graveyard);
            v.FatigueCount = p.FatigueCount;
            v.LandCap = core.ElementPool.GetLandCap(p);
            v.BankText = BankTextLocal(core, p);
            foreach (ManaType m in Enum.GetValues(typeof(ManaType)))
                v.WalletByColor[(int)m] = core.ElementPool.GetAvailableManaCount(m, p);
            v.HeroAttack = p.GetPower(); // Player 分支=弹药层数钳非负（EntityEffectExtensions）
            v.Armor = Math.Max(0, p.GetCounterCount(CardCore.Attribute.KeywordRules.ArmorCounter));
            v.Counters = p.EnumerateCounters() // 角色层全量指示物（滤 0 层——展示口径与卡快照一致）
                .Select(kvp => new CounterEntryDTO { Key = kvp.Key, Value = kvp.Value }).ToArray();

            var skill = HeroSkillSystem.ResolveSkillCard(core, p);
            if (skill != null)
            {
                v.Skill = FromRuntime(SerializableRuntimeCardState.FromCard(skill), skill, core.Player2 == p);
                // 技能栏费用=各主动效果的派生发动价（2026-10-15 改版：技能=法术可挂 1~3 效果——
                // 多效果显示「最低~最高」区间，发动时弹窗择一现付；单效果只显示该价）
                var anchors = HeroSkillSystem.SkillEffectsOf(skill)
                    .Select(fx => CostDerivationService.DeriveElementCosts(fx)?.Total ?? 0f)
                    .Where(t => t >= 0f).ToList();
                if (anchors.Count == 0) v.Skill.CostText = "0";
                else if (anchors.Count == 1) v.Skill.CostText = CostTextOf(ElementCost.FromValue(ManaType.Gray, anchors[0])); // 真实小数直显（2026-10-30）
                else v.Skill.CostText = $"{anchors.Min():0.#}~{anchors.Max():0.#}";
            }
        }

        private static BattleCardView UnitViewLocal(GameCore core, BoardState board, Card card, bool ownerIsOpponent)
        {
            var view = FromRuntime(SerializableRuntimeCardState.FromCard(card), card, ownerIsOpponent);
            int x, z;
            if (board != null && board.TryGetCell(card, out x, out z)) { view.X = x; view.Z = z; }
            return view;
        }

        private static void FillLandsLocal(List<BattleCardView> list, GameCore core, BoardState board,
            Player p, bool ownerIsOpponent)
        {
            var pooled = core.ElementPool.GetPooledCards(p);
            if (pooled == null) return;
            foreach (var land in pooled)
            {
                if (land?.SourceCard == null) continue;
                var view = FromRuntime(SerializableRuntimeCardState.FromCard(land.SourceCard), land.SourceCard, ownerIsOpponent);
                int x, z;
                if (board != null && board.TryGetCell(land.SourceCard, out x, out z))
                {
                    view.X = x;
                    view.Z = z; // 同色组共享格（2026-10-14 按色叠格；BoardState 权威）
                }

                // 池包装的锁色/横置态是权威（卡上无地牌字段）：覆写进 Runtime（显示与快照同源）
                view.Runtime.LockedColor = (int)land.LockedColor;
                view.Runtime.IsTapped = land.IsTapped;
                view.LandTokensText = $"锁:{ManaZh(land.LockedColor)}";

                list.Add(view);
            }
        }

        private static int Count(GameCore core, Player p, Zone z)
            => core.ZoneManager.GetCards(p, z)?.Count ?? 0;

        private static string BankTextLocal(GameCore core, Player p)
        {
            var parts = new List<string>();
            foreach (ManaType type in Enum.GetValues(typeof(ManaType)))
            {
                int n = core.ElementPool.GetAvailableManaCount(type, p);
                if (n > 0) parts.Add($"{ManaZh(type)}{n}");
            }
            return parts.Count > 0 ? string.Join(" ", parts) : "—";
        }

        // ============================================================
        // 网络模式构建（MsgGameStateSync 快照；格位=ZoneCards 列表序 first-free
        // 重建后**归一到观察者视角**（己方=P1 半场 z4/5/6、对手=P2 半场 z1/2/3）——
        // 服务器 BoardState 是绝对坐标，直接渲染会让 1 号座位观察者战场全空
        // （2026-09-30 修复；零协议改动）
        // ============================================================
        public static BattleViewData BuildNet(MsgGameStateSync snap)
        {
            int me = snap.ViewerSeat, them = 1 - me;
            var d = new BattleViewData
            {
                TurnNumber = snap.CurrentTurn,
                Phase = (PhaseType)snap.CurrentPhase,
                MyTurn = snap.ActiveSeat == me,
                MyPriority = snap.PrioritySeat == me,
                StackNotEmpty = snap.StackV2 != null && snap.StackV2.Length > 0,
                NetViewerSeat = me,
                // ChainSpeed 保持 -1：MsgGameStateSync 未承载记速器（网络局占位「—」）——
                // 联机复活时随协议补字段在此回填
            };

            FillPlayerNet(d.Self, snap, me);
            FillPlayerNet(d.Opp, snap, them);

            FillUnitsNet(d.SelfUnits, snap, me, ownerIsOpponent: false);
            FillUnitsNet(d.OppUnits, snap, them, ownerIsOpponent: true);
            FillLandsNet(d.SelfLands, snap, me, ownerIsOpponent: false);
            FillLandsNet(d.OppLands, snap, them, ownerIsOpponent: true);
            FillHandNet(d.SelfHand, snap, me);

            // 施放占格（2026-10-15 发动格拆除）：网络侧落格=观察者视角 first-free 近似
            //（快照不承载 BoardState 钉格；跳过该方单位已占格——与 FillUnitsNet 列表序对应）
            foreach (var seat in new[] { me, them })
            {
                var casting = ZoneCardsNet(snap, seat, Zone.Activation);
                if (casting.Length == 0) continue;
                bool isOpp = seat != me;
                var cells = BoardLayout.UnitCells(isOpp ? 1 : 0);
                int i = ZoneCardsNet(snap, seat, Zone.Battlefield).Length;
                foreach (var s in casting)
                {
                    if (i >= cells.Count) break; // 无空格不立牌
                    var v = FromRuntime(s, null, isOpp);
                    v.X = cells[i].x;
                    v.Z = cells[i].z;
                    d.CastingCards.Add(v);
                    i++;
                }
            }

            if (snap.StackV2 != null)
            {
                foreach (var item in snap.StackV2)
                {
                    string src = EntityTextOf(item.Source, me);
                    string eff = string.IsNullOrEmpty(item.EffectId) ? "" : $"·{EffectShortName(item.EffectId)}";
                    d.StackLines.Add($"{src}{eff}{(item.IsCardCast ? "（施放）" : "")}");
                }
            }
            if (d.StackLines.Count == 0) d.StackLines.Add("栈：空");
            return d;
        }

        private static void FillPlayerNet(BattlePlayerView v, MsgGameStateSync snap, int seat)
        {
            var ps = snap.Players?.FirstOrDefault(p => p.Seat == seat);
            if (ps == null) return;
            v.Name = ps.Name;
            v.Life = ps.Life;
            v.MaxHealth = ps.MaxHealth;
            v.DeckCount = ps.DeckCount;
            v.HandCount = ps.HandCount;
            v.GraveyardCount = ps.GraveyardCount;
            v.FatigueCount = ps.FatigueCount;
            v.LandCap = ps.LandCap;
            v.IsAI = ps.IsAI;
            v.HeroAttack = Math.Max(0, ps.Counters?.FirstOrDefault(c => c.Key == CardCore.Attribute.CounterRules.HeroAttackCounter)?.Value ?? 0);
            v.Armor = Math.Max(0, ps.Counters?.FirstOrDefault(c => c.Key == CardCore.Attribute.KeywordRules.ArmorCounter)?.Value ?? 0);
            v.Counters = ps.Counters; // 角色层全量指示物（协议已携带，直接回填视图模型）
            v.BankText = ps.ElementBank == null || ps.ElementBank.Length == 0 ? "—"
                : string.Join(" ", ps.ElementBank
                    .Where(e => e.Value > 0)
                    .Select(e => $"{ManaZh((ManaType)e.ManaType)}{e.Value}"));
            foreach (var e in ps.ElementBank ?? Array.Empty<ManaEntryDTO>())
                if (e.ManaType >= 0 && e.ManaType < v.WalletByColor.Length)
                    v.WalletByColor[e.ManaType] = e.Value;

            var fields = ZoneCardsNet(snap, seat, Zone.FieldZone);
            if (fields.Length > 0)
                v.Skill = FromRuntime(fields[0], null, seat != snap.ViewerSeat);
        }

        private static void FillUnitsNet(List<BattleCardView> list, MsgGameStateSync snap, int seat, bool ownerIsOpponent)
        {
            // 列表序 i → **观察者视角**格位：己方恒 P1 半场（z3/4）、对手恒 P2 半场（z1/2）。
            // 服务器 BoardState.Resync 是绝对坐标（seat1 己方=z1/2）——若直接填绝对坐标，
            // 1 号座位观察者的整片战场（双方单位+地牌）都落不进渲染行、恒空白（2026-09-30 修复）。
            // 列表序与服务器 AssignCells first-free 逐格对应，观察者侧仅做半场归一。
            // 2026-10-28 网络选格：快照不承载坐标（位置权威=客户端表现层）——己方单位先落
            // NetCellPins 钉格（出牌/放地选格时写入），其余按格序 first-free 避开已占钉格。
            var units = ZoneCardsNet(snap, seat, Zone.Battlefield);
            var cells = BoardLayout.UnitCells(ownerIsOpponent ? 1 : 0);
            var used = new HashSet<int>();
            int CellKey(int x, int z) => x * 100 + z;
            if (!ownerIsOpponent)
            {
                // 钉格持久（同 BoardState._pinnedCells 语义：卡在场上每次重建优先回钉格）；
                // 离场卡（快照战场区无此 RuntimeId）钉子随之修剪
                NetCellPins.PruneTo(units);
                for (int i = 0; i < units.Length; i++)
                {
                    if (units[i] != null && NetCellPins.TryGet(units[i].RuntimeId, out var pin)
                        && cells.Any(c => c.x == pin.x && c.z == pin.z))
                        used.Add(CellKey(pin.x, pin.z));
                }
            }
            int next = 0;
            for (int i = 0; i < units.Length; i++)
            {
                var view = FromRuntime(units[i], null, ownerIsOpponent);
                // 场地卡（2026-10-29 P8）：落位恒为己方场地格（确定性映射，无需钉格——
                // 与服务器 BoardState.AssignCells 的 FieldCell 分支同构；不占单位区格位）
                var fdata = CardCatalog.GetByInstanceId(units[i].ID);
                if (fdata != null && fdata.IsField)
                {
                    var fc = BoardLayout.FieldCell(ownerIsOpponent ? 1 : 0);
                    view.X = fc.x;
                    view.Z = fc.z;
                    list.Add(view);
                    continue;
                }
                if (!ownerIsOpponent && NetCellPins.TryGet(units[i].RuntimeId, out var pinned)
                    && cells.Any(c => c.x == pinned.x && c.z == pinned.z))
                {
                    view.X = pinned.x;
                    view.Z = pinned.z;
                }
                else
                {
                    while (next < cells.Count && used.Contains(CellKey(cells[next].x, cells[next].z))) next++;
                    if (next < cells.Count)
                    {
                        view.X = cells[next].x;
                        view.Z = cells[next].z;
                        used.Add(CellKey(cells[next].x, cells[next].z));
                        next++;
                    }
                }
                list.Add(view);
            }
        }

        private static void FillLandsNet(List<BattleCardView> list, MsgGameStateSync snap, int seat, bool ownerIsOpponent)
        {
            // 同 FillUnitsNet：观察者视角归一（己方地牌行 z6、对手 z0）。
            // 固定元素格（2026-10-14 晚定案）：与服务器 BoardState.AssignLandCells 同构——
            // 颜色 → BoardLayout.LandCellOfElement 固定格（灰红蓝绿黑白），同色全部同格。
            // 锁色权威在 Runtime.LockedColor（快照侧 BuildZone 覆写）；无锁色（-1）不落格。
            var lands = ZoneCardsNet(snap, seat, Zone.ElementPool);
            int owner = ownerIsOpponent ? 1 : 0;
            foreach (var s in lands)
            {
                var view = FromRuntime(s, null, ownerIsOpponent);
                if (s.LockedColor >= 0)
                {
                    var cell = BoardLayout.LandCellOfElement(owner, (ManaType)s.LockedColor);
                    if (cell.x >= 0) { view.X = cell.x; view.Z = cell.z; }
                }
                list.Add(view);
            }
        }

        private static void FillHandNet(List<BattleCardView> list, MsgGameStateSync snap, int seat)
        {
            var handCards = ZoneCardsNet(snap, seat, Zone.Hand);
            if (handCards.Length == 0) return;
            var own = snap.Hands?.FirstOrDefault(h => h.Seat == seat)?.OwnRuntimeIds;
            if (own == null) return;
            var byId = handCards.ToDictionary(c => c.RuntimeId);
            foreach (var rid in own)
                if (byId.TryGetValue(rid, out var s))
                    list.Add(FromRuntime(s, null, false));
        }

        private static SerializableRuntimeCardState[] ZoneCardsNet(MsgGameStateSync snap, int seat, Zone zone)
            => snap.ZoneCards?.FirstOrDefault(z => z.Seat == seat && (Zone)z.Zone == zone)?.Cards
               ?? new SerializableRuntimeCardState[0];

        /// <summary>网络栈/事件里的实体引用 → 短文本（玩家按座位、卡按 CardId 查表名）。</summary>
        public static string EntityTextOf(NetEntityRef r, int viewerSeat)
        {
            if (r == null) return "?";
            if (r.IsPlayer)
                return r.Seat == viewerSeat ? "我方" : "对手";
            var data = string.IsNullOrEmpty(r.CardId) ? null : CardCatalog.GetByInstanceId(r.CardId);
            string name = data != null && !string.IsNullOrEmpty(data.CardName) ? data.CardName : (r.CardId ?? "?");
            return $"{name}#{r.RuntimeId % 10000}";
        }

        /// <summary>效果短名（EffectId 查本地效果库；miss 取前 8 位）。</summary>
        public static string EffectShortName(string effectId)
        {
            if (string.IsNullOrEmpty(effectId)) return "?";
            return effectId.Length <= 10 ? effectId : effectId.Substring(0, 8) + "…";
        }
    }

    /// <summary>
    /// 网络局选格钉（2026-10-28 放地/永久物出牌选格）：快照不承载棋盘坐标（位置权威=客户端
    /// 表现层），本表登记本端为卡选定的落格，BuildNet/FillUnitsNet 消费（钉格须属己方单位区，
    /// 否则按无钉 first-free——与本地 BoardState._pinnedCells 同语义）。卡离场随快照修剪；
    /// 跨局残留由 BattleScreen.OnEnter（网络局）ClearAll 收口（RuntimeId 每局重起，防串号）。
    /// </summary>
    public static class NetCellPins
    {
        private static readonly Dictionary<uint, (int x, int z)> Pins = new Dictionary<uint, (int x, int z)>();

        public static void Pin(uint runtimeId, int x, int z)
        {
            if (runtimeId == 0) return;
            Pins[runtimeId] = (x, z);
        }

        public static bool TryGet(uint runtimeId, out (int x, int z) cell)
            => Pins.TryGetValue(runtimeId, out cell);

        /// <summary>修剪到当前战场快照（离场卡钉子清除）。</summary>
        public static void PruneTo(SerializableRuntimeCardState[] onBoard)
        {
            if (Pins.Count == 0) return;
            var alive = new HashSet<uint>();
            if (onBoard != null)
                foreach (var s in onBoard)
                    if (s != null) alive.Add(s.RuntimeId);
            if (alive.Count == 0) { Pins.Clear(); return; }
            var dead = Pins.Keys.Where(k => !alive.Contains(k)).ToList();
            foreach (var k in dead) Pins.Remove(k);
        }

        public static void ClearAll() => Pins.Clear();
    }
}
