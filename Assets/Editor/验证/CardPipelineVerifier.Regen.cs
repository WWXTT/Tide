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
        // ======================================== 测试卡表费用重生成 ========================================

        /// <summary>
        /// 重推导测试卡组费用 + 重写 ID：逐卡按统一计价换建议档位分布（声明价作废），
        /// 逐卡输出 S/K/E/f/D/Ĉ 明细供人工过目，回写 TestDecks 下每一套卡组 JSON（形状不变）。
        /// ID 重写为内容哈希（CardIdentityService.ContentId）：内容变 → ID 变；卡名/描述随便改
        /// 不动 ID（同内容跨文件同 ID——测试集导入训练池后天然对齐，无别名漂移）。
        /// 2026-09-11 追加：原子表 ID 列同批重推——手工改表
        /// （DisplayName 改动/增删行）后 ID 漂移，由工具统一收口（2026-10-30 起遍历原子表五分文件）。
        /// 2026-09-21：卡/效果/卡组属用户数据统一落盘（2026-10-18 热更迁移：编辑器改住
        /// Assets/CardData，经 CardDataPaths 收口，不再直连 StreamingAssets）。
        /// 2026-10-16：换号同步重映射——RegenAtomicTableIds 产出 旧ID→新ID 映射，RegenOneDeck
        /// 内重写 Effects.json/Cards.json 的 refId/effectIds 引用（此前换号不同步，悬空引用被
        /// 装载端静默剔原子）。
        /// 2026-10-20：教学副表只跟换原子 id（RemapTutorialAtomRefs）——不重推费用、不换卡/效果
        /// id（教学卡按剧本钉死，允许不符计价标准）；教学卡组文件只引卡 id，无需跟换。
        /// 2026-11-01：预组+教学纳入重推（RegenTutorialAndPresetTables，用户定案）——教学效果 id
        ///（HashEffect）/卡 id（C_+HashCard）/费用快照（EnsureCost）同批重推改写，卡组 json 与
        /// TutorialConfig 的卡 id 引用跟换；旧「只跟原子 id」保守口径作废。
        /// </summary>
        /// 2026-11-06 用户定案：入口恢复。原子行 ID 仍永久冻结——TextHash 文本漂移不再换行号，
        /// 改为登记进 `Configs/原子表ID别名.json`（新文本哈希 → 冻结行 ID），让按文本派生 ID 的
        /// 工具/存档仍能解析回本行；卡/效果 id 现状不变（卡 id 冻结只刷 hash，效果 id 随内容换号）。
        [MenuItem("Tools/重推导测试卡表费用与ID")]
        public static void RegenerateTestTableCosts()
        {
            // 2026-09-14 双表口径：唯一卡表经 CardDataPaths（2026-10-18 热更迁移后编辑器=Assets/CardData；
            // TestDecks 已随效果引用化退役）
            string path = SynergyUI.CardDataPaths.FileIn("Cards.json");
            if (!File.Exists(path))
            {
                Debug.LogError($"[Regen] 找不到卡表 {path}");
                return;
            }

            // 三层推导链（2026-09-16 定案，顺序敏感）：原子表 id（**2026-11-05 起冻结**，不再由文本派生）→
            // 效果 id（HashEffect：文本+参数+原子引用）→ 卡 id（HashCard：文本+参数+效果id序列）。
            // 原子表必须先行——效果/卡里的 refId 按 id 引用表行（GetByHashId），新行分配 id 后才会被引用
            var atomRenames = RegenAtomicTableIds();

            // 2026-11-01 门闸改版：正式表 2026-10-27 起恒空（主表清空回设计态）——RegenOneDeck 空表
            // 返回 false 不再阻断下游（旧口径下教学跟换同样从未真正执行过）。正式段与教学段各带
            // 悬空守卫互不连坐：正式悬空只中止正式重写，教学段照常（两表独立文件）。
            RegenOneDeck(path, atomRenames);
            RemapTutorialAtomRefs(atomRenames); // 教学副表原子 refId 跟换（重推前置——效果/卡 id 依赖原子行 id）
            RegenTutorialAndPresetTables();     // 2026-11-01 预组+教学纳入费用/ID 重推+引用跟换
            RegenTrainingTables(atomRenames);   // 2026-11-03 训练池分家：训练副表（TrainingData）同批重推
            RunColorAudit(); // 2026-11-02 颜色校验（README《效果颜色规范》定案）：只报不改——收尾统计
            RunDiscountExclusiveAudit(); // 2026-11-02 减费三选一校验：只报不改——收尾统计
        }

        // ======================================== 颜色校验（2026-11-02 README《效果颜色规范（2026-11-02 定案）》落地——只报不改） ========================================

        /// <summary>规范色位（六轴落四色：信息/节奏=蓝、资源/生存=绿、规则=灰、场面=红；
        /// README 第二步多成分拆色 = Flags 组合（吸取=伤害+回复=红|绿）。</summary>
        [System.Flags]
        private enum SpecColor { None = 0, Gray = 1, Red = 2, Blue = 4, Green = 8 }

        /// <summary>EffectType → 应得色，**硬编码**（README 落地条款：不新开配置表）：
        /// 三步走轴映射（改变场面数字=红 / 资源数量=绿 / 行动许可·信息=蓝 / 规则·属性定义=灰，
        /// 先命中先定色）+ 逐原子特例表优先 + 纯工具（衍生物/变形/复制等）=灰。
        /// **只作校验依据——不据此改任何原子 ManaList，改色由人决定。**
        /// 系统表不计价行（引擎主干/门槛/Attack 族，ManaList null 或全零）装载后不参与比对，
        /// 下列对应条目仅为行将来计价时预留的口径。</summary>
        private static readonly Dictionary<AtomicEffectType, SpecColor> SpecColorOf =
            new Dictionary<AtomicEffectType, SpecColor>
        {
            // ---- 场面（红）：改变场上数字（攻/血/存亡）----
            { AtomicEffectType.DealDamage, SpecColor.Red },
            { AtomicEffectType.LifeLoss, SpecColor.Red },                    // 流失=扣上限直伤（伤害族；表 2026-11-05 回红定案，11-03 绿口径作废）
            { AtomicEffectType.DrainLife, SpecColor.Red | SpecColor.Green }, // 特例：吸取=伤害(红)+回复(绿) 双色
            { AtomicEffectType.AddHeroAttack, SpecColor.Red },               // 特例：加攻=红
            { AtomicEffectType.Devour, SpecColor.Red },                      // 特例：消灭=红
            { AtomicEffectType.Smash, SpecColor.Red },                       // 摧毁=场面移除
            { AtomicEffectType.Sacrifice, SpecColor.Red },
            { AtomicEffectType.Exile, SpecColor.Red },                       // 特例：除外=红（从场上移除=场面）
            { AtomicEffectType.ReturnToHand, SpecColor.Red },                // 弹回=从场上移除（除外同口径；表 2026-11-05 回红定案，11-04 蓝口径作废）
            { AtomicEffectType.BounceToTop, SpecColor.Red },
            { AtomicEffectType.BounceToBottom, SpecColor.Red },
            { AtomicEffectType.GainControl, SpecColor.Red },
            { AtomicEffectType.ChangeOwner, SpecColor.Red },
            { AtomicEffectType.AddPowerUp, SpecColor.Red },                   // 改攻击力=红
            { AtomicEffectType.AddPowerDown, SpecColor.Red },
            { AtomicEffectType.AddLifeDown, SpecColor.Green },                // 定案 2026-11-02：生命值族全绿（用户表侧改定；旧「减血=伤害族红」作废）
            { AtomicEffectType.AddPermanentPowerUp, SpecColor.Red },          // 永久攻血四轴（2026-11-02 拆分定案，原双属性±两行退役）：攻=红
            { AtomicEffectType.AddPermanentPowerDown, SpecColor.Red },
            { AtomicEffectType.AddPermanentLifeUp, SpecColor.Green },         // 血=绿（生命值族同口径）
            { AtomicEffectType.AddPermanentLifeDown, SpecColor.Green },
            { AtomicEffectType.Purify, SpecColor.Red },                       // 驱散=全剥关键词/指示物（削场面）
            { AtomicEffectType.AddToxin, SpecColor.Red },                     // 毒素=回合末造伤（伤害族）
            { AtomicEffectType.GrantRetaliation, SpecColor.Red },             // 报复=反噬伤害
            { AtomicEffectType.GrantVenom, SpecColor.Red },                   // 特例：剧毒=红
            { AtomicEffectType.GrantPathogen, SpecColor.Red },                // 病原体=毒素族
            { AtomicEffectType.GrantOverwhelm, SpecColor.Red },               // 碾压=战斗场面
            { AtomicEffectType.GrantSplash, SpecColor.Red },                  // 溅射=战斗场面
            { AtomicEffectType.GrantFirstStrike, SpecColor.Red },             // 先攻=战斗顺序（场面）
            { AtomicEffectType.GrantDoubleStrike, SpecColor.Red },            // 连击=战斗场面
            { AtomicEffectType.GrantHunt, SpecColor.Red },                    // 狩猎=回合末攻击（场面）
            { AtomicEffectType.AddVulnerable, SpecColor.Red },                // 特例：易损=红
            { AtomicEffectType.BurstDamage, SpecColor.Red },
            { AtomicEffectType.ShieldSlam, SpecColor.Red },                   // 护盾猛击=护甲转伤害（伤害产出族）
            { AtomicEffectType.MoveUnit, SpecColor.Red },                     // 移动=场面（位置族）
            { AtomicEffectType.Attack, SpecColor.Red },                       // 系统行（预留）
            // ---- 资源/生存（绿）：手牌/牌库/墓地/元素池/生命总量/减伤/防护 ----
            { AtomicEffectType.Heal, SpecColor.Green },
            { AtomicEffectType.DrawCard, SpecColor.Green },                   // 特例：抽卡=绿
            { AtomicEffectType.DiscardCard, SpecColor.Green },                // 特例：弃牌=绿
            { AtomicEffectType.MillCard, SpecColor.Green },                   // 特例：磨牌=绿
            { AtomicEffectType.RecoverToHand, SpecColor.Green },              // 特例：回收=绿
            { AtomicEffectType.ShuffleIntoDeck, SpecColor.Green },            // 特例：洗回=绿
            { AtomicEffectType.RecoverFromExile, SpecColor.Green },           // 特例：除外回收=绿
            { AtomicEffectType.GainAnyElement, SpecColor.Green },             // 特例：元素合成=绿
            { AtomicEffectType.ReturnFromGraveyard, SpecColor.Green },        // 苏生=墓地资源回流
            { AtomicEffectType.IncreaseLandCap, SpecColor.Green },            // 成长=资源（地牌槽）
            { AtomicEffectType.RemoveDebuffs, SpecColor.Green },              // 净化=解除负面（2026-10-28 P2a 先例）
            { AtomicEffectType.AddLifeUp, SpecColor.Green },                  // 改生命/回复=绿
            { AtomicEffectType.AddArmor, SpecColor.Green },                   // 护甲=绿（属性与卡体）
            { AtomicEffectType.GrantToughness, SpecColor.Green },             // 坚韧=减伤=生存（减半同口径）
            { AtomicEffectType.GrantProtection, SpecColor.Gray },             // 特例：减半=伤害规则改写（2026-11-05 定案灰；旧绿/蓝作废）
            { AtomicEffectType.GrantReborn, SpecColor.Green },                // 复生=生存
            { AtomicEffectType.GrantDivineShield, SpecColor.Green },          // 圣盾=生存
            { AtomicEffectType.GrantGuardian, SpecColor.Green },              // 守护=拦截保护
            { AtomicEffectType.GrantIndestructible, SpecColor.Green },        // 不灭=生存
            { AtomicEffectType.GrantRegeneration, SpecColor.Green },          // 再生=生存
            { AtomicEffectType.LivingArmor, SpecColor.Green },                // 活体护甲=护甲族
            { AtomicEffectType.Guard, SpecColor.Green },                      // 系统行（预留）
            { AtomicEffectType.SetLife, SpecColor.Green },                    // 系统行（预留）
            // ---- 信息/节奏（蓝）：行动许可·能不能被指定·看到什么·反制打断 ----
            { AtomicEffectType.SearchDeck, SpecColor.Blue },                  // 特例：检索=蓝
            { AtomicEffectType.LookAtTopCards, SpecColor.Blue },              // 特例：占卜=蓝
            { AtomicEffectType.DiscoverCard, SpecColor.Blue },                // 发现=检索族（信息）
            { AtomicEffectType.RevealByDimension, SpecColor.Blue },           // 筛选展示=信息
            { AtomicEffectType.RevealCard, SpecColor.Blue },                  // 展示=信息
            { AtomicEffectType.DeclareHand, SpecColor.Blue },                 // 特例：宣言（强夺）=蓝
            { AtomicEffectType.DeclareDeck, SpecColor.Blue },                 // 特例：宣言（窃取）=蓝
            { AtomicEffectType.DeclareDeckTop, SpecColor.Blue },              // 特例：宣言/预言（禁言）=蓝
            { AtomicEffectType.ProphecyNextCard, SpecColor.Blue },            // 特例：预言=蓝（引擎行·预留）
            { AtomicEffectType.Untap, SpecColor.Blue },                       // 激励=解除横置（行动许可）
            { AtomicEffectType.LockCard, SpecColor.Blue },                    // 锁定=无法使用（行动许可）
            { AtomicEffectType.NegateActivation, SpecColor.Blue },            // 反制打断=节奏
            { AtomicEffectType.NegateEffect, SpecColor.Blue },
            { AtomicEffectType.TakeExtraTurn, SpecColor.Blue },               // 额外回合=节奏
            { AtomicEffectType.SkipTurn, SpecColor.Blue },                    // 跳过阶段/回合=节奏（四相位档同）
            { AtomicEffectType.GrantStealth, SpecColor.Blue },                // 特例：潜行=蓝
            { AtomicEffectType.GrantConcealed, SpecColor.Blue },              // 隐密=潜行持续版
            { AtomicEffectType.Silence, SpecColor.Blue },
            { AtomicEffectType.AddNullify, SpecColor.Blue },                  // 特例：失效=蓝
            { AtomicEffectType.Freeze, SpecColor.Blue },                      // 特例：冻结=蓝
            { AtomicEffectType.RushSickness, SpecColor.Blue },                // 特例：紊乱=蓝
            { AtomicEffectType.GrantDepravity, SpecColor.Green },             // 放血=反疗封锁=生存轴对侧（2026-11-05 定案绿；旧蓝作废）
            { AtomicEffectType.GrantReplay, SpecColor.Blue },                 // 重放=节奏（重复施放）
            { AtomicEffectType.Sleep, SpecColor.Blue },                       // 沉睡=行动封锁
            { AtomicEffectType.GrantSpellShield, SpecColor.Blue },            // 法术护盾=反制
            { AtomicEffectType.GrantVigilance, SpecColor.Blue },              // 警戒=攻击不横置（行动许可）
            { AtomicEffectType.GrantDisarm, SpecColor.Blue },                 // 缴械=压制反击（行动许可）
            { AtomicEffectType.GrantTaunt, SpecColor.Blue },                  // 帷幕=指定限制（能否被指定）
            { AtomicEffectType.AddDurabilityUp, SpecColor.Blue },             // 特例：耐久=蓝（2026-11-03 定案：结界耐久轴同口径——身材耐久桶同日灰→蓝，0.5蓝/点）
            { AtomicEffectType.SealUnit, SpecColor.Red },                     // 封印=场面移除（2026-11-05 确认定案红；11-03 蓝3 口径作废）
            // ---- 场面/生存双成分（红|绿）----
            { AtomicEffectType.GrantLifesteal, SpecColor.Red },                  // 吸血=红（2026-11-03 用户定案·不随吸取双色同构）
            { AtomicEffectType.GrantLifelink, SpecColor.Red | SpecColor.Green },   // 系命=伤害+回复 双色
            // ---- 规则/工具（灰）：规则改写·属性定义·纯工具 ----
            { AtomicEffectType.ModifyGameRule, SpecColor.Gray },              // 特例：各仪典/各禁令=灰（行级细化见 SpecRowOverrides）
            { AtomicEffectType.Erase, SpecColor.Gray },                       // 特例：通用抹除=灰
            { AtomicEffectType.GrantSpellban, SpecColor.Gray },               // 特例：无实体=灰（伤害规则改写；表 2026-11-05 确认回灰，11-03 蓝5 作废）
            { AtomicEffectType.GrantInsulator, SpecColor.Gray },              // 绝缘体=拦获得（规则改写，无实体同口径）
            { AtomicEffectType.SummonToken, SpecColor.Gray },                 // 第三步：衍生物=纯工具
            { AtomicEffectType.EchoCopy, SpecColor.Blue },                   // 回响=复制重演=节奏（2026-11-05 定案蓝；旧灰「纯工具」作废）
            { AtomicEffectType.GrantTemporary, SpecColor.Gray },              // 遗忘=消失标记（纯工具）
            { AtomicEffectType.SetPower, SpecColor.Gray },                    // 系统行（预留）
            { AtomicEffectType.SetCost, SpecColor.Gray },                     // 系统行（预留）
            { AtomicEffectType.DeclareVictory, SpecColor.Gray },              // 系统行（预留）
            { AtomicEffectType.Morph, SpecColor.Gray },                       // 模拟=纯工具（动态费用行·预留）
            { AtomicEffectType.GrantMorph, SpecColor.Gray },                  // 变形=纯工具（动态费用行·预留）
            //（Charge 充能条目已随 2026-11-05 充能系统动作化改版删除——表行已删，充能不再是原子效果）
            { AtomicEffectType.SituationGate, SpecColor.Gray },               // 门槛行=引擎条件（预留）
            { AtomicEffectType.EngineCountdown, SpecColor.Gray },             // 引擎主干族（均预留）
            { AtomicEffectType.EngineLuckRoll, SpecColor.Gray },
            { AtomicEffectType.EngineClash, SpecColor.Gray },
            { AtomicEffectType.EngineDeathToll, SpecColor.Gray },
            { AtomicEffectType.EngineManaSurplus, SpecColor.Gray },
            { AtomicEffectType.EngineUnity, SpecColor.Gray },
            { AtomicEffectType.EngineCurseOnDraw, SpecColor.Gray },
            { AtomicEffectType.EngineBlessingOnDraw, SpecColor.Gray },
        };

        /// <summary>按原子行覆盖（键=表行中文短名 EnumName 列——进表指纹=身份稳定；**全表生效**，
        /// 不限 ModifyGameRule——光环表 EffectType 全同 ModifyGameRule，EffectType 映射不够用，行级
        /// 定案只能落这里；键=短名跨表须唯一）：2026-11-02 用户定案批次，优先于 SpecColorOf；
        /// 未列行沿用 EffectType 映射。</summary>
        private static readonly Dictionary<string, SpecColor> SpecRowOverrides =
            new Dictionary<string, SpecColor>
        {
            // ---- 关键词表 ----
            { "系命", SpecColor.Green },                    // 定案 2026-11-02：系命=绿（覆盖字典 红+绿）
            // ---- 光环表（ModifyGameRule 默认灰）——2026-11-05 光环表侧仪典/禁令族全量改灰（工作区改动，
            // 总额保持），11-02 彩色覆盖八行随之作废删除（丰盈仪典/禁攻令/禁守令/禁发令/禁召令/禁抽令/
            // 禁高费令/牌库顶当手牌），回落字典默认灰——与 README「各仪典/各禁令=灰」同口径 ----
            // ---- 一般效果表 ----
            { "抽卡", SpecColor.Blue },                     // 定案 2026-11-02：蓝（字典绿作废）
            { "激励", SpecColor.Red },                      // 定案 2026-11-02：红（字典蓝作废）
            { "磨牌", SpecColor.Blue },                     // 定案 2026-11-02：蓝（字典绿作废）
            { "弃牌", SpecColor.Blue },                     // 定案 2026-11-02：蓝（字典绿作废）
            { "移动", SpecColor.Gray },                     // 定案 2026-11-02：灰（字典红作废）
            // ---- 指示物表 ----
            { "减半", SpecColor.Gray },                     // 定案 2026-11-05：灰（字典同批改灰；11-02 蓝/更早绿均作废）
            // 属性减少 override 随行删除（2026-11-02 属性±行退役——用户定案「只能分开加减」）
            // 灵视 2026-11-02 回默认灰（旧蓝覆盖随本批作废——用户定案「按应值改：灵视→灰」）
        };

        /// <summary>豁免清单：费用增减四色行（AddCostUp/Down×灰红蓝绿）**已按四色处理**
        ///（2026-11-02 用户裁定：每行只动对应色，不动）——不按「费用增减=灰」特例报，恒放行。</summary>
        private static readonly HashSet<AtomicEffectType> SpecColorExempt = new HashSet<AtomicEffectType>
        {
            AtomicEffectType.AddCostUpGray, AtomicEffectType.AddCostUpRed,
            AtomicEffectType.AddCostUpBlue, AtomicEffectType.AddCostUpGreen,
            AtomicEffectType.AddCostDownGray, AtomicEffectType.AddCostDownRed,
            AtomicEffectType.AddCostDownBlue, AtomicEffectType.AddCostDownGreen,
        };

        /// <summary>关键词 id → 规范色（印刷/引用化本体关键词的行色反查：扫 Grant 族表行经
        /// GrantKeywordHandlerFactory 建 map；惰性一份）。</summary>
        private static Dictionary<string, SpecColor> _kwSpecColors;

        private static Dictionary<string, SpecColor> KeywordSpecColors()
        {
            if (_kwSpecColors != null) return _kwSpecColors;
            var map = new Dictionary<string, SpecColor>();
            foreach (var cfg in CardCore.Attribute.AtomicEffectTable.GetAll())
            {
                if (cfg?.ManaList == null || cfg.ManaList.IsZero) continue;
                if (System.Enum.TryParse<AtomicEffectType>(cfg.EnumName, out var t)
                    && GrantKeywordHandlerFactory.TryGetKeywordId(t, out var kwId))
                    map[kwId] = FlagsOf(cfg.ManaList);
            }
            _kwSpecColors = map;
            return map;
        }

        /// <summary>费用构成 → 规范色位集（非零色并集；任意不计入——不进卡费）。</summary>
        private static SpecColor FlagsOf(CardCore.ElementCost c)
        {
            var f = SpecColor.None;
            if (c == null) return f;
            foreach (var m in c.NonzeroColors())
                f |= SpecOf(m);
            return f;
        }

        private static SpecColor SpecOf(CardCore.ManaType m)
        {
            switch (m)
            {
                case CardCore.ManaType.Gray: return SpecColor.Gray;
                case CardCore.ManaType.Red: return SpecColor.Red;
                case CardCore.ManaType.Blue: return SpecColor.Blue;
                case CardCore.ManaType.Green: return SpecColor.Green;
                default: return SpecColor.None; // 任意（Any）不进卡费
            }
        }

        private static string ColorZh(SpecColor c)
        {
            if (c == SpecColor.None) return "无";
            var parts = new List<string>();
            if ((c & SpecColor.Gray) != 0) parts.Add("灰");
            if ((c & SpecColor.Red) != 0) parts.Add("红");
            if ((c & SpecColor.Blue) != 0) parts.Add("蓝");
            if ((c & SpecColor.Green) != 0) parts.Add("绿");
            return string.Join("+", parts);
        }

        /// <summary>
        /// 颜色校验收尾（README《效果颜色规范（2026-11-02 定案)》落地——**只报不改**）：
        /// ① 原子级——五分表全部计价行，现值色集 ≠ 应得色集 → 逐条 Warning「原子名 / 现值 / 应值」；
        /// ② 卡级（混色借用三档）——本家=内容色最大份额色（代价抵扣前逐色推导费，剔除身材灰 S 与
        /// 结构加价灰——计价内核口径，规范侧攻=红/血=绿，不把身材灰当异色防全体带身材卡误报）：
        /// Ĉ1-2 异色分量>总费50% 报警 / Ĉ3-6 主效果（非 Then 奖励）异色报警 / Ĉ7-9 任何异色报警。
        /// Console 收尾统计：「原子符合 X / 不符合 Y（前 10 条）；卡级违规 K 张（前 10 张）」。
        /// 改色由人决定——本口不写任何数据。
        /// </summary>
        private static void RunColorAudit()
        {
            CardCore.Attribute.AtomicEffectTable.Reload(); // 读现值（上游已写盘，此口只读）
            CardCore.EffectsLibrary.Reload();

            // ---------- ① 原子级 ----------
            int atomOk = 0;
            var atomBad = new List<string>();
            foreach (var cfg in CardCore.Attribute.AtomicEffectTable.GetAll())
            {
                if (cfg == null || cfg.ManaList == null || cfg.ManaList.IsZero) continue; // 不计价行（引擎主干/系统/门槛）不比对
                if (!System.Enum.TryParse<AtomicEffectType>(cfg.EnumName, out var t)) continue; // 解析失败装载期已告警
                if (SpecColorExempt.Contains(t)) { atomOk++; continue; }
                if (!SpecColorOf.TryGetValue(t, out var want))
                {
                    atomBad.Add($"{cfg.DisplayName} / 现值{ColorZh(FlagsOf(cfg.ManaList))} / 应值未收录——SpecColorOf 缺行请补");
                    continue;
                }
                if (SpecRowOverrides.TryGetValue(cfg.DisplayName, out var rowWant))
                    want = rowWant; // 行级定案覆盖（全表生效——光环族 EffectType 全同，行级判色唯一落点）
                var cur = FlagsOf(cfg.ManaList);
                if (cur == want) atomOk++;
                else atomBad.Add($"{cfg.DisplayName} / 现值{ColorZh(cur)} / 应值{ColorZh(want)}");
            }
            foreach (var m in atomBad)
                Debug.LogWarning($"[颜色][原子] {m}");

            // ---------- ② 卡级（主表 + 教学副表）----------
            var cards = new List<CardCore.CardData>();
            string mainPath = SynergyUI.CardDataPaths.FileIn("Cards.json");
            if (File.Exists(mainPath))
                cards.AddRange(CardLoader.LoadCardsFromText(File.ReadAllText(mainPath)));
            string tutPath = SynergyUI.CardDataPaths.TutorialFileIn("TutorialCards.json");
            if (File.Exists(tutPath))
                cards.AddRange(CardLoader.LoadCardsFromText(File.ReadAllText(tutPath)));

            var kwMap = KeywordSpecColors();

            // ---------- ③ 代价色对齐（弱版·2026-11-05 恢复，只报不改） ----------
            // 净费两处均**逐行色锁**（卡层 ApplyCastDeduction / 效果级 EffectActivationGate.NetBill 同口径，
            // 2026-11-05 收口定案）：代价行可产出色 ∩ 可抵账单色 = ∅ ⇒ 抵扣恒 0——代价沦为纯下位
            //（执行照旧但无任何折扣），多为内容错配（如弹回红色化后蓝套「对手抽1」抵不了红锚），
            // 点名供表侧对色；同色/有交集 = 合法不报。空账单（0费）不报——纯下位代价属设计自由。
            // 账单色取值：主动效果行=该效果锚价色（NetBill 抵扣对象）；卡级行+非主动效果行
            // = 整卡抵扣前费色 ∪ 主动效果锚价色（法术施放账单=基价+锚价整笔，GetCardCost 同口径）。
            int costMisalign = 0;
            foreach (var cd in cards)
            {
                if (cd == null) continue;

                // 效果级代价行（主动效果=发动期净费，账单=本效果锚价）
                if (cd.Effects != null)
                    foreach (var fxr in cd.Effects)
                    {
                        if (fxr == null || fxr.Costs == null || fxr.Costs.Count == 0) continue;
                        bool isActive = fxr.TriggerTiming == 19 || fxr.TriggerTiming == 20 || fxr.TriggerTiming == 21;
                        if (!isActive) continue; // 非主动行走施放期账单（下方卡级段并口径）
                        var def = CardEffectConverter.ConvertOne(fxr, cd.ID);
                        if (def == null) continue;
                        var anchor = CostDerivationService.DeriveElementCosts(def, 0);
                        if (anchor.IsZero) continue;
                        var billFlags = FlagsOf(anchor);
                        foreach (var row in fxr.Costs)
                        {
                            if (row == null) continue;
                            foreach (var pe in row.AllPayloads())
                            {
                                var atomRow = CardCore.Attribute.AtomicEffectTable.GetByHashId(pe?.refId);
                                if (atomRow == null || atomRow.ManaList == null || atomRow.ManaList.IsZero) continue;
                                var costFlags = FlagsOf(atomRow.ManaList);
                                if ((costFlags & billFlags) == 0)
                                {
                                    costMisalign++;
                                    Debug.LogWarning("[颜色][代价对齐] " + cd.CardName + "(" + cd.ID + ") 效果「"
                                        + fxr.DisplayName + "」锚价" + ColorZh(billFlags) + " vs 代价「"
                                        + atomRow.DisplayName + "」" + ColorZh(costFlags)
                                        + "——无交集：抵扣恒0（2026-11-05 色锁收口），代价纯下位，请表侧对色");
                                }
                            }
                        }
                    }

                // 非主动效果行（施放期账单 = 抵扣前费 ∪ 主动效果锚价；卡级行已随 2026-11-08 效果级化整删）
                bool hasCastRows = false;
                if (cd.Effects != null)
                    foreach (var fxr in cd.Effects)
                        if (fxr?.Costs != null && fxr.Costs.Count > 0
                            && !(fxr.TriggerTiming == 19 || fxr.TriggerTiming == 20 || fxr.TriggerTiming == 21))
                        { hasCastRows = true; break; }
                if (hasCastRows)
                {
                    var r = CardCostService.Derive(cd);
                    var bill = r.PrePayloadCost.Clone();
                    if (cd.Effects != null)
                        foreach (var fxr in cd.Effects)
                        {
                            if (fxr == null) continue;
                            bool isActive = fxr.TriggerTiming == 19 || fxr.TriggerTiming == 20 || fxr.TriggerTiming == 21;
                            if (!isActive) continue;
                            var def = CardEffectConverter.ConvertOne(fxr, cd.ID); // 主动锚价并入施放账单（法术口径）
                            if (def == null) continue;
                            var a = CostDerivationService.DeriveElementCosts(def, 0);
                            foreach (var col in a.NonzeroColors())
                                bill[col] = bill[col] + a[col];
                        }
                    if (!bill.IsZero)
                    {
                        var billFlags = FlagsOf(bill);
                        var rows = new List<CardCore.CostEntry>();
                        if (cd.Effects != null)
                            foreach (var fxr in cd.Effects)
                            {
                                if (fxr == null || fxr.Costs == null) continue;
                                if (fxr.TriggerTiming == 19 || fxr.TriggerTiming == 20 || fxr.TriggerTiming == 21) continue;
                                rows.AddRange(fxr.Costs);
                            }
                        foreach (var row in rows)
                        {
                            if (row == null) continue;
                            foreach (var pe in row.AllPayloads())
                            {
                                var atomRow = CardCore.Attribute.AtomicEffectTable.GetByHashId(pe?.refId);
                                if (atomRow == null || atomRow.ManaList == null || atomRow.ManaList.IsZero) continue;
                                var costFlags = FlagsOf(atomRow.ManaList);
                                if ((costFlags & billFlags) == 0)
                                {
                                    costMisalign++;
                                    Debug.LogWarning("[颜色][代价对齐] " + cd.CardName + "(" + cd.ID + ") 施放账单"
                                        + ColorZh(billFlags) + " vs 卡级代价「" + atomRow.DisplayName + "」"
                                        + ColorZh(costFlags)
                                        + "——无交集：抵扣恒0（色锁定案），代价纯下位，请表侧对色");
                                }
                            }
                        }
                    }
                }
            }

            Debug.Log("[颜色校验] 原子符合 " + atomOk + " / 不符合 " + atomBad.Count +
                      "（前 10 条：" + (atomBad.Count == 0 ? "无" : string.Join("；", atomBad.Take(10))) +
                      "）——只报不改，改色由人决定。卡级分色校验已于 2026-11-02 删除：没有「本家色」，" +
                      "卡可带任意颜色效果，可用性由卡组校验负责。代价色对齐（弱版·2026-11-05 恢复）：无交集 " +
                      costMisalign + " 条" + (costMisalign == 0 ? "" : "（见 [颜色][代价对齐] 警告——抵扣恒0，表侧对色候选）"));
        }

        /// <summary>步骤递归收集原子行色：kind=0 主干入主效果、branch.then 入 Then 奖励；
        /// kind=1 门/产出/引擎条件的 then/else 全为奖励侧；kind=2 抉择各模式=主效果变体。</summary>
        private static void WalkColorStep(CardCore.EffectStepData st, List<string> main, List<string> then)
        {
            if (st == null) return;
            switch (st.kind)
            {
                case 0:
                    if (st.atomic != null)
                    {
                        AddAtomColor(st.atomic.refId, main);
                        if (st.atomic.branch?.then != null)
                            foreach (var t in st.atomic.branch.then) AddAtomColor(t?.refId, then);
                    }
                    break;
                case 1:
                    if (st.thenSteps != null) foreach (var t in st.thenSteps) AddAtomColor(t?.refId, then);
                    if (st.elseSteps != null) foreach (var t in st.elseSteps) AddAtomColor(t?.refId, then);
                    break;
                case 2:
                    if (st.choices != null)
                        foreach (var c in st.choices)
                            if (c?.steps != null) foreach (var sub in c.steps) WalkColorStep(sub, main, then);
                    break;
            }
        }

        /// <summary>原子引用 → 「行名(色集)」；不计价行（ManaList null/全零=引擎行等）无色不入表。</summary>
        private static void AddAtomColor(string refId, List<string> sink)
        {
            var row = CardCore.Attribute.AtomicEffectTable.GetByHashId(refId);
            if (row == null || row.ManaList == null || row.ManaList.IsZero) return;
            sink.Add($"{row.DisplayName}({ColorZh(FlagsOf(row.ManaList))})");
        }

        /// <summary>色集含本家外任一色的原子（结构化判——不回解字符串）；去重保序。</summary>
        private static List<string> DistinctOff(IEnumerable<string> atoms, SpecColor homeF)
        {
            var seen = new HashSet<string>();
            var outList = new List<string>();
            foreach (var a in atoms)
            {
                int p = a.LastIndexOf('(');
                if (p < 0) continue;
                string name = a.Substring(0, p);
                var f = SpecColor.None;
                foreach (var zh in a.Substring(p + 1).TrimEnd(')').Split('+'))
                {
                    if (zh == "灰") f |= SpecColor.Gray;
                    else if (zh == "红") f |= SpecColor.Red;
                    else if (zh == "蓝") f |= SpecColor.Blue;
                    else if (zh == "绿") f |= SpecColor.Green;
                }
                if (f == SpecColor.None || (f & ~homeF) == 0) continue;
                if (seen.Add(name)) outList.Add(a);
            }
            return outList;
        }

        /// <summary>卡 ID 重复校验（2026-11-05 冻结配套，三池共用）：冻结后 id 不随内容变化，
        /// 重复 id 在「先清后写」或按 id upsert 时会互相覆盖——宁可中止也不静默吞卡。</summary>
        private static bool HasDuplicateCardIds(List<CardCore.CardData> cards, string poolLabel)
        {
            if (cards == null || cards.Count == 0) return false;
            var dups = cards.GroupBy(c => c.ID)
                            .Where(g => !string.IsNullOrEmpty(g.Key) && g.Count() > 1)
                            .ToList();
            if (dups.Count == 0) return false;
            foreach (var g in dups)
                Debug.LogError($"[Regen][{poolLabel}] 卡 ID 重复：{g.Key} × {g.Count()} 张" +
                               $"（{string.Join("、", g.Select(c => c.CardName))}）——已中止落盘，请先修表");
            return true;
        }

        /// <summary>仅重推费用（2026-11-05 三池口径）：不动任何 ID——不含 RegenAtomicTableIds，
        /// 主干是 RegenOneDeck（正式池）+ RegenTutorialAndPresetTables（教学/预组）+ RegenTrainingTables（训练池），
        /// 三者与全量入口共用同一套落盘代码，区别只在「不重推原子 id、不换卡/效果号」。
        /// 每条路径落盘前都过 HasDuplicateCardIds。</summary>
        [MenuItem("Tools/仅重推费用（三池·不动ID）")]
        public static void RegenerateCardCostsOnly()
        {
            var noRenames = new Dictionary<string, string>(); // 冻结后不存在换号：空映射即「只重推费用」
            string path = SynergyUI.CardDataPaths.FileIn("Cards.json");
            if (File.Exists(path))
            {
                RegenOneDeck(path, noRenames);
            }
            else
            {
                Debug.LogWarning($"[Regen] 找不到正式卡表 {path}（玩家池空=设计态，跳过）");
            }

            RemapTutorialAtomRefs(noRenames);   // 空映射＝无操作，保留与全量入口同序
            RegenTutorialAndPresetTables();     // 教学池 + 三套预组
            RegenTrainingTables(noRenames);     // 训练池
        }

        /// <summary>原子表 ID 台账（2026-11-05 定案：**ID 冻结**，取代旧的「ID=sha256(DisplayName) 重推」）。
        ///
        /// 旧口径（2026-09-16 三层推导链）把 ID 当内容派生值：改 DisplayName → 换 ID → 调用方重映射全库引用。
        /// 代价是任何文本润色（甚至改标点）都会打断卡 refId / 效果 steps / 存档 / 回放 / 训练标签 /
        /// AI manifest 指纹，mod 与本地化无从谈起。新口径把身份与文本拆开：
        ///   · ID = 永久稳定引用键，**永不改写、永不回收**（已退役 ID 不得被复用）；
        ///   · TextHash = sha256(DisplayName.Trim()) 前 8 位，降级为三用：新建行初始 ID 生成器 /
        ///     同文查重键 / 文本漂移检测器；
        ///   · 查重从「主键顺带」变成「落盘前的显式检查」——同一 TextHash 出现两行 = 语义重复，
        ///     该文件**拒绝落盘**（保留磁盘原样），列明细人工处置；
        ///   · 语义变更（改数值/目标域/EffectType）应新建行 + 旧行 SupersededBy，而非原地改写。
        ///
        /// 返回值 renames 恒为空（冻结后不存在「换号」），保留签名给下游重映射调用点，避免改动调用链；
        /// 写盘后立即 AtomicEffectTable.Reload——静态映射不随外部改表自刷新。</summary>
        private static Dictionary<string, string> RegenAtomicTableIds()
        {
            var renames = new Dictionary<string, string>();
            using var sha = System.Security.Cryptography.SHA256.Create();

            // 第一遍：收集全部已有 ID（跨文件全局唯一），供新建行撞号顺延
            var files = new List<(string relPath, Newtonsoft.Json.Linq.JArray root)>();
            var usedIds = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (var relPath in CardCore.Attribute.AtomicEffectTable.TableFiles)
            {
                string path = Path.Combine(Application.dataPath, relPath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(path))
                {
                    Debug.LogError($"[Regen] 找不到原子表分表 {path}");
                    return null;
                }
                var root = Newtonsoft.Json.Linq.JArray.Parse(File.ReadAllText(path));
                files.Add((relPath, root));
                foreach (var row in root.OfType<Newtonsoft.Json.Linq.JObject>())
                {
                    var id = row["ID"]?.ToString();
                    if (!string.IsNullOrEmpty(id)) usedIds.Add(id);
                }
            }

            // 第二遍：冻结 ID、维护 TextHash、查重
            var hashOwner = new Dictionary<string, string>(System.StringComparer.Ordinal);
            var textAliases = new Dictionary<string, string>(System.StringComparer.Ordinal); // 2026-11-06：文本漂移 → 别名
            int totalRows = 0, newIds = 0, drift = 0, dups = 0, blockedFiles = 0;
            foreach (var (relPath, root) in files)
            {
                string path = Path.Combine(Application.dataPath, relPath.Replace('/', Path.DirectorySeparatorChar));
                bool fileOk = true;

                foreach (var row in root.OfType<Newtonsoft.Json.Linq.JObject>())
                {
                    totalRows++;
                    var dn = row["DisplayName"]?.ToString();
                    if (string.IsNullOrWhiteSpace(dn)) continue;

                    string textHash = System.BitConverter.ToString(
                            sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(dn.Trim())))
                        .Replace("-", "").ToLowerInvariant().Substring(0, 8);

                    string id = row["ID"]?.ToString();
                    if (string.IsNullOrEmpty(id))
                    {
                        // 新建行：ID 取 TextHash，撞号顺延（usedIds 含已退役 ID，故不复用）
                        string cand = textHash;
                        int n = 1;
                        while (usedIds.Contains(cand)) { n++; cand = textHash + "-" + n; }
                        row["ID"] = cand;
                        usedIds.Add(cand);
                        id = cand;
                        newIds++;
                        Debug.Log($"[Regen] 原子表新行分配 ID {cand}：{dn}");
                    }

                    string oldHash = row["TextHash"]?.ToString();
                    if (!string.IsNullOrEmpty(oldHash) && oldHash != textHash)
                    {
                        drift++;
                        // 2026-11-06：文本漂移不换行号——新文本哈希登记为别名（新哈希 → 冻结行 ID），
                        // 供按文本派生 ID 的旧工具/存档回落；行 ID 永不改写。
                        if (textHash != id) textAliases[textHash] = id;
                        Debug.LogWarning($"[Regen] 文本漂移别名：{textHash} → {id}：{dn}" +
                                         "——行 ID 保持冻结；旧引用无需跟换，新文本哈希经别名回落本行。");
                    }
                    row["TextHash"] = textHash;

                    if (hashOwner.TryGetValue(textHash, out var owner))
                    {
                        dups++;
                        Debug.LogError($"[Regen] 原子表语义重复：{relPath} 的 {id} 与 {owner} 同文" +
                                       $"（TextHash={textHash}）：{dn} —— 查重失败，本文件不落盘");
                        fileOk = false; // 该文件整体拒写（已改的内存态丢弃，磁盘留原样）
                    }
                    else
                    {
                        hashOwner[textHash] = $"{relPath}/{id}";
                    }
                }

                if (!fileOk) { blockedFiles++; continue; }

                var sb = new System.Text.StringBuilder();
                using (var tw = new StringWriter(sb))
                using (var jw = new Newtonsoft.Json.JsonTextWriter(tw)
                       { Formatting = Newtonsoft.Json.Formatting.Indented, IndentChar = ' ', Indentation = 2 })
                    root.WriteTo(jw);
                File.WriteAllText(path, sb.ToString() + "\n");
                Debug.Log($"[Regen] 原子表分表落盘（ID 冻结）：{root.Count} 行 → {path}");
            }

            // 2026-11-06：文本漂移别名先落盘再重载——下方连带校验读的是含新别名的 AliasMap。
            WriteTextAliases(textAliases, usedIds);
            CardCore.Attribute.AtomicEffectTable.Reload();

            // 别名表连带校验（2026-11-05）：坏别名会让「旧号回落」静默失效——落盘时一并报出。
            // usedIds = 本次落盘后的全部现行行 id（跨文件累计）。
            foreach (var kv in CardCore.Attribute.AtomicEffectTable.AliasMap)
            {
                if (usedIds.Contains(kv.Key))
                {
                    Debug.LogError($"[Regen] 别名源仍是现行行：{kv.Key} → {kv.Value}（应删除该别名）");
                    continue;
                }
                string cur = kv.Value;
                int hop = 0;
                bool resolved = false;
                while (hop <= 8)
                {
                    if (usedIds.Contains(cur)) { resolved = true; break; }
                    if (!CardCore.Attribute.AtomicEffectTable.AliasMap.TryGetValue(cur, out var next)) break;
                    cur = next;
                    hop++;
                }
                if (!resolved)
                    Debug.LogError($"[Regen] 别名断链/成环：{kv.Key} → … → {cur}");
            }

            Debug.Log($"[Regen] 原子表 ID 台账：{totalRows} 行，新分配 ID {newIds} 条，文本漂移 {drift} 条，" +
                      $"语义重复 {dups} 条，拒写文件 {blockedFiles} 个");
            if (dups > 0)
                Debug.LogError("[Regen] 存在语义重复的原子行：请改措辞，或显式合并（旧行 SupersededBy 指向保留行）后重跑");
            return renames;
        }

        /// <summary>2026-11-06：原子表文本漂移别名落盘——`Configs/原子表ID别名.json` 的 aliases
        /// 合并写入（只增不删，保留 _note/version 与既有条目）。校验：源不得是现行行 ID、目标必须
        /// 存在、源≠目标；坏条目只报不写。方向=新文本哈希 → 冻结行 ID（按文本派生 ID 的旧工具/
        /// 存档靠别名回落本行；行 ID 本身永不换号）。写盘后由调用方 Reload 生效。</summary>
        private static void WriteTextAliases(Dictionary<string, string> additions, HashSet<string> currentIds)
        {
            if (additions == null || additions.Count == 0)
            {
                Debug.Log("[Regen] 文本漂移别名：本次无新增");
                return;
            }
            string aliasPath = Path.Combine(Application.dataPath,
                CardCore.Attribute.AtomicEffectTable.AliasFile.Replace('/', Path.DirectorySeparatorChar));
            Newtonsoft.Json.Linq.JObject root;
            if (File.Exists(aliasPath))
                root = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(aliasPath));
            else
                root = new Newtonsoft.Json.Linq.JObject(
                    new Newtonsoft.Json.Linq.JProperty("_note", "原子表 ID 别名（旧 → 现行）。"),
                    new Newtonsoft.Json.Linq.JProperty("version", 1),
                    new Newtonsoft.Json.Linq.JProperty("aliases", new Newtonsoft.Json.Linq.JObject()));
            var aliases = root["aliases"] as Newtonsoft.Json.Linq.JObject
                          ?? new Newtonsoft.Json.Linq.JObject();
            int added = 0, skipped = 0, invalid = 0;
            foreach (var kv in additions)
            {
                string from = kv.Key, to = kv.Value;
                if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to) || from == to
                    || currentIds.Contains(from) || !currentIds.Contains(to))
                {
                    invalid++;
                    Debug.LogError($"[Regen] 文本漂移别名非法：{from} → {to}（源为现行行/目标缺失/自环）——不写入");
                    continue;
                }
                if (aliases.Property(from) != null) { skipped++; continue; }
                aliases[from] = to;
                added++;
            }
            if (added == 0)
            {
                Debug.Log($"[Regen] 文本漂移别名：新增 0 条，跳过 {skipped} 条，非法 {invalid} 条");
                return;
            }
            root["aliases"] = aliases;
            var sb = new System.Text.StringBuilder();
            using (var tw = new StringWriter(sb))
            using (var jw = new Newtonsoft.Json.JsonTextWriter(tw)
                   { Formatting = Newtonsoft.Json.Formatting.Indented, IndentChar = ' ', Indentation = 2 })
                root.WriteTo(jw);
            File.WriteAllText(aliasPath, sb.ToString() + "\n");
            Debug.Log($"[Regen] 文本漂移别名：新增 {added} 条，跳过 {skipped} 条，非法 {invalid} 条 → {aliasPath}");
            foreach (var kv in additions)
            {
                var prop = aliases.Property(kv.Key);
                if (prop != null && prop.Value?.ToString() == kv.Value)
                    Debug.Log($"[Regen]   别名 {kv.Key} → {kv.Value}");
            }
        }

        /// <summary>原子表 ID 台账体检（只读，2026-11-05 冻结口径的常备闸门）：
        /// ①ID 非空且全局唯一 ②TextHash 已回填且等于 sha256(DisplayName.Trim()) 前 8 位
        /// ③没有两条未退役行同 TextHash（同文两行 = 语义重复）。只报不写、不改 ID。</summary>
        [MenuItem("Tools/验证/原子表 ID 台账体检")]
        public static void VerifyAtomIdLedger()
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            var idOwner = new Dictionary<string, string>(System.StringComparer.Ordinal);
            var hashOwner = new Dictionary<string, string>(System.StringComparer.Ordinal);
            int rows = 0, bad = 0, drifted = 0;

            foreach (var relPath in CardCore.Attribute.AtomicEffectTable.TableFiles)
            {
                string path = Path.Combine(Application.dataPath, relPath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(path))
                {
                    Debug.LogError($"[台账] 找不到分表 {path}");
                    bad++;
                    continue;
                }

                var root = Newtonsoft.Json.Linq.JArray.Parse(File.ReadAllText(path));
                foreach (var row in root.OfType<Newtonsoft.Json.Linq.JObject>())
                {
                    var dn = row["DisplayName"]?.ToString();
                    if (string.IsNullOrWhiteSpace(dn)) continue;
                    rows++;

                    string id = row["ID"]?.ToString();
                    if (string.IsNullOrEmpty(id))
                    {
                        Debug.LogError($"[台账] 缺 ID：{relPath} {dn}");
                        bad++;
                    }
                    else if (idOwner.TryGetValue(id, out var idDup))
                    {
                        Debug.LogError($"[台账] ID 重复 {id}：{relPath} 与 {idDup}");
                        bad++;
                    }
                    else
                    {
                        idOwner[id] = relPath;
                    }

                    string calc = System.BitConverter.ToString(
                            sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(dn.Trim())))
                        .Replace("-", "").ToLowerInvariant().Substring(0, 8);
                    string th = row["TextHash"]?.ToString();
                    if (string.IsNullOrEmpty(th))
                    {
                        Debug.LogError($"[台账] 缺 TextHash：{relPath} {id} {dn}（跑 Config/atom_id_ledger.py backfill）");
                        bad++;
                        continue;
                    }
                    if (th != calc)
                    {
                        drifted++;
                        Debug.LogWarning($"[台账] 文本已变更（ID 不变 {id}）：{th} → {calc}：{dn}");
                    }

                    if (hashOwner.TryGetValue(th, out var hashDup))
                    {
                        Debug.LogError($"[台账] 语义重复（TextHash={th}）：{relPath}/{id} 与 {hashDup}：{dn}");
                        bad++;
                    }
                    else
                    {
                        hashOwner[th] = $"{relPath}/{id}";
                    }
                }
            }

            // ---- 别名表校验（2026-11-05 ID 冻结配套）----
            // 三条：①源 id 不得仍是现行行（否则掩盖真实引用）②沿链必须落在现行行 ③不得成环/超 8 跳。
            string aliasPath = Path.Combine(Application.dataPath,
                CardCore.Attribute.AtomicEffectTable.AliasFile.Replace('/', Path.DirectorySeparatorChar));
            var aliases = CardCore.Attribute.AtomicEffectTable.AliasMap;
            int aliasCount = aliases != null ? aliases.Count : 0;
            if (File.Exists(aliasPath))
            {
                string aliasRaw = File.ReadAllText(aliasPath);
                if (aliasRaw.Contains("\"aliases\"") && aliasCount == 0)
                {
                    Debug.LogError($"[台账] 别名表存在但解析出 0 条（格式坏？）：{aliasPath}");
                    bad++;
                }
                foreach (var kv in aliases)
                {
                    if (idOwner.ContainsKey(kv.Key))
                    {
                        Debug.LogError($"[台账] 别名源仍是现行行：{kv.Key} → {kv.Value}（应删除该别名）");
                        bad++;
                        continue;
                    }
                    string cur = kv.Value;
                    int hop = 0;
                    bool resolved = false;
                    while (hop <= 8)
                    {
                        if (idOwner.ContainsKey(cur)) { resolved = true; break; }
                        if (!aliases.TryGetValue(cur, out var next)) break;
                        cur = next;
                        hop++;
                    }
                    if (!resolved)
                    {
                        Debug.LogError($"[台账] 别名断链/成环：{kv.Key} → … → {cur}");
                        bad++;
                    }
                }
            }

            if (bad == 0)
                Debug.Log($"[台账] PASS：{rows} 行 / 别名 {aliasCount} 条，ID 唯一、TextHash 齐备、无同文重复、" +
                          $"别名链可解析（文本漂移 {drifted} 条，不影响引用）");
            else
                Debug.LogError($"[台账] FAIL：{rows} 行 / 别名 {aliasCount} 条，{bad} 处问题（详见上方）");
        }

        /// <summary>
        /// 对双表卡表重推导费用并回写（2026-09-14 引用化口径）。
        /// 装载（LoadCardsFromText）已完成 effectIds 解析；2026-10-22 恒推导定案后 EnsureCost 恒刷新
        /// 推导费显示缓存（旧「清声明重推导」「抉择=最大模式费声明」两分支退役——快照本就不参与支付；
        /// ModeCostCache internal，经 EnsureCost 回填缓存）。
        /// 回写统一走 CardConfigSerializer.Save——costList / C_+HashCard 卡 id / effectIds
        ///（Effects.json 幂等 upsert）全走序列化器单源，天然按内容去重（同内容同 id），
        /// 消灭旧裸 ContentId 双轨。**先清后写**：防止重推导换 id 后旧条目残留。
        /// 返回 false=悬空中止/空表（调用方据此跳过教学副表跟换）。
        /// </summary>
        private static bool RegenOneDeck(string path, Dictionary<string, string> atomRenames)
        {
            // 防陈旧缓存（2026-10-16 修复）：原子表静态三映射不随外部直改 JSON 自刷新——不重读则
            // 费用推导（ComputeAtomCostByColor / 效果锚价快照）仍按旧表计价，即「重推导后效果表
            // 费用还是旧的」根因。原子表先行：重映射悬空守卫需按现行行 ID 校验引用。
            CardCore.Attribute.AtomicEffectTable.Reload();
            if (!RemapAndRefreshEffectLibrary(path, atomRenames)) return false; // 悬空引用中止（防整库重写剔原子坐实丢内容）
            CardCore.EffectsLibrary.Reload(); // 防陈旧缓存：外部直改 Effects.json 后编辑器静态 _byId 不会自刷新（此处亦读回上一步重写的库文件）
            var cards = CardLoader.LoadCardsFromText(File.ReadAllText(path));

            // ID 重复校验（2026-11-05 冻结配套）：冻结后 id 不随内容变化，重复 id 会互相覆盖——
            // 落盘前拦下并中止，宁可这一步失败也不静默吞掉一张卡。
            if (HasDuplicateCardIds(cards, "正式")) return false;
            if (cards.Count == 0)
            {
                // 2026-11-03 措辞改版：主表=玩家自建内容，空=设计态常态（2026-10-27 起玩家池清空、
                // 2026-11-03 训练池迁 TrainingData）——不再是错误，降级提示即可。
                Debug.LogWarning($"[Regen] 主表为空（玩家池空=设计态，训练池在 TrainingData）——跳过主表重推：{path}");
                return false;
            }

            foreach (var card in cards)
            {
                card.ResetCache(); // 清费用缓存（原子表/效果库已 Reload——防旧推导驻留）
                CardCostService.EnsureCost(card); // 恒刷新推导费显示缓存（抉择=最大模式费）
                var r = CardCostService.Derive(card);
                // D=0（无身材无效果无关键词/效果被契约剔除）= 空 Cost——合法态，日志守卫防空引用
                string costStr = card.Cost != null && !card.Cost.IsZero
                    ? card.Cost.ToString()
                    : "(空 Cost——D=0 或效果全被内容契约剔除)";
                Debug.Log($"[Regen] {card.CardName} S={r.S} K={r.K} E={r.EAnchor} D={r.DerivedTotal} → 档位Ĉ={r.DerivedTier}：{costStr}");
                // 极性校验（2026-11-02 定案）：代价型原子指向自己（弃牌@己方手牌/自伤@Self/消灭己方/
                // 磨己方牌库等，白名单见 CostDerivationService.SelfCostAtoms）——计价层已裁定 0 费，
                // 此处按「卡名+效果+表锚价」上报不静默；命中=设计意图问题，请改走代价栏或改指对侧。
                foreach (var v in CardCore.CostDerivationService.SelfCostViolations(card))
                    Debug.LogError($"[Regen][极性] {card.CardName}({card.ID}) {v}");
                // 代价栏翻转限制（2026-11-02 定案）：极性0/无 target 原子进代价栏——装载期已剔除
                //（Error 日志），此处按「卡名+效果+原子 id」上报不静默，重推不洗白违规数据。
                foreach (var v in CardCore.CostDerivationService.PayloadCostViolations(card))
                    Debug.LogError($"[Regen][代价栏] {card.CardName}({card.ID}) {v}");
            }

            // 清文件后逐张 Save：CardConfigSerializer 单源回写（C_+HashCard id / costList / effectIds+效果库 upsert）
            File.WriteAllText(path, "{\n    \"cards\": [],\n    \"deckConfig\": {\n        \"copiesPerCard\": 1\n    }\n}");
            foreach (var card in cards)
                SynergyUI.CardConfigSerializer.Save(card);
            Debug.Log($"[Regen] 已重写 {cards.Count} 张卡的费用与内容 ID → {path}");
            return true;
        }

        // ======================================== 效果库引用重映射 + 锚价重推导（2026-10-16 修复） ========================================

        /// <summary>
        /// 效果库原子引用重映射 + 全库锚价快照重推导 + 卡表引用重指。
        /// 根因：原子表换号（RegenAtomicTableIds）后 Effects.json/Cards.json 的 refId 引用未同步——
        /// 装载端 EffectSlim.ToStep 严格按 refId 解析，悬空即整原子剔除（静默丢内容）；且 refId 入
        /// HashEffect/HashCard 依赖链，换号连坐效果 id/卡 id，卡表 effectIds 引用须同步重指。
        /// 流程：①全库（含未挂卡条的独立条目）refId 旧→新重映射；②悬空守卫——重映射后仍有未知
        /// refId = 中止不落盘（列明细人工处置，防 ③ 整库重写把剔原子后的缺内容写死）；③清档重写：
        /// 逐条 Save（id 重算 + 锚价快照按**现行原子表**实时推导——「仅重推费用」入口亦经此刷新
        /// 全库快照；清档防旧 id 条目残留，upsert 不删旧行）；④效果 id 旧→新 + 原子旧→新映射回写
        /// Cards.json（effectIds 列=效果引用与关键词原子引用混装，按目标分表查换；payload.refId 同步）。
        /// 返回 false=存在悬空引用已中止，调用方应放弃本轮推导。
        /// </summary>
        private static bool RemapAndRefreshEffectLibrary(string cardsPath, Dictionary<string, string> atomRenames)
        {
            string fxPath = Path.Combine(Path.GetDirectoryName(cardsPath), "Effects.json");
            if (!File.Exists(fxPath)) return true; // 库文件不存在=空库（卡装载自会告警）

            var validAtomIds = new HashSet<string>(
                CardCore.Attribute.AtomicEffectTable.GetAll()
                    .Where(c => c != null && !string.IsNullOrEmpty(c.HashId))
                    .Select(c => c.HashId));

            var wrapper = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(fxPath));
            var items = wrapper["items"] as Newtonsoft.Json.Linq.JArray;
            if (items == null || items.Count == 0) return true;

            // ① refId 重映射（Descendants 深扫：主干步骤/分支 then/代价 payload 全覆盖）+ ② 悬空守卫
            int remapped = 0;
            var dangling = new List<string>();
            foreach (var item in items.OfType<Newtonsoft.Json.Linq.JObject>())
            {
                string host = item["name"]?.ToString() ?? item["id"]?.ToString() ?? "?";
                foreach (var prop in item.Descendants().OfType<Newtonsoft.Json.Linq.JProperty>()
                         .Where(p => p.Name == "refId" && p.Value.Type == Newtonsoft.Json.Linq.JTokenType.String).ToList())
                {
                    string rid = (string)prop.Value;
                    if (atomRenames != null && atomRenames.TryGetValue(rid, out var nid))
                    {
                        prop.Value = nid;
                        remapped++;
                        rid = nid;
                    }
                    if (!validAtomIds.Contains(rid)) dangling.Add($"{host}·{rid}");
                }
            }
            if (dangling.Count > 0)
            {
                Debug.LogError($"[Regen] Effects.json 悬空原子引用 {dangling.Count} 处——中止推导不落盘" +
                               $"（防装载端剔原子后整库重写坐实丢内容），请人工核对原子表行 ID：{string.Join("，", dangling.Distinct())}");
                return false;
            }
            if (remapped > 0)
                WriteJTokenIndented(fxPath, wrapper);

            // ③ 清档重写：Save 逐条重算 id（HashEffect）+ 锚价快照按现行原子表实时推导
            var graphs = SynergyUI.EffectLibrarySerializer.LoadAll();
            var effectIdMap = new Dictionary<string, string>();
            foreach (var g in graphs)
                if (g != null && !string.IsNullOrEmpty(g.id)) effectIdMap[g.id] = g.id;
            File.WriteAllText(fxPath, "{\"items\":[]}");
            int rekeyed = 0;
            foreach (var g in graphs)
            {
                string oldId = g.id;
                SynergyUI.EffectLibrarySerializer.Save(g);
                if (!string.IsNullOrEmpty(oldId) && effectIdMap.TryGetValue(oldId, out var cur) && cur != g.id)
                {
                    effectIdMap[oldId] = g.id; // 内容变 → 效果 id 变（卡表 effectIds 待重指）
                    rekeyed++;
                }
            }

            // ④ 卡表引用重指：effectIds（效果引用+关键词原子引用混装）与 payload.refId
            var cardsRoot = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(cardsPath));
            int cardsRemapped = 0;
            foreach (var card in (cardsRoot["cards"] as Newtonsoft.Json.Linq.JArray
                                  ?? new Newtonsoft.Json.Linq.JArray()).OfType<Newtonsoft.Json.Linq.JObject>())
            {
                foreach (var ids in card.Descendants().OfType<Newtonsoft.Json.Linq.JProperty>()
                         .Where(p => p.Name == "effectIds" && p.Value is Newtonsoft.Json.Linq.JArray).ToList())
                {
                    var arr = (Newtonsoft.Json.Linq.JArray)ids.Value;
                    for (int i = 0; i < arr.Count; i++)
                    {
                        if (arr[i].Type != Newtonsoft.Json.Linq.JTokenType.String) continue;
                        string s = (string)arr[i];
                        if (atomRenames != null && atomRenames.TryGetValue(s, out var na)) { arr[i] = na; cardsRemapped++; }
                        else if (effectIdMap.TryGetValue(s, out var ne) && ne != s) { arr[i] = ne; cardsRemapped++; }
                    }
                }
                foreach (var prop in card.Descendants().OfType<Newtonsoft.Json.Linq.JProperty>()
                         .Where(p => p.Name == "refId" && p.Value.Type == Newtonsoft.Json.Linq.JTokenType.String).ToList())
                {
                    string rid = (string)prop.Value;
                    if (atomRenames != null && atomRenames.TryGetValue(rid, out var na)) { prop.Value = na; cardsRemapped++; }
                }
            }
            if (cardsRemapped > 0)
            {
                WriteJTokenIndented(cardsPath, cardsRoot);
                Debug.Log($"[Regen] 卡表引用重指：{cardsRemapped} 条 → {cardsPath}");
            }

            Debug.Log($"[Regen] 效果库重推导：{graphs.Count} 条（原子引用重映射 {remapped} 处·效果 id 换号 {rekeyed} 条）→ {fxPath}");
            return true;
        }

        /// <summary>
        /// 教学副表原子引用跟换（2026-10-20 定案）：原子表换号后 TutorialEffects.json 的 refId 与
        /// TutorialEffects/TutorialCards 的 effectIds 原子段同步重映射——**只换原子 id**：教学效果 id
        ///（EffectsLibrary.MergeTutorialEffects 原样读入不换号）、卡 id 与费用一律不动（教学卡按
        /// 剧本钉死，允许不符计价标准）；教学卡组文件（basics/jinjie 等）只引卡 id，无需跟换。
        /// 悬空引用仅告警不中止——教学表人工维护，与正式池推导解耦（正式池悬空中止见
        /// RemapAndRefreshEffectLibrary）。
        /// </summary>
        private static void RemapTutorialAtomRefs(Dictionary<string, string> atomRenames)
        {
            if (atomRenames == null || atomRenames.Count == 0) return; // 无换号=无跟换
            var validAtomIds = new HashSet<string>(
                CardCore.Attribute.AtomicEffectTable.GetAll()
                    .Where(c => c != null && !string.IsNullOrEmpty(c.HashId))
                    .Select(c => c.HashId));
            foreach (string file in new[] { "TutorialEffects.json", "TutorialCards.json" })
            {
                string path = SynergyUI.CardDataPaths.TutorialFileIn(file);
                if (!File.Exists(path)) continue;
                var root = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(path));
                int remapped = 0;
                var dangling = new List<string>();
                foreach (var prop in root.Descendants().OfType<Newtonsoft.Json.Linq.JProperty>()
                         .Where(p => p.Name == "refId" && p.Value.Type == Newtonsoft.Json.Linq.JTokenType.String).ToList())
                {
                    string rid = (string)prop.Value;
                    if (atomRenames.TryGetValue(rid, out var nid)) { prop.Value = nid; remapped++; rid = nid; }
                    if (!validAtomIds.Contains(rid)) dangling.Add(rid);
                }
                // effectIds 混装（效果库 id+关键词原子 refId，同正式卡口径）：只换命中重映射键的原子段
                foreach (var ids in root.Descendants().OfType<Newtonsoft.Json.Linq.JProperty>()
                         .Where(p => p.Name == "effectIds" && p.Value is Newtonsoft.Json.Linq.JArray).ToList())
                {
                    var arr = (Newtonsoft.Json.Linq.JArray)ids.Value;
                    for (int i = 0; i < arr.Count; i++)
                    {
                        if (arr[i].Type != Newtonsoft.Json.Linq.JTokenType.String) continue;
                        if (atomRenames.TryGetValue((string)arr[i], out var na)) { arr[i] = na; remapped++; }
                    }
                }
                if (dangling.Count > 0)
                    Debug.LogWarning($"[Regen] {file} 悬空原子引用 {dangling.Count} 处（教学表不中止，请人工核对原子表行 ID）：{string.Join("，", dangling.Distinct())}");
                if (remapped > 0)
                {
                    WriteJTokenIndented(path, root);
                    Debug.Log($"[Regen] {file} 原子引用跟换 {remapped} 处 → {path}");
                }
            }
        }

        /// <summary>JToken 2 空格缩进写盘（与原子表重推/迁移工具写盘风格一致）。</summary>
        private static void WriteJTokenIndented(string path, Newtonsoft.Json.Linq.JToken token)
        {
            var sb = new System.Text.StringBuilder();
            using (var tw = new StringWriter(sb))
            using (var jw = new Newtonsoft.Json.JsonTextWriter(tw)
                   { Formatting = Newtonsoft.Json.Formatting.Indented, IndentChar = ' ', Indentation = 2 })
                token.WriteTo(jw);
            File.WriteAllText(path, sb.ToString() + "\n");
        }

        // ======================================== 预组+教学副表重推导（2026-11-01 用户定案） ========================================

        /// <summary>原子行跨表迁移台账（旧 id → 现行 id；一次性历史映射）。现行表内 DisplayName 换号由
        /// RegenAtomicTableIds 自动收口；跨表迁行旧 id 在现行表无迹可循、工具无法自推，发现孤儿引用时
        /// 逐条记入本台账接回。2026-10-31 指示物族迁表：指示物原子自一般效果表迁原子表_指示物并按新
        /// 文案重铸 ID——正式表当年已跟，教学侧因正式表恒空门闸（2026-11-01 才修）漏跟成 13 处悬空
        ///（蓝控 8 槽位/绿慢 10 槽位/进阶教学 1 张，装载端静默剔原子即内容缺失）。</summary>
        private static readonly Dictionary<string, string> AtomMigrationLedger = new Dictionary<string, string>
        {
            { "bf3f8fb9", "c770c18f" }, // 失效
            { "655a7e5b", "2addfe0c" }, // 潜行
            { "a948c9f3", "973884b1" }, // 冻结
            { "085e4cfc", "973884b1" }, // 冻结（2026-11-02 纯横置改版临时号→同日回退指示物版）
            { "cd8bfa7e", "d3d792bb" }, // 生命值减少
            { "ff8922af", "9ff1954d" }, // 护甲
            { "6d003e66", "38c8c978" }, // 沉睡
            { "23e6f45e", "3d68a8bf" }, // 毒素
        };

        /// <summary>
        /// 预组与教学副表纳入重推导+改写（旧 2026-10-20「教学只跟原子 id、不重推费用/ID」口径作废）。
        /// 由 Tools/重推导测试卡表费用与ID 统一入口调用，须在 RegenAtomicTableIds + RemapTutorialAtomRefs
        /// 之后（原子 refId 已换到位——效果/卡 id 依赖链自底向上）：
        /// ① TutorialEffects 迁移台账自愈（跨表迁行孤儿引用接回现行行）→ 悬空守卫（同正式池：中止
        ///   不落盘——防装载端剔原子后清档重写坐实丢内容）→ 逐条 id 重推（Save 内 HashEffect 单源）
        ///   + 锚价快照刷新（清档重写防旧 id 条目残留）；
        /// ② TutorialCards：effectIds 跟换新效果号 + 台账自愈 payload refId → 装载重推费用快照
        ///   （EnsureCost——费用不参与卡哈希，改费不换号）→ 卡 id 重推（"C_"+ContentHasher.HashCard
        ///   单源——内容变才变）；
        /// ③ 引用跟换：预组三件 + 教学/AI 卡组 + 测试卡组（cardIds/skillCardId/tokenCardIds/tokenBindings）
        ///   按新卡 id 重指；TutorialConfig 三件 C_ 文本引用同批跟换；CardData 目录玩家档（复制预组=
        ///   纯引用卡 id）一并重指——ID 换血后包外旧档/训练基线吊死属预期，不在本口。
        /// </summary>
        private static void RegenTutorialAndPresetTables()
        {
            string fxPath = SynergyUI.CardDataPaths.TutorialFileIn("TutorialEffects.json");
            string cardsPath = SynergyUI.CardDataPaths.TutorialFileIn("TutorialCards.json");
            if (!File.Exists(fxPath) || !File.Exists(cardsPath))
            {
                Debug.LogError($"[Regen] 教学副表缺失：{fxPath} / {cardsPath}——跳过预组+教学重推");
                return;
            }

            // ---------- ① 效果表：迁移台账自愈 → 悬空守卫 → 清档重写（Save 内重算 id/锚价快照） ----------
            var validAtomIds = new HashSet<string>(
                CardCore.Attribute.AtomicEffectTable.GetAll()
                    .Where(c => c != null && !string.IsNullOrEmpty(c.HashId))
                    .Select(c => c.HashId));
            var fxRoot = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(fxPath));
            int healedFx = HealDanglingAtomRefs(fxRoot, validAtomIds, fxPath);
            var dangling = new List<string>();
            foreach (var prop in fxRoot.Descendants().OfType<Newtonsoft.Json.Linq.JProperty>()
                     .Where(p => p.Name == "refId" && p.Value.Type == Newtonsoft.Json.Linq.JTokenType.String).ToList())
            {
                string rid = (string)prop.Value;
                if (!validAtomIds.Contains(rid)) dangling.Add(rid);
            }
            if (dangling.Count > 0)
            {
                Debug.LogError($"[Regen] TutorialEffects.json 悬空原子引用 {dangling.Count} 处——预组+教学重推中止不落盘" +
                               $"（与正式池同守卫；台账外孤儿引用请核对后记入 AtomMigrationLedger）：{string.Join("，", dangling.Distinct())}");
                return;
            }

            var graphs = SynergyUI.EffectLibrarySerializer.LoadAll(true);
            var fxRenames = new Dictionary<string, string>();
            if (graphs.Count > 0)
            {
                File.WriteAllText(fxPath, "{\"items\":[]}"); // 清档重写：防旧 id 条目残留（upsert 不删旧行）
                foreach (var g in graphs)
                {
                    if (g == null) continue;
                    string oldId = g.id;
                    SynergyUI.EffectLibrarySerializer.Save(g, true); // Save 内重算 id（HashEffect）+ 锚价快照
                    if (!string.IsNullOrEmpty(oldId) && oldId != g.id) fxRenames[oldId] = g.id;
                }
            }
            Debug.Log($"[Regen] 教学效果表 id 重推：{graphs.Count} 条（换号 {fxRenames.Count} 条）→ {fxPath}");
            CardCore.EffectsLibrary.Reload(); // 新效果号生效：卡表 effectIds 解析/费用推导按新库

            // ---------- ② 卡表：台账自愈 payload refId + effectIds 跟换 → 装载 → 费用快照 + 卡 id 重推 ----------
            var cardsRoot = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(cardsPath));
            HealDanglingAtomRefs(cardsRoot, validAtomIds, cardsPath); // payload.refId 跨表迁移孤儿接回
            var cardsArr = cardsRoot["cards"] as Newtonsoft.Json.Linq.JArray;
            int idsRemapped = 0;
            if (cardsArr != null)
                foreach (var card in cardsArr.OfType<Newtonsoft.Json.Linq.JObject>())
                {
                    var idsArr = card["effectIds"] as Newtonsoft.Json.Linq.JArray;
                    if (idsArr == null) continue;
                    for (int i = 0; i < idsArr.Count; i++)
                    {
                        if (idsArr[i].Type != Newtonsoft.Json.Linq.JTokenType.String) continue;
                        if (fxRenames.TryGetValue((string)idsArr[i], out var ne)) { idsArr[i] = ne; idsRemapped++; }
                    }
                }
            if (idsRemapped > 0)
                WriteJTokenIndented(cardsPath, cardsRoot); // 先落 effectIds 跟换再装载（解析按新效果号）

            var loaded = CardLoader.LoadCardsFromText(File.ReadAllText(cardsPath));
            if (loaded == null || loaded.Count == 0)
            {
                Debug.LogError($"[Regen] 教学卡表装载为空/失败（effectIds 跟换后解析断裂？）——中止，卡表未换号");
                return;
            }
            if (cardsArr != null && loaded.Count != cardsArr.Count)
                Debug.LogWarning($"[Regen] 教学卡表装载 {loaded.Count} 张 ≠ 文件 {cardsArr.Count} 条（坏行被剔除？）——按装载序回写，请人工核对");
            if (HasDuplicateCardIds(loaded, "教学")) return;

            var cardRenames = new Dictionary<string, string>();
            int costRewritten = 0;
            int cardDrift = 0;
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
                    Debug.Log($"[Regen] {obj["cardName"]} S={r.S} K={r.K} E={r.EAnchor} D={r.DerivedTotal} 档位Ĉ={r.DerivedTier}：{costStr}");
                obj["costList"] = new Newtonsoft.Json.Linq.JArray(
                    card.Cost[CardCore.ManaType.Gray], card.Cost[CardCore.ManaType.Red],
                    card.Cost[CardCore.ManaType.Blue], card.Cost[CardCore.ManaType.Green]);
                costRewritten++;
                string oldId = card.ID;
                string nid = "C_" + SynergyUI.ContentHasher.HashCard(card);
                // 2026-11-05 ID 冻结：内容变只刷 hash 列，不换号（换号会打断卡组/存档/训练标签引用）。
                obj["hash"] = nid.Substring(2);
                if (string.IsNullOrEmpty(oldId))
                {
                    obj["id"] = nid; // 只有无 id 的新行才分配
                }
                else if (oldId != nid)
                {
                    cardDrift++;
                }
            }
            WriteJTokenIndented(cardsPath, cardsRoot);
            Debug.Log($"[Regen] 教学卡表：费用快照重写 {costRewritten} 张（id 冻结·内容漂移 {cardDrift} 张·effectIds 跟换 {idsRemapped} 条）→ {cardsPath}");

            // ---------- ③ 引用跟换：卡组 json + TutorialConfig + CardData 玩家档 ----------
            if (cardRenames.Count > 0)
            {
                // 预组三件（名=文件名——补 .json 后缀：2026-11-01 修复，裸名 File.Exists 恒 false，
                // 三预组 skillCardId/cardIds 曾被静默跳过不跟换）
                var deckFiles = new List<string>(
                    SynergyUI.CardDataPaths.PresetDeckNames.Select(n => n + ".json"));
                deckFiles.AddRange(new[] { "basics.json", "basics_ai.json", "jinjie.json", "jinjie_ai.json", "测试卡组.json" });
                // 教学效果表 str 段的卡 id 引用（召唤衍生物模板指向模板卡——值=卡 id，RemapDeckCardRefs
                // 的全字符串字段扫描天然覆盖）也须跟换，防模板悬空
                RemapDeckCardRefs(fxPath, cardRenames);
                foreach (var f in deckFiles)
                {
                    string p = SynergyUI.CardDataPaths.TutorialFileIn(f);
                    if (File.Exists(p)) RemapDeckCardRefs(p, cardRenames);
                }
                try
                {
                    foreach (var p in Directory.GetFiles(SynergyUI.CardDataPaths.CardDir, "*.json"))
                    {
                        string fn = Path.GetFileName(p);
                        if (fn == "Cards.json" || fn == "Effects.json") continue;
                        RemapDeckCardRefs(p, cardRenames); // 玩家档（复制预组=纯引用）——换号防吊死
                    }
                }
                catch (System.Exception ex) { Debug.LogWarning($"[Regen] 玩家卡组档扫描失败（跳过）：{ex.Message}"); }
                for (int ci = 1; ci <= 3; ci++)
                {
                    string cfgPath = Path.Combine(Application.dataPath, "Configs",
                        ci == 1 ? "TutorialConfig.json" : $"TutorialConfig{ci}.json");
                    if (!File.Exists(cfgPath)) continue;
                    string text = File.ReadAllText(cfgPath);
                    string orig = text;
                    foreach (var kv in cardRenames) text = text.Replace(kv.Key, kv.Value);
                    if (text != orig)
                    {
                        File.WriteAllText(cfgPath, text);
                        Debug.Log($"[Regen] TutorialConfig 卡 id 跟换 → {cfgPath}");
                    }
                }
            }
            Debug.Log($"[Regen] 预组+教学重推完成：效果换号 {fxRenames.Count} · 卡换号 {cardRenames.Count}" +
                      (cardRenames.Count > 0 ? "（卡组/TutorialConfig/玩家档引用已跟换）" : "（卡 id 无漂移——卡组无需跟换）"));
        }

    }
}
