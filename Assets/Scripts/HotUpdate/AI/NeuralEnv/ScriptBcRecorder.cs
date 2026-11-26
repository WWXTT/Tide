using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CardCore;
using CardCore.AI.NeuralEnv;

namespace CardCore.AI.NeuralEnv
{
    /// <summary>
    /// S3-2 脚本 BC 录制器（二期 §5，2026-11-26）：把脚本臂（SimpleAI）的每次**已成功应用**的
    /// 引擎动作，对齐成 (obs, chosen_row) 对落盘——BC 的标签源（计划定案标签源 (a)：「给脚本做
    /// 按 LAE 行选动作的 actor」。绿 NN 教师不存在（§3.7 三次绿专精门 0 过），脚本 actor 是唯一
    /// 可行标签源；用户 2026-11-26「s3全面执行」指令下按 (a) 定案）。
    ///
    /// 架构（**拦截匹配**，不改脚本行为）：SimpleAI 的引擎调用点（PlayLands/PlayBestAffordableCard/
    /// UseHeroSkill/ActivateEffect/DoCombat/DrainVoluntary/EndTurn）在**调用前**调 BeginAction——
    /// 此刻枚举 LAE + 建 TideObservation（决策点前置状态，与驱动器 Main 停靠同式）；调用返回后
    /// CommitXxx(success)——成功才按 (Type, Card, ModeIndex, Targets/Effect/Pending) 匹配动作行，
    /// 写出 (obs, 全行特征, chosen)。引擎拒绝的尝试不落标签（只计 attempts），SimpleAI 行为零变化。
    ///
    /// 与 S3-0a 探针（ScriptActionProbe）分工：那边=事件层、全动作轻量 jsonl（S3-0 三数 +
    /// T4 + S3-0c 对账基准）；这边=调用点拦截、带 obs 的二进制流（BC shard 原料）。
    ///
    /// 输出（每局一段）：`bc_<gameId>.bin`（记录流）+ `bc_<gameId>.meta.json`（局元数据）。
    /// 记录（小端）：int32 chosen(-1=未匹配) / int32 nActions / int32 turn / int32 ot / int32 seat /
    /// int32 type / int32 mode / 16B cardId(ASCII 补零) / float32 cards(191×158) /
    /// float32 globals(100) / float32 行特征(nActions×26)。维度由 Python 侧
    /// tide_features 常量对账（MAX_CARDS=191/N_CARD_FEATURES=158/N_GLOBAL=100/N_ACTION=26）。
    /// 覆盖说明（v1 限制）：只录 Main 相 SimpleAI 调用点；响应窗动作走 AiResponder/
    /// TryApplyAiResponse（D2c 链）不在拦截面——meta.responseSkipped 不计、绿套无响应卡，
    /// 记录于此供后续扩展判读。
    ///
    /// 启用口（缺省关=零开销）：环境变量 TIDE_PROBE_BC_DIR 或编辑器内静态赋值 OutputDir。
    /// </summary>
    public static class ScriptBcRecorder
    {
        /// <summary>BC 流输出目录。null/空=关闭（缺省，零开销）。环境变量 TIDE_PROBE_BC_DIR 优先。</summary>
        public static string OutputDir { get; set; }
            = Environment.GetEnvironmentVariable("TIDE_PROBE_BC_DIR");

        /// <summary>一次拦截的上下文（Begin→Commit 生命周期；SimpleAI 调用点持有透传）。</summary>
        public sealed class Pending
        {
            internal float[] ObsCards;      // 决策点前置观测 cards_（191×158 展开）
            internal float[] ObsGlobals;    // 决策点前置观测 global_
            internal float[] RowFeatures;   // LAE 全表特征（512×26 展开——写出按合法前缀截）
            internal List<TideAction> Rows; // 动作行引用（匹配用）
            internal int ValidRows;         // 合法前缀长度（[0]valid 位的权威读法）
            internal Player Me;             // 决策座位
            internal int Turn;
        }

        private static int _gameId;
        private static BinaryWriter _bin;         // 本局记录流（BeginGame 开 / EndGame 收）
        private static int _steps, _unmatched, _attemptsFailed;
        private static int _p1Genel, _p2Genel;    // 双方卡组归根张数（绿侧判据，与探针同式）
        private static int? _seed;

