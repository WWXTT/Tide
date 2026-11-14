using System.Collections.Generic;

namespace CardCore.Attribute
{
    /// <summary>规范色位（六轴落四色：信息/节奏=蓝、资源/生存=绿、规则=灰、场面=红；
    /// README 第二步多成分拆色 = Flags 组合（吸取=伤害+回复=红|绿）。
    /// 2026-11-10 随双轨计价从 CardPipelineVerifier.Regen 搬迁公开（原审计私有）。</summary>
    [System.Flags]
    public enum SpecColor { None = 0, Gray = 1, Red = 2, Blue = 4, Green = 8 }

    /// <summary>
    /// 原子规范色单源（2026-11-10 双轨计价定案：自 CardPipelineVerifier.Regen 搬迁公开）——
    /// 原为颜色审计私有字典，现升级为 Tier 写法的颜色生成器：原子表 Tier 行解析期展开
    /// ManaList = For(effectType, enumName) 色 × PriceOf(tier) 价（AtomicEffectTable.BuildConfig 单口）。
    /// 三步走轴映射（改变场面数字=红 / 资源数量=绿 / 行动许可·信息=蓝 / 规则·属性定义=灰，
    /// 先命中先定色）+ 逐原子特例（SpecColorOf）+ 行级定案覆盖（SpecRowOverrides，键=中文短名，
    /// 全表生效——光环表 EffectType 全同，行级判色唯一落点）+ 豁免（SpecColorExempt：费用增减
    /// 四色行已按四色处理，不参与生成）。
    /// **2026-11-14 基础属性归灰定案（优先于上面的轴映射）**：攻击力/生命值/耐久三类**数值**
    ///（含永久档、角色攻击力、攻守互换）统一灰——0.5 灰 = 1 点数值（与 StatUnit=2 /
    /// StatAnchor=0.5 / DurabilityUnitCost=0.5 / AtomTier.Dm1_25=0.5 同口径）。
    /// 改色定案流程不变：本表仍是定案记录处；表侧直写行由人按定案改（段 2/3 迁移），
    /// RunColorAudit 对账「生成值 vs 显式值」不等即 Fail（2026-11-10 收尾起零容忍，无白名单）。
    /// </summary>
    public static class AtomColorRules
    {
        /// <summary>EffectType → 应得色，**硬编码**（README 落地条款：不新开配置表）。
        /// **作为 Tier 生成器**：Tier 行的色即由本表生成；多色 Flags（吸取红|绿）不可走 Tier——
        /// Tier 展开要求单色，双色行直写 ManaList。
        /// 系统表不计价行（引擎主干/门槛/Attack 族，ManaList null 或全零）装载后不参与比对，
        /// 下列对应条目仅为行将来计价时预留的口径。</summary>
        public static readonly Dictionary<CardCore.AtomicEffectType, SpecColor> SpecColorOf =
            new Dictionary<CardCore.AtomicEffectType, SpecColor>
        {
            // ---- 场面（红）：改变场上数字（存亡/移场；攻/血/耐久自 2026-11-14 改归灰）----
            { CardCore.AtomicEffectType.DealDamage, SpecColor.Red },
            { CardCore.AtomicEffectType.LifeLoss, SpecColor.Red },                    // 流失=扣上限直伤（伤害族；表 2026-11-05 回红定案，11-03 绿口径作废）
            { CardCore.AtomicEffectType.DrainLife, SpecColor.Red | SpecColor.Green }, // 特例：吸取=伤害(红)+回复(绿) 双色
            { CardCore.AtomicEffectType.AddHeroAttack, SpecColor.Gray },              // 2026-11-14 基础属性归灰：角色攻击力=灰（原红）
            { CardCore.AtomicEffectType.Devour, SpecColor.Red },                      // 消灭=红
            { CardCore.AtomicEffectType.Smash, SpecColor.Red },                       // 摧毁=场面移除
            { CardCore.AtomicEffectType.Sacrifice, SpecColor.Red },
            { CardCore.AtomicEffectType.Exile, SpecColor.Red },                       // 特例：除外=红（从场上移除=场面）
            { CardCore.AtomicEffectType.ReturnToHand, SpecColor.Red },                // 弹回=从场上移除（除外同口径；表 2026-11-05 回红定案，11-04 蓝口径作废）
            { CardCore.AtomicEffectType.BounceToTop, SpecColor.Red },
            { CardCore.AtomicEffectType.BounceToBottom, SpecColor.Red },
            { CardCore.AtomicEffectType.GainControl, SpecColor.Red },
            { CardCore.AtomicEffectType.ChangeOwner, SpecColor.Red },
            { CardCore.AtomicEffectType.AddPowerUp, SpecColor.Gray },                  // 2026-11-14 基础属性归灰：攻击力增加=灰（原红）
            { CardCore.AtomicEffectType.AddPowerDown, SpecColor.Gray },                // 2026-11-14 基础属性归灰：攻击力减少=灰（原红）
            { CardCore.AtomicEffectType.AddLifeDown, SpecColor.Gray },                 // 2026-11-14 基础属性归灰：生命值减少=灰（2026-11-02「生命族全绿」作废）
            { CardCore.AtomicEffectType.AddPermanentPowerUp, SpecColor.Gray },         // 2026-11-14 基础属性归灰：永久攻=灰（原红；永久档单价仍 1 灰/点）
            { CardCore.AtomicEffectType.AddPermanentPowerDown, SpecColor.Gray },       // 2026-11-14 基础属性归灰：永久攻=灰
            { CardCore.AtomicEffectType.AddPermanentLifeUp, SpecColor.Gray },          // 2026-11-14 基础属性归灰：永久血=灰（原绿）
            { CardCore.AtomicEffectType.AddPermanentLifeDown, SpecColor.Gray },        // 2026-11-14 基础属性归灰：永久血=灰
            { CardCore.AtomicEffectType.Purify, SpecColor.Red },                       // 驱散=全剥关键词/指示物（削场面）
            { CardCore.AtomicEffectType.AddToxin, SpecColor.Red },                     // 毒素=回合末造伤（伤害族）
            { CardCore.AtomicEffectType.GrantRetaliation, SpecColor.Red },             // 报复（旧枚举名；行 2026-11-09 随荆棘改名换 GrantRetaliate，本行保留防旧引用）
            { CardCore.AtomicEffectType.GrantRetaliate, SpecColor.Red },               // 报复=反噬伤害（2026-11-10 搬迁补录：荆棘改名时字典漏跟）
            { CardCore.AtomicEffectType.GrantCrit, SpecColor.Red },                    // 暴击=战斗场面族（2026-11-10 搬迁补录：此前未收录）
            { CardCore.AtomicEffectType.GrantVenom, SpecColor.Red },                   // 特例：剧毒=红
            { CardCore.AtomicEffectType.GrantPathogen, SpecColor.Red },                // 病原体=毒素族
            { CardCore.AtomicEffectType.GrantOverwhelm, SpecColor.Red },               // 碾压=战斗场面
            { CardCore.AtomicEffectType.GrantSplash, SpecColor.Red },                  // 溅射=战斗场面
            { CardCore.AtomicEffectType.GrantFirstStrike, SpecColor.Red },             // 先攻=战斗顺序（场面）
            { CardCore.AtomicEffectType.GrantDoubleStrike, SpecColor.Red },            // 连击=战斗场面
            { CardCore.AtomicEffectType.GrantHunt, SpecColor.Red },                    // 狩猎=回合末攻击（场面）
            { CardCore.AtomicEffectType.AddVulnerable, SpecColor.Red },                // 特例：易损=红
            { CardCore.AtomicEffectType.BurstDamage, SpecColor.Red },
            { CardCore.AtomicEffectType.ShieldSlam, SpecColor.Red },                   // 护盾猛击=护甲转伤害（伤害产出族）
            { CardCore.AtomicEffectType.MoveUnit, SpecColor.Red },                     // 移动=场面（位置族；行级定案灰见 SpecRowOverrides）
            { CardCore.AtomicEffectType.Attack, SpecColor.Gray },                      // 系统行（预留；2026-11-14 攻击族归灰）
            // ---- 资源/生存（绿）：手牌/牌库/墓地/元素池/生命总量/减伤/防护 ----
            { CardCore.AtomicEffectType.Heal, SpecColor.Green },
            { CardCore.AtomicEffectType.DrawCard, SpecColor.Green },                   // 特例：抽卡=绿（行级定案蓝见 SpecRowOverrides）
            { CardCore.AtomicEffectType.DiscardCard, SpecColor.Green },                // 特例：弃牌=绿（行级定案蓝见 SpecRowOverrides）
            { CardCore.AtomicEffectType.MillCard, SpecColor.Green },                   // 特例：磨牌=绿（行级定案蓝见 SpecRowOverrides）
            { CardCore.AtomicEffectType.RecoverToHand, SpecColor.Green },              // 特例：回收=绿
            { CardCore.AtomicEffectType.ShuffleIntoDeck, SpecColor.Green },            // 特例：洗回=绿
            { CardCore.AtomicEffectType.RecoverFromExile, SpecColor.Green },           // 特例：除外回收=绿
            // 元素合成（GainAnyElement）2026-11-09 整链删除——教学二改挂牺牲，零消费者
            { CardCore.AtomicEffectType.ReturnFromGraveyard, SpecColor.Green },        // 苏生=墓地资源回流
            { CardCore.AtomicEffectType.IncreaseLandCap, SpecColor.Green },            // 成长=资源（地牌槽）
            { CardCore.AtomicEffectType.RemoveDebuffs, SpecColor.Green },              // 净化=解除负面（2026-10-28 P2a 先例）
            { CardCore.AtomicEffectType.AddLifeUp, SpecColor.Gray },                   // 2026-11-14 基础属性归灰：生命值增加=灰（回复 Heal 仍绿）
            { CardCore.AtomicEffectType.AddArmor, SpecColor.Green },                   // 护甲=绿（生存轴，非基础属性）
            { CardCore.AtomicEffectType.GrantToughness, SpecColor.Green },             // 坚韧=减伤=生存（减半同口径）
            { CardCore.AtomicEffectType.ProtectionHalve, SpecColor.Gray },             // 减半指示物=伤害规则改写（2026-11-10 搬迁补录：此前 EffectType 未收录，行级「减半」→灰定案随本行入字典）
            { CardCore.AtomicEffectType.GrantProtection, SpecColor.Gray },             // 特例：减半=伤害规则改写（2026-11-05 定案灰；旧绿/蓝作废）
            { CardCore.AtomicEffectType.GrantReborn, SpecColor.Green },                // 复生=生存
            { CardCore.AtomicEffectType.GrantDivineShield, SpecColor.Green },          // 圣盾=生存
            { CardCore.AtomicEffectType.GrantGuardian, SpecColor.Green },              // 守护=拦截保护
            { CardCore.AtomicEffectType.GrantIndestructible, SpecColor.Green },        // 不灭=生存
            { CardCore.AtomicEffectType.GrantRegeneration, SpecColor.Green },          // 再生=生存
            { CardCore.AtomicEffectType.LivingArmor, SpecColor.Green },                // 活体护甲=护甲族
            { CardCore.AtomicEffectType.Guard, SpecColor.Green },                      // 系统行（预留）
            { CardCore.AtomicEffectType.SetLife, SpecColor.Gray },                     // 系统行（预留；2026-11-14 生命值族归灰）
            // ---- 信息/节奏（蓝）：行动许可·能不能被指定·看到什么·反制打断 ----
            { CardCore.AtomicEffectType.SearchDeck, SpecColor.Blue },                  // 特例：检索=蓝
            { CardCore.AtomicEffectType.LookAtTopCards, SpecColor.Blue },              // 特例：占卜=蓝
            { CardCore.AtomicEffectType.DiscoverCard, SpecColor.Blue },                // 发现=检索族（信息）
            { CardCore.AtomicEffectType.RevealByDimension, SpecColor.Blue },           // 筛选展示=信息
            { CardCore.AtomicEffectType.RevealCard, SpecColor.Blue },                  // 展示=信息
            { CardCore.AtomicEffectType.DeclareHand, SpecColor.Blue },                 // 特例：宣言（强夺）=蓝
            { CardCore.AtomicEffectType.DeclareDeck, SpecColor.Blue },                 // 特例：宣言（窃取）=蓝
            { CardCore.AtomicEffectType.DeclareDeckTop, SpecColor.Blue },              // 特例：宣言/预言（禁言）=蓝
            { CardCore.AtomicEffectType.ProphecyNextCard, SpecColor.Blue },            // 特例：预言=蓝（引擎行·预留）
            { CardCore.AtomicEffectType.Untap, SpecColor.Blue },                       // 激励=解除横置（行动许可；行级定案红见 SpecRowOverrides）
            { CardCore.AtomicEffectType.LockCard, SpecColor.Blue },                    // 锁定=无法使用（行动许可）
            { CardCore.AtomicEffectType.NegateActivation, SpecColor.Blue },            // 反制打断=节奏
            { CardCore.AtomicEffectType.NegateEffect, SpecColor.Blue },
            { CardCore.AtomicEffectType.TakeExtraTurn, SpecColor.Blue },               // 额外回合=节奏
            { CardCore.AtomicEffectType.SkipTurn, SpecColor.Blue },                    // 跳过阶段/回合=节奏（四相位档同）
            { CardCore.AtomicEffectType.GrantStealth, SpecColor.Blue },                // 特例：潜行=蓝
            { CardCore.AtomicEffectType.GrantConcealed, SpecColor.Blue },              // 隐密=潜行持续版
            { CardCore.AtomicEffectType.Silence, SpecColor.Blue },
            { CardCore.AtomicEffectType.AddNullify, SpecColor.Blue },                  // 特例：失效=蓝
            { CardCore.AtomicEffectType.Freeze, SpecColor.Blue },                      // 特例：冻结=蓝
            { CardCore.AtomicEffectType.RushSickness, SpecColor.Blue },                // 特例：紊乱=蓝
            { CardCore.AtomicEffectType.GrantDepravity, SpecColor.Green },             // 放血=反疗封锁=生存轴对侧（2026-11-05 定案绿；旧蓝作废）
            { CardCore.AtomicEffectType.GrantReplay, SpecColor.Blue },                 // 重放=节奏（重复施放）
            { CardCore.AtomicEffectType.Sleep, SpecColor.Blue },                       // 沉睡=行动封锁
            { CardCore.AtomicEffectType.GrantSpellShield, SpecColor.Blue },            // 法术护盾=反制
            { CardCore.AtomicEffectType.GrantVigilance, SpecColor.Blue },              // 警戒=攻击不横置（行动许可）
            { CardCore.AtomicEffectType.GrantDisarm, SpecColor.Blue },                 // 缴械=压制反击（行动许可）
            { CardCore.AtomicEffectType.GrantTaunt, SpecColor.Blue },                  // 帷幕=指定限制（能否被指定）
            { CardCore.AtomicEffectType.AddDurabilityUp, SpecColor.Gray },             // 2026-11-14 基础属性归灰：耐久=灰（2026-11-03「耐久=蓝」作废；0.5 灰/点）
            { CardCore.AtomicEffectType.SealUnit, SpecColor.Red },                     // 封印=场面移除（2026-11-05 确认定案红；11-03 蓝3 口径作废）
            { CardCore.AtomicEffectType.SwapPowerLife, SpecColor.Gray },               // 2026-11-14 基础属性归灰：攻守互换=灰（原 2026-11-09 蓝2 作废——属性重排归基础属性轴）
            // ---- 场面/生存双成分（红|绿）----
            { CardCore.AtomicEffectType.GrantLifesteal, SpecColor.Red },                  // 吸血=红（2026-11-03 用户定案·不随吸取双色同构；表侧行待段3颜色迁移）
            { CardCore.AtomicEffectType.GrantLifelink, SpecColor.Red | SpecColor.Green },   // 系命=伤害+回复 双色（行级定案绿见 SpecRowOverrides）
            // ---- 规则/工具（灰）：规则改写·属性定义·纯工具 ----
            { CardCore.AtomicEffectType.ModifyGameRule, SpecColor.Gray },              // 特例：各仪典/各禁令=灰（行级细化见 SpecRowOverrides）
            { CardCore.AtomicEffectType.Erase, SpecColor.Gray },                       // 特例：通用抹除=灰
            { CardCore.AtomicEffectType.GrantSpellban, SpecColor.Gray },               // 特例：无实体=灰（伤害规则改写；表 2026-11-05 确认回灰，11-03 蓝5 作废）
            { CardCore.AtomicEffectType.GrantInsulator, SpecColor.Gray },              // 绝缘体=拦获得（规则改写，无实体同口径）
            { CardCore.AtomicEffectType.SummonToken, SpecColor.Gray },                 // 第三步：衍生物=纯工具
            { CardCore.AtomicEffectType.EchoCopy, SpecColor.Blue },                   // 回响=复制重演=节奏（2026-11-05 定案蓝；旧灰「纯工具」作废）
            { CardCore.AtomicEffectType.GrantTemporary, SpecColor.Gray },              // 遗忘=消失标记（纯工具）
            { CardCore.AtomicEffectType.SetPower, SpecColor.Gray },                    // 系统行（预留）
            { CardCore.AtomicEffectType.SetCost, SpecColor.Gray },                     // 系统行（预留）
            { CardCore.AtomicEffectType.DeclareVictory, SpecColor.Gray },              // 系统行（预留）
            { CardCore.AtomicEffectType.Morph, SpecColor.Gray },                       // 模拟=纯工具（动态费用行·预留）
            { CardCore.AtomicEffectType.GrantMorph, SpecColor.Gray },                  // 变形=纯工具（动态费用行·预留）
            //（Charge 充能条目已随 2026-11-05 充能系统动作化改版删除——表行已删，充能不再是原子效果）
            { CardCore.AtomicEffectType.SituationGate, SpecColor.Gray },               // 门槛行=引擎条件（预留）
            { CardCore.AtomicEffectType.EngineCountdown, SpecColor.Gray },             // 引擎主干族（均预留）
            { CardCore.AtomicEffectType.EngineLuckRoll, SpecColor.Gray },
            { CardCore.AtomicEffectType.EngineClash, SpecColor.Gray },
            { CardCore.AtomicEffectType.EngineDeathToll, SpecColor.Gray },
            { CardCore.AtomicEffectType.EngineManaSurplus, SpecColor.Gray },
            { CardCore.AtomicEffectType.EngineUnity, SpecColor.Gray },
            { CardCore.AtomicEffectType.EngineCurseOnDraw, SpecColor.Gray },
            { CardCore.AtomicEffectType.EngineBlessingOnDraw, SpecColor.Gray },
        };

