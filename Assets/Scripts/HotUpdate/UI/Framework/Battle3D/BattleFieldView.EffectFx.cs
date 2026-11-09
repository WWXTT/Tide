using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using Cysharp.Threading.Tasks;
using GameBoard;
using MonsterRig;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SynergyUI
{
    public sealed partial class BattleFieldView
    {
        // ======================================== 效果命中演出（2026-10-19 Arctibyte 接入：弹体/爆点/浮字） ========================================

        /// <summary>效果目标解析结果（伤害/回复/指示物浮字共用）：单位（生物/结界）或角色立柱。</summary>
        private sealed class EffectTarget
        {
            public UnitVisual Unit;      // 单位目标（null=角色目标）
            public int HeroOwner = -1;   // 角色目标归属（-1=非角色）
            public Vector3 ImpactAnchor; // 爆点/弹体落点（单位=胸口注视高，角色=立柱中段）
            public Transform FloatParent;// 浮字挂载父（wrapper / 立柱根）
            public float FloatHeight;    // 浮字本地高度（头顶数值之上）
        }

        /// <summary>解析效果目标（Card→场上单位；Player→角色立柱）。不在视图（教学介绍段
        /// SuppressUnits/已退场/未建柱）返回 null——调用方静默跳过，绝不报错。</summary>
        private EffectTarget ResolveEffectTarget(Entity target, Func<Player, int> ownerOf)
        {
            if (target is Card tc)
            {
                if (!_units.TryGetValue(tc.RuntimeId, out var uv) || uv.Wrapper == null) return null;
                var et = new EffectTarget { Unit = uv, FloatParent = uv.Wrapper };
                et.FloatHeight = uv.StatsHeightY + 0.55f; // 头顶锚高之上（2026-10-26 迁格后按锚高推算，不再取 stats 件位）
                et.ImpactAnchor = uv.Wrapper.position + Vector3.up * uv.LookHeight; // 胸口（同注视点口径）
                return et;
            }
            var player = target as Player;
            if (player == null || ownerOf == null) return null;
            int owner = ownerOf(player);
            if (!_heroes.TryGetValue(owner, out var hv) || hv.Root == null) return null;
            return new EffectTarget
            {
                HeroOwner = owner,
                FloatParent = hv.Root,
                FloatHeight = 1.9f, // 立柱 stats（1.3）之上的高位浮字（同疲劳口径）
                ImpactAnchor = hv.Root.position + Vector3.up * 1.1f,
            };
        }

        /// <summary>解析效果源锚点（弹体出发点，2026-10-19 用户定案：**法术源头=施法者角色**）：
        /// 来源是场上单位（生物主动/触发效果）→ 单位胸口；否则（施放中的法术/手牌来源）→
        /// 施法者（Controller/Owner）角色立柱。解析失败返回 null——调用方跳过飞行段直落爆点。</summary>
        private Vector3? ResolveEffectSourceAnchor(Entity source, Func<Player, int> ownerOf)
        {
            Player caster = source as Player;
            if (caster == null && source is Card sc)
            {
                if (_units.TryGetValue(sc.RuntimeId, out var v) && v.Wrapper != null && !v.Dead)
                    return v.Wrapper.position + Vector3.up * v.LookHeight; // 单位来源效果：从单位胸口发出
                caster = sc.GetController() ?? sc.GetOwner(); // 非场上来源=施法者角色
            }
            if (caster == null || ownerOf == null) return null;
            int owner = ownerOf(caster);
            return _heroes.TryGetValue(owner, out var hv) && hv.Root != null
                ? hv.Root.position + Vector3.up * 1.1f
                : (Vector3?)null;
        }

        /// <summary>效果演出色（2026-10-19 六色复用定案）：来源卡施放费用的红/蓝/绿主色
        /// （同出场粒子 DominantChromatic 口径——非彩或无卡来源=灰）。费用单源=GameActions.GetCardCost。</summary>
        private static Color DominantCostColor(Entity source)
            => BattleCastArrival.ManaColor(DominantCostMana(source));

        /// <summary>DominantCostColor 的 ManaType 版（2026-10-23 VFX 路由接入）：主色循环同源拷贝——
        /// BattleVfxFx 按 ManaType 查 BattleVfxCatalog（红蓝绿主色/无卡或无彩=Gray）。</summary>
        private static ManaType DominantCostMana(Entity source)
        {
            if (source is Card sc)
            {
                var cost = GameActions.GetCardCost(sc);
                ManaType best = ManaType.Gray;
                float bestV = 0f;
                for (var m = ManaType.Red; m <= ManaType.Green; m++)
                {
                    float v = cost[m];
                    if (v > bestV) { bestV = v; best = m; }
                }
                return best;
            }
            return ManaType.Gray;
        }

        /// <summary>效果伤害演出（AtomicDamageEvent·IsCombatDamage=false——投影器延迟载荷随回放步
        /// 放出，非回放路径直跑）：源锚点 → MagicOrb 弹体 0.35s → Impact 爆点 + 红「-N」浮字 +
        /// 受击（生物=Hit 动画 / 角色=立柱脉冲）。信息性表演占视觉门（含排队等待全程——回放导演
        /// WaitVisualsIdleAsync 逐步等待）；目标不在视图静默返回。
        /// castCell（2026-10-20 时序定案）：法术伤害变体——**先等扣费粒子到位于法术位**，弹体改从
        /// 施放贴片上起飞（数据层来源仍是角色，表现层归位法术）。
        /// 单位串行链（2026-10-20 定案）：单位目标排队等前驱（同单位伤害/回复/碎裂）完成后开演；
        /// 角色目标无链直演。</summary>
        public async UniTask PlayEffectDamageAsync(Entity source, Entity target, int amount,
            Func<Player, int> ownerOf, (int x, int z)? castCell = null, ManaType? effectTint = null)
        {
            var et = ResolveEffectTarget(target, ownerOf);
            if (et == null) return;
            BeginVisual();
            try
            {
                if (target is Card tc)
                    await RunUnitFxAsync(tc.RuntimeId, () => DamageFxCoreAsync(et, source, amount, ownerOf, castCell, effectTint));
                else
                    await DamageFxCoreAsync(et, source, amount, ownerOf, castCell, effectTint);
            }
            finally { EndVisual(); }
        }

        /// <summary>伤害演出本体（串行链内执行）。受击判据=碎裂未开演（DeathExitStarted）：
        /// Dead 会被 HoldForEffectDeath 在死亡事件期早锁，链化排队后执行时 Dead 常 true
        /// 但碎裂排在链尾尚未开演——受击照放（受击→碎裂顺序由链保证）；角色脉冲只在真角色目标走。
        /// effectTint=效果基础色（2026-10-20 定案：按所用效果的原子行费用基准主色——事件下发；
        /// null=无基准回落来源卡费用主色 DominantCostColor）。</summary>
        private async UniTask DamageFxCoreAsync(EffectTarget et, Entity source, int amount,
            Func<Player, int> ownerOf, (int x, int z)? castCell, ManaType? effectTint)
        {
            if (castCell != null)
                await WaitCastPayArrived(ownerOf(source as Player)); // 时序①→②：费用粒子到位才起飞
            var src = castCell != null
                ? CastingAnchor(castCell.Value.x, castCell.Value.z) + Vector3.up * 0.35f // 贴片平躺地面——抬起一点起飞
                : ResolveEffectSourceAnchor(source, ownerOf);
            var mana = effectTint ?? DominantCostMana(source); // 路由色（2026-10-23 VFX 接入）：效果基准主色，无基准回落来源卡费用主色
            var tint = BattleCastArrival.ManaColor(mana);
            // 颜色路由链（Phase 1）：Cast→弹体→Hit 三节点查 BattleVfxCatalog；used=false（缺件/
            // 未配置/并发超限/异常/开关关）整链回落旧 MagicOrb+Impact——两套不同播。
            // 调用点 scale 恒 1f（2026-10-23 晚整改：entry.scale=生成器按角色归一化的绝对目标尺寸，勿双重缩放）。
            bool used = false;
            if (BattleVfxFx.Enabled && src != null)
            {
                try
                {
                    used = BattleVfxFx.TryPlayCast(mana, src.Value, 1f);
                    if (used) await BattleVfxFx.TryPlayProjectileAsync(mana, src.Value, et.ImpactAnchor, 1f);
                    if (used) BattleVfxFx.TryPlayHit(mana, et.ImpactAnchor, 1f);
                }
                catch (Exception ex) { BattleVfxFx.NoteFailure(ex); used = false; }
            }
            if (!used)
            {
                if (src != null)
                    await BattleEffectFx.PlayOrbAsync(src.Value, et.ImpactAnchor, tint); // 飞行段（缺件=立即返回直落爆点）
                BattleEffectFx.PlayImpact(et.ImpactAnchor, tint);
            }
            if (et.Unit != null && !et.Unit.DeathExitStarted)
                et.Unit.Anim?.Play(MonsterAction.Hit); // 受击（一次性压扁回弹，叠在待机上）；碎裂退场排在链尾——受击先放
            else if (et.Unit == null)
                HeroImpactPulseAsync(et.HeroOwner).Forget(); // 角色立柱脉冲（自带门配对）
#if UNITY_EDITOR
            Debug.Log($"[EffectFx] 伤害演出 amount={amount} 目标单位{(et.Unit != null ? "" : "=无（角色）")}"
                      + $" 路由={(used ? "VFX三节点" : "旧链")} 弹体={(src != null ? 1 : 0)}"
                      + $"{(castCell != null ? $" 法术位({castCell.Value.x},{castCell.Value.z})" : "")}"); // 演出取证口（构建不打）
#endif
            await FloatTextAsync(et.FloatParent, et.FloatHeight, "-" + amount, StatDownColor);
        }

        /// <summary>回复演出（HealEvent）：绿火花（Dissolve Template 复用，脚下涌起）+ 绿「+N」浮字。
        /// amount 为扣溢后实效（发布方 Amount-Overfill，屏侧算好传入）；占视觉门；单位目标入串行链
        ///（2026-10-20 定案：同一单位串行——回复不与伤害/碎裂抢拍）。</summary>
        public async UniTask PlayEffectHealAsync(Entity target, int amount, Func<Player, int> ownerOf)
        {
            // 取证口（2026-10-21 祝福+20无绿字排查）：演出链入口——amount≤0 早退/目标解析失败/根本未被调用三分支一翻便知
            TideLog.Info($"[HealFx] 回复演出入口：target={(target is Player ? "角色" : (target as Card)?.ID ?? "null")} amount={amount}");
            if (amount <= 0) return;
            var et = ResolveEffectTarget(target, ownerOf);
            if (et == null) return;
            BeginVisual();
            try
            {
                if (target is Card tc)
                    await RunUnitFxAsync(tc.RuntimeId, () => HealFxCoreAsync(et, amount));
                else
                    await HealFxCoreAsync(et, amount);
            }
            finally { EndVisual(); }
        }

        private async UniTask HealFxCoreAsync(EffectTarget et, int amount)
        {
            Vector3 ground = et.Unit != null && et.Unit.Wrapper != null
                ? et.Unit.Wrapper.position
                : et.FloatParent != null ? et.FloatParent.position : et.ImpactAnchor;
            // 回复节点优先（Phase 1）：绿=HealBig/白=HealNova（回复事件无效果色——绿为治疗主色档）；
            // 未路由回落绿火花（Dissolve 复用）。锚同火花口径抬 SparkLift 防六棱柱地块遮挡。
            bool healed = false;
            if (BattleVfxFx.Enabled)
            {
                try { healed = BattleVfxFx.TryPlayHeal(ManaType.Green, ground + Vector3.up * SparkLift, 1f); }
                catch (Exception ex) { BattleVfxFx.NoteFailure(ex); }
            }
            if (!healed)
                BattleEffectFx.PlaySpark(ground + Vector3.up * SparkLift, SparkUpColor);
            await FloatTextAsync(et.FloatParent, et.FloatHeight, "+" + amount, StatUpColor);
        }

        /// <summary>火花色（回复/增益共用）：StatUpColor 向白提亮一档——纯绿粒子自发光观感偏暗。</summary>
        private static readonly Color SparkUpColor = Color.Lerp(new Color(0.42f, 1f, 0.45f), Color.white, 0.25f);

        /// <summary>诅咒回落火花色：黑→紫直映（同粒子色直映射口径）。</summary>
        private static readonly Color DeckMarkCurseColor = new Color(0.38f, 0.32f, 0.55f);

        /// <summary>诅咒/祝福施加到牌库的演出（2026-10-26 CounterChangedEvent Amount>0 钩子）：
        /// 卡堆顶一次性爆发+卡堆脉冲。2026-11-01 catalog 释放件重分配后恒灰查表命中——
        /// 祝福=灰 Heal（HealNova·原白新星位）/诅咒=灰 Hit（ShadowExplosion·原黑爆位），
        /// 旧观感回归；未命中仍回落 PlaySpark 兜底。目标卡在牌库**无视图
        /// 单位**：锚=BattleZoneProps.DeckTopWorldPosition（不走 ResolveEffectTarget），归属=卡
        /// Owner/Controller 经 ownerOf 映射。不占视觉门、无浮字（施加语义战报行已有）。</summary>
        public UniTask PlayDeckMarkAsync(Entity target, Func<Player, int> ownerOf, bool blessing)
        {
            if (target is Card c && ownerOf != null)
            {
                var ownerPlayer = c.GetOwner() ?? c.GetController();
                if (ownerPlayer != null)
                {
                    int owner = ownerOf(ownerPlayer);
                    if (BattleZoneProps.DeckTopWorldPosition(owner, out var top))
                    {
                        BattleZoneProps.PulseDeck(owner);
                        Vector3 at = top + Vector3.up * 0.3f; // 堆顶之上的小抬升（低于 SparkLift——堆顶已抬空）
                        var color = ManaType.Gray; // 灰档（祝福 Heal/诅咒 Hit 同灰，极性分色系）
                        bool played = false;
                        if (BattleVfxFx.Enabled)
                        {
                            try
                            {
                                played = blessing
                                    ? BattleVfxFx.TryPlayHeal(color, at, 1f)
                                    : BattleVfxFx.TryPlayHit(color, at, 1f);
                            }
                            catch (Exception ex) { BattleVfxFx.NoteFailure(ex); }
                        }
                        if (!played)
                            BattleEffectFx.PlaySpark(at, blessing ? SparkUpColor : DeckMarkCurseColor);
                    }
                }
            }
            return UniTask.CompletedTask;
        }

        /// <summary>赋予爆发粒子（KeywordEvent/CounterChangedEvent·2026-11-01「极性×颜色 8 种」定案）：
        /// 赋予原子落地在目标锚点播一次性爆发——减益=Hit 族 / 增益=Heal 族（参照旧诅咒/祝福施加
        /// 爆发的单爆点形态），颜色=原子行基准主色（effectTint）无基准回落来源卡费用主色
        ///（同伤害演出口径）。无弹体/无浮字（战报行已有）；单位目标入串行链（同伤害/回复
        /// 防同单位叠拍）；目标不在视图（手牌/牌库/墓地等）静默返回；不占视觉门。</summary>
        public async UniTask PlayGrantBurstAsync(Entity target, Entity source, Func<Player, int> ownerOf,
            ManaType? effectTint, bool debuff)
        {
            var et = ResolveEffectTarget(target, ownerOf);
            if (et == null) return;
            if (target is Card tc)
                await RunUnitFxAsync(tc.RuntimeId, () => GrantBurstCoreAsync(et, source, effectTint, debuff));
            else
                await GrantBurstCoreAsync(et, source, effectTint, debuff);
        }

        /// <summary>赋予爆发本体（串行链内执行）：8 种=极性 2（Hit/Heal）×颜色 4（灰红蓝绿）——
        /// catalog 2026-11-01 重分配后四色 Heal/Hit 全配位；未命中回落效果色 Spark。</summary>
        private UniTask GrantBurstCoreAsync(EffectTarget et, Entity source, ManaType? effectTint, bool debuff)
        {
            var mana = effectTint ?? DominantCostMana(source);
            Vector3 at = et.ImpactAnchor + Vector3.up * SparkLift; // 抬升防六棱柱地块遮挡（同回复火花口径）
            bool played = false;
            if (BattleVfxFx.Enabled)
            {
                try { played = debuff ? BattleVfxFx.TryPlayHit(mana, at, 1f) : BattleVfxFx.TryPlayHeal(mana, at, 1f); }
                catch (Exception ex) { BattleVfxFx.NoteFailure(ex); }
            }
            if (!played)
                BattleEffectFx.PlaySpark(at, BattleCastArrival.ManaColor(mana));
#if UNITY_EDITOR
            Debug.Log($"[GrantFx] 赋予爆发：{(debuff ? "减益Hit" : "增益Heal")} 色={mana}" // 演出取证口（构建不打）
                      + $" 路由={(played ? "VFX" : "Spark兜底")}{(effectTint == null ? "（无基准·回落来源主色）" : "")}");
#endif
            return UniTask.CompletedTask;
        }

        /// <summary>拼点演出（ClashResolvedEvent·2026-10-20 教学二表现接入）：攻击宣言期双方手牌展示
        /// 最高攻生物（扇区同拍翻面），本演出在**引擎卡**头顶出结果句「己方x，对方x，成功/失败」
        ///（胜=金/败=灰——正差额未达门槛的惜败同判灰；粒子版因观感基本不可见已退役，美术表现后置）。
        /// 占视觉门；引擎卡入单位串行链（同单位伤害/回复排队）；引擎卡不在视图（教学介绍段/未上场）
        /// 静默返回。奖励本体另有各自演出（抽牌飞入/伤害弹体…），本演出只担「比拼结果」一拍。</summary>
        public async UniTask PlayClashResolvedAsync(Card engineCard, int myPower, int theirPower,
            int threshold, bool success, Func<Player, int> ownerOf)
        {
            if (engineCard == null) return;
            var et = ResolveEffectTarget(engineCard, ownerOf);
            if (et == null) return;
            BeginVisual();
            try
            {
                await RunUnitFxAsync(engineCard.RuntimeId,
                    () => ClashFxCoreAsync(et, myPower, theirPower, threshold, success));
            }
            finally { EndVisual(); }
        }

        /// <summary>运势掷骰浮字（LuckRolledEvent·2026-10-20 定案：**不搞特效**——TMP 文本一拍）：
        /// 来源引擎卡**头顶**出「投掷结果 {d1}+{d2}，效果通过/不通过」（默认字体=PZD 中文齐备），
        /// 通过=金／不通过=灰（拼点同款双色）。占视觉门（同单位伤害/回复串行链）；引擎卡不在
        /// 视图（教学介绍段/已退场）静默返回，绝不报错。骰值细节另有战报行（KeywordApplied）。</summary>
        public async UniTask PlayLuckRollAsync(Card source, int d1, int d2, bool success,
            Func<Player, int> ownerOf)
        {
            if (source == null) return;
            var et = ResolveEffectTarget(source, ownerOf);
            if (et == null) return;
            string label = $"投掷结果 {d1}+{d2}，效果{(success ? "通过" : "不通过")}";
            BeginVisual();
            try
            {
                await RunUnitFxAsync(source.RuntimeId,
                    () => FloatTextAsync(et.FloatParent, et.FloatHeight, label,
                        success ? ClashWinColor : ClashLoseColor, LuckRollFontSize));
            }
            finally { EndVisual(); }
        }

        private async UniTask ClashFxCoreAsync(EffectTarget et, int myPower, int theirPower,
            int threshold, bool success)
        {
#if UNITY_EDITOR
            Debug.Log($"[EffectFx] 拼点演出 己方{myPower} 对方{theirPower} 门槛{threshold}={(success ? "成功" : "失败")}"
                      + "（手牌展示比攻击力）"); // 演出取证口（构建不打）
#endif
            await FloatTextAsync(et.FloatParent, et.FloatHeight,
                $"己方{myPower}，对方{theirPower}，{(success ? "成功" : "失败")}",
                success ? ClashWinColor : ClashLoseColor, ClashFontSize);
        }

        /// <summary>属性指示物浮字（CounterChangedEvent·白名单过滤）：攻/血/±1/角色弹药族 →
        /// 绿/红「±N」头顶浮字。文案 ASCII（TMP 默认字体数字/ASCII 口径，与头顶 P/L 同语言）；
        /// 毒素/护甲/耐久/状态类已有指示物 UI，不浮字防刷屏。占视觉门（浮字期人机节奏等待）。</summary>
        public async UniTask PlayCounterFloatAsync(Entity target, string counterType, int amount,
            Func<Player, int> ownerOf)
        {
            string label;
            Color color;
            switch (counterType)
            {
                case CardCore.Attribute.CounterRules.PowerUpCounter:
                    label = "P +" + amount; color = StatUpColor; break;
                case CardCore.Attribute.CounterRules.PowerDownCounter:
                    label = "P -" + amount; color = StatDownColor; break;
                case CardCore.Attribute.CounterRules.LifeUpCounter:
                    label = "L +" + amount; color = StatUpColor; break;
                case CardCore.Attribute.CounterRules.LifeDownCounter:
                    label = "L -" + amount; color = StatDownColor; break;
                case CardCore.Attribute.CounterRules.PlusOneCounter:
                    label = "+" + amount + "/+" + amount; color = StatUpColor; break;
                case CardCore.Attribute.CounterRules.MinusOneCounter:
                    label = "-" + amount + "/-" + amount; color = StatDownColor; break;
                case CardCore.Attribute.CounterRules.HeroAttackCounter:
                    label = "P +" + amount; color = StatUpColor; break;
                default: return; // 非属性族（毒素/护甲/耐久/系统计数…）——已有指示物 UI，零成本直返
            }
            var et = ResolveEffectTarget(target, ownerOf);
            if (et == null) return; // 手牌费用族（CostUp/Down 在白名单外）/目标离场——静默
            BeginVisual();
            try { await FloatTextAsync(et.FloatParent, et.FloatHeight, label, color); }
            finally { EndVisual(); }
        }

        /// <summary>火花抬升（2026-10-19 实测定案）：贴地火花被六棱柱地块遮挡不可见——
        /// 与 BattleCastArrival.LandSpawnLift 同防（0.6+）。</summary>
        private const float SparkLift = 0.65f;

        /// <summary>效果死亡预持（CardDestroyEvent 引擎时点同步调，投影器死亡钩子）：
        /// 尸体在视图且无人持有（编舞器战斗击杀已持有=不接管）→ 锁 Dead+持票，防快照对账
        /// RemoveStale 抢先销毁（效果致死卡已入墓不进快照）。返回后由 PresentEffectDeathAsync 消费。</summary>
        public void HoldForEffectDeath(CardDestroyEvent e)
        {
            var card = e?.DestroyedCard;
            if (card == null) return;
            if (!_units.TryGetValue(card.RuntimeId, out var v) || v.Wrapper == null) return;
            if (v.Dead || IsHeld(card.RuntimeId)) return; // 已在退场/编舞器持有（战斗击杀）=不接管
            v.Dead = true; // 锁存：重复死亡事件/对账分支不再双触发
            HoldUnit(card.RuntimeId);
            _effectDeathHeld.Add(card.RuntimeId);
        }

        /// <summary>效果死亡演出（延迟载荷随回放步放出；2026-10-20 时序定案=**效果发动后的表现**）：
        /// 整体入单位串行链一次跑完（排在同单位伤害/回复之后），占视觉门（效果致死档退出
        /// 「可忽略不占门」口径——导演逐步等待覆盖；战斗编舞击杀不接管=编舞器自有串行不变）。
        /// 链内两段：③消灭/摧毁族（Reason=Destroyed/Smashed）飞行体——先等扣费粒子到位于法术位
        ///（castCell 事件期抓拍，回放期卡已入墓查不到；null=技能/触发等照旧角色锚点）→弹体飞到
        /// 目标→紫火花涌起；④碎裂退场——预持接管时开演（DeathExitStarted 置位让受击判据让位）
        /// 并弃预持票。伤害击杀（非消灭族）只有④——排在链上的伤害演出之后=受击后碎裂。
        /// 地牌在池内无视图单位——静默返回。</summary>
        public async UniTask PresentEffectDeathAsync(Entity source, Card card, DestroyReason reason,
            Func<Player, int> ownerOf, (int x, int z)? castCell = null, ManaType? effectTint = null)
        {
            if (card == null) return;
            bool destroyFamily = reason == DestroyReason.Destroyed || reason == DestroyReason.Smashed;
            bool heldTaken = _effectDeathHeld.Remove(card.RuntimeId);
            if (!heldTaken && !destroyFamily) return; // 编舞器接管/无预持且非消灭族=零动作

            var rid = card.RuntimeId;
            var et = destroyFamily ? ResolveEffectTarget(card, null) : null;
            bool fly = destroyFamily && et?.Unit?.Wrapper != null; // 消灭飞行体需目标在视图

            BeginVisual();
            try
            {
                await RunUnitFxAsync(rid, async () =>
                {
                    // 段③：消灭族飞行体（效果弹体——先等扣费粒子到位于法术位再起飞）
                    if (fly)
                    {
                        if (castCell != null)
                            await WaitCastPayArrived(ownerOf(source as Player));
                        var src = castCell != null
                            ? CastingAnchor(castCell.Value.x, castCell.Value.z) + Vector3.up * 0.35f // 法术位起飞（同伤害弹体口径）
                            : ResolveEffectSourceAnchor(source, ownerOf); // 技能/触发等无发动位=施法者角色
                        var mana = effectTint ?? DominantCostMana(source); // 路由色（2026-10-23 VFX 接入，同伤害链口径）
                        var tint = BattleCastArrival.ManaColor(mana);
                        // 范围节点优先（Phase 1）：消灭族目标点 AOE 爆；未路由走原弹体+爆点链。紫火花=消灭印记恒保留
                        bool areaRouted = false;
                        if (BattleVfxFx.Enabled)
                        {
                            try { areaRouted = BattleVfxFx.TryPlayArea(mana, et.ImpactAnchor, 1f); }
                            catch (Exception ex) { BattleVfxFx.NoteFailure(ex); }
                        }
                        if (!areaRouted)
                        {
                            if (src != null)
                                await BattleEffectFx.PlayOrbAsync(src.Value, et.ImpactAnchor, tint);
                            BattleEffectFx.PlayImpact(et.ImpactAnchor, tint); // 命中爆点（2026-10-20 补齐）：同伤害/飞行链口径——锚=弹体落点、色=效果色
                        }
                        BattleEffectFx.PlaySpark(et.Unit.Wrapper.position + Vector3.up * SparkLift, DestroySparkColor, 16000f);
#if UNITY_EDITOR
                        Debug.Log($"[EffectFx] 消灭演出 目标=单位{rid % 10000} 路由={(areaRouted ? "AOE" : "旧链")} 弹体={(src != null ? 1 : 0)}"
                                  + $"{(castCell != null ? $" 法术位({castCell.Value.x},{castCell.Value.z})" : "")}");
#endif
                    }
                    // 段④：碎裂退场（预持接管时）
                    if (heldTaken)
                    {
                        if (_units.TryGetValue(rid, out var v) && v.Wrapper != null)
                        {
                            v.DeathExitStarted = true; // 链内开演置位（受击判据让位）
                            await PlayDeathExitAsync(rid, v.Owner); // 链内 await（不再 Forget——串行收尾）
                        }
                        ReleaseUnit(rid); // 弃预持票（退场已自持/收尾已 Retire 幂等）
#if UNITY_EDITOR
                        Debug.Log($"[EffectFx] 死亡碎裂 目标=单位{rid % 10000} reason={reason}（链尾开演）");
#endif
                    }
                });
            }
            finally { EndVisual(); }
        }

        /// <summary>效果飞行演出（EffectFlightEvent·2026-10-20 定案：**效果无效或对目标不适用≠
        /// 发动无效**——费已付，弹体照飞+到达爆点；无伤害/死亡事件=无浮字/受击/碎裂）：
        /// 等扣费粒子到位于法术位→弹体法术位→目标→爆点。单位目标入串行链+占门（同伤害口径）。</summary>
        public async UniTask PlayEffectFlightAsync(Entity source, Entity target,
            Func<Player, int> ownerOf, (int x, int z)? castCell, ManaType? effectTint)
        {
            var et = ResolveEffectTarget(target, ownerOf);
            if (et == null) return;
            BeginVisual();
            try
            {
                if (target is Card tc)
                    await RunUnitFxAsync(tc.RuntimeId, () => FlightFxCoreAsync(et, source, ownerOf, castCell, effectTint));
                else
                    await FlightFxCoreAsync(et, source, ownerOf, castCell, effectTint);
            }
            finally { EndVisual(); }
        }

        private async UniTask FlightFxCoreAsync(EffectTarget et, Entity source,
            Func<Player, int> ownerOf, (int x, int z)? castCell, ManaType? effectTint)
        {
            if (castCell != null)
                await WaitCastPayArrived(ownerOf(source as Player));
            var src = castCell != null
                ? CastingAnchor(castCell.Value.x, castCell.Value.z) + Vector3.up * 0.35f
                : ResolveEffectSourceAnchor(source, ownerOf);
            var mana = effectTint ?? DominantCostMana(source); // 路由色（2026-10-23 VFX 接入，同伤害链口径）
            var tint = BattleCastArrival.ManaColor(mana);
            // 颜色路由（Phase 1）：弹体→Hit 两节点（飞行事件无发动位=无 Cast 节点）；失败整链回落旧 orb+impact
            bool routed = false;
            if (BattleVfxFx.Enabled)
            {
                try
                {
                    if (src != null)
                    {
                        routed = await BattleVfxFx.TryPlayProjectileAsync(mana, src.Value, et.ImpactAnchor, 1f);
                        if (routed) BattleVfxFx.TryPlayHit(mana, et.ImpactAnchor, 1f);
                    }
                    else // 无源锚点=直落命中（同旧链 src==null 只播爆点口径）
                        routed = BattleVfxFx.TryPlayHit(mana, et.ImpactAnchor, 1f);
                }
                catch (Exception ex) { BattleVfxFx.NoteFailure(ex); routed = false; }
            }
            if (!routed)
            {
                if (src != null)
                    await BattleEffectFx.PlayOrbAsync(src.Value, et.ImpactAnchor, tint);
                BattleEffectFx.PlayImpact(et.ImpactAnchor, tint);
            }
#if UNITY_EDITOR
            Debug.Log($"[EffectFx] 飞行演出（效果无效/不适用·发动照播）目标={(et.Unit != null ? "单位" : "角色")} 路由={(routed ? "VFX" : "旧链")}"
                      + $"{(castCell != null ? $" 法术位({castCell.Value.x},{castCell.Value.z})" : "")}");
#endif
        }

        /// <summary>消灭火花色：暗紫提亮（纯暗紫自发光不可见，向白提一档）。</summary>
        private static readonly Color DestroySparkColor = new Color(0.62f, 0.34f, 0.95f);

        /// <summary>头顶浮字（上浮渐隐 0.9s，world TMP 一次性建毁——数学同疲劳浮字）。</summary>
        private static async UniTask FloatTextAsync(Transform parent, float height, string text, Color color,
            float fontSize = 6f)
        {
            var floatTxt = CreateStatsText(parent, height);
            if (floatTxt != null)
            {
                floatTxt.fontSize = fontSize;
                floatTxt.text = text;
                floatTxt.color = color;
            }
            var startLocal = floatTxt != null ? (Vector3)floatTxt.transform.localPosition : Vector3.zero;
            const float totalDur = 0.9f, fadeStart = 0.45f, rise = 0.85f;
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < totalDur)
            {
                if (parent == null) break; // 拆台/单位退场销毁——浮字随父同殁，直接收
                if (floatTxt != null)
                {
                    float t = Time.realtimeSinceStartup - t0;
                    float k = Mathf.Clamp01(t / totalDur);
                    floatTxt.transform.localPosition = startLocal + Vector3.up * (rise * (1f - (1f - k) * (1f - k)));
                    float alpha = t < fadeStart ? 1f : Mathf.Clamp01(1f - (t - fadeStart) / (totalDur - fadeStart));
                    var c = floatTxt.color; c.a = alpha; floatTxt.color = c;
                }
                await UniTask.Yield();
            }
            if (floatTxt != null) Object.Destroy(floatTxt.gameObject);
        }
    }
}
