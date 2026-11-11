using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.AI;
using CardCore.Serialization;
using SynergyUI;
using UnityEditor;
using UnityEngine;
using UniTask = Cysharp.Threading.Tasks.UniTask;

namespace CardCore.Editor.Tests
{
    /// <summary>
    /// 战斗回放验证器（2026-11-11 战斗演出统一重构 S2·方案 §4-S2 验收）：
    /// A 帧 round-trip（合成剧本 TideJson+GZip 落盘/读盘逐字一致+存取生命周期）；
    /// B 无头 AI 同 seed 两局（投影器+LocalFilmSource 录制——帧数/末帧/全剧本 JSON 确定性，
    /// 对拍前 RuntimeId/EventId 计数器按验证器口径归零）；
    /// C 桩 sink 播完不抛异常（同步时钟整链推到底——视图/指令/游标计数对账）；
    /// D SeekTo 跳转（中段续播到末帧、就近 View 预应用、越界拒绝）。
    /// 菜单：Tools/验证/战斗回放；输出 `[回放验证] PASS=n FAIL=0`。
    /// </summary>
    public static class BattleFilmVerifier
    {
        private const int DeterminismSeed = 20261111; // B 对拍种子（固定——结果漂移即回归信号）
        private const int MaxTurns = 60;              // 无头对局回合上限（够打完即收）

        /// <summary>手动验收入口（v1 唯一复盘入口·Play 模式）：播 Films 里最近一局——进战场复盘屏
        ///（时间轴铺全量行/点行跳转/退出复盘键）。先 Play（到主菜单）再点本菜单。</summary>
        [MenuItem("Tools/验证/战斗回放/播放最近剧本（Play 模式）")]
        public static void PlayLatestFilm()
        {
            if (!Application.isPlaying)
            {
                Debug.LogError("[回放验证] 播放剧本须在 Play 模式——先 Play（到主菜单）再点本菜单");
                return;
            }
            var ids = BattleFilmStore.ListIds();
            if (ids.Count == 0)
            {
                Debug.LogError("[回放验证] Films 目录无剧本——先打完一局本地对局（终局自动录制）或先跑上面验证器");
                return;
            }
            var mgr = UIBootstrap.ActiveManager;
            if (mgr == null)
            {
                Debug.LogError("[回放验证] UIBootstrap.ActiveManager 为空（界面未就绪）");
                return;
            }
            BattleEntry.Mode = BattleMode.Replay;
            BattleEntry.ReplayFilmId = ids[0];
            BattleStageDirector.SwallowToBattleAsync(() => mgr.Show<BattleScreen>()).Forget();
            Debug.Log($"[回放验证] 复盘开播：{ids[0]}（日志行点击跳转；「退出复盘」键返回）");
        }

