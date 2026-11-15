using UnityEngine;
using Tide.HotUpdate;
using Object = UnityEngine.Object;

namespace SynergyUI
{
    /// <summary>
    /// 生物脚下卡背指示牌（2026-11-03 用户定案·横置视觉改版）：每只生物模型脚下常驻一张平贴
    /// 卡背（竖版 344:512），可用=竖直、横置=原地旋 90°（平滑转向）——横置状态由卡面朝向表达，
    /// 旧口径「未横置=脚下扩散波常亮」同日退役（波改为目标选择期的候选区域专属标志，
    /// 见 BattleTileFx.SetUnitGlow 注释；史：2026-10-13 卡背退役定案由此翻案）。
    ///
    /// 面片=Element.prefab 平贴四边形（与假影/瞄准面片同管线同模板），材质=BattleCardBack_Back_Mat
    /// 直引（与 3D 牌堆顶面同观感，零落盘资产）。挂单位 wrapper 下：位移 XZ 跟随；
    /// LateUpdate 把世界 Y 钉回所在地块顶面并 slerp 到目标朝向（不随演出俯仰翻滚）。
    /// 依赖缺失告警一次降级为不显示，不阻塞对局（UnitBlobShadow 同口径）。
    ///
    /// 调参：规格参数在 CardPlateMetrics（2026-11-15 起与地牌面片共读单源），Play 中
    /// execute_code 直改即时生效（如 SynergyUI.CardPlateMetrics.Width = 0.6f;），满意后写回缺省。
    /// </summary>
    public class UnitCardPlate : MonoBehaviour
    {
        private const string ElementPrefabPath = "Assets/Art/Prefab/Element.prefab";
        private const string CardBackMatPath = "Assets/Art/Materials/BattleCardBack_Back_Mat.asset";

        // 可调参数已迁 CardPlateMetrics（2026-11-15 地牌长条定案：与 LandStripFx 面片共读单源）——
        // Width/LiftY/TurnTime/Portrait/Landscape 全部改引该类

        private static GameObject _prefab;
        private static Material _mat;

        private Transform _tile;          // 建时所在地块（BattleTileFx.TileOf；tile 原点=顶面）
        private float _y = float.MinValue;
        private float _baseScale = 1f;    // 预制体烘焙尺寸（Width 换算基准）
        private bool _tapped;
        private bool _visible = true;
        private Renderer _rend;

        /// <summary>在单位 wrapper 下建卡背（幂等：已有即返；依赖缺失返 null 降级）。
        /// x/z=建时格坐标（钉地锚，非跟随源——位移跟随来自 wrapper 父子）。</summary>
        public static UnitCardPlate Attach(Transform wrapper, int x, int z)
        {
            var existing = wrapper.GetComponentInChildren<UnitCardPlate>();
            if (existing != null) return existing;
            if (!EnsureDeps()) return null;

            var go = Object.Instantiate(_prefab, wrapper, false);
            go.name = "fx-card-plate";
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col); // 不吃射线：格点击/单位悬停路由不被截胡（假影同口径）
            var rend = go.GetComponent<MeshRenderer>();
            rend.sharedMaterial = _mat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;

            var self = go.AddComponent<UnitCardPlate>();
            self._baseScale = go.transform.localScale.x;
            self._tile = BattleTileFx.TileOf(x, z);
            self._rend = rend;
            self.transform.localRotation = CardPlateMetrics.Portrait;
            self.ApplyVisual(); // 建=即对齐当前参数（尺寸/贴地）
            return self;
        }

        /// <summary>横置态写入（对账驱动）：true=转向横置，false=竖直。朝向在 LateUpdate 平滑到位。</summary>
        public void SetTapped(bool tapped) => _tapped = tapped;

        /// <summary>显隐（死亡隐藏/复活恢复；对账驱动）。</summary>
        public void SetVisible(bool visible) => _visible = visible;

        /// <summary>按当前参数对齐本件（尺寸/贴地高度/显隐；朝向在 LateUpdate 平滑）。LateUpdate
        /// 每帧调用=热调即时生效；编辑态（无帧循环）可显式调用对齐。显隐只关 Renderer——
        /// 不能 SetActive(false)（组件失活 LateUpdate 停跑，复活 SetVisible(true) 将永不生效）。</summary>
        private void ApplyVisual()
        {
            float w = Mathf.Max(0.05f, CardPlateMetrics.Width);
            float target = w / _baseScale;
            var scale = Vector3.one * target;
            scale.y = scale.x * CardPlateMetrics.Aspect; // 四边形局部 Y=贴地后的长边（竖版高）
            transform.localScale = scale;
            if (_tile != null) _y = _tile.position.y + Mathf.Max(0f, CardPlateMetrics.LiftY);
            if (_rend != null) _rend.enabled = _visible;
        }

        private void LateUpdate()
        {
            ApplyVisual();
            if (_y > float.MinValue)
            {
                var p = transform.position;
                p.y = _y;
                transform.position = p;
            }
            var targetRot = _tapped ? CardPlateMetrics.Landscape : CardPlateMetrics.Portrait;
            // localRotation 相对 wrapper（演出期 wrapper 可能带世界旋转——本地口径与假影压平同理，
            // 直接写 local 至目标，保证贴地朝向不被演出姿态污染）
            transform.localRotation = CardPlateMetrics.TurnTime <= 0f
                ? targetRot
                : Quaternion.Slerp(transform.localRotation, targetRot, Mathf.Clamp01(Time.deltaTime / CardPlateMetrics.TurnTime));
        }

        private static bool EnsureDeps()
        {
            if (_mat != null && _prefab != null) return true;
            if (_prefab == null)
            {
                _prefab = HotUpdateAssets.Load<GameObject>("Element", ElementPrefabPath);
                if (_prefab == null) { Debug.LogWarning("[UnitCardPlate] Element.prefab 缺失——卡背降级为不显示"); return false; }
            }
            if (_mat == null)
            {
                _mat = HotUpdateAssets.Load<Material>("BattleCardBack_Back", CardBackMatPath);
                if (_mat == null) { Debug.LogWarning("[UnitCardPlate] BattleCardBack_Back_Mat 缺失——卡背降级为不显示"); return false; }
            }
            return true;
        }
    }
}