        /// <summary>驱动接线：一局开始（scriptVsScript 分支；OutputDir 未设=零操作）。</summary>
        public static void BeginGame(GameCore core, GameBoard.BoardState board, int? seed,
            List<CardData> deck1, List<CardData> deck2)
        {
            if (string.IsNullOrEmpty(OutputDir)) return;
            try
            {
                Directory.CreateDirectory(OutputDir);
                _gameId++;
                _seed = seed;
                _steps = _unmatched = _attemptsFailed = 0;
                _p1Genel = deck1?.Count(cd => cd != null && cd.CardName == "归根") ?? 0;
                _p2Genel = deck2?.Count(cd => cd != null && cd.CardName == "归根") ?? 0;
                _bin = new BinaryWriter(new FileStream(Path.Combine(OutputDir, $"bc_{_gameId}.bin"),
                    FileMode.Create, FileAccess.Write, FileShare.Read));
            }
            catch (Exception ex)
            {
                TideLog.Error($"[ScriptBcRecorder] BeginGame 失败（录制停用）: {ex.Message}");
                try { _bin?.Dispose(); } catch { /* 录制收尾不抛 */ }
                _bin = null;
            }
        }

        /// <summary>驱动接线：一局结束（scriptVsScript finally；写局元数据并收流）。</summary>
        public static void EndGame(Player winner, string reason, int turns)
        {
            if (_bin == null) return;
            try
            {
                var meta = new StringBuilder(256);
                meta.Append("{\"game\":").Append(_gameId)
                    .Append(",\"seed\":").Append(_seed ?? 0)
                    .Append(",\"p1Genel\":").Append(_p1Genel)
                    .Append(",\"p2Genel\":").Append(_p2Genel)
                    .Append(",\"winner\":\"").Append(winner == null ? "" : (ReferenceEquals(winner, GameCore.Instance.Player1) ? "0" : "1"))
                    .Append("\",\"reason\":\"").Append(reason ?? "").Append('"')
                    .Append(",\"turns\":").Append(turns)
                    .Append(",\"steps\":").Append(_steps)
                    .Append(",\"unmatched\":").Append(_unmatched)
                    .Append(",\"attemptsFailed\":").Append(_attemptsFailed)
                    .Append(",\"responseWindowCovered\":false}"); // v1 限制：响应窗动作不在拦截面
                File.WriteAllText(Path.Combine(OutputDir, $"bc_{_gameId}.meta.json"), meta.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                TideLog.Error($"[ScriptBcRecorder] EndGame 元数据失败: {ex.Message}");
            }
            finally
            {
                try { _bin.Dispose(); } catch { /* 录制收尾不抛 */ }
                _bin = null;
            }
        }

        // ===================================================== 拦截口（SimpleAI 调用点） =====================================================

        /// <summary>动作前拦截：枚举 LAE + 构建决策点前置观测。未启用/无合法行 → null（调用点透传）。</summary>
        public static Pending BeginAction(GameCore core, Player me, GameBoard.BoardState board)
        {
            if (_bin == null || core == null || me == null) return null;
            try
            {
                var legal = new LegalActionEnumerator();
                legal.Enumerate(core, me);
                int valid = 0;
                var feats = legal.Features;
                int nf = TideObservation.NAction;
                while (valid < legal.Count && feats[valid * nf] > 0.5f) valid++;
                if (valid == 0) return null; // 冻结兜底态（唯一 EndTurn 行也是合法——valid 恒≥1；0=异常）
                var obs = TideObservation.Build(core, me, false, board);
                return new Pending
                {
                    ObsCards = obs.Cards,
                    ObsGlobals = obs.Globals,
                    RowFeatures = feats,
                    Rows = legal.Actions,
                    ValidRows = valid,
                    Me = me,
                    Turn = core.TurnEngine.TurnNumber,
                };
            }
            catch (Exception ex)
            {
                TideLog.Error($"[ScriptBcRecorder] BeginAction 失败（本步跳录）: {ex.Message}");
                return null;
            }
        }

        /// <summary>出牌（PlayBestAffordableCard / 战前法术）：成功才落标签。</summary>
        public static void CommitPlayCard(Pending p, Player me, Card card, int mode,
            List<Entity> targets, bool success)
            => Commit(p, me, TideActionType.PlayCard, card, mode, targets, null, null, false, success);

        /// <summary>放地（PlayLands）：colorChoice null=缺省主色（匹配任意同行）。</summary>
        public static void CommitPlayLand(Pending p, Player me, Card card, ManaType? colorChoice, bool success)
            => Commit(p, me, TideActionType.PlayLand, card, colorChoice.HasValue ? (int)colorChoice.Value : -1,
                null, null, null, false, success);

        /// <summary>攻击宣言（单位/角色）。</summary>
        public static void CommitAttack(Pending p, Player me, Card attackerCard, Entity target, bool success)
            => Commit(p, me, TideActionType.Attack, attackerCard, 0, null, null, target,
                attackerCard == null, success);

        /// <summary>英雄技能（UseHeroSkill）。</summary>
        public static void CommitHeroSkill(Pending p, Player me, int effectIndex,
            List<Entity> targets, bool success)
            => Commit(p, me, TideActionType.HeroSkill, null, effectIndex, targets, null, null, false, success);

        /// <summary>战场卡主动效果（ActivateBattlefieldAbilities）。</summary>
        public static void CommitActivate(Pending p, Player me, EffectDefinition def, Card source, bool success)
            => Commit(p, me, TideActionType.Activate, source, 0, null, def, null, false, success);

        /// <summary>自愿桶待发效果（DrainVoluntaryQueue）。</summary>
        public static void CommitVoluntary(Pending p, Player me, PendingEffect pending, bool success)
            => Commit(p, me, TideActionType.VoluntaryTrigger, null, 0, null, null, null, false, success, pending);

        /// <summary>结束回合（TakeTurnAsync 收尾）。</summary>
        public static void CommitEndTurn(Pending p, Player me, bool success)
            => Commit(p, me, TideActionType.EndTurn, null, 0, null, null, null, false, success);

        // ===================================================== 匹配与写出 =====================================================

        private static void Commit(Pending p, Player me, TideActionType type, Card card, int mode,
            List<Entity> targets, EffectDefinition effect, Entity target, bool heroAttacker,
            bool success, PendingEffect pending = null)
        {
            if (p == null || _bin == null) return;
            if (!success)
            {
                _attemptsFailed++; // 引擎拒绝的尝试不落标签（SimpleAI 会换动作重试）
                return;
            }
            try
            {
                int chosen = -1;
                for (int i = 0; i < p.ValidRows; i++)
                {
                    var a = p.Rows[i];
                    if (a.Type != type) continue;
                    switch (type)
                    {
                        case TideActionType.PlayCard:
                            if (!ReferenceEquals(a.Card, card) || a.ModeIndex != mode) continue;
                            if (!SeqEqual(a.Targets, targets)) continue;
                            break;
                        case TideActionType.PlayLand:
                            if (!ReferenceEquals(a.Card, card)) continue;
                            if (mode >= 0 && a.ModeIndex != mode) continue;
                            break;
                        case TideActionType.Attack:
                            if (heroAttacker != a.HeroAttacker) continue;
                            if (!heroAttacker && !ReferenceEquals(a.Card, card)) continue;
                            if (!ReferenceEquals(a.Target, target)) continue;
                            break;
                        case TideActionType.HeroSkill:
                            if (a.ModeIndex != mode || !SeqEqual(a.Targets, targets)) continue;
                            break;
                        case TideActionType.Activate:
                            if (!ReferenceEquals(a.Card, card) || !ReferenceEquals(a.Effect, effect)) continue;
                            break;
                        case TideActionType.VoluntaryTrigger:
                            if (!ReferenceEquals(a.Pending, pending)) continue;
                            break;
                        case TideActionType.EndTurn:
                            break;
                        default:
                            continue;
                    }
                    chosen = i;
                    break;
                }
                if (chosen < 0) _unmatched++;

                int nf = TideObservation.NAction;
                _bin.Write(chosen);
                _bin.Write(p.ValidRows);
                _bin.Write(p.Turn);
                _bin.Write(OwnTurnOf(p.Me, p.Turn));
                _bin.Write(SeatOf(p.Me));
                _bin.Write((int)type);
                _bin.Write(mode);
                WriteCardId(card);
                WriteFloats(p.ObsCards);
                WriteFloats(p.ObsGlobals);
                for (int i = 0; i < p.ValidRows; i++)
                    for (int j = 0; j < nf; j++)
                        _bin.Write(p.RowFeatures[i * nf + j]);
                _steps++;
            }
            catch (Exception ex)
            {
                TideLog.Error($"[ScriptBcRecorder] Commit 写出失败（忽略）: {ex.Message}");
            }
        }

        /// <summary>目标序列等价（null=无目标/引擎自解析——与空表视为同义：SimpleAI 传空表、
        /// 行侧 null 是同一语义的两个表达）。</summary>
        private static bool SeqEqual(List<Entity> a, List<Entity> b)
        {
            bool aEmpty = a == null || a.Count == 0;
            bool bEmpty = b == null || b.Count == 0;
            if (aEmpty || bEmpty) return aEmpty && bEmpty;
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (!ReferenceEquals(a[i], b[i])) return false;
            return true;
        }

        private static void WriteCardId(Card card)
        {
            var id = card?.ID ?? "";
            var bytes = new byte[16];
            for (int i = 0; i < Math.Min(id.Length, 16); i++)
                bytes[i] = (byte)id[i];
            _bin.Write(bytes);
        }

        private static void WriteFloats(float[] arr)
        {
            if (arr == null) return;
            foreach (var f in arr) _bin.Write(f);
        }

        private static int SeatOf(Player p)
            => p != null && GameCore.Instance != null && ReferenceEquals(p, GameCore.Instance.Player1) ? 0 : 1;

        private static int OwnTurnOf(Player p, int turn)
            => ReferenceEquals(p, GameCore.Instance.Player1) ? (turn + 1) / 2 : turn / 2;
    }
}
