using System.Collections.Generic;
using CardCore;
using GameBoard;
using Tide.HotUpdate;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SynergyUI
{
    /// <summary>
    /// 地牌长条（2026-11-15 定案）：地牌行 4 格**视觉合并**为一条长条——每张地牌一张平贴面片
    ///（规格与生物横置卡背同源=CardPlateMetrics：宽 0.55×高 0.819），长条靠卡组一侧留 0.72m
    /// 数字段（填充/上限）。横置=原地绕 Y 旋 90°（0.12s）+压暗 0.55；横置张按序微抬分层防叠面闪。
    ///
    /// 硬约束：引擎格坐标零改动——BoardLayout.LandCells（4 格）/AssignLandCells/ElementPool
    /// 全保留，合并只发生在视觉与命中层。面片 collider 名 land-quad-{rid}（3D 射线直点横置产色，
    /// 见 BattleScreen.OnLandQuadClick）；长条底板 collider 名 land-strip-{self/opp}（空白段点击
    /// 反查格开 Information）。
    ///
    /// 生命周期：Reconcile 惰性 Mount（BattleFieldView.Apply→ReconcileLands 驱动，幂等差量对账
    /// 键=RuntimeId）；BattleStageDirector.DismountBoard→Unmount 清池与运行时材质实例。
    /// 底板/数字段锚=BattleStage.prefab 预烘焙节点（Board 下 land-strip-self/opp，用户可挪整条
    /// ——节点锚=localPosition，面片全部挂节点下随动）；面片本体=LandCardQuad.prefab 实例化
    ///（缺失回落 Element.prefab+运行时补 BoxCollider）。排布几何单源=BoardLayout+BattleBoardGrid
    /// 推导（烘焙同源重跑；格距改动须重烘焙 BattleStage.prefab）。
    /// </summary>
    public static class LandStripFx
    {
        private const string QuadPrefabAddress = "LandCardQuad";
        private const string QuadPrefabPath = "Assets/Art/Prefab/LandCardQuad.prefab";
        private const string FallbackPrefabPath = "Assets/Art/Prefab/Element.prefab";
        private const string ZoneMatPath = "Assets/Art/Materials/BattleTile_Zone02_P1Unit.mat"; // 着色克隆模板（URP Lit·与角色柱同源）

        public const string SelfNodeName = "land-strip-self";
        public const string OppNodeName = "land-strip-opp";
        public const string QuadPrefix = "land-quad-";

        // 排布参数（2026-11-15 改版定稿）：cube 满长前面片间距 ≤0.62；满长后**收缩间距**密排至 0.28
        private const float MaxPitch = 0.62f;
        private const float MinPitch = 0.28f;
        private const float TappedLayerY = 0.0015f; // 横置张分层抬升（后放的横置张依次抬高防叠面闪）
        private const float StripWidth = 0.9f;      // 长条底板宽（Z 向）
        private const float StripLift = 0.010f;     // 底板抬升（地块顶面 0 与面片 0.012 之间）
        private const float IconLocalScale = 0.38f; // 面片角标小元素图标（quad 本地系；世界≈0.38×0.55）
        // 2026-11-15 地牌改版 §8.2：多色角标——底部一行小元素图标逐可产色点亮（仅多色地显示，
        // 单色地主图标已表达；dot 共用 quad prefab 实例化=「实例化共用预制体」豁免口径）
        private const float DotLocalScale = 0.20f;  // 角标小图标尺寸（IconLocalScale 的 ~0.53）
        private const float DotPitch = 0.24f;       // 角标间距（quad 本地系）
        private const float DotY = -0.50f;          // 角标行 Y（面片底部）
        private static readonly Color PlateColor = new Color(0.085f, 0.095f, 0.115f); // 底板深色不透明
        private static readonly Color CapFullColor = new Color(1f, 0.78f, 0.28f);     // 满槽金
        private static readonly Color CapFreeColor = Color.white;

        private sealed class QuadEntry
        {
            public GameObject Go;
            public LandCardQuadView View;
            public Renderer IconRend;
            public readonly GameObject[] Dots = new GameObject[4]; // 可产色角标（灰/红/蓝/绿——下标=(int)ManaType）
            public int Color = -1;
            public int Mask; // 当前角标掩码（0=全隐）
        }

        private static readonly Dictionary<uint, QuadEntry>[] _quads =
            { new Dictionary<uint, QuadEntry>(), new Dictionary<uint, QuadEntry>() };
        private static readonly Transform[] _nodes = new Transform[2];
        private static readonly TextMeshPro[] _capLabels = new TextMeshPro[2];
        private static readonly int[] _lastCount = { -1, -1 };  // 对账日志证据（变更才刷）
        private static readonly int[] _lastCap = { -1, -1 };
        private static readonly Dictionary<int, Material> _colorMats = new Dictionary<int, Material>();
        private static readonly Dictionary<int, Material> _tappedMats = new Dictionary<int, Material>();
        private static Material _plateMat;
        private static GameObject _quadPrefab;
        private static float _quadBaseScale = 1f;
        private static bool _warned;

        private struct Geo
        {
            public float StartX, DeckX;    // 临时元素格中心 / 卡组格中心（Board 局部 X——两锚定地牌区）
            public float RowZ, Dir;        // 行 Z / 延伸方向（P1=+x、P2=−x：从临时元素格伸向卡组）
        }

        private static readonly Geo?[] _geo = new Geo?[2];
        private static readonly List<uint> _staleBuf = new List<uint>(16);

        /// <summary>域重载防跨 Play 残留（与 BattleTileFx.ResetStatics 同口径）。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _quads[0].Clear();
            _quads[1].Clear();
            _nodes[0] = _nodes[1] = null;
            _capLabels[0] = _capLabels[1] = null;
            _lastCount[0] = _lastCount[1] = -1;
            _lastCap[0] = _lastCap[1] = -1;
            _geo[0] = _geo[1] = null;
            _colorMats.Clear();
            _tappedMats.Clear();
            _plateMat = null;
            _quadPrefab = null;
            _quadBaseScale = 1f;
            _warned = false;
        }

        // ======================================== 对账（BattleFieldView.ReconcileLands 驱动） ========================================

        /// <summary>幂等差量对账：面片数=lands.Count（引擎池序=放入顺序，从该方视角左→右铺排）。
        /// cap=地牌上限（数字段 {填充}/{上限}）。</summary>
        public static void Reconcile(int owner, List<BattleCardView> lands, int cap)
        {
            if (!BattleBoardGrid.Ready) return;
            var node = EnsureNode(owner);
            if (node == null) return;
            var g = GeometryOf(owner);
            var anchor = node.localPosition; // 节点锚（Board 局部）——用户挪节点整条随动

            var dict = _quads[owner];
            int n = lands != null ? lands.Count : 0;
            // 地牌区（2026-11-15 用户定稿）：cube 长=min(max(1.3, 1.3×n), 9.5)；满长后收面片间距
            float len = Mathf.Clamp(BlockBase * Mathf.Max(n, 1), BlockBase, BlockMaxLen);
            float faceStart = g.StartX + g.Dir * BlockStartOffset;          // 面片区起点（cube 头）
            float usable = Mathf.Max(0.5f, len - 0.3f);                     // 面片区净长（两端内缩）
            float pitch = n > 0 ? Mathf.Clamp(usable / n, MinPitch, MaxPitch) : MaxPitch;
            float groupW = pitch * n;
            float originX = faceStart + g.Dir * (usable - groupW) * 0.5f;   // 面片区内居中

            var seen = new HashSet<uint>();
            int tappedSeen = 0;
            for (int i = 0; i < n; i++)
            {
                var land = lands[i];
                if (land == null || !seen.Add(land.RuntimeId)) continue;
                int color = land.Runtime != null && land.Runtime.LockedColor >= 0
                    ? land.Runtime.LockedColor : (int)ManaType.Gray;
                bool tapped = land.IsTapped;
                var e = EnsureQuad(owner, land.RuntimeId, color);
                if (e == null) continue;
                ApplyMask(e, land.LandColorMask); // 2026-11-15 地牌改版：多色角标（≥2 色才显示）
                float x = originX + g.Dir * pitch * (i + 0.5f);
                float y = CardPlateMetrics.LiftY + (tapped ? tappedSeen * TappedLayerY : 0f);
                // 节点局部坐标（节点无旋转缩放，直接平移差）
                e.Go.transform.localPosition = new Vector3(x - anchor.x, y, g.RowZ - anchor.z);
                e.Go.transform.SetSiblingIndex(i); // 渲染序=池序（后放的横置张盖在上层）
                e.View.SetTapped(tapped);
                if (tapped) tappedSeen++;
            }

            // 离场面片回收（fake-null 判死重建）
            _staleBuf.Clear();
            foreach (var rid in dict.Keys)
                if (!seen.Contains(rid)) _staleBuf.Add(rid);
            foreach (var rid in _staleBuf)
            {
                var e = dict[rid];
                dict.Remove(rid);
                if (e?.Go != null)
                {
                    if (Application.isPlaying) Object.Destroy(e.Go);
                    else Object.DestroyImmediate(e.Go);
                }
            }

            UpdateCapTri(owner, n, cap);
            SyncLandBlock(owner, g, n, len); // 2026-11-15 改版：cube 自临时元素格伸向卡组、满长封顶

            if (n != _lastCount[owner] || cap != _lastCap[owner]) // 对账证据日志（变更才刷，不随事件刷屏）
            {
                _lastCount[owner] = n;
                _lastCap[owner] = cap;
                Debug.Log($"[LandStrip] owner={owner} 面片 {n}/{cap}（横置 {tappedSeen}·pitch={pitch:F2}·池序左→右）");
            }
        }

        // ======================================== 命中/锚点/高亮（屏侧消费） ========================================

        /// <summary>land-quad-{rid} 命中名解析。</summary>
        public static bool TryParseHit(string hitName, out uint rid)
        {
            rid = 0;
            if (string.IsNullOrEmpty(hitName) || !hitName.StartsWith(QuadPrefix, System.StringComparison.Ordinal))
                return false;
            return uint.TryParse(hitName.Substring(QuadPrefix.Length), out rid);
        }

        /// <summary>land-strip-{self/opp} 命中名解析（out owner 0/1）。</summary>
        public static bool IsStripHit(string hitName, out int owner)
        {
            owner = -1;
            if (string.IsNullOrEmpty(hitName)) return false;
            if (hitName == SelfNodeName) { owner = 0; return true; }
            if (hitName == OppNodeName) { owner = 1; return true; }
            return false;
        }

        /// <summary>面片世界锚点（粒子/箭头；未入池返回 false）。</summary>
        public static bool QuadAnchor(uint rid, out Vector3 pos)
        {
            pos = default;
            for (int o = 0; o < 2; o++)
                if (_quads[o].TryGetValue(rid, out var e) && e?.Go != null)
                {
                    pos = e.Go.transform.position + Vector3.up * 0.02f;
                    return true;
                }
            return false;
        }

        /// <summary>面片点按反馈（缩放脉冲；OnLandQuadClick 消费——不触发整格下沉）。</summary>
        public static void PressPop(uint rid)
        {
            for (int o = 0; o < 2; o++)
                if (_quads[o].TryGetValue(rid, out var e) && e?.View != null)
                {
                    e.View.PressPop();
                    return;
                }
        }

        // SetStripHighlight（拖拽期色带描边高亮）已随 2026-11-15 地牌改版整删：放地不再选色带，
        // 拖拽视觉=3D 箭头+临时元素格波标（UpdateDragVisualAt 单源），零调用点实证后移除。

        /// <summary>单一地牌区 cube（2026-11-15 用户定稿）：挂临时元素格 tile 下——默认显示
        /// 1.3×1.3（0 张也在）、长=1.3×n 封顶 9.5、pos.x=scale.x/2+1、高 0.25、y=-0.12、无旋转。</summary>
        private static void SyncLandBlock(int owner, Geo g, int n, float len)
        {
            var start = BoardLayout.AnyElementCell(owner);
            var anchor = BattleTileFx.TileOf(start.x, start.z);
            var block = anchor != null ? anchor.Find("land-block") : null;
            if (block == null) return; // 旧 prefab 未烘焙：面片照铺（降级口径）
            block.localScale = new Vector3(len, 0.25f, BlockBase);
            block.localPosition = new Vector3((len / 2f + BlockStartOffset) * g.Dir, -0.12f, 0f);
            block.localRotation = Quaternion.identity;
            if (!block.gameObject.activeSelf) block.gameObject.SetActive(true); // 默认显示（0 张=1.3 基线）
        }

        private static Transform EnsureNode(int owner)
        {
            if (_nodes[owner] != null) return _nodes[owner]; // fake-null 由判空+重查兜
            if (BattleBoardGrid.Root == null) return null;
            var node = BattleBoardGrid.Root.transform.Find(owner == 0 ? SelfNodeName : OppNodeName);
            if (node == null)
            {
                // 旧 prefab 未烘焙（回落链/未重烘焙）：就地建节点（无底板/collider，面片照铺）——降级不拦对局
                if (!_warned)
                {
                    _warned = true;
                    Debug.LogWarning("[LandStrip] BattleStage.prefab 缺 land-strip 节点（旧版 prefab）——"
                                     + "长条降级为无底板（重跑烘焙菜单恢复）");
                }
                var go = new GameObject(owner == 0 ? SelfNodeName : OppNodeName);
                go.transform.SetParent(BattleBoardGrid.Root.transform, false);
                var g = GeometryOf(owner);
                go.transform.localPosition = new Vector3((g.StartX + g.DeckX) * 0.5f, 0f, g.RowZ);
                node = go.transform;
            }
            _nodes[owner] = node;
            MountPlateMaterial(node);
            return node;
        }

        /// <summary>底板着色（烘焙件只有几何——材质归运行时实例，用户可在 prefab 换资产材质）。</summary>
        private static void MountPlateMaterial(Transform node)
        {
            var plate = node.Find("plate");
            var rend = plate != null ? plate.GetComponent<Renderer>() : null;
            if (rend == null) return;
            rend.sharedMaterial = PlateMaterial();
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
        }

        private static Geo GeometryOf(int owner)
        {
            if (_geo[owner] is Geo cached) return cached;
            var start = BoardLayout.AnyElementCell(owner); // 临时元素格=地牌区起点（2026-11-15 改版）
            var deck = owner == 0
                ? (BoardLayout.P1DeckX, BoardLayout.P1DeckZ)
                : BoardLayout.MirrorSeat(BoardLayout.P1DeckX, BoardLayout.P1DeckZ);
            var s = BattleBoardGrid.CellCenterLocal(start.x, start.z);
            var d = BattleBoardGrid.CellCenterLocal(deck.Item1, deck.Item2);
            var g = new Geo
            {
                StartX = s.x,
                DeckX = d.x,
                RowZ = s.z,
                Dir = d.x >= s.x ? 1f : -1f, // 从临时元素格伸向卡组
            };
            _geo[owner] = g;
            return g;
        }

        /// <summary>地牌区 cube 规则（2026-11-15 用户定稿）：默认显示 1.3×1.3（0 张也在）、
        /// 随张数伸长（1.3×n）、x 极限 9.5；pos.x=scale.x/2+1；满长后收**面片间距**（MinPitch）。</summary>
        private const float BlockBase = 1.3f;      // 默认长=宽（1 张基线）
        private const float BlockMaxLen = 9.5f;    // x 极限长度
        private const float BlockStartOffset = 1f; // 起点偏移（临时元素格中心 → cube 左缘）

        private static QuadEntry EnsureQuad(int owner, uint rid, int color)
        {
            var dict = _quads[owner];
            if (dict.TryGetValue(rid, out var e) && e?.Go != null)
            {
                ApplyColor(e, color);
                return e;
            }
            if (!EnsureQuadPrefab()) return null;
            var node = _nodes[owner];

            var go = Object.Instantiate(_quadPrefab, node, false);
            go.name = QuadPrefix + rid;
            // 命中面（BoxCollider 缺则补：Element 模板无碰撞体；覆盖面片正张）
            if (go.GetComponent<Collider>() == null) go.AddComponent<BoxCollider>();
            var rend = go.GetComponent<MeshRenderer>();
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
            // 尺寸=CardPlateMetrics 单源（prefab 烘焙缩放为基准换算——同 UnitCardPlate 口径）
            float w = Mathf.Max(0.05f, CardPlateMetrics.Width) / _quadBaseScale;
            var scale = Vector3.one * w;
            scale.y = scale.x * CardPlateMetrics.Aspect;
            go.transform.localScale = scale;
            go.transform.localRotation = CardPlateMetrics.Portrait;

            // 角标小元素图标（Element.prefab 同管线；localRotation=identity 抵消父面片 90° 俯旋=面朝上；
            // 面片 scale.y 含 Aspect 拉伸——图标 y 分量除回 Aspect 保正方形）
            var icon = Object.Instantiate(_quadPrefab, go.transform, false);
            icon.name = "icon";
            var iconCol = icon.GetComponent<Collider>();
            if (iconCol != null)
            {
                if (Application.isPlaying) Object.Destroy(iconCol);
                else Object.DestroyImmediate(iconCol);
            }
            icon.transform.localRotation = Quaternion.identity;
            icon.transform.localPosition = new Vector3(0f, 0.52f, -0.003f);
            icon.transform.localScale = new Vector3(IconLocalScale / _quadBaseScale,
                IconLocalScale / CardPlateMetrics.Aspect / _quadBaseScale, 1f);
            var iconRend = icon.GetComponent<MeshRenderer>();
            iconRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            iconRend.receiveShadows = false;

            var e2 = new QuadEntry();
            // 多色角标位（2026-11-15 §8.2）：4 个小图标位预建（dot-{c}，默认隐藏），ApplyMask 点亮
            for (int c = 0; c < 4; c++)
            {
                var dot = Object.Instantiate(_quadPrefab, go.transform, false);
                dot.name = "dot-" + c;
                var dotCol = dot.GetComponent<Collider>();
                if (dotCol != null)
                {
                    if (Application.isPlaying) Object.Destroy(dotCol);
                    else Object.DestroyImmediate(dotCol);
                }
                dot.transform.localRotation = Quaternion.identity;
                dot.transform.localScale = new Vector3(DotLocalScale / _quadBaseScale,
                    DotLocalScale / CardPlateMetrics.Aspect / _quadBaseScale, 1f);
                var dotRend = dot.GetComponent<MeshRenderer>();
                dotRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                dotRend.receiveShadows = false;
                var dotMat = BattleTileFx.ElemIconMaterial(c);
                if (dotMat != null) dotRend.sharedMaterial = dotMat;
                dot.SetActive(false);
                e2.Dots[c] = dot;
            }
            e2.Go = go;
            e2.View = go.AddComponent<LandCardQuadView>();
            e2.IconRend = iconRend;
            e = e2;
            dict[rid] = e;
            ApplyColor(e, color);
            return e;
        }

        private static void ApplyColor(QuadEntry e, int color)
        {
            if (e.Color == color) return;
            e.Color = color;
            var iconMat = BattleTileFx.ElemIconMaterial(color);
            if (iconMat != null) e.IconRend.sharedMaterial = iconMat;
            var bodyMat = ColorMaterial(color, false);
            if (bodyMat != null) e.View.SetMaterials(bodyMat, ColorMaterial(color, true));
        }

        /// <summary>可产色角标（2026-11-15 地牌改版）：mask=LandColorMask（bit0..3=灰红蓝绿）。
        /// **仅多色地**（≥2 位）点亮底部角标行——单色地主图标已表达，不添噪点；行内按位序居中排布。</summary>
        private static void ApplyMask(QuadEntry e, int mask)
        {
            int masked = mask & 0xF;
            if (e.Mask == masked) return;
            e.Mask = masked;
            int count = 0;
            for (int c = 0; c < 4; c++) count += (masked >> c) & 1;
            bool show = count >= 2;
            int shown = 0;
            for (int c = 0; c < 4; c++)
            {
                var dot = e.Dots[c];
                if (dot == null) continue;
                bool on = show && ((masked >> c) & 1) != 0;
                if (on)
                {
                    dot.transform.localPosition = new Vector3(
                        (shown - (count - 1) * 0.5f) * DotPitch, DotY, -0.003f);
                    shown++;
                }
                if (dot.activeSelf != on) dot.SetActive(on);
            }
        }

        /// <summary>地牌上限说明（2026-11-15 用户预制体口径）：**只写文本**——pie-0 下 cap-text
        /// 写「当前地牌数/上限」（初始 "0/0"）；外观全归预制体，缺件不建。</summary>
        private static void UpdateCapTri(int owner, int n, int cap)
        {
            var label = _capLabels[owner];
            if (label == null)
            {
                var start = BoardLayout.AnyElementCell(owner);
                var tile = BattleTileFx.TileOf(start.x, start.z);
                var textT = tile != null ? tile.Find("land-pie/pie-0/cap-text") : null;
                label = textT != null ? textT.GetComponent<TextMeshPro>() : null;
                if (label != null) _capLabels[owner] = label;
            }
            if (label == null) return; // 预制体缺件：不建不改（告警见 EnsureNode 口径）
            string txt = $"{n}/{cap}";
            if (label.text != txt) label.text = txt;
        }

        private static bool EnsureTmpFont()
        {
            bool ok = TMP_Settings.defaultFontAsset != null;
            if (!ok && !_warned)
            {
                _warned = true;
                Debug.LogWarning("[LandStrip] TMP 默认字体未配置——数字段禁用");
            }
            return ok;
        }

        private static bool EnsureQuadPrefab()
        {
            if (_quadPrefab != null) return true;
            _quadPrefab = HotUpdateAssets.Load<GameObject>(QuadPrefabAddress, QuadPrefabPath);
            if (_quadPrefab == null)
            {
                _quadPrefab = HotUpdateAssets.Load<GameObject>("Element", FallbackPrefabPath);
                if (_quadPrefab == null)
                {
                    if (!_warned) { _warned = true; Debug.LogWarning("[LandStrip] LandCardQuad/Element.prefab 缺失——地牌面片降级为不显示"); }
                    return false;
                }
            }
            _quadBaseScale = _quadPrefab.transform.localScale.x;
            if (_quadBaseScale <= 0.001f) _quadBaseScale = 1f;
            return true;
        }

        private static Material PlateMaterial()
        {
            if (_plateMat != null) return _plateMat;
            var template = HotUpdateAssets.Load<Material>("BattleTile_Zone02", ZoneMatPath);
            if (template == null) { WarnTemplate(); return null; }
            _plateMat = new Material(template) { name = "LandStrip_Plate" };
            _plateMat.SetColor("_BaseColor", PlateColor);
            return _plateMat;
        }

        private static Material ColorMaterial(int color, bool tapped)
        {
            var cache = tapped ? _tappedMats : _colorMats;
            if (cache.TryGetValue(color, out var m) && m != null) return m;
            var template = HotUpdateAssets.Load<Material>("BattleTile_Zone02", ZoneMatPath);
            if (template == null) { WarnTemplate(); return null; }
            var c = BattleTileFx.ElementColorOf(color); // 底色单源=UiStyle.ManaBlock*（与费用块/描边环同源）
            if (tapped) c *= 0.55f;                     // 横置压暗（2026-11-15 定案）
            m = new Material(template) { name = "LandStrip_" + color + (tapped ? "_T" : "") };
            m.SetColor("_BaseColor", c);
            cache[color] = m;
            return m;
        }

        private static void WarnTemplate()
        {
            if (_warned) return;
            _warned = true;
            Debug.LogWarning("[LandStrip] 着色模板缺失（BattleTile_Zone02_P1Unit.mat）——面片走预制体自带材质");
        }

        /// <summary>拆台清池（BattleStageDirector.DismountBoard 调用）：面片/标签随棋盘根销毁，
        /// 运行时材质实例逐张显式销毁（跨域 fake-null 防口）。</summary>
        public static void Unmount()
        {
            for (int o = 0; o < 2; o++)
            {
                foreach (var e in _quads[o].Values)
                    if (e?.Go != null)
                    {
                        if (Application.isPlaying) Object.Destroy(e.Go);
                        else Object.DestroyImmediate(e.Go);
                    }
                _quads[o].Clear();
                _nodes[o] = null;
                _capLabels[o] = null; // 底板/标签随棋盘根销毁
                _lastCount[o] = _lastCap[o] = -1;
                _geo[o] = null;
            }
            DestroyMat(_plateMat);
            _plateMat = null;
            foreach (var m in _colorMats.Values) DestroyMat(m);
            _colorMats.Clear();
            foreach (var m in _tappedMats.Values) DestroyMat(m);
            _tappedMats.Clear();
        }

        private static void DestroyMat(Material m)
        {
            if (m == null) return;
            if (Application.isPlaying) Object.Destroy(m);
            else Object.DestroyImmediate(m);
        }

        /// <summary>单张地牌面片行为件（挂 LandCardQuad 实例）：横置朝向 0.12s slerp 平滑
        ///（材质/压暗由 LandStripFx 对账直写）+ 点按缩放脉冲（OnLandQuadClick 反馈）。</summary>
        private sealed class LandCardQuadView : MonoBehaviour
        {
            private bool _tapped;
            private Material _matNormal, _matTapped;
            private Renderer _rend;
            private float _pop;
            private Vector3 _baseScale; // 建时排布基准（EnsureQuad 先定缩放再挂本件）——脉冲乘子基准

            public void SetMaterials(Material normal, Material tapped)
            {
                _matNormal = normal;
                _matTapped = tapped;
                ApplyMat();
            }

            public void SetTapped(bool tapped)
            {
                if (_tapped == tapped) return;
                _tapped = tapped;
                ApplyMat();
            }

            public void PressPop() => _pop = 1f;

            private void ApplyMat()
            {
                if (_rend == null) _rend = GetComponent<Renderer>();
                var m = _tapped ? _matTapped : _matNormal;
                if (m != null && _rend != null) _rend.sharedMaterial = m;
            }

            private void LateUpdate()
            {
                if (_baseScale == Vector3.zero) _baseScale = transform.localScale;
                var target = _tapped ? CardPlateMetrics.Landscape : CardPlateMetrics.Portrait;
                transform.localRotation = CardPlateMetrics.TurnTime <= 0f
                    ? target
                    : Quaternion.Slerp(transform.localRotation, target,
                        Mathf.Clamp01(Time.deltaTime / CardPlateMetrics.TurnTime));
                if (_pop > 0f) // 点按脉冲：1.12 倍回弹 0.25s（乘子回乘基准，不累积）
                {
                    _pop = Mathf.Max(0f, _pop - Time.deltaTime * 4f);
                    float k = 1f + 0.12f * _pop;
                    transform.localScale = new Vector3(_baseScale.x * k, _baseScale.y * k, _baseScale.z);
                }
            }
        }
    }
}
