using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using CardCore;

namespace SynergyUI
{
    /// <summary>
    /// 运行时 UI 入口（2026-10-01 预制体化定案）：本组件独占 Canvas——挂在场景空对象上，
    /// OnEnable 幂等补齐自身 Canvas(ScreenSpaceOverlay)+CanvasScaler(1920×1080)+GraphicRaycaster，
    /// 挂载根=自身 RectTransform（UguiCanvas/ScreenRoot 两层中间包装已剃除，不再创建）。
    /// 所有屏幕（含主菜单）统一生命周期：Show=实例化各自预制体、离屏=销毁自己的实例
    /// （UIScreen.PrefabAssetPath 声明预制体，详见 UIManager/UIScreen）。
    ///
    /// 编辑器域重载/重编译：OnEnable 以"组件有则复用"幂等重建（界面回到主菜单）。
    /// </summary>
    public sealed class UIBootstrap : MonoBehaviour
    {
        private UIManager _manager;

        private void OnEnable()
        {
            //定死60帧
            Application.targetFrameRate = 60;
            // 幂等补齐 Canvas 体系（场景挂点通常只有本脚本；AddComponent<Canvas> 会自动带上 RectTransform）
            var canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 0;
            }

            if (GetComponent<CanvasScaler>() == null)
            {
                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
            }

            if (GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();

            EnsureEventSystem();

            if (_manager == null || _manager.Current == null)
            {
                // 域重载/重编译防护：热重载会丢 _manager 引用但孤儿屏实例仍在根下
                // （根下只可能有运行时屏实例，无烘焙内容）——重建前清一次再回主菜单。
                var rootRect = (RectTransform)transform;
                for (int i = rootRect.childCount - 1; i >= 0; i--)
                    Destroy(rootRect.GetChild(i).gameObject);

                _manager = new UIManager(rootRect);
                _manager.Show<MainMenuScreen>();
            }
        }

        /// <summary>全项目唯一 EventSystem（存在即复用）。</summary>
        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var existing = FindFirstObjectByType<EventSystem>();
            if (existing != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(go);
        }

        /// <summary>
        /// 每帧驱动引擎主循环（结算栈）——契约不变：
        /// 对战界面依赖它推进栈/触发结算；非对局进行中时 GameCore.Update 内部自检空转，无副作用。
        /// 双驱动闸：对局托管期（HasLiveMatch=等卡组/对战中）停驱——引擎由服务器泵独占驱动；
        /// 会话服务器运行期同停。非对局期本地 AI 对战照常由本处帧驱推进。
        /// </summary>
        private void Update()
        {
            // 比赛禁联机（NetGate）：闸关时不可能有服务器托管态，短路直驱引擎
            if (!CardCore.Network.NetGate.OnlineEnabled)
            {
                GameCore.Instance?.Update();
                return;
            }

            var lobby = CardCore.Network.NetLobbyServer.Current;
            if (lobby != null && lobby.HasLiveMatch)
                return;
            if (CardCore.Network.NetSessionServer.IsRunning)
                return;
            GameCore.Instance?.Update();
        }
    }
}
