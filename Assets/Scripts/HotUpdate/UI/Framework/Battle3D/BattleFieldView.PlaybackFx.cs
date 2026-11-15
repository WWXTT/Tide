using System;
using System.Collections.Generic;
using CardCore;
using Cysharp.Threading.Tasks;
using GameBoard;
using MonsterRig;
using UnityEngine;

namespace SynergyUI
{
    // ============================================================
    // 回放演出入口（2026-11-11 战斗演出统一重构 S2·方案 §2.2）：
    // ref 版（FxRef 视图空间引用）演出——与 Entity 版（EffectFx/DeathFx，Live 闭包路径）共用
    // 私有原语（EffectTarget 结构/_units/_heroes/串行链/视觉门/浮字/VFX 路由）；
    // 颜色/锚点全部由 BattleFxOp 捕获时解析好的数据携带（Tint/CastX/CastZ/Seat），执行侧不查引擎。
    // S5 本地 Live 切数据指令后，本文件即唯一实现（Entity 版随闭包路径退役）。
    // ============================================================
    public sealed partial class BattleFieldView
    {
        /// <summary>ref 目标解析（镜像 ResolveEffectTarget）：单位=场上 _units[Rid]；角色=立柱 _heroes[Seat]。
        /// 不在视图（未上场/已退场/未建柱）返回 null——调用方静默跳过，绝不报错。</summary>
        private EffectTarget ResolveFxTarget(FxRef r)
        {
            if (r == null) return null;
            if (!r.IsPlayer)
            {
                if (r.Rid == 0 || !_units.TryGetValue(r.Rid, out var uv) || uv.Wrapper == null) return null;
                return new EffectTarget
                {
                    Unit = uv,
                    FloatParent = uv.Wrapper,
                    FloatHeight = uv.StatsHeightY + 0.55f,
                    ImpactAnchor = uv.Wrapper.position + Vector3.up * uv.LookHeight,
                };
            }
            if (r.Seat < 0 || !_heroes.TryGetValue(r.Seat, out var hv) || hv.Root == null) return null;
            return new EffectTarget
            {
                HeroOwner = r.Seat,
                FloatParent = hv.Root,
                FloatHeight = 1.9f,
                ImpactAnchor = hv.Root.position + Vector3.up * 1.1f,
            };
        }

        /// <summary>ref 源锚点（镜像 ResolveEffectSourceAnchor）：场上单位=胸口；否则（施放中法术/
        /// 角色/无源）= Seat 角色立柱。解析失败 null——调用方跳过飞行段直落爆点。</summary>
        private Vector3? ResolveFxSourceAnchor(FxRef r)
        {
            if (r == null) return null;
            if (!r.IsPlayer && r.Rid != 0
                && _units.TryGetValue(r.Rid, out var v) && v.Wrapper != null && !v.Dead)
                return v.Wrapper.position + Vector3.up * v.LookHeight;
            int seat = r.IsPlayer ? r.Seat : -1; // 非场上卡来源=施法者（捕获时 Seat 已留 -1 的走立柱兜底不可能——见 Capture 端 Source 记 Controller seat）
            if (seat < 0) seat = 0; // 数据缺座位（防御）——我方立柱兜底
            return _heroes.TryGetValue(seat, out var hv) && hv.Root != null
                ? hv.Root.position + Vector3.up * 1.1f
                : (Vector3?)null;
        }

        /// <summary>法术位（op.CastX/Z >= 0 时转爆点参照）：法术位起飞+先等扣费粒子到位（同 Entity 版口径）。</summary>
        private (int x, int z)? CastCellOf(BattleFxOp op)
            => op != null && op.CastX >= 0 && op.CastZ >= 0 ? (op.CastX, op.CastZ) : null;

        /// <summary>法术位等待的付费座位（Source.Seat——捕获时 Controller 座位；缺省我方兜底）。</summary>
        private static int CastSeatOf(BattleFxOp op)
            => op?.Source != null && op.Source.Seat >= 0 ? op.Source.Seat : 0;

        /// <summary>Cast→弹体→Hit 三节点 VFX 路由（镜像 DamageFxCoreAsync 路由段；false=回落旧链）。</summary>
        private async UniTask<bool> TryRouteCastChainAsync(ManaType mana, Vector3? src, Vector3 dst)
        {
            if (!BattleVfxFx.Enabled || src == null) return false;
            bool used = false;
            try
            {
                used = BattleVfxFx.TryPlayCast(mana, src.Value, 1f);
                if (used) used = await BattleVfxFx.TryPlayProjectileAsync(mana, src.Value, dst, 1f);
                if (used) BattleVfxFx.TryPlayHit(mana, dst, 1f);
            }
            catch (Exception ex) { BattleVfxFx.NoteFailure(ex); used = false; }
            return used;
        }

