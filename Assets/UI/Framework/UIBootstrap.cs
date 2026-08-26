using UnityEngine;
using UnityEngine.UIElements;
using CardCore;

namespace SynergyUI
{
    /// <summary>
    /// 运行时 UI 入口。把本组件挂在场景中任意一个空 GameObject 上，按 Play 即可。
    ///
    /// 装配流程（零 Inspector 手连）：
    ///   1. 从 Resources 加载共享 PanelSettings；
    ///   2. 动态添加 UIDocument 组件并赋值 PanelSettings；
    ///   3. 用 rootVisualElement 创建 UIManager；
    ///   4. 进入主菜单。
    ///
    /// 所有资源（PanelSettings / UXML / 主题）均走 Resources.Load，无需在
    /// Inspector 拖任何引用。
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class UIBootstrap : MonoBehaviour
    {
        // PanelSettings 在 Resources 下的路径（不含扩展名）。
        private const string PanelSettingsResourcePath = "SynergyPanelSettings";

        private UIManager _manager;

        private void Awake()
        {
            var doc = GetComponent<UIDocument>();

            // 运行时确保 PanelSettings 已设置（若 Inspector 未指定则从 Resources 加载）。
            if (doc.panelSettings == null)
            {
                var panelSettings = Resources.Load<PanelSettings>(PanelSettingsResourcePath);
                if (panelSettings == null)
                {
                    Debug.LogError(
                        $"[UIBootstrap] 找不到 PanelSettings: Resources/{PanelSettingsResourcePath}。" +
                        "请确认 Assets/UI/Resources/SynergyPanelSettings.asset 已导入。");
                    return;
                }
                doc.panelSettings = panelSettings;
            }

            _manager = new UIManager(doc.rootVisualElement);
        }

        private void Start()
        {
            _manager?.Show<MainMenuScreen>();
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
