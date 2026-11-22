using System;
using CardCore;
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
    /// 生命周期：BattleScreen.OnEnter 创建、OnExit 释放（CombatChoreographer 同款纯 C# 组合件，
    /// 不上预制体）。本地/联机/复盘同一事件流（网络局与回放的事件经同一 EventManager 合成发布），
    /// 回放快进的机关枪由 AudioService 的 0.1s unscaled 节流闸兜住（§4.3）。
    /// 高频事件（LifeChangeEvent 等）刻意不订（§4.3 防刷）。
    /// </summary>
    public sealed class BattleAudioBridge : IDisposable
    {
        private readonly Func<Player> _self; // 本地座次（判胜负；网络局=客户端视角 P1）
        private bool _disposed;
        /// <summary>角色死亡所在帧（CardDestroy 同帧只播最强一档，§4.3）。</summary>
        private int _heroDeathFrame = -1;

        public BattleAudioBridge(Func<Player> selfPlayer)
        {
            _self = selfPlayer;
            var bus = EventManager.Instance;
            bus.Subscribe<CardDrawEvent>(OnCardDraw);
            bus.Subscribe<CombatDamageEvent>(OnCombatDamage);
            bus.Subscribe<AtomicDamageEvent>(OnAtomicDamage);
            bus.Subscribe<CardDestroyEvent>(OnCardDestroy);
            bus.Subscribe<RoleDeathEvent>(OnRoleDeath);
            bus.Subscribe<GameOverEvent>(OnGameOver);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            var bus = EventManager.Instance;
            bus.Unsubscribe<CardDrawEvent>(OnCardDraw);
            bus.Unsubscribe<CombatDamageEvent>(OnCombatDamage);
            bus.Unsubscribe<AtomicDamageEvent>(OnAtomicDamage);
            bus.Unsubscribe<CardDestroyEvent>(OnCardDestroy);
            bus.Unsubscribe<RoleDeathEvent>(OnRoleDeath);
            bus.Unsubscribe<GameOverEvent>(OnGameOver);
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
            var self = _self?.Invoke();
            bool win = self != null && e.Winner == self;
            AudioService.PlaySfx(win ? SfxId.Win : SfxId.Lose);
            AudioService.PlayOutro(1.5f); // 终局淡出/outro（§4.4；回菜单由场景表接 menu 曲）
        }
    }
}
