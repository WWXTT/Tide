using System;
using CardCore;
using Cysharp.Threading.Tasks;
using Tide.HotUpdate;
using UnityEngine;

namespace SynergyUI
{
    /// <summary>
    /// 战斗颜色路由 VFX 播放器（Phase 1 2026-10-23）：按颜色+角色查 BattleVfxCatalog 播
    /// Cast/弹体/Hit（Area/Heal 可选）。三包分工——红火/蓝水=PixPlays，绿/白/黑=Epic Toon FX
    ///（黑混 Synty 爆炸），灰=Synty+Hovl 光球；全部走 prefab 粒子，不染 VFXGraph。
    /// 降级纪律（与 BattleEffectFx 同口径）：catalog 缺失/条目未配/并发超限/异常——一律返回
    /// false 由调用方回退 BattleEffectFx 的 MagicOrb+Impact，绝不悬挂演出态、不空引用、
    /// 不双系统同播。召唤演出 BattleCastArrival 独立体系不经本类。
    /// </summary>
    public static class BattleVfxFx
    {
        /// <summary>总开关（验收 c 项：false=整链回落 BattleEffectFx）。</summary>
        public static bool Enabled = true;

        /// <summary>粒子密度档（性能预算）：发射速率/burst 乘数统一缩放。</summary>
        public static readonly float DensityScale = 0.5f;

        /// <summary>重 VFX 密度档：PixPlays 火/水按色降一档（0.35）。</summary>
        private const float PixPlaysDensityScale = 0.35f;

        /// <summary>并发上限：同时在途的粒子实例数（含飞行弹体与驻留爆点）。</summary>
        private const int MaxConcurrent = 8;

        private const string CatalogAddress = "Art/VFX/BattleVfxCatalog";
        private const string CatalogPath = "Assets/Art/VFX/BattleVfxCatalog.prefab";

        private static BattleVfxCatalog _catalog; // prefab 上的组件（Find 只读 entries，不实例化本体）
        private static bool _tried;
        private static int _active;
        private static bool _loggedFailure;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() // 域重载关闭防跨 Play 残留（同 BattleEffectFx 口径）
        {
            _catalog = null;
            _tried = false;
            _active = 0;
            _loggedFailure = false;
        }

        /// <summary>catalog 就绪（首访触发加载；失败只告警一次，恒 Ready=false 走降级）。</summary>
        public static bool Ready
        {
            get { TryLoad(); return _catalog != null; }
        }

        private static void TryLoad()
        {
            if (_tried) return;
            _tried = true;
            var prefab = HotUpdateAssets.Load<GameObject>(CatalogAddress, CatalogPath, false);
            if (prefab != null) _catalog = prefab.GetComponent<BattleVfxCatalog>();
            if (_catalog == null)
                Debug.LogWarning($"[BattleVfxFx] {CatalogPath} 缺失或未挂 BattleVfxCatalog——颜色路由 VFX 整链降级 BattleEffectFx");
        }

        /// <summary>调用方侧异常记账口（BattleFieldView 接入点 try/catch 走此——同一次会话只记一次）。</summary>
        public static void NoteFailure(Exception ex) => LogFailureOnce(ex);

        private static void LogFailureOnce(Exception ex)
        {
            if (_loggedFailure) return;
            _loggedFailure = true;
            Debug.LogWarning($"[BattleVfxFx] 路由 VFX 异常（后续同类静默返回 false 走降级）：{ex}");
        }

        private static float DensityFor(ManaType color)
            => color == ManaType.Red || color == ManaType.Blue ? PixPlaysDensityScale : DensityScale;

        private static bool TryTakeSlot()
        {
            if (_active >= MaxConcurrent) return false;
            _active++;
            return true;
        }

        /// <summary>并发槽随寿命到点归还（与 Destroy(go, life) 同拍；计数器口径=纯数字，拆台早毁不影响）。</summary>
        private static async UniTaskVoid ReleaseSlotAfter(float seconds)
        {
            await UniTask.Delay((int)(seconds * 1000f));
            if (_active > 0) _active--;
        }

        /// <summary>实例化并定位（MapRoot 缺失=未装台/预览台场景，挂场景根不报错）。</summary>
        private static GameObject Spawn(VfxEntry e, Vector3 at, float scale, Quaternion rot)
        {
            var go = Instantiate(e.prefab);
            go.name = $"fx-route-{e.color}-{e.role}";
            if (BattleStageDirector.MapRoot != null) go.transform.SetParent(BattleStageDirector.MapRoot, true);
            go.transform.position = at;
            go.transform.rotation = rot;
            go.transform.localScale *= e.scale * scale; // 保留 prefab 自身比例，均匀乘演出档
            return go;
        }

        private static GameObject Instantiate(GameObject prefab)
        {
            // 微缝层：单点收口实例化（未来按需换池化/地址加载不动调用面）
            return UnityEngine.Object.Instantiate(prefab);
        }

