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
        public string CostText;
        public string SupertypeText;
        public int ArrowFlags; // 屏幕六向箭头位（对手侧已按 LinkAuraSystem 同规镜像）

        // 派生显示
        public int X = -1, Z = -1;  // 棋盘格位（-1=未落格）
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
        public BattleCardView Skill; // 英雄技能卡（FieldZone）
        public bool IsAI;
    }

    /// <summary>对战整局视图。</summary>
    public sealed class BattleViewData
    {
        public int TurnNumber;
        public PhaseType Phase;
        public bool MyTurn;       // 行动方=我（本地=P1 回合；网络=ActiveSeat==viewerSeat）
        public bool MyPriority;   // 优先权在我（网络响应窗口；本地=栈优先权持有者）
        public bool StackNotEmpty;
        public int NetViewerSeat = -1; // 网络模式=快照 ViewerSeat（本地 -1）

        public BattlePlayerView Self = new BattlePlayerView();
        public BattlePlayerView Opp = new BattlePlayerView();

        public List<BattleCardView> SelfUnits = new List<BattleCardView>();
        public List<BattleCardView> OppUnits = new List<BattleCardView>();
        public List<BattleCardView> SelfLands = new List<BattleCardView>();
        public List<BattleCardView> OppLands = new List<BattleCardView>();
        public List<BattleCardView> SelfHand = new List<BattleCardView>(); // 己方手牌（正面）
        public List<string> StackLines = new List<string>();               // 栈条目摘要
        public List<string> ActivationTexts = new List<string>();          // 发动区摘要

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
        // ---- 中文映射（展示串中文） ----
        private static readonly Dictionary<PhaseType, string> PhaseNames = new Dictionary<PhaseType, string>
        {
            { PhaseType.Standby, "准备" }, { PhaseType.Main, "主要" }, { PhaseType.End, "结束" },
        };
        private static readonly Dictionary<ManaType, string> ManaNames = new Dictionary<ManaType, string>
        {
            { ManaType.Gray, "灰" }, { ManaType.Red, "红" }, { ManaType.Blue, "蓝" },
            { ManaType.Green, "绿" }, { ManaType.White, "白" }, { ManaType.Black, "黑" },
        };

        public static string PhaseZh(PhaseType p) => PhaseNames.TryGetValue(p, out var s) ? s : p.ToString();
        public static string ManaZh(ManaType m) => ManaNames.TryGetValue(m, out var s) ? s : m.ToString();

        /// <summary>声明费用字典 → 中文（如「红2 灰1」；空=0）。</summary>
        public static string CostTextOf(Dictionary<int, float> cost)
        {
            if (cost == null || cost.Count == 0) return "0";
            return string.Join(" ", cost
                .Where(kv => kv.Value > 0)
                .Select(kv => $"{ManaZh((ManaType)kv.Key)}{kv.Value:0.#}"));
        }

        private static readonly Dictionary<Cardtype, string> SupertypeNames = new Dictionary<Cardtype, string>
        {
            { Cardtype.Creature, "生物" }, { Cardtype.Spell, "法术" }, { Cardtype.Enchantment, "结界" },
            { Cardtype.Artifact, "装备" }, { Cardtype.Land, "地" }, { Cardtype.Field, "场地" },
        };

        public static string SupertypeZh(Cardtype t) => SupertypeNames.TryGetValue(t, out var s) ? s : t.ToString();

        // 英雄技能卡中文名（ID=HEROSKILL_+枚举名；引擎内置生成不进卡表）
        private static readonly Dictionary<string, string> SkillChipNames = new Dictionary<string, string>
        {
            { "HEROSKILL_BlueInsight", "蓝·洞察" },
            { "HEROSKILL_GreenCultivate", "绿·培育" },
            { "HEROSKILL_RedFrenzy", "红·狂热" },
        };

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

            var data = CardCatalog.GetById(s.ID);
            if (data != null)
            {
                v.Name = string.IsNullOrEmpty(data.CardName) ? data.ID : data.CardName;
                v.CostText = CostTextOf(data.Cost);
                v.SupertypeText = SupertypeZh(data.Supertype);
                v.ArrowFlags = ScreenArrowBits(data.ArrowDirections, ownerIsOpponent);
            }
            else if (!string.IsNullOrEmpty(s.ID) && s.ID.StartsWith("HEROSKILL_"))
            {
                // 英雄技能卡（引擎内置生成，不在 Cards.json）：ID 直译中文名
                v.Name = SkillChipNames.TryGetValue(s.ID, out var zh) ? zh : s.ID;
                v.SupertypeText = "结界";
            }
            else
            {
                v.Name = s.ID; // 查表 miss（如临时卡/token）退化为 ID
            }

            if (s.RemainingLandTokens != null && s.RemainingLandTokens.Length > 0)
            {
                var parts = s.RemainingLandTokens.Where(t => t.Value > 0)
                    .Select(t => $"{ManaZh((ManaType)t.ManaType)}{t.Value}").ToList();
                if (parts.Count > 0) v.LandTokensText = string.Join(" ", parts);
            }

            v.IsDead = s.Life <= 0;
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

            foreach (var p in new[] { self, opp })
            {
                foreach (var card in new List<Card>(core.ZoneManager.GetCards(p, Zone.Activation)))
                {
                    var v = FromRuntime(SerializableRuntimeCardState.FromCard(card), card, p == opp);
                    d.ActivationTexts.Add($"{(p == self ? "我方" : "对手")}发动：{v.Name}");
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

            var skill = HeroSkillSystem.ResolveSkillCard(core, p);
            if (skill != null)
                v.Skill = FromRuntime(SerializableRuntimeCardState.FromCard(skill), skill, core.Player2 == p);
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
                if (board != null && board.TryGetCell(land.SourceCard, out x, out z)) { view.X = x; view.Z = z; }

                // 池包装的横置态/剩余指示物是权威（FromCard 读的是卡内字段）
                var parts = land.Tokens?
                    .Where(kv => kv.Value > 0)
                    .Select(kv => $"{ManaZh(kv.Key)}{kv.Value}").ToList();
                if (parts != null && parts.Count > 0) view.LandTokensText = string.Join(" ", parts);
                view.Runtime.IsTapped = land.IsTapped;

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
        // 网络模式构建（MsgGameStateSync 快照；格位=ZoneCards 列表序按
        // BoardLayout.UnitCells/LandCells first-free 规则重建——与服务器
        // BoardState.Resync 同源规则，逐格一致，零协议改动）
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
            };

            FillPlayerNet(d.Self, snap, me);
            FillPlayerNet(d.Opp, snap, them);

            FillUnitsNet(d.SelfUnits, snap, me, ownerIsOpponent: false);
            FillUnitsNet(d.OppUnits, snap, them, ownerIsOpponent: true);
            FillLandsNet(d.SelfLands, snap, me, ownerIsOpponent: false);
            FillLandsNet(d.OppLands, snap, them, ownerIsOpponent: true);
            FillHandNet(d.SelfHand, snap, me);

            foreach (var seat in new[] { me, them })
            {
                foreach (var a in ZoneCardsNet(snap, seat, Zone.Activation))
                    d.ActivationTexts.Add($"{(seat == me ? "我方" : "对手")}发动：{FromRuntime(a, null, seat != me).Name}");
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
            v.BankText = ps.ElementBank == null || ps.ElementBank.Length == 0 ? "—"
                : string.Join(" ", ps.ElementBank
                    .Where(e => e.Value > 0)
                    .Select(e => $"{ManaZh((ManaType)e.ManaType)}{e.Value}"));

            var fields = ZoneCardsNet(snap, seat, Zone.FieldZone);
            if (fields.Length > 0)
                v.Skill = FromRuntime(fields[0], null, seat != snap.ViewerSeat);
        }

        private static void FillUnitsNet(List<BattleCardView> list, MsgGameStateSync snap, int seat, bool ownerIsOpponent)
        {
            // 列表序 i → UnitCells(seat)[i]（与服务器 AssignCells 同规 first-free）
            var units = ZoneCardsNet(snap, seat, Zone.Battlefield);
            var cells = BoardLayout.UnitCells(seat);
            for (int i = 0; i < units.Length; i++)
            {
                var view = FromRuntime(units[i], null, ownerIsOpponent);
                if (i < cells.Count) { view.X = cells[i].x; view.Z = cells[i].z; }
                list.Add(view);
            }
        }

        private static void FillLandsNet(List<BattleCardView> list, MsgGameStateSync snap, int seat, bool ownerIsOpponent)
        {
            var lands = ZoneCardsNet(snap, seat, Zone.ElementPool);
            var cells = BoardLayout.LandCells(seat);
            for (int i = 0; i < lands.Length; i++)
            {
                var view = FromRuntime(lands[i], null, ownerIsOpponent);
                if (i < cells.Count) { view.X = cells[i].x; view.Z = cells[i].z; }
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
            var data = string.IsNullOrEmpty(r.CardId) ? null : CardCatalog.GetById(r.CardId);
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
}
