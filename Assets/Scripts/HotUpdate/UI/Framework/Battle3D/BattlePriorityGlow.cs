using Tide.HotUpdate;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SynergyUI
{
    /// <summary>
    /// 优先权底部面板（2026-10-15 两轮定案→2026-10-23 蜂窝版→**2026-10-30 大包围终版**）：
    /// 战场底部每方一张大包围 mesh=**落盘资产** BattlePriorityPanel_Self/_Opp.asset（编辑器烘焙自
    /// BattleTileFx.BuildSideRegionMesh：中心连接式扇面三角、Map 世界系绝对坐标）——己方=BoardLayout
    /// z0..3、对手=z3..6（**各含中线行**），全部地块视觉剪影的凸包+四周斜接偏移外扩一条地缝宽
    ///（非正六边形大致包围、缺口不进凸包）。旧蜂窝逐格版（相互隔离六边形、中线留缝）按用户
    /// 2026-10-30 口径退役：简化成整片、相邻不截断连成一片；盘形/格距改动须重烘焙 mesh。
    /// 梯度=Tide/BattlePriorityPanel 按 UV 归一（v=己方 0 己侧亮边 → 1 中线侧灭 / 对手烘反，
    /// 亮边在各自外侧；**2026-10-26 灭线修复**沿用：saturate(1-v) 全跨度衰减）；亮方本色、
    /// 另一方暗置 12%（2026-10-26 扳机锚侧定案：亮**发动方**——栈非空=栈顶效果归属侧，
    /// 对手触发的效果亮对手侧；空栈=回合方；seat 由 BattleFieldView.GlowSeatOf 复合推导）。
    ///
    /// 面片高度=世界 y=PanelY（高于地形顶 ≈-0.78、低于地块底 ≈-0.197，地缝视线里位于
    /// 地面之上、地块之下——2026-10-30 格距×1.1 加宽地缝即为本面板的表现空间）。
    /// 挂 BattleStageDirector.MapRoot（Map 恒 identity=世界坐标），无 Collider 不挡棋盘射线；
    /// DismountBoard 显式销毁。双方面板在中线行带重叠：additive 叠加且双方中线侧均已渐灭，
    /// 无叠面冲突。
    /// </summary>
    public static class BattlePriorityGlow
    {
        /// <summary>优先权高亮=红蓝分色（2026-10-24 用户定案改回，v6 鎏金单色 2026-10-23 §11.2 退役）：
        /// 己方持有=蓝（近 BattleFieldView.P1UnitColor 提亮作发光用）、对手持有=红（近 P2UnitColor 提亮）；
        /// 非持有方暗置 12%（DimK 亮暗对调语义不变）。</summary>
        public static readonly Color SelfColor = new Color(0.30f, 0.55f, 1.00f);
        public static readonly Color OppColor = new Color(1.00f, 0.32f, 0.26f);

        /// <summary>非持有方暗置系数：面片仍在场可读归属，扳机切换=亮暗对调。</summary>
        private const float DimK = 0.12f;

        // 2026-10-26 资源重组迁移：Battle3D/Materials → Art/Materials（旧路径文件已删，
        // 指旧路径=材质加载空=面板整组不建——「场景里找不到了」根因）。
        private const string PanelMatPath = "Assets/Art/Materials/BattlePriorityPanel.mat";
        // 面板 mesh=落盘资产（2026-10-30 终案）：编辑器烘焙自 BattleTileFx.BuildSideRegionMesh
        //（中心连接式扇面·Map 世界系绝对坐标）；盘形/格距改动须重烘焙（与 BattleStage.prefab 同节奏）
        private const string PanelMeshSelfPath = "Assets/Art/Battle3D/BattlePriorityPanel_Self.asset";
        private const string PanelMeshOppPath = "Assets/Art/Battle3D/BattlePriorityPanel_Opp.asset";
        private static readonly int PanelColorId = Shader.PropertyToID("_PanelColor");

        // 2026-10-23 v4 §10.1：-0.77（地编让位·被地面件顶面挡）→ -0.30（地形顶 ≈-0.78 与
        // 地块底 ≈-0.197 之间——地缝里读优先权面板）。回滚=本值改回 -0.2（初版悬空整盘平铺）。
        private const float PanelY = -0.30f;

        private static Transform _panelsRoot;         // Map 下独立根「fx-priority-bars」
        private static Renderer _selfPanel, _oppPanel; // null=未建/已拆（fake-null 判死重建）
        private static Material _panelMat;
        private static MaterialPropertyBlock _mpb;
        private static bool _depsLogged;
        private static Mesh _selfMesh, _oppMesh;      // 面板 mesh=落盘资产（缺失回落运行时生成）：建一次跨挂载复用

        /// <summary>域重载关闭（Enter Play Mode Options）防跨 Play 残留：与 BattleTileFx.ResetStatics 同口径。
        ///（面板 mesh 是引擎对象无域态，不在此清——fake-null 重建时复用缓存几何与配套中心。）</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _panelsRoot = null;
            _selfPanel = _oppPanel = null;
            _panelMat = null;
            _depsLogged = false;
        }

        /// <summary>拆台销毁（BattleStageDirector.DismountBoard 调用；Map 随后整根销毁，此处显式拆+清引用）。</summary>
        public static void Unmount()
        {
            if (_panelsRoot != null)
            {
                if (Application.isPlaying) Object.Destroy(_panelsRoot.gameObject);
                else Object.DestroyImmediate(_panelsRoot.gameObject);
            }
            _panelsRoot = null;
            _selfPanel = _oppPanel = null;
        }

        /// <summary>扳机切换对账（BattleFieldView.Apply 调用，幂等）：seat 侧面片本色、另一侧暗置
        ///（2026-10-26 锚侧定案：seat=发动方座位——栈顶效果归属；seat&lt;0=双方暗置，如终局/未知）。</summary>
        public static void Apply(int seat)
        {
            EnsurePanels();
            Paint(_selfPanel, SelfColor, seat == 0);
            Paint(_oppPanel, OppColor, seat == 1);
        }

        // ======================================== 内部 ========================================

        private static void EnsurePanels()
        {
            if (_selfPanel != null && _oppPanel != null) return;
            var mapRoot = BattleStageDirector.MapRoot;
            if (mapRoot == null) return;

            if (_panelMat == null)
                _panelMat = HotUpdateAssets.Load<Material>("BattlePriorityPanel", PanelMatPath);
            if (_panelMat == null) { WarnDeps("BattlePriorityPanel.mat——优先权面片降级为不显示"); return; }

            if (_panelsRoot == null)
            {
                var go = new GameObject("fx-priority-bars");
                _panelsRoot = go.transform;
                _panelsRoot.SetParent(mapRoot, false);
            }
            _selfPanel = BuildPanel("fx-priority-self", selfSide: true);
            _oppPanel = BuildPanel("fx-priority-opp", selfSide: false);
        }

        private static Renderer BuildPanel(string name, bool selfSide)
        {
            // 2026-10-30 终案：mesh=落盘资产（中心连接式扇面·绝对坐标）；缺失回落运行时生成并告警
            if (selfSide && _selfMesh == null)
            {
                _selfMesh = HotUpdateAssets.Load<Mesh>("BattlePriorityPanel_Self", PanelMeshSelfPath);
                if (_selfMesh == null)
                { WarnDeps("BattlePriorityPanel_Self.asset——回落运行时生成（盘形/格距改动后请重烘焙）"); _selfMesh = BattleTileFx.BuildSideRegionMesh(true); }
            }
            if (!selfSide && _oppMesh == null)
            {
                _oppMesh = HotUpdateAssets.Load<Mesh>("BattlePriorityPanel_Opp", PanelMeshOppPath);
                if (_oppMesh == null)
                { WarnDeps("BattlePriorityPanel_Opp.asset——回落运行时生成（盘形/格距改动后请重烘焙）"); _oppMesh = BattleTileFx.BuildSideRegionMesh(false); }
            }
            var mesh = selfSide ? _selfMesh : _oppMesh;
            if (mesh == null) return null;
            var go = new GameObject(name);
            go.transform.SetParent(_panelsRoot, false);
            // mesh 顶点=Map 世界系绝对坐标（Map 恒 identity）——恒 identity 旋转、原点摆位，y 钉 PanelY
            go.transform.localPosition = new Vector3(0f, PanelY, 0f);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var rend = go.AddComponent<MeshRenderer>();
            rend.sharedMaterial = _panelMat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
            return rend;
        }

        private static void Paint(Renderer panel, Color c, bool holding)
        {
            if (panel == null) return;
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            _mpb.SetColor(PanelColorId, holding ? c : c * DimK);
            panel.SetPropertyBlock(_mpb);
        }

        private static void WarnDeps(string what)
        {
            if (_depsLogged) return;
            _depsLogged = true;
            Debug.LogWarning("[BattlePriorityGlow] 依赖缺失：" + what);
        }
    }
}
