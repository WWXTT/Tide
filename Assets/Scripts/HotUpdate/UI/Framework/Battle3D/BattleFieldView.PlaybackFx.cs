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
            var src = castCell != null
                ? CastingAnchor(castCell.Value.x, castCell.Value.z) + Vector3.up * 0.35f
                : ResolveFxSourceAnchor(op.Source);
            var mana = (op.TintOf ?? ManaType.Gray);
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

        /// <summary>回复（ref 版·镜像 PlayEffectHealAsync）：绿火花+「+N」浮字；单位目标入串行链。</summary>
        public async UniTask PlayHealByRefAsync(BattleFxOp op)
        {
            if (op.Amount <= 0) return;
            var et = ResolveFxTarget(op?.Target);
            if (et == null) return;
            BeginVisual();
            try
            {
                if (et.Unit != null) await RunUnitFxAsync(op.Target.Rid, () => HealRefCoreAsync(et, op.Amount));
                else await HealRefCoreAsync(et, op.Amount);
            }
            finally { EndVisual(); }
        }

        private async UniTask HealRefCoreAsync(EffectTarget et, int amount)
        {
            Vector3 ground = et.Unit != null && et.Unit.Wrapper != null
                ? et.Unit.Wrapper.position
                : et.FloatParent != null ? et.FloatParent.position : et.ImpactAnchor;
            bool healed = false;
            if (BattleVfxFx.Enabled)
            {
                try { healed = BattleVfxFx.TryPlayHeal(ManaType.Green, ground + Vector3.up * SparkLift, 1f); }
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
            var et = destroyFamily ? ResolveFxTarget(card) : null;
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

        /// <summary>赋予爆发粒子（ref 版·CounterBurst/KeywordBurst 共口·镜像 PlayGrantBurstAsync：
        /// 减益=Hit 族/增益=Heal 族，颜色=捕获 Tint）。</summary>
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
                try { played = op.Flag ? BattleVfxFx.TryPlayHit(mana, at, 1f) : BattleVfxFx.TryPlayHeal(mana, at, 1f); }
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
                var color = ManaType.Gray;
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

        // ======================================== 战斗演出回放（CombatShotPlayer v1） ========================================

        /// <summary>战斗演出回放（方案 §2.2 CombatShot v1）：攻击者飞向目标临格（sin 半弧）→攻击前扑→
        /// 目标受击+逐笔伤害浮字（反击笔挂攻击者）→幸存者飞回。守卫闪现/反击/溅射走位不还原（已知差异）；
        /// 死亡退场归 Death 指令（PreOps/Ops）；角色攻击者原地不动只播目标侧受击。占视觉门。</summary>
        public async UniTask PlayCombatShotAsync(CombatShot shot)
        {
            if (shot?.Attacker == null || shot.Target == null) return;
            var attEt = ResolveFxTarget(shot.Attacker);
            var tgtEt = ResolveFxTarget(shot.Target);
            // 目标锚兜底：目标不在视图（已死被收/未建）→目标格地面锚（格坐标由捕获携带）
            Vector3 tgtAnchor = tgtEt?.ImpactAnchor
                ?? (shot.TargetX >= 0 ? CastingAnchor(shot.TargetX, shot.TargetZ) + Vector3.up * 0.8f : Vector3.zero);
            if (tgtEt == null && shot.TargetX < 0) return;

            BeginVisual();
            try
            {
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
                await UniTask.Delay(180); // 攻击前扑到命中节拍（同编舞 ImpactDelayMs）
                // 逐笔命中：主击挂目标、反击笔挂攻击者（就地浮字+受击拍）
                for (int i = 0; i < shot.Hits.Count; i++)
                {
                    var hit = shot.Hits[i];
                    var hitEt = ResolveFxTarget(hit.Target);
                    if (hitEt == null) continue;
                    if (hitEt.Unit != null && !hitEt.Unit.DeathExitStarted) hitEt.Unit.Anim?.Play(MonsterAction.Hit);
                    else if (hitEt.Unit == null) HeroImpactPulseAsync(hitEt.HeroOwner).Forget();
                    FloatTextAsync(hitEt.FloatParent, hitEt.FloatHeight,
                        (hit.IsCounter ? "↩-" : "-") + hit.Amount, StatDownColor).Forget();
                }
                await UniTask.Delay(340); // 命中后缓冲（同编舞 AttackBeatMs 尾段）
                if (moved) await ArcMoveAsync(wrapper, wrapper.position, home, 0.7f, 13f);
            }
            finally { EndVisual(); }
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