        [MenuItem("Tools/验证/战斗回放/运行验证器（A-F）")]
        public static void Run()
        {
            int pass = 0, fail = 0;
            var lines = new List<string>();
            void Section(string name, Action body)
            {
                try
                {
                    body();
                    lines.Add($"PASS {name}");
                    pass++;
                }
                catch (Exception ex)
                {
                    lines.Add($"FAIL {name}：{ex.Message}");
                    fail++;
                    Debug.LogError($"[回放验证] {name} 异常：{ex}");
                }
            }

            // A：合成剧本 round-trip（纯数据，无引擎）
            Section("A 帧round-trip（合成剧本落盘/读盘逐字一致）", () =>
            {
                var film = BuildSyntheticFilm();
                BattleFilmStore.Save(film);
                if (!BattleFilmStore.Has(film.Header.MatchId)) throw new Exception("落盘后 Has=false");
                var loaded = BattleFilmStore.Load(film.Header.MatchId);
                if (loaded == null) throw new Exception("读盘返回 null");
                string a = TideJson.ToJson(film), b = TideJson.ToJson(loaded);
                if (a != b) throw new Exception($"round-trip JSON 不一致（{a.Length} vs {b.Length} 字符）");
                var dmg = loaded.Frames[1].Ops[0];
                if (dmg.Kind != BattleFxKind.Damage || dmg.Amount != 3 || dmg.CastX != 1 || dmg.Tint != 1)
                    throw new Exception($"Damage op 字段漂移：{dmg.Kind}/{dmg.Amount}/{dmg.CastX}/{dmg.Tint}");
                // 全 Kind 覆盖断言（16 种每条在盘+关键字段无损：Paid 字典/Text/D1D2/Seat/Flag）
                var ops2 = loaded.Frames[1].Ops;
                var kinds = new HashSet<BattleFxKind>();
                foreach (var o in ops2) kinds.Add(o.Kind);
                foreach (BattleFxKind kind in Enum.GetValues(typeof(BattleFxKind)))
                    if (!kinds.Contains(kind))
                        throw new Exception($"round-trip 丢失 Kind：{kind}（ops={ops2.Count} 条）");
                var arrival = ops2.First(o => o.Kind == BattleFxKind.Arrival);
                if (arrival.Paid == null || arrival.Paid.Count != 1 || Math.Abs(arrival.Paid[1] - 2.5f) > 1e-6)
                    throw new Exception("Paid 字典 round-trip 漂移");
                var luck = ops2.First(o => o.Kind == BattleFxKind.LuckRoll);
                if (luck.D1 != 4 || luck.D2 != 5 || luck.Threshold != 6 || !luck.Flag || luck.Text != "计数器")
                    throw new Exception("D1/D2/Threshold/Flag/Text round-trip 漂移");
                if (loaded.Frames[2].Combat == null || loaded.Frames[2].Combat.Hits.Count != 1
                    || !loaded.Frames[2].Combat.Hits[0].IsCounter)
                    throw new Exception("CombatShot/Hits 丢失");
                if (loaded.Frames[3].Kind != BattleFrameKind.End) throw new Exception("末帧非 End");
                BattleFilmStore.Delete(film.Header.MatchId);
                if (BattleFilmStore.Has(film.Header.MatchId)) throw new Exception("Delete 后 Has=true");
                Debug.Log($"[回放验证] A：{film.Frames.Count} 帧 round-trip 一致（{a.Length} 字符）");
            });

            // B：无头同 seed 两局确定性
            BattleFilm film1 = null, film2 = null;
            Section("B 无头同seed两局（帧数/末帧/全剧本JSON一致）", () =>
            {
                film1 = RunHeadlessWithFilm(DeterminismSeed);
                film2 = RunHeadlessWithFilm(DeterminismSeed);
                if (film1 == null || film2 == null) throw new Exception("录制返回 null（引擎/对局异常见日志）");
                if (film1.Frames.Count != film2.Frames.Count)
                    throw new Exception($"帧数不一致：{film1.Frames.Count} vs {film2.Frames.Count}");
                if (film1.Frames.Count < 8) throw new Exception($"帧数过少（{film1.Frames.Count}）——对局未正常推进");
                if (film1.Frames[film1.Frames.Count - 1].Kind != BattleFrameKind.End)
                    throw new Exception("末帧非 End（终局封卷缺失）");
                string a = Fingerprint(film1), b = Fingerprint(film2);
                if (a != b)
                {
                    int i = FirstDiff(a, b);
                    throw new Exception($"剧本指纹不一致（首差 @{i}：{Snippet(a, i)} vs {Snippet(b, i)}）");
                }
                // 真实剧本也过一遍 round-trip（快照带引擎 CoreCard——[NonSerialized] 跳过验证）
                BattleFilmStore.Save(film1);
                var reloaded = BattleFilmStore.Load(film1.Header.MatchId);
                if (TideJson.ToJson(film1) != TideJson.ToJson(reloaded))
                    throw new Exception("真实剧本 round-trip JSON 不一致");
                BattleFilmStore.Delete(film1.Header.MatchId);
                Debug.Log($"[回放验证] B：{film1.Frames.Count} 帧两连跑逐字一致（指纹 {a.Length} 字符）");
            });

            // C：桩 sink 播完不抛异常（同步时钟）
            Section("C 桩sink播完（视图/指令/游标计数对账）", () =>
            {
                if (film1 == null) throw new Exception("依赖 B 的剧本（B 未通过）");
                var sink = new StubSink();
                var playhead = NewSyncPlayhead();
                playhead.Load(film1, sink);
                if (playhead.Busy) throw new Exception("播完后 Busy 仍 true（收卷缺失）");
                int expectViews = film1.Frames.Count(f => f.View != null);
                if (sink.AppliedViews != expectViews)
                    throw new Exception($"视图应用数不符：{sink.AppliedViews} vs {expectViews}");
                if (sink.MaxCursor != film1.Frames.Count - 1)
                    throw new Exception($"游标未到末帧：{sink.MaxCursor} vs {film1.Frames.Count - 1}");
                if (sink.FxExecuted == 0) throw new Exception("零演出指令执行（一局必有效果/入场）");
                Debug.Log($"[回放验证] C：视图 {sink.AppliedViews} 指令 {sink.FxExecuted} 战斗 {sink.CombatPlayed} 游标 {sink.MaxCursor}");
            });

            // D：SeekTo 跳转
            Section("D SeekTo（中段续播到末帧/就近View/越界拒绝）", () =>
            {
                if (film1 == null) throw new Exception("依赖 B 的剧本（B 未通过）");
                var sink = new StubSink();
                var playhead = NewSyncPlayhead();
                playhead.Load(film1, sink);
                int mid = film1.Frames.Count / 2;
                int before = sink.AppliedViews;
                if (!playhead.SeekTo(mid)) throw new Exception($"SeekTo({mid}) 返回 false");
                if (playhead.Busy) throw new Exception("跳转续播后 Busy 仍 true");
                int expect = 1 /*目标帧前最近 View 预应用（帧 #0 恒 View）*/
                            + film1.Frames.Skip(mid).Count(f => f.View != null);
                if (sink.AppliedViews - before != expect)
                    throw new Exception($"跳转视图应用数不符：{sink.AppliedViews - before} vs {expect}");
                if (sink.MaxCursor != film1.Frames.Count - 1)
                    throw new Exception($"跳转后游标未到末帧：{sink.MaxCursor}");
                if (playhead.SeekTo(-1)) throw new Exception("SeekTo(-1) 应拒绝");
                if (playhead.SeekTo(film1.Frames.Count + 5)) throw new Exception("越界 SeekTo 应拒绝");
                if (!playhead.SeekTo(0)) throw new Exception("SeekTo(0) 回开头应允许");
                Debug.Log($"[回放验证] D：跳 {mid} → 续播到末帧，视图 {sink.AppliedViews - before} 次对账");
            });

            // E：演出指令捕获覆盖（P0-2 补强——stub 播放不执行 ref 演出，捕获链须独立取证）
            Section("E 演出指令捕获覆盖（无头对局 op 全 Kind 计数审计）", () =>
            {
                if (film1 == null) throw new Exception("依赖 B 的剧本（B 未通过）");
                var counts = new Dictionary<BattleFxKind, int>();
                foreach (var f in film1.Frames)
                {
                    foreach (var o in f.PreOps.Concat(f.Ops))
                        counts[o.Kind] = counts.TryGetValue(o.Kind, out var n) ? n + 1 : 1;
                }
                void Require(BattleFxKind k, int min)
                {
                    counts.TryGetValue(k, out var n);
                    if (n < min) throw new Exception($"{k} 捕获 {n} 条 < {min}——捕获链缺口");
                }
                // 核心链必载：效果伤害/死亡预持+死亡演出/入场粒子/属性浮字（红快攻局必然发生）
                Require(BattleFxKind.Damage, 1);
                Require(BattleFxKind.DeathHold, 1);
                Require(BattleFxKind.Death, 1);
                Require(BattleFxKind.Arrival, 1);
                Require(BattleFxKind.CounterFloat, 1);
                // Paid 软断言：扣费粒子只在「施放中法术」付费时录（生物入场粒子由 Arrival 承载防双份）
                // ——该 seed 局 23 条付费全是生物/结界打出（实测无一张法术施放），=0 是卡组事实非链路缺口
                counts.TryGetValue(BattleFxKind.Paid, out var paidN);
                Debug.Log("[回放验证] E：op 捕获 " + string.Join("，",
                    counts.OrderBy(kv => kv.Key.ToString()).Select(kv => $"{kv.Key}={kv.Value}"))
                    + (paidN == 0 ? "（Paid=0：本局无法术施放——法术扣费链由 A 序列化/F 分发覆盖）" : ""));
            });

            // F：执行冒烟（P0-2 补强——C 桩 sink 不触 PlaybackFx；本段对全部 Kind 走真执行器+
            // BattleFieldView ref 口的「目标不在视图=静默返回」契约。粒子/走位观感属 Play 人工验收）
            Section("F 执行冒烟（全 Kind 真分发+目标不在视图静默契约）", () =>
            {
                var field = new BattleFieldView(); // 编辑器纯 C# 构造（未装台——目标解析全走守卫路径）
                try
                {
                    var executor = new BattleFxOpExecutor(() => field);
                    var ghost = new FxRef { Rid = 0x7FFFFF0, CardId = "C_GHOST", Name = "幽灵", Seat = 1 };
                    foreach (BattleFxKind kind in Enum.GetValues(typeof(BattleFxKind)))
                        executor.ExecuteAsync(new BattleFxOp
                        {
                            Kind = kind,
                            Source = ghost,
                            Target = ghost,
                            Amount = 2, Tint = 1, CastX = -1, CastZ = -1, Reason = 0, Flag = true,
                            D1 = 1, D2 = 2, Seat = 0, Text = CardCore.Attribute.CounterRules.PowerUpCounter,
                            Paid = null, // Paid=null 走分发守卫（粒子路径属 Play 验收——编辑器无场景件）
                        });
                    // CombatShot 回放守卫：目标不在视图且无目标格（TargetX<0）=同步早退不抛
                    field.PlayCombatShotAsync(new CombatShot { Attacker = ghost, Target = ghost, TargetX = -1, TargetZ = -1 });
                    if (field.VisualsBusy) throw new Exception("守卫路径结束后视觉门未归零（BeginVisual 未配对 EndVisual）");
                    Debug.Log($"[回放验证] F：{Enum.GetValues(typeof(BattleFxKind)).Length} 种 Kind 分发+CombatShot 守卫全静默通过");
                }
                finally
                {
                    field.Dispose();
                }
            });

            foreach (var l in lines) Debug.Log($"[回放验证] {l}");
            Debug.Log($"[回放验证] PASS={pass} FAIL={fail}");
        }

