using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.AI;
using SynergyUI;
using UnityEngine;

namespace CardCore.Editor.Tests
{
    /// <summary>
    /// AI 自动对战的复盘日志录制器（2026-11-12 方案落地·docs/AI自动对战·模拟复盘日志方案.md）：
    /// 把「AI 自动对战（选卡组）」窗口的单局双脚本对局按**联机复盘同一套真实数据格式**落盘——
    /// Films/&lt;matchId&gt;.film.gz（BattleFilmStore.Save 联机同一出口）+ 可选
    /// Encounters/&lt;matchId&gt;.json（大厅对战历史行——btn-replay 判定=BattleFilmStore.Has(matchId)）
    /// + Logs/SimLog_&lt;matchId&gt;.md（战报另存，不进对局记录防云存档膨胀）。
    ///
    /// 配方=BattleFilmVerifier.RunHeadlessWithFilm 已验证的无头录制（投影器钩子全空 +
    /// LocalFilmSource 播放器=null 纯录制）套在 AiDuelRunner.Run 外圈，另补两件事（方案 §1）：
    /// ① AiDuelRunner 自建棋盘经 BindBoard 接到外部 BattleController——CaptureSnapshot 读
    ///    ctrl.Board，不接棋盘时所有单位/地牌格位 X/Z=-1，回放棋盘空一片；
    /// ② 从 AttackDeclaration/CombatDamage 事件合成 CombatShot 挂攻击步——无头没有
    ///    CombatChoreographer，实测残片 19 个攻击步零战斗演出。
    /// 帧前 Resync：订阅放 projector.Wire() 之前（EventManager 按订阅序派发——快照与真实局
    /// 同口径，RefreshField 的既有教训）。
    /// </summary>
    public static class AiBattleReplayRecorder
    {
        /// <summary>录制选项：剧本头与对局记录的文案来源。</summary>
        public sealed class Options
        {
            public string SelfName;                 // 剧本头「我方」（如「P1·红·快攻」）
            public string OppName;                  // 剧本头「对手」=对局记录 opponentName（如「AI·蓝·控制」）
            public string OwnDeckName;              // 记录行 ownDeckName
            public string OppDeckName;              // 记录行 opponentDeckName
            public bool WriteEncounterRecord = true; // 是否写大厅历史行（窗口勾选，默认关由调用方给）
        }

        /// <summary>录制结果（窗口弹窗/日志汇报用）。</summary>
        public sealed class Result
        {
            public DuelResult Duel;
            public BattleFilm Film;
            public string MatchId, FilmPath, RecordPath, LogPath, Error;
            public int Frames, Lines, Ops, CombatFrames;
        }

