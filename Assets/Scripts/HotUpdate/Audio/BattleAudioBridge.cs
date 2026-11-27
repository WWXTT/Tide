using System;
using System.Collections.Generic;
using CardCore;
using SynergyUI;
using UnityEngine;
using CombatDamageEvent = CardCore.CombatDamageEvent;
using AtomicDamageEvent = CardCore.Attribute.AtomicDamageEvent;

namespace Tide.HotUpdate
{
    /// <summary>
    /// 战场音频桥（2026-11-22 音效与 BGM 系统计划 §4.3/B2）：订阅引擎事件 → AudioService。
    /// 四组接线：抽牌（CardDrawEvent）/ 命中（CombatDamage + AtomicDamage 按量分档）/
    /// 死亡（CardDestroy=单位、RoleDeath=角色——同帧角色死亡压制单位声）/ 胜负（GameOver——
    /// 按本地座次判胜负 + 终局收尾曲）。
    /// 战斗 BGM 强度（2026-11-27）：<see cref="OnView"/> 读投影器快照判「转激昂」——地牌上限到
    /// 默认成长上限 10，或任一方进生命 ≤7 危险区／对手场面攻击力已够击杀（护甲计入有效生命）；
    /// 单向闩，整局只切一次。视图单源=本地/联机/复盘同一口径（不依赖 Core，回放/无 Core 局可用）。
    /// 生命周期：BattleScreen.OnEnter 创建、OnExit 释放（CombatChoreographer 同款纯 C# 组合件，
    /// 不上预制体）。本地/联机/复盘同一事件流（网络局与回放的事件经同一 EventManager 合成发布），
    /// 回放快进的机关枪由 AudioService 的 0.1s unscaled 节流闸兜住（§4.3）。
    /// 高频事件（LifeChangeEvent 等）刻意不订（§4.3 防刷）。
    /// </summary>
    public sealed class BattleAudioBridge : IDisposable
    {
        /// <summary>激昂触发的地牌上限阈值：地牌槽上限到 10（标准成长曲线默认上限；含 LandCapBonus 加成）。</summary>
        private const int FierceLandCapThreshold = 10;
        /// <summary>激昂触发的生命阈值区上界（任一方生命 ≤7 即入危险区）。</summary>
        private const int FierceLifeThreshold = 7;
        /// <summary>转激昂时舒缓曲淡出/过渡段淡入秒数（0.3s 以内）。</summary>
        private const float FierceFadeSec = 0.25f;

        private readonly Func<Player> _self; // 本地座次（判胜负；网络局=客户端视角 P1）
        private readonly Func<GameCore> _core; // 本地引擎（精确攻击力读 LayerEngine；联机/复盘无 Core=null）
        private bool _disposed;
        /// <summary>角色死亡所在帧（CardDestroy 同帧只播最强一档，§4.3）。</summary>
        private int _heroDeathFrame = -1;
        /// <summary>激昂单向闩（转过后不再回落；跨局随桥重建复位）。</summary>
        private bool _fierce;
        /// <summary>终局已收尾（GameOver 后视图仍可能刷新——不得再覆盖 outro）。</summary>
        private bool _gameOver;

        public BattleAudioBridge(Func<Player> selfPlayer, Func<GameCore> core = null)
        {
            _self = selfPlayer;
            _core = core;
            var bus = EventManager.Instance;
            bus.Subscribe<CardDrawEvent>(OnCardDraw);
            bus.Subscribe<CombatDamageEvent>(OnCombatDamage);
            bus.Subscribe<AtomicDamageEvent>(OnAtomicDamage);
            bus.Subscribe<CardDestroyEvent>(OnCardDestroy);
            bus.Subscribe<RoleDeathEvent>(OnRoleDeath);
            bus.Subscribe<GameOverEvent>(OnGameOver);

            // 进战斗固定从平静起（重开/换局同样复位）；激昂由 OnView 单向切。战斗曲播放权归本桥，
            // ScreenMusic 只负责教学压音量——防从设置/暂停返回触发屏激活时把激昂顶回平静。
            AudioService.PlayMusic(BgmId.BattleCalm);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            AudioService.CancelQueuedMusic(); // 离场作废“等舒缓播完再切激昂”的排队
            var bus = EventManager.Instance;
            bus.Unsubscribe<CardDrawEvent>(OnCardDraw);
            bus.Unsubscribe<CombatDamageEvent>(OnCombatDamage);
            bus.Unsubscribe<AtomicDamageEvent>(OnAtomicDamage);
            bus.Unsubscribe<CardDestroyEvent>(OnCardDestroy);
            bus.Unsubscribe<RoleDeathEvent>(OnRoleDeath);
            bus.Unsubscribe<GameOverEvent>(OnGameOver);
        }

        // ================= 战斗曲强度（平静 → 激昂，单向） =================

        /// <summary>每次视图落地调用（BattleScreen.ApplyProjection 单点）：首帧满足激昂条件即切曲。
        /// 视图是本地/联机/复盘统一输入——联机/复盘无 Core 也照常判定。</summary>
        public void OnView(BattleViewData view)
        {
            if (_fierce || _gameOver || view == null) return;
            if (!TryEscalate(view, out string reason)) return;
            _fierce = true;
            Debug.Log($"[Audio] 战斗曲转激昂：{reason}");
            // 舒缓未必到结尾：剩余 > 过渡段长度则 0.25s 快速淡出立即接过渡；否则等循环播完再接。
            AudioService.PlayMusicAtLoopBoundary(BgmId.BattleFierce, FierceFadeSec);
        }