        // ============================================================ A 合成剧本 ============================================================

        private static BattleFilm BuildSyntheticFilm()
        {
            var film = new BattleFilm
            {
                Header = new BattleFilmHeader
                {
                    Version = 1, MatchId = "verify-synthetic", Mode = 0, ViewerSeat = 0,
                    SelfName = "我方", OppName = "对手", Seed = 42, CreatedAtUnix = 1700000000,
                },
            };
            var f0 = new BattleFrame { Kind = BattleFrameKind.View, Label = "开局", LastEventId = 0 };
            f0.View = new BattleViewData { TurnNumber = 1 };
            f0.View.SelfUnits.Add(new BattleCardView
            {
                RuntimeId = 7, CardId = "C_TEST", Name = "测试兽", X = 2, Z = 3,
                Runtime = new SerializableRuntimeCardState { RuntimeId = 7, ID = "C_TEST", Power = 2, Life = 3 },
            });
            var f1 = new BattleFrame
            {
                Kind = BattleFrameKind.Step, Label = "测试兽", CardRid = 7, Remaining = 2, LastEventId = 5,
                View = new BattleViewData { TurnNumber = 1 },
            };
            f1.PreOps.Add(new BattleFxOp { Kind = BattleFxKind.DeathHold, Target = new FxRef { Rid = 9, CardId = "C_T2", Name = "靶子", Seat = 1 } });
            f1.Ops.Add(new BattleFxOp
            {
                Kind = BattleFxKind.Damage,
                Source = new FxRef { Rid = 7, CardId = "C_TEST", Name = "测试兽", Seat = 0 },
                Target = new FxRef { Rid = 9, CardId = "C_T2", Name = "靶子", Seat = 1 },
                Amount = 3, Tint = 1, CastX = 1, CastZ = 2,
            });
            // 全 Kind 序列化覆盖（2026-11-11 P0-2 补强）：除 Damage（上方首条）外每 Kind 一条满字段 op
            foreach (BattleFxKind kind in Enum.GetValues(typeof(BattleFxKind)))
            {
                if (kind == BattleFxKind.Damage) continue;
                f1.Ops.Add(new BattleFxOp
                {
                    Kind = kind,
                    Source = new FxRef { Rid = 7, CardId = "C_TEST", Name = "测试兽", Seat = 0 },
                    Target = new FxRef { Rid = 9, CardId = "C_T2", Name = "靶子", Seat = 1 },
                    Amount = 1, Tint = 2, CastX = 1, CastZ = 2, Reason = 3, Flag = true,
                    D1 = 4, D2 = 5, Threshold = 6, Seat = 1, Text = "计数器",
                    Paid = new Dictionary<int, float> { { 1, 2.5f } },
                });
            }
            f1.Lines.Add(new BattleLogLine { EventId = 4, FrameIndex = 1, Text = "测试行", FullText = "测试行", Kind = 4, Flags = 2, Class = "log-line--effect" });
            var f2 = new BattleFrame
            {
                Kind = BattleFrameKind.Step, Label = "攻击宣言", CardRid = 7, IsAttack = true, Remaining = 0, LastEventId = 12,
                View = new BattleViewData { TurnNumber = 2 },
                Combat = new CombatShot
                {
                    Attacker = new FxRef { Rid = 7, CardId = "C_TEST", Name = "测试兽", Seat = 0 },
                    Target = new FxRef { IsPlayer = true, Seat = 1, Name = "对手" },
                    TargetX = 3, TargetZ = 0,
                },
            };
            f2.Combat.Hits.Add(new CombatHit { Target = new FxRef { IsPlayer = true, Seat = 1, Name = "对手" }, Amount = 2, IsCounter = true });
            var f3 = new BattleFrame { Kind = BattleFrameKind.End, Label = "终局", LastEventId = 20 };
            f3.View = new BattleViewData { TurnNumber = 2, GameOver = true };
            film.Frames.Add(f0);
            film.Frames.Add(f1);
            film.Frames.Add(f2);
            film.Frames.Add(f3);
            return film;
        }

