using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Tide.RenderFX
{
    /// <summary>
    /// 战场后处理装配口（2026-11-23 验收：教学描边高光不出光晕——战场相机从未开
    /// renderPostProcessing、场景也无 Volume，DefaultVolumeProfile 里的 Bloom 空转）。
    /// Ensure=相机开后处理 + 建全局 Volume（Bloom 运行时代建，阈值/强度照
    /// DefaultVolumeProfile 现值 0.7/0.75——RenderFX 不引 HotUpdate 资产装载口，
    /// 免资产依赖）。开在战场舞台装配时（BattleStageDirector），全战斗常驻：
    /// 描边 HDR 边（TutorialOutlineFeature，BeforeRenderingPostProcessing）由 bloom
    /// 晕开成边缘光；遮罩成片拷贝（AfterRenderingPostProcessing）本就含 bloom。
    /// Volume 由调用方挂到战场 Map 总根（RenderFX 不引 HotUpdate——父节点参数化，随战场销毁；
    /// 相机数据随相机存活）。被销毁（fake-null）后下一次 Ensure 自动重建。
    /// </summary>
    public static class BattlePostFx
    {
        private static Volume _volume;

        public static void Ensure(Camera cam, Transform parent)
        {
            if (cam != null)
            {
                var data = cam.GetUniversalAdditionalCameraData();
                if (data != null && !data.renderPostProcessing)
                    data.renderPostProcessing = true;
            }
            if (_volume != null) return;
            var go = new GameObject("battle-post-volume");
            if (parent != null) go.transform.SetParent(parent, false);
            _volume = go.AddComponent<Volume>();
            _volume.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.7f);
            bloom.intensity.Override(0.75f);
            _volume.profile = profile;
            _volume.enabled = true;
        }
    }
}
