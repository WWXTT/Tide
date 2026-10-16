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
    /// 选中才上盘（2026-10-16 晚定案：开窗不铺候选半透明档，选中/取消选中随
    /// RegionTargetSelector.Toggle 全量对拍 Show/Hide——与卡面 sel-frame 高亮同拍；
    /// 原候选档缩尺/半透明材质随定案退役，只留全尺寸不透明单档）。
    /// 生命周期随选择窗：关窗 ClearAll 单口（确认/超时代选同走）。
    ///
    /// 形态：Quad 面片挂 tile-{x}-{z} 下（钉 tile 不随演出走位——与生物箭头同约定），
    /// 抬 16mm（菲涅尔 4/箭头 6/优先权条 12/格面文字 14 之上），朝向与地牌格平贴文字同款
    ///（可读面朝 -z 侧相机）；材质=BattleCardBack_Back_Mat 透明底材实例（施放立牌同管线，
    /// mainTexture 换 Aim）。面片不吃射线（销毁 Collider）——格点击/单位悬停路由不被拦。
    /// 依赖缺失（底材/Aim.png）告警一次降级为不显示，不阻塞对局（BattleTileFx 同口径）。
    /// </summary>
    public static class BattleTargetReticle
    {
        private const string BaseMatPath = "Assets/Art/Battle3D/Materials/BattleCardBack_Back_Mat.asset";
        private const string AimTexPath = "Assets/Art/Battle3D/Icons/Aim.png";

        private const float QuadSize = 1.6f;   // 面片边长（格外接径 2m/对边距 1.73m——略收留边）
        private const float TileLift = 0.016f; // 抬高：格面平贴文字(0.014)之上

        private static Material _mat;
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
            _depsLogged = false;
        }

        /// <summary>选中上盘（幂等——同 id 重复 Show 不重建；棋盘未装台/依赖缺失静默跳过）：
        /// 全尺寸不透明单档，随 RegionTargetSelector.Toggle 起落。</summary>
        public static void Show(uint runtimeId, int x, int z)
        {
            if (_quads.TryGetValue(runtimeId, out var q) && q != null) return; // fake-null 判死重建
            if (EnsureMaterials() == null) return;
            var tile = BattleBoardGrid.Root != null ? BattleBoardGrid.Root.transform.Find($"tile-{x}-{z}") : null;
            if (tile == null) return;

            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "fx-target-aim-" + runtimeId % 10000;
            Object.Destroy(go.GetComponent<Collider>()); // 不吃射线：tile-/cast-/unit 路由不被面片截胡
            go.transform.SetParent(tile, false);
            go.transform.localPosition = new Vector3(0f, TileLift, 0f);
            go.transform.localRotation = Quaternion.LookRotation(Vector3.down, Vector3.forward); // 平贴·可读面朝相机（格面文字同款）
            go.transform.localScale = new Vector3(QuadSize, QuadSize, 1f);
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

        /// <summary>关窗清面片（选择窗收尾单口：确认/超时代选/退化自动选同走）。</summary>
        public static void ClearAll()
        {
            foreach (var q in _quads.Values)
                if (q != null)
                {
                    if (Application.isPlaying) Object.Destroy(q);
                    else Object.DestroyImmediate(q);
                }
            _quads.Clear();
        }

        // ======================================== 内部 ========================================

        /// <summary>透明底材×Aim 贴图单档材质；依赖缺失告警一次返回 null=不显示。</summary>
        private static Material EnsureMaterials()
        {
            if (_mat != null) return _mat;
            var src = HotUpdateAssets.Load<Material>("BattleCardBack_Back", BaseMatPath);
            if (src == null) { WarnDeps("BattleCardBack_Back_Mat（透明底材）"); return null; }
            var tex = HotUpdateAssets.Load<Texture2D>("Aim", AimTexPath);
            if (tex == null) { WarnDeps("Aim.png（红瞄准图）"); return null; }
            _mat = new Material(src) { mainTexture = tex, color = Color.white };
            return _mat;
        }

        private static void WarnDeps(string what)
        {
            if (_depsLogged) return;
            _depsLogged = true;
            Debug.LogWarning("[BattleTargetReticle] 依赖缺失：" + what + "——目标选择红瞄准面片降级为不显示");
        }
    }
}
