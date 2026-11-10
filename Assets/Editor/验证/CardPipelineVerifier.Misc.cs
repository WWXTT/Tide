using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using GameBoard;
using CardCore.Attribute.Handlers;
using Cysharp.Threading.Tasks;

namespace CardCore.Editor
{
    public static partial class CardPipelineVerifier
    {
        // ======================================== 训练副表重推导（2026-11-03 训练池分家） ========================================

        /// <summary>训练池独立副表（Assets/Data/TrainingData）重推导——由 Tools/重推导测试卡表费用与ID
        /// 统一入口调用（须在 RegenAtomicTableIds 之后，原子行 id 已收口）。与教学段
        ///（RegenTutorialAndPresetTables）同构：
        /// ① TrainingEffects：迁移台账自愈 → 原子 refId 跟换 → 悬空守卫（**中止不落盘**——训练表
        ///   为机器生成内容，悬空=真实腐坏，与教学「人工维护仅告警」口径不同）→ 清档重写
        ///（Save 内 HashEffect 重算 id + 锚价快照按现行原子表实时推导）；
        /// ② TrainingCards：effectIds 跟换（新效果号 + 原子换号）+ payload refId 台账自愈/跟换 →
        ///   装载重推费用快照（EnsureCost）→ 卡 id 重推（"C_"+HashCard——内容变才变）。
        /// 训练池无卡组 json 引用（训练桥=随机 30/主题整组口径），无③跟换段。2026-11-05 ID 冻结 +
        /// 身份稳定化后：卡/效果/原子 id 均不换号、身份哈希锚定冻结 ID（表内容编辑不再换血）——
        /// tide_rl/card_identity_manifest.json（v2）经 RegisterIdentities 幂等追加续排，仅新内容
        /// 才占新行；旧基线吊死仅发生在 v1→v2 一次性重键（盐拆除）。</summary>
        private static void RegenTrainingTables(Dictionary<string, string> atomRenames)
        {
            string fxPath = SynergyUI.CardDataPaths.TrainingFileIn("TrainingEffects.json");
            string cardsPath = SynergyUI.CardDataPaths.TrainingFileIn("TrainingCards.json");
            if (!File.Exists(fxPath) || !File.Exists(cardsPath))
            {
                Debug.LogError($"[Regen] 训练副表缺失：{fxPath} / {cardsPath}——跳过训练池重推");
                return;
            }

            // ---------- ① 效果表：台账自愈 → 原子 refId 跟换 → 悬空守卫 → 清档重写 ----------
            var validAtomIds = new HashSet<string>(
                CardCore.Attribute.AtomicEffectTable.GetAll()
                    .Where(c => c != null && !string.IsNullOrEmpty(c.HashId))
                    .Select(c => c.HashId));
            var fxRoot = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(fxPath));
            HealDanglingAtomRefs(fxRoot, validAtomIds, fxPath);
            int atomRemapped = 0;
            var dangling = new List<string>();
            foreach (var prop in fxRoot.Descendants().OfType<Newtonsoft.Json.Linq.JProperty>()
                     .Where(p => p.Name == "refId" && p.Value.Type == Newtonsoft.Json.Linq.JTokenType.String).ToList())
            {
                string rid = (string)prop.Value;
                if (atomRenames != null && atomRenames.TryGetValue(rid, out var nid))
                {
                    prop.Value = nid;
                    atomRemapped++;
                    rid = nid;
                }
                if (!validAtomIds.Contains(rid)) dangling.Add(rid);
            }
            if (dangling.Count > 0)
            {
                Debug.LogError($"[Regen] TrainingEffects.json 悬空原子引用 {dangling.Count} 处——训练池重推中止不落盘" +
                               $"（同正式池守卫；台账外孤儿引用请核对后记入 AtomMigrationLedger）：{string.Join("，", dangling.Distinct())}");
                return;
            }
            if (atomRemapped > 0)
            {
                WriteJTokenIndented(fxPath, fxRoot);
                Debug.Log($"[Regen] TrainingEffects.json 原子引用跟换 {atomRemapped} 处 → {fxPath}");
            }

            var graphs = SynergyUI.EffectLibrarySerializer.LoadAll(false, training: true);
            var fxRenames = new Dictionary<string, string>();
            if (graphs.Count > 0)
            {
                File.WriteAllText(fxPath, "{\"items\":[]}"); // 清档重写：防旧 id 条目残留（upsert 不删旧行）
                foreach (var g in graphs)
                {
                    if (g == null) continue;
                    string oldId = g.id;
                    SynergyUI.EffectLibrarySerializer.Save(g, false, training: true); // Save 内重算 id + 锚价快照
                    if (!string.IsNullOrEmpty(oldId) && oldId != g.id) fxRenames[oldId] = g.id;
                }
            }
            Debug.Log($"[Regen][训练] 效果表 id 重推：{graphs.Count} 条（换号 {fxRenames.Count} 条）→ {fxPath}");
            CardCore.EffectsLibrary.Reload(); // 新效果号生效：卡表 effectIds 解析/费用推导按新库

            // ---------- ② 卡表：effectIds/payload refId 跟换 → 装载 → 费用快照 + 卡 id 重推 ----------
            var cardsRoot = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(cardsPath));
            HealDanglingAtomRefs(cardsRoot, validAtomIds, cardsPath); // payload.refId 跨表迁移孤儿接回
            var cardsArr = cardsRoot["cards"] as Newtonsoft.Json.Linq.JArray;
            int idsRemapped = 0;
            if (cardsArr != null)
                foreach (var card in cardsArr.OfType<Newtonsoft.Json.Linq.JObject>())
                {
                    var idsArr = card["effectIds"] as Newtonsoft.Json.Linq.JArray;
                    if (idsArr != null)
                        for (int i = 0; i < idsArr.Count; i++)
                        {
                            if (idsArr[i].Type != Newtonsoft.Json.Linq.JTokenType.String) continue;
                            string s = (string)idsArr[i];
                            if (fxRenames.TryGetValue(s, out var ne)) { idsArr[i] = ne; idsRemapped++; }
                            else if (atomRenames != null && atomRenames.TryGetValue(s, out var na)) { idsArr[i] = na; idsRemapped++; }
                        }
                    // effectIds 混装（效果库 id+关键词原子 refId，同正式卡口径）；payload.refId 同批跟换
                    foreach (var prop in card.Descendants().OfType<Newtonsoft.Json.Linq.JProperty>()
                             .Where(p => p.Name == "refId" && p.Value.Type == Newtonsoft.Json.Linq.JTokenType.String).ToList())
                    {
                        string rid = (string)prop.Value;
                        if (atomRenames != null && atomRenames.TryGetValue(rid, out var na)) { prop.Value = na; idsRemapped++; }
                    }
                }
            if (idsRemapped > 0)
                WriteJTokenIndented(cardsPath, cardsRoot); // 先落跟换再装载（解析按新效果号/原子号）

            var loaded = CardLoader.LoadCardsFromText(File.ReadAllText(cardsPath));
            if (loaded == null || loaded.Count == 0)
            {
                Debug.LogError($"[Regen] 训练卡表装载为空/失败（effectIds 跟换后解析断裂？）——中止，卡表未换号：{cardsPath}");
                return;
            }
            if (cardsArr != null && loaded.Count != cardsArr.Count)
                Debug.LogWarning($"[Regen] 训练卡表装载 {loaded.Count} 张 ≠ 文件 {cardsArr.Count} 条（坏行被剔除？）——按装载序回写，请人工核对");
            if (HasDuplicateCardIds(loaded, "训练")) return;

