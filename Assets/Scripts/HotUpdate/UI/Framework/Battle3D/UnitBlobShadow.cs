using UnityEngine;
using Tide.HotUpdate;
using Object = UnityEngine.Object;

namespace SynergyUI
{
    /// <summary>
    /// 生物脚下假圆形阴影（2026-10-26 用户定案：生物模型不再投真影——光追阴影闪烁与性能双收口，
    /// 脚下以贴地软圆片代替）。面=Element.prefab 平贴四边形（与地牌元素图标/BattleTargetReticle
    /// 瞄准面片同管线同模板），材质=Elem_White 透明模板克隆换运行时径向渐变黑纹理（零落盘资产）。
    ///
    /// 挂单位 wrapper 下：出击飞行/守卫瞬移等一切位移 XZ 跟随（wrapper 只承载位移/演出旋转）；
    /// LateUpdate 把世界 Y 钉回所在地块顶面（tile 原点=顶面；地块浮起动画由此跟随，飞行抬升
    /// 不带走影子）并压平朝向（不随演出俯仰/旋转翻滚）。地块查表走 BattleTileFx.TileOf
    /// （建时捕获；盘拆 tile 销毁后维持末次高度）。依赖缺失告警一次降级为无假影，不阻塞对局
    /// （BattleTileFx 同口径）。真影关闭在 BattleFieldView.CreateUnit（本件只管假影）。
    ///
    /// 调参（2026-10-26）：四个公共静态参数，Play 中 execute_code 直改即时生效（改动经
    /// LateUpdate/ApplyVisual 惰性同步到已建件与共享材质），满意后把值写回此处缺省——
    /// 例：SynergyUI.UnitBlobShadow.Alpha = 0.4f; SynergyUI.UnitBlobShadow.Size = 1.1f;
    /// </summary>
    public class UnitBlobShadow : MonoBehaviour
    {
        private const string ElementPrefabPath = "Assets/Art/Prefab/Element.prefab";
        private const string TemplateMatPath = "Assets/Art/Materials/BattleCardBack_Elem_White_Mat.asset";

        // ---- 可调参数（即时生效；写回缺省=定稿） ----

        /// <summary>脚印直径（面片边长，世界单位）。缺省 0.9（占位胶囊 0.7 宽/常规模型 1 档体型）。</summary>
        public static float Size = 0.9f;
        /// <summary>浓度（中心峰值透明度 0..1）。缺省 0.52。</summary>
        public static float Alpha = 0.52f;
        /// <summary>抬升（地块顶面之上，米；防与菲涅尔壳 4mm/元素图标 8mm 叠面闪）。缺省 0.011。</summary>
        public static float LiftY = 0.011f;
        /// <summary>软边（径向渐变指数 0.5..4：小=边缘糊得远，大=边缘收得急）。缺省 1.35。</summary>
        public static float Softness = 1.35f;

        private static GameObject _prefab;
        private static Material _mat;
        private static Texture2D _tex;
        private static float _appliedAlpha = -1f;  // 材质浓度惰性同步记账
        private static float _appliedSoft = -1f;   // 软边=贴图重生成记账

        private static readonly Quaternion FlatUp = Quaternion.Euler(90f, 0f, 0f);

        private Transform _tile;     // 建时所在地块（BattleTileFx.TileOf；tile 原点=顶面）
        private float _y = float.MinValue;
        private float _baseScale = 1f; // 预制体烘焙尺寸（Size 换算基准）

        /// <summary>在单位 wrapper 下建假影（幂等：已有即返；依赖缺失返 null 降级）。
        /// x/z=建时格坐标（地块查表锚，非跟随源——位移跟随来自 wrapper 父子）。</summary>
        public static UnitBlobShadow Attach(Transform wrapper, int x, int z)
        {
            var existing = wrapper.GetComponentInChildren<UnitBlobShadow>();
            if (existing != null) return existing;
            if (!EnsureDeps()) return null;

            var go = Object.Instantiate(_prefab, wrapper, false);
            go.name = "fx-blob-shadow";
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col); // 不吃射线：格点击/单位悬停路由不被截胡（Reticle 同口径）
            var rend = go.GetComponent<MeshRenderer>();
            rend.sharedMaterial = _mat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;