        /// <summary>激昂判定（2026-11-27 定案，满足任一即触发）：
        /// ① 任一方地牌槽上限 ≥ 10（默认成长上限）；
        /// ② 任一方生命 ≤7；或生命未到 7，但对手场面生物攻击力合计 ≥ 其有效生命
        ///    （有效生命 = 当前生命 + 护甲指示物层数）。
        /// 只数生物（Cardtype.Creature）、排除尸体与 0 攻；不判横置/守卫/穿透，属「威胁读数」不是杀伤模拟。
        /// 攻击力：本地局有 Core 时走 LayerEngine（含连接光环/连续层，与战斗结算同源）；
        /// 联机/复盘无 Core 时回落视图快照的 Power（近似口径）。</summary>
        private bool TryEscalate(BattleViewData v, out string reason)
        {
            reason = null;

            int selfCap = v.Self.LandCap;
            int oppCap = v.Opp.LandCap;
            if (selfCap >= FierceLandCapThreshold)
            {
                reason = $"我方地牌上限 {selfCap} ≥ {FierceLandCapThreshold}";
                return true;
            }
            if (oppCap >= FierceLandCapThreshold)
            {
                reason = $"对手地牌上限 {oppCap} ≥ {FierceLandCapThreshold}";
                return true;
            }

            if (v.Self.Life <= FierceLifeThreshold)
            {
                reason = $"我方生命 {v.Self.Life} ≤ {FierceLifeThreshold}";
                return true;
            }
            if (v.Opp.Life <= FierceLifeThreshold)
            {
                reason = $"对手生命 {v.Opp.Life} ≤ {FierceLifeThreshold}";
                return true;
            }

            var core = _core?.Invoke();
            int selfEffectiveLife = EffectiveLife(v.Self);
            int oppEffectiveLife = EffectiveLife(v.Opp);
            int oppBoardPower = BoardPower(v.OppUnits, core); // 对手场面 → 打我方
            if (oppBoardPower > 0 && oppBoardPower >= selfEffectiveLife)
            {
                reason = $"对手场面攻击力 {oppBoardPower} ≥ 我方有效生命 {selfEffectiveLife}";
                return true;
            }

            int selfBoardPower = BoardPower(v.SelfUnits, core); // 我方场面 → 打对手
            if (selfBoardPower > 0 && selfBoardPower >= oppEffectiveLife)
            {
                reason = $"我方场面攻击力 {selfBoardPower} ≥ 对手有效生命 {oppEffectiveLife}";
                return true;
            }

            return false;
        }

        /// <summary>有效生命 = 当前生命 + 护甲指示物（护甲逐点吸收伤害，按有效生命判击杀线）。</summary>
        private static int EffectiveLife(BattlePlayerView p)
            => Math.Max(0, p.Life) + Math.Max(0, p.Armor);

        /// <summary>场面生物攻击力合计（只算活着的生物；召唤/永久物不计）。
        /// 本地局优先 LayerEngine（含光环/连续层）；无 Core 或卡对象缺失时用视图快照值。</summary>
        private static int BoardPower(List<BattleCardView> units, GameCore core)
        {
            if (units == null) return 0;
            int sum = 0;
            foreach (var u in units)
            {
                if (u == null || u.Type != Cardtype.Creature || u.IsDead) continue;
                int power = core?.LayerEngine != null && u.CoreCard != null
                    ? core.LayerEngine.CalculatePower(u.CoreCard)
                    : (u.Runtime?.Power ?? 0);
                if (power > 0) sum += power;
            }
            return sum;
        }

        private void OnCardDraw(CardDrawEvent e)
        {
            AudioService.PlaySfx(SfxId.Draw);
        }

        private void OnCombatDamage(CombatDamageEvent e)
        {
            if (e == null || e.Damage <= 0) return;
            PlayHit(e.Damage);
        }

        /// <summary>效果伤害（非战斗路径；战斗伤害走 CombatDamageEvent，IsCombatDamage 的
        /// AtomicDamageEvent 是同一次伤害的原子管线副本，跳过防双发）。</summary>
        private void OnAtomicDamage(AtomicDamageEvent e)
        {
            if (e == null || e.Damage <= 0 || e.IsCombatDamage) return;
            PlayHit(e.Damage);
        }

        private static void PlayHit(int damage)
        {
            AudioService.PlaySfx(damage >= AudioTable.Load().hitHeavyThreshold ? SfxId.HitHeavy : SfxId.HitLight);
        }

        private void OnCardDestroy(CardDestroyEvent e)
        {
            // 同帧角色死亡已占最强一档（§4.3：同帧多死只播最强一档）
            if (Time.frameCount == _heroDeathFrame) return;
            AudioService.PlaySfx(SfxId.DeathUnit);
        }

        private void OnRoleDeath(RoleDeathEvent e)
        {
            _heroDeathFrame = Time.frameCount;
            AudioService.PlaySfx(SfxId.DeathHero);
        }

        private void OnGameOver(GameOverEvent e)
        {
            if (e == null) return;
            _gameOver = true; // 终局后视图仍会刷新：锁住激昂判定，避免盖掉 outro
            var self = _self?.Invoke();
            bool win = self != null && e.Winner == self;
            AudioService.PlaySfx(win ? SfxId.Win : SfxId.Lose);
            AudioService.PlayOutro(1.5f); // 终局淡出/outro（§4.4；回菜单由场景表接 menu 曲）
        }
    }
}
