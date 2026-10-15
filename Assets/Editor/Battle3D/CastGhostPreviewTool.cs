using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.VFX;
using SynergyUI;

/// <summary>
/// 影子模型浮现预览 v2（编辑器摆场工具·2026-10-15 深夜二改）：
/// 消融=原 Tide/YGOMonster shader 世界Y分支（MPB 驱动真模型，多模型共享零串染）；
/// 影子=隐形合并网格，仅供 VFX（Dissolve Template）粒子出生定位——Transform 三件套手写
/// （不走 INab Binder，根除 +z 偏移类错位），遮罩 Plane 世界Y与 _DissolveY 同值驱动=粒子带与消融线严格对齐。
/// 菜单：摆场（选中模型）→ 自动扫一遍（编辑态动画 2.5s）→ 还原。全程不进 Play。
/// INab 材质/InteractiveEffect 不再参与（Burn 黄光/编辑态 _PositionVector 不刷新两根因随之退役）。
/// </summary>
public static class CastGhostPreviewTool
{
    private const string SetupMenu = "Tools/Battle3D/影子模型浮现预览（选中模型）";
    private const string ScanMenu = "Tools/Battle3D/影子模型浮现预览（自动扫一遍）";
    private const string RestoreMenu = "Tools/Battle3D/影子模型浮现预览（还原）";
    private const string GhostName = "cast-ghost-preview";
    private const string VfxPath = "Assets/Art/VFX/Dissolve Template.vfx"; // 2026-10-15 保 GUID 迁出 INab
    private const float ScanSeconds = 2.5f;

    private static GameObject _orig;
    private static Renderer[] _renderers;
    private static Bounds _worldBounds;      // 原件渲染器合并世界包围盒
    private static GameObject _ghost;
    private static VisualEffect _ghostVfx;
    private static MaterialPropertyBlock _mpb;
    private static double _scanStart;
    private static bool _scanning;
    private static readonly Color EdgeColor = new Color(1.0f, 0.28f, 0.2f, 0.9f); // 淡光边默认红（观感用，可改）

    [MenuItem(SetupMenu)]
    private static void Setup()
    {
        var sel = Selection.activeGameObject;
        if (sel == null)
        {
            Debug.LogWarning("[GhostPreview] 请先选中场景中的模型（Hierarchy）");
            return;
        }
        if (sel.GetComponentInChildren<MeshFilter>(true) == null)
        {
            Debug.LogWarning($"[GhostPreview] 选中物 {sel.name} 下没有任何 MeshFilter——无法做影子");
            return;
        }
        CleanupGhost(); // 重摆先清旧影子

        _orig = sel;
        _renderers = sel.GetComponentsInChildren<Renderer>(true);
        _worldBounds = ComputeWorldBounds(_renderers);

        // ① 真模型消融：MPB 挂世界Y（用户规范：自 -0.5 上行至模型顶+0.5；初始=-0.5 全消融不可见）
        _mpb = new MaterialPropertyBlock();
        ApplyDissolve(0f);

        // ② 影子=隐形合并网格，仅供 VFX 出生定位
        var merged = MergeModelMeshes(sel);
        _ghost = null; _ghostVfx = null;
        if (merged == null)
        {
            Debug.LogWarning("[GhostPreview] 合并 0 网格——粒子不可用，仅 shader 消融可看（自动扫仍可用）");
        }
        else
        {
            var asset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(VfxPath);
            if (asset == null) Debug.LogWarning($"[GhostPreview] 找不到 {VfxPath}——粒子缺失");
            else
            {
                _ghost = new GameObject(GhostName);
                _ghost.transform.SetPositionAndRotation(sel.transform.position, sel.transform.rotation);
                _ghost.transform.localScale = sel.transform.lossyScale;
                var mf = _ghost.AddComponent<MeshFilter>();
                mf.sharedMesh = merged;
                var mr = _ghost.AddComponent<MeshRenderer>();
                mr.enabled = false; // 只借网格采样，不可见
                _ghostVfx = _ghost.AddComponent<VisualEffect>();
                _ghostVfx.visualEffectAsset = asset;
                _ghostVfx.initialEventName = "OnPlay";
                var baker = new TideVfxUniformMeshBaker();
                baker.Bake(_ghostVfx, mr);
                // Transform 三件套手写（勿用 INab Binder——Target 错挂=固定偏移根因）
                _ghostVfx.SetMesh("Mesh Renderer", merged);
                _ghostVfx.SetBool("Use Skinned Mesh", false);
                _ghostVfx.SetVector3("Transform_position", _ghost.transform.position);
                _ghostVfx.SetVector3("Transform_angles", _ghost.transform.eulerAngles);
                _ghostVfx.SetVector3("Transform_scale", _ghost.transform.lossyScale);
                _ghostVfx.SetBool("Use Plane", true);
                _ghostVfx.SetBool("Use Box", false);
                _ghostVfx.SetBool("Use Ellipse", false);
                _ghostVfx.SetVector4("Mask Up", new Vector4(0f, 1f, 0f, 0f));
                _ghostVfx.SetVector4("Mask Right", new Vector4(1f, 0f, 0f, 0f));
                _ghostVfx.SetVector4("Mask Forward", new Vector4(0f, 0f, 1f, 0f));
                _ghostVfx.SetVector4("Mask Scale", new Vector4(1f, 1f, 1f, 0f));
                _ghostVfx.SetFloat("Spawn Rate", 50000f);
                _ghostVfx.SetFloat("Spawn Rate Multiplier", 1f);
                _ghostVfx.SetFloat("Emission Power", 2f);
                _ghostVfx.SetVector4("Color", (Vector4)Color.red);
                SetMaskY(0f);
                Undo.RegisterCreatedObjectUndo(_ghost, "影子浮现预览");

                // 对齐自检（数据层）：影子网格世界包围盒 应≈ 原件渲染器合并包围盒
                var gBounds = mf.sharedMesh.bounds;
                gBounds.Encapsulate(mf.sharedMesh.bounds);
                var world = mf.transform.localToWorldMatrix;
                var gc = world.MultiplyPoint3x4(gBounds.center);
                var ge = world.MultiplyVector(gBounds.extents);
                var ghostWorld = new Bounds(gc, ge * 2f);
                float delta = Vector3.Distance(ghostWorld.center, _worldBounds.center)
                              + Vector3.Distance(ghostWorld.extents, _worldBounds.extents);
                Debug.Log($"[GhostPreview] 对齐自检：影子中心={ghostWorld.center.ToString("0.000")} 尺寸={ghostWorld.size.ToString("0.000")}"
                          + $" | 原件中心={_worldBounds.center.ToString("0.000")} 尺寸={_worldBounds.size.ToString("0.000")}"
                          + $" → {(delta < 0.15f ? "PASS" : "FAIL Δ=" + delta.ToString("0.000"))}");
            }
        }

        Selection.activeGameObject = sel;
        Debug.Log($"[GhostPreview] 摆场就位：{_orig.name} 渲染器×{_renderers.Length}"
                  + $" 世界包围盒 y∈[{_worldBounds.min.y:0.00},{_worldBounds.max.y:0.00}]，"
                  + $"影子={( _ghost != null ? "已建" : "缺失")}。消融线已置 -0.5（模型不可见）。\n"
                  + $"点「{ScanMenu}」自动扫（-0.5→顶+0.5 上行浮现），或「{RestoreMenu}」还原。\n"
                  + "注意：编辑态看粒子须开 Scene 视图工具栏的 VFX 播放开关（Toggle VFX Play）"
                  + "——shader 消融不依赖它，直接可见。");
    }