        /// <summary>效果伤害（ref 版·镜像 PlayEffectDamageAsync）：法术位先等扣费粒子；单位目标入串行链。</summary>
        public async UniTask PlayDamageByRefAsync(BattleFxOp op)
        {
            var et = ResolveFxTarget(op?.Target);
            if (et == null) return;
            BeginVisual();
            try
            {
                if (et.Unit != null)
                    await RunUnitFxAsync(op.Target.Rid, () => DamageRefCoreAsync(et, op));
                else
                    await DamageRefCoreAsync(et, op);
            }
            finally { EndVisual(); }
        }

        private async UniTask DamageRefCoreAsync(EffectTarget et, BattleFxOp op)
        {
            var castCell = CastCellOf(op);
            if (castCell != null) await WaitCastPayArrived(CastSeatOf(op));
            var mana = (op.TintOf ?? ManaType.Gray);
            // 范围伤害（L4-4·2026-11-13 阶段2）：AOE 伤害在目标处播 Area 大件（1.2s），命中链
            //（Cast→弹体→Hit）退为回落——缺条目/并发满自动回旧链，不喧宾夺主。
            if (op.Flag)
            {
                bool area = false;
                if (BattleVfxFx.Enabled)
                {
                    try { area = BattleVfxFx.TryPlayArea(mana, et.ImpactAnchor, 1f); }
                    catch (Exception ex) { BattleVfxFx.NoteFailure(ex); }
                }
                if (area)
                {
                    if (et.Unit != null && !et.Unit.DeathExitStarted)
                        et.Unit.Anim?.Play(MonsterAction.Hit);
                    else if (et.Unit == null)
                        HeroImpactPulseAsync(et.HeroOwner).Forget();
                    await FloatTextAsync(et.FloatParent, et.FloatHeight, "-" + op.Amount, StatDownColor);
                    return;
                }
            }
            var src = castCell != null
                ? CastingAnchor(castCell.Value.x, castCell.Value.z) + Vector3.up * 0.35f
                : ResolveFxSourceAnchor(op.Source);
            var tint = BattleCastArrival.ManaColor(mana);
            bool used = await TryRouteCastChainAsync(mana, src, et.ImpactAnchor);
            if (!used)
            {
                if (src != null) await BattleEffectFx.PlayOrbAsync(src.Value, et.ImpactAnchor, tint);
                BattleEffectFx.PlayImpact(et.ImpactAnchor, tint);
            }
            if (et.Unit != null && !et.Unit.DeathExitStarted)
                et.Unit.Anim?.Play(MonsterAction.Hit);
            else if (et.Unit == null)
                HeroImpactPulseAsync(et.HeroOwner).Forget();
            await FloatTextAsync(et.FloatParent, et.FloatHeight, "-" + op.Amount, StatDownColor);
        }

        /// <summary>回复（ref 版·镜像 PlayEffectHealAsync）：效果色火花+「+N」浮字（L4-1：
        /// op.Tint 捕获时已解析——Heal 原子带效果基准色，其余来源 null=回落绿）；单位目标入串行链。</summary>
        public async UniTask PlayHealByRefAsync(BattleFxOp op)
        {
            if (op.Amount <= 0) return;
            var et = ResolveFxTarget(op?.Target);
            if (et == null) return;
            BeginVisual();
            try
            {
                if (et.Unit != null) await RunUnitFxAsync(op.Target.Rid, () => HealRefCoreAsync(et, op.Amount, op.TintOf));
                else await HealRefCoreAsync(et, op.Amount, op.TintOf);
            }
            finally { EndVisual(); }
        }

        private async UniTask HealRefCoreAsync(EffectTarget et, int amount, ManaType? tint)
        {
            Vector3 ground = et.Unit != null && et.Unit.Wrapper != null
                ? et.Unit.Wrapper.position
                : et.FloatParent != null ? et.FloatParent.position : et.ImpactAnchor;
            var mana = tint ?? ManaType.Green; // 回复吃效果色（L4-1）；无基准=绿（回复旧行为）
            bool healed = false;
            if (BattleVfxFx.Enabled)
            {
                try { healed = BattleVfxFx.TryPlayHeal(mana, ground + Vector3.up * SparkLift, 1f); }
                catch (Exception ex) { BattleVfxFx.NoteFailure(ex); }
            }
            if (!healed) BattleEffectFx.PlaySpark(ground + Vector3.up * SparkLift, SparkUpColor);
            await FloatTextAsync(et.FloatParent, et.FloatHeight, "+" + amount, StatUpColor);
        }

