using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CardCore;
using CardCore.Attribute;   // S3-0a：CardDiscardEvent 在 CardCore.Attribute（2026-11-26 编译修复）

namespace CardCore.AI.NeuralEnv
{
    /// <summary>
    /// S3-0a/0b 引擎侧只读「已应用动作行」探针（二期 S3-0，2026-11-26）。
    ///
    /// 背景（docs/二期训练架构_实施计划.md §5 S3-0a）：G5 的动作行记录只写在模型臂
    /// （g_sampling.py 的 sample/argmax 分支），脚本臂走整回合脚本驱动、Python 侧拿不到
    /// 逐步动作行——而 BC 的标签源正是脚本臂。本探针在**引擎事件层**只读订阅（零行为变化、
    /// 零 RNG 消耗），把脚本局（scriptVsScript）双方已应用的卡牌动作落成 jsonl 流：
    ///   {"rec":"act","game":N,"seat":0/1,"turn":T,"ot":OT,"kind":"PlayCard|PlayLand|Discard|
    ///    Attack|HeroSkill","card":"归根","cardId":"C_…","mode":N,"respCast":0/1,
    ///    "costT":8.0,"cost0..4":…（灰/红/蓝/绿/Any）,"wallet0..4":…,"lands":N,
    ///    "handGenel":N,"graveGenel":N}
    /// 每局首行 {"rec":"game",...} 记双方卡组归根张数（绿侧判据）、胜者、终局原因、回合数。
    ///
    /// S3-0b（T4 核对）与 S3-0c（BC shard 闸对账基准）用同一份流回答：
    ///   ① 脚本局「施放归根」次数/占比 ② 当地次数 ③ 施放时动态费（6.0/8.0 各占比）
    ///   ④ cast_genel_by_turn4 / 总局数（脚本打到第 4 回合时是否已施放归根）。
    ///
    /// 启用口（两通道，缺省全关=零开销）：
    ///   - 环境变量 TIDE_PROBE_ACTIONS=jsonl 路径（batchmode 起进程前设好）；
    ///   - 编辑器内静态赋值 ScriptActionProbe.ActionsLogPath（execute_code 配方）。
    /// 生命周期：driver.ResetCore 的 scriptVsScript 分支 BeginGame / finally EndGame
    /// （覆盖 MaxTurns/GameTimeout 等无 GameOverEvent 的终局路径）。
    /// 事件覆盖说明：施放=CardPlayEvent（含响应窗出牌 IsResponseCast）、当地=ElementPoolAddEvent、
    /// 弃牌=CardDiscardEvent（效果弃牌链；手牌上限弃牌是否走同事件以引擎发布点为准）、
    /// 攻击=AttackDeclarationEvent、技能=HeroSkillActivatedEvent、潮痕碑衍生物=CardZoneChangeEvent
    /// （入场战场，S3-3 蓝链里程碑同源）。费用=宣言时点 GameActions.GetCardCost（含动态数量——
    /// 经 StackEngine.GetEngagedEntryOf 取本次 cast 的 DynamicCounts；归根 run1 实测 6.0/8.0 并存）。
    /// </summary>
    public static class ScriptActionProbe
    {
        /// <summary>动作流 jsonl 输出路径。null/空 = 探针关闭（缺省，零开销）。
        /// 环境变量 TIDE_PROBE_ACTIONS 优先（静态赋值可覆盖，编辑器配方用）。</summary>
        public static string ActionsLogPath { get; set; }
            = Environment.GetEnvironmentVariable("TIDE_PROBE_ACTIONS");

        private static bool _subscribed;          // 进程级一次性订阅（启用后常驻）
        private static StreamWriter _writer;      // 按局开关（BeginGame 开 / EndGame 收）
        private static int _gameId;               // 本进程累计局号（1 起）
        private static int? _seed;                // 本局种子（协议回显）
        private static string _tag = "";          // 本局口径标签
        private static string _winnerSeat = "";   // 本局胜者座次（"" 平局）
        private static string _reason = "";       // 终局原因
        private static bool _gameOverSeen;        // GameOverEvent 已到（EndGame 兜底不重写）

        // 每局双方卡组归根张数（绿侧判据：绿预组 11 张归根，红蓝 0）
        private static int _p1GenelInDeck, _p2GenelInDeck;

        // 归根卡 ID（与 TideHeadlessDriver.IdGreenRecall 同值——探针不引 driver 私有常量，独立声明）
        internal const string GenelCardId = "C_D1A00009";

