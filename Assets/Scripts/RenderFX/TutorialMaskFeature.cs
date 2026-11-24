using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Tide.RenderFX
{
    /// <summary>
    /// 教学遮罩·场景成片缓存（2026-11-21 用户实测口径修正：**不把主相机渲进 RT**——
    /// targetTexture 重定向会动整个渲染路径；新管线正道=后处理脚本定义一块缓存，
    /// 相机照常渲屏，RenderGraph 在 AfterRenderingPostProcessing 把**成片**（含 bloom）拷进缓存，
    /// 教学遮罩链直接取用）。UI 侧 TutorialMaskController 开关 Requested：
    /// false=不挂 pass 零开销；true=每帧把 activeColorTexture 拷入持久 RT（RTHandle·随屏自动 resize）。
    /// 缓存是相机色（3D+后处理，Overlay UI 不在其中）——遮罩图盖在 UI 之上即达成
    /// 「窗外连 UI 一起被盖掉、窗内露出真实 3D+UI」的 v0.3/v0.4 视觉口径。
    /// **第七轮（2026-11-21 深夜）**：缓存不再只是成片——RGB=成片、**alpha=遮罩目标剪影**
    ///（feature 同一 pass 里用 Tide/TutorialSceneCopy 的 Pass 1 只写 alpha），
    /// 遮罩侧直接读这张 alpha 挖洞 → 镂空与成片同源、位置天然对齐（旧独立剪影 RT 已废）。
    /// </summary>
    public static class TutorialMaskSceneCache
    {
        /// <summary>遮罩链要缓存（控制器 Apply 置 true / Clear 置 false）。</summary>
        public static bool Requested;

        /// <summary>成片缓存（feature 每帧写入；null=本帧未产出——控制器等首帧）。RGB=成片，A=目标剪影。</summary>
        public static RenderTexture SceneCopy;

        /// <summary>本帧要挖成镂空的 renderer（控制器逐帧填、feature 在成片 pass 内只写 alpha；
        /// null/空=无洞（目标解析失败→整屏模糊兜底）。动态列表由控制器持有复用，免逐帧 GC。</summary>
        public static IList<Renderer> SilhouetteRenderers;

        /// <summary>域重载关闭防跨 Play 残留（RT 由 feature 持有随销毁，这里只清引用）。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Requested = false;
            SceneCopy = null;
            SilhouetteRenderers = null;
        }
    }

    /// <summary>
    /// 教学遮罩·成片拷贝 RendererFeature：AfterRenderingPostProcessing 挂 raster pass
    ///（全屏 quad·Tide/TutorialSceneCopy Pass 0）把 activeColorTexture 拷进导入的持久 RTHandle，
    /// 同一 pass 再用 **Pass 1（SilhouetteAlpha·ColorMask 0 0 0 1）** 把 TutorialMaskSceneCache
    /// 里的目标 renderer 平涂进 alpha 通道——剪影与成片共用管线矩阵/渲染目标/朝向，
    /// 彻底告别"独立剪影 RT + 手工投影矩阵"时代的翻转/错位（第七轮修复）。
    /// 只在 TutorialMaskSceneCache.Requested 时挂——平时零开销。源经 SetGlobalTexture 绑定
    ///（RasterCommandBuffer 无 per-material TextureHandle 重载，与描边 feature 同套路）。
    /// shader 走序列化引用（随资产进包不经 YooAsset）。注册：DbdURP_Renderer.asset。
    /// </summary>
    [DisallowMultipleRendererFeature("Tutorial Mask Scene Copy")]
    public sealed class TutorialMaskFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader m_CopyShader; // Tide/TutorialSceneCopy（注册资产时烘焙引用）

        private RTHandle _sceneCopy;
        private Material _copyMaterial;
        private Mesh _quad;
        private TutorialMaskCopyPass _pass;

        public override void Create()
        {
            if (_sceneCopy == null)
            {
                // scale=Vector2.one：随相机色同尺寸自动 resize（竖切/改分辨率免手管）
                _sceneCopy = RTHandles.Alloc(Vector2.one, 1, DepthBits.None,
                    GraphicsFormat.R16G16B16A16_SFloat, FilterMode.Bilinear, TextureWrapMode.Clamp,
                    name: "_TutorialSceneCopy");
            }
            if (_copyMaterial == null && m_CopyShader != null)
                _copyMaterial = CoreUtils.CreateEngineMaterial(m_CopyShader);
            if (_quad == null) _quad = BuildFullscreenQuad();
            if (_pass == null) _pass = new TutorialMaskCopyPass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!TutorialMaskSceneCache.Requested || _sceneCopy == null || _copyMaterial == null) return; // 平时零开销
            if (renderingData.cameraData.cameraType != CameraType.Game) return;  // SceneView/Preview 不拷
            _pass.Setup(_sceneCopy, _copyMaterial, _quad, TutorialMaskSceneCache.SilhouetteRenderers);
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            _pass = null;
            CoreUtils.Destroy(_copyMaterial);
            _copyMaterial = null;
            _sceneCopy?.Release();
            _sceneCopy = null;
            TutorialMaskSceneCache.SceneCopy = null;
            if (_quad != null)
            {
                Object.DestroyImmediate(_quad);
                _quad = null;
            }
        }

        /// <summary>全屏 quad（位置即裁剪空间·uv 左下原点）——同 TutorialOutlineFeature 自建套路。</summary>
        private static Mesh BuildFullscreenQuad()
        {
            var mesh = new Mesh { name = "TutorialMaskFullscreenQuad" };
            mesh.vertices = new[]
            {
                new Vector3(-1f, -1f, 0f), new Vector3(1f, -1f, 0f),
                new Vector3(-1f, 1f, 0f), new Vector3(1f, 1f, 0f),
            };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            return mesh;
        }

        private sealed class TutorialMaskCopyPass : ScriptableRenderPass
        {
            private RTHandle _target;
            private Material _copyMaterial;
            private Mesh _quad;
            private IList<Renderer> _silhouettes;
            private static readonly int SrcId = Shader.PropertyToID("_TutorialSceneSrcTex");

            /// <summary>Tide/TutorialSceneCopy 的 Pass 1（剪影只写 alpha）；Pass 0=成片拷贝。</summary>
            private const int SilhouettePass = 1;

            private sealed class PassData
            {
                internal TextureHandle Source;
                internal Material CopyMaterial;
                internal Mesh Quad;
                internal IList<Renderer> Silhouettes;
            }

            internal void Setup(RTHandle target, Material copyMaterial, Mesh quad, IList<Renderer> silhouettes)
            {
                _target = target;
                _copyMaterial = copyMaterial;
                _quad = quad;
                _silhouettes = silhouettes;
                // 成片之后（§3-7：模糊是在成片之后做的——窗外光晕一起糊开是固定代价）
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_target == null || _copyMaterial == null) return;
                var resourceData = frameData.Get<UniversalResourceData>();
                var src = resourceData.activeColorTexture;
                if (!src.IsValid()) return;
                var dst = renderGraph.ImportTexture(_target);

                using (var builder = renderGraph.AddRasterRenderPass<PassData>(
                           "TutorialMask.SceneCopy", out var data, new ProfilingSampler("Tutorial Mask Scene Copy")))
                {
                    data.Source = src;
                    data.CopyMaterial = _copyMaterial;
                    data.Quad = _quad;
                    data.Silhouettes = _silhouettes;
                    builder.SetRenderAttachment(dst, 0, AccessFlags.Write);
                    builder.UseTexture(src, AccessFlags.Read);
                    builder.AllowGlobalStateModification(true); // 源经 SetGlobalTexture 绑定
                    builder.SetRenderFunc((PassData d, RasterGraphContext ctx) =>
                    {
                        ctx.cmd.SetGlobalTexture(SrcId, d.Source);
                        ctx.cmd.DrawMesh(d.Quad, Matrix4x4.identity, d.CopyMaterial, 0, 0);
                        // 剪影：同一渲染目标、同一管线矩阵，只写 alpha（Pass 1）——与成片同源即同位置
                        var silhouettes = d.Silhouettes;
                        if (silhouettes != null)
                            for (int i = 0; i < silhouettes.Count; i++)
                            {
                                var rend = silhouettes[i];
                                if (rend == null) continue; // Unity fake-null（死亡换区/拆台）
                                int subCount = rend.sharedMaterials.Length; // 多材质=多 submesh 全涂（轮廓完整；sharedMaterials 免逐帧分配）
                                for (int sub = 0; sub < subCount; sub++)
                                    ctx.cmd.DrawRenderer(rend, d.CopyMaterial, sub, SilhouettePass);
                            }
                    });
                }
                // 拷贝完成即对 UI 侧可见（Canvas 渲染晚于相机——同帧拿到本帧成片）
                TutorialMaskSceneCache.SceneCopy = _target.rt;
            }
        }
    }
}
