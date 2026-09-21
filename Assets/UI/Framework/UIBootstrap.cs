using UnityEngine;
using UnityEngine.UIElements;
using CardCore;

namespace SynergyUI
{
    /// <summary>
    /// 运行时 UI 入口（2026-09-14 迁移 PanelRenderer——Unity 6000.5 起 UIDocument 移入 Legacy 菜单）。
    /// 把本组件挂在场景中带 Panel Renderer 组件的 GameObject 上，按 Play 即可。
    ///
    /// 装配流程（零 Inspector 手连）：
    ///   1. OnEnable：确保 PanelSettings（Inspector 未指定则从 Resources 加载）；
    ///   2. 注册 RegisterUIReloadCallback——PanelRenderer 的根节点不再经 rootVisualElement
    ///      属性暴露，改经回调取得（UI 已装载时立即回调一次；编辑器 UXML 热重载/
    ///      换 visualTreeAsset 时再回调）；
    ///   3. 首次回调：建 UIManager 并进主菜单；再次回调（热重载）：换根并重建当前界面。
    ///
    /// 版本号守卫防重复初始化（回调偶发双发）。若进 Play 无 UI 且无报错，
    /// 可给 Panel Renderer 的 visualTreeAsset 字段随便指一个 UXML（如 MainMenu）触发装载——
    /// UIManager 装配时会清空根重建，不影响任何界面。
    /// </summary>
    [RequireComponent(typeof(PanelRenderer))]
    public sealed class UIBootstrap : MonoBehaviour
    {
        // PanelSettings 在 Resources 下的路径（不含扩展名）。
        private const string PanelSettingsResourcePath = "SynergyPanelSettings";

        private PanelRenderer _renderer;
        private UIManager _manager;
        private int _panelVersion = -1;

        private void OnEnable()
        {
            _renderer = GetComponent<PanelRenderer>();

            // 运行时确保 PanelSettings 已设置（若 Inspector 未指定则从 Resources 加载）。
            if (_renderer.panelSettings == null)
            {
                var panelSettings = Resources.Load<PanelSettings>(PanelSettingsResourcePath);
                if (panelSettings == null)
                {
                    Debug.LogError(
                        $"[UIBootstrap] 找不到 PanelSettings: Resources/{PanelSettingsResourcePath}。" +
                        "请确认 Assets/UI/Resources/SynergyPanelSettings.asset 已导入。");
                    return;
                }
                _renderer.panelSettings = panelSettings;
            }

            _renderer.RegisterUIReloadCallback(OnUIReload);
        }

        private void OnDisable()
        {
            if (_renderer != null)
                _renderer.UnregisterUIReloadCallback(OnUIReload);
        }

        /// <summary>UI 装载/重载回调（PanelRenderer 根节点唯一获取口）。</summary>
        private void OnUIReload(PanelRenderer renderer, VisualElement root, int version)
        {
            if (version == _panelVersion) return; // 双发防重复初始化
            _panelVersion = version;

            if (_manager == null)
            {
                _manager = new UIManager(root);
                _manager.Show<MainMenuScreen>();
            }
            else
            {
                // 编辑器热重载：根节点被替换——UIManager 换根并重建当前界面（导航栈保持）
                _manager.ReattachRoot(root);
            }
        }

        /// <summary>
        /// 每帧驱动引擎主循环（结算栈）。对战界面依赖它推进栈/触发结算；
        /// 非对局进行中时 GameCore.Update 内部自检空转，无副作用。
        /// </summary>
        private void Update()
        {
            GameCore.Instance?.Update();
        }
    }
}