        /// <summary>卡名读取（与 driver.CardNameOf 同口径：包装数据卡名，无名回退 "?"）。</summary>
        internal static string CardNameOf(Card c)
            => (c as CardWrapper)?.GetData()?.CardName ?? "?";

        /// <summary>驱动接线：一局开始（scriptVsScript 分支调用；未启用=零操作）。</summary>
        public static void BeginGame(GameCore core, List<CardData> deck1, List<CardData> deck2,
            int? seed, string tag)
        {
            if (string.IsNullOrEmpty(ActionsLogPath)) return;
            try
            {
                EnsureSubscribed();
                _gameId++;
                _seed = seed;
                _tag = tag ?? "";
                _winnerSeat = "";
                _reason = "";
                _gameOverSeen = false;
                _p1GenelInDeck = CountGenel(deck1);
                _p2GenelInDeck = CountGenel(deck2);
                _writer = new StreamWriter(new FileStream(ActionsLogPath, FileMode.Append,
                    FileAccess.Write, FileShare.Read), Encoding.UTF8) { AutoFlush = true };
                WriteJson($"{{\"rec\":\"game\",\"game\":{_gameId},\"tag\":\"{_tag}\",\"seed\":{(_seed?.ToString() ?? "0")}"
                    + $",\"p1Genel\":{_p1GenelInDeck},\"p2Genel\":{_p2GenelInDeck}}}");
            }
            catch (Exception ex)
            {
                TideLog.Error($"[ScriptActionProbe] BeginGame 失败（探针停用，不影响对局）: {ex.Message}");
                SafeCloseWriter();
            }
        }

        /// <summary>驱动接线：一局结束（scriptVsScript finally 调用——覆盖无 GameOverEvent 的
        /// MaxTurns/GameTimeout 收口；已见 GameOverEvent 时不重写胜者字段）。</summary>
        public static void EndGame(Player winner, string reason)
        {
            if (_writer == null) return;
            try
            {
                if (!_gameOverSeen)
                {
                    _reason = reason ?? "";
                    _winnerSeat = winner == null ? "" : SeatOf(winner).ToString();
                }
                WriteJson($"{{\"rec\":\"end\",\"game\":{_gameId},\"winner\":\"{_winnerSeat}\",\"reason\":\"{_reason}\"}}");
            }
            catch (Exception ex)
            {
                TideLog.Error($"[ScriptActionProbe] EndGame 收尾失败: {ex.Message}");
            }
            finally
            {
                SafeCloseWriter();
            }
        }

        private static void SafeCloseWriter()
        {
            try { _writer?.Dispose(); } catch { /* 探针收尾不抛 */ }
            _writer = null;
        }

        private static int CountGenel(List<CardData> deck)
            => deck?.Count(cd => cd != null && cd.CardName == "归根") ?? 0;

        private static int SeatOf(Player p)
            => p != null && GameCore.Instance != null && ReferenceEquals(p, GameCore.Instance.Player1) ? 0 : 1;

        private static int OwnTurnOf(Player p, int turn)
            => ReferenceEquals(p, GameCore.Instance.Player1) ? (turn + 1) / 2 : turn / 2;

        // ===================================================== 事件订阅（启用后进程级一次性） =====================================================

        private static void EnsureSubscribed()
        {
            if (_subscribed) return;
            _subscribed = true;
            EventManager.Instance.Subscribe<CardPlayEvent>(OnCardPlay);
            EventManager.Instance.Subscribe<ElementPoolAddEvent>(OnPoolAdd);
            EventManager.Instance.Subscribe<CardDiscardEvent>(OnDiscard);
            EventManager.Instance.Subscribe<AttackDeclarationEvent>(OnAttack);
            EventManager.Instance.Subscribe<HeroSkillActivatedEvent>(OnHeroSkill);
            EventManager.Instance.Subscribe<CardZoneChangeEvent>(OnZoneChange);
            EventManager.Instance.Subscribe<GameOverEvent>(OnGameOver);
        }