        /// <summary>效果飞行（ref 版·镜像 PlayEffectFlightAsync——效果无效/不适用，发动照播）。</summary>
        public async UniTask PlayFlightByRefAsync(BattleFxOp op)
        {
            var et = ResolveFxTarget(op?.Target);
            if (et == null) return;
            BeginVisual();
            try
            {
                if (et.Unit != null) await RunUnitFxAsync(op.Target.Rid, () => FlightRefCoreAsync(et, op));
                else await FlightRefCoreAsync(et, op);
            }
            finally { EndVisual(); }
        }

        private async UniTask FlightRefCoreAsync(EffectTarget et, BattleFxOp op)
        {
            var castCell = CastCellOf(op);
            if (castCell != null) await WaitCastPayArrived(CastSeatOf(op));
            var src = castCell != null
                ? CastingAnchor(castCell.Value.x, castCell.Value.z) + Vector3.up * 0.35f
                : ResolveFxSourceAnchor(op.Source);
            var mana = (op.TintOf ?? ManaType.Gray);
            var tint = BattleCastArrival.ManaColor(mana);
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
                    else routed = BattleVfxFx.TryPlayHit(mana, et.ImpactAnchor, 1f);
                }
                catch (Exception ex) { BattleVfxFx.NoteFailure(ex); routed = false; }
            }
            if (!routed)
            {
                if (src != null) await BattleEffectFx.PlayOrbAsync(src.Value, et.ImpactAnchor, tint);
                BattleEffectFx.PlayImpact(et.ImpactAnchor, tint);
            }
        }

        /// <summary>效果死亡预持（ref 版·镜像 HoldForEffectDeath——PreOps 段，先于 View 应用）。</summary>
        public void HoldForDeathByRef(uint rid)
        {
            if (rid == 0) return;
            if (!_units.TryGetValue(rid, out var v) || v.Wrapper == null) return;
            if (v.Dead || IsHeld(rid)) return;
            v.Dead = true;
            HoldUnit(rid);
            _effectDeathHeld.Add(rid);
        }

        /// <summary>效果死亡演出（ref 版·镜像 PresentEffectDeathAsync）：消灭族（Destroyed/Smashed）
        /// 飞行体+紫火花；预持接管时碎裂退场。整体入单位串行链。</summary>
        public async UniTask PresentDeathByRefAsync(BattleFxOp op)
        {
            var card = op?.Target;
            if (card == null || card.Rid == 0) return;
            bool destroyFamily = op.Reason == (int)DestroyReason.Destroyed || op.Reason == (int)DestroyReason.Smashed;
            bool heldTaken = _effectDeathHeld.Remove(card.Rid);
            if (!heldTaken && !destroyFamily) return; // 编舞器接管/无预持且非消灭族=零动作
            // 2026-11-13 补位（L3-5）：目标恒解析——非消灭族死因（Combat/Annihilated/LegendaryRule）
            // 此前 et 恒 null=只有碎裂退场、零爆点；现在要拿 ImpactAnchor 补一枚 Death 角色爆点。
            var et = ResolveFxTarget(card);
            bool fly = destroyFamily && et?.Unit?.Wrapper != null;
            var castCell = CastCellOf(op);
            BeginVisual();
            try
            {
                await RunUnitFxAsync(card.Rid, async () =>
                {
                    if (fly)
                    {
                        if (castCell != null)
                            await WaitCastPayArrived(CastSeatOf(op));
                        var src = castCell != null
                            ? CastingAnchor(castCell.Value.x, castCell.Value.z) + Vector3.up * 0.35f
                            : ResolveFxSourceAnchor(op.Source);
                        var mana = op.TintOf ?? ManaType.Gray;
                        var tint = BattleCastArrival.ManaColor(mana);
                        bool areaRouted = false;
                        if (BattleVfxFx.Enabled)
                        {
                            try { areaRouted = BattleVfxFx.TryPlayArea(mana, et.ImpactAnchor, 1f); }
                            catch (Exception ex) { BattleVfxFx.NoteFailure(ex); }
                        }
                        if (!areaRouted)
                        {
                            if (src != null) await BattleEffectFx.PlayOrbAsync(src.Value, et.ImpactAnchor, tint);
                            BattleEffectFx.PlayImpact(et.ImpactAnchor, tint);
                        }
                        BattleEffectFx.PlaySpark(et.Unit.Wrapper.position + Vector3.up * SparkLift, DestroySparkColor, 16000f);
                    }
                    else if (et != null)
                    {
                        // 补位（L3-5·2026-11-13）：非消灭族死因补一记短爆——PixPlays Blast 四色
                        // （此前从未入表）。缺条目/并发满=回落紫 Spark（消灭印记同色，与消灭族尾巴一致）。
                        var mana = op.TintOf ?? ManaType.Gray;
                        bool burst = false;
                        if (BattleVfxFx.Enabled)
                        {
                            try { burst = BattleVfxFx.TryPlayDeath(mana, et.ImpactAnchor, 1f); }
                            catch (Exception ex) { BattleVfxFx.NoteFailure(ex); }
                        }
                        if (!burst)
                            BattleEffectFx.PlaySpark(et.Unit?.Wrapper != null
                                ? et.Unit.Wrapper.position + Vector3.up * SparkLift
                                : et.ImpactAnchor, DestroySparkColor);
                    }
                    if (heldTaken)
                    {
                        if (_units.TryGetValue(card.Rid, out var v) && v.Wrapper != null)
                        {
                            v.DeathExitStarted = true;
                            await PlayDeathExitAsync(card.Rid, v.Owner);
                        }
                        ReleaseUnit(card.Rid);
                    }
                });
            }
            finally { EndVisual(); }
        }

        /// <summary>属性指示物浮字（ref 版·镜像 PlayCounterFloatAsync——白名单同表，非属性族零成本直返）。</summary>
        public async UniTask PlayCounterFloatByRefAsync(BattleFxOp op)
        {
            string label;
            Color color;
            switch (op?.Text)
            {
                case CardCore.Attribute.CounterRules.PowerUpCounter:
                    label = "P +" + op.Amount; color = StatUpColor; break;
                case CardCore.Attribute.CounterRules.PowerDownCounter:
                    label = "P -" + op.Amount; color = StatDownColor; break;
                case CardCore.Attribute.CounterRules.LifeUpCounter:
                    label = "L +" + op.Amount; color = StatUpColor; break;
                case CardCore.Attribute.CounterRules.LifeDownCounter:
                    label = "L -" + op.Amount; color = StatDownColor; break;
                case CardCore.Attribute.CounterRules.PlusOneCounter:
                    label = "+" + op.Amount + "/+" + op.Amount; color = StatUpColor; break;
                case CardCore.Attribute.CounterRules.MinusOneCounter:
                    label = "-" + op.Amount + "/-" + op.Amount; color = StatDownColor; break;
                case CardCore.Attribute.CounterRules.HeroAttackCounter:
                    label = "P +" + op.Amount; color = StatUpColor; break;
                default: return; // 非属性族（毒素/护甲/耐久/系统计数…）——已有指示物 UI，零成本直返
            }
            var et = ResolveFxTarget(op.Target);
            if (et == null) return;
            BeginVisual();
            try { await FloatTextAsync(et.FloatParent, et.FloatHeight, label, color); }
            finally { EndVisual(); }
        }

        // ==== 值浮字统一入口（2026-11-13 战场数值 P2·§4.3）====
        // 与伤害/回复共用 FloatTextAsync/锚点/字号（CreateStatsText 单源），差异三则：
        // · 不占视觉门（装饰层——多指示物不拖 AI 节拍；伤害/回复/疲劳仍占门属战斗节拍）；
        // · 颜色由 op.Polarity 三档（+1 绿/-1 红/0 灰），Text=生产侧拼好的短标签（含符号）；
        // · 同目标并发堆叠（§3.2）：每条起点 +0.28 高度、最多 4 条，超出按 0.18s 间隔排队。
        private static readonly Color ValueNeutralColor = new Color(0.9f, 0.9f, 0.9f); // §3.2 中性 #E6E6E6
        private readonly Dictionary<uint, int> _valueFloatStack = new Dictionary<uint, int>();

        /// <summary>值浮字（ValueFloat op）：标签/极性由 BattleProjector 经 ValueFloatRules 单源拼好。
        /// 不在视图/已退场静默；同单位走 RunUnitFxAsync 串行链（与伤害/回复同链防穿插）。</summary>
        public async UniTask PlayValueFloatByRefAsync(BattleFxOp op)
        {
            if (op == null || string.IsNullOrEmpty(op.Text)) return;
            var et = ResolveFxTarget(op.Target);
            if (et == null) return; // 目标不在视图（未上场/已退场/未建柱）——静默不报错
            Color color = op.Polarity > 0 ? StatUpColor : op.Polarity < 0 ? StatDownColor : ValueNeutralColor;

            // 堆叠：同目标并发计数（角色键=高位标记|Seat，单位键=Rid）
            uint stackKey = op.Target.IsPlayer
                ? 0x80000000u | (uint)Math.Max(0, op.Target.Seat)
                : op.Target.Rid;
            _valueFloatStack.TryGetValue(stackKey, out int depth);
            if (depth >= 4) await UniTask.Delay(180 * (depth - 3)); // 超出 4 条按 0.18s/条排队（§3.2）
            _valueFloatStack[stackKey] = depth + 1;
            float lift = Mathf.Min(depth, 3) * 0.28f;

            async UniTask Core()
            {
                try { await FloatTextAsync(et.FloatParent, et.FloatHeight + lift, op.Text, color); }
                finally
                {
                    if (--_valueFloatStack[stackKey] <= 0) _valueFloatStack.Remove(stackKey);
                }
            }
            if (et.Unit != null) await RunUnitFxAsync(op.Target.Rid, Core); // 同单位串行链
            else await Core();
        }

        /// <summary>赋予爆发粒子（ref 版·CounterBurst/KeywordBurst 共口·镜像 PlayGrantBurstAsync：
        /// 减益=Hit 族/增益=Shield→Heal 族，颜色=捕获 Tint）。</summary>
        public async UniTask PlayGrantBurstByRefAsync(BattleFxOp op)
        {
            var et = ResolveFxTarget(op?.Target);
            if (et == null) return;
            if (et.Unit != null) await RunUnitFxAsync(op.Target.Rid, () => GrantBurstRefCoreAsync(et, op));
            else await GrantBurstRefCoreAsync(et, op);
        }

        private UniTask GrantBurstRefCoreAsync(EffectTarget et, BattleFxOp op)
        {
            var mana = op.TintOf ?? ManaType.Gray;
            Vector3 at = et.ImpactAnchor + Vector3.up * SparkLift;
            bool played = false;
            if (BattleVfxFx.Enabled)
            {
                try
                {
                    // 2026-11-13 补位（L4-5）：正面赋予改走 Shield「罩住」件（红/绿两条配置此前零消费者），
                    // 缺色（蓝/灰）回落 Heal——先有画面，观感后调。
                    played = op.Flag
                        ? BattleVfxFx.TryPlayHit(mana, at, 1f)
                        : (BattleVfxFx.TryPlayShield(mana, at, 1f) || BattleVfxFx.TryPlayHeal(mana, at, 1f));
                }
                catch (Exception ex) { BattleVfxFx.NoteFailure(ex); }
            }
            if (!played) BattleEffectFx.PlaySpark(at, BattleCastArrival.ManaColor(mana));
            return UniTask.CompletedTask;
        }

        /// <summary>诅咒/祝福牌库标记（ref 版·镜像 PlayDeckMarkAsync）：卡堆顶爆发+脉冲（归属座位直连）。</summary>
        public UniTask PlayDeckMarkByRefAsync(int seat, bool blessing)
        {
            if (seat >= 0 && BattleZoneProps.DeckTopWorldPosition(seat, out var top))
            {
                BattleZoneProps.PulseDeck(seat);
                Vector3 at = top + Vector3.up * 0.3f;
                // 2026-11-13 补位（L4-2）：祝福/诅咒不再同灰——祝福走红档暖黄爆（SparkleExplosionYellow），
                // 诅咒维持灰档暗爆（ShadowExplosion）；缺条目回落 Spark（暖绿/暗紫旧色）。
                var color = blessing ? ManaType.Red : ManaType.Gray;
                bool played = false;
                if (BattleVfxFx.Enabled)
                {
                    try
                    {
                        played = blessing ? BattleVfxFx.TryPlayHeal(color, at, 1f) : BattleVfxFx.TryPlayHit(color, at, 1f);
                    }
                    catch (Exception ex) { BattleVfxFx.NoteFailure(ex); }
                }
                if (!played) BattleEffectFx.PlaySpark(at, blessing ? SparkUpColor : DeckMarkCurseColor);
            }
            return UniTask.CompletedTask;
        }

        /// <summary>拼点结果浮字（ref 版·镜像 PlayClashResolvedAsync）。</summary>
        public async UniTask PlayClashByRefAsync(BattleFxOp op)
        {
            var et = ResolveFxTarget(op?.Target);
            if (et == null) return;
            BeginVisual();
            try
            {
                await RunUnitFxAsync(op.Target.Rid, () => FloatTextAsync(et.FloatParent, et.FloatHeight,
                    $"己方{op.D1}，对方{op.D2}，{(op.Flag ? "成功" : "失败")}",
                    op.Flag ? ClashWinColor : ClashLoseColor, ClashFontSize));
            }
            finally { EndVisual(); }
        }

        /// <summary>运势掷骰浮字（ref 版·镜像 PlayLuckRollAsync）。</summary>
        public async UniTask PlayLuckRollByRefAsync(BattleFxOp op)
        {
            var et = ResolveFxTarget(op?.Target);
            if (et == null) return;
            BeginVisual();
            try
            {
                await RunUnitFxAsync(op.Target.Rid, () => FloatTextAsync(et.FloatParent, et.FloatHeight,
                    $"投掷结果 {op.D1}+{op.D2}，效果{(op.Flag ? "通过" : "不通过")}",
                    op.Flag ? ClashWinColor : ClashLoseColor, LuckRollFontSize));
            }
            finally { EndVisual(); }
        }

        /// <summary>法术扣费粒子（ref 版·镜像 CapturePaid 的 PlayCastPay 段）：实付色粒子自地格汇聚
        /// 至施放者角色格（座位→布局角色格，不查引擎棋盘）。</summary>
        public void PlayCastPayByRef(int seat, Dictionary<int, float> paid)
        {
            if (seat < 0 || paid == null || paid.Count == 0) return;
            var (hx, hz) = BoardLayout.CharacterCell(seat);
            PlayCastPay(seat, paid, hx, hz);
        }

        /// <summary>轻入场（2026-11-13 补位 L2-1）：召唤/复活/衍生物/控制权变更——单位脚下播一枚
        /// Summon 角色汇聚件（PixPlays Aura 四色，此前从未入表）；**不走** BattleCastArrival 的
        /// 2s 消融（模型即显，纯叠加，绝不遮挡入场）。单位不在视图/已退场静默返回；
        /// 缺条目/并发满回落灰色 Spark。一次性件不占视觉门、不占用单位串行链。</summary>
        public void PlaySummonArrivalByRef(uint rid, int seat, Dictionary<int, float> paid)
        {
            if (rid == 0) return;
            if (!_units.TryGetValue(rid, out var v) || v.Wrapper == null || v.Dead) return;
            var mana = BattleCastArrival.DominantManaType(paid);
            Vector3 at = v.Wrapper.position + Vector3.up * SparkLift;
            bool played = false;
            if (BattleVfxFx.Enabled)
            {
                try { played = BattleVfxFx.TryPlaySummon(mana, at, 1f); }
                catch (Exception ex) { BattleVfxFx.NoteFailure(ex); }
            }
            if (!played) BattleEffectFx.PlaySpark(at, BattleCastArrival.ManaColor(mana));
        }

        /// <summary>放地入池短闪（L2-2·2026-11-13 阶段4）：对应元素地格顶面一枚小 Hit 件
        ///（0.8s 一次性自灭——不走 BattleTileFx 候选波：那是选择期状态标志、会被对账 ClearAll 清）。
        /// 缺条目/并发满回落元素色 Spark；未装台静默。</summary>
        public void PlayLandPulseByRef(BattleFxOp op)
        {
            if (op == null || op.Seat < 0 || !BattleBoardGrid.Ready) return;
            var element = op.TintOf ?? ManaType.Any;
            var (x, z) = GameBoard.BoardLayout.LandCellOfElement(op.Seat, element);
            Vector3 at = CastingAnchor(x, z) + Vector3.up * 0.1f;
            bool played = false;
            if (BattleVfxFx.Enabled)
            {
                try { played = BattleVfxFx.TryPlayHit(element == ManaType.Any ? ManaType.Gray : element, at, 0.7f); }
                catch (Exception ex) { BattleVfxFx.NoteFailure(ex); }
            }
            if (!played) BattleEffectFx.PlaySpark(at, BattleTileFx.ElementColorOf((int)element));
        }

        /// <summary>离场微拖尾（L2-2·阶段4）：弃牌/牌库弃置/除外——起点（op 格≥0=战场除外直送；
        /// 否则牌堆顶近似）→ 对应漩涡格心一枚灰 Orb（0.35s 单程短拖尾）。占视觉门；未装台静默。</summary>
        public async UniTask PlayDepartByRefAsync(BattleFxOp op)
        {
            if (op == null || op.Seat < 0 || !BattleBoardGrid.Ready) return;
            var cell = op.Reason == 1 ? BattleZoneProps.ExileCellOf(op.Seat) : BattleZoneProps.GraveCellOf(op.Seat);
            Vector3 dst = CastingAnchor(cell.x, cell.z) + Vector3.up * 0.15f;
            // src/dst 先算再占门（异常须发生在 BeginVisual 之前——编辑器冒烟守卫契约）
            Vector3 src;
            if (op.CastX >= 0 && op.CastZ >= 0) src = CastingAnchor(op.CastX, op.CastZ) + Vector3.up * 0.5f;
            else if (BattleZoneProps.DeckTopWorldPosition(op.Seat, out var top)) src = top;
            else return;
            BeginVisual();
            try { await BattleEffectFx.PlayOrbAsync(src, dst, BattleCastArrival.ManaColor(ManaType.Gray)); }
            finally { EndVisual(); }
        }

        // ======================================== 战斗演出回放（CombatShotPlayer） ========================================

        /// <summary>守卫挡位偏移（镜像 CombatChoreographer.GuardCoverOffset=0.7）：原目标格心向
        /// 攻击侧挪——与被保护者错开身位（L3-1 闪现赴挡落点）。</summary>
        private const float ReplayGuardCoverOffset = 0.7f;

        /// <summary>战斗演出回放（方案 §2.2 CombatShot；2026-11-13 阶段3 精度批）：攻击者飞向目标临格
        ///（sin 半弧）→攻击前扑→目标受击+逐笔伤害浮字（反击笔挂攻击者+受击退让；致死笔加强爆+大字）
        /// →幸存者飞回。守卫闪现赴挡（Blocker+Original 格：现身→挡位→归位）；角色攻击者立柱前扑脉冲
        ///（L3-3）。死亡退场归 Death 指令（PreOps/Ops）。旧剧本缺字段容错（Blocker null/Lethal false/
        /// Original=-1 → 退化 v1 行为）。占视觉门。</summary>
        public async UniTask PlayCombatShotAsync(CombatShot shot)
        {
            if (shot?.Attacker == null || shot.Target == null) return;
            var attEt = ResolveFxTarget(shot.Attacker);
            var tgtEt0 = ResolveFxTarget(shot.Target);
            if (tgtEt0 == null && shot.TargetX < 0) return;

            BeginVisual();
            try
            {
                // 守卫闪现赴挡（L3-1）：拦截者瞬移到原目标格向攻击侧偏移的挡位（Aura 现身件），
                // 战后归位。守卫不在视图/旧剧本无载荷=静默跳过（退化为 v1 目标侧演出）。
                Transform guardWrapper = null;
                Vector3 guardHome = Vector3.zero;
                if (shot.Blocker != null && shot.OriginalX >= 0)
                {
                    var guardEt = ResolveFxTarget(shot.Blocker);
                    var guardUnit = guardEt?.Unit;
                    if (guardUnit != null && guardUnit.Wrapper != null && !guardUnit.Dead)
                    {
                        Vector3 attackerPos = attEt?.ImpactAnchor
                            ?? (shot.Attacker.IsPlayer && shot.Attacker.Seat >= 0 && _heroes.TryGetValue(shot.Attacker.Seat, out var ahv) && ahv.Root != null
                                ? ahv.Root.position + Vector3.up * 1.1f
                                : Vector3.zero);
                        Vector3 origAnchor = CastingAnchor(shot.OriginalX, shot.OriginalZ);
                        Vector3 dir = attackerPos - origAnchor;
                        dir.y = 0f;
                        Vector3 cover = dir.sqrMagnitude > 0.01f
                            ? origAnchor + dir.normalized * ReplayGuardCoverOffset
                            : origAnchor;
                        guardWrapper = guardUnit.Wrapper;
                        guardHome = guardWrapper.position;
                        guardWrapper.position = cover;
                        BattleVfxFx.TryPlaySummon(ManaType.Gray, cover + Vector3.up * SparkLift, 1f); // 现身汇聚（缺件静默）
                        Debug.Log($"[PlaybackFx] 守卫闪现赴挡：rid={shot.Blocker.Rid} → ({shot.OriginalX},{shot.OriginalZ}) 偏移 {ReplayGuardCoverOffset}");
                    }
                }
                // 闪现后重解析目标锚（拦截者已在挡位——飞行临格按新位算）
                var tgtEt = ResolveFxTarget(shot.Target);
                Vector3 tgtAnchor = tgtEt?.ImpactAnchor
                    ?? (shot.TargetX >= 0 ? CastingAnchor(shot.TargetX, shot.TargetZ) + Vector3.up * 0.8f : Vector3.zero);

                var attUnit = attEt?.Unit;
                Transform wrapper = attUnit?.Wrapper;
                Vector3 home = wrapper != null ? wrapper.position : Vector3.zero;
                bool moved = false;
                if (wrapper != null && tgtEt != null && tgtEt.Unit != null)
                {
                    // 飞行段：目标格心向攻击侧回撤一格当临格（近似——回放不寻路）
                    Vector3 dir = home - tgtEt.Unit.Wrapper.position;
                    dir.y = 0f;
                    Vector3 dest = dir.sqrMagnitude > 0.01f
                        ? tgtEt.Unit.Wrapper.position + dir.normalized * 1.1f
                        : tgtEt.Unit.Wrapper.position;
                    await ArcMoveAsync(wrapper, home, dest, 0.9f, 13f);
                    moved = true;
                }
                attUnit?.Anim?.Play(MonsterAction.Attack);
                if (attUnit == null && shot.Attacker.IsPlayer && shot.Attacker.Seat >= 0)
                    HeroImpactPulseAsync(shot.Attacker.Seat).Forget(); // 英雄攻方立柱前扑脉冲（L3-3）
                await UniTask.Delay(180); // 攻击前扑到命中节拍（同编舞 ImpactDelayMs）
                // 逐笔命中：主击挂目标、反击笔挂攻击者（就地浮字+受击拍+退让；致死笔加强爆+大字）
                Vector3 hitFrom = wrapper != null ? wrapper.position : tgtAnchor;
                for (int i = 0; i < shot.Hits.Count; i++)
                {
                    var hit = shot.Hits[i];
                    var hitEt = ResolveFxTarget(hit.Target);
                    if (hitEt == null) continue;
                    if (hitEt.Unit != null && !hitEt.Unit.DeathExitStarted)
                    {
                        hitEt.Unit.Anim?.Play(MonsterAction.Hit);
                        if (hitEt.Unit.Wrapper != null)
                            RecoilAsync(hitEt.Unit.Wrapper, hitFrom).Forget(); // 受击退让（L3-2：反击/溅射第三方同款）
                    }
                    else if (hitEt.Unit == null)
                        HeroImpactPulseAsync(hitEt.HeroOwner).Forget();
                    if (hit.Lethal)
                    {
                        // 致死特写（L3-4）：目标处加强爆（Area 大件 1.2s）+大字浮标
                        bool burst = false;
                        try { burst = BattleVfxFx.TryPlayArea(ManaType.Red, hitEt.ImpactAnchor, 1.3f); }
                        catch (Exception ex) { BattleVfxFx.NoteFailure(ex); }
                        FloatTextAsync(hitEt.FloatParent, hitEt.FloatHeight,
                            (hit.IsCounter ? "↩-" : "-") + hit.Amount, StatDownColor, ClashFontSize).Forget();
                        if (!burst)
                            BattleEffectFx.PlayImpact(hitEt.ImpactAnchor, BattleCastArrival.ManaColor(ManaType.Red));
                    }
                    else
                        FloatTextAsync(hitEt.FloatParent, hitEt.FloatHeight,
                            (hit.IsCounter ? "↩-" : "-") + hit.Amount, StatDownColor).Forget();
                }
                await UniTask.Delay(340); // 命中后缓冲（同编舞 AttackBeatMs 尾段）
                if (moved) await ArcMoveAsync(wrapper, wrapper.position, home, 0.7f, 13f);
                if (guardWrapper != null) // 守卫归位（L3-1 尾段：挡完回原格）
                    await ArcMoveAsync(guardWrapper, guardWrapper.position, guardHome, 0.7f, 13f);
            }
            finally { EndVisual(); }
        }

        /// <summary>受击退让（L3-2·反击/溅射走位近似）：受击者沿攻击来向快速被推 0.35 格再弹回
        ///（180ms 往返，fire-and-forget 不占节拍）；wrapper 中途销毁自动中止。</summary>
        private static async UniTaskVoid RecoilAsync(Transform t, Vector3 attackFrom)
        {
            if (t == null) return;
            Vector3 home = t.position;
            Vector3 dir = home - attackFrom;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) return;
            Vector3 pushed = home + dir.normalized * 0.35f;
            const float half = 0.09f;
            float t0 = Time.realtimeSinceStartup;
            while (t != null)
            {
                float k = Mathf.Clamp01((Time.realtimeSinceStartup - t0) / (half * 2f));
                // 去程快回程缓：k<0.5 推出、k>=0.5 弹回
                float bend = k < 0.5f ? k * 2f : (1f - k) * 2f;
                t.position = Vector3.Lerp(home, pushed, bend);
                if (k >= 1f) break;
                await UniTask.Yield();
            }
            if (t != null) t.position = home;
        }

        /// <summary>sin 半弧位移（编舞飞行同数学：拔起-掠行-落定；realtime 驱动——不受 Time.timeScale 影响）。</summary>
        private static async UniTask ArcMoveAsync(Transform t, Vector3 from, Vector3 to, float height, float speed)
        {
            if (t == null) return;
            float dist = Vector3.Distance(from, to);
            float dur = dist <= 0.05f ? 0.05f : dist / speed;
            float t0 = Time.realtimeSinceStartup;
            while (t != null)
            {
                float k = Mathf.Clamp01((Time.realtimeSinceStartup - t0) / dur);
                t.position = Vector3.Lerp(from, to, k) + Vector3.up * (height * 4f * k * (1f - k));
                if (k >= 1f) break;
                await UniTask.Yield();
            }
        }
    }
}
