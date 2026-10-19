using System;
using Cysharp.Threading.Tasks;
using Tide.HotUpdate;
using UnityEngine;
using UnityEngine.VFX;

namespace SynergyUI
{
    /// <summary>
    /// 效果命中表现件（2026-10-19 Arctibyte 资源接入）：效果伤害的 弹体(MagicOrb)→爆点(Impact)，
    /// 回复/增益的彩色火花（复用 Dissolve Template——BattleCastArrival 同资产同配方）。
    /// 纯静态播放器：锚点解析/视觉门/浮字/受击动画由 BattleFieldView.PlayEffectXxx 编排（口径对齐
    /// BattleCastArrival 的资产纪律：HotUpdateAssets.Load 双址、失败一次告警恒降级不刷屏、
    /// 缺件=调用方保底继续，绝不悬挂演出态）。
    /// 资产来源：Assets/Arctibyte/Magio/Demo/Common/DemoVFX 两枚纯 VFXGraph（零脚本依赖零暴露属性）
    /// 拷入 Assets/Art/VFX（收集器只收 Assets/Art；依赖子图/贴图由 YooAsset 引用链自动连带进包）。
    /// </summary>
    public static class BattleEffectFx
    {
        private const string OrbAddress = "MagicOrb";
        private const string OrbPath = "Assets/Art/VFX/MagicOrb.vfx";
        private const string ImpactAddress = "Impact";
        private const string ImpactPath = "Assets/Art/VFX/Impact.vfx";
        private const string SparkAddress = "Dissolve Template";
        private const string SparkPath = "Assets/Art/VFX/Dissolve Template.vfx";

        /// <summary>弹体飞行段（源→目标，easeIn 加速冲向目标）。</summary>
        public const float OrbFlySeconds = 0.35f;

        /// <summary>爆点驻留（含余烬尾；fire-and-forget 定时销毁）。</summary>
        public const float ImpactLingerSeconds = 1.0f;

        /// <summary>火花发射期/清理期（回复与指示物增益共用一件短促火花）。</summary>
        private const float SparkEmitSeconds = 0.55f;
        private const float SparkCleanupSeconds = 1.3f;

        private static VisualEffectAsset _orb, _impact, _spark;
        private static bool _tried;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _orb = _impact = _spark = null; _tried = false; } // 域重载关闭防跨 Play 残留

        public static bool OrbReady { get { TryLoad(); return _orb != null; } }
        public static bool ImpactReady { get { TryLoad(); return _impact != null; } }
        public static bool SparkReady { get { TryLoad(); return _spark != null; } }

        private static void TryLoad()
        {
            if (_tried) return;
            _tried = true;
            _orb = HotUpdateAssets.Load<VisualEffectAsset>(OrbAddress, OrbPath);
            _impact = HotUpdateAssets.Load<VisualEffectAsset>(ImpactAddress, ImpactPath);
            _spark = HotUpdateAssets.Load<VisualEffectAsset>(SparkAddress, SparkPath);
            if (_orb == null) Debug.LogWarning($"[EffectFx] {OrbPath} 缺失——伤害弹体退役（直落爆点）");
            if (_impact == null) Debug.LogWarning($"[EffectFx] {ImpactPath} 缺失——命中爆点退役（仅浮字/受击）");
            if (_spark == null) Debug.LogWarning($"[EffectFx] {SparkPath} 缺失——回复/增益火花退役（仅浮字）");
        }

        private static GameObject NewFxRoot(string name, Vector3 worldPos)
        {
            var go = new GameObject(name);
            if (BattleStageDirector.MapRoot != null) go.transform.SetParent(BattleStageDirector.MapRoot, true);
            go.transform.position = worldPos;
            return go;
        }