        /// <summary>进程级反订阅（域重载/探针退役；防编辑器内重复启用跨局泄漏）。</summary>
        public static void UnsubscribeAll()
        {
            if (!_subscribed) return;
            _subscribed = false;
            EventManager.Instance.Unsubscribe<CardPlayEvent>(OnCardPlay);
            EventManager.Instance.Unsubscribe<ElementPoolAddEvent>(OnPoolAdd);
            EventManager.Instance.Unsubscribe<CardDiscardEvent>(OnDiscard);
            EventManager.Instance.Unsubscribe<AttackDeclarationEvent>(OnAttack);
            EventManager.Instance.Unsubscribe<HeroSkillActivatedEvent>(OnHeroSkill);
            EventManager.Instance.Unsubscribe<CardZoneChangeEvent>(OnZoneChange);
            EventManager.Instance.Unsubscribe<GameOverEvent>(OnGameOver);
            SafeCloseWriter();
        }

        private static void OnGameOver(GameOverEvent e)
        {
            if (_writer == null || e == null) return;
            _gameOverSeen = true;
            _winnerSeat = e.Winner == null ? "" : SeatOf(e.Winner).ToString();
            _reason = e.Reason.ToString();
        }

        private static void OnCardPlay(CardPlayEvent e)
        {
            if (_writer == null || e?.PlayedCard == null || e.Player == null) return;
            var core = GameCore.Instance;
            var card = e.PlayedCard;
            // 动态费：宣言时点 GetCardCost（S3b 卡留手牌——本次 cast 的 DynamicCounts 从栈条目回读；
            // 条目缺失（防御）退化为不带动态数量的口径）
            var entry = core?.StackEngine?.GetEngagedEntryOf(card);
            var cost = GameActions.GetCardCost(card, e.ModeIndex, entry?.DynamicCounts);
            WriteAct(e.Player, "PlayCard", card, e.ModeIndex, e.IsResponseCast, cost);
        }

        private static void OnPoolAdd(ElementPoolAddEvent e)
        {
            if (_writer == null || e?.AddedCard == null || e.Player == null) return;
            WriteAct(e.Player, "PlayLand", e.AddedCard, (int)e.LockedColor, false, null);
        }

        private static void OnDiscard(CardDiscardEvent e)
        {
            if (_writer == null || e?.Card == null || e.Player == null) return;
            WriteAct(e.Player, "Discard", e.Card, 0, false, null);
        }

        private static void OnAttack(AttackDeclarationEvent e)
        {
            if (_writer == null || e?.AttackingPlayer == null) return;
            // 攻击行只有目标语义（卡/角色），无费用——cost 恒 null；目标类型记 mode 字段备用
            //（0=打卡 1=打脸，攻击者=卡时;角色攻击恒 1）
            int mode = e.Attacker is Card ? (e.Target is Card ? 0 : 1) : 1;
            WriteAct(e.AttackingPlayer, "Attack", e.Attacker as Card, mode, false, null);
        }

        private static void OnHeroSkill(HeroSkillActivatedEvent e)
        {
            if (_writer == null || e?.Player == null) return;
            WriteAct(e.Player, "HeroSkill", e.SkillCard, 0, false, null);
        }

        /// <summary>潮痕碑衍生物召唤（蓝链里程碑，S3-3 同源）：卡入场战场。只记名字命中的
        /// 衍生物（战场地牌/普通入场不记——ElementPoolAddEvent 已覆盖地牌）。</summary>
        private static void OnZoneChange(CardZoneChangeEvent e)
        {
            if (_writer == null || e?.Card == null || e.NewZone != Zone.Battlefield) return;
            var name = CardNameOf(e.Card);
            if (name != "潮痕碑") return;
            var owner = e.Controller ?? GameCore.Instance?.TurnEngine?.TurnPlayer;
            if (owner == null) return;
            WriteAct(owner, "SummonToken", e.Card, 0, false, null);
        }

        // ===================================================== 记录写出 =====================================================

