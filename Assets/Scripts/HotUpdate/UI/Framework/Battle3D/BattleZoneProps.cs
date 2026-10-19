using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameBoard;
using Tide.HotUpdate;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SynergyUI
{
    /// <summary>
    /// 特殊格道具层（2026-10-13）：卡组格=长方体卡堆——牌库数 0..60 钳制线性映射高度
    /// （每张 2cm，60 张=1.2m 封顶；0 张隐藏）；脚印 0.86×1.28（2:3 卡面比例，六边形地块内接），
    /// 顶面=卡背材质（BattleCardBack_Back_Mat，subMesh 分材质）、四侧=条纹 shader
    /// （Tide/BattleCardStackSide，世界 Y 定纹距，伸缩不变形）。墓地/除外格=顶面漩涡面片
    /// （Tide/BattleVortex，_Time 自驱；蓝=墓地/紫=除外，双方四格常驻，与卡数无关）。
    ///
    /// 形态与生命周期同 BattleTileFx：道具挂 tile-{x}-{z} 下（随浮起动画同走）；
    /// BattleStageDirector 装台③后 Mount 建（幂等；依赖缺失降级为该格无道具并告警一次），
    /// BattleFieldView.Apply 对账 SetDeckCount 幂等直写高度；拆台 DismountBoard 清索引
    /// （对象随棋盘根销毁，mesh 显式销毁防跨局泄漏）。
    /// </summary>
    public static class BattleZoneProps
    {
        private const string SideMatPath = "Assets/Art/Battle3D/Materials/BattleCardStack_Side.mat";
        private const string TopMatPath = "Assets/Art/Battle3D/Materials/BattleCardBack_Back_Mat.asset";
        private const string VortexGraveMatPath = "Assets/Art/Battle3D/Materials/BattleTile_Vortex_Grave.mat";
        private const string VortexExileMatPath = "Assets/Art/Battle3D/Materials/BattleTile_Vortex_Exile.mat";

        public const int MaxCards = 60;             // 高度映射上限（>60 钳制）
        private const float CardThickness = 0.02f;  // 每张卡 2cm（与侧面 shader 默认 _CardHeight 一致）
        private const float StackW = 0.86f;         // 卡堆脚印 X（六边形内接，内径 0.814）
        private const float StackD = 1.28f;         // 卡堆脚印 Z（2:3 卡面比例，顶点向 0.94）
        private const float VortexSize = 1.44f;     // 漩涡方片边长（渐隐收圆半径≈0.72 < 内径 0.814）
        private const float LiftY = 0.004f;         // 抬离地块顶面（顶面 y=0，防叠面闪，同菲涅尔壳）

        private static readonly Transform[] _stacks = new Transform[2]; // 下标=归属 0/1
        private static readonly Dictionary<long, Transform> _vortexes = new Dictionary<long, Transform>();
        private static Mesh _stackMesh;
        private static Material _sideMat, _topMat, _graveMat, _exileMat, _litTemplate;
        private static bool _depsLogged;

        /// <summary>装台（棋盘拼装后调，幂等）：双方卡堆 + 四格漩涡惰性建齐；返回实建道具数。</summary>
        public static int Mount()
        {
            if (!BattleBoardGrid.Ready) return 0;
            EnsureStack(0);
            EnsureStack(1);
            foreach (int owner in new[] { 0, 1 })
            {
                var grave = GraveCell(owner);
                var exile = ExileCell(owner);
                EnsureVortex(grave.x, grave.z, "grave", GraveMaterial());
                EnsureVortex(exile.x, exile.z, "exile", ExileMaterial());
            }

            int props = 0;
            foreach (var s in _stacks) if (s != null) props++;
            props += _vortexes.Count;
            Debug.Log($"[BattleZoneProps] 装台：卡堆 {(_stacks[0] != null ? 1 : 0) + (_stacks[1] != null ? 1 : 0)}/2"
                      + $" 漩涡 {_vortexes.Count}/4（依赖缺失格降级为无道具）");
            return props;
        }

        /// <summary>对账幂等直写：牌库数 → 卡堆高度（0..MaxCards 钳制；0 张隐藏）。</summary>
        public static void SetDeckCount(int owner, int count)
        {
            if (owner < 0 || owner > 1) return;
            var stack = EnsureStack(owner);
            if (stack == null) return;
            int n = Mathf.Clamp(count, 0, MaxCards);
            stack.localScale = new Vector3(1f, n * CardThickness, 1f);
            stack.gameObject.SetActive(n > 0);
        }

        /// <summary>卡堆顶面世界坐标（2026-10-17 抽卡飞入起点）：未装台/依赖缺失返回 false。
        /// 高度取当前对账值——抽牌瞬间先于 SetDeckCount 变矮调用，拿到的是抽牌前的顶面。</summary>
        public static bool DeckTopWorldPosition(int owner, out Vector3 pos)
        {
            pos = default;
            if (owner < 0 || owner > 1) return false;
            var stack = _stacks[owner];
            if (stack == null) return false; // fake-null 同 EnsureStack 口径
            pos = stack.position + Vector3.up * stack.localScale.y;
            return true;
        }

        /// <summary>卡堆抽牌脉冲（2026-10-17 抽卡演出）：XZ 0.25s 1→1.12→1 正弦，Y 每帧取对账
        /// 现值（与 SetDeckCount 直写共存不打架）；fire-and-forget 纯装饰不占视觉门。</summary>
        public static void PulseDeck(int owner)
        {
            if (owner < 0 || owner > 1) return;
            var stack = _stacks[owner];
            if (stack == null) return;
            PulseDeckAsync(stack).Forget();
        }

        private static async UniTaskVoid PulseDeckAsync(Transform stack)
        {
            float t0 = Time.realtimeSinceStartup;
            const float dur = 0.25f;
            while (stack != null && Time.realtimeSinceStartup - t0 < dur)
            {
                float t = (Time.realtimeSinceStartup - t0) / dur;
                float s = 1f + 0.12f * Mathf.Sin(t * Mathf.PI);
                stack.localScale = new Vector3(s, stack.localScale.y, s);
                await UniTask.Yield();
            }
            if (stack != null)
                stack.localScale = new Vector3(1f, stack.localScale.y, 1f);
        }

        /// <summary>拆台清索引+销毁运行时 mesh（对象本体随棋盘根销毁；DismountBoard 调用）。</summary>
        public static void Unmount()
        {
            _stacks[0] = null;
            _stacks[1] = null;
            _vortexes.Clear();
            if (_stackMesh != null)
            {
                if (Application.isPlaying) Object.Destroy(_stackMesh);
                else Object.DestroyImmediate(_stackMesh);
                _stackMesh = null;
            }
        }

        /// <summary>域重载关闭（Enter Play Mode Options）防跨 Play 残留：与 BattleTileFx.ResetStatics 同口径。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _stacks[0] = null;
            _stacks[1] = null;
            _vortexes.Clear();
            _stackMesh = null;
            _sideMat = null;
            _topMat = null;
            _graveMat = null;
            _exileMat = null;
            _litTemplate = null;
            _depsLogged = false;
        }

        // ======================================== 内部 ========================================

        private static (int x, int z) DeckCell(int owner)
            => owner == 0 ? (BoardLayout.P1DeckX, BoardLayout.P1DeckZ)
                          : BoardLayout.Rotate180(BoardLayout.P1DeckX, BoardLayout.P1DeckZ);

        private static (int x, int z) GraveCell(int owner)
            => owner == 0 ? (BoardLayout.P1GraveX, BoardLayout.P1GraveZ)
                          : BoardLayout.Rotate180(BoardLayout.P1GraveX, BoardLayout.P1GraveZ);

        /// <summary>墓地格（公开访问，2026-10-13 战斗编舞）：死亡漩涡吸走演出取吸入口世界坐标。</summary>
        public static (int x, int z) GraveCellOf(int owner) => GraveCell(owner);

        private static (int x, int z) ExileCell(int owner)
            => owner == 0 ? (BoardLayout.P1ExileX, BoardLayout.P1ExileZ)
                          : BoardLayout.Rotate180(BoardLayout.P1ExileX, BoardLayout.P1ExileZ);

        private static long Key(int x, int z) => ((long)x << 32) | (uint)z;

        private static Transform TileOf(int x, int z)
        {
            return BattleBoardGrid.Root != null ? BattleBoardGrid.Root.transform.Find($"tile-{x}-{z}") : null;
        }

        private static Transform EnsureStack(int owner)
        {
            if (_stacks[owner] != null) return _stacks[owner]; // fake-null：棋盘根销毁后整树亡，不会悬挂
            var cell = DeckCell(owner);
            var tile = TileOf(cell.x, cell.z);
            if (tile == null) return null;
            if (!EnsureStackMaterials()) return null;

            var go = new GameObject($"deck-stack-{(owner == 0 ? "self" : "opp")}");
            go.transform.SetParent(tile, false);
            go.transform.localPosition = new Vector3(0f, LiftY, 0f);
            // 卡背竖版 v=1 朝 +Z（己方远端）；对方(P2)半场旋转 180° 使卡面朝向其玩家侧
            go.transform.localRotation = Quaternion.Euler(0f, owner == 0 ? 0f : 180f, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = BuildStackMesh();
            var rend = go.AddComponent<MeshRenderer>();
            rend.sharedMaterials = new[] { _sideMat, _topMat }; // subMesh0=四侧+底(条纹) 1=顶面(卡背)
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _stacks[owner] = go.transform;
            return go.transform;
        }

        private static bool EnsureStackMaterials()
        {
            if (_sideMat == null)
            {
                _sideMat = HotUpdateAssets.Load<Material>("BattleCardStack_Side", SideMatPath);
                if (_sideMat == null) { WarnDeps("BattleCardStack_Side.mat"); return false; }
            }
            if (_topMat == null)
            {
                _topMat = HotUpdateAssets.Load<Material>("BattleCardBack_Back", TopMatPath);
                if (_topMat == null) { WarnDeps("BattleCardBack_Back_Mat（卡背资产）"); return false; }
            }
            return true;
        }

        /// <summary>开场段四候选卡组堆共享资产（2026-10-19 五段式开局·BattleDeckPickStage）：与卡组格
        /// 正主卡堆同 mesh 同双材质（卡背顶面+条纹侧面）；mesh 生命周期随 Unmount（开场期恒已装台）。
        /// false=依赖缺失（调用方降级为无堆）。</summary>
        public static bool TryGetStackAssets(out Mesh mesh, out Material side, out Material top)
        {
            mesh = null; side = null; top = null;
            if (!EnsureStackMaterials()) return false;
            mesh = BuildStackMesh();
            side = _sideMat;
            top = _topMat;
            return true;
        }

        /// <summary>URP Lit 纯色材质模板（2026-10-19 开场件公用）：硬币面盘/选堆色环的着色克隆源
        ///（HeroPillarMaterial 同源模板）。返回资产原件——调用方须 new Material 克隆后改色，勿直改。</summary>
        public static Material LitTemplate
        {
            get
            {
                if (_litTemplate == null)
                    _litTemplate = HotUpdateAssets.Load<Material>(
                        "BattleTile_Zone02", "Assets/Art/Battle3D/Materials/BattleTile_Zone02_P1Unit.mat");
                return _litTemplate;
            }
        }

        private static void EnsureVortex(int x, int z, string kind, Material mat)
        {
            var key = Key(x, z);
            if (_vortexes.TryGetValue(key, out var v) && v != null) return;
            if (mat == null) return; // 材质缺失已由加载侧告警
            var tile = TileOf(x, z);
            if (tile == null) return;

            var go = GameObject.CreatePrimitive(PrimitiveType.Quad); // 自带 collider 须拆
            var col = go.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Object.Destroy(col);
                else Object.DestroyImmediate(col); // 编辑态验证可走（Destroy 编辑态非法）
            }
            go.name = $"fx-vortex-{kind}";
            go.transform.SetParent(tile, false);
            go.transform.localPosition = new Vector3(0f, 0.01f, 0f); // 地块顶面上 1cm（低于菲涅尔壳语义层）
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // 面朝上（同 BattleTileFx 元素图标）
            go.transform.localScale = Vector3.one * VortexSize;
            var rend = go.GetComponent<MeshRenderer>();
            rend.sharedMaterial = mat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
            _vortexes[key] = go.transform;
        }

        /// <summary>卡堆长方体 mesh：内建 Cube 变形（脚印 0.86×1.28 绝对尺寸、Y∈[0,1] pivot=底心，
        /// Y 缩放即高度）；subMesh0=四侧+底（法线 y≤0 三角）、subMesh1=顶面（法线 y>0），
        /// 顶面 UV 按 (x,z) 显式重映射——卡背竖版 v=1 落 +Z，脚印 2:3=贴图 344×512 无拉伸。</summary>
        private static Mesh BuildStackMesh()
        {
            if (_stackMesh != null) return _stackMesh;
            var src = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var srcVerts = src.vertices;
            var srcTris = src.triangles;

            var verts = new Vector3[srcVerts.Length];
            var uvs = new Vector2[srcVerts.Length];
            for (int i = 0; i < srcVerts.Length; i++)
            {
                var v = srcVerts[i]; // 内建 Cube 顶点 ±0.5
                verts[i] = new Vector3(v.x * StackW, v.y + 0.5f, v.z * StackD); // y∈[0,1]
                uvs[i] = v.y > 0.25f // 顶面顶点：竖版卡背 UV（v=1 在 +Z）
                    ? new Vector2(verts[i].x / StackW + 0.5f, verts[i].z / StackD + 0.5f)
                    : Vector2.zero; // 侧面/底面不用 UV（条纹走世界 Y）
            }

            var sides = new List<int>();
            var top = new List<int>();
            for (int t = 0; t < srcTris.Length; t += 3)
            {
                (src.normals[srcTris[t]].y > 0.5f ? top : sides).AddRange(
                    new[] { srcTris[t], srcTris[t + 1], srcTris[t + 2] });
            }

            var mesh = new Mesh { name = "BattleDeckStack" };
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.normals = src.normals; // 轴对齐法线：非均匀缩放不改变轴向
            mesh.subMeshCount = 2;
            mesh.SetTriangles(sides, 0);
            mesh.SetTriangles(top, 1);
            _stackMesh = mesh;
            return mesh;
        }

        private static Material GraveMaterial()
        {
            if (_graveMat == null)
            {
                _graveMat = HotUpdateAssets.Load<Material>("BattleTile_Vortex_Grave", VortexGraveMatPath);
                if (_graveMat == null) WarnDeps("BattleTile_Vortex_Grave.mat");
            }
            return _graveMat; // null=缺失：该格不建漩涡
        }

        private static Material ExileMaterial()
        {
            if (_exileMat == null)
            {
                _exileMat = HotUpdateAssets.Load<Material>("BattleTile_Vortex_Exile", VortexExileMatPath);
                if (_exileMat == null) WarnDeps("BattleTile_Vortex_Exile.mat");
            }
            return _exileMat;
        }

        private static void WarnDeps(string what)
        {
            if (_depsLogged) return;
            _depsLogged = true;
            Debug.LogWarning("[BattleZoneProps] 依赖缺失：" + what + "——对应特殊格道具降级为不显示");
        }
    }
}
