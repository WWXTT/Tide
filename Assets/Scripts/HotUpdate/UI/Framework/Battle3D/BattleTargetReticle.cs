using System.Collections.Generic;
using Tide.HotUpdate;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SynergyUI
{
    /// <summary>
    /// 目标选择红瞄准面片（2026-10-16）：Region 面板选目标期间，候选中「在场上」的卡
    /// （BattleFieldView.TryGetOnBoardCell 口径：单位/施放占格立牌；角色候选=角色格）
    /// 在脚下地块钉一枚平贴面片渲染 Aim.png（用户提供的红色瞄准图）。
    /// 2026-10-18 扩容：攻击选目标悬停/宣言同用（CombatChoreographer）——守卫拦截不转移
    /// （守卫者瞬移赴原目标格挡打，贴片原地即=守卫者脚下）。
    /// 选中才上盘（2026-10-16 晚定案：开窗不铺候选半透明档，选中/取消选中随
    /// RegionTargetSelector.Toggle 全量对拍 Show/Hide——与卡面 sel-frame 高亮同拍；
    /// 原候选档缩尺/半透明材质随定案退役，只留全尺寸单档）。
    /// 生命周期随选择窗：关窗按登记逐 id Hide（2026-10-18 改——原 ClearAll 单口会连坐
    /// 攻击链贴片：连锁响应开窗期间 CombatChoreographer 钉的贴片不受本窗关合影响）。
    ///
    /// 形态（2026-10-18 黑边修复改版）：Element.prefab 平贴件换绑 Aim 材质——与地牌元素
    /// 图标/施放类型图标同管线。旧 CreatePrimitive+卡背底材被否：BattleCardBack_Back 的
    /// 透明路径走 shader_feature 变体，真机剥离后 Aim 透明边缘渲染成黑边（Tide/IconAlpha
    /// 立案缘起，见 icon-alpha 定案）。材质=Elem_White（IconAlpha 零变体）克隆换贴图。
    /// 面片挂 tile-{x}-{z} 下（钉 tile 不随演出走位——与生物箭头同约定），抬 16mm（菲涅尔 4/
    /// 箭头 6/优先权条 12/格面文字 14 之上），朝向与地牌格平贴文字同款（可读面朝 -z 侧相机）。
    /// 面片不吃射线（销毁 Collider）——格点击/单位悬停路由不被拦。
    /// 依赖缺失（预制体/底材/Aim.png）告警一次降级为不显示，不阻塞对局（BattleTileFx 同口径）。
    /// </summary>
    public static class BattleTargetReticle
    {
        private const string ElementPrefabPath = "Assets/Art/Prefab/Element.prefab";
        private const string SourceMatPath = "Assets/Art/Battle3D/Materials/BattleCardBack_Elem_White_Mat.asset";
        private const string AimTexPath = "Assets/Art/Battle3D/Icons/Aim.png";

        private const float QuadSize = 1.28f;  // 面片边长（2026-10-18 晚 ×0.8 定稿——1.6 用户反馈太大；格外接径 2m/对边距 1.73m）
        private const float TileLift = 0.016f; // 抬高：格面平贴文字(0.014)之上

        private static Material _mat;
        private static GameObject _elementPrefab;
        private static readonly Dictionary<uint, GameObject> _quads = new Dictionary<uint, GameObject>(); // key=RuntimeId
        private static bool _depsLogged;

        /// <summary>当前面片数（数据层证据：选择窗开合日志消费）。</summary>
        public static int Count => _quads.Count;

        /// <summary>域重载关闭（Enter Play Mode Options）防跨 Play 残留：BattleTileFx.ResetStatics 同口径。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _quads.Clear();
            _mat = null;
            _elementPrefab = null;
            _depsLogged = false;
        }

        /// <summary>选中上盘（幂等——同 id 重复 Show 不重建；棋盘未装台/依赖缺失静默跳过）：
        /// 全尺寸单档，随 RegionTargetSelector.Toggle 起落；攻击链路由 CombatChoreographer 读写。
        /// 同 id 换格不挪位（早退）——调用方须先 Hide 再 Show 换钉新格。</summary>
        public static void Show(uint runtimeId, int x, int z)
        {
            if (_quads.TryGetValue(runtimeId, out var q) && q != null) return; // fake-null 判死重建
            if (!EnsureDeps()) return;
            var tile = BattleBoardGrid.Root != null ? BattleBoardGrid.Root.transform.Find($"tile-{x}-{z}") : null;
            if (tile == null) return;

            var go = Object.Instantiate(_elementPrefab, tile, false);
            go.name = "fx-target-aim-" + runtimeId % 10000;
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col); // 不吃射线：tile-/cast-/unit 路由不被面片截胡（防御——prefab 现无碰撞体）
            float baseScale = go.transform.localScale.x; // 预制体烘焙尺寸——除算还原目标边长
            go.transform.localPosition = new Vector3(0f, TileLift, 0f);
            go.transform.localRotation = Quaternion.LookRotation(Vector3.down, Vector3.forward); // 平贴·可读面朝相机（格面文字同款）
            go.transform.localScale = Vector3.one * (QuadSize / baseScale);
            var rend = go.GetComponent<MeshRenderer>();
            rend.sharedMaterial = _mat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
            _quads[runtimeId] = go;
        }

        /// <summary>取消选中下盘（RegionTargetSelector.Toggle 同拍；不在盘=无操作）。</summary>
        public static void Hide(uint runtimeId)
        {
            if (!_quads.TryGetValue(runtimeId, out var q)) return;
            _quads.Remove(runtimeId);
            if (q == null) return;
            if (Application.isPlaying) Object.Destroy(q);
            else Object.DestroyImmediate(q);
        }

        // ======================================== 内部 ========================================

        /// <summary>Element 预制体 + Elem_White 克隆换 Aim 贴图（Tide/IconAlpha 零变体透明——
        /// 真机无黑边）；依赖缺失告警一次返回 false=不显示。</summary>
        private static bool EnsureDeps()
        {
            if (_elementPrefab == null)
            {
                _elementPrefab = HotUpdateAssets.Load<GameObject>("Element", ElementPrefabPath);
                if (_elementPrefab == null) { WarnDeps("Element.prefab（平贴件模板）"); return false; }
            }
            if (_mat == null)
            {
                var src = HotUpdateAssets.Load<Material>("BattleCardBack_Elem_White", SourceMatPath);
                if (src == null) { WarnDeps("BattleCardBack_Elem_White_Mat（IconAlpha 底材）"); return false; }
                var tex = HotUpdateAssets.Load<Texture2D>("Aim", AimTexPath);
                if (tex == null) { WarnDeps("Aim.png（红瞄准图）"); return false; }
                _mat = new Material(src) { mainTexture = tex, color = Color.white };
            }
            return true;
        }

        private static void WarnDeps(string what)
        {
            if (_depsLogged) return;
            _depsLogged = true;
            Debug.LogWarning("[BattleTargetReticle] 依赖缺失：" + what + "——目标选择红瞄准面片降级为不显示");
        }
    }
}