        /// <summary>按原子行覆盖（键=表行中文短名 EnumName 列——进表指纹=身份稳定；**全表生效**，
        /// 不限 ModifyGameRule——光环表 EffectType 全同 ModifyGameRule，EffectType 映射不够用，行级
        /// 定案只能落这里；键=短名跨表须唯一）：2026-11-02 用户定案批次，优先于 SpecColorOf；
        /// 未列行沿用 EffectType 映射。</summary>
        public static readonly Dictionary<string, SpecColor> SpecRowOverrides =
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
        ///（2026-11-02 用户裁定：每行只动对应色，不动）——不生成、不比对，恒放行。</summary>
        public static readonly HashSet<CardCore.AtomicEffectType> SpecColorExempt =
            new HashSet<CardCore.AtomicEffectType>
        {
            CardCore.AtomicEffectType.AddCostUpGray, CardCore.AtomicEffectType.AddCostUpRed,
            CardCore.AtomicEffectType.AddCostUpBlue, CardCore.AtomicEffectType.AddCostUpGreen,
            CardCore.AtomicEffectType.AddCostDownGray, CardCore.AtomicEffectType.AddCostDownRed,
            CardCore.AtomicEffectType.AddCostDownBlue, CardCore.AtomicEffectType.AddCostDownGreen,
        };

