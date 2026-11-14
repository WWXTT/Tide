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

        // ======================================== 牺牲原子（2026-11-14 域=候选声明改版） ========================================

        /// <summary>
        /// 牺牲原子端到端（2026-11-14 域=候选声明改版）：①表行锚=0费 Pol=0、域=己方战场:生物/
        /// 己方战场:结界/己方元素池:卡（TargetKind 1/3/13，无 filter）——行域即牺牲候选；
        /// ②候选域=己方三区卡（对手单位不入候选）；③行为：目标卡**规则送墓**（非效果死亡：
        /// 不发 CardDestroyEvent，不灭无从拦），每张按归属者 +1 临时彩元素；地牌先出池再入墓。
        /// 数量档由解析层选定，handler 逐目标送墓（不再自建弹窗/自收集候选）。
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

            // ---- 1. 表行锚（2026-11-14 域=候选声明：0费 Pol=0 己方战场生物/结界+己方元素池地牌） ----
            var cfg = CardCore.Attribute.AtomicEffectTable.GetByType(AtomicEffectType.Sacrifice);
            CardCore.TargetAxis sacAxis = null;
            bool sacAxisOk = cfg != null && CardCore.TargetAxisRules.TryParseDomain(cfg.Domain, out sacAxis);
            Assert(cfg != null && cfg.TotalUnitCost == 0f && cfg.Polarity == 0f
                   && cfg.GetTargetKindList().SequenceEqual(new[] { 1, 3, 13 })
                   && (cfg.TargetFilter ?? "") == ""
                   && sacAxisOk && sacAxis != null
                   && sacAxis.Zh.Contains("己方战场:生物")
                   && sacAxis.Zh.Contains("己方战场:结界")
                   && sacAxis.Zh.Contains("己方元素池:卡")
                   && ElementAffinities.GetAffinityForEffect(AtomicEffectType.Sacrifice).IsGeneric,
                   $"牺牲表行：0费 Pol=0 域=己方战场:生物/结界 + 己方元素池:卡（2026-11-14 域=候选声明；" +
                   $"kinds={string.Join("/", cfg.GetTargetKindList())}；axis={sacAxis?.Zh ?? "<null>"}）");

            // ---- 2. 候选域：表行域即候选（己方生物/结界/池内地牌；对手单位不入）----
            var ctx = new EffectExecutionContext
            {
                Source = p1,
                Controller = p1,
                ZoneManager = core.ZoneManager,
                ElementPool = core.ElementPool,
            };

            // ---- 3. 行为：目标卡规则送墓（非死亡；不灭无从拦）——每张按归属者 +1 临时彩元素 ----
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
            Card SpawnEnchant(Player owner)
            {
                var data = new CardData
                {
                    ID = "VERIFY_SAC_E" + (++seq), CardName = "SACE" + seq,
                    Supertype = Cardtype.Enchantment, Power = 0, Life = 0,
                };
                var card = new CardWrapper(data);
                card.SetController(owner);
                Assert(core.ZoneManager.TryAddToBattlefield(card, owner), $"牺牲段合成结界入场（{data.CardName}）");
                return card;
            }

            // 候选：己方生物 + 己方结界 + 己方池内地牌；对手单位不入（行域=己方三区）
            var mine = Spawn(p1, 2, 2);
            var mineEnchant = SpawnEnchant(p1);
            var foeUnit = Spawn(p2, 3, 3, Attribute.KeywordRules.Indestructible); // 不灭也不入对手候选
            var foeEnchant = SpawnEnchant(p2);
            var landData = new CardData
            {
                // 2/2 身材推导绿 1——锁色候选非空才可入池（0/1 零费卡 GetLandColorChoices 空被拒）；
                // 2026-11-06 opt-in 补攻守：无资格则免费属性底吞掉 2/2 身材、推导 0 费入池被拒
                ID = "VERIFY_SAC_LAND", CardName = "牺牲之地", Supertype = Cardtype.Creature,
                AttackAbility = true, GuardAbility = true, Power = 2, Life = 2,
            };
            var land = InjectCard(core, p1, landData);
            Assert(GameActions.AddToElementPool(core, p1, land), "牺牲段：地牌入池");
            var rowCandidates = CardCore.Attribute.EffectHandlerRegistry.ResolveCandidates(
                cfg.GetTargetKindList(), null, ctx);
            Assert(rowCandidates.Contains(mine) && rowCandidates.Contains(mineEnchant) && rowCandidates.Contains(land)
                   && !rowCandidates.Contains(foeUnit) && !rowCandidates.Contains(foeEnchant),
                   "牺牲候选域：己方生物/结界/池内地牌入候选，对手单位不入（行域=己方三区）");

            // 行为：目标卡规则送墓（非死亡；不灭无从拦）——每张按归属者 +1 临时彩元素
            var atom = new CardCore.AtomicEffectInstance { Type = AtomicEffectType.Sacrifice, Value = 1 };
            var execCtx = new EffectExecutionContext
            {
                Source = p1,
                Controller = p1,
                ZoneManager = core.ZoneManager,
                ElementPool = core.ElementPool,
                Targets = new List<Entity> { mine, mineEnchant, land },
            };
            int sacDestroyEvents = 0;
            void OnSacDestroy(CardDestroyEvent e) => sacDestroyEvents++;
            EventManager.Instance.Subscribe<CardDestroyEvent>(OnSacDestroy);
            int anyP1 = core.ElementPool.GetAvailableManaCount(ManaType.Any, p1);
            Assert(CardCore.Attribute.EffectHandlerRegistry.ExecuteEffect(atom, execCtx),
                   "牺牲原子执行（目标=己方生物/结界/池内地牌）");
            EventManager.Instance.Unsubscribe<CardDestroyEvent>(OnSacDestroy);

            Assert(!mine.IsAlive && core.ZoneManager.IsCardInZone(mine, p1, Zone.Graveyard)
                   && !mineEnchant.IsAlive && core.ZoneManager.IsCardInZone(mineEnchant, p1, Zone.Graveyard)
                   && core.ZoneManager.IsCardInZone(land, p1, Zone.Graveyard)
                   && !core.ZoneManager.GetCards(p1, Zone.ElementPool).Contains(land)
                   && !core.ElementPool.GetPool(p1).PooledCards.Any(pc => pc.SourceCard == land),
                   "牺牲：目标卡全部规则送墓（生物/结界/地牌先出池——非死亡路径）");
            Assert(sacDestroyEvents == 0, "牺牲非死亡：全批不发 CardDestroyEvent（死亡时点/消灭替代/击杀统计不触发）");
            Assert(core.ElementPool.GetAvailableManaCount(ManaType.Any, p1) == anyP1 + 3,
                   $"牺牲入账：每送墓一张 +1 临时彩元素（3 张 = +3；实际 {core.ElementPool.GetAvailableManaCount(ManaType.Any, p1) - anyP1}）");

            // ---- 3b. 不灭无从拦（非死亡路径）：己方不灭单位照样规则送墓 ----
            var indestructible = Spawn(p1, 3, 3, Attribute.KeywordRules.Indestructible);
            var sacCtx2 = new EffectExecutionContext
            {
                Source = p1,
                Controller = p1,
                ZoneManager = core.ZoneManager,
                ElementPool = core.ElementPool,
                Targets = new List<Entity> { indestructible },
            };
            int anyP1b = core.ElementPool.GetAvailableManaCount(ManaType.Any, p1);
            Assert(CardCore.Attribute.EffectHandlerRegistry.ExecuteEffect(atom, sacCtx2), "牺牲原子执行（目标=己方不灭单位）");
            Assert(!indestructible.IsAlive && core.ZoneManager.IsCardInZone(indestructible, p1, Zone.Graveyard),
                   "牺牲：不灭无从拦——规则送墓非死亡，不灭/复生不拦");
            Assert(core.ElementPool.GetAvailableManaCount(ManaType.Any, p1) == anyP1b + 1,
                   "牺牲不灭入账：送墓同计 +1 临时彩元素");

            // ---- 3c. 多目标：数量档由解析层选定，handler 逐目标送墓（Value 不再驱动自选）----
            var u1 = Spawn(p1, 1, 1);
            var u2 = Spawn(p1, 1, 1);
            var multiCtx = new EffectExecutionContext
            {
                Source = p1,
                Controller = p1,
                ZoneManager = core.ZoneManager,
                ElementPool = core.ElementPool,
                Targets = new List<Entity> { u1, u2 },
            };
            int anyP1c = core.ElementPool.GetAvailableManaCount(ManaType.Any, p1);
            Assert(CardCore.Attribute.EffectHandlerRegistry.ExecuteEffect(
                       new CardCore.AtomicEffectInstance { Type = AtomicEffectType.Sacrifice, Value = 2 }, multiCtx),
                   "牺牲原子多目标执行（value=2·目标 2 个单位）");
            Assert(!u1.IsAlive && !u2.IsAlive
                   && core.ZoneManager.IsCardInZone(u1, p1, Zone.Graveyard)
                   && core.ZoneManager.IsCardInZone(u2, p1, Zone.Graveyard),
                   "牺牲多目标：逐目标全部送墓（落点由解析层选定，handler 不再自收集候选）");
            Assert(core.ElementPool.GetAvailableManaCount(ManaType.Any, p1) == anyP1c + 2,
                   "牺牲多目标入账：2 个单位 = +2 临时彩元素");

            // 段界卫生：对手侧探针卡退场（不参与后续段）
            RetireCards(core, p2, foeUnit, foeEnchant);
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
        /// 第 31 次截断（只防自循环，常规触发远低于此）；④自由分支引擎奖励 2026-11-13 起也取消每回合上限
        /// （频率由结界耐久/生物挂载价付费，见 TestBranchEngines 元素充盈段）。
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

            // 历史档不再溢价：同一 OnDraw 伤害 5 在 limit 1/2/3/-1 下同价
            //（2026-11-14 档位制：OnDraw=一回合多次，f=1（次数由结界耐久支付）；且 2026-11-13 起 f 不进锚价——
            //  锚价=原价红5。旧「f=1.2^3.8≈2.0 → 红10」/「两档 ×1 → 红5」的口径均作废，同价性断言不变）
            CardCore.EffectDefinition TrigDef(int limit) => CardEffectConverter.ConvertOne(new CardEffectData
            {
                Id = $"VERIFY_CAP_P{limit}",
                TriggerTiming = (int)TriggerTiming.OnDraw,
                TriggerLimitPerTurn = limit,
                AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(CardCore.AtomicEffectType.DealDamage, value: 5) },
            }, "VERIFY_CAP_P");
            float RedCostOf(CardCore.EffectDefinition d) => CardCore.CostDerivationService.DeriveElementCosts(d)[CardCore.ManaType.Red];
            Assert(System.Math.Abs(RedCostOf(TrigDef(1)) - 5f) < 1e-2f
                   && System.Math.Abs(RedCostOf(TrigDef(2)) - 5f) < 1e-2f
                   && System.Math.Abs(RedCostOf(TrigDef(3)) - 5f) < 1e-2f
                   && System.Math.Abs(RedCostOf(TrigDef(-1)) - 5f) < 1e-2f,
                   "历史次数档零消费：limit 1/2/3/-1 同价（OnDraw 一回合多次 f=1；锚价=原价红5）");

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

        // ======================================== P9：时机档位（一回合一次/稀有/一回合多次）+ 效果适用卡型（2026-11-14 档位制） ========================================

        /// <summary>P9 断言（**2026-11-14 档位制**——取代 2026-11-05 单次/多次两档）：
        /// ①一回合多次（OnDraw 等）挂生物→拒、结界→过、法术→拒（含合成器候选谓词）；
        /// ②生物动作族（攻/守/横置/重置 4 值）挂生物→过（一回合一次 ×1）、挂结界→拒（不横置）；
        /// ③引擎载荷只有结界可挂（生物拒）；④双宿主 OnAttacked 生物/结界都过（一回合一次 ×1）；
        /// ⑤稀有档（被破坏/被除外/占卜…）生物/结界都过（按概率计价）；
        /// ⑥OnTurnStart 自本批起属一回合一次档：生物/结界可挂、法术拒；
        /// ⑦时机系数=档位制（一回合一次/主动/一回合多次恒 1，稀有=表 Factor 概率）；
        /// ⑧全链推导同证（效果锚价=原价；生物挂载：一回合一次×1、稀有×概率）；
        /// ⑨历史次数档 limit=2/3/-1 运行时不再拦；⑩内容审计（档位口径）+35 行时机×宿主矩阵。</summary>
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
            var multiDef = Def(TriggerTiming.OnDraw, CardCore.AtomicEffectType.DrawCard);      // 一回合多次档（抽牌时）
            var onceDef = Def(TriggerTiming.OnPlay, CardCore.AtomicEffectType.DealDamage);     // 一回合一次档（登场）
            var tapDef = Def(TriggerTiming.OnAttack, CardCore.AtomicEffectType.DrawCard);      // 生物动作族（一回合一次）
            var rareDef = Def(TriggerTiming.OnDestroy, CardCore.AtomicEffectType.DealDamage);  // 稀有档（被破坏）
            var engineDef = new CardCore.EffectDefinition                                      // 引擎载荷（时机=奖励发射窗）
            {
                Id = "VERIFY_P9_ENG",
                TriggerTiming = TriggerTiming.OnStandbyPhase,
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

            // ---- 1. 一回合多次档：结界专属（生物拒/结界过/法术拒）----
            Assert(CardCore.EffectEligibility.RejectReason(Cardtype.Creature, multiDef) != null,
                   "P9-1 一回合多次时机（抽牌）挂生物：拒（耐久即次数池，结界专属）");
            Assert(CardCore.EffectEligibility.RejectReason(Cardtype.Enchantment, multiDef) == null,
                   "P9-1b 一回合多次时机挂结界：过（每次触发耐久 −1，耐久归零销毁）");
            Assert(CardCore.EffectEligibility.RejectReason(Cardtype.Spell, multiDef) != null,
                   "P9-1c 一回合多次时机挂法术：拒（法术不驻场）");
            var repeatGraph = new SynergyUI.EffectGraphData("P9一回合多次")
            {
                header = new CardEffectData
                {
                    Id = "VERIFY_P9_REPEAT",
                    TriggerTiming = (int)TriggerTiming.OnDraw,
                    AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(CardCore.AtomicEffectType.DrawCard, value: 1) },
                },
            };
            Assert(SynergyUI.CardComposerScreen.HostRejectReason(Cardtype.Creature, repeatGraph) != null,
                   "P9-1d 合成器候选分区谓词：一回合多次图不进生物候选（原因可读）");
            // 生物动作族（攻击/守卫/横置/重置）：一回合一次档，只有生物可挂
            Assert(CardCore.EffectEligibility.RejectReason(Cardtype.Creature, tapDef) == null,
                   "P9-1e 攻击（生物动作族）挂生物：过（一回合一次 ×1，横置限频）");
            Assert(CardCore.EffectEligibility.RejectReason(Cardtype.Enchantment, tapDef) != null,
                   "P9-1e2 攻击（生物动作族）挂结界：拒（结界不横置、不宣言攻击）");
            Assert(CardCore.EffectEligibility.HostCanTakeTiming(Cardtype.Creature, TriggerTiming.OnAttack)
                   && !CardCore.EffectEligibility.HostCanTakeTiming(Cardtype.Enchantment, TriggerTiming.OnAttack)
                   && !CardCore.EffectEligibility.HostCanTakeTiming(Cardtype.Spell, TriggerTiming.OnAttack),
                   "P9-1e3 合成器时机下拉宿主过滤：生物动作族仅生物可见");
            // 引擎载荷（2026-11-13 收口）：引擎=无上限触发，生物没有耐久对价 → 生物拒，只有结界可挂
            var engineReject = CardCore.EffectEligibility.RejectReason(Cardtype.Creature, engineDef);
            Assert(engineReject != null,
                   $"P9-1f 引擎载荷挂生物：拒（引擎只能挂结界——实际 {engineReject ?? "过"}）");
            Assert(CardCore.EffectEligibility.RejectReason(Cardtype.Enchantment, engineDef) == null,
                   "P9-1f2 引擎载荷挂结界：过（耐久买次数）");
            // ---- 1b. 档位制：双宿主 / 稀有档 / 一回合一次高频锚点 / OnTurnStart 解禁 ----
            var uniDef = Def(TriggerTiming.OnAttacked, CardCore.AtomicEffectType.DrawCard); // 双宿主（被攻击）
            Assert(CardCore.EffectEligibility.RejectReason(Cardtype.Creature, uniDef) == null
                   && CardCore.EffectEligibility.RejectReason(Cardtype.Enchantment, uniDef) == null,
                   "P9-1g 双宿主时机 OnAttacked：生物/结界都过（一回合一次 ×1，无溢价）");
            Assert(CardCore.EffectEligibility.HostCanTakeTiming(Cardtype.Creature, TriggerTiming.OnAttacked)
                   && CardCore.EffectEligibility.HostCanTakeTiming(Cardtype.Enchantment, TriggerTiming.OnAttacked),
                   "P9-1g2 合成器下拉：OnAttacked 生物/结界双宿主可见");
            Assert(CardCore.EffectEligibility.RejectReason(Cardtype.Creature, rareDef) == null
                   && CardCore.EffectEligibility.RejectReason(Cardtype.Enchantment, rareDef) == null
                   && CardCore.EffectEligibility.RejectReason(Cardtype.Creature,
                       Def(TriggerTiming.OnScry, CardCore.AtomicEffectType.DrawCard)) == null,
                   $"P9-1h 稀有档（被破坏×{CardCore.CostDerivationService.TimingCostFactorOf(TriggerTiming.OnDestroy):0.##}"
                   + $" / 占卜×{CardCore.CostDerivationService.TimingCostFactorOf(TriggerTiming.OnScry):0.##}）：生物/结界都过（按概率计价）");
            Assert(CardCore.EffectEligibility.RejectReason(Cardtype.Creature,
                       Def(TriggerTiming.OnStandbyPhase, CardCore.AtomicEffectType.DrawCard)) == null,
                   "P9-1i 一回合一次档（OnStandbyPhase）挂生物：过（旧 E[N]>1 全拒口径作废——档位制按次数不按 E[N]）");
            var turnStartDef = Def(TriggerTiming.OnTurnStart, CardCore.AtomicEffectType.DrawCard);
            Assert(CardCore.EffectEligibility.RejectReason(Cardtype.Creature, turnStartDef) == null
                   && CardCore.EffectEligibility.RejectReason(Cardtype.Enchantment, turnStartDef) == null
                   && CardCore.EffectEligibility.RejectReason(Cardtype.Spell, turnStartDef) != null
                   && CardCore.EffectEligibility.HostCanTakeTiming(Cardtype.Creature, TriggerTiming.OnTurnStart)
                   && CardCore.EffectEligibility.HostCanTakeTiming(Cardtype.Enchantment, TriggerTiming.OnTurnStart),
                   "P9-1j OnTurnStart 一回合一次：生物/结界可挂、法术拒（2026-11-14 解禁——与准备阶段并列）");

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

            // ---- 4. 时机系数（2026-11-14 档位制）：一回合一次/主动/一回合多次恒 1；
            //      稀有档=表 Factor（触发概率）。缺行/表不可读回落 TimingFactor.Once（安全侧）。----
            float F(TriggerTiming t) => CardCore.CostDerivationService.TimingCostFactorOf(t);
            {
                int checkedCount = 0;
                bool srcOk = true;
                foreach (TriggerTiming t in System.Enum.GetValues(typeof(TriggerTiming)))
                {
                    var tier = CardCore.TimingWeights.TierOf(t);
                    float want;
                    if (tier == CardCore.TimingTier.Rare)
                        want = CardCore.TimingWeights.TryGet(t, out var w) && w.Factor > 0f ? w.Factor : 1f;
                    else want = 1f; // Active / PerTurn / Multi
                    checkedCount++;
                    if (System.Math.Abs(F(t) - want) > 1e-4f)
                    {
                        Debug.LogError($"[P9-4 系数漂移] {t} 档位={tier} F={F(t):0.###} want={want:0.###}");
                        srcOk = false;
                        break;
                    }
                }
                Assert(srcOk && checkedCount == 35,
                       $"P9-4 档位制单源：{checkedCount} 时机——一回合一次/主动/一回合多次=1，稀有=表 Factor（概率）");
                Assert(System.Math.Abs(F(TriggerTiming.OnPlay) - 1f) < 1e-4f
                       && System.Math.Abs(F(TriggerTiming.Activate_Active) - 1f) < 1e-4f
                       && System.Math.Abs(F(TriggerTiming.Activate_Instant) - 1f) < 1e-4f
                       && System.Math.Abs(F(TriggerTiming.Activate_Response) - 1f) < 1e-4f
                       && System.Math.Abs(F(TriggerTiming.OnTurnStart) - 1f) < 1e-4f
                       && System.Math.Abs(F(TriggerTiming.OnAttack) - 1f) < 1e-4f
                       && System.Math.Abs(F(TriggerTiming.OnTurnEnd) - 1f) < 1e-4f,
                       "P9-4a 档位语义锚：登场/回合开始/攻击/回合结束=1（一回合一次）、主动三档恒 1、一回合多次恒 1");
                Assert(System.Math.Abs(F(TriggerTiming.OnDestroy) - 0.55f) < 1e-4f
                       && System.Math.Abs(F(TriggerTiming.OnExile) - 0.12f) < 1e-4f
                       && System.Math.Abs(F(TriggerTiming.OnDeath) - 0.6f) < 1e-4f,
                       "P9-4a2 稀有档保留概率系数：被破坏 0.55 / 被除外 0.12 / 死亡 0.6");
            }
            {
                int per = 0, rare = 0, multi = 0, active = 0;
                foreach (TriggerTiming t in System.Enum.GetValues(typeof(TriggerTiming)))
                    switch (CardCore.TimingWeights.TierOf(t))
                    {
                        case CardCore.TimingTier.PerTurn: per++; break;
                        case CardCore.TimingTier.Rare: rare++; break;
                        case CardCore.TimingTier.Multi: multi++; break;
                        default: active++; break;
                    }
                Assert(per == 11 && rare == 7 && multi == 14 && active == 3,
                       $"P9-4b 档位归类：一回合一次 {per}/11、稀有 {rare}/7、一回合多次 {multi}/14、主动 {active}/3");
                Assert(CardCore.TimingWeights.IsPerTurnTiming(TriggerTiming.OnPlay)
                       && CardCore.TimingWeights.IsPerTurnTiming(TriggerTiming.OnAttack)
                       && CardCore.TimingWeights.IsPerTurnTiming(TriggerTiming.OnBlockDeclare)
                       && CardCore.TimingWeights.IsPerTurnTiming(TriggerTiming.OnTurnStart)
                       && CardCore.TimingWeights.IsPerTurnTiming(TriggerTiming.OnStandbyPhase)
                       && CardCore.TimingWeights.IsPerTurnTiming(TriggerTiming.OnTurnEnd)
                       && CardCore.TimingWeights.IsPerTurnTiming(TriggerTiming.OnAttacked)
                       && CardCore.TimingWeights.IsPerTurnTiming(TriggerTiming.OnSummon)
                       && CardCore.TimingWeights.IsRareTiming(TriggerTiming.OnDeath)
                       && CardCore.TimingWeights.IsRareTiming(TriggerTiming.OnDestroy)
                       && CardCore.TimingWeights.IsRareTiming(TriggerTiming.OnExile)
                       && CardCore.TimingWeights.IsRareTiming(TriggerTiming.OnLeaveBattlefield)
                       && CardCore.TimingWeights.IsRareTiming(TriggerTiming.OnReturnFromGraveyard)
                       && CardCore.TimingWeights.IsRareTiming(TriggerTiming.OnRoleDeath)
                       && CardCore.TimingWeights.IsRareTiming(TriggerTiming.OnScry)
                       && CardCore.TimingWeights.IsMultiPerTurnTiming(TriggerTiming.OnDraw)
                       && CardCore.TimingWeights.IsMultiPerTurnTiming(TriggerTiming.OnDealDamage)
                       && CardCore.TimingWeights.IsMultiPerTurnTiming(TriggerTiming.OnTakeDamage)
                       && CardCore.TimingWeights.IsMultiPerTurnTiming(TriggerTiming.OnCardPlayed)
                       && CardCore.TimingWeights.IsMultiPerTurnTiming(TriggerTiming.OnCardCastSettled)
                       && CardCore.TimingWeights.IsMultiPerTurnTiming(TriggerTiming.OnAtomicEffectResolution)
                       && CardCore.TimingWeights.IsMultiPerTurnTiming(TriggerTiming.OnPhaseStart)
                       && CardCore.TimingWeights.IsMultiPerTurnTiming(TriggerTiming.OnSpellCast),
                       "P9-4c 档位成员：一回合一次/稀有/一回合多次的逐时机归类（含回合开始与攻击/守卫）");
            }

            // ---- 5. 全链推导同证（2026-11-13 计价改版后）：效果锚价=原价（不带 f）；f 只在
            //      卡层宿主挂载系数 HostMountFactor——生物×f、结界×1；档位制下一回合一次 f=1、
            //      稀有 f=概率、一回合多次 f=1（生成本不可挂）。----
            float WantF(TriggerTiming t) => F(t); // 当前档位系数（P9-4 已钉单源）
            CardCore.EffectDefinition TimingDef(TriggerTiming t) => CardEffectConverter.ConvertOne(new CardEffectData
            {
                Id = "VERIFY_P9_T" + (int)t,
                TriggerTiming = (int)t,
                AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(CardCore.AtomicEffectType.DealDamage, value: 5) },
            }, "VERIFY_P9_T");
            float RedOf(CardCore.EffectDefinition d) => CardCore.CostDerivationService.DeriveElementCosts(d)[CardCore.ManaType.Red];
            float MountOf(CardCore.EffectDefinition d, Cardtype host)
                => CardCore.CostDerivationService.HostMountFactor(d, host);
            Assert(System.Math.Abs(RedOf(TimingDef(TriggerTiming.OnPlay)) - 5f) < 1e-4f
                   && System.Math.Abs(RedOf(TimingDef(TriggerTiming.OnDraw)) - 5f) < 1e-4f
                   && System.Math.Abs(RedOf(TimingDef(TriggerTiming.OnDealDamage)) - 5f) < 1e-4f,
                   "P9-5 效果锚价=原价（红5，不乘 f——f 移出效果层）");
            Assert(System.Math.Abs(MountOf(TimingDef(TriggerTiming.OnPlay), Cardtype.Creature) - 1f) < 1e-4f
                   && System.Math.Abs(MountOf(TimingDef(TriggerTiming.OnDestroy), Cardtype.Creature) - WantF(TriggerTiming.OnDestroy)) < 1e-4f
                   && System.Math.Abs(MountOf(TimingDef(TriggerTiming.OnDraw), Cardtype.Enchantment) - 1f) < 1e-4f
                   && System.Math.Abs(MountOf(TimingDef(TriggerTiming.OnDestroy), Cardtype.Enchantment) - 1f) < 1e-4f,
                   $"P9-5a 宿主挂载：生物——一回合一次×1、稀有×概率（被破坏 {WantF(TriggerTiming.OnDestroy):0.###}）；结界×1");
            Assert(System.Math.Abs(MountOf(TimingDef(TriggerTiming.OnAttack), Cardtype.Creature) - 1f) < 1e-4f
                   && System.Math.Abs(MountOf(TimingDef(TriggerTiming.OnBlockDeclare), Cardtype.Creature) - 1f) < 1e-4f
                   && System.Math.Abs(MountOf(TimingDef(TriggerTiming.OnTap), Cardtype.Creature) - 1f) < 1e-4f
                   && System.Math.Abs(MountOf(TimingDef(TriggerTiming.OnUntap), Cardtype.Creature) - 1f) < 1e-4f,
                   "P9-5b 生物动作族（攻/守/横置/重置）全部 f=1（一回合一次档，2026-11-14）");

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

        /// <summary>内容审计（**2026-11-14 档位制**）：教学/预组+训练全表——
        /// ①生物 + 一回合多次（Multi）→ Error（结界专属，每次触发耐久 −1）；
        /// ②生物 + 引擎载荷 → Error（引擎无触发上限，只有结界能用耐久买次数——2026-11-13 收口）；
        /// ③结界 + 生物动作族（攻/守/横置/重置 4 值）→ Error。
        /// 一回合一次（含 OnTurnStart）与稀有两档对生物/结界全开（法术仅登场另拦）。
        /// 与装载期 CardLoader.ValidateCreatureShape / ValidateEnchantShape 同口径，
        /// 命中点名。附 35 行「时机×生物/结界可挂」矩阵打印（CreatureCanTakeTiming 单源——验收用）。</summary>
        private static void AssertHostTimingAudit()
        {
            var hits = new List<string>();
            int legacyEngineFixtures = 0; // 2026-11-13 引擎宿主收口遗留（教学/训练夹具；上限 22，见检查点注释）
            const int LegacyEngineFixtureCap = 22;
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
                        // ② 生物 + 引擎载荷（2026-11-13 收口：引擎无上限触发，只有结界有耐久对价）
                        //    2026-11-14：教学/训练池的引擎载体夹具是收口遗留债（教学内容待重做，见
                        //    docs/循环控制盘点_v0.1.md §11）——只告警不失败，并以 22 为上限防新增；
                        //    玩家池/新增夹具仍按硬违规点名。
                        if (creature && engine.Contains(id))
                        {
                            legacyEngineFixtures++;
                            Debug.LogWarning($"[P9-7 遗留夹具] {label} {(string)c["cardName"]}({(string)c["id"]}) 效果 {id}"
                                             + $" timing={(TriggerTiming)t}——生物+引擎载荷（2026-11-13 收口遗留，"
                                             + $"上限 {LegacyEngineFixtureCap}；教学内容待重做）");
                            continue;
                        }
                        // ① 生物 + 一回合多次（Multi——结界专属；CreatureCanTakeTiming 单源）
                        if (creature && !CardCore.EffectEligibility.CreatureCanTakeTiming((TriggerTiming)t))
                            hits.Add($"{label} {(string)c["cardName"]}({(string)c["id"]}) 效果 {id} timing={(TriggerTiming)t}"
                                     + $"（档位={CardCore.TimingWeights.TierOf((TriggerTiming)t)}"
                                     + $" 每回合≈{CardCore.TimingWeights.ExpectedOf((TriggerTiming)t):0.##} 次）"
                                     + "——生物+一回合多次（结界专属，每次触发耐久 −1）");
                        // ③ 结界 + 生物动作族 4 值
                        if (enchant && CardCore.EffectEligibility.IsTapAnchoredTiming((TriggerTiming)t))
                            hits.Add($"{label} {(string)c["cardName"]}({(string)c["id"]}) 效果 {id} timing={(TriggerTiming)t}"
                                     + "——结界+生物动作族（攻/守/横置/重置）");
                    }
                }
            }
            Scan(SynergyUI.CardDataPaths.TutorialFileIn("TutorialCards.json"),
                 SynergyUI.CardDataPaths.TutorialFileIn("TutorialEffects.json"), "教学");
            Scan(SynergyUI.CardDataPaths.TrainingFileIn("TrainingCards.json"),
                 SynergyUI.CardDataPaths.TrainingFileIn("TrainingEffects.json"), "训练");
            foreach (var h in hits) Debug.LogError($"[P9-7 内容审计] 宿主+时机不合规：{h}");
            Assert(hits.Count == 0 && legacyEngineFixtures <= LegacyEngineFixtureCap,
                   $"P9-7 内容审计（2026-11-14 档位制）：教学/训练全表无「生物+一回合多次时机」、"
                   + $"无「结界+生物动作族 4 值」；生物+引擎载荷=收口遗留夹具只告警不失败"
                   + $"（新违规 {hits.Count}；遗留 {legacyEngineFixtures}/{LegacyEngineFixtureCap}）"
                   + "——生物改挂一回合一次/稀有或改宿主为结界；结界改挂一回合一次/稀有/一回合多次）");

            // ---- 35 行「时机×宿主」可挂矩阵（CreatureCanTakeTiming / 结界=!生物动作族 单源） ----
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[P9-7b 时机×宿主矩阵]（档位制：生物=一回合一次/稀有 ✓、一回合多次 ✗；"
                          + "结界=非生物动作族；主动三档不经本闸；每回合≈=Expected 留档值）");
            foreach (TriggerTiming t in System.Enum.GetValues(typeof(TriggerTiming)))
            {
                bool cr = CardCore.EffectEligibility.CreatureCanTakeTiming(t);
                bool en = !CardCore.EffectEligibility.IsTapAnchoredTiming(t);
                sb.AppendLine($"  {t,-26} {CardCore.TimingWeights.TierOf(t),-8} 生物 {(cr ? "✓" : "✗")}   "
                              + $"结界 {(en ? "✓" : "✗")}   每回合≈{CardCore.TimingWeights.ExpectedOf(t):0.###}"
                              + $"   f={CardCore.CostDerivationService.TimingCostFactorOf(t):0.###}");
            }
            Debug.Log(sb.ToString());
        }

        // ======================================== P9-10：ValueSystemConfig 表值=默认值清单（2026-11-10 KV→section 结构化迁移，只报不改） ========================================

        /// <summary>P9-10（只报不改）：ValueSystemConfig.json 逐字段与代码默认值比对，列出「表值 == 默认值」
        /// 的字段——这类字段无论表写没写、写没写对，运行值都一样，疑似漏填/拼错（人眼不可分辨配置是否生效）。
        /// 表未覆盖的字段不在清单（本就吃代码默认，如 CardCost.StatAnchor 已于 2026-11-14 入表，其余未覆盖字段仍吃默认）。
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

        // ======================================== P9-9：时机档位表对账（2026-11-14 档位制，只报不改） ========================================

        /// <summary>P9-9（2026-11-14 档位制）：TimingWeights.json 对账——
        /// ①表可读非空 35 行；②Timing 名与 TriggerTiming 枚举双射（无未知/重复/缺行）；
        /// ③Bucket 与内置档位镜像一致（PerTurn/Rare/Multi/Active——Bucket 只用于归类与宿主校验）；
        /// ④档位×系数自洽（行内）：PerTurn/Multi/Active 的 Factor 恒 1；Rare 的 Factor=触发概率
        ///   （0&lt;p≤1）且 Expected 同值；PerTurn 的 Expected=1、Multi 的 Expected=λ；
        /// ⑤35 行档位×f 表全量打印（λ/Expected 只作每回合频率留档）。
        /// 结构断言只锁档位语义，不锁具体概率——稀有档改概率只改表 Factor。</summary>
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

            // ---- ③ Bucket 与内置档位镜像一致（表是单源，镜像兜底——两边必须同档）----
            var bucketHits = new List<string>();
            foreach (var r in rows)
            {
                if (r == null || string.IsNullOrEmpty(r.Timing)
                    || !System.Enum.TryParse(r.Timing, out TriggerTiming t)) continue;
                string want = CardCore.TimingWeights.FallbackTierOf(t).ToString();
                if (r.Bucket != want) bucketHits.Add($"{r.Timing}: 表={r.Bucket} 内置镜像={want}");
            }
            foreach (var h in bucketHits) Debug.LogError($"[P9-9c Bucket 漂移] {h}");
            Assert(bucketHits.Count == 0,
                   $"P9-9c Bucket 与内置档位镜像一致（命中漂移 {bucketHits.Count}——PerTurn/Rare/Multi/Active）");

            // ---- ④ 档位×系数自洽 ----
            var formulaHits = new List<string>();
            foreach (var r in rows)
            {
                if (r == null || string.IsNullOrEmpty(r.Timing)) continue;
                string tag = r.Timing;
                CardCore.TimingWeights.TryParseTier(r.Bucket, out var tier);
                switch (tier)
                {
                    case CardCore.TimingTier.Active:
                    case CardCore.TimingTier.PerTurn:
                    case CardCore.TimingTier.Multi:
                        if (System.Math.Abs(r.Factor - 1f) > 1e-4f)
                            formulaHits.Add($"{tag}: {tier} Factor={r.Factor:0.###} ≠ 1");
                        break;
                    case CardCore.TimingTier.Rare:
                        if (r.Factor <= 0f || r.Factor > 1f)
                            formulaHits.Add($"{tag}: Rare Factor={r.Factor:0.###} 须在 (0,1]（触发概率）");
                        if (System.Math.Abs(r.Factor - r.Expected) > 0.01f)
                            formulaHits.Add($"{tag}: Rare Factor={r.Factor:0.###} ≠ Expected(p)={r.Expected:0.###}");
                        break;
                }
                if (tier == CardCore.TimingTier.PerTurn && System.Math.Abs(r.Expected - 1f) > 0.01f)
                    formulaHits.Add($"{tag}: PerTurn Expected={r.Expected:0.###} ≠ 1（每回合至多一次）");
                if (tier == CardCore.TimingTier.Multi && System.Math.Abs(r.Expected - r.Lambda) > 0.01f)
                    formulaHits.Add($"{tag}: Multi Expected={r.Expected:0.###} ≠ λ={r.Lambda:0.###}");
            }
            foreach (var h in formulaHits) Debug.LogError($"[P9-9d 档位漂移] {h}");
            Assert(formulaHits.Count == 0,
                   $"P9-9d 档位×系数自洽：PerTurn/Multi/Active=1、Rare=概率、PerTurn E=1、Multi E=λ（漂移 {formulaHits.Count}）");

            // ---- ⑤ 35 行档位×f 表全量打印 ----
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[P9-9e 档位表] TimingWeights.json（2026-11-14 档位制：一回合一次/稀有/一回合多次/主动）"
                          + "——λ/Expected 只作每回合频率留档，计价读 Factor；耐久：结界每次触发 −1");
            foreach (var r in rows)
                sb.AppendLine($"  {r.Timing,-26} {r.Bucket,-8} λ={r.Lambda,6:0.###} 每回合≈{r.Expected,6:0.###} f={r.Factor,6:0.###}  {r.Note}");
            Debug.Log(sb.ToString());
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

        // ======================================== 牌库查重（2026-11-13 用户定案） ========================================

        /// <summary>牌库查重端到端（2026-11-13 用户定案：牌库不允许保存一模一样的卡）：
        /// ① 全库（主表+教学表+训练表）无功能重复卡——名称/标签/立绘不参与功能身份
        ///   （ContentHasher.SealCard 口径，含身材/耐久/提速/资格/关键词/效果）；同身材占位卡
        ///   （白板甲/乙/丙、先攻甲/乙…）已按 2026-11-13 定案并入同一张卡，场景/教程引用改 #N 实例。
        /// ② 三套预组内无「两个不同 id 但功能完全相同」的卡（同名多册合法；火苗/余烬 的回归闸）；
        /// ③ 保存闸单源 CardCatalog.FindFunctionalDuplicate（卡组合 OnSave 同一函数）：
        ///   改名克隆被判重、原卡自身保存放行、改过功能的新卡放行。</summary>
        private static void TestCardLibraryDedupe(List<CardData> catalogCards)
        {
            string SealOf(CardData c) => SynergyUI.ContentHasher.SealCard(c);

            // ① 全库功能重复=0（catalogCards=CardCatalog.LoadAll 正式池，已含教学+训练副表）
            var allDup = catalogCards
                .Where(c => c != null && !string.IsNullOrEmpty(c.ID))
                .GroupBy(SealOf)
                .Where(g => g.Count() > 1)
                .Select(g => string.Join("、", g.Select(c => $"{c.CardName}({c.ID})")))
                .ToList();
            Assert(allDup.Count == 0,
                   $"牌库查重①：全库（主+教学+训练）无功能重复卡（重复 {allDup.Count} 组"
                   + (allDup.Count > 0 ? "——" + string.Join("；", allDup) : "") + "）");

            // ② 预组内不同 id 同功能=0（同名多册按 id 去重后比封印）
            var deckHits = new List<string>();
            foreach (var deck in SynergyUI.DeckCatalog.LoadPresets())
            {
                var firstBySeal = new Dictionary<string, CardData>();
                foreach (var id in (deck.cardIds ?? new List<string>()).Distinct())
                {
                    var card = SynergyUI.CardCatalog.GetById(id);
                    if (card == null) continue;
                    string seal = SealOf(card);
                    if (firstBySeal.TryGetValue(seal, out var first) && first.ID != card.ID)
                        deckHits.Add($"{deck.name}：{first.CardName}({first.ID}) ≈ {card.CardName}({card.ID})");
                    else
                        firstBySeal[seal] = card;
                }
            }
            Assert(deckHits.Count == 0,
                   $"牌库查重②：三套预组内无功能重复卡（命中 {deckHits.Count}"
                   + (deckHits.Count > 0 ? "——" + string.Join("；", deckHits) : "") + "）");

            // 余烬（C_5BB0FCD6）与火苗功能完全相同，已按本规则并入火苗——卡表不得再有该条目
            Assert(SynergyUI.CardCatalog.GetById("C_5BB0FCD6") == null,
                   "牌库查重②：余烬（C_5BB0FCD6）已从卡表移除（与火苗功能重复，预组改用 10 张火苗）");

            // ③ 保存闸：卡表内功能唯一的卡做夹具（全库清重复后每张卡都唯一）
            var unique = catalogCards.FirstOrDefault(c => c != null && c.ID == "C_FFF2FD83"); // 预组·28 4/5
            Assert(unique != null, "牌库查重③ 夹具：卡表取到功能唯一的「预组·28」（C_FFF2FD83）");
            if (unique == null) return;

            var renamed = CloneForDedupe(unique, "预组·28·改名");
            string renamedId = "C_" + SynergyUI.ContentHasher.HashCard(renamed);
            var hit = SynergyUI.CardCatalog.FindFunctionalDuplicate(renamed, renamedId);
            Assert(hit != null && hit.ID == unique.ID,
                   $"牌库查重③：改名克隆被判为「{hit?.CardName}」({hit?.ID}) 的重复——保存闸拒绝"
                   + "（与卡组合 OnSave 同一函数）");

            Assert(SynergyUI.CardCatalog.FindFunctionalDuplicate(unique, unique.ID) == null,
                   "牌库查重③：原卡自身保存放行（savedId 豁免，不会把自己判成重复）");

            var buffed = CloneForDedupe(unique, "预组·28·强化");
            buffed.Power = (unique.Power ?? 0) + 1;
            Assert(SynergyUI.CardCatalog.FindFunctionalDuplicate(buffed, "C_VERIFY_DEDUPE_BUFFED") == null,
                   "牌库查重③：功能不同（攻击 +1）的新卡放行——查重只拦「一模一样」");
        }

        /// <summary>查重夹具克隆：只复制参与 SealCard 的功能字段（名称换成新名，模拟「只有名字不同」）。</summary>
        private static CardData CloneForDedupe(CardData src, string newName)
        {
            return new CardData
            {
                ID = null,
                CardName = newName,
                Supertype = src.Supertype,
                Power = src.Power,
                Life = src.Life,
                ArrowDirections = src.ArrowDirections,
                IsField = src.IsField,
                Keywords = src.Keywords != null ? new List<string>(src.Keywords) : new List<string>(),
                LinkAuras = src.LinkAuras != null ? new List<LinkAuraData>(src.LinkAuras) : null,
                Effects = src.Effects != null ? new List<CardEffectData>(src.Effects) : new List<CardEffectData>(),
                AttackAbility = src.AttackAbility,
                GuardAbility = src.GuardAbility,
                ChargeAbility = src.ChargeAbility,
            };
        }

    }
}
