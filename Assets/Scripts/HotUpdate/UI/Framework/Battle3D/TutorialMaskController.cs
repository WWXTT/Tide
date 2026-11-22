using System.Collections.Generic;
using Tide.HotUpdate;
using Tide.RenderFX;
using UnityEngine;
using UnityEngine.UI;

namespace SynergyUI
{
    /// <summary>
    /// 教学战场遮罩控制器（2026-11-21 遮罩计划 v0.4·第七轮定稿）：相机照常渲屏（不碰
    /// targetTexture），成片由 Tide.RenderFX.TutorialMaskFeature 拷进 TutorialMaskSceneCache，
    /// 本控制器逐帧跑**一条 blit 链**产出遮罩成品：
    /// · **目标剪影 = 成片缓存的 alpha 通道**（第七轮定稿）：控制器只把本步目标的 renderer
    ///   交给 TutorialMaskSceneCache.SilhouetteRenderers，由 feature 在**拷成片的那一个 pass**
    ///   里平涂（Tide/TutorialSceneCopy Pass 1，只写 alpha）→ 剪影与成片同矩阵/同目标/同朝向，
    ///   **没有独立剪影 RT、没有屏幕矩形换算、没有 uv 翻转分支**（旧做法用 CommandBuffer 手工设
    ///   相机原始投影绘独立 RT，缺 RT 必需的 GPU Y 翻转 → 镂空相对画面上下镜像 = 用户实测
    ///   "镂空位置不对"的根因，已整条拆除）；
    /// · 模糊链：SceneCopy（RGB）→ qA(H) → qB(V) → ×3 迭代小半径（平滑无重影）；
    /// · 合成（Tide/TutorialMask 做 blit）：读成片 alpha 做 9 tap 均值软边，剪影处 alpha=0
    ///   （透明露出真实 3D+UI）、其余=模糊图不透明（连 UI 一起盖掉）→ qFinal（**ARGBHalf 保 alpha**）；
    /// · mask-img 全屏 RawImage 用**默认 UI 材质**直显 qFinal（RT 自带 alpha 洞）。
    /// 点击恢复口（§5.2）：点目标屏幕 AABB（bounds 投影 WindowPx，仅供交互/日志——不参与合成）
    /// → BattlePauseBus.ResumeByWindow。关闭=层隐藏+RT Release。
    /// </summary>
    public static class TutorialMaskController
    {
        private const string BlurMatPath = "Assets/Art/Materials/TutorialBlur.mat";
        private const string MaskMatPath = "Assets/Art/Materials/TutorialMask.mat";
        private const string ImgNodeName = "mask-img";
        /// <summary>像素参考分辨率（§2：px 值按 1920×1080 写、运行时按屏比换算）。</summary>
        private const float RefHeight = 1080f;
        /// <summary>迭代轮数（每轮 H+V 半径 1）：3 轮≈平滑中强模糊。</summary>
        private const int BlurIterations = 3;

        private static TutorialFocusMaskSpec _spec;
        private static Camera _cam;
        private static RenderTexture _qA, _qB, _qFinal; // 1/4 分辨率工作对 + 成品
        private static Material _blurMatH, _blurMatV, _cutMat; // H/V 独立实例 + 合成
        private static RawImage _img;
        private static bool _active;
        private static Rect _windowPx;   // 目标屏幕 AABB（bounds 投影；点击恢复/走查日志用，不参与合成）
        private static bool _windowValidLast = true;    // 有效性转换日志去重
        /// <summary>本帧剪影 renderer 缓冲（复用免 GC；feature 同帧读取，逐帧重填）。</summary>
        private static readonly List<Renderer> _silBuffer = new List<Renderer>(64);

        /// <summary>遮罩在役。</summary>
        public static bool Active => _active;

        /// <summary>目标屏幕 AABB（px·左下原点；zero=目标丢失）。点窗内恢复与验收打点用。</summary>
        public static Rect WindowPx => _active ? _windowPx : Rect.zero;

        /// <summary>域重载关闭防跨 Play 残留（BattleTileFx.ResetStatics 同口径——RT/材质引擎对象
        /// 随场景销毁，这里只清托管引用防 fake-null 误用）。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            TutorialMaskSceneCache.Requested = false;
            TutorialMaskSceneCache.SceneCopy = null;
            TutorialMaskSceneCache.SilhouetteRenderers = null;
            _spec = null;
            _cam = null;
            _qA = _qB = _qFinal = null;
            _blurMatH = _blurMatV = _cutMat = null;
            _img = null;
            _active = false;
            _windowPx = Rect.zero;
            _windowValidLast = true;
            _silBuffer.Clear();
        }

