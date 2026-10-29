using System.Text;
using GameBoard;
using UnityEngine;

namespace SynergyUI
{
    /// <summary>
    /// 对战棋盘格位服务（2026-10-13 战场重做二段→缩盘定案二段：HexMap/DOTS 已整体迁出，
    /// 战场=BattleHex 单枚 mesh 运行时拼装 8×7 全盘（2026-10-14 中线加行 9×7 → 当晚缩盘），见 BattleHexBoard）。
    ///
    /// 8×7 odd-r 六角格心公式（单一事实源，与 BoardMath 邻居规则配套）：
    /// local = ((x + (z&amp;1)·0.5) × InnerRadius·2, 0, z × OuterRadius·1.5)，OuterRadius = 1m。
    /// 生命周期：BattleStageDirector 装台 Mount(实例根)/拆台 Unmount；
    /// BattleFieldView 摆位、取景、BoardMountSmoke 冒烟统一消费 WorldPositionOf。
    /// </summary>
    public static class BattleBoardGrid
    {
        public const float OuterRadius = 1f;
        public const float InnerRadius = 0.866025404f;

        /// <summary>地块视觉缩格比例（2026-10-23 战场美化 P0：整盘统一缩小露缝，缝下见祭坛台面）。
        /// 格心/点选面积不变——拼装设 localScale=TileScale，BattleHexBoard 加同名反缩拾取代理
        /// （预制体根实况=MeshCollider 非简报所记 BoxCollider）；挂格子的平贴文字（中线行/
        /// 地牌计数条）按 1/TileScale 反缩保原尺寸。单一来源勿写魔数。</summary>
        public const float TileScale = 0.82f;

        /// <summary>棋盘实例根（装台后非 null；null=未装台）。</summary>
        public static Transform Root { get; private set; }

        /// <summary>格心局部坐标（prefab 本地系；世界位 = Root.TransformPoint）。</summary>
        public static Vector3 CellCenterLocal(int x, int z)
            => new Vector3((x + (z & 1) * 0.5f) * InnerRadius * 2f, 0f, z * OuterRadius * 1.5f);

        /// <summary>逻辑格 → 世界坐标（未装台返回局部值——调用方用 Ready 门禁）。</summary>
        public static Vector3 WorldPositionOf(int x, int z)
            => Root != null ? Root.TransformPoint(CellCenterLocal(x, z)) : CellCenterLocal(x, z);

        /// <summary>棋盘就绪（装台完成；mesh 为预生成资产，无流式装载等待）。</summary>
        public static bool Ready => Root != null;

        /// <summary>棋盘局部包围盒（取景用；含 0.2 高度余量）。算全盘
        /// （0..Width−1 × 0..Height−1=拼装域，含两侧特殊格列与中线行）——取景贴合 7×7 实际可见盘。</summary>
        public static Bounds LocalBounds()
        {
            var min = CellCenterLocal(0, 0);
            var max = CellCenterLocal(BoardLayout.Width - 1, BoardLayout.Height - 1);
            var pad = new Vector3(OuterRadius, 0.2f, OuterRadius);
            return new Bounds((min + max) * 0.5f, max - min + pad * 2f);
        }

        public static void Mount(Transform root) => Root = root;
        public static void Unmount() => Root = null;

        /// <summary>装台自检（数据层证据）：实例根/mesh/subMesh 数/材质数/格心公式抽样对拍，明细走日志。</summary>
        public static bool SelfCheck()
        {
            var sb = new StringBuilder("[BattleBoardGrid]");
            bool ok = Root != null;
            sb.Append($" 根={(Root != null ? Root.name : "null!")}");

            var mf = Root != null ? Root.GetComponentInChildren<MeshFilter>() : null;
            var mr = Root != null ? Root.GetComponentInChildren<MeshRenderer>() : null;
            if (mf == null || mf.sharedMesh == null) { ok = false; sb.Append(" mesh=缺失!"); }
            else sb.Append($" mesh顶点={mf.sharedMesh.vertexCount} subMesh={mf.sharedMesh.subMeshCount}");
            if (mr == null || mr.sharedMaterials.Length != mf.sharedMesh.subMeshCount)
            { ok = false; sb.Append($" 材质数={(mr != null ? mr.sharedMaterials.Length : 0)}≠subMesh!"); }
            else sb.Append($" 材质={mr.sharedMaterials.Length}");

            // 格心公式抽样对拍（BoardLayout 常量格，独立复算口径与 CellCenterLocal 同式）
            int spotBad = 0;
            foreach (var (x, z) in new[] { (1, 0), (2, 0), (3, 3), (6, 5), (0, 1) })
            {
                var expect = new Vector3((x + (z & 1) * 0.5f) * InnerRadius * 2f, 0f, z * OuterRadius * 1.5f);
                if ((WorldPositionOf(x, z) - (Root != null ? Root.TransformPoint(expect) : expect)).sqrMagnitude > 1e-6f)
                    spotBad++;
            }
            sb.Append($" 公式抽拍异常={spotBad}");
            if (spotBad > 0) ok = false;

            // 拼装规格（2026-10-29 P11 盘形 6765676）：全盘实拼 43 tile（每半场行格数 6/7/6 +
            // 中线 5 格居中；缺口 6=六格行尾格 + 中线两端），逐 tile 须带 MeshFilter/sharedMesh
            int tiles = 0, tileBad = 0;
            if (Root != null)
                foreach (Transform child in Root)
                {
                    tiles++;
                    var f = child.GetComponent<MeshFilter>();
                    if (f == null || f.sharedMesh == null) tileBad++;
                }
            sb.Append($" 格数={tiles}(期望43) 缺mesh格={tileBad}");
            if (tiles != 43 || tileBad > 0) ok = false;

            Debug.Log(ok ? $"[PASS]{sb}" : $"[FAIL]{sb}");
            return ok;
        }
    }
}