        // ============================================================ B 无头录制 ============================================================

        /// <summary>无头跑一局并录剧本：投影器（无表现钩子）+ LocalFilmSource（播放器=null 纯录制）
        /// 挂在共享 AiDuelRunner 外圈——对拍计数器按验证器口径归零（Entity/GameEventBase/NetEntityDirectory）。</summary>
        private static BattleFilm RunHeadlessWithFilm(int seed)
        {
            Entity.ResetRuntimeIdCounterForVerification(); // M3 对拍口径：同 seed 两局 RuntimeId 逐值对拍
            GameEventBase.ResetEventIdCounterForVerification();
            CardCore.Network.NetEntityDirectory.Clear();
            MatchLogService.ClearAll();
            MatchLogService.EnsureStarted();

            var core = GameCore.Instance;
            var ctrl = new BattleController();
            BattleProjector projector = null;
            var film = new LocalFilmSource(null, () => core, () => projector, null); // 纯录制：无播放器/无编舞/无即时演出；投影器晚绑定（下行赋值）
            projector = new BattleProjector(() => core, () => ctrl,
                _ => { }, () => { }, () => { }, _ => { }, _ => { }, _ => { }, () => { }, null, film); // 钩子全空（无头无表现）——op 构造与快照捕获照走（演出经 Present 路由，无即时执行器）
            projector.WireEarly();
            projector.Wire();
            film.Wire(); // 行录制（先于 InitGame——首回合头行不漏）
            try
            {
                var decks = BattleDeckSources.ThemeDecks();
                var cfg = new DuelConfig
                {
                    Deck1 = decks.Count > 0 ? decks[0].deck : BattleDeckSources.AllCards(),  // 红·快攻
                    Deck2 = decks.Count > 2 ? decks[2].deck : BattleDeckSources.AllCards(), // 蓝·控制
                    Seed = seed, MaxTurns = MaxTurns,
                    Strategy1 = AiStrategy.General, Strategy2 = AiStrategy.General,
                };
                var result = AiDuelRunner.Run(cfg);
                if (!result.Completed)
                    throw new Exception($"对局未完成（{result.Reason}，{result.TotalTurns} 回合；错误 {result.Errors.Count} 条）");
                return film.Film;
            }
            finally
            {
                projector.Dispose();
                film.Dispose();
            }
        }