        /// <summary>段 3 待迁移白名单 SpecColorPending 已删除（2026-11-10 收尾：颜色迁移
        /// B/C 组落地——10 行填 Tier 时已是红、关键词 6 行已红、吸血落位红 3，候补清零）——
        /// 全表「显式色≠生成色」零容忍，无过渡豁免口；如再需分批迁移，临时白名单须重新定案登记。</summary>

        /// <summary>行 → 生成色：行级覆盖（SpecRowOverrides）优先，其次 EffectType 映射
        ///（SpecColorOf）。未收录 / 豁免（费用增减四色行）返回 None——Tier 展开侧 None=拒
        ///（不可生成色的行请直写 ManaList），审计侧 None=未收录报缺行。</summary>
        public static SpecColor For(CardCore.AtomicEffectType type, string enumName)
        {
            if (SpecColorExempt.Contains(type)) return SpecColor.None;
            if (!SpecColorOf.TryGetValue(type, out var color)) return SpecColor.None;
            if (!string.IsNullOrEmpty(enumName) && SpecRowOverrides.TryGetValue(enumName, out var row))
                color = row; // 行级定案覆盖（全表生效——光环族 EffectType 全同，行级判色唯一落点）
            return color;
        }

        /// <summary>生成色是否单色（Tier 展开要求单色）：单色 out 对应 ManaType 并 true；
        /// 多色（吸取 红|绿 等双色行）/None → false——此类行直写 ManaList，不走 Tier。</summary>
        public static bool TryGetSingleColor(SpecColor color, out CardCore.ManaType mana)
        {
            mana = CardCore.ManaType.Gray;
            switch (color)
            {
                case SpecColor.Gray: mana = CardCore.ManaType.Gray; return true;
                case SpecColor.Red: mana = CardCore.ManaType.Red; return true;
                case SpecColor.Blue: mana = CardCore.ManaType.Blue; return true;
                case SpecColor.Green: mana = CardCore.ManaType.Green; return true;
                default: return false; // None（未收录/豁免）或多色组合
            }
        }

        // ==== 换算工具（2026-11-10 随三件套自 Regen 搬迁——审计侧 Regen 与验证器 P9-8 共用单源） ====

        /// <summary>ManaType → 规范色位（Any 不进卡费= None）。</summary>
        public static SpecColor SpecOf(CardCore.ManaType m)
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

        /// <summary>费用构成 → 规范色位集（非零色并集；任意不计入——不进卡费）。</summary>
        public static SpecColor FlagsOf(CardCore.ElementCost c)
        {
            var f = SpecColor.None;
            if (c == null) return f;
            foreach (var m in c.NonzeroColors())
                f |= SpecOf(m);
            return f;
        }

        /// <summary>色集中文（灰+红+蓝+绿 组合；None=无）——审计点名/P9-8 断言消息共用。</summary>
        public static string ColorZh(SpecColor c)
        {
            if (c == SpecColor.None) return "无";
            var parts = new List<string>();
            if ((c & SpecColor.Gray) != 0) parts.Add("灰");
            if ((c & SpecColor.Red) != 0) parts.Add("红");
            if ((c & SpecColor.Blue) != 0) parts.Add("蓝");
            if ((c & SpecColor.Green) != 0) parts.Add("绿");
            return string.Join("+", parts);
        }
    }
}