            var self = go.AddComponent<UnitBlobShadow>();
            self._baseScale = go.transform.localScale.x;
            self._tile = BattleTileFx.TileOf(x, z);
            self.ApplyVisual(); // 建=即对齐当前参数（尺寸/朝向/贴地）
            return self;
        }

        /// <summary>按当前参数对齐本件（尺寸/贴地高度/朝向）。LateUpdate 每帧调用=热调即时生效；
        /// 编辑态（无帧循环）可显式调用对齐。</summary>
        public void ApplyVisual()
        {
            float size = Mathf.Max(0.05f, Size);
            float target = size / _baseScale;
            if (!Mathf.Approximately(transform.localScale.x, target))
                transform.localScale = Vector3.one * target;
            if (_tile != null) _y = _tile.position.y + Mathf.Max(0f, LiftY);
            if (_y > float.MinValue)
            {
                var p = transform.position;
                p.y = _y;
                transform.position = p;
            }
            if (transform.rotation != FlatUp) transform.rotation = FlatUp;
            SyncMaterial(); // 浓度/软边惰性同步（共享材质单点）
        }

        /// <summary>材质侧参数同步（浓度=颜色 alpha，软边=贴图重生成；记账防每帧重做）。</summary>
        private static void SyncMaterial()
        {
            if (_mat == null) return;
            float a = Mathf.Clamp01(Alpha);
            if (!Mathf.Approximately(_appliedAlpha, a))
            {
                _appliedAlpha = a;
                var c = new Color(1f, 1f, 1f, a); // rgb=白×贴图黑=黑；alpha=浓度
                _mat.SetColor("_BaseColor", c);
                _mat.SetColor("_Color", c); // 模板双颜色口同口径
            }
            float s = Mathf.Clamp(Softness, 0.5f, 4f);
            if (!Mathf.Approximately(_appliedSoft, s))
            {
                _appliedSoft = s;
                if (_tex != null) Object.Destroy(_tex);
                _tex = BuildTexture(s);
                _mat.SetTexture("_BaseMap", _tex);
                _mat.SetTexture("_MainTex", _tex);
            }
        }

        private static bool EnsureDeps()
        {
            if (_mat != null && _prefab != null) return true;
            if (_prefab == null)
            {
                _prefab = HotUpdateAssets.Load<GameObject>("Element", ElementPrefabPath);
                if (_prefab == null) { Debug.LogWarning("[UnitBlobShadow] Element.prefab 缺失——假影降级为不显示"); return false; }
            }
            if (_mat == null)
            {
                var template = HotUpdateAssets.Load<Material>("BattleCardBack_Elem_White", TemplateMatPath);
                if (template == null) { Debug.LogWarning("[UnitBlobShadow] Elem_White 模板缺失——假影降级为不显示"); return false; }
                _mat = new Material(template) { name = "UnitBlobShadow_Runtime" };
                _appliedAlpha = -1f; _appliedSoft = -1f; // 逼首次同步
            }
            return true;
        }

        /// <summary>径向渐变黑圆片：rgb 恒黑、alpha=smoothstep 软边^指数（浓度乘法在材质颜色 alpha）。</summary>
        private static Texture2D BuildTexture(float softness)
        {
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
            { name = "UnitBlobShadow_Tex", wrapMode = TextureWrapMode.Clamp };
            float c = (n - 1) * 0.5f;
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float r = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                    float a = Mathf.Clamp01(1f - r);
                    a = a * a * (3f - 2f * a);
                    a = Mathf.Pow(a, softness);
                    px[y * n + x] = new Color(0f, 0f, 0f, a);
                }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        private void LateUpdate() => ApplyVisual();
    }
}
