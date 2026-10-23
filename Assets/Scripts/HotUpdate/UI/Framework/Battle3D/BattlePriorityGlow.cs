using Tide.HotUpdate;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SynergyUI
{
    /// <summary>
    /// 优先权底部面片（2026-10-15 用户定案两轮：半场逐格泛光退役→底部双光条盒体→终版=双长方「面片」
    /// ——盒体侧立面出高光已否决，只留面）。战场底部两张平铺面片各覆盖半场（footprint 用户实测定档：
    /// 己方 (7.3,·,0.8) / 对手 (7.3,·,7)，14.7×5），梯度=靠己方一侧最亮、向中线衰减到 0
    ///（Tide/BattlePriorityPanel：k=pow(saturate(−y·2),GradPow)，对手片 Y 转 180° 复用同式）。
    /// 持有方本色、非持有方暗置 12%（扳机切换=亮暗对调）；holderSeat 由 BattleViewData 复合推导：
    /// 栈非空=优先权持有方，栈空=回合方（见 BattleFieldView.PrioritySeatOf）。
    ///
    /// 面片高度=世界 y=PanelY（2026-10-15 用户定档 -0.2 → 2026-10-23 战场美化让位改贴台面，见常量注）。
    /// 挂 BattleStageDirector.MapRoot（Map 恒 identity=世界坐标；棋盘根子计数被
    /// SelfCheck 钉死 55 不得加子），无 Collider 不挡棋盘射线；DismountBoard 显式销毁。
    /// </summary>
    public static class BattlePriorityGlow
    {
        /// <summary>己方持有=蓝（近 BattleFieldView.P1UnitColor 提亮作发光用）。</summary>
        public static readonly Color SelfColor = new Color(0.30f, 0.55f, 1.00f);
        /// <summary>对手持有=红（近 P2UnitColor 提亮）。</summary>
        public static readonly Color OppColor = new Color(1.00f, 0.32f, 0.26f);

        /// <summary>非持有方暗置系数：面片仍在场可读归属，扳机切换=亮暗对调。</summary>
        private const float DimK = 0.12f;

        private const string PanelMatPath = "Assets/Art/Battle3D/Materials/BattlePriorityPanel.mat";
        private static readonly int PanelColorId = Shader.PropertyToID("_PanelColor");

        // 摆位（Map 恒 identity=世界系；footprint 2026-10-15 用户实测：各片覆盖半场，中线行留缝）
        private static readonly Vector3 PanelSize = new Vector3(14.7f, 5f, 1f); // Quad 网格 XY 平面：x=盘宽 y=盘深
        // 2026-10-23 战场美化 P0' 地编让位（plan 红线7"为让位环境"·待 DSH 确认）：原定档 y=-0.2 悬空
        // 平铺整盘——从缩格地缝往下看视线必先穿这两张面片，台面/法阵被完全遮蔽（缝里只读蓝色面片）。
        // 地编版层次：面片抬到地面件顶(≈-0.78)之上、金法阵(≈-0.76)之下——从"悬空蓝片"变"台面上的
        // 半场归属光带"：归属语义不变，缝里可透出地面瓦片与法阵金纹。回滚=本值改回 -0.2。
        private const float PanelY = -0.77f;
        private static readonly Vector3 SelfPos = new Vector3(7.3f, PanelY, 0.8f);
        private static readonly Vector3 OppPos = new Vector3(7.3f, PanelY, 7f);
        private static readonly Quaternion SelfRot = Quaternion.Euler(90f, 0f, 0f);  // 平铺朝上·梯度亮边=南(己侧)
        private static readonly Quaternion OppRot = Quaternion.Euler(90f, 180f, 0f); // 平铺朝上·梯度亮边=北(对侧)

        private static Transform _panelsRoot;         // Map 下独立根「fx-priority-bars」
        private static Renderer _selfPanel, _oppPanel; // null=未建/已拆（fake-null 判死重建）
        private static Material _panelMat;
        private static MaterialPropertyBlock _mpb;
        private static bool _depsLogged;

        /// <summary>域重载关闭（Enter Play Mode Options）防跨 Play 残留：与 BattleTileFx.ResetStatics 同口径。</summary>
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
            _selfPanel = BuildPanel("fx-priority-self", SelfPos, SelfRot);
            _oppPanel = BuildPanel("fx-priority-opp", OppPos, OppRot);
        }

        private static Renderer BuildPanel(string name, Vector3 pos, Quaternion rot)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            // CreatePrimitive 自带 Collider 会截胡棋盘射线（射线只打格的既定通路）——即建即拆
            var col = go.GetComponent<Collider>();
            if (Application.isPlaying) Object.Destroy(col);
            else Object.DestroyImmediate(col);
            go.transform.SetParent(_panelsRoot, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = PanelSize;
            var rend = go.GetComponent<MeshRenderer>();
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

        private static void WarnDeps()
        {
            if (_depsLogged) return;
            _depsLogged = true;
            Debug.LogWarning("[BattlePriorityGlow] 依赖缺失：BattlePriorityPanel.mat——优先权面片降级为不显示");
        }
    }
}
