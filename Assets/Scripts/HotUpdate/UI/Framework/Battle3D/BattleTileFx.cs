using System.Collections.Generic;
using Tide.HotUpdate;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SynergyUI
{
    /// <summary>
    /// 地块菲涅尔特效层（2026-10-13 卡背方案退役改版）：可行动性/地牌状态改由地块自身表达——
    /// 生物未横置未死亡=脚下地块暖白菲涅尔，横置或死亡即熄；
    /// 地牌未横置=锁色菲涅尔 + 格面元素图标（Element.prefab，叠层错位两张），横置（已产元素）即熄。
    ///
    /// 形态：每格一枚菲涅尔壳（BattleHex mesh 同形，挂 tile-{x}-{z} 下随浮起动画同走；
    /// 抬 4mm + 侧壁微外扩防与本体叠面闪烁），材质=落盘资产 BattleTile_Fresnel.mat
    /// （Inspector 调 Power/Intensity 归用户），颜色按格走 MaterialPropertyBlock 覆写。
    ///
    /// 消费模型：全量对账——BattleFieldView.Apply 开头 ClearAll 全熄，随 Reconcile 按数据逐格
    /// 点亮（幂等；对账频率=引擎事件，非逐帧）。依赖缺失（shader/材质/图标 prefab）逐级回落
    /// 为"该格无特效"并告警一次，不阻塞对局。
    /// </summary>
    public static class BattleTileFx
    {
        private const string FresnelMatPath = "Assets/Art/Battle3D/Materials/BattleTile_Fresnel.mat";
        private const string HexMeshPath = "Assets/Art/Battle3D/BattleHex.asset";
        private const string ElementPrefabPath = "Assets/Art/Prefab/Element.prefab";

        private static readonly int FresnelColorId = Shader.PropertyToID("_FresnelColor");
        private static readonly int WaveWeightId = Shader.PropertyToID("_WaveWeight");

        // 地牌菲涅尔配色：下标=ManaType 序 [灰,红,蓝,绿,白,黑]；黑=additive 黑不发光，以紫罗兰替代
        private static readonly Color[] GlowColors =
        {
            new Color(0.62f, 0.64f, 0.70f), // 灰
            new Color(1.00f, 0.28f, 0.20f), // 红
            new Color(0.25f, 0.50f, 1.00f), // 蓝
            new Color(0.30f, 0.95f, 0.40f), // 绿
            new Color(0.95f, 0.95f, 0.88f), // 白
            new Color(0.62f, 0.38f, 0.95f), // 黑（紫替代）
        };

        // 生物可行动高光=淡蓝扩散波（2026-10-13 晚用户定案：菲涅尔侧壁太弱，改格心向外扩散光波；
        // 波参数全局调在 BattleTile_Fresnel.mat，此处只给色+波模式）
        private static readonly Color UnitGlowColor = new Color(0.45f, 0.72f, 1.00f);

        private static readonly string[] ElemMatNames = { "Gray", "Red", "Blue", "Green", "White", "Black" };
        private static readonly Material[] _elemMats = new Material[6]; // 元素图标面片材质（下标=ManaType 序）

        private static Material _fresnelMat;
        private static Mesh _hexMesh;
        private static GameObject _elementPrefab;
        private static MaterialPropertyBlock _mpb;

        private static readonly Dictionary<long, Transform> _tiles = new Dictionary<long, Transform>();
        private static readonly Dictionary<long, Renderer> _glows = new Dictionary<long, Renderer>();
        private static readonly List<GameObject> _icons = new List<GameObject>();
        private static bool _depsLogged;

        /// <summary>当前点亮的菲涅尔壳数（对账日志证据）。</summary>
        public static int GlowOn
        {
            get
            {
                int n = 0;
                foreach (var r in _glows.Values) if (r != null && r.enabled) n++;
                return n;
            }
        }

        /// <summary>当前格面元素图标数（对账日志证据）。</summary>
        public static int IconCount => _icons.Count;

        /// <summary>域重载关闭（Enter Play Mode Options）防跨 Play 残留：与 BattleStageDirector.ResetStatics 同口径。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _tiles.Clear();
            _glows.Clear();
            _icons.Clear();
            _fresnelMat = null;
            _hexMesh = null;
            _elementPrefab = null;
            System.Array.Clear(_elemMats, 0, _elemMats.Length); // readonly 数组：清元素不换引用
            _depsLogged = false;
        }

        /// <summary>全熄+图标全清（对账开头调用；菲涅尔壳 renderer 留存复用，图标按次重建）。</summary>
        public static void ClearAll()
        {
            foreach (var r in _glows.Values)
                if (r != null) r.enabled = false; // fake-null（棋盘重建）跳过，下次 Ensure 判死重建
            foreach (var icon in _icons)
                if (icon != null)
                {
                    if (Application.isPlaying) Object.Destroy(icon); // 编辑态调用（工具/冒烟）走立即销毁
                    else Object.DestroyImmediate(icon);
                }
            _icons.Clear();
        }

        /// <summary>拆台清索引（对象本体随棋盘根销毁；BattleStageDirector.DismountBoard 调用）。</summary>
        public static void Unmount()
        {
            _tiles.Clear();
            _glows.Clear();
            _icons.Clear();
        }

        /// <summary>生物可行动：脚下地块淡蓝扩散波（格心向外循环推进）。</summary>
        public static void SetUnitGlow(int x, int z) => Light(x, z, UnitGlowColor, wave: true);

        /// <summary>地牌色组仍有未横置张：锁色菲涅尔（lockedColor&lt;0 按灰；2026-10-14 按色叠格后每格恒单色）。</summary>
        public static void SetLandGlow(int x, int z, int lockedColor)
            => Light(x, z, GlowColors[Mathf.Clamp(lockedColor, 0, GlowColors.Length - 1)], wave: false);

        // 环境光 SetAmbientGlow 随半场泛光退役（2026-10-15 用户定案：与地格玩法高光共用壳互相覆盖——
        // 优先权指示迁 BattlePriorityGlow 战场底部双光条）

        /// <summary>地牌格面元素图标（2026-10-14 按色叠格改版：每色一格单图标居中；
        /// offset=格面局部偏移，技能格黑白双图标错位复用本口；未锁色/依赖缺失不显示）。</summary>
        public static void AddElement(int x, int z, int lockedColor, Vector2 offset)
        {
            if (lockedColor < 0) return;
            var tile = TileOf(x, z);
            if (tile == null) return;
            if (_elementPrefab == null)
            {
                _elementPrefab = HotUpdateAssets.Load<GameObject>("Element", ElementPrefabPath);
                if (_elementPrefab == null) { WarnDeps("Element.prefab"); return; }
            }
            var mat = ElemMaterial(lockedColor);
            if (mat == null) return;

            var icon = Object.Instantiate(_elementPrefab, tile, false);
            icon.name = "fx-element-" + ElemMatNames[Mathf.Clamp(lockedColor, 0, ElemMatNames.Length - 1)];
            icon.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // 面朝上（与 prefab 同值，显式防御）
            icon.transform.localPosition = new Vector3(offset.x, 0.008f, offset.y);
            icon.transform.localScale = Vector3.one * 0.5f;
            var rend = icon.GetComponent<MeshRenderer>();
            rend.sharedMaterial = mat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _icons.Add(icon);
        }

        // ======================================== 内部 ========================================

        private static long Key(int x, int z) => ((long)x << 32) | (uint)z;

        private static void Light(int x, int z, Color c, bool wave)
        {
            var r = EnsureGlow(x, z);
            if (r == null) return;
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            _mpb.SetColor(FresnelColorId, c);
            _mpb.SetFloat(WaveWeightId, wave ? 1f : 0f); // 波=生物可行动档 / 菲涅尔=地牌档
            r.SetPropertyBlock(_mpb);
            r.enabled = true;
        }

        /// <summary>格 → tile Transform（root.Find 按名；浮起动画不动父子关系，缓存跨帧安全）。</summary>
        private static Transform TileOf(int x, int z)
        {
            var key = Key(x, z);
            if (_tiles.TryGetValue(key, out var t) && t != null) return t; // fake-null 判死重查
            t = BattleBoardGrid.Root != null ? BattleBoardGrid.Root.transform.Find($"tile-{x}-{z}") : null;
            if (t != null) _tiles[key] = t; // 未装台/格名异常不入缓存
            return t;
        }

        /// <summary>菲涅尔壳惰性建（每格最多一枚；依赖缺失回落 null=该格无特效）。</summary>
        private static Renderer EnsureGlow(int x, int z)
        {
            var key = Key(x, z);
            if (_glows.TryGetValue(key, out var r) && r != null) return r; // fake-null 判死重建
            var tile = TileOf(x, z);
            if (tile == null) return null;

            if (_fresnelMat == null)
            {
                _fresnelMat = HotUpdateAssets.Load<Material>("BattleTile_Fresnel", FresnelMatPath);
                if (_fresnelMat == null) { WarnDeps("BattleTile_Fresnel.mat"); return null; }
            }
            if (_hexMesh == null)
            {
                _hexMesh = HotUpdateAssets.Load<Mesh>("BattleHex", HexMeshPath);
                if (_hexMesh == null) { WarnDeps("BattleHex.asset(mesh)"); return null; }
            }

            var go = new GameObject("fx-fresnel");
            go.transform.SetParent(tile, false);
            go.transform.localPosition = new Vector3(0f, 0.004f, 0f);   // 抬 4mm：顶面 y=0 错开防叠面闪
            go.transform.localScale = new Vector3(1.015f, 1f, 1.015f); // 侧壁微外扩（顶面 y=0 不受 Y 缩放影响）
            go.AddComponent<MeshFilter>().sharedMesh = _hexMesh;
            var rend = go.AddComponent<MeshRenderer>();
            rend.sharedMaterial = _fresnelMat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
            rend.enabled = false; // 建=熄，Light 时点亮
            _glows[key] = rend;
            return rend;
        }

        /// <summary>元素图标面片材质（下标=ManaType 序；沿卡背时代资产 BattleCardBack_Elem_*_Mat）。</summary>
        private static Material ElemMaterial(int lockedColor)
        {
            if (lockedColor < 0 || lockedColor >= _elemMats.Length) return null;
            if (_elemMats[lockedColor] != null) return _elemMats[lockedColor];
            _elemMats[lockedColor] = HotUpdateAssets.Load<Material>(
                "Elem_" + ElemMatNames[lockedColor],
                "Assets/Art/Battle3D/Materials/BattleCardBack_Elem_" + ElemMatNames[lockedColor] + "_Mat.asset");
            return _elemMats[lockedColor];
        }

        private static void WarnDeps(string what)
        {
            if (_depsLogged) return;
            _depsLogged = true;
            Debug.LogWarning("[BattleTileFx] 依赖缺失：" + what + "——地块菲涅尔/元素图标降级为不显示");
        }
    }
}