    [MenuItem(ScanMenu)]
    private static void Scan()
    {
        if (_renderers == null || _renderers.Length == 0)
        {
            Debug.LogWarning("[GhostPreview] 请先摆场（选中模型后点第一个菜单）");
            return;
        }
        if (_scanning) return;
        _scanning = true;
        _scanStart = EditorApplication.timeSinceStartup;
        _ghostVfx?.Play();
        EditorApplication.update += ScanTick;
        Debug.Log("[GhostPreview] 自动扫开始（消融线与粒子遮罩同 Y 驱动）");
    }

    private static void ScanTick()
    {
        float t = (float)(EditorApplication.timeSinceStartup - _scanStart) / ScanSeconds;
        if (t >= 1f)
        {
            EditorApplication.update -= ScanTick;
            _scanning = false;
            ApplyDissolve(1000f);           // 复位=不生效（模型完全显现）
            _ghostVfx?.Stop();
            SceneView.RepaintAll();
            Debug.Log("[GhostPreview] 自动扫完成（_DissolveY 已复位 1000）");
            return;
        }
        float k = 1f - Mathf.Pow(1f - t, 2f); // easeOut：起扫快、近顶缓
        float y = Mathf.Lerp(0f, _worldBounds.max.y + 0.5f, k); // 线自 y=0 上行至顶+0.5=自下而上浮现（与生产同规范）
        ApplyDissolve(y);
        SetMaskY(y);
        SceneView.RepaintAll();
    }

    [MenuItem(RestoreMenu)]
    private static void Restore()
    {
        if (_scanning) { EditorApplication.update -= ScanTick; _scanning = false; }
        int cleared = 0;
        if (_renderers != null)
        {
            foreach (var r in _renderers)
                if (r != null) { r.SetPropertyBlock(null); cleared++; }
        }
        _renderers = null; _orig = null;
        CleanupGhost();
        Debug.Log(cleared > 0
            ? $"[GhostPreview] 已还原（{cleared} 个渲染器 MPB 清除、影子删除）"
            : "[GhostPreview] 没有可还原的摆场（未摆过或已还原）");
    }

    // ---- 内部 ----

    private static void ApplyDissolve(float y)
    {
        _mpb.SetFloat("_DissolveY", y);
        _mpb.SetFloat("_DissolveBand", 0.12f);
        _mpb.SetColor("_DissolveEdgeColor", EdgeColor);
        foreach (var r in _renderers)
            if (r != null) r.SetPropertyBlock(_mpb);
    }

    private static void SetMaskY(float worldY)
    {
        if (_ghostVfx == null) return;
        var c = _worldBounds.center;
        _ghostVfx.SetVector4("Mask Position", new Vector4(c.x, worldY, c.z, 0f));
    }

    private static Bounds ComputeWorldBounds(Renderer[] renderers)
    {
        var b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return b;
    }

    private static void CleanupGhost()
    {
        if (_ghost != null) Object.DestroyImmediate(_ghost);
        _ghost = null; _ghostVfx = null;
    }

    /// <summary>拼装预制体→单网格：全部子 MeshFilter 按世界矩阵合到模型根本地系（与生产 BattleCastArrival 同式）。</summary>
    private static Mesh MergeModelMeshes(GameObject template)
    {
        var filters = template.GetComponentsInChildren<MeshFilter>(true);
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
        if (list.Count == 0) return null;
        var merged = new Mesh { name = GhostName + "-mesh" };
        merged.CombineMeshes(list.ToArray(), true, true);
        merged.RecalculateBounds();
        if (skipped > 0)
            Debug.Log($"[GhostPreview] 合并跳过不可读网格 {skipped} 个（read/write 未开启，影子可能缺件）");
        return merged;
    }
}
