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

        /// <summary>死亡退场演出（战斗编舞与通用死亡同路径；2026-10-17 定案：砍掉滚墓与墓上
        /// 自旋段，退场不再去墓地格）：生物=倒地碎裂（部件抛物线散落）原地停 DeathRestSeconds
        /// 沉降即收；无 rig 单位（结界等）暂无碎裂，整身原地沉降入板面消失。中途复活
        /// （v.Dead 翻回）/拆屏即中止并释放 hold（体型朝向与站位复原交还对账）；退场完
        /// RetireUnit 自清。2026-10-17 可忽略表演定案：**纯装饰不占视觉在途门**——逻辑上卡早已
        /// 入墓（SBA 结算即完成），退场动画期间玩家/AI 照常行动（立即发动墓地相关效果在墓地里
        /// 找得到目标）；调用方 fire-and-forget。</summary>
        public async UniTask PlayDeathExitAsync(uint id, int owner)
        {
            // 可忽略表演：不 BeginVisual——不阻塞 AI 节奏等待/结束回合守卫（战斗主序列/受击等
            // 信息性表演才占门；纯装饰表演此后按此口径分类，预计还有多种）
            if (!_units.TryGetValue(id, out var v) || v.Wrapper == null) return;
                v.DeathExitStarted = true; // 退场开演（效果受击判据让位——战斗/兜底路径同置位）
                HoldUnit(id); // 防 RemoveStale 抢销毁

            // 格面读数同帧消失（中途复活由对账分支恢复显示；2026-10-26 迁格——原头顶数值同款时序）
            v.SetStatsVisible(false);
            bool finished;
            if (v.Anim != null)
            {
                // 生物：碎裂散落（部件脱离层级抛物线落地），固定等 0.5s 沉降即收
                v.Anim.Play(MonsterAction.FallDown);
                finished = await DeadHoldAsync(v, DeathRestSeconds);
            }
            else
            {
                // 结界等无 rig 单位：暂无碎裂，整身沉降入板面后收
                finished = await SinkUnderBoardAsync(v, DeathSinkSeconds);
            }
            if (finished)
            {
                Debug.Log($"[BattleField] 单位 {id % 10000} 死亡退场完成（owner={owner}）");
                RetireUnit(id);
            }
            else ReleaseUnit(id);
            // 可忽略表演：无门配对/无 try 包装——异常自然上抛由调用方 Forget 吞记
        }

        /// <summary>死亡等待：固定时长（复活/销毁即提前返回 false 交还对账）。</summary>
        private static async UniTask<bool> DeadHoldAsync(UnitVisual v, float seconds)
        {
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < seconds)
            {
                if (v.Wrapper == null || !v.Dead) return false;
                await UniTask.Yield();
            }
            return v.Wrapper != null && v.Dead;
        }

        /// <summary>整身沉降入板面（无 rig 单位的死亡退场）：wrapper 缓入下沉（渐快），深度=模型
        /// 顶高+余量（整只没入地面下）；复活竞争即停并复原站位，销毁即停。</summary>
        private static async UniTask<bool> SinkUnderBoardAsync(UnitVisual v, float seconds)
        {
            Vector3 start = v.Wrapper.position;
            float depth = ModelHeightAbove(v) + 0.15f;
            float t0 = Time.realtimeSinceStartup;
            while (v.Wrapper != null && v.Dead)
            {
                float t = (Time.realtimeSinceStartup - t0) / seconds;
                if (t >= 1f) break;
                v.Wrapper.position = start + Vector3.down * (depth * t * t); // 缓入：下沉渐快
                await UniTask.Yield();
            }
            if (v.Wrapper != null && !v.Dead) v.Wrapper.position = start; // 复活竞争：复原站位
            return v.Wrapper != null && v.Dead;
        }

        /// <summary>模型顶相对 wrapper 原点的高度（沉降深度用；取模型子树渲染器包围盒）。</summary>
        private static float ModelHeightAbove(UnitVisual v)
        {
            if (v.Model == null) return 2f;
            bool has = false;
            Bounds bounds = default;
            foreach (var r in v.Model.GetComponentsInChildren<Renderer>())
            {
                if (!has) { bounds = r.bounds; has = true; }
                else bounds.Encapsulate(r.bounds);
            }
            if (!has) return 2f;
            return Mathf.Max(0.5f, bounds.max.y - v.Wrapper.position.y);
        }

        /// <summary>角色受击脉动（2026-10-13 裁定②：角色目标无模型，立柱缩放脉冲表受击）。</summary>
        public async UniTask HeroImpactPulseAsync(int owner)
        {
            BeginVisual(); // 视觉在途门：脉冲期间人机局 AI 等待
            try
            {
                if (!_heroes.TryGetValue(owner, out var v) || v.Root == null) return;
                var baseScale = new Vector3(0.55f, 0.6f, 0.55f);
                float t0 = Time.realtimeSinceStartup;
                const float dur = 0.28f;
                while (v.Root != null && Time.realtimeSinceStartup - t0 < dur)
                {
                    float t = (Time.realtimeSinceStartup - t0) / dur;
                    float pulse = 1f + 0.18f * Mathf.Sin(t * Mathf.PI);
                    v.Root.localScale = new Vector3(baseScale.x * pulse, baseScale.y * (1f - 0.08f * Mathf.Sin(t * Mathf.PI)), baseScale.z * pulse);
                    await UniTask.Yield();
                }
                if (v.Root != null) v.Root.localScale = baseScale;
            }
            finally { EndVisual(); }
        }

        /// <summary>疲劳受击演出（2026-10-17）：立柱红闪脉冲（0.36s，同受击脉冲数学）+ 头顶红色
        /// 「疲劳 -N」浮字上浮渐隐（0.9s，world TMP 一次性建毁）。信息性表演占视觉门（口径同
        /// HeroImpactPulse）——节奏 AI 等演完再动。立柱未建（教学介绍段 SuppressHeroes）静默返回；
        /// 同 owner 重入（连续疲劳）跳过脉冲只补浮字（防双写缩放/颜色）。</summary>
        public async UniTask HeroFatigueHitAsync(int owner, int damage)
        {
            BeginVisual();
            try
            {
                if (!_heroes.TryGetValue(owner, out var v) || v.Root == null) return;

                var floatTxt = CreateStatsText(v.Root, 1.9f); // 立柱 stats（1.3）之上的高位浮字
                if (floatTxt != null)
                {
                    floatTxt.fontSize = 6f;
                    floatTxt.text = "疲劳 -" + damage;
                    floatTxt.color = StatDownColor;
                }

                bool pulse = _fatigueBusy.Add(owner);
                Renderer rend = pulse && v.Root != null ? v.Root.GetComponent<Renderer>() : null;
                Material rendMat = rend != null ? rend.material : null; // 实例一次取用：脉冲循环内直写，免每帧 getter 查找
                Color baseColor = owner == 0 ? P1UnitColor : P2UnitColor;
                var baseScale = new Vector3(0.55f, 0.6f, 0.55f);
                var startLocal = floatTxt != null ? (Vector3)floatTxt.transform.localPosition : Vector3.zero;

                float t0 = Time.realtimeSinceStartup;
                const float pulseDur = 0.36f, totalDur = 0.9f, fadeStart = 0.45f, rise = 0.85f;
                while (Time.realtimeSinceStartup - t0 < totalDur)
                {
                    float t = Time.realtimeSinceStartup - t0;
                    if (pulse && t < pulseDur && v.Root != null)
                    {
                        float wave = Mathf.Sin(t / pulseDur * Mathf.PI);
                        v.Root.localScale = new Vector3(
                            baseScale.x * (1f + 0.18f * wave),
                            baseScale.y * (1f - 0.08f * wave),
                            baseScale.z * (1f + 0.18f * wave));
                        if (rendMat != null) rendMat.SetColor("_BaseColor", Color.Lerp(baseColor, StatDownColor, wave * 0.85f)); // 2026-10-18 角色柱换 URP Lit 克隆实例：红闪直写 _BaseColor
                    }
                    if (floatTxt != null)
                    {
                        float k = Mathf.Clamp01(t / totalDur);
                        // easeOut 上浮 + 后半程渐隐
                        floatTxt.transform.localPosition = startLocal + Vector3.up * (rise * (1f - (1f - k) * (1f - k)));
                        float alpha = t < fadeStart ? 1f : Mathf.Clamp01(1f - (t - fadeStart) / (totalDur - fadeStart));
                        var c = floatTxt.color; c.a = alpha; floatTxt.color = c;
                    }
                    await UniTask.Yield();
                }
                if (pulse)
                {
                    if (v.Root != null)
                    {
                        v.Root.localScale = baseScale;
                        if (rendMat != null) rendMat.color = baseColor;
                    }
                    _fatigueBusy.Remove(owner);
                }
                if (floatTxt != null) Object.Destroy(floatTxt.gameObject);
            }
            finally { EndVisual(); }
        }

    }
}