        /// <summary>剧本指纹（MatchId/CreatedAtUnix 含墙钟会异——抹平；RuntimeId/Rid 受全局实体计数器
        /// 跨局残留偏移影响——按首次出现序重编号（保相对引用等价，消实例号噪声）；其余全字段一致即确定性）。</summary>
        private static string Fingerprint(BattleFilm f)
        {
            var id = f.Header.MatchId;
            var ts = f.Header.CreatedAtUnix;
            f.Header.MatchId = "";
            f.Header.CreatedAtUnix = 0;
            var s = TideJson.ToJson(f);
            f.Header.MatchId = id;
            f.Header.CreatedAtUnix = ts;
            return NormalizeInstanceIds(s);
        }

        /// <summary>RuntimeId/Rid/CardRid 出现序重编号（同一 id 全片映射一致——两局仅实例号偏移时归一后逐字相等）。</summary>
        private static string NormalizeInstanceIds(string json)
        {
            var map = new Dictionary<string, string>();
            return System.Text.RegularExpressions.Regex.Replace(json, "\"(?:RuntimeId|Rid|CardRid)\":(\\d+)", m =>
            {
                var k = m.Groups[1].Value;
                if (!map.TryGetValue(k, out var v))
                {
                    v = map.Count.ToString();
                    map[k] = v;
                }
                return "\"RuntimeId\":" + v;
            });
        }