        private static void WriteAct(Player player, string kind, Card card, int mode,
            bool respCast, ElementCost cost)
        {
            try
            {
                var core = GameCore.Instance;
                int turn = core?.TurnEngine?.TurnNumber ?? 0;
                var sb = new StringBuilder(256);
                sb.Append("{\"rec\":\"act\",\"game\":").Append(_gameId)
                  .Append(",\"seat\":").Append(SeatOf(player))
                  .Append(",\"turn\":").Append(turn)
                  .Append(",\"ot\":").Append(OwnTurnOf(player, turn))
                  .Append(",\"kind\":\"").Append(kind).Append('"');
                if (card != null)
                {
                    AppendStringField(sb, "card", CardNameOf(card));
                    sb.Append(",\"cardId\":\"").Append(card.ID).Append('"');
                }
                else
                {
                    sb.Append(",\"card\":\"\",\"cardId\":\"\"");
                }
                sb.Append(",\"mode\":").Append(mode);
                sb.Append(",\"respCast\":").Append(respCast ? 1 : 0);
                if (cost != null)
                {
                    sb.Append(",\"costT\":").Append(Fmt(cost.Total));
                    for (int i = 0; i <= 4; i++)
                        sb.Append(",\"cost").Append(i).Append("\":").Append(Fmt(i < cost.v.Length ? cost.v[i] : 0f));
                }
                else
                {
                    sb.Append(",\"costT\":0,\"cost0\":0,\"cost1\":0,\"cost2\":0,\"cost3\":0,\"cost4\":0");
                }
                AppendWallet(sb, player);
                AppendGenelContext(sb, player);
                sb.Append('}');
                WriteJson(sb.ToString());
            }
            catch (Exception ex)
            {
                // 探针异常绝不外溢进对局（零行为变化铁律）——记日志继续
                TideLog.Error($"[ScriptActionProbe] 记录失败（忽略）: {ex.Message}");
            }
        }

        /// <summary>钱包五元（g[58..62] 同式：逐色 TempElements+未横置锁色地、Any 单独）。</summary>
        private static void AppendWallet(StringBuilder sb, Player p)
        {
            var ep = GameCore.Instance?.ElementPool;
            int w0 = 0, w1 = 0, w2 = 0, w3 = 0, wAny = 0;
            if (ep != null)
            {
                var pool = ep.GetPool(p);
                if (pool != null)
                {
                    int T(ManaType t) => pool.TempElements != null && pool.TempElements.ContainsKey(t)
                        ? pool.TempElements[t] : 0;
                    w0 = T(ManaType.Gray); w1 = T(ManaType.Red); w2 = T(ManaType.Blue); w3 = T(ManaType.Green);
                    wAny = T(ManaType.Any);
                    if (pool.PooledCards != null)
                        foreach (var pc in pool.PooledCards)
                        {
                            if (pc == null || pc.IsTapped) continue;
                            switch (pc.LockedColor)
                            {
                                case ManaType.Gray: w0++; break;
                                case ManaType.Red: w1++; break;
                                case ManaType.Blue: w2++; break;
                                case ManaType.Green: w3++; break;
                            }
                        }
                }
            }
            sb.Append(",\"wallet0\":").Append(w0)
              .Append(",\"wallet1\":").Append(w1)
              .Append(",\"wallet2\":").Append(w2)
              .Append(",\"wallet3\":").Append(w3)
              .Append(",\"wallet4\":").Append(wAny);
        }

        /// <summary>链上下文（S3-0 直接消费）：手牌归根张数 / 墓地归根张数 / 池地总数。</summary>
        private static void AppendGenelContext(StringBuilder sb, Player p)
        {
            int handG = 0, graveG = 0, lands = 0;
            var core = GameCore.Instance;
            if (core != null)
            {
                handG = ZoneCount(core, p, Zone.Hand, GenelCardId);
                graveG = ZoneCount(core, p, Zone.Graveyard, GenelCardId);
                var pooled = core.ElementPool?.GetPooledCards(p);
                lands = pooled?.Count ?? 0;
            }
            sb.Append(",\"handGenel\":").Append(handG)
              .Append(",\"graveGenel\":").Append(graveG)
              .Append(",\"lands\":").Append(lands);
        }

        private static int ZoneCount(GameCore core, Player p, Zone z, string cardId)
        {
            var cards = core.ZoneManager?.GetCards(p, z);
            int n = 0;
            if (cards != null)
                foreach (var c in cards)
                    if (c != null && c.ID == cardId) n++;
            return n;
        }

        private static void AppendStringField(StringBuilder sb, string key, string value)
        {
            sb.Append(",\"").Append(key).Append("\":\"");
            foreach (var ch in value ?? "")
                sb.Append(ch == '"' ? "\\\"" : ch == '\\' ? "\\\\" : ch.ToString());
            sb.Append('"');
        }

        private static void WriteJson(string line)
        {
            // 行内 double 用不变文化（点号小数），逐字段直接拼好的串写一行
            _writer?.WriteLine(line);
        }

        private static string Fmt(float f) => f.ToString("0.0#", CultureInfo.InvariantCulture);
    }
}