        /// <summary>粒子统一调校：全子层级激活+播放；发射/burst 按密度档缩放；maxParticles 封顶 1000。
        /// 兼容演示 prefab 的默认关停子件（PixPlays LocationVfx 子件靠脚本 SetActive）。</summary>
        private static void Tune(GameObject go, float density)
        {
            var systems = go.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
            {
                var ps = systems[i];
                var em = ps.emission;
                em.rateOverTimeMultiplier *= density;
                if (em.burstCount > 0)
                {
                    var bursts = new ParticleSystem.Burst[em.burstCount];
                    em.GetBursts(bursts);
                    for (int b = 0; b < bursts.Length; b++)
                        bursts[b].count = Mathf.Max(1, Mathf.RoundToInt(bursts[b].count.constant * density));
                    em.SetBursts(bursts);
                }
                var main = ps.main;
                if (main.maxParticles > 1000) main.maxParticles = 1000;
                ps.gameObject.SetActive(true);
                ps.Play(true);
            }
        }

        /// <summary>一发即忘类节点（Cast/Hit/Area/Heal 共用）：占槽→生成→按 entry.life 定时销毁。</summary>
        private static bool PlayOneShot(ManaType color, VfxRole role, Vector3 at, float scale)
        {
            if (!Enabled || !Ready) return false;
            try
            {
                var e = _catalog.Find(color, role);
                if (e == null || !TryTakeSlot()) return false;
                var go = Spawn(e, at, scale, Quaternion.identity);
                Tune(go, DensityFor(color));
                UnityEngine.Object.Destroy(go, e.life);
                ReleaseSlotAfter(e.life).Forget();
                return true;
            }
            catch (Exception ex)
            {
                LogFailureOnce(ex);
                return false;
            }
        }

        /// <summary>发动节点：施法者锚点起手（法阵/起手爆，无飞行）。</summary>
        public static bool TryPlayCast(ManaType color, Vector3 at, float scale)
            => PlayOneShot(color, VfxRole.Cast, at, scale);

        /// <summary>命中节点：目标锚点爆点。</summary>
        public static bool TryPlayHit(ManaType color, Vector3 at, float scale)
            => PlayOneShot(color, VfxRole.Hit, at, scale);

        /// <summary>范围节点（Phase 1 可选：消灭族/未来 AOE）。</summary>
        public static bool TryPlayArea(ManaType color, Vector3 at, float scale)
            => PlayOneShot(color, VfxRole.Area, at, scale);

        /// <summary>回复节点（Phase 1 可选：绿 HealBig/白 HealNova）。</summary>
        public static bool TryPlayHeal(ManaType color, Vector3 at, float scale)
            => PlayOneShot(color, VfxRole.Heal, at, scale);

        /// <summary>常驻实例（2026-10-26 牌库标记氛围）：查表实例化+调校，但**不占并发槽、不自动
        /// 销毁**——生命周期归调用方（BattleZoneProps 随装台建/随拆台亡；与 PlayOneShot 一次性
        /// 口径分离）。缺条目/未启用/异常返回 null（调用方降级为无常驻件，不回退一次性件）。</summary>
        public static GameObject TrySpawnPersistent(ManaType color, VfxRole role, Vector3 at, float scale)
        {
            if (!Enabled || !Ready) return null;
            try
            {
                var e = _catalog.Find(color, role);
                if (e == null) return null;
                var go = Spawn(e, at, scale, Quaternion.identity);
                Tune(go, DensityFor(color));
                return go;
            }
            catch (Exception ex)
            {
                LogFailureOnce(ex);
                return null;
            }
        }

        /// <summary>弹体飞行段：easeIn 加速冲向目标（缓动公式同 BattleEffectFx.PlayOrbAsync），
        /// 到达即毁（prefab 弹体=一次性移动件，非 MagicOrb 持续发射——无需尾焰驻留）。
        /// 拆台/场景卸载（go 已销）按已播返 true——调用方照常走命中节点，不触发双兜底。</summary>
        public static async UniTask<bool> TryPlayProjectileAsync(ManaType color, Vector3 from, Vector3 to, float scale)
        {
            if (!Enabled || !Ready) return false;
            GameObject go = null;
            bool slotTaken = false;
            try
            {
                var e = _catalog.Find(color, VfxRole.Projectile);
                if (e == null || !TryTakeSlot()) return false;
                slotTaken = true;
                var dir = to - from;
                var rot = dir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(dir.normalized) : Quaternion.identity;
                go = Spawn(e, from, scale, rot);
                Tune(go, DensityFor(color));
                float fly = BattleEffectFx.OrbFlySeconds; // 0.5s，与旧弹体同拍（导演节奏不改）
                float t0 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t0 < fly)
                {
                    if (go == null) return true;
                    float k = (Time.realtimeSinceStartup - t0) / fly;
                    go.transform.position = Vector3.Lerp(from, to, k * k); // easeIn：加速冲向目标
                    await UniTask.Yield();
                }
                UnityEngine.Object.Destroy(go);
                go = null;
                return true;
            }
            catch (Exception ex)
            {
                LogFailureOnce(ex);
                return false;
            }
            finally
            {
                if (go != null) UnityEngine.Object.Destroy(go);
                if (slotTaken && _active > 0) _active--; // 槽随弹体到站归还（并发口径=在途件数）
            }
        }
    }
}