        /// <summary>启用遮罩（导演逐步调用）：目标整步不可解析=false 不启用（只警告不炸）。
        /// 层缺失/相机缺失=false——遮罩跳过，教学流程不断。</summary>
        public static bool Apply(RectTransform root, TutorialFocusMaskSpec spec)
        {
            Clear();
            if (spec == null || string.IsNullOrEmpty(spec.target) || root == null) return false;
            _cam = BattleStageDirector.WorldCam;
            if (_cam == null) { Debug.LogWarning("[TutorialMask] 无世界相机——遮罩跳过"); return false; }
            _img = UiKit.FindDeep(root, ImgNodeName)?.GetComponent<RawImage>();
            if (_img == null)
            {
                Debug.LogWarning($"[TutorialMask] BattleUI.prefab 缺 {ImgNodeName} 层——遮罩跳过");
                return false;
            }
            if (_blurMatH == null)
            {
                var blurAsset = HotUpdateAssets.Load<Material>("TutorialBlur", BlurMatPath);
                var maskAsset = HotUpdateAssets.Load<Material>("TutorialMask", MaskMatPath);
                if (blurAsset == null || maskAsset == null)
                {
                    Debug.LogWarning("[TutorialMask] TutorialBlur/TutorialMask.mat 缺失——遮罩跳过");
                    _img = null;
                    return false;
                }
                _blurMatH = Object.Instantiate(blurAsset); // H/V 独立实例：方向烘死，免逐帧改状态
                _blurMatV = Object.Instantiate(blurAsset);
                _blurMatH.SetVector(UniformDirection, new Vector4(1f, 0f, 0f, 0f));
                _blurMatV.SetVector(UniformDirection, new Vector4(0f, 1f, 0f, 0f));
                _cutMat = Object.Instantiate(maskAsset);   // 开窗合成（blit 用，非 UGUI 材质）
            }
            if (!TutorialTargetResolver.TryResolveBounds(spec.target, out _, out var error))
            {
                Debug.LogWarning($"[TutorialMask] 窗目标不可解析：{error}——遮罩跳过（本步无窗）");
                _img = null;
                return false;
            }

            _spec = spec;
            _img.material = null;    // 默认 UI 材质：qFinal 自带 alpha 洞（Blend SrcAlpha OneMinusSrcAlpha）
            _img.raycastTarget = false;
            _img.gameObject.SetActive(true);
            TutorialMaskSceneCache.Requested = true; // feature 开始逐帧拷成片 + 写目标剪影
            _active = true;
            _windowPx = Rect.zero;
            _windowValidLast = true;
            _silBuffer.Clear();
            TutorialMaskSceneCache.SilhouetteRenderers = _silBuffer;
            Debug.Log($"[TutorialMask] 遮罩启用：目标 {spec.target}（feather={spec.feather} strength={spec.strength}·剪影挖空）");
            return true;
        }

        /// <summary>关闭遮罩：Requested 撤、剪影摘、层隐藏、RT 释放（退出即零开销）。</summary>
        public static void Clear()
        {
            if (!_active && _qA == null && _img == null) { TutorialMaskSceneCache.Requested = false; TutorialMaskSceneCache.SilhouetteRenderers = null; return; }
            TutorialMaskSceneCache.Requested = false;
            TutorialMaskSceneCache.SilhouetteRenderers = null;
            _silBuffer.Clear();
            if (_img != null) { _img.material = null; _img.texture = null; _img.gameObject.SetActive(false); }
            ReleaseRenderTextures();
            if (_blurMatH != null) { Object.Destroy(_blurMatH); _blurMatH = null; }
            if (_blurMatV != null) { Object.Destroy(_blurMatV); _blurMatV = null; }
            if (_cutMat != null) { Object.Destroy(_cutMat); _cutMat = null; }
            _spec = null;
            _img = null;
            _cam = null;
            _active = false;
            _windowPx = Rect.zero;
        }