        public static Result Run(DuelConfig cfg, Options opt = null)
        {
            opt ??= new Options();
            var result = new Result { MatchId = EncounterStore.NewMatchId() }; // 联机同形：剧本名与记录共用
            var core = GameCore.Instance;
            var ctrl = new BattleController();
            var announcer = new ConsoleAnnouncer();
            BattleProjector projector = null;
            var film = new LocalFilmSource(null, () => core, () => projector, null, matchId: result.MatchId);
            projector = new BattleProjector(() => core, () => ctrl,
                _ => { }, () => { }, () => { }, _ => { }, _ => { }, () => { }, null, film); // 钩子全空（无头无表现）
            int SeatOf(Player p) => p == null ? -1 : p == core.Player2 ? 1 : 0;

            // 补丁①（帧前 Resync·方案 §3.3.1）：订阅序在 projector.Wire() 之前——步帧快照构建前
            // 棋盘先落格（对订阅序免疫；EventManager 按订阅顺序派发）
            Action<StackResolutionEndEvent> onPreResync = _ => ctrl.Board?.Resync();

            // 补丁②（CombatShot 事件合成·方案 §3.3.2）：AttackDeclaration 开捕获 → CombatDamage
            // 累积命中（Attacker≠本步攻击者=反击）→ StackResolutionEnd 封 shot 挂攻击步帧（订阅在
            // Wire() 之后——本步帧已进 film.Film.Frames）；CombatCancelled 丢弃。守卫拦截不还原、
            // Lethal 恒 false——与联机 v1 同口径（BattleFilm.CombatShot 注释）。
            Entity shotAttacker = null, shotTarget = null;
            var shotHits = new List<CombatHit>();
            Action<AttackDeclarationEvent> onDeclare = e =>
            {
                shotAttacker = e?.Attacker;
                shotTarget = e?.Target;
                shotHits.Clear();
            };
            Action<CombatDamageEvent> onDamage = e =>
            {
                if (e == null || shotAttacker == null) return;
                shotHits.Add(new CombatHit
                {
                    Target = FxRef.Of(e.Defender, SeatOf),
                    Amount = e.Damage,
                    IsCounter = !ReferenceEquals(e.Attacker, shotAttacker),
                });
            };
            Action<CombatCancelledEvent> onCancelled = _ =>
            {
                shotAttacker = shotTarget = null;
                shotHits.Clear();
            };
            Action<StackResolutionEndEvent> onShotFrame = _ =>
            {
                var frames = film.Film?.Frames;
                if (frames == null || frames.Count == 0) return;
                var frame = frames[frames.Count - 1];
                if (!frame.IsAttack || frame.Combat != null || shotAttacker == null) return;
                var shot = new CombatShot
                {
                    Attacker = FxRef.Of(shotAttacker, SeatOf),
                    Target = FxRef.Of(shotTarget, SeatOf),
                    TargetX = -1, TargetZ = -1, // 未知格=回放器自跳（NetFilmSource 同约定）
                };
                // 目标格参照（编舞器 CaptureShot 同口径）：角色目标=防守方角色格；单位目标=帧视图内同 Rid 格位
                if (shotTarget is Player tp)
                {
                    var hc = GameBoard.BoardLayout.CharacterCell(SeatOf(tp));
                    shot.TargetX = hc.x;
                    shot.TargetZ = hc.z;
                }
                else if (shotTarget is Card tc && frame.View != null)
                {
                    var uv = FindUnit(frame.View, tc.RuntimeId);
                    if (uv != null) { shot.TargetX = uv.X; shot.TargetZ = uv.Z; }
                }
                foreach (var h in shotHits) shot.Hits.Add(h);
                frame.Combat = shot;
                result.CombatFrames++;
            };

            announcer.Attach();
            EventManager.Instance.Subscribe(onPreResync);
            projector.WireEarly();
            projector.Wire();
            film.Wire(); // 行录制（先于 InitGame——首回合头行不漏）
            EventManager.Instance.Subscribe(onDeclare);
            EventManager.Instance.Subscribe(onDamage);
            EventManager.Instance.Subscribe(onCancelled);
            EventManager.Instance.Subscribe(onShotFrame);
            try
            {
                result.Duel = AiDuelRunner.Run(cfg, ctrl); // 唯一执行器——对局逻辑零改动
                film.Finalize(); // 回合上限/异常未封卷兜底（已封卷幂等空转）
                if (film.Film == null)
                {
                    result.Error = "剧本未开卷（InitGame 前异常或卡组为空）";
                    return result;
                }

                // 头信息抹平（Save 之前——Save 覆盖同 id 文件）：BattleEntry.Mode 是静态字段会跨局
                // 残留（实测无头残片曾录成 Replay），LocalFilmSource 只在 OnGameStart 抄一次——必须显式改写。
                var h = film.Film.Header;
                h.MatchId = result.MatchId;
                h.Mode = (int)BattleMode.LocalAI; // 诚实反映来源；Header 不参与播放判定
                h.ViewerSeat = 0;
                h.Seed = cfg.Seed;
                h.SelfName = string.IsNullOrEmpty(opt.SelfName) ? "P1" : opt.SelfName;
                h.OppName = string.IsNullOrEmpty(opt.OppName) ? "AI" : opt.OppName;
                h.CreatedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(); // 联机同款 UTC 秒
                // 双方卡表（2026-11-12 阶段2·回放摘要）：卡 ID 与 DeckData.cardIds 同源；旧剧本无此字段=null
                h.SelfCardIds = cfg.Deck1?.Select(c => c != null ? c.ID : null).Where(s => !string.IsNullOrEmpty(s)).ToArray();
                h.OppCardIds = cfg.Deck2?.Select(c => c != null ? c.ID : null).Where(s => !string.IsNullOrEmpty(s)).ToArray();
                BattleFilmStore.Save(film.Film); // 联机同一出口（Films/<matchId>.film.gz，覆盖同 id）

                result.Film = film.Film;
                result.Frames = film.Film.Frames.Count;
                result.Lines = film.Film.Frames.Sum(f => f.Lines?.Count ?? 0);
                result.Ops = film.Film.Frames.Sum(f => (f.Ops?.Count ?? 0) + (f.PreOps?.Count ?? 0));
                result.FilmPath = PathOfFilm(result.MatchId);
                result.LogPath = MatchLogService.ExportMarkdown($"Logs/SimLog_{result.MatchId}.md");

                if (opt.WriteEncounterRecord)
                {
                    // 对局记录（大厅历史行，方案 §3.4 逐字段）：log=null 防云存档膨胀（战报另存 SimLog）；
                    // opponentCardIds=空数组——非空会让「复制对手卡组」可点（AI 预组无导入意义）
                    EncounterStore.Append(new EncounterRecord
                    {
                        matchId = result.MatchId,
                        finishedAtUnix = h.CreatedAtUnix,
                        opponentName = h.OppName,
                        win = result.Duel != null && result.Duel.Winner != null
                              && ReferenceEquals(result.Duel.Winner, core.Player1), // 我方=P1 座位
                        ownDeckName = opt.OwnDeckName ?? "",
                        opponentDeckName = opt.OppDeckName ?? "",
                        opponentCardIds = Array.Empty<string>(),
                    });
                    result.RecordPath = PathOfRecord(result.MatchId);
                }
                return result;
            }
            catch (Exception ex)
            {
                result.Error = $"{ex.GetType().Name}: {ex.Message}";
                return result;
            }
            finally
            {
                // 收尾（与 AiDuelRunner 自身 finally 同则：静态接线不得跨局残留）
                EventManager.Instance.Unsubscribe(onPreResync);
                EventManager.Instance.Unsubscribe(onDeclare);
                EventManager.Instance.Unsubscribe(onDamage);
                EventManager.Instance.Unsubscribe(onCancelled);
                EventManager.Instance.Unsubscribe(onShotFrame);
                projector.Dispose();
                film.Dispose();
                announcer.Detach();
            }
        }

        private static BattleCardView FindUnit(BattleViewData view, uint rid)
        {
            BattleCardView Of(List<BattleCardView> list) => list?.FirstOrDefault(u => u != null && u.RuntimeId == rid);
            return Of(view.SelfUnits) ?? Of(view.OppUnits);
        }

        private static string PathOfFilm(string matchId)
            => System.IO.Path.Combine(BattleFilmStore.Dir, matchId + ".film.gz");

        private static string PathOfRecord(string matchId)
            => System.IO.Path.Combine(EncounterStore.Dir, matchId + ".json");
    }
}