        /// <summary>弹体飞行（占位协程：transform 驱动 GO，VFX 图粒子随宿主走；抵达即毁）。
        /// color=六色主色（图加 Color 暴露属性后生效——HasProperty 守卫，未改图=图本色）。
        /// 资产缺失/异常兜底=立即返回（调用方径直接爆点）。</summary>
        public static async UniTask PlayOrbAsync(Vector3 from, Vector3 to, Color color)
        {
            if (!OrbReady) return;
            GameObject go = null;
            try
            {
                go = NewFxRoot("fx-orb", from);
                var vfx = go.AddComponent<VisualEffect>();
                vfx.visualEffectAsset = _orb;
                vfx.initialEventName = "OnPlay";
                ApplyColor(vfx, color);
                vfx.Play();
                float t0 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t0 < OrbFlySeconds)
                {
                    float k = (Time.realtimeSinceStartup - t0) / OrbFlySeconds;
                    if (go == null) return; // 拆台/场景卸载
                    go.transform.position = Vector3.Lerp(from, to, k * k); // easeIn：加速冲向目标
                    await UniTask.Yield();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[EffectFx] 弹体异常（兜底=立即爆点）：{ex.Message}");
            }
            finally
            {
                if (go != null) UnityEngine.Object.Destroy(go);
            }
        }

        /// <summary>命中爆点：目标锚点即播即忘（alive 探测进日志供冒烟取证）；color 同弹体六色口径。</summary>
        public static VisualEffect PlayImpact(Vector3 at, Color color)
        {
            if (!ImpactReady) return null;
            var go = NewFxRoot("fx-impact", at);
            var vfx = go.AddComponent<VisualEffect>();
            vfx.visualEffectAsset = _impact;
            vfx.initialEventName = "OnPlay";
            ApplyColor(vfx, color);
            vfx.Play();
            UnityEngine.Object.Destroy(go, ImpactLingerSeconds);
            return vfx;
        }

        /// <summary>六色染色（2026-10-19 六色复用·实测修正）：两图出厂即带颜色暴露，无需改图——
        /// MagicOrb=**Color**（单色），Impact=**Color1（主）+Color2（副）**，两图另有 Size(float)
        ///（暴露名经 Set/Get 回读探针证实——此序列化格式 text-grep 查不到，"零暴露"旧结论作废）。
        /// SetVector4 对未暴露名实测静默忽略→三名全写，各图吃各自暴露名；Color2=提亮副色保层次。</summary>
        private static void ApplyColor(VisualEffect vfx, Color color)
        {
            vfx.SetVector4("Color", color);
            vfx.SetVector4("Color1", color);
            vfx.SetVector4("Color2", Color.Lerp(color, Color.white, 0.55f));
        }

        /// <summary>短促彩色火花（回复=绿/增益可选色）：Dissolve Template + 水平网格烘焙 +
        /// 平面遮罩贴目标脚下（配方同 BattleCastArrival.CreateGroup——该图必须喂网格采样缓冲）。
        /// fire-and-forget：0.55s 停发、1.3s 清理（含 GraphicsBuffer 释放）。</summary>
        public static void PlaySpark(Vector3 at, Color color, float spawnRate = 7000f)
        {
            if (!SparkReady) return;
            Runner runner = null;
            try
            {
                var go = NewFxRoot("fx-spark", at);
                runner = go.AddComponent<Runner>();
                runner.Begin(_spark, at, color, spawnRate);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[EffectFx] 火花启动异常（忽略）：{ex.Message}");
                if (runner != null) UnityEngine.Object.Destroy(runner.gameObject);
            }
        }

        /// <summary>火花生命周期驱动（停发→缓冲释放→自毁；OnDestroy 兜底释放 GraphicsBuffer）。</summary>
        private sealed class Runner : MonoBehaviour
        {
            private TideVfxUniformMeshBaker _baker;
            private Mesh _mesh;
            private VisualEffect _vfx;