        /// <summary>逐帧（BattleScreen 更新器驱动）：剪影喂料 + 模糊链 + 合成 + 点目标恢复。</summary>
        public static void Tick()
        {
            if (!_active) return;
            var scene = TutorialMaskSceneCache.SceneCopy;
            if (_cam == null || _spec == null) { Clear(); return; }   // 相机/规格蒸发自清
            if (scene == null) return;                                // 成片缓存首帧未到=等

            // 分辨率/竖切 resize：工作对/成品同批重建
            int qw = Mathf.Max(8, scene.width / 4), qh = Mathf.Max(8, scene.height / 4);
            if (_qFinal == null || _qFinal.width != qw || _qFinal.height != qh)
            {
                ReleaseRenderTextures();
                _qA = new RenderTexture(qw, qh, 0, RenderTextureFormat.DefaultHDR) { name = "tutorial-mask-q-a" };
                _qB = new RenderTexture(qw, qh, 0, RenderTextureFormat.DefaultHDR) { name = "tutorial-mask-q-b" };
                _qFinal = new RenderTexture(qw, qh, 0, RenderTextureFormat.ARGBHalf) { name = "tutorial-mask-final" }; // 保 alpha（DefaultHDR 可能=无 alpha 的 R11G11B10）
                _img.texture = _qFinal;
            }

            // ---- 目标解析：bounds（交互 AABB/日志）+ 剪影 renderer（交 feature 写进成片 alpha） ----
            bool valid = TutorialTargetResolver.TryResolveBounds(_spec.target, out var bounds, out _, verbose: false);
            _windowPx = valid ? TutorialTargetResolver.ScreenRectOf(bounds, _cam, _spec.pad * PxScale) : Rect.zero;
            if (valid != _windowValidLast)
            {
                _windowValidLast = valid;
                Debug.Log(valid
                    ? $"[TutorialMask] 目标在位：屏幕 AABB {_windowPx}（{scene.width}×{scene.height}）"
                    : "[TutorialMask] 目标丢失——全糊兜底（剪影为空）");
            }
            FillSilhouette();

            // ---- 模糊链：降采样带糊 + 迭代小半径 H/V（平滑无重影；strength→首档半径） ----
            float r0 = Mathf.Lerp(1.5f, 3f, Mathf.Clamp01(_spec.strength));
            _blurMatH.SetFloat(UniformRadius, r0);
            _blurMatV.SetFloat(UniformRadius, r0);
            Graphics.Blit(scene, _qA, _blurMatH);   // 降采样即横糊
            Graphics.Blit(_qA, _qB, _blurMatV);     // 纵糊
            _blurMatH.SetFloat(UniformRadius, 1f);
            _blurMatV.SetFloat(UniformRadius, 1f);
            for (int i = 0; i < BlurIterations; i++)
            {
                Graphics.Blit(_qB, _qA, _blurMatH);
                Graphics.Blit(_qA, _qB, _blurMatV);
            }

            // ---- 合成：成片 alpha 里的剪影挖进成品 alpha（blit——UGUI 只显成品） ----
            _cutMat.SetTexture(UniformScene, scene);
            _cutMat.SetFloat(UniformFeatherTexel, Mathf.Max(0f, _spec.feather * PxScale)); // 屏幕px → 成片texel（成片与屏幕 1:1）
            _cutMat.SetFloat(UniformUVFlip, SystemInfo.graphicsUVStartsAtTop ? 1f : 0f);    // 显示空间翻转（D3D/Vulkan 需翻）
            Graphics.Blit(_qB, _qFinal, _cutMat);

            ResumeByWindowClick();
        }

        /// <summary>剪影喂料：本步目标的可见 renderer → 复用缓冲 → 交 feature（同帧的相机 pass 读取）。
        /// 隐藏物体过滤（reveal 藏住的格/单位不挖洞，防幽灵洞；与高亮同口径）。</summary>
        private static void FillSilhouette()
        {
            _silBuffer.Clear();
            if (TutorialTargetResolver.TryResolveRenderers(_spec.target, out var renderers, out _, verbose: false)
                && renderers != null)
            {
                for (int i = 0; i < renderers.Count; i++)
                {
                    var r = renderers[i];
                    if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                    _silBuffer.Add(r);
                }
            }
            TutorialMaskSceneCache.SilhouetteRenderers = _silBuffer; // 空表=无洞（全糊兜底）
        }

        /// <summary>点目标区域内恢复（§5.2 教学暂停 resumeBy 含 window）：点击落在目标屏幕 AABB 即恢复。</summary>
        private static void ResumeByWindowClick()
        {
            if (!BattlePauseBus.Paused || BattlePauseBus.ResumeBy == BattlePauseResume.Confirm) return;
            if (_windowPx.width <= 0f || _windowPx.height <= 0f) return;
            bool pressed = Input.GetMouseButtonDown(0)
                           || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began);
            if (!pressed) return;
            var p = Input.touchCount > 0 ? (Vector2)Input.GetTouch(0).position : (Vector2)Input.mousePosition;
            if (_windowPx.Contains(p))
                BattlePauseBus.ResumeByWindow();
        }

        /// <summary>像素比例（§2：px 值按 1080 高参考换算）。</summary>
        private static float PxScale => Mathf.Max(0.01f, Screen.height / RefHeight);

        private static void ReleaseRenderTextures()
        {
            if (_qA != null) { _qA.Release(); Object.Destroy(_qA); _qA = null; }
            if (_qB != null) { _qB.Release(); Object.Destroy(_qB); _qB = null; }
            if (_qFinal != null) { _qFinal.Release(); Object.Destroy(_qFinal); _qFinal = null; }
        }

        // shader uniform 名（防字符串散落；blit 链的 _MainTex 由 Graphics.Blit 绑源）
        private const string UniformDirection = "_Direction";
        private const string UniformRadius = "_Radius";
        private const string UniformScene = "_SceneTex";
        private const string UniformFeatherTexel = "_FeatherTexel";
        private const string UniformUVFlip = "_UVFlip";
    }
}