        private static int FirstDiff(string a, string b)
        {
            int n = Math.Min(a.Length, b.Length);
            for (int i = 0; i < n; i++)
                if (a[i] != b[i]) return i;
            return n;
        }

        private static string Snippet(string s, int at)
        {
            int from = Math.Max(0, at - 30);
            return s.Substring(from, Math.Min(80, s.Length - from));
        }

        // ============================================================ C/D 桩舞台 ============================================================

        /// <summary>同步时钟播放器（delay/yield 全同步完成——回放整链在 Load 调用栈内推到底，可同步断言）。</summary>
        private static BattlePlayhead NewSyncPlayhead()
            => new BattlePlayhead(() => null, () => null, () => null, () => null, () => null, null,
                delay: _ => UniTask.CompletedTask, yield: () => UniTask.CompletedTask);

        /// <summary>桩舞台（计数对账+null 视图即时抛错；游标回退合法——SeekTo 回跳续播的正常表现）。</summary>
        private sealed class StubSink : IBattleStageSink
        {
            public int AppliedViews, FxExecuted, CombatPlayed, AppendedLines, Cursor = -1, MaxCursor = -1;

            public void ApplyView(BattleViewData view)
            {
                if (view == null) throw new InvalidOperationException("ApplyView(null)");
                AppliedViews++;
            }

            public UniTask ExecuteFxAsync(BattleFxOp op)
            {
                FxExecuted++;
                return UniTask.CompletedTask;
            }

            public void AppendLines(IReadOnlyList<BattleLogLine> lines)
            {
                if (lines != null) AppendedLines += lines.Count;
            }

            public void AdvanceTimeline(int frameIndex)
            {
                Cursor = frameIndex;
                if (frameIndex > MaxCursor) MaxCursor = frameIndex;
            }

            public UniTask PlayCombatAsync(CombatShot shot)
            {
                if (shot == null) throw new InvalidOperationException("PlayCombat(null)");
                CombatPlayed++;
                return UniTask.CompletedTask;
            }

            public UniTask WaitVisualsIdleAsync() => UniTask.CompletedTask;
        }
    }
}