            public void Begin(VisualEffectAsset asset, Vector3 at, Color color, float spawnRate)
            {
                var mr = gameObject.AddComponent<MeshRenderer>();
                mr.enabled = false; // 只借网格采样，不可见（同 BattleCastArrival 发射组口径）
                _mesh = BuildHorizontalGrid(1.2f, 16);
                var mf = gameObject.AddComponent<MeshFilter>();
                mf.sharedMesh = _mesh;
                _vfx = gameObject.AddComponent<VisualEffect>();
                _vfx.visualEffectAsset = asset;
                _vfx.initialEventName = "OnPlay";
                _baker = new TideVfxUniformMeshBaker();
                _baker.Bake(_vfx, mr);
                _vfx.SetVector3("Transform_position", at);
                _vfx.SetVector3("Transform_angles", Vector3.zero);
                _vfx.SetVector3("Transform_scale", Vector3.one);
                _vfx.SetBool("Use Plane", true);
                _vfx.SetBool("Use Box", false);
                _vfx.SetBool("Use Ellipse", false);
                _vfx.SetVector4("Mask Position", at);
                _vfx.SetVector4("Mask Scale", new Vector4(1f, 1f, 1f, 0f));
                _vfx.SetVector4("Mask Up", new Vector4(0f, 1f, 0f, 0f));
                _vfx.SetVector4("Mask Right", new Vector4(1f, 0f, 0f, 0f));
                _vfx.SetVector4("Mask Forward", new Vector4(0f, 0f, 1f, 0f));
                _vfx.SetVector4("Color", color);
                var grad = new Gradient();
                grad.SetKeys(
                    new[] { new GradientColorKey(Color.Lerp(color, Color.white, 0.6f), 0f), new GradientColorKey(color, 1f) },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
                _vfx.SetGradient("Color Gradient", grad);
                _vfx.SetFloat("Emission Power", 2.5f);
                _vfx.SetFloat("Spawn Rate", spawnRate);
                _vfx.SetFloat("Spawn Rate Multiplier", 1f);
                _vfx.SetFloat("Scale Multiplier", 2.5f);
                _vfx.Play();
                LifeAsync().Forget();
            }

            private async UniTaskVoid LifeAsync()
            {
                await UniTask.Delay((int)(SparkEmitSeconds * 1000f));
                if (_vfx != null)
                {
                    _vfx.SetFloat("Spawn Rate", 0f); // 图无 OnStop 上下文——停发靠速率归零（同 BattleCastArrival）
                    _vfx.Stop();
                }
                await UniTask.Delay((int)((SparkCleanupSeconds - SparkEmitSeconds) * 1000f));
                if (gameObject != null) Destroy(gameObject);
            }

            private void OnDestroy()
            {
                _baker?.OnDisable();
                if (_mesh != null) Destroy(_mesh);
            }
        }

        /// <summary>水平三角网格（烘焙采样池；XZ 平面居中——抄 BattleCastArrival 同名件缩档 16 段）。</summary>
        private static Mesh BuildHorizontalGrid(float size, int seg)
        {
            var m = new Mesh { name = "fx-spark-grid" };
            int vc = seg + 1;
            var verts = new Vector3[vc * vc];
            for (int iz = 0; iz <= seg; iz++)
                for (int ix = 0; ix <= seg; ix++)
                    verts[iz * vc + ix] = new Vector3(
                        (ix / (float)seg - 0.5f) * size, 0f, (iz / (float)seg - 0.5f) * size);
            var tris = new int[seg * seg * 6];
            int ti = 0;
            for (int iz = 0; iz < seg; iz++)
                for (int ix = 0; ix < seg; ix++)
                {
                    int a = iz * vc + ix, b = a + 1, c = a + vc, d = c + 1;
                    tris[ti++] = a; tris[ti++] = c; tris[ti++] = b;
                    tris[ti++] = b; tris[ti++] = c; tris[ti++] = d;
                }
            m.vertices = verts;
            m.triangles = tris;
            return m;
        }
    }
}
