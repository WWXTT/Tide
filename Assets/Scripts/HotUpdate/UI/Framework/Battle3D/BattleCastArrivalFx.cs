using System;
using System.Collections;
using System.Collections.Generic;
using CardCore;
using GameBoard;
using Tide.HotUpdate;
using UnityEngine;
using UnityEngine.VFX;

namespace SynergyUI
{
    /// <summary>
    /// 出场浮现 v2（2026-10-15 深夜二改·用户定案架构）：
    /// 消融=Tide/YGOMonster shader 世界Y分支（噪声边+元素色淡光边）——MPB 按渲染器驱真模型，
    /// 按片元世界坐标比较=多模型共享 shader 零串染；影子=隐形合并网格仅供 VFX 粒子出生定位
    ///（Transform 三件套手写不走 INab Binder；INab 材质/InteractiveEffect 全退役）。
    ///
    /// 节拍：t=0 各色地格起粒子 → 0.15-1.0s 消融线自模型世界顶 easeOut 下沉（MPB+影子遮罩同 Y 驱动）
    /// → 0.45s 遮罩携火花群自地格(抬升0.6m)飞向发动位（1s 抵达）→ 1-2s 消融浮现 → t=2 MPB 复位+onArrived → t≈2.6 清理。
    /// 地格发射组=每个实付颜色一个 VisualEffect（Dissolve Template+Plane 遮罩，烘焙三角形采样）。
    /// 颜色口径=ElementPoolPayEvent.PaidCost（实付，BattleScreen 侧组装），浓度 ∝ 费用。
    /// 兜底：资产缺失/账单为空/启动异常/拆台一律立即回调 onArrived+清 MPB——绝不悬挂消融态单位。
    /// </summary>
    public static class BattleCastArrival
    {
        // 2026-10-15 深夜定案（用户 cube 复现实证 alive=24233）：Attract Sparks Dissolve 变体对非雕像网格
        // 有未查明门控（任意遮罩/速率均 alive=0）；正主=Dissolve Template + Plane 遮罩——同样暴露
        // Attract To Mask Center/Attraction Strength/Color/Color Gradient/Emission Power，视觉能力不减。
        private const string VfxAddress = "Dissolve Template";
        private const string VfxPath =
            "Assets/Art/VFX/Dissolve Template.vfx"; // 2026-10-15 保 GUID 迁出 INab（插件移除）

        /// <summary>模型显现时刻=表演总时长（2026-10-15 用户定案 2 秒：1s 地牌飞行 + 1s 浮现）。</summary>
        public const float RevealSeconds = 2.0f;
        private const float LandPhaseSeconds = 1.0f;   // 确定后 1s：地牌粒子飞抵发动位
        private const float LandHoverSeconds = 0.45f;  // 飞行前半：遮罩滞留地格上空聚量
        private const float StopEmitSeconds = 1.0f;    // 地格组随飞行到位停发
        private const float CleanupSeconds = 2.6f;     // 发射组清理
        private const float LandSpawnLift = 0.6f;      // 地格出生抬升（清地块遮挡——贴地火花被六棱柱挡）
        private const float AttractionStrength = 12f;  // 拉火花随遮罩飞向发动位
        // 2026-10-18 用户实测验收值：÷5 减档（6000/24000）被否决——浓度肉眼不可见，恢复原值勿再降；
        // 移动端若需减压走分级配置/容量下调（capacity 只省内存不改观感），须用户重新验收
        private const float SpawnRatePerCost = 30000f; // 每点费用发射率
        private const float SpawnRateCap = 120000f;

        /// <summary>出场粒子总闸。2026-10-18 深夜曾临时停用：真根因=坏导入期（INabCommon 缺失+切目标撞车）
        /// 把 .vfx 资产撕裂写伤（主图对 Uniform Mesh Sample 子图依赖被置 {fileID: 0}+槽位重编号），
        /// 生成 HLSL 引用未声明 _Position → Initialize compute 编译失败 → "Kernel at index (0) is invalid"
        /// （该编译错误只落 Editor.log 不进 Console）。终修=VFX 目录还原 HEAD+VFXCompat 垫片后强制重导入，零错误。</summary>
        public static bool ParticlesEnabled = true;

