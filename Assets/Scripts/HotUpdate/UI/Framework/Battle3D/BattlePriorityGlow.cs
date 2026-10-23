using Tide.HotUpdate;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SynergyUI
{
    /// <summary>
    /// 优先权底部蜂窝面（2026-10-15 两轮定案→2026-10-23 v4 §10.1 蜂窝终版）：战场底部两张
    /// **六边形蜂窝网格**各覆盖半场（己方=行 z0..2、对手=行 z4..6，中线行 z3 留缝）——按
    /// BattleBoardGrid.CellCenterLocal 的格位、格半径 1.0 未缩格生成，与地块剪影同形
    ///（BattleHex 顶面六角实测角点），合并成一张 mesh；相邻格边贴边，被 0.82 缩格地块
    /// 盖住的部分自然不可见——从 0.31m 地缝看下去是蜂窝面而非整块平板。
    /// 梯度=Tide/BattlePriorityPanel 按 UV 归一（u=(x-x0)/14.7、v=己方 (z+1.7)/5 / 对手
    /// (9.5-z)/5——对手片翻转烘进 UV，亮边在各自外侧、向中线衰减到 0，footprint 与原
    /// 14.7×5 Quad 版一致）；持有方本色、非持有方暗置 12%（扳机切换=亮暗对调）；
    /// holderSeat 由 BattleViewData 复合推导（栈非空=持有方，栈空=回合方）。
    ///
    /// 面片高度=世界 y=PanelY（-0.2 → -0.77 让位地编 → v4 §10.1 抬到 -0.30：高于地形顶
    /// ≈-0.78、低于缩格地块底 ≈-0.164，地缝视线里蜂窝位于地面之上、地块之下）。
    /// 挂 BattleStageDirector.MapRoot（Map 恒 identity=世界坐标；棋盘根子计数被
    /// SelfCheck 钉死 55 不得加子），无 Collider 不挡棋盘射线；DismountBoard 显式销毁。
    /// </summary>
    public static class BattlePriorityGlow
    {
        /// <summary>优先权高亮=v6 唯一鎏金强调 #C6A24B（2026-10-23 §11.2）：持有方亮金、非持有方
        /// 暗置 12%（DimK 亮暗对调语义不变）；归属由亮起的半场/时间格位置读出，不再红蓝分色。</summary>
        public static readonly Color SelfColor = new Color(0.776f, 0.635f, 0.294f);
        public static readonly Color OppColor = new Color(0.776f, 0.635f, 0.294f);

        /// <summary>非持有方暗置系数：面片仍在场可读归属，扳机切换=亮暗对调。</summary>
        private const float DimK = 0.12f;

        private const string PanelMatPath = "Assets/Art/Battle3D/Materials/BattlePriorityPanel.mat";
        private static readonly int PanelColorId = Shader.PropertyToID("_PanelColor");

        // 摆位（Map 恒 identity=世界系；面板中心沿用原 Quad footprint 中心：14.7×5、中线留缝）
        private static readonly Vector3 SelfPos = new Vector3(7.3f, PanelY, 0.8f);
        private static readonly Vector3 OppPos = new Vector3(7.3f, PanelY, 7.0f);
        // 2026-10-23 v4 §10.1：-0.77（地编让位·被地面件顶面挡）→ -0.30（地形顶 ≈-0.78 与
        // 缩格地块底 ≈-0.164 之间——地缝里读蜂窝面）。回滚=本值改回 -0.2（初版悬空整盘平铺）。
        private const float PanelY = -0.30f;

        // UV footprint（原 Quad 版同一事实源）：西缘 x0=-0.05=7.3-14.7/2；己方南缘 -1.7=0.8-5/2；
        // 对手北缘 9.5=7+5/2。v 己方=(z+1.7)/5 南亮北灭、对手=(9.5-z)/5 北亮南灭（=原 180° 翻转）。
        private const float FootX0 = -0.05f;
        private const float FootW = 14.7f;
        private const float FootD = 5f;
        private const float SelfV0 = -1.7f;
        private const float OppV1 = 9.5f;

        /// <summary>蜂窝单格外轮廓（BattleHex.asset 顶面六角实测：X 半宽 0.814 / Z 半顶 0.94，
        /// 与地块剪影同形；半径 1.0 未缩格，相邻格边贴边无缝）。数组逆时针（atan2 升序）。</summary>
        private static readonly Vector2[] HexCorners =
        {
            new Vector2(-0.814f, -0.47f), new Vector2(0f, -0.94f), new Vector2(0.814f, -0.47f),
            new Vector2(0.814f, 0.47f), new Vector2(0f, 0.94f), new Vector2(-0.814f, 0.47f),
        };

        private static Transform _panelsRoot;         // Map 下独立根「fx-priority-bars」
        private static Renderer _selfPanel, _oppPanel; // null=未建/已拆（fake-null 判死重建）
        private static Material _panelMat;
        private static MaterialPropertyBlock _mpb;
        private static bool _depsLogged;
        private static Mesh _selfMesh, _oppMesh;      // 蜂窝几何恒定：建一次跨挂载复用

        /// <summary>域重载关闭（Enter Play Mode Options）防跨 Play 残留：与 BattleTileFx.ResetStatics 同口径。
        ///（蜂窝 mesh 是引擎对象无域态，不在此清——fake-null 重建时复用缓存几何。）</summary>
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

        /// <summary>扳机切换对账（BattleFieldView.Apply 调用，幂等）：持有方面片本色、非持有方暗置
        /// （holderSeat&lt;0=双方暗置，如终局/未知）。</summary>
        public static void Apply(int holderSeat)
        {
            EnsurePanels();
            Paint(_selfPanel, SelfColor, holderSeat == 0);
            Paint(_oppPanel, OppColor, holderSeat == 1);
        }

        // ======================================== 内部 ========================================

        private static void EnsurePanels()
        {
            if (_selfPanel != null && _oppPanel != null) return;
            var mapRoot = BattleStageDirector.MapRoot;
            if (mapRoot == null) return;

            if (_panelMat == null)
                _panelMat = HotUpdateAssets.Load<Material>("BattlePriorityPanel", PanelMatPath);
            if (_panelMat == null) { WarnDeps(); return; }

            if (_panelsRoot == null)
            {
                var go = new GameObject("fx-priority-bars");
                _panelsRoot = go.transform;
                _panelsRoot.SetParent(mapRoot, false);
            }
            _selfPanel = BuildPanel("fx-priority-self", SelfPos, selfSide: true);
            _oppPanel = BuildPanel("fx-priority-opp", OppPos, selfSide: false);
        }

        private static Renderer BuildPanel(string name, Vector3 center, bool selfSide)
        {
            var mesh = selfSide ? (_selfMesh ?? (_selfMesh = BuildHoneycomb(center, selfSide)))
                                : (_oppMesh ?? (_oppMesh = BuildHoneycomb(center, selfSide)));
            var go = new GameObject(name);
            go.transform.SetParent(_panelsRoot, false);
            go.transform.localPosition = center; // mesh 顶点已按中心偏移建在 XZ 平面——恒 identity 旋转
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var rend = go.AddComponent<MeshRenderer>();
            rend.sharedMaterial = _panelMat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
            return rend;
        }

        /// <summary>半场蜂窝 mesh：格位=CellCenterLocal（格半径 1.0 未缩格，己方 z0..2 / 对手 z4..6，
        /// x0..7 全列）；每格=中心点+六角扇面 6 三角（绕序朝上=Cull Back 俯视可见）；
        /// UV 按 footprint 归一保 BattlePriorityPanel 梯度（见类注）。</summary>
        private static Mesh BuildHoneycomb(Vector3 center, bool selfSide)
        {
            const int cols = 8, cellTris = 6;
            int zFrom = selfSide ? 0 : 4, zTo = selfSide ? 2 : 6;
            int rows = zTo - zFrom + 1, cells = rows * cols;
            var boardPos = BattleBoardGrid.Root != null ? BattleBoardGrid.Root.position
                                                        : new Vector3(0.866f, 0f, -0.75f); // 未装台兜底=拼装偏移
            var verts = new Vector3[cells * 7];
            var uvs = new Vector2[cells * 7];
            var tris = new int[cells * cellTris * 3];
            int vi = 0, ti = 0;
            for (int z = zFrom; z <= zTo; z++)
            {
                for (int x = 0; x < cols; x++, vi += 7)
                {
                    var c = BattleBoardGrid.CellCenterLocal(x, z);
                    float wx = c.x + boardPos.x, wz = c.z + boardPos.z;
                    verts[vi] = new Vector3(wx - center.x, 0f, wz - center.z);
                    uvs[vi] = new Vector2((wx - FootX0) / FootW,
                                          selfSide ? (wz - SelfV0) / FootD : (OppV1 - wz) / FootD);
                    for (int k = 0; k < 6; k++)
                    {
                        var corner = HexCorners[k];
                        verts[vi + 1 + k] = new Vector3(wx + corner.x - center.x, 0f, wz + corner.y - center.z);
                        uvs[vi + 1 + k] = new Vector2((wx + corner.x - FootX0) / FootW,
                                                      selfSide ? (wz + corner.y - SelfV0) / FootD
                                                               : (OppV1 - wz - corner.y) / FootD);
                    }
                    for (int k = 0; k < 6; k++)
                    {
                        int n = (k + 1) % 6; // (中心, 下一角, 当前角)=俯视顺时针，法线 +Y
                        tris[ti++] = vi;
                        tris[ti++] = vi + 1 + n;
                        tris[ti++] = vi + 1 + k;
                    }
                }
            }
            var mesh = new Mesh { name = selfSide ? "priority-honeycomb-self" : "priority-honeycomb-opp" };
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void Paint(Renderer panel, Color c, bool holding)
        {
            if (panel == null) return;
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            _mpb.SetColor(PanelColorId, holding ? c : c * DimK);
            panel.SetPropertyBlock(_mpb);
        }

        private static void WarnDeps()
        {
            if (_depsLogged) return;
            _depsLogged = true;
            Debug.LogWarning("[BattlePriorityGlow] 依赖缺失：BattlePriorityPanel.mat——优先权面片降级为不显示");
        }
    }
}