            int costRewritten = 0, idRekeyed = 0, cardDrift = 0;
            for (int i = 0; i < loaded.Count && cardsArr != null && i < cardsArr.Count; i++)
            {
                var card = loaded[i];
                var obj = cardsArr[i] as Newtonsoft.Json.Linq.JObject;
                if (card == null || obj == null) continue;
                card.ResetCache();
                CardCostService.EnsureCost(card); // 恒推导：costList 显示快照重刷（费用不参与卡哈希）
                var r = CardCostService.Derive(card);
                string costStr = card.Cost != null && !card.Cost.IsZero ? card.Cost.ToString() : "(D=0 空 Cost)";
                if (obj["cardName"] != null)
                    Debug.Log($"[Regen][训练] {obj["cardName"]} S={r.S} K={r.K} E={r.EAnchor} D={r.DerivedTotal} 档位Ĉ={r.DerivedTier}：{costStr}");
                obj["costList"] = new Newtonsoft.Json.Linq.JArray(
                    card.Cost[CardCore.ManaType.Gray], card.Cost[CardCore.ManaType.Red],
                    card.Cost[CardCore.ManaType.Blue], card.Cost[CardCore.ManaType.Green]);
                costRewritten++;
                string nid = "C_" + SynergyUI.ContentHasher.HashCard(card);
                // 2026-11-05 ID 冻结：同教学表口径——只刷 hash 列，已有 id 一律沿用
                obj["hash"] = nid.Substring(2);
                if (string.IsNullOrEmpty((string)obj["id"]))
                {
                    obj["id"] = nid;
                    idRekeyed++;
                }
                else if ((string)obj["id"] != nid)
                {
                    cardDrift++;
                }
            }
            WriteJTokenIndented(cardsPath, cardsRoot);
            Debug.Log($"[Regen][训练] 训练池重推完成：效果换号 {fxRenames.Count} · 卡费用重写 {costRewritten} 张" +
                      $"（新分配 id {idRekeyed} 张·内容漂移 {cardDrift} 张·引用跟换 {idsRemapped} 条）→ {cardsPath}");
        }

        /// <summary>迁移台账自愈（AtomMigrationLedger）：JToken 深扫 refId，命中台账且目标行现存则接回，
        /// 有接回才写盘。返回接回处数。目标行缺失=台账过期——不接（留给悬空守卫按孤儿引用报错）。</summary>
        private static int HealDanglingAtomRefs(Newtonsoft.Json.Linq.JContainer root, HashSet<string> validAtomIds, string path)
        {
            int healed = 0;
            var pairs = new List<string>();
            foreach (var prop in root.Descendants().OfType<Newtonsoft.Json.Linq.JProperty>()
                     .Where(p => p.Name == "refId" && p.Value.Type == Newtonsoft.Json.Linq.JTokenType.String).ToList())
            {
                string rid = (string)prop.Value;
                if (AtomMigrationLedger.TryGetValue(rid, out var nid) && validAtomIds.Contains(nid))
                {
                    prop.Value = nid;
                    healed++;
                    pairs.Add($"{rid}→{nid}");
                }
            }
            if (healed > 0)
            {
                WriteJTokenIndented(path, root);
                Debug.Log($"[Regen] 迁移台账自愈：{Path.GetFileName(path)} 接回悬空原子引用 {healed} 处（{string.Join("，", pairs.Distinct())}）");
            }
            return healed;
        }

        /// <summary>卡组 json 卡 id 跟换（值等于卡 id 的字符串全覆盖：cardIds/skillCardId/
        /// tokenCardIds/tokenBindings 模板位；带 #N 实例后缀同跟）。无命中不写盘。
        /// 2026-11-01 修复：旧口径只扫 JProperty（对象成员）——**cardIds 是字符串数组**，
        /// 数组元素是 JValue 不是 JProperty，三套预组的 cardIds 因此从未被跟换过（跑完重推导
        /// 卡 id 换号后卡组仍指旧号=整组吊死）。改为直扫全部字符串 JValue，数组/对象统一覆盖。</summary>
        private static void RemapDeckCardRefs(string path, Dictionary<string, string> renames)
        {
            var root = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(path));
            int n = 0;
            foreach (var val in root.Descendants().OfType<Newtonsoft.Json.Linq.JValue>()
                     .Where(v => v.Type == Newtonsoft.Json.Linq.JTokenType.String).ToList())
            {
                string s = (string)val.Value;
                if (string.IsNullOrEmpty(s)) continue;
                int hash = s.IndexOf('#');
                string baseId = hash > 0 ? s.Substring(0, hash) : s;
                if (renames.TryGetValue(baseId, out var nid) && nid != baseId)
                {
                    val.Value = hash > 0 ? nid + s.Substring(hash) : nid;
                    n++;
                }
            }
            if (n > 0)
            {
                WriteJTokenIndented(path, root);
                Debug.Log($"[Regen] 卡组卡 id 跟换 {n} 处 → {path}");
            }
        }

        // ======================================== 两个随机（2026-09-13 定案） ========================================

        /// <summary>
        /// 两个随机端到端：
        /// ① 数值随机：RollValue 边界（3±100% 样本 ⊆[0,6] 且两端可达；幅度 0 恒名义值；
        ///    实例名义 Value 字段不随掷值漂移——计价锚点）；
        /// ② 单位随机（RandomTarget 标志，2026-09-16 自 SelectionMode 移出）：TargetCount=2 从完整候选域
        ///    种子抽取（数量=2、成员⊆候选、不弹交互）；隐密/潜行口径——手动显示域不含（弹窗不显示）、
        ///    完整候选域含、随机可命中（绕过选择）；全部档（TargetCount≤0）≡全取；选一/选多预检用显示域
        ///    （唯一候选被隐藏时不可发动；扰魔断言已随 2026-10-13 退役改隐密）。
        /// </summary>
        private static void TestRandomness(GameCore core, Player p1, Player p2)
        {
            EnsureMainPhase(core, p1);

            int seq = 0;
            Card Spawn(Player owner, int power, int life, params string[] keywords)
            {
                var data = new CardData
                {
                    ID = "VERIFY_RND_" + (++seq), CardName = "RND" + seq,
                    Supertype = Cardtype.Creature, Power = power, Life = life,
                };
                foreach (var kw in keywords) data.Keywords.Add(kw);
                var card = new CardWrapper(data);
                card.SetController(owner);
                Assert(core.ZoneManager.TryAddToBattlefield(card, owner), $"随机段合成随从入场（{data.CardName}）");
                card.Untap();
                return card;
            }

            // ---- 0. RollValue 边界 ----
            GameRng.Reseed(20260913);
            int lo = int.MaxValue, hi = int.MinValue;
            for (int i = 0; i < 500; i++)
            {
                int v = GameRng.RollValue(3, 1f);
                if (v < 0 || v > 6) { Assert(false, $"RollValue 越界: {v}（应 ⊆[0,6]）"); break; }
                lo = System.Math.Min(lo, v);
                hi = System.Math.Max(hi, v);
            }
            Assert(lo == 0 && hi == 6, $"数值随机：3±100% 样本覆盖 [0,6] 两端可达（实际 [{lo},{hi}]）");
            bool steady = true;
            for (int i = 0; i < 50; i++)
                if (GameRng.RollValue(3, 0f) != 3) { steady = false; break; }
            Assert(steady, "数值随机：幅度 0 恒名义值");

            var inst = new CardCore.AtomicEffectInstance
            {
                Type = AtomicEffectType.DealDamage,
                Value = 3,
                RandomAmplitude = 1f,
            };
            bool inRange = true;
            for (int i = 0; i < 200; i++)
            {
                int v = inst.GetRolledValue();
                if (v < 0 || v > 6) { inRange = false; break; }
            }
            Assert(inRange && inst.Value == 3, "实例掷值：GetRolledValue ⊆[0,6] 且名义 Value 恒 3（计价锚点不漂移）");

            // ---- 1. 隐密口径：完整候选域 vs 手动显示域（原扰魔断言 2026-10-13 随机制退役改隐密同口径） ----
            var hiddenUnit = Spawn(p2, 2, 2);
            hiddenUnit.AddKeyword(Attribute.KeywordRules.Concealed, KeywordLane.Setting);
            var plain = Spawn(p2, 3, 3);
            var ctx = new EffectExecutionContext
            {
                Source = p1,
                Controller = p1,
                ZoneManager = core.ZoneManager,
                ElementPool = core.ElementPool,
            };
            var kinds = new List<int> { (int)CardCore.TargetKind.EnemyLivingUnit };
            var full = CardCore.Attribute.EffectHandlerRegistry.ResolveCandidates(kinds, "", ctx);
            Assert(full.Contains(hiddenUnit) && full.Contains(plain),
                   "隐密口径：完整候选域含隐密单位（范围波及/随机可命中——域层不再剔除）");
            var manual = CardCore.TargetResolver.ExcludeUnselectable(full, p1);
            Assert(!manual.Contains(hiddenUnit) && manual.Contains(plain),
                   "隐密口径：手动显示域不含隐密（弹窗不显示，AI/无头代替选取同口径）");

            // ---- 2. 目标随机：TargetCount=2 不弹交互、从完整域抽取 ----
            var def = new CardCore.EffectDefinition
            {
                Id = "VERIFY_RANDOM_2",
                SelectionMode = CardCore.SelectionMode.Multiple, // 单范围·选多（域={EnemyLivingUnit}）
                RandomTarget = true,
                TargetCount = 2,
                TargetDomain = new List<int>(kinds),
            };
            var picked = CardCore.Attribute.EffectHandlerRegistry
                .ResolveCompositionTargetsAsync(def, ctx).GetAwaiter().GetResult();
            Assert(picked.Count == 2 && picked.All(t => full.Contains(t)),
                   $"单位随机：抽 2 且成员 ⊆ 完整候选域（实际 {picked.Count} 个）");

            // 随机可命中隐密（概率断言：候选=p2+hiddenUnit+plain 共 3，抽 2 单轮命中概率 2/3；40 轮全 miss ≈ 1e-19）
            GameRng.Reseed(777);
            bool hiddenEverHit = false;
            for (int i = 0; i < 40 && !hiddenEverHit; i++)
                hiddenEverHit = CardCore.Attribute.EffectHandlerRegistry
                    .ResolveCompositionTargetsAsync(def, ctx).GetAwaiter().GetResult()
                    .Contains(hiddenUnit);
            Assert(hiddenEverHit, "单位随机：随机可命中隐密（绕过选择——不弹弹窗不受显示域限制）");

            // 全部档（TargetCount≤0）随机 ≡ 全取
            var defAll = new CardCore.EffectDefinition
            {
                Id = "VERIFY_RANDOM_ALL",
                SelectionMode = CardCore.SelectionMode.Multiple,
                RandomTarget = true,
                TargetDomain = new List<int>(kinds),
            };
            var pickedAll = CardCore.Attribute.EffectHandlerRegistry
                .ResolveCompositionTargetsAsync(defAll, ctx).GetAwaiter().GetResult();
            Assert(pickedAll.Count == full.Count, $"单位随机：全部档 ≡ 全取（{pickedAll.Count} vs {full.Count}）");

            // ---- 3. 选一预检走显示域：唯一候选是扰魔 → 不可发动 ----
            var loneDef = new CardCore.EffectDefinition
            {
                Id = "VERIFY_RANDOM_MANUAL",
                SelectionMode = CardCore.SelectionMode.Single,
                TargetCount = 1,
                // Stealth filter = 仅指潜行中——完整候选只剩新造的潜行单位（普通/隐密被 filter 排除），
                // 而选一/选多显示域又把它隐藏 → "完整域有候选、显示域空"的正交样本
                TargetDomain = new List<int> { (int)CardCore.TargetKind.EnemyLivingUnit },
                TargetFilter = "Stealth",
            };
            var stealthUnit = Spawn(p2, 1, 1);
            stealthUnit.AddCounters(CardCore.Attribute.CounterRules.StealthCounter, 1); // 潜行已指示物化（2026-10-08）
            var stealthCtx = new EffectExecutionContext
            {
                Source = p1,
                Controller = p1,
                ZoneManager = core.ZoneManager,
                ElementPool = core.ElementPool,
            };
            // 潜行也走显示域剔除：Manual 有候选（完整域）但显示域空 → HasCandidates=false（弹窗不空弹）
            var manualPicked = CardCore.Attribute.EffectHandlerRegistry
                .ResolveCompositionTargetsAsync(loneDef, stealthCtx).GetAwaiter().GetResult();
            Assert(manualPicked.Count == 0,
                   "弹窗口径：潜行单位在 Manual 显示域外——唯一候选被隐藏则选择结果为空");
            Assert(CardCore.TargetDomainService.HasCandidates(loneDef, stealthCtx) == false,
                   "弹窗口径：Manual 预检用显示域（不可发动），潜行可被随机/全域命中");

            RetireCards(core, p1);
            RetireCards(core, p2, hiddenUnit, plain, stealthUnit);
        }

        // ======================================== 帷幕/紊乱效果目标口径（2026-09-13 修复+更名） ========================================

        /// <summary>
        /// 目标硬限制：①帷幕（原"嘲讽"2026-09-13 更名，与守卫职责冲突后收窄）=只吸引**效果**目标、
        /// 不拦攻击——攻击侧自由（守卫拦截承担强制）；效果侧选择层（Manual 显示域/Random 抽取池
        /// 收窄为帷幕卡；Full 全域不受限）+ 外给目标收口；②紊乱=不可指角色——候选域层剔除（既有）
        /// + 外给目标收口（修复）。
        /// </summary>
        private static void TestTauntAndSickness(GameCore core, Player p1, Player p2)
        {
            EnsureMainPhase(core, p1);

            int seq = 0;
            Card Spawn(Player owner, int power, int life, params string[] keywords)
            {
                var data = new CardData
                {
                    ID = "VERIFY_TS_" + (++seq), CardName = "TS" + seq,
                    Supertype = Cardtype.Creature, Power = power, Life = life,
                };
                foreach (var kw in keywords) data.Keywords.Add(kw);
                var card = new CardWrapper(data);
                card.SetController(owner);
                Assert(core.ZoneManager.TryAddToBattlefield(card, owner), $"帷幕段合成随从入场（{data.CardName}）");
                card.Untap();
                return card;
            }

            var ctx = new EffectExecutionContext
            {
                Source = p1,
                Controller = p1,
                ZoneManager = core.ZoneManager,
                ElementPool = core.ElementPool,
            };
            var enemyKinds = new List<int> { (int)CardCore.TargetKind.EnemyLivingUnit };

            // ---- 1. 帷幕：不拦攻击（攻击侧由守卫承担） ----
            var curtain = Spawn(p2, 2, 5, Attribute.KeywordRules.Taunt);
            var other = Spawn(p2, 3, 3);
            var attacker = Spawn(p1, 4, 4);
            var combat = core.CombatSystem;
            Assert(combat.CanAttackTarget(attacker, p2, p1) && combat.CanAttackTarget(attacker, other, p1)
                   && combat.CanAttackTarget(attacker, curtain, p1),
                   "帷幕（2026-09-13 更名）：不拦攻击——角色/非帷幕/帷幕随从均可指（攻击侧由守卫承担）");

            // ---- 2. 帷幕：效果侧选择层（选一/随机收窄；全取不受限） ----
            var manualDef = new CardCore.EffectDefinition
            {
                Id = "VERIFY_TAUNT_M", SelectionMode = CardCore.SelectionMode.Single,
                TargetCount = 1, TargetDomain = new List<int>(enemyKinds),
            };
            var manualPicked = CardCore.Attribute.EffectHandlerRegistry
                .ResolveCompositionTargetsAsync(manualDef, ctx).GetAwaiter().GetResult();
            Assert(manualPicked.Count == 1 && manualPicked[0] == curtain,
                   "帷幕效果侧：选一档选择收窄为帷幕卡（角色/非帷幕随从不可选）");

            var randDef = new CardCore.EffectDefinition
            {
                Id = "VERIFY_TAUNT_R", SelectionMode = CardCore.SelectionMode.Single,
                RandomTarget = true,
                TargetCount = 1, TargetDomain = new List<int>(enemyKinds),
            };
            var randPicked = CardCore.Attribute.EffectHandlerRegistry
                .ResolveCompositionTargetsAsync(randDef, ctx).GetAwaiter().GetResult();
            Assert(randPicked.Count == 1 && randPicked[0] == curtain,
                   "帷幕效果侧：随机抽取池收窄为帷幕卡（指定与随机同受限）");

            var fullDef = new CardCore.EffectDefinition
            {
                Id = "VERIFY_TAUNT_F", SelectionMode = CardCore.SelectionMode.Whole,
                TargetDomain = new List<int>(enemyKinds),
            };
            var fullPicked = CardCore.Attribute.EffectHandlerRegistry
                .ResolveCompositionTargetsAsync(fullDef, ctx).GetAwaiter().GetResult();
            Assert(fullPicked.Contains(other) && fullPicked.Contains(curtain),
                   "帷幕效果侧：全取档全域不受限（范围波及照常命中全部）");

            // ---- 3. 帷幕：外给目标收口（声明期 UI/AI 直给目标） ----
            var external = new List<Entity> { p2, other, curtain };
            var filtered = CardCore.TargetResolver.FilterPreselectedTargets(external, p1, p1, core.ZoneManager);
            Assert(filtered.Count == 1 && filtered[0] == curtain,
                   "帷幕：外给目标收口——非法目标剔除，仅留帷幕卡");

            // ---- 4. 紊乱：不可指角色（候选域 + 攻击 + 外给收口） ----
            var sick = Spawn(p1, 3, 3);
            sick.AddCounters(Attribute.KeywordRules.RushSicknessCounter, 1);
            var sickCtx = new EffectExecutionContext
            {
                Source = sick,
                Controller = p1,
                ZoneManager = core.ZoneManager,
                ElementPool = core.ElementPool,
            };
            var cands = CardCore.Attribute.EffectHandlerRegistry.ResolveCandidates(enemyKinds, "", sickCtx);
            Assert(!cands.Contains(p2) && cands.Contains(other),
                   "紊乱：效果候选域剔除角色（源紊乱的效果不可指角色）");
            Assert(!combat.CanAttackTarget(sick, p2, p1) && combat.CanAttackTarget(sick, other, p1),
                   "紊乱：攻击不可指角色、随从照常");

            p1.AddCounters(Attribute.KeywordRules.RushSicknessCounter, 1); // 施法者紊乱（法术来源=角色）
            var extSick = CardCore.TargetResolver.FilterPreselectedTargets(
                new List<Entity> { p2, curtain }, p1, p1, core.ZoneManager);
            Assert(extSick.Count == 1 && extSick[0] == curtain,
                   "紊乱：外给目标收口剔除角色（帷幕收窄同批生效）");
            p1.AddCounters(Attribute.KeywordRules.RushSicknessCounter, -1); // 清理

            RetireCards(core, p1, attacker, sick);
            RetireCards(core, p2, curtain, other);
        }

        // ======================================== 全域组合表达（2026-09-21 固有全域原子退役） ========================================

        /// <summary>
        /// 全域语义端到端（2026-09-21 定案：固有全域原子已退役，全域=组合期 TargetKinds 定域 +
        /// SelectionMode 全取档）——行为面：打出后对域内全部有生命单位（含角色）结算，无弹窗。
        /// （2026-10-09 清理：①计价锚（全取按期望目标数4=红6）删除——2026-10-05 档位化后全取档=3，旧期望4 口径过时。）
        /// </summary>
        private static void TestFullDomainCompose(GameCore core, Player p1, Player p2)
        {
            EnsureMainPhase(core, p1);

            // ---- 4. 行为端到端：对双方全部有生命单位（含角色）结算，无弹窗 ----
            int seq = 0;
            Card Spawn(Player owner, int power, int life)
            {
                var data = new CardData
                {
                    ID = "VERIFY_SWEEP_" + (++seq), CardName = "SW" + seq,
                    Supertype = Cardtype.Creature, Power = power, Life = life,
                };
                var card = new CardWrapper(data);
                card.SetController(owner);
                Assert(core.ZoneManager.TryAddToBattlefield(card, owner), $"固有全域段合成随从入场（{data.CardName}）");
                card.Untap();
                return card;
            }

            var a1 = Spawn(p1, 2, 5);
            var a2 = Spawn(p1, 3, 5);
            var b1 = Spawn(p2, 2, 5);
            var b2 = Spawn(p2, 3, 5);

            var pool1 = core.ElementPool.GetPool(p1);
            var snap = new Dictionary<ManaType, int>(pool1.TempElements);
            try
            {
                FillSixColors(pool1, 99);

                var sweepCardData = new CardData
                {
                    ID = "VERIFY_SWEEP_CR", CardName = "验证全域伤害", Supertype = Cardtype.Spell,
                    Cost = CostOf((ManaType.Red, 4)),
                };
                sweepCardData.Effects.Add(new CardEffectData
                {
                    Id = "VERIFY_SWEEP_ONPLAY",
                    TriggerTiming = (int)TriggerTiming.OnPlay,
                    SelectionMode = (int)CardCore.SelectionMode.WholeUnion, // 全域=组合期全取档（2026-09-21）
                    AtomicEffects = new List<AtomicEffectEntry>
                    {
                        // 双侧域 {1,2}（SideLock=0 不属错边）；显式费红4=1×1×期望4 口径
                        AtomRefs.New(CardCore.AtomicEffectType.DealDamage, value: 1, kinds: new List<int> { 1, 2 })
                    },
                });
                var sweepCard = InjectCard(core, p1, sweepCardData);
                int p1Life = p1.Life, p2Life = p2.Life;
                Assert(GameActions.PlayCard(core, p1, sweepCard), "打出全域伤害（红4）");
                GameActions.DrainStack(core);

                Assert(a1.GetLife() == 4 && a2.GetLife() == 4 && b1.GetLife() == 4 && b2.GetLife() == 4
                       && p1.Life == p1Life && p2.Life == p2Life,
                       "全域行为：双方全部生物各受 1（全取档结算，域={1,2}；角色不参与全取——2026-11-06 定案）");
            }
            finally
            {
                foreach (var kv in snap) pool1.TempElements[kv.Key] = kv.Value;
            }

            RetireCards(core, p1, a1, a2);
            RetireCards(core, p2, b1, b2);
        }

        // ======================================== 牺牲原子（2026-10-09 舍弃并档定案） ========================================

        /// <summary>
        /// 牺牲原子端到端：①表行锚（黑5 Polarity=-1 域 1,2,3,4 filter "Player"=仅角色）；
        /// ②候选域=双方角色（单位不入候选——Player 过滤激活）；③行为：目标玩家（持有者）自行选择
        /// 一个己方战场单位（生物+无生命单位，摒弃并池）**直送墓地**——非效果死亡：
        /// 不发 CardDestroyEvent（死亡时点/消灭替代/击杀统计不触发），不灭无从拦
        ///（非死亡路径；headless 自动选首个；多单位时走交互 Chooser=持有者）。
        /// </summary>
        private static void TestSacrificeAtom(GameCore core, Player p1, Player p2)
        {
            EnsureMainPhase(core, p1);
            // 段界清池（前序段落入池卡不外泄——池账跨段存留会毒化本段候选序[战场序→地牌序]；
            // 正式路径卡 zone 字段齐全走 MoveCard，裸入池卡容器直删）
            foreach (var pc in core.ElementPool.GetPool(p1).PooledCards.ToList())
            {
                var leftover = pc.SourceCard;
                core.ElementPool.RemoveCardFromPool(leftover, p1);
                if (leftover.GetZone() == Zone.ElementPool)
                    core.ZoneManager.MoveCard(leftover, p1, Zone.ElementPool, Zone.Graveyard);
                else core.ZoneManager.GetZoneContainer(p1).Remove(leftover, Zone.ElementPool);
            }
            // 容器孤儿（前序段落裸入池/回收残账——无包装器，上述包装器清扫覆盖不到）
            foreach (var orphan in core.ZoneManager.GetCards(p1, Zone.ElementPool).ToList())
                core.ZoneManager.GetZoneContainer(p1).Remove(orphan, Zone.ElementPool);

            // ---- 1. 表行锚（2026-11-03 改版：0费 Pol=0 域={1}——自选N送墓每个+1临时彩元素） ----
            var cfg = CardCore.Attribute.AtomicEffectTable.GetByType(AtomicEffectType.Sacrifice);
            Assert(cfg != null && cfg.TotalUnitCost == 0f && cfg.Polarity == 0f
                   && cfg.GetTargetKindList().SequenceEqual(new[] { 1 })
                   && (cfg.TargetFilter ?? "") == ""
                   && CardCore.TargetAxisRules.TryParseDomain(cfg.Domain, out var sacAxis)
                   && sacAxis.Zh == "己方战场:卡"
                   && ElementAffinities.GetAffinityForEffect(AtomicEffectType.Sacrifice).IsGeneric,
                   "牺牲表行：0费 Pol=0 域={1} Domain=己方战场:卡（A3 定案 2026-11-05：选单位送墓——生物+结界+池内地牌全卡）");

            // ---- 2. 候选域：Player 过滤滤单位得角色（机制口四单位类→双方角色）；表行域收窄→仅己方角色 ----
            var ctx = new EffectExecutionContext
            {
                Source = p1,
                Controller = p1,
                ZoneManager = core.ZoneManager,
                ElementPool = core.ElementPool,
            };
            var candidates = CardCore.Attribute.EffectHandlerRegistry.ResolveCandidates(
                new List<int> { (int)CardCore.TargetKind.OwnLivingUnit, (int)CardCore.TargetKind.EnemyLivingUnit,
                                (int)CardCore.TargetKind.OwnNonLivingUnit, (int)CardCore.TargetKind.EnemyNonLivingUnit },
                "Player", ctx);
            Assert(candidates.Contains(p1) && candidates.Contains(p2) && candidates.All(c => c is Player),
                   "牺牲候选域：仅双方角色（Player 过滤滤除单位）");
            var rowCandidates = CardCore.Attribute.EffectHandlerRegistry.ResolveCandidates(
                cfg.GetTargetKindList(), "Player", ctx);
            Assert(rowCandidates.Contains(p1) && !rowCandidates.Contains(p2),
                   "牺牲候选域收窄：表行域={1}（己方单位）→ 仅己方角色（旧双方域作废）");

            // ---- 3. 行为：持有者交出单位（规则送墓非死亡；不灭无从拦）——每个 +1 临时彩元素 ----
            int seq = 0;
            Card Spawn(Player owner, int power, int life, params string[] keywords)
            {
                var data = new CardData
                {
                    ID = "VERIFY_SAC_" + (++seq), CardName = "SAC" + seq,
                    Supertype = Cardtype.Creature, Power = power, Life = life,
                };
                foreach (var kw in keywords) data.Keywords.Add(kw);
                var card = new CardWrapper(data);
                card.SetController(owner);
                Assert(core.ZoneManager.TryAddToBattlefield(card, owner), $"牺牲段合成随从入场（{data.CardName}）");
                card.Untap();
                return card;
            }

            var foeIndestructible = Spawn(p2, 3, 3, Attribute.KeywordRules.Indestructible); // 唯一单位=不灭——自动选它
            var mine = Spawn(p1, 2, 2); // p1 唯一单位

            var atom = new CardCore.AtomicEffectInstance { Type = AtomicEffectType.Sacrifice, Value = 1 };
            var execCtx = new EffectExecutionContext
            {
                Source = p1,
                Controller = p1, // 施法者=p1；送墓归属=各自持有者
                ZoneManager = core.ZoneManager,
                ElementPool = core.ElementPool,
                Targets = new List<Entity> { p2, p1 }, // 作用对象=双方角色（handler 逐角色结算——机制口）
            };
            int sacDestroyEvents = 0;
            void OnSacDestroy(CardDestroyEvent e) => sacDestroyEvents++;
            EventManager.Instance.Subscribe<CardDestroyEvent>(OnSacDestroy);
            int anyP1 = core.ElementPool.GetAvailableManaCount(ManaType.Any, p1);
            int anyP2 = core.ElementPool.GetAvailableManaCount(ManaType.Any, p2);
            Assert(CardCore.Attribute.EffectHandlerRegistry.ExecuteEffect(atom, execCtx), "牺牲原子执行（作用对象=双方角色）");
            EventManager.Instance.Unsubscribe<CardDestroyEvent>(OnSacDestroy);

            Assert(!foeIndestructible.IsAlive && core.ZoneManager.IsCardInZone(foeIndestructible, p2, Zone.Graveyard),
                   "牺牲：对手持有者交出唯一单位（规则送墓非死亡——不灭无从拦）");
            Assert(!mine.IsAlive && core.ZoneManager.IsCardInZone(mine, p1, Zone.Graveyard),
                   "牺牲：己方持有者同批交出（送墓归属=各自持有者）");
            Assert(sacDestroyEvents == 0, "牺牲非死亡：全批不发 CardDestroyEvent（死亡时点不触发）");
            Assert(core.ElementPool.GetAvailableManaCount(ManaType.Any, p1) == anyP1 + 1
                   && core.ElementPool.GetAvailableManaCount(ManaType.Any, p2) == anyP2 + 1,
                   "牺牲入账：每送墓一个单位 +1 临时彩元素（交出方各 +1，任一回合结束清空）");

            // ---- 3b. 多选 {value}（2026-11-03 改版）：候选序前 N 送墓（战场序优先、地牌殿后——3 候选取 2） ----
            var u1 = Spawn(p1, 1, 1);
            var u2 = Spawn(p1, 1, 1);
            var landData = new CardData
            {
                // 2/2 身材推导绿 1——锁色候选非空才可入池（0/1 零费卡 GetLandColorChoices 空被拒）；
                // 2026-11-06 opt-in 补攻守：无资格则免费属性底吞掉 2/2 身材、推导 0 费入池被拒
                ID = "VERIFY_SAC_LAND", CardName = "牺牲之地", Supertype = Cardtype.Creature,
                AttackAbility = true, GuardAbility = true, Power = 2, Life = 2,
            };
            // 正式放地路径：入手牌（路径闸要求）→ 三写齐全（池包装器+容器+卡 zone 字段——
            // 裸 AddCardToPool 不写 zone 字段会让 SendToGrave 的 MoveCard 落空；旧手工补容器登记造重复条目）
            var land = InjectCard(core, p1, landData);
            Assert(GameActions.AddToElementPool(core, p1, land), "牺牲段：地牌入池");

            var multiCtx = new EffectExecutionContext
            {
                Source = p1,
                Controller = p1,
                ZoneManager = core.ZoneManager,
                ElementPool = core.ElementPool,
                Targets = new List<Entity> { p1 },
            };
            int anyP1b = core.ElementPool.GetAvailableManaCount(ManaType.Any, p1);
            Assert(CardCore.Attribute.EffectHandlerRegistry.ExecuteEffect(
                       new CardCore.AtomicEffectInstance { Type = AtomicEffectType.Sacrifice, Value = 2 }, multiCtx),
                   "牺牲原子多选执行（value=2·候选 3=单位2+地牌1）");
            Assert(!u1.IsAlive && !u2.IsAlive
                   && core.ZoneManager.IsCardInZone(u1, p1, Zone.Graveyard)
                   && core.ZoneManager.IsCardInZone(u2, p1, Zone.Graveyard),
                   "牺牲多选：自动取候选前 2 个送墓（战场序优先）");
            Assert(core.ZoneManager.GetCards(p1, Zone.ElementPool).Contains(land),
                   "牺牲多选：地牌殿后不占前 2 席（候选序=战场序→地牌序）");
            Assert(core.ElementPool.GetAvailableManaCount(ManaType.Any, p1) == anyP1b + 2,
                   "牺牲多选入账：2 个单位 = +2 临时彩元素");

            // ---- 3c. 地牌扩池（2026-11-03 定案：牺牲范围=场上地牌+生物+结界）：地牌出池入墓照计 Any ----
            int anyP1c = core.ElementPool.GetAvailableManaCount(ManaType.Any, p1);
            Assert(CardCore.Attribute.EffectHandlerRegistry.ExecuteEffect(atom, multiCtx),
                   "牺牲原子再执行（战场空、仅池内地牌——地牌为唯一候选）");
            Assert(core.ZoneManager.IsCardInZone(land, p1, Zone.Graveyard)
                   && !core.ZoneManager.GetCards(p1, Zone.ElementPool).Contains(land),
                   $"牺牲扩池：地牌入候选——出池直送墓（旧「地牌非单位不入池」口径作废）" +
                   $"——诊断 land区={land.GetZone()} alive={land.IsAlive} 池内={core.ElementPool.GetPool(p1).PooledCards.Count}" +
                   $" 战场余={core.ZoneManager.GetCards(p1, Zone.Battlefield).Count}");
            Assert(!core.ElementPool.GetPool(p1).PooledCards.Any(pc => pc.SourceCard == land),
                   "牺牲扩池：出池落账（PooledCards 移除）");
            Assert(core.ElementPool.GetAvailableManaCount(ManaType.Any, p1) == anyP1c + 1,
                   "牺牲扩池入账：地牌送墓同计 +1 临时彩元素");

            // ---- 4. 帷幕豁免：牺牲类选择权在目标方——帷幕只约束对手的选择（机制口与表行域解耦：
            //      手工四单位类域——域收窄后真实卡只能以己方角色为作用对象，豁免机制本段独立验证） ----
            var curtain = Spawn(p2, 2, 5, Attribute.KeywordRules.Taunt);
            var p1Second = Spawn(p1, 2, 2);
            var sacDef = new CardCore.EffectDefinition
            {
                Id = "VERIFY_SAC_CURTAIN",
                // 缺省 SelectionMode=None 会让组合解析早退返回空目标——显式选多·多范围（域跨双方角色）
                SelectionMode = CardCore.SelectionMode.MultipleUnion,
                // 候选序=单位在角色前：count=4 全取（两单位+两角色）——锚「角色不被帷幕收窄」本身
                TargetCount = 4,
                TargetDomain = new List<int> { (int)CardCore.TargetKind.OwnLivingUnit, (int)CardCore.TargetKind.EnemyLivingUnit,
                                               (int)CardCore.TargetKind.OwnNonLivingUnit, (int)CardCore.TargetKind.EnemyNonLivingUnit },
                // B2：Player 类别 token 已删——本段验证豁免机制（手工域，与表行域解耦）：域=四单位类直接表达
                TargetFilter = null,
                Effects = new List<CardCore.AtomicEffectInstance> { new CardCore.AtomicEffectInstance { Type = AtomicEffectType.Sacrifice, Value = 1 } },
            };
            var curtainPicked = CardCore.Attribute.EffectHandlerRegistry
                .ResolveCompositionTargetsAsync(sacDef, ctx).GetAwaiter().GetResult();
            Assert(curtainPicked.Contains(p2) && curtainPicked.Contains(p1),
                   "牺牲豁免帷幕：对手有帷幕卡时角色目标照常可选（选择权在持有者）");
            var dmgDef2 = new CardCore.EffectDefinition
            {
                Id = "VERIFY_SAC_VS_DMG", SelectionMode = CardCore.SelectionMode.Single,
                TargetCount = 1,
                TargetDomain = new List<int> { (int)CardCore.TargetKind.EnemyLivingUnit },
            };
            var dmgPicked2 = CardCore.Attribute.EffectHandlerRegistry
                .ResolveCompositionTargetsAsync(dmgDef2, ctx).GetAwaiter().GetResult();
            Assert(dmgPicked2.Count == 1 && dmgPicked2[0] == curtain,
                   "对照：普通效果仍受帷幕收窄（对方侧仅帷幕卡）——豁免只给 edict");
            RetireCards(core, p1, p1Second);
            RetireCards(core, p2, curtain);

            // ---- 5. 摒弃并池（2026-10-09 舍弃并档）：无生命单位入牺牲池，规则送墓照计 Any ----
            var p2Enchant = new CardData
            {
                ID = "VERIFY_SAC_ENCH", CardName = "牺牲标的结界", Supertype = Cardtype.Enchantment, Power = 0, Life = 0,
            };
            var enchant = new CardWrapper(p2Enchant);
            enchant.SetController(p2);
            Assert(core.ZoneManager.TryAddToBattlefield(enchant, p2), "牺牲段：结界入场（无生命单位）");
            var sacCtx2 = new EffectExecutionContext
            {
                Source = p1,
                Controller = p1,
                ZoneManager = core.ZoneManager,
                ElementPool = core.ElementPool,
                Targets = new List<Entity> { p2 }, // p2 唯一单位=结界——自动选它
            };
            int anyP2b = core.ElementPool.GetAvailableManaCount(ManaType.Any, p2);
            Assert(CardCore.Attribute.EffectHandlerRegistry.ExecuteEffect(atom, sacCtx2), "牺牲原子再执行（作用对象=对方角色）");
            Assert(core.ZoneManager.IsCardInZone(enchant, p2, Zone.Graveyard),
                   "牺牲并池：持有者交出唯一无生命单位（结界规则送墓——摒弃覆盖并入牺牲）");
            Assert(core.ElementPool.GetAvailableManaCount(ManaType.Any, p2) == anyP2b + 1,
                   "牺牲并池入账：结界送墓同计 +1 临时彩元素");
        }

        // ======================================== 守护（2026-11-05 简化定案：固定护角色） ========================================

        /// <summary>
        /// 守护=关键词（表行位 0 关键词 + 位 7 拉黑——退出连接光环族）：**固定保护己方角色**——
        /// 角色受到的伤害改写为自己受到等量伤害。无弹选无配对表：关键词在场即生效
        /// （GuardianRules.FindGuardian 实时扫描持有者——在场/存活/未被自动压制），改写在
        /// ApplyDamage 咽喉单跳；无限次直到守护者死亡/离场（扫描自然落空，无状态无需断链）；
        /// 墓地复活即恢复保护。2026-10-08 配对制（登场弹选任意己方目标、单位可被守护）退役：
        /// 单位受伤不再改写。旧三形态（箭头光环改写/扫场护角色/关键词挂角色）此前已退役。
        /// </summary>
        private static void TestGuardianRelay(GameCore core, Player p1, Player p2)
        {
            EnsureMainPhase(core, p1);

            // 清场隔离：守护者扫描=己方战场，须控制取首的确定性
            RetireCards(core, p1, core.ZoneManager.GetCards(p1, Zone.Battlefield).ToArray());
            RetireCards(core, p2, core.ZoneManager.GetCards(p2, Zone.Battlefield).ToArray());

            var used = new List<Card>();
            Card Make(Player owner, int power, int life, params string[] keywords)
            {
                var data = new CardData { ID = "VERIFY_GUARD_" + used.Count, CardName = "守" + used.Count };
                data.Supertype = Cardtype.Creature;
                data.Power = power;
                data.Life = life;
                foreach (var kw in keywords) data.Keywords.Add(kw);
                var card = new CardWrapper(data);
                card.SetController(owner);
                Assert(core.ZoneManager.TryAddToBattlefield(card, owner), "守护段入场：" + data.ID);
                used.Add(card);
                return card;
            }

            try
            {
                // ---- 1. 固定绑定（无弹选）：FindGuardian(角色)=持有者；正查回控制器；
                //      单位不再是合法被守护对象（旧配对制退役）----
                var mate = Make(p1, 1, 10);
                var guard = Make(p1, 2, 20, Attribute.KeywordRules.Guardian);
                Assert(guard.HasKeyword(Attribute.KeywordRules.Guardian), "守护：印刷关键词入场物化");
                Assert(ReferenceEquals(Attribute.GuardianRules.FindGuardian(p1), guard),
                       "守护固定绑定：FindGuardian(己方角色)=Guardian 持有者（无弹选，在场即生效）");
                Assert(ReferenceEquals(Attribute.GuardianRules.FindWard(guard), p1),
                       "守护正查：FindWard（状态段书写口 2026-10-16）回固定对象=自身控制器角色");
                Assert(Attribute.GuardianRules.FindGuardian(mate) == null,
                       "守护域收窄：单位受伤不再改写（2026-10-08 配对制的单位被守护对象退役）");

                // ---- 2. 角色受伤改写（无限次不设闸；角色不掉血）----
                int p1Life = p1.Life;
                Attribute.KeywordRules.ApplyDamage(null, p1, 3, false);
                Attribute.KeywordRules.ApplyDamage(null, p1, 2, false);
                Assert(p1.Life == p1Life && guard.GetLife() == 15,
                       "守护改写：角色两笔 3+2 全转守护者（20−5=15，无限次、角色不掉血）");

                // ---- 3. 守护者离场 → 扫描落空 → 角色恢复全额（无状态无需断链）----
                core.ZoneManager.MoveCard(guard, p1, Zone.Battlefield, Zone.Graveyard);
                p1Life = p1.Life;
                Attribute.KeywordRules.ApplyDamage(null, p1, 2, false);
                Assert(p1.Life == p1Life - 2 && Attribute.GuardianRules.FindGuardian(p1) == null,
                       "守护失效：守护者离场（死亡/弹回/流放/牺牲统一）后扫描落空，角色恢复全额");
                Assert(Attribute.GuardianRules.FindWard(guard) == null,
                       "守护正查失效：离场后 FindWard 亦为 null（状态段不残留）");

                // ---- 4. 墓地复活=即恢复保护（无状态：无配对可残留，无需重建）----
                Assert(core.ZoneManager.TryMoveToBattlefield(guard, p1, Zone.Graveyard),
                       "守护复活：苏生入场成功");
                Assert(ReferenceEquals(Attribute.GuardianRules.FindGuardian(p1), guard),
                       "守护复活恢复：无状态实时扫描——入场即护（旧「重新弹选」步骤随配对制退役）");
                p1Life = p1.Life;
                int guardLife = guard.GetLife();
                Attribute.KeywordRules.ApplyDamage(null, p1, 1, false);
                Assert(p1.Life == p1Life && guard.GetLife() == guardLife - 1,
                       "守护复活后再挡：关键词在场即生效");

                // ---- 5. 单跳防链式：直接打守护者本身不再改写 ----
                guardLife = guard.GetLife();
                Attribute.KeywordRules.ApplyDamage(null, guard, 1, false);
                Assert(guard.GetLife() == guardLife - 1, "守护单跳：伤害落在守护者本身不改写（guardRerouted）");

                // ---- 6. 侧别隔离：守护只护本侧角色——对手的守护者不吸收己方角色伤害 ----
                var foe = Make(p2, 3, 6, Attribute.KeywordRules.Guardian);
                Assert(ReferenceEquals(Attribute.GuardianRules.FindGuardian(p2), foe)
                       && ReferenceEquals(Attribute.GuardianRules.FindWard(foe), p2),
                       "守护侧别：对手守护者绑定对手角色（正查回 p2）");
                int p2Life = p2.Life;
                int foeLife = foe.GetLife();
                Attribute.KeywordRules.ApplyDamage(null, p2, 3, false);
                guardLife = guard.GetLife();
                Attribute.KeywordRules.ApplyDamage(null, p1, 2, false);
                Assert(p2.Life == p2Life && foe.GetLife() == foeLife - 3,
                       "守护改写（对手镜像）：对手角色受伤转由其守护者承受");
                Assert(p1.Life == p1Life && guard.GetLife() == guardLife - 2 && foe.IsAlive,
                       "守护侧别隔离：己方角色伤害只进己方守护者（对手守护者不跨侧吸收）");

                // ---- 7. 压制口：沉睡守护者被实时校验跳过（SuppressRules 统一口）----
                foe.AddCounters(Attribute.KeywordRules.SleepCounter, 1, null);
                p2Life = p2.Life;
                Attribute.KeywordRules.ApplyDamage(null, p2, 2, false);
                Assert(p2.Life == p2Life - 2 && Attribute.GuardianRules.FindGuardian(p2) == null,
                       "守护压制：沉睡守护者不生效——角色恢复全额");
            }
            finally
            {
                foreach (var c in used)
                {
                    var owner = c.GetController() ?? p1;
                    core.ZoneManager.GetZoneContainer(owner).Remove(c, Zone.Battlefield);
                    core.ZoneManager.GetZoneContainer(owner).Remove(c, Zone.Graveyard);
                }
            }
        }

        // ======================================== 触发次数（2026-10-30 无上限 + 自馈闸门 30） ========================================

        /// <summary>
        /// 触发式端到端（2026-10-30）：①历史列仍按存档口径透传（EffectSlim/内容哈希稳定，但零消费——
        /// 同价证明）；②同一效果同回合连续多次触发全部入栈、不静默丢弃；③用时台账既作读数
        ///（条件计数/卡面统计）又作**自馈闸门**计数——同一来源实例+效果 Id 每回合上限 30 次，
        /// 第 31 次截断（只防自循环，常规触发远低于此）；④自由分支引擎奖励的每卡每回合 1 次由
        /// BranchEngines 自限（见 TestBranchEngines 元素充盈段）。
        /// </summary>
        private static void TestTriggerCap(GameCore core, Player p1, Player p2)
        {
            EnsureMainPhase(core, p1);

            // ---- 1. 数据兼容：converter 对历史列的解析不变（0→1；显式 N/-1 透传；零消费）----
            CardCore.EffectDefinition Convert(CardEffectData d) => CardEffectConverter.ConvertOne(d, "VERIFY_CAP");
            var normal = Convert(new CardEffectData
            {
                Id = "VERIFY_CAP_1",
                TriggerTiming = (int)TriggerTiming.OnDraw, // 触发式
                AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(CardCore.AtomicEffectType.DrawCard, value: 1) },
            });
            Assert(normal.TriggerLimitPerTurn == 1, "历史列：未声明 → 1（仅存档透传，无运行时消费）");

            var declared = Convert(new CardEffectData
            {
                Id = "VERIFY_CAP_2",
                TriggerTiming = (int)TriggerTiming.OnDraw,
                TriggerLimitPerTurn = 2,
                AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(CardCore.AtomicEffectType.DrawCard, value: 1) },
            });
            Assert(declared.TriggerLimitPerTurn == 2, "历史列：显式 N=2 原样透传（不再影响计价/闸门）");

            var unlimited = Convert(new CardEffectData
            {
                Id = "VERIFY_CAP_3",
                TriggerTiming = (int)TriggerTiming.OnDraw,
                TriggerLimitPerTurn = -1,
                AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(CardCore.AtomicEffectType.DrawCard, value: 1) },
            });
            Assert(unlimited.TriggerLimitPerTurn == -1, "历史列：显式无限（-1）原样透传");

            // 历史档不再溢价：同一 OnDraw 伤害 5 在 limit 1/2/3/-1 下同价（时机两档均 ×1 → 红5）
            CardCore.EffectDefinition TrigDef(int limit) => CardEffectConverter.ConvertOne(new CardEffectData
            {
                Id = $"VERIFY_CAP_P{limit}",
                TriggerTiming = (int)TriggerTiming.OnDraw,
                TriggerLimitPerTurn = limit,
                AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(CardCore.AtomicEffectType.DealDamage, value: 5) },
            }, "VERIFY_CAP_P");
            float RedCostOf(CardCore.EffectDefinition d) => CardCore.CostDerivationService.DeriveElementCosts(d)[CardCore.ManaType.Red];
            Assert(System.Math.Abs(RedCostOf(TrigDef(1)) - 5f) < 1e-4f
                   && System.Math.Abs(RedCostOf(TrigDef(2)) - 5f) < 1e-4f
                   && System.Math.Abs(RedCostOf(TrigDef(3)) - 5f) < 1e-4f
                   && System.Math.Abs(RedCostOf(TrigDef(-1)) - 5f) < 1e-4f,
                   "历史次数档零消费：limit 1/2/3/-1 同价（OnDraw 多次档 ×1 → 红5）");

            // ---- 2. 运行时：同回合 3 次触发全部上栈（无闸门）----
            var src = SpawnCapSource(core, p1);
            var queue = core.StackEngine;
            var cappedDef = normal;
            queue.AddPendingEffect(MakeAutoTrigger(cappedDef, src, p1));
            queue.AddPendingEffect(MakeAutoTrigger(cappedDef, src, p1));
            queue.AddPendingEffect(MakeAutoTrigger(cappedDef, src, p1));
            core.StackEngine.ProcessTriggeredEffects();
            Assert(core.StackEngine.StackSize == 3,
                   "无上限：同回合 3 次触发全部入栈（旧一回合一次闸门已删）");
            Assert(core.StackEngine.GetExecutor().GetTurnUsageOf(cappedDef.Id, src) == 3,
                   "台账仅作读数：3 次入栈记为 3（不再拦住第 2 次）");
            GameActions.DrainStack(core);
            Assert(core.StackEngine.IsEmpty, "无上限：结算排干");

            // 同回合第 4 次触发照常入栈（不再按回合封顶）
            queue.AddPendingEffect(MakeAutoTrigger(cappedDef, src, p1));
            core.StackEngine.ProcessTriggeredEffects();
            Assert(core.StackEngine.StackSize == 1, "无上限：同回合第 4 次触发照常入栈");
            GameActions.DrainStack(core);

            // ---- 2b. 自馈闸门（2026-10-30）：同一来源实例+效果 Id 每回合上限 30 次 ----
            //（2026-11-10 §11 每卡过载预算与旧 per-effect 闸叠加：同一源卡在本段前已累计 4 次
            //  每卡预算——隔离旧闸语义须先清每卡台账（模拟回合更替），否则 31 连发会在第 26 发
            //  先撞每卡 30 过载，旧闸「前 30 入栈」被提前截断）
            CardEffectBudget.Instance.OnNewTurn(999);
            var loopDef = Convert(new CardEffectData
            {
                Id = "VERIFY_CAP_LOOP",
                TriggerTiming = (int)TriggerTiming.OnDraw,
                AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(CardCore.AtomicEffectType.DrawCard, value: 0) },
            });
            for (int i = 0; i < 31; i++)
                queue.AddPendingEffect(MakeAutoTrigger(loopDef, src, p1));
            core.StackEngine.ProcessTriggeredEffects();
            Assert(core.StackEngine.StackSize == 30,
                   "自馈闸门：同一效果同回合第 31 次触发被截断（上限 30；前 30 次正常入栈）");
            Assert(core.StackEngine.GetExecutor().GetTurnUsageOf(loopDef.Id, src) == 30,
                   "自馈闸门：用量台账计到 30（超限不计）");
            core.StackEngine.Clear(); // 不结算 30 个空抽，只验证入栈闸门

            // ---- 3. 旧档 N=2 / 无限档同回合并触发全部入栈 ----
            //（§2b 末尾源卡已挂过载——先清每卡预算再验「旧档不再静默丢弃」的旧闸语义）
            CardEffectBudget.Instance.OnNewTurn(999);
            var twiceDeclared = Convert(new CardEffectData
            {
                Id = "VERIFY_CAP_5",
                TriggerTiming = (int)TriggerTiming.OnDraw,
                TriggerLimitPerTurn = 2,
                AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(CardCore.AtomicEffectType.DrawCard, value: 1) },
            });
            queue.AddPendingEffect(MakeAutoTrigger(twiceDeclared, src, p1));
            queue.AddPendingEffect(MakeAutoTrigger(twiceDeclared, src, p1));
            queue.AddPendingEffect(MakeAutoTrigger(unlimited, src, p1));
            core.StackEngine.ProcessTriggeredEffects();
            Assert(core.StackEngine.StackSize == 3,
                   "无上限：旧档 N=2 与无限档同回合并触发全部入栈（不再静默丢弃）");

            GameActions.DrainStack(core);
            RetireCards(core, p1, src);
        }

        private static Card SpawnCapSource(GameCore core, Player p1)
        {
            var data = new CardData
            {
                ID = "VERIFY_CAP_SRC", CardName = "闸门源", Supertype = Cardtype.Creature, Power = 2, Life = 5,
            };
            var card = new CardWrapper(data);
            card.SetController(p1);
            Assert(core.ZoneManager.TryAddToBattlefield(card, p1), "触发上限段：源随从入场");
            card.Untap();
            return card;
        }

        private static CardCore.PendingEffect MakeAutoTrigger(CardCore.EffectDefinition def, Card source, Player controller)
            => CardCore.PendingEffect.Create(def, source, controller, controller,
                CardCore.PhaseType.Main, 0);

        // ======================================== P9：时机三档 + 无次数上限 + 效果适用卡型（2026-11-10 资格层新口径修订） ========================================

        /// <summary>P9 断言（2026-11-05 两档收敛；2026-11-09 生物专属档拆分；2026-11-10 资格层对齐
        /// 时机系数新口径）：①周期/观察族多次时机挂生物→按 E[N] 判（>1 拒/≤1 过——收回全量拒口径）、
        /// 结界→过、法术→拒（含合成器候选谓词）；攻/守/横置族（4 值）为生物专属自动效果——生物过
        ///（影子 ×1.5·宿主自身行动承担次数）、结界拒（不横置）、法术拒；双宿主 OnAttacked 生物/结界都过；
        /// OnTurnStart 任何宿主拒；引擎载荷豁免（发射窗非重复语义）；②一次性触发三卡型通过；
        /// ③关键词挂法术拒；④系数——影子桶（单次/多次 ×1、OnAttacked 归 Repeat ×1.0、攻/守/横置 ×1.5）
        /// 或新表行 Factor+IsRepeatTiming 归类；⑤全链推导同证（红5×当前模式系数）；⑥历史次数档
        /// limit=2/3/-1 运行时不再拦（同回合全上栈）；⑦内容审计（新口径）+35 行时机×宿主矩阵。</summary>
        private static void TestP9TimingTiersAndEligibility(GameCore core, Player p1)
        {
            EnsureMainPhase(core, p1);

            // ---- 公共 def 夹具 ----
            CardCore.EffectDefinition Def(TriggerTiming t, params CardCore.AtomicEffectType[] types) => new CardCore.EffectDefinition
            {
                Id = "VERIFY_P9",
                TriggerTiming = t,
                Effects = types.Select(tp => new CardCore.AtomicEffectInstance { Type = tp, Value = 1 }).ToList(),
            };
            var repeatDef = Def(TriggerTiming.OnDraw, CardCore.AtomicEffectType.DrawCard); // 周期多次档（抽牌时）
            var onceDef = Def(TriggerTiming.OnPlay, CardCore.AtomicEffectType.DealDamage); // 单次档（登场）
            var tapDef = Def(TriggerTiming.OnAttack, CardCore.AtomicEffectType.DrawCard);  // 攻/守/横置族（生物专属 ×1.5）
            var engineDef = new CardCore.EffectDefinition                                   // 引擎载荷+周期时机（豁免口）
            {
                Id = "VERIFY_P9_ENG",
                TriggerTiming = TriggerTiming.OnTurnStart,
                Effects = new List<CardCore.AtomicEffectInstance>
                {
                    new CardCore.AtomicEffectInstance
                    {
                        Type = CardCore.AtomicEffectType.DrawCard,
                        Branch = new CardCore.BranchPayload
                        {
                            Settle = CardCore.BranchSettleKind.Engine,
                            EngineKind = CardCore.BranchEngineKind.Countdown,
                            EngineParam = 2,
                        },
                    },
                },
            };

            // ---- 1. 周期/观察族多次时机：生物拒（2026-11-05 收回 10-30 放开——耐久经济专属）、
            //      结界过（耐久即次数池）、法术拒（不驻场）----
            Assert(CardCore.EffectEligibility.RejectReason(Cardtype.Creature, repeatDef) != null,
                   "P9-1 周期多次时机挂生物：拒（两档收敛——周期价值须由耐久支付，生物白嫖无对价）");
            Assert(CardCore.EffectEligibility.RejectReason(Cardtype.Enchantment, repeatDef) == null,
                   "P9-1b 周期多次时机挂结界：过（耐久即次数池，每触发 −1 耐久）");
            Assert(CardCore.EffectEligibility.RejectReason(Cardtype.Spell, repeatDef) != null,
                   "P9-1c 周期多次时机挂法术：拒（法术不驻场）");
            var repeatGraph = new SynergyUI.EffectGraphData("P9周期多次")
            {
                header = new CardEffectData
                {
                    Id = "VERIFY_P9_REPEAT",
                    TriggerTiming = (int)TriggerTiming.OnDraw,
                    AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(CardCore.AtomicEffectType.DrawCard, value: 1) },
                },
            };
            Assert(SynergyUI.CardComposerScreen.HostRejectReason(Cardtype.Creature, repeatGraph) != null,
                   "P9-1d 合成器候选分区谓词：周期多次图不进生物候选（原因可读）");
            // 攻/守/横置族（2026-11-09 生物专属档）：归生物专属但生物能挂——宿主自身行动承担次数；结界不横置拒
            Assert(CardCore.EffectEligibility.RejectReason(Cardtype.Creature, tapDef) == null,
                   "P9-1e 攻/守/横置族（生物专属 ×1.5）挂生物：过（横置锚天然限频）");
            Assert(CardCore.EffectEligibility.RejectReason(Cardtype.Enchantment, tapDef) != null,
                   "P9-1e2 攻/守/横置族挂结界：拒（结界不横置、无从触发）");
            Assert(CardCore.EffectEligibility.HostCanTakeTiming(Cardtype.Creature, TriggerTiming.OnAttack)
                   && !CardCore.EffectEligibility.HostCanTakeTiming(Cardtype.Enchantment, TriggerTiming.OnAttack)
                   && !CardCore.EffectEligibility.HostCanTakeTiming(Cardtype.Spell, TriggerTiming.OnAttack),
                   "P9-1e3 合成器时机下拉宿主过滤：攻/守/横置族仅生物可见");
            // 引擎载荷豁免：效果头时机=奖励发射窗，非重复语义（倒计时/运势/充盈存量生物卡合法）
            Assert(CardCore.EffectEligibility.RejectReason(Cardtype.Creature, engineDef) == null,
                   "P9-1f 引擎载荷+周期时机挂生物：过（发射窗豁免——与计价 TimingCostFactor 同口径）");
            // ---- 1b. 2026-11-10 资格层新口径：双宿主 / E[N] 门槛 / OnTurnStart 全宿主禁 ----
            var uniDef = Def(TriggerTiming.OnAttacked, CardCore.AtomicEffectType.DrawCard); // 双宿主（被攻击）
            Assert(CardCore.EffectEligibility.RejectReason(Cardtype.Creature, uniDef) == null
                   && CardCore.EffectEligibility.RejectReason(Cardtype.Enchantment, uniDef) == null,
                   "P9-1g 双宿主时机 OnAttacked：生物/结界都过（脱离生物专属，计价按 Repeat 桶/行 E[N]——无溢价）");
            Assert(CardCore.EffectEligibility.HostCanTakeTiming(Cardtype.Creature, TriggerTiming.OnAttacked)
                   && CardCore.EffectEligibility.HostCanTakeTiming(Cardtype.Enchantment, TriggerTiming.OnAttacked),
                   "P9-1g2 合成器下拉：OnAttacked 生物/结界双宿主可见");
            Assert(CardCore.EffectEligibility.RejectReason(Cardtype.Creature,
                       Def(TriggerTiming.OnScry, CardCore.AtomicEffectType.DrawCard)) == null
                   && CardCore.EffectEligibility.RejectReason(Cardtype.Enchantment,
                       Def(TriggerTiming.OnScry, CardCore.AtomicEffectType.DrawCard)) == null,
                   $"P9-1h 低频多次档 E[N]≤1（OnScry={CardCore.TimingWeights.ExpectedOf(TriggerTiming.OnScry):0.##}）：生物/结界都过（接受方差，不加每回合闸）");
            string creatureHi = CardCore.EffectEligibility.RejectReason(Cardtype.Creature,
                Def(TriggerTiming.OnStandbyPhase, CardCore.AtomicEffectType.DrawCard));
            Assert(creatureHi != null && creatureHi.Contains("结界专属"),
                   $"P9-1i 高频多次档 E[N]>1（OnStandbyPhase={CardCore.TimingWeights.ExpectedOf(TriggerTiming.OnStandbyPhase):0.##}）：生物拒（reason 含「结界专属」——实际 {creatureHi ?? "null"}）");
            var turnStartDef = Def(TriggerTiming.OnTurnStart, CardCore.AtomicEffectType.DrawCard);
            Assert(CardCore.EffectEligibility.RejectReason(Cardtype.Creature, turnStartDef) != null
                   && CardCore.EffectEligibility.RejectReason(Cardtype.Enchantment, turnStartDef) != null
                   && CardCore.EffectEligibility.RejectReason(Cardtype.Spell, turnStartDef) != null
                   && !CardCore.EffectEligibility.HostCanTakeTiming(Cardtype.Creature, TriggerTiming.OnTurnStart)
                   && !CardCore.EffectEligibility.HostCanTakeTiming(Cardtype.Enchantment, TriggerTiming.OnTurnStart),
                   "P9-1j OnTurnStart 任何宿主拒（系统专用——不靠 Factor=0 静默变免费，装载期点名 Error）");

            // ---- 2. 一次性触发（登场=施放即结算的一次性形态）三种卡型都通过 ----
            Assert(CardCore.EffectEligibility.RejectReason(Cardtype.Creature, onceDef) == null
                   && CardCore.EffectEligibility.RejectReason(Cardtype.Enchantment, onceDef) == null
                   && CardCore.EffectEligibility.RejectReason(Cardtype.Spell, onceDef) == null,
                   "P9-2 一次性触发（登场）挂生物/结界/法术三卡型都通过");

            // ---- 3. 关键词挂法术 → 拒（生物/结界印刷位不受影响）----
            var kwRow = CardCore.Attribute.AtomicEffectTable.GetAll()
                .FirstOrDefault(r => CardCore.ComposerCatalog.HasMountBit(r, CardCore.MountKind.Keyword));
            CardCore.AtomicEffectType kwType = CardCore.AtomicEffectType.DrawCard;
            bool kwParsed = kwRow != null
                            && System.Enum.TryParse<CardCore.AtomicEffectType>(kwRow.EnumName, out kwType);
            Assert(kwParsed, "P9-3 前置：原子表存在关键词挂位行");
            var kwDef = new CardCore.EffectDefinition
            {
                Id = "VERIFY_P9_KW",
                TriggerTiming = TriggerTiming.OnPlay,
                Effects = new List<CardCore.AtomicEffectInstance> { new CardCore.AtomicEffectInstance { Type = kwType, Value = 1 } },
            };
            Assert(CardCore.EffectEligibility.RejectReason(Cardtype.Spell, kwDef) != null,
                   "P9-3 关键词挂法术：拒（法术无关键词载体）");
            Assert(CardCore.EffectEligibility.RejectReason(Cardtype.Creature, kwDef) == null
                   && CardCore.EffectEligibility.RejectReason(Cardtype.Enchantment, kwDef) == null,
                   "P9-3b 关键词挂生物/结界：可挂");

            // ---- 4. 时机系数（2026-11-10 时机系数规格 v0.1，段 2b 已切正：影子开关删除）：
            //      TimingCostFactorOf = TimingWeights 逐时机 f；缺行/表不可读才回落 TimingFactor 桶（安全侧）。----
            float F(TriggerTiming t) => CardCore.CostDerivationService.TimingCostFactorOf(t);
            {
                // 单源一致性：新表模式下 35 时机全部 = 行 Factor（缺行回落桶）；语义锚（校准不变式）：
                // OnPlay=1（法术锚点）/主动三档=1（现付）/OnTurnStart=0（系统专用禁用）。
                bool srcOk = true;
                foreach (TriggerTiming t in System.Enum.GetValues(typeof(TriggerTiming)))
                {
                    float want = CardCore.TimingWeights.TryGet(t, out var w) ? w.Factor : TimingBucketFactorOf(t);
                    if (System.Math.Abs(F(t) - want) > 1e-4f) { srcOk = false; break; }
                }
                Assert(srcOk,
                       "P9-4 新表模式单源：35 时机 TimingCostFactorOf = TimingWeights 行 Factor（缺行回落桶；λ 校准改表后断言零改动）");
                Assert(System.Math.Abs(F(TriggerTiming.OnPlay) - 1f) < 1e-4f
                       && System.Math.Abs(F(TriggerTiming.Activate_Active) - 1f) < 1e-4f
                       && System.Math.Abs(F(TriggerTiming.Activate_Instant) - 1f) < 1e-4f
                       && System.Math.Abs(F(TriggerTiming.Activate_Response) - 1f) < 1e-4f
                       && System.Math.Abs(F(TriggerTiming.OnTurnStart)) < 1e-4f,
                       "P9-4a 新表语义锚：OnPlay=1（法术锚）/主动三档恒 1（现付）/OnTurnStart=0（禁用）");
            }
            bool Once(TriggerTiming t) => !CardCore.CostDerivationService.IsRepeatTiming(t);
            Assert(Once(TriggerTiming.OnPlay) && Once(TriggerTiming.OnDeath) && Once(TriggerTiming.OnDestroy)
                   && Once(TriggerTiming.OnExile) && Once(TriggerTiming.OnLeaveBattlefield)
                   && Once(TriggerTiming.OnReturnFromGraveyard) && Once(TriggerTiming.OnGameStart)
                   && Once(TriggerTiming.OnRoleDeath)
                   && !Once(TriggerTiming.OnDraw) && !Once(TriggerTiming.OnTurnEnd) && !Once(TriggerTiming.OnStandbyPhase)
                   && !Once(TriggerTiming.OnDealDamage) && !Once(TriggerTiming.OnAttack) && !Once(TriggerTiming.OnTap)
                   && !Once(TriggerTiming.OnCardPlayed) && !Once(TriggerTiming.OnSummon),
                   "P9-4b 单次/多次归类（FiresOncePerPaidLifetime 唯一判定）：8 单次档；抽牌/回合末/准备/伤害/攻击/横置/使用卡牌/召唤进场归多次档");

            // ---- 5. 全链推导同证：红5 × 当前模式系数（影子=×1/×1.5；新表=×f）----
            float WantF(TriggerTiming t) => F(t); // 当前模式系数（P9-4 已钉单源）
            CardCore.EffectDefinition TimingDef(TriggerTiming t) => CardEffectConverter.ConvertOne(new CardEffectData
            {
                Id = "VERIFY_P9_T" + (int)t,
                TriggerTiming = (int)t,
                AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(CardCore.AtomicEffectType.DealDamage, value: 5) },
            }, "VERIFY_P9_T");
            float RedOf(CardCore.EffectDefinition d) => CardCore.CostDerivationService.DeriveElementCosts(d)[CardCore.ManaType.Red];
            Assert(System.Math.Abs(RedOf(TimingDef(TriggerTiming.OnPlay)) - 5f * WantF(TriggerTiming.OnPlay)) < 1e-4f
                   && System.Math.Abs(RedOf(TimingDef(TriggerTiming.OnDraw)) - 5f * WantF(TriggerTiming.OnDraw)) < 1e-4f
                   && System.Math.Abs(RedOf(TimingDef(TriggerTiming.OnDealDamage)) - 5f * WantF(TriggerTiming.OnDealDamage)) < 1e-4f,
                   $"P9-5 时机系数入价：红5×f（新表已切正——登场 {5f * WantF(TriggerTiming.OnPlay):0.###} / 抽牌 {5f * WantF(TriggerTiming.OnDraw):0.###} / 造成伤害 {5f * WantF(TriggerTiming.OnDealDamage):0.###}）");
            Assert(System.Math.Abs(RedOf(TimingDef(TriggerTiming.OnAttack)) - 5f * WantF(TriggerTiming.OnAttack)) < 1e-4f
                   && System.Math.Abs(RedOf(TimingDef(TriggerTiming.OnTap)) - 5f * WantF(TriggerTiming.OnTap)) < 1e-4f,
                   $"P9-5b 生物专属攻/守/横置入价：红5×f（攻击宣言 {5f * WantF(TriggerTiming.OnAttack):0.###} / 横置 {5f * WantF(TriggerTiming.OnTap):0.###}）");

            // ---- 6. 历史次数档 limit=2/3/-1 无运行时限流（同回合全部入栈）----
            CardCore.EffectDefinition RuntimeDef(int limit) => CardEffectConverter.ConvertOne(new CardEffectData
            {
                Id = $"VERIFY_P9_R{limit}",
                TriggerTiming = (int)TriggerTiming.OnDraw,
                TriggerLimitPerTurn = limit,
                AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(CardCore.AtomicEffectType.DrawCard, value: 1) },
            }, "VERIFY_P9_R");
            var src = SpawnCapSource(core, p1);
            var queue = core.StackEngine;
            foreach (var lim in new[] { 2, 3, -1 })
            {
                var def = RuntimeDef(lim);
                GameActions.DrainStack(core);
                queue.AddPendingEffect(MakeAutoTrigger(def, src, p1));
                queue.AddPendingEffect(MakeAutoTrigger(def, src, p1));
                core.StackEngine.ProcessTriggeredEffects();
                Assert(core.StackEngine.StackSize == 2,
                       $"P9-6 旧档 limit={lim} 无运行时限流——同回合 2 次触发全部入栈");
                GameActions.DrainStack(core);
            }
            RetireCards(core, p1, src);

            // ---- 7. 内容审计：教学/预组 + 训练全表——生物不挂周期/观察族；结界不挂攻/守/横置族 ----
            AssertHostTimingAudit();

            // ---- 8. 时机系数权重表对账 + 影子价差清单（2026-11-10 时机系数规格 §5/§7，只报不改）----
            TestP9TimingWeightTable();

            // ---- 9. ValueSystemConfig 表值=默认值清单（2026-11-10 结构化迁移，只报不改）----
            TestP9ConfigDefaultAudit();
        }

        /// <summary>内容审计（2026-11-10 资格层对齐时机系数新口径；旧「生物+周期族」全量拒口径作废）：
        /// 教学/预组+训练全表——①生物+多次档且 E[N]&gt;1 且非双宿主（OnAttacked）→ Error（E[N]≤1 低频放行）；
        /// ②结界+攻/守/横置族（4 值）→ Error；③OnTurnStart 任何宿主 → Error（系统专用）。
        /// 引擎载荷仅豁免生物臂与 OnTurnStart（效果头时机=奖励发射窗，raw 值经归一化）；结界+攻守横置
        /// 即便带引擎载荷也不放行。与装载期 CardLoader.ValidateCreatureShape / ValidateEnchantShape 同口径，
        /// 命中点名。附 35 行「时机×生物/结界可挂」矩阵打印（CreatureCanTakeTiming 单源——验收用）。</summary>
        private static void AssertHostTimingAudit()
        {
            var hits = new List<string>();
            void Scan(string cardsPath, string fxPath, string label)
            {
                if (!File.Exists(cardsPath) || !File.Exists(fxPath)) return;
                var fxRoot = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(fxPath));
                var timing = new Dictionary<string, int>();  // effectId → timing（缺省 0=OnPlay）
                var engine = new HashSet<string>();          // 含引擎载荷（branch.settle==3）的效果 id
                foreach (var e in (fxRoot["items"] as Newtonsoft.Json.Linq.JArray
                         ?? new Newtonsoft.Json.Linq.JArray()).OfType<Newtonsoft.Json.Linq.JObject>())
                {
                    string eid = (string)e["id"];
                    timing[eid] = (int?)e["timing"] ?? 0;
                    foreach (var p in e.Descendants().OfType<Newtonsoft.Json.Linq.JProperty>())
                        if (p.Name == "settle" && (int)p.Value == (int)CardCore.BranchSettleKind.Engine)
                        {
                            engine.Add(eid);
                            break;
                        }
                }
                var cardsRoot = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(cardsPath));
                foreach (var c in (cardsRoot["cards"] as Newtonsoft.Json.Linq.JArray)
                         ?? new Newtonsoft.Json.Linq.JArray())
                {
                    string supertype = (string)c["supertype"];
                    bool creature = supertype == "Creature", enchant = supertype == "Enchantment";
                    if (!creature && !enchant) continue;
                    foreach (var idTok in (c["effectIds"] as Newtonsoft.Json.Linq.JArray)
                             ?? new Newtonsoft.Json.Linq.JArray())
                    {
                        string id = (string)idTok;
                        if (!timing.TryGetValue(id, out int t)) continue; // 关键词行（原子表 id）不在效果表
                        if (t == (int)TriggerTiming.Activate_Active || t == (int)TriggerTiming.Activate_Instant
                            || t == (int)TriggerTiming.Activate_Response)
                            continue;
                        // ③ OnTurnStart 任何宿主（引擎载荷豁免——raw 值经 NormalizeEngineTiming 归一）
                        if (t == (int)TriggerTiming.OnTurnStart && !engine.Contains(id))
                        {
                            hits.Add($"{label} {(string)c["cardName"]}({(string)c["id"]}) 效果 {id} timing=OnTurnStart"
                                     + "——系统专用时点（任何宿主禁挂，装载期 Error）");
                            continue;
                        }
                        // ① 生物 + 多次档 E[N]>1 且非双宿主（CreatureCanTakeTiming 单源——引擎豁免）
                        if (creature && !CardCore.EffectEligibility.CreatureCanTakeTiming((TriggerTiming)t)
                            && !engine.Contains(id))
                            hits.Add($"{label} {(string)c["cardName"]}({(string)c["id"]}) 效果 {id} timing={(TriggerTiming)t}"
                                     + $"（E[N]={CardCore.TimingWeights.ExpectedOf((TriggerTiming)t):0.##}）"
                                     + "——生物+期望触发>1 次的周期时机（结界专属）");
                        // ② 结界 + 攻/守/横置族 4 值（引擎载荷也不放行）
                        if (enchant && CardCore.EffectEligibility.IsTapAnchoredTiming((TriggerTiming)t))
                            hits.Add($"{label} {(string)c["cardName"]}({(string)c["id"]}) 效果 {id} timing={(TriggerTiming)t}"
                                     + "——结界+攻/守/横置族（生物专属 4 值）");
                    }
                }
            }
            Scan(SynergyUI.CardDataPaths.TutorialFileIn("TutorialCards.json"),
                 SynergyUI.CardDataPaths.TutorialFileIn("TutorialEffects.json"), "教学");
            Scan(SynergyUI.CardDataPaths.TrainingFileIn("TrainingCards.json"),
                 SynergyUI.CardDataPaths.TrainingFileIn("TrainingEffects.json"), "训练");
            foreach (var h in hits) Debug.LogError($"[P9-7 内容审计] 宿主+时机不合规：{h}");
            Assert(hits.Count == 0,
                   $"P9-7 内容审计（2026-11-10 新口径）：教学/训练全表无「生物+E[N]>1 周期时机」、"
                   + $"无「结界+攻/守/横置族 4 值」、无「任何宿主+OnTurnStart」（命中 {hits.Count}"
                   + "——生物改挂单次/攻守横置/双宿主/低频时机或改宿主为结界；结界改挂单次/周期观察/双宿主时机）");

            // ---- 35 行「时机×宿主」可挂矩阵（CreatureCanTakeTiming / 结界=!攻守横置&&!OnTurnStart 单源） ----
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[P9-7b 时机×宿主矩阵]（生物=CreatureCanTakeTiming：OnTurnStart✗ 单次✓ 攻守横置4值✓ "
                          + "双宿主(OnAttacked)✓ 其余按 E[N]≤1；结界=非攻守横置且非OnTurnStart；E[N] 缺行回落 1.0）");
            foreach (TriggerTiming t in System.Enum.GetValues(typeof(TriggerTiming)))
            {
                bool cr = CardCore.EffectEligibility.CreatureCanTakeTiming(t);
                bool en = !CardCore.EffectEligibility.IsTapAnchoredTiming(t) && t != TriggerTiming.OnTurnStart;
                sb.AppendLine($"  {t,-26} 生物 {(cr ? "✓" : "✗")}   结界 {(en ? "✓" : "✗")}   E[N]={CardCore.TimingWeights.ExpectedOf(t):0.###}");
            }
            Debug.Log(sb.ToString());
        }

        // ======================================== P9-10：ValueSystemConfig 表值=默认值清单（2026-11-10 KV→section 结构化迁移，只报不改） ========================================

        /// <summary>P9-10（只报不改）：ValueSystemConfig.json 逐字段与代码默认值比对，列出「表值 == 默认值」
        /// 的字段——这类字段无论表写没写、写没写对，运行值都一样，疑似漏填/拼错（人眼不可分辨配置是否生效）。
        /// 表未覆盖的字段不在清单（本就吃代码默认，如 CardCost.StatAnchor 等 10 个）。
        /// 直接读表解析比对（不经装载缓存，编辑器改表重跑即取新读数）；顶层裸数组旧格式同样支持
        /// （回滚期跑验证器不空转）。未知 section/Key 不在此重复点名——装载期已 Error。</summary>
        private static void TestP9ConfigDefaultAudit()
        {
            const string relPath = "Configs/ValueSystemConfig.json";
            string raw = CardCore.TidePaths.ReadConfigText(relPath);
            if (string.IsNullOrEmpty(raw))
            {
                Debug.LogWarning($"[P9-10] {relPath} 不可读，跳过表值=默认值审计");
                return;
            }
            string trimmed = raw.TrimStart();
            Newtonsoft.Json.Linq.JObject root;
            try
            {
                if (trimmed.StartsWith("["))
                {
                    // 旧 KV 数组（回滚兼容）：按 Category 分组转 section（与装载器 LegacyKvToSections 同构）
                    root = new Newtonsoft.Json.Linq.JObject();
                    foreach (var item in (Newtonsoft.Json.Linq.JArray.Parse(raw)
                             .OfType<Newtonsoft.Json.Linq.JObject>()))
                    {
                        string category = (string)item["Category"];
                        string key = (string)item["Key"];
                        if (string.IsNullOrEmpty(category) || string.IsNullOrEmpty(key)) continue;
                        var sec = root[category] as Newtonsoft.Json.Linq.JObject;
                        if (sec == null) { sec = new Newtonsoft.Json.Linq.JObject(); root[category] = sec; }
                        sec[key] = item["Value"];
                    }
                }
                else
                {
                    root = Newtonsoft.Json.Linq.JObject.Parse(raw);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[P9-10] {relPath} 解析失败: {e.Message}，跳过表值=默认值审计");
                return;
            }

            var fresh = new CardCore.ValueSystemRuntimeConfig(); // 字段初始化器 = 代码默认值
            var hits = new List<string>();
            int fields = 0;
            foreach (var sec in root.Properties())
            {
                var secField = typeof(CardCore.ValueSystemRuntimeConfig).GetField(sec.Name + "Config");
                if (secField == null || !(sec.Value is Newtonsoft.Json.Linq.JObject secObj)) continue;
                object defaults = secField.GetValue(fresh);
                foreach (var p in secObj.Properties())
                {
                    var f = secField.FieldType.GetField(p.Name);
                    if (f == null) continue;
                    fields++;
                    object def = f.GetValue(defaults);
                    object val;
                    try { val = ((Newtonsoft.Json.Linq.JToken)p.Value).ToObject(f.FieldType); }
                    catch { continue; } // 类型不符：装载期 PopulateObject 已 Error 点名，此处不重复
                    if (Equals(val, def)) hits.Add($"{sec.Name}.{p.Name} = {val}（{val.GetType().Name}）");
                }
            }
            Debug.Log($"[P9-10 表值=默认值清单]（只报不改——疑似漏填/拼错后回落默认；比对 {fields} 字段，命中 {hits.Count}）：\n"
                      + (hits.Count > 0 ? string.Join("\n", hits) : "（无——所有表值均偏离代码默认值）"));
        }

        // ======================================== P9-9：时机系数权重表对账 + 价差清单（2026-11-10 时机系数规格 v0.1 §5，只报不改） ========================================

        /// <summary>桶默认系数（TimingFactorConfig 单源镜像——新表缺行兜底值；
        /// 与 CostDerivation.TimingCostFactorOf 的回落链同口径）。</summary>
        private static float TimingBucketFactorOf(TriggerTiming t)
        {
            var f = CardCore.ValueSystemConfigManager.Instance.GetOrCreateConfig().TimingFactorConfig;
            if (CardCore.EffectEligibility.IsTapAnchoredTiming(t)) return f.CreatureOnly;
            return CardCore.CostDerivationService.FiresOncePerPaidLifetime(t) ? f.Once : f.Repeat;
        }

        /// <summary>旧口径时机系数（规格 §7 影子侧）：非触发/引擎载荷恒 1，其余走桶（×1/×1/×1.5）。</summary>
        private static float TimingOldFactorOf(CardCore.EffectDefinition def)
        {
            if (def == null || !def.IsTriggeredEffect
                || CardCore.EffectEligibility.HasEnginePayload(def)) return 1f;
            return TimingBucketFactorOf(def.TriggerTiming);
        }

        /// <summary>时机系数（TimingWeights 行 Factor，缺行回落桶；非触发/引擎载荷恒 1）——
        /// 与 TimingCostFactorOf 同口径（段 2b 已切正：影子开关删除）。</summary>
        private static float TimingNewFactorOf(CardCore.EffectDefinition def)
        {
            if (def == null || !def.IsTriggeredEffect
                || CardCore.EffectEligibility.HasEnginePayload(def)) return 1f;
            return CardCore.TimingWeights.TryGet(def.TriggerTiming, out var w) ? w.Factor
                : TimingBucketFactorOf(def.TriggerTiming);
        }

        /// <summary>P9-9（2026-11-10 时机系数规格 v0.1 §5/§7）：TimingWeights.json 对账 + 影子价差清单
        /// （只报不改——规格 §7 落地开关：切换前必看本段输出，段 2b 切换 = Enabled:true 与
        /// ValueSystemConfig TimingFactor.CreatureOnly 1.5→1.0 **同批**）：
        /// ①表可读非空 35 行；②Timing 名与 TriggerTiming 枚举双射（无未知/重复/缺行）；
        /// ③Bucket 与单源分类器一致（Once↔FiresOncePerPaidLifetime、TapAnchored↔IsTapAnchoredTiming、
        ///   Active=Activate_* 三值、Disabled=OnTurnStart、其余=Repeat——Bucket 只用于归类与宿主校验不参与计算）；
        /// ④公式自洽（行内）：Factor=min(Expected,Cutoff)（Active 恒 1=现付不进时序、Disabled 恒 0）；
        ///   Repeat 的 Expected=λ×L(4)、TapAnchored 的 Expected=λ×Lc(3) 重算一致（Once 的 p 无重算源只验钳顶）；
        /// ⑤35 行 f 表全量打印（含 Enabled/Cutoff/Base/share/每满 N 次触发扣整份读数；Cutoff≠Base 只警不 Fail——§9 风险1）；
        /// ⑥影子价差清单（只报不改）：效果库全量效果锚价 旧系数 vs 新 f——基准价=时机中性 shim
        ///  （同数据 timing=Activate_Active 推导，f 为全 def 一致乘数）×(f新−f旧)；命中数/价差合计
        ///   + 教学·训练两池按卡 rollup。λ 校准（§6）后重跑即得新对账——断言只锁结构不锁数值。</summary>
        private static void TestP9TimingWeightTable()
        {
            CardCore.TimingWeights.Invalidate(); // 改表后重跑验证器免域重载即取新读数
            var rows = CardCore.TimingWeights.Rows;

            // ---- ①② 双射 ----
            Assert(rows != null && rows.Count == 35,
                   $"P9-9a 权重表可读：35 行非空（实读 {rows?.Count ?? 0}——Assets/Configs/TimingWeights.json）");
            var unknown = new List<string>();
            var seen = new HashSet<string>();
            var dup = new List<string>();
            foreach (var r in rows)
            {
                if (r == null || string.IsNullOrEmpty(r.Timing)) { unknown.Add("<空行>"); continue; }
                if (!System.Enum.TryParse(r.Timing, out TriggerTiming _)) unknown.Add(r.Timing);
                if (!seen.Add(r.Timing)) dup.Add(r.Timing);
            }
            var missing = System.Enum.GetValues(typeof(TriggerTiming)).Cast<TriggerTiming>()
                .Where(t => !seen.Contains(t.ToString())).Select(t => t.ToString()).ToList();
            foreach (var h in unknown) Debug.LogError($"[P9-9b 未知时机名] {h}（须为 TriggerTiming 枚举名）");
            foreach (var h in dup) Debug.LogError($"[P9-9b 重复行] {h}（首行优先，余行被忽略）");
            foreach (var h in missing) Debug.LogError($"[P9-9b 缺行] {h}（计价回落桶默认）");
            Assert(unknown.Count == 0 && dup.Count == 0 && missing.Count == 0,
                   $"P9-9b Timing 名与枚举双射：未知 {unknown.Count} / 重复 {dup.Count} / 缺行 {missing.Count}");

            // ---- ③ Bucket 与单源分类器一致 ----
            var bucketHits = new List<string>();
            foreach (var r in rows)
            {
                if (r == null || string.IsNullOrEmpty(r.Timing)
                    || !System.Enum.TryParse(r.Timing, out TriggerTiming t)) continue;
                string want = CardCore.EffectEligibility.IsTapAnchoredTiming(t) ? "TapAnchored"
                    : CardCore.EffectEligibility.IsUniversalRepeatTiming(t) ? "Universal"
                    : CardCore.CostDerivationService.FiresOncePerPaidLifetime(t) ? "Once"
                    : t == TriggerTiming.OnTurnStart ? "Disabled"
                    : t == TriggerTiming.Activate_Active || t == TriggerTiming.Activate_Instant
                      || t == TriggerTiming.Activate_Response ? "Active"
                    : "Repeat";
                if (r.Bucket != want) bucketHits.Add($"{r.Timing}: 表={r.Bucket} 单源={want}");
            }
            foreach (var h in bucketHits) Debug.LogError($"[P9-9c Bucket 漂移] {h}");
            Assert(bucketHits.Count == 0,
                   $"P9-9c Bucket 与单源分类器一致（命中漂移 {bucketHits.Count}——Bucket 只用于归类与宿主校验，不参与计算）");

            // ---- ④ 公式自洽 ----
            float cutoff = CardCore.TimingWeights.Cutoff;
            var formulaHits = new List<string>();
            foreach (var r in rows)
            {
                if (r == null || string.IsNullOrEmpty(r.Timing)) continue;
                string tag = r.Timing;
                if (r.Bucket == "Active")
                {
                    if (System.Math.Abs(r.Factor - 1f) > 1e-4f) formulaHits.Add($"{tag}: Active Factor={r.Factor} ≠ 1（现付不进时序）");
                    continue;
                }
                if (r.Bucket == "Disabled")
                {
                    if (System.Math.Abs(r.Factor) > 1e-4f) formulaHits.Add($"{tag}: Disabled Factor={r.Factor} ≠ 0（系统专用禁用）");
                    continue;
                }
                if (System.Math.Abs(r.Factor - System.Math.Min(r.Expected, cutoff)) > 0.01f)
                    formulaHits.Add($"{tag}: Factor={r.Factor:0.###} ≠ min(Expected={r.Expected:0.###}, Cutoff={cutoff:0.###})");
                if (r.Bucket == "Repeat")
                {
                    float e = r.Lambda * CardCore.TimingWeights.EnchantLifetimeTurns;
                    if (System.Math.Abs(r.Expected - e) > 0.01f) formulaHits.Add($"{tag}: Repeat Expected={r.Expected:0.###} ≠ λ×L={e:0.###}");
                }
                else if (r.Bucket == "TapAnchored" || r.Bucket == "Universal")
                {
                    // Universal（OnAttacked，2026-11-10 解绑双宿主）沿用 λ×Lc 口径——存量 E=3=λ1×3
                    float e = r.Lambda * CardCore.TimingWeights.CreatureLifetimeTurns;
                    if (System.Math.Abs(r.Expected - e) > 0.01f) formulaHits.Add($"{tag}: {r.Bucket} Expected={r.Expected:0.###} ≠ λ×Lc={e:0.###}");
                }
            }
            foreach (var h in formulaHits) Debug.LogError($"[P9-9d 公式漂移] {h}");
            Assert(formulaHits.Count == 0,
                   $"P9-9d 公式自洽：f=min(E[N],Cutoff)、Repeat E=λ×L、TapAnchored E=λ×Lc、Active=1、Disabled=0（漂移 {formulaHits.Count}）");
            if (System.Math.Abs(CardCore.TimingWeights.Base - cutoff) > 0.01f)
                Debug.LogWarning($"[P9-9] Cutoff={cutoff:0.###} ≠ Base={CardCore.TimingWeights.Base:0.###}——规格 §9 风险 1："
                                 + "Cutoff 是次数、Base 是耐久点，两者解耦须有明确理由（耐久只兑现 min 次的口径会被破坏）");

            // ---- ⑤ 35 行 f 表全量打印 ----
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[P9-9e f 表] TimingWeights.json：已切正（段 2b·2026-11-10，影子开关已删：计价直接乘行 f）"
                          + $" Cutoff={cutoff:0.###} Base={CardCore.TimingWeights.Base:0.###}"
                          + $" share={CardCore.TimingWeights.DurabilitySharePerTrigger:0.######}"
                          + $" 每满 {CardCore.TimingWeights.TriggersPerDeduction} 次触发扣整份耐久 D（D 恒兑现 3 次）");
            foreach (var r in rows)
                sb.AppendLine($"  {r.Timing,-26} {r.Bucket,-12} λ={r.Lambda,6:0.###} E[N]={r.Expected,7:0.###} f={r.Factor,6:0.###}  {r.Note}");
            Debug.Log(sb.ToString());

            // ---- ⑥ 影子价差清单（只报不改）：效果库全量 旧系数 vs 新 f ----
            var fxDelta = new Dictionary<string, (string name, float fOld, float fNew, float baseCost, float delta)>();
            foreach (var it in CardCore.EffectsLibrary.GetAll())
            {
                var data = CardCore.EffectsLibrary.Resolve(it.id);
                if (data == null) continue;
                var def = CardEffectConverter.ConvertOne(data, it.id);
                if (def == null) continue;
                float fOld = TimingOldFactorOf(def), fNew = TimingNewFactorOf(def);
                if (System.Math.Abs(fNew - fOld) < 1e-4f) continue; // 系数不变（非触发/引擎载荷/桶值恰同）不进清单
                // 基准价：时机中性 shim（同数据 timing=Activate_Active——f 是全 def 一致乘数，可分离）
                var nd = CardCore.TideJson.FromJson<CardEffectData>(CardCore.TideJson.ToJson(data));
                nd.TriggerTiming = (int)TriggerTiming.Activate_Active;
                float baseCost = CardCore.CostDerivationService.DeriveElementCosts(CardEffectConverter.ConvertOne(nd, it.id)).Total;
                if (baseCost < 0.01f) continue; // 解析后零原子/零价（悬空引用剔空等）——系数无作用面
                fxDelta[it.id] = (it.name, fOld, fNew, baseCost, baseCost * (fNew - fOld));
            }
            float fxSum = fxDelta.Values.Sum(v => v.delta);
            var sbFx = new System.Text.StringBuilder();
            sbFx.AppendLine($"[P9-9f 影子价差·效果清单]（只报不改）受影响效果 {fxDelta.Count} 条、效果锚价 Δ 合计 {fxSum:+0.##;-0.##;0}：");
            foreach (var kv in fxDelta.OrderByDescending(kv => System.Math.Abs(kv.Value.delta)))
                sbFx.AppendLine($"  {kv.Key} {kv.Value.name}: f {kv.Value.fOld:0.###}→{kv.Value.fNew:0.###}"
                                + $" 基准 {kv.Value.baseCost:0.##} Δ {kv.Value.delta:+0.##;-0.##;0}");
            Debug.Log(sbFx.ToString());

            // ---- ⑥b 按卡 rollup（教学·训练两池——受影响卡数/Δ 合计/逐卡清单）----
            void Rollup(string cardsPath, string label)
            {
                if (!File.Exists(cardsPath)) return;
                var root = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(cardsPath));
                var sbC = new System.Text.StringBuilder();
                int cardsHit = 0;
                float sum = 0f;
                foreach (var c in (root["cards"] as Newtonsoft.Json.Linq.JArray)
                         ?? new Newtonsoft.Json.Linq.JArray())
                {
                    string idTok = (string)c["id"];
                    float cardDelta = 0f;
                    foreach (var e in (c["effectIds"] as Newtonsoft.Json.Linq.JArray)
                             ?? new Newtonsoft.Json.Linq.JArray())
                        if (fxDelta.TryGetValue((string)e, out var v)) cardDelta += v.delta;
                    if (System.Math.Abs(cardDelta) < 0.01f) continue;
                    cardsHit++;
                    sum += cardDelta;
                    sbC.AppendLine($"  {label} {(string)c["cardName"]}({idTok}): 效果锚价 Δ {cardDelta:+0.##;-0.##;0}");
                }
                Debug.Log($"[P9-9g 影子价差·{label}卡]（只报不改）受影响卡 {cardsHit} 张、Δ 合计 {sum:+0.##;-0.##;0}：\n{sbC}");
            }
            Rollup(SynergyUI.CardDataPaths.TutorialFileIn("TutorialCards.json"), "教学");
            Rollup(SynergyUI.CardDataPaths.TrainingFileIn("TrainingCards.json"), "训练");

            Debug.Log($"[P9-9h 切换检查单] 段 2b 切换 = TimingWeights.json Enabled:false→true **同批**"
                      + " ValueSystemConfig TimingFactor.CreatureOnly 1.5→1.0（缺行兜底不加价）"
                      + $"——受影响效果 {fxDelta.Count} 条、效果锚价 Δ 合计 {fxSum:+0.##;-0.##;0}"
                      + "（耐久消耗口径随 Enabled 同批切 D/3 累计）。先看本段清单与卡费曲线再切（规格 §7）；"
                      + "λ 实测校准（§6）后重跑本段即得新对账。");
        }

        // ======================================== P9-8：原子档位双轨对账（2026-11-10 定案；同日改版：d 值档名 + AtomColorRules 生成色） ========================================

        /// <summary>P9-8 断言（2026-11-10 双轨定案·段1；同日改版）：原子表 Tier 写法（d 值档名 →
        /// 解析期展开单色 ManaList = AtomColorRules.For 生成色 × PriceOf 档位价，ValueSystemConfig.AtomTier
        /// 段公式 Base+Slope×d）与直写 ManaList 双轨共存的对账口：
        /// ①互斥——Tier 与 ManaList 同行=双源冲突（读原始 JSON 复核：装载后 ManaList 已是展开值）；
        /// ②档位名必须是 AtomTier 字段（d 值名 Dm1_5/Dm1_25/Dm1/Dm0_5/D0/Dp1/Dp1_5/Dp3）；
        /// ③色可生成——Tier 行经 AtomColorRules.For 须产出单色（未收录/豁免/多色=报——此类行直写 ManaList）；
        /// ④展开正确性——装载行 ManaList 与 生成色×PriceOf 价差&gt;0.01 报出；
        /// ⑤八档价快照 0/0.5/1/2/3/5/6/9（Base/Slope 有意调价时同步本断言）；
        /// ⑥价格档读数——直写行总价 ∉ 八档价的清单（迁移期候补名单，只读数不断言）；
        /// ⑦全表色对账——生成色 vs 显式色不等=Fail（与 RunColorAudit 同源同口径；**零容忍硬断言**：
        /// 2026-11-10 收尾白名单 SpecColorPending 已随 B/C 组色迁移清零删除——候补必须为 0）。
        /// A+B 组 52 行 Tier 化后：①-④ 零命中、⑦ 0 不符 / 0 候补。</summary>
        private static void TestP9AtomTierDualTrack()
        {
            var tierCfg = CardCore.ValueSystemConfigManager.Instance.GetOrCreateConfig().AtomTierConfig;
            string[] tierNames = { "Dm1_5", "Dm1_25", "Dm1", "Dm0_5", "D0", "Dp1", "Dp1_5", "Dp3" };

            // ---- ①②③ 原始 JSON 复核（五分文件——装载层看不见冲突面）----
            var mutexHits = new List<string>();
            var unknownHits = new List<string>();
            var colorHits = new List<string>();
            var rawPrices = new HashSet<float>(); // 直写行总价集合（⑥读数用）
            foreach (var file in CardCore.Attribute.AtomicEffectTable.TableFiles)
            {
                string raw;
                try { raw = CardCore.TidePaths.ReadConfigText(file); } catch (System.Exception) { raw = null; }
                if (string.IsNullOrEmpty(raw)) continue;
                if (!(Newtonsoft.Json.Linq.JToken.Parse(raw) is Newtonsoft.Json.Linq.JArray arr)) continue;
                foreach (var row in arr.OfType<Newtonsoft.Json.Linq.JObject>())
                {
                    string tier = (string)row["Tier"];
                    var mana = row["ManaList"] as Newtonsoft.Json.Linq.JArray;
                    bool manaWritten = mana != null && mana.Count > 0;
                    string name = (string)row["EnumName"] ?? "?";
                    string id = (string)row["ID"] ?? "?";
                    if (!string.IsNullOrEmpty(tier) && manaWritten)
                        mutexHits.Add($"{name}({id})");
                    else if (manaWritten)
                        rawPrices.Add(mana.Sum(t => (float?)t ?? 0f)); // lambda 产 float → Sum 返回非空 float（外层勿再 ??）
                    if (!string.IsNullOrEmpty(tier) && !tierCfg.TryGetD(tier, out _))
                        unknownHits.Add($"{name}({id}) Tier='{tier}'");
                    // ③Tier 行色可生成：EffectType+短名 → For 须单色（颜色不走表列——AtomColorRules 单源）
                    if (!string.IsNullOrEmpty(tier)
                        && System.Enum.TryParse<CardCore.AtomicEffectType>((string)row["EffectType"], out var t))
                    {
                        var spec = CardCore.Attribute.AtomColorRules.For(t, name);
                        if (!CardCore.Attribute.AtomColorRules.TryGetSingleColor(spec, out _))
                            colorHits.Add($"{name}({id}) 生成色={CardCore.Attribute.AtomColorRules.ColorZh(spec)}"
                                          + "——未收录/豁免/多色，应直写 ManaList");
                    }
                }
            }
            foreach (var h in mutexHits) Debug.LogError($"[P9-8 双源冲突] Tier 与 ManaList 同行（清空其一）：{h}");
            Assert(mutexHits.Count == 0,
                   $"P9-8a Tier/ManaList 互斥：双源同行 0 行（命中 {mutexHits.Count}——双轨定案不许静默取一）");
            foreach (var h in unknownHits) Debug.LogError($"[P9-8 未知档位] {h}");
            Assert(unknownHits.Count == 0,
                   $"P9-8b 档位名合法：未知档 0 行（命中 {unknownHits.Count}——AtomTier 表无同名字段，档名=d 值名）");
            foreach (var h in colorHits) Debug.LogError($"[P9-8 色不可生成] {h}");
            Assert(colorHits.Count == 0,
                   $"P9-8c 色可生成：Tier 行经 AtomColorRules.For 均为单色（命中 {colorHits.Count}——未收录/豁免/多色行走直写 ManaList）");

            // ---- ④ 展开正确性：Tier 行装载 ManaList = 生成色 × PriceOf（价差>0.01 报出）----
            // 注：零价档（Dm1_5=0）展开后 IsZero、PrimaryColor 回落 Gray——色比较只对非零价行做。
            var driftHits = new List<string>();
            foreach (var cfg in CardCore.Attribute.AtomicEffectTable.GetAll())
            {
                if (cfg == null || string.IsNullOrEmpty(cfg.Tier)) continue;
                float price = tierCfg.PriceOf(cfg.Tier);
                float loaded = cfg.ManaList?.Total ?? 0f;
                bool colorOk = price <= 0f
                               || cfg.ManaList == null // 未知档/色不可生成行已 Error 不计价，不算漂移
                               || cfg.ManaList.PrimaryColor.ToString() == cfg.Color;
                if (System.Math.Abs(price - loaded) > 0.01f || !colorOk)
                    driftHits.Add($"{cfg.DisplayName}({cfg.HashId}) Tier={cfg.Tier} 期望={price} 实载={loaded} 色={cfg.Color}");
            }
            foreach (var h in driftHits) Debug.LogError($"[P9-8 展开漂移] {h}");
            Assert(driftHits.Count == 0,
                   $"P9-8d 展开正确性：Tier 行装载价 = 生成色×PriceOf（价差>0.01 命中 {driftHits.Count}）");

            // ---- ⑤ 八档价快照（Base/Slope 有意调价时同步本断言——防误录乱档）----
            float[] expected = { 0f, 0.5f, 1f, 2f, 3f, 5f, 6f, 9f };
            var got = tierNames.Select(t => tierCfg.PriceOf(t)).OrderBy(v => v).ToArray();
            bool pricesOk = got.Length == expected.Length
                            && !got.Where((v, i) => System.Math.Abs(v - expected[i]) > 0.01f).Any();
            Assert(pricesOk,
                   $"P9-8e 八档价快照：{string.Join("/", tierNames.Select(t => $"{t}={tierCfg.PriceOf(t):0.##}"))}"
                   + $" —— 期望 0/0.5/1/2/3/5/6/9（Base+Slope×d；调价改 Base/Slope 时同步本断言）");

            // ---- ⑥ 价格档读数（只读不断言）：直写行总价 ∉ 八档价 —— 迁移期「价差≠0」候补名单 ----
            var offTier = rawPrices.Where(p => !got.Any(g => System.Math.Abs(g - p) <= 0.01f)).OrderBy(p => p).ToList();
            Debug.Log($"[P9-8f 价格档读数] 直写 ManaList 行总价共 {rawPrices.Count} 档；不在八档价（0/0.5/1/2/3/5/6/9）"
                      + $" 的 {offTier.Count} 档：{string.Join("/", offTier.Select(p => p.ToString("0.##")))}——迁 Tier 前先归档或扩档");

            // ---- ⑦ 全表色对账（生成值 vs 显式值，与 RunColorAudit 同源同口径；零容忍硬断言——
            //      2026-11-10 收尾：白名单 SpecColorPending 已随 B/C 组色迁移清零删除，候补必须为 0） ----
            var colorFail = new List<string>();
            foreach (var cfg in CardCore.Attribute.AtomicEffectTable.GetAll())
            {
                if (cfg == null || cfg.ManaList == null || cfg.ManaList.IsZero) continue;
                if (!System.Enum.TryParse<CardCore.AtomicEffectType>(cfg.EnumName, out var t)) continue;
                if (CardCore.Attribute.AtomColorRules.SpecColorExempt.Contains(t)) continue;
                var want = CardCore.Attribute.AtomColorRules.For(t, cfg.DisplayName);
                var cur = CardCore.Attribute.AtomColorRules.FlagsOf(cfg.ManaList);
                if (want == CardCore.Attribute.SpecColor.None)
                    colorFail.Add($"{cfg.DisplayName} 现值{CardCore.Attribute.AtomColorRules.ColorZh(cur)}/未收录——SpecColorOf 缺行请补");
                else if (cur != want)
                    colorFail.Add($"{cfg.DisplayName} 现值{CardCore.Attribute.AtomColorRules.ColorZh(cur)} 应值{CardCore.Attribute.AtomColorRules.ColorZh(want)}");
            }
            foreach (var h in colorFail) Debug.LogError($"[P9-8g 色差] {h}");
            Assert(colorFail.Count == 0,
                   $"P9-8g 全表色对账：生成色=显式色 0 不符 / 0 候补（命中 {colorFail.Count}——AtomColorRules 单源，与 RunColorAudit 同口径；白名单已随色迁移收尾删除）");
        }

    }
}