        private static VisualEffectAsset _asset;
        private static bool _assetTried;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _asset = null; _assetTried = false; } // 域重载关闭防跨 Play 残留

        /// <summary>入场演出。paid 键=ManaType 下标、值=该色实付量；空/总量≤0/资产缺失=不播（立即回调）。
        /// modelTemplate=入场模型根（拼装预制体亦可）：运行时合并全部子网格做"幽灵替身"——
        /// 幽灵配 INab 消融材质（真实模型材质零改动），遮罩自上而下扫=模型自下而上显形，
        /// t=1s 销毁幽灵换真模型（两问题方案：①多 mesh 预制体→合并 ②模型 shader 不支持消融→替身承担）。
        /// onArrived 恰好一次（t=RevealSeconds 或兜底路径），调用方回调内自行判空销毁。</summary>
        public static void Play(uint runtimeId, int seat, IReadOnlyDictionary<int, float> paid,
            Vector3 targetWorld, GameObject modelTemplate, Action onArrived)
        {
            if (onArrived == null) return;
            if (!TryLoadAsset()) { Debug.LogWarning("[CastArrival] Play 跳过：特效资产未载入"); onArrived(); return; }
            if (paid == null || paid.Count == 0) { onArrived(); return; }

            float total = 0f;
            foreach (var v in paid.Values) total += Mathf.Max(0f, v);
            if (total <= 0.0001f) { onArrived(); return; }

            var root = new GameObject("cast-arrival-" + runtimeId % 10000);
            var runner = root.AddComponent<Runner>();
            try
            {
                runner.Begin(seat, paid, total, targetWorld, modelTemplate, onArrived, _asset);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CastArrival] 启动异常（rid={runtimeId}）——立即显现兜底：{ex}");
                UnityEngine.Object.Destroy(root);
                onArrived();
            }
        }

        private static bool TryLoadAsset()
        {
            if (_asset != null) return true;
            if (_assetTried) return false;
            _assetTried = true;
            _asset = HotUpdateAssets.Load<VisualEffectAsset>(VfxAddress, VfxPath);
            if (_asset == null)
                Debug.LogWarning($"[CastArrival] {VfxPath} 缺失——出场粒子整体退役（模型即时显现）");
            return _asset != null;
        }

        /// <summary>每色发射组：地格代理网格 + VisualEffect + 一次性烘焙。</summary>
        private sealed class EmitterGroup
        {
            public GameObject Go;
            public VisualEffect Vfx;
            public TideVfxUniformMeshBaker Baker;
            public Mesh Mesh;
            public Vector3 From; // 地格位（遮罩漂移起点）
        }

        private sealed class Runner : MonoBehaviour
        {
            private const float GhostStartSeconds = 1.0f;   // 浮现开始=地牌飞行段结束（1+1=2）
            private const float GhostSweepSeconds = 1.0f;   // 消融线扫完=模型完全显形

            private readonly List<EmitterGroup> _groups = new List<EmitterGroup>();
            private GameObject _root;
            private Action _onArrived;
            private Vector3 _target;
            private bool _arrived;

            // 模型消融+粒子影子（2026-10-15 v2：世界Y消融=Tide/YGOMonster shader 分支，MPB 驱动真模型；
            // 影子=隐形合并网格仅供 VFX 出生定位，Transform 三件套手写不走 INab Binder）
            private GameObject _ghostGo;
            private VisualEffect _ghostVfx;
            private TideVfxUniformMeshBaker _ghostBaker;
            private Mesh _ghostMesh;
            private Renderer[] _modelRenderers;
            private MaterialPropertyBlock _mpb;
            private Bounds _modelBounds;              // 真模型渲染器合并世界包围盒
            private float _dissolveStartY, _dissolveEndY; // 消融线行程（用户规范：-0.5 → 模型顶+0.5，上行=自下而上浮现）
            private Color _edgeColor = new Color(0f, 0f, 0f, 0f);

            public void Begin(int seat, IReadOnlyDictionary<int, float> paid, float total,
                Vector3 target, GameObject modelTemplate, Action onArrived, VisualEffectAsset asset)
            {
                _root = gameObject;
                _onArrived = onArrived;
                _target = target;
                if (BattleStageDirector.MapRoot != null) // 运行时临时对象收进 Map 总根
                    _root.transform.SetParent(BattleStageDirector.MapRoot, true);

                var dominant = DominantChromatic(paid); // 消融边+全部粒子统一主色（无三色则灰）
                if (ParticlesEnabled)
                {
                    foreach (var kv in paid)
                    {
                        float amount = Mathf.Max(0f, kv.Value);
                        if (amount <= 0.0001f) continue;
                        var element = (ManaType)kv.Key;
                        var (x, z) = BoardLayout.LandCellOfElement(seat, element);
                        if (x < 0) continue;
                        var g = CreateGroup(asset, element, amount, total,
                            BattleBoardGrid.WorldPositionOf(x, z), target, dominant);
                        if (g == null) continue;
                        _groups.Add(g);
                    }
                }

                CreateGhost(modelTemplate, paid, asset);

                if (_groups.Count == 0 && _modelRenderers == null)
                {
                    Arrive();
                    Destroy(_root);
                    return;
                }
                StartCoroutine(Timeline());
            }

            /// <summary>模型消融+粒子影子（v2）：真模型 MPB 挂 _DissolveY（初始=世界顶+0.2 全消融不可见）；
            /// 影子=隐形合并网格复制模板世界位姿，仅供 VFX 粒子出生定位。淡光边=实付首色。</summary>
            private void CreateGhost(GameObject template, IReadOnlyDictionary<int, float> paid, VisualEffectAsset asset)
            {
                if (template == null) return;
                _modelRenderers = template.GetComponentsInChildren<Renderer>(true);
                if (_modelRenderers.Length == 0) return;

                _modelBounds = _modelRenderers[0].bounds;
                for (int i = 1; i < _modelRenderers.Length; i++) _modelBounds.Encapsulate(_modelRenderers[i].bounds);
                // 用户规范（2026-10-15）：消融线自 y=0 上行至模型顶+0.5——
                // 线上被裁=自下而上浮现；淡光边/粒子=卡的最高主色（无三色则灰）
                _dissolveStartY = 0f; // 用户定案（2026-10-15）：扫描自 y=0 起（非 -0.5）
                _dissolveEndY = _modelBounds.max.y + 0.5f;

                _edgeColor = DominantChromatic(paid);
                _edgeColor.a = 0.9f;

                _mpb = new MaterialPropertyBlock();
                ApplyDissolve(_dissolveStartY); // 入场瞬间线在 -0.5=模型全消融不可见

                if (!ParticlesEnabled) return; // 总闸关：消融已配好，影子 VFX 不建（防内核报错刷屏）

                // 影子（粒子出生体）：合并网格 + 隐形渲染器 + VFX 手写属性
                var merged = MergeModelMeshes(template);
                if (merged == null)
                {
                    Debug.LogWarning("[CastArrival] 影子合并 0 网格——模型侧粒子缺失（shader 消融照常）");
                    return;
                }
                _ghostMesh = merged;
                _ghostGo = new GameObject("cast-ghost");
                _ghostGo.transform.SetParent(_root.transform.parent, false); // 与 fx root 同父（Map 总根）
                _ghostGo.transform.SetPositionAndRotation(template.transform.position, template.transform.rotation);
                _ghostGo.transform.localScale = template.transform.lossyScale;
                var mf = _ghostGo.AddComponent<MeshFilter>();
                mf.sharedMesh = merged;
                var mr = _ghostGo.AddComponent<MeshRenderer>();
                mr.enabled = false; // 只借网格采样，不可见

                _ghostVfx = _ghostGo.AddComponent<VisualEffect>();
                _ghostVfx.visualEffectAsset = asset;
                _ghostVfx.initialEventName = "OnPlay";
                _ghostBaker = new TideVfxUniformMeshBaker();
                _ghostBaker.Bake(_ghostVfx, mr);
                // Transform 三件套手写（勿用 INab Binder——Target 错挂=固定偏移根因）
                _ghostVfx.SetMesh("Mesh Renderer", merged);
                _ghostVfx.SetBool("Use Skinned Mesh", false);
                _ghostVfx.SetVector3("Transform_position", _ghostGo.transform.position);
                _ghostVfx.SetVector3("Transform_angles", _ghostGo.transform.eulerAngles);
                _ghostVfx.SetVector3("Transform_scale", _ghostGo.transform.lossyScale);
                _ghostVfx.SetBool("Use Plane", true);
                _ghostVfx.SetBool("Use Box", false);
                _ghostVfx.SetBool("Use Ellipse", false);
                _ghostVfx.SetVector4("Mask Up", new Vector4(0f, 1f, 0f, 0f));
                _ghostVfx.SetVector4("Mask Right", new Vector4(1f, 0f, 0f, 0f));
                _ghostVfx.SetVector4("Mask Forward", new Vector4(0f, 0f, 1f, 0f));
                _ghostVfx.SetVector4("Mask Scale", new Vector4(1f, 1f, 1f, 0f));
                _ghostVfx.SetFloat("Spawn Rate", 50000f); // 模型侧表面火花（用户反馈浓度偏低，2万→5万）
                _ghostVfx.SetFloat("Spawn Rate Multiplier", 1f);
                _ghostVfx.SetFloat("Emission Power", 2f);
                _ghostVfx.SetFloat("Scale Multiplier", 3f); // 粒子×3（用户定案）
                _ghostVfx.SetVector4("Color", _edgeColor);
                _ghostVfx.SetGradient("Color Gradient", BuildGradient(_edgeColor));
                SetGhostMaskY(_dissolveStartY);
            }

            /// <summary>真模型 MPB 消融驱动（Tide/YGOMonster 世界Y分支）。</summary>
            private void ApplyDissolve(float worldY)
            {
                if (_modelRenderers == null) return;
                _mpb.SetFloat("_DissolveY", worldY);
                _mpb.SetFloat("_DissolveBand", 0.12f);
                _mpb.SetColor("_DissolveEdgeColor", _edgeColor);
                for (int i = 0; i < _modelRenderers.Length; i++)
                    if (_modelRenderers[i] != null) _modelRenderers[i].SetPropertyBlock(_mpb);
            }

            /// <summary>消融复位（模型完全显现）——t=1 显现与异常/拆台兜底共用。</summary>
            private void ClearDissolve()
            {
                if (_modelRenderers == null) return;
                for (int i = 0; i < _modelRenderers.Length; i++)
                    if (_modelRenderers[i] != null) _modelRenderers[i].SetPropertyBlock(null);
            }

            /// <summary>影子 VFX 遮罩平面世界 Y（与 shader 消融线同值=粒子带对齐分界线）。</summary>
            private void SetGhostMaskY(float worldY)
            {
                if (_ghostVfx == null) return;
                var c = _modelBounds.center;
                _ghostVfx.SetVector4("Mask Position", new Vector4(c.x, worldY, c.z, 0f));
            }

            /// <summary>拼装预制体→单网格：全部子 MeshFilter 按世界矩阵合到模型根本地系（可读网格逐 submesh 合并）。</summary>
            private static Mesh MergeModelMeshes(GameObject template)
            {
                var filters = template.GetComponentsInChildren<MeshFilter>(false);
                var list = new List<CombineInstance>();
                int skipped = 0;
                var root = template.transform;
                foreach (var f in filters)
                {
                    var m = f.sharedMesh;
                    if (m == null || !m.isReadable) { skipped++; continue; }
                    for (int s = 0; s < m.subMeshCount; s++)
                        list.Add(new CombineInstance
                        {
                            mesh = m,
                            subMeshIndex = s,
                            transform = root.worldToLocalMatrix * f.transform.localToWorldMatrix,
                        });
                }
                if (list.Count == 0)
                {
                    Debug.LogWarning($"[CastArrival] 幽灵合并 0 网格（不可读 {skipped} 个）——替身跳过");
                    return null;
                }
                var merged = new Mesh { name = "cast-ghost-merged" };
                merged.CombineMeshes(list.ToArray(), true, true);
                merged.RecalculateBounds();
                if (skipped > 0)
                    Debug.Log($"[CastArrival] 幽灵合并跳过不可读网格 {skipped} 个（read/write 未开启）");
                return merged;
            }

            private EmitterGroup CreateGroup(VisualEffectAsset asset, ManaType element, float amount,
                float total, Vector3 from, Vector3 to, Color dominant)
            {
                var go = new GameObject("emit-" + element);
                go.transform.SetParent(transform, false);
                var lifted = from + new Vector3(0f, LandSpawnLift, 0f); // 抬升出生带（清地块遮挡）——网格与遮罩起点同高
                var birth = lifted;
                go.transform.position = birth;

                // 采样网格：水平 24×24（1152 三角形）——烘焙器以三角形数为采样池容量
                var mesh = BuildHorizontalGrid(1.4f, 24);
                var mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.enabled = false; // 只出粒子不出面片（烘焙只读网格数据）
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;

                var vfx = go.AddComponent<VisualEffect>();
                vfx.visualEffectAsset = asset;
                vfx.initialEventName = "OnPlay"; // 图内事件上下文=OnPlay（空串=Play() 空发不启动，alive 恒 -1）

                var baker = new TideVfxUniformMeshBaker();
                baker.Bake(vfx, mr);

                // 网格变换（INab VFXLossyTransformBinder 同口径，代理静态一次性写）
                vfx.SetVector3("Transform_position", birth);
                vfx.SetVector3("Transform_angles", Vector3.zero);
                vfx.SetVector3("Transform_scale", Vector3.one);

                // 平面遮罩（用户 cube 配方实证：Dissolve Template+Plane 任意网格可出生）：
                // 出生时贴地格，前段漂向目标格（溶解前沿扫过代理表面=火花自地格涌出）；
                // Attract 拉火花跟遮罩中心走
                vfx.SetBool("Use Plane", true);
                vfx.SetBool("Use Box", false);
                vfx.SetBool("Use Ellipse", false);
                vfx.SetVector4("Mask Position", from);
                vfx.SetVector4("Mask Scale", new Vector4(1f, 1f, 1f, 0f));
                vfx.SetVector4("Mask Up", new Vector4(0f, 1f, 0f, 0f));
                vfx.SetVector4("Mask Right", new Vector4(1f, 0f, 0f, 0f));
                vfx.SetVector4("Mask Forward", new Vector4(0f, 0f, 1f, 0f));
                vfx.SetBool("Attract To Mask Center", true);
                vfx.SetFloat("Attraction Strength", AttractionStrength);

                var color = dominant; // 统一主色（element 仅决定出发地格）
                vfx.SetVector4("Color", color); // 本版 VisualEffect 无 SetColor——Color 属性走 Vector4 隐式转换
                vfx.SetGradient("Color Gradient", BuildGradient(color));
                vfx.SetFloat("Emission Power", 2.5f);
                vfx.SetFloat("Spawn Rate", Mathf.Min(SpawnRatePerCost * amount, SpawnRateCap)); // 浓度 ∝ 该色实付量
                vfx.SetFloat("Spawn Rate Multiplier", 1f);
                vfx.SetFloat("Scale Multiplier", Mathf.Clamp((0.7f + 0.1f * total) * 3f, 2.1f, 4.8f)); // 粒子×3（用户定案）

                vfx.Play();
                return new EmitterGroup { Go = go, Vfx = vfx, Baker = baker, Mesh = mesh, From = lifted };
            }

            private IEnumerator Timeline()
            {
                float t = 0f;
                bool emitting = true;
                bool ghostStarted = false;
                while (t < CleanupSeconds)
                {
                    t += Time.deltaTime;

                    // 地牌飞行段（0→0.8s）：前半遮罩滞留地格上空聚量，后半快速摆到发动位——
                    // 吸力（12）拖着已出生的火花群从地格飞向发动位
                    if (t < LandPhaseSeconds)
                    {
                        float k = t < LandHoverSeconds
                            ? 0f
                            : Mathf.SmoothStep(0f, 1f, (t - LandHoverSeconds) / (LandPhaseSeconds - LandHoverSeconds));
                        for (int i = 0; i < _groups.Count; i++)
                            _groups[i].Vfx.SetVector4("Mask Position",
                                Vector3.Lerp(_groups[i].From, _target, k));
                    }

                    // 模型消融：t=0.8 起线自 -0.5 上行（MPB 驱动真模型 + 影子遮罩同 Y=粒子带对齐分界线）
                    if (_modelRenderers != null)
                    {
                        if (!ghostStarted && t >= GhostStartSeconds)
                        {
                            ghostStarted = true;
                            if (_ghostVfx != null) _ghostVfx.Play();
                        }
                        if (ghostStarted)
                        {
                            float k = Mathf.Clamp01((t - GhostStartSeconds) / GhostSweepSeconds);
                            float y = Mathf.Lerp(_dissolveStartY, _dissolveEndY, 1f - Mathf.Pow(1f - k, 2f)); // 线上行=浮现
                            ApplyDissolve(y);
                            SetGhostMaskY(y);
                        }
                    }

                    if (emitting && t >= StopEmitSeconds)
                    {
                        emitting = false;
                        for (int i = 0; i < _groups.Count; i++)
                        {
                            _groups[i].Vfx.SetFloat("Spawn Rate", 0f); // 图无 OnStop 上下文——停发靠速率归零兜底
                            _groups[i].Vfx.Stop();
                        }
                    }

                    if (!_arrived && t >= RevealSeconds)
                    {
                        ClearDissolve(); // 线沉底=模型完全显现（MPB 复位）
                        if (_ghostVfx != null) _ghostVfx.Stop();
                        Arrive(); // 影子留到 Cleanup 一并拆（尾巴无突兀）
                    }

                    yield return null;
                }

                foreach (var g in _groups)
                {
                    g.Baker.OnDisable(); // 释放 GraphicsBuffer
                    if (g.Mesh != null) Destroy(g.Mesh);
                }
                _groups.Clear();
                _ghostBaker?.OnDisable();
                if (_ghostMesh != null) Destroy(_ghostMesh);
                if (_ghostGo != null) Destroy(_ghostGo);
                ClearDissolve(); // 兜底：异常路径也不留消融态
                if (_root != null) Destroy(_root);
            }

            /// <summary>模型显现回调（恰好一次；正常路径 t=RevealSeconds，兜底路径提前）。</summary>
            private void Arrive()
            {
                if (_arrived) return;
                _arrived = true;
                _onArrived?.Invoke();
            }

            private void OnDestroy()
            {
                // 场景拆台等异常路径：回调兜底一次（回调内已判空销毁）+消融态必清（模型不能卡半消）
                if (!_arrived)
                {
                    _arrived = true;
                    try { _onArrived?.Invoke(); } catch { /* 拆台期回调异常吞掉（回调判空为主） */ }
                }
                ClearDissolve();
                foreach (var g in _groups) g.Baker?.OnDisable();
                _ghostBaker?.OnDisable();
            }
        }

        /// <summary>水平三角网格（烘焙采样池；XZ 平面居中）。</summary>
        private static Mesh BuildHorizontalGrid(float size, int seg)
        {
            var m = new Mesh { name = "cast-arrival-grid" };
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

        /// <summary>卡的最高主色（用户口径 2026-10-15）：实付中红/蓝/绿占比最高者；三色皆无=灰。
        /// 消融淡光边与全部粒子统一用色。</summary>
        private static Color DominantChromatic(IReadOnlyDictionary<int, float> paid)
        {
            ManaType best = ManaType.Gray;
            float bestV = 0f;
            foreach (var kv in paid)
            {
                var m = (ManaType)kv.Key;
                if (m != ManaType.Red && m != ManaType.Blue && m != ManaType.Green) continue;
                if (kv.Value > bestV) { bestV = kv.Value; best = m; }
            }
            return bestV > 0.0001f ? ManaColor(best) : ManaColor(ManaType.Gray);
        }

        /// <summary>六色→粒子色（UiStyle 方格色为基；黑抬亮——纯黑粒子不可见；其余向白提一档自发光观感）。</summary>
        private static Color ManaColor(ManaType m)
        {
            Color c = m switch
            {
                ManaType.Red => UiStyle.DotRed,
                ManaType.Blue => UiStyle.DotBlue,
                ManaType.Green => UiStyle.DotGreen,
                ManaType.Black => UiStyle.DotBlack,
                ManaType.White => UiStyle.DotWhite,
                _ => UiStyle.DotGray,
            };
            if (m == ManaType.Black) return Color.Lerp(c, new Color(0.38f, 0.32f, 0.55f), 0.85f);
            return Color.Lerp(c, Color.white, 0.15f);
        }

        private static Gradient BuildGradient(Color c)
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.Lerp(c, Color.white, 0.6f), 0f), new GradientColorKey(c, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            return g;
        }
    }
}
