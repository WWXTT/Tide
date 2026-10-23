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

        /// <summary>进 Play 清引擎/事件总线单例（2026-10-14，域重载关闭防护）：静态单例跨 Play
        /// 存活时，上一局已销毁屏的订阅链会让本局事件逐条抛 MissingReferenceException——
        /// SubsystemRegistration 档在每次进 Play（含跳域重载）必发。**必须成对重置**：
        /// GameCore.ResetInstance 先丢引擎单例（旧实例构造期订阅的 TurnStart/TurnEnd/PhaseEnd
        /// 自动化在旧总线上，只丢总线不丢它＝新对局不解横置/不抽牌/AI 空转——当日回归根因），
        /// EventManager.ResetInstance 再丢总线。与 BattleTileFx.ResetStatics 同口径。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetPlayStatics()
        {
            GameCore.ResetInstance();
            EventManager.ResetInstance();
        }

        private void OnEnable()
        {
            // 移动端锁 30 帧压发热（真机反馈"热飞了"），PC 保持 60；与 Launch.Start 同口径
            Application.targetFrameRate = Application.isMobilePlatform ? 30 : 60;
            // 幂等补齐 Canvas 体系（场景挂点通常只有本脚本；AddComponent<Canvas> 会自动带上 RectTransform）
            var canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 0;
            }

            var existingScaler = GetComponent<CanvasScaler>();
            if (existingScaler == null)
            {
                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            }
            else
            {
                // Phase A（2026-10-23）：Expand 让画布恒 ≥1920×1080——任意横屏比例不裁切，
                // 多出的部分成为设计台留边（由 backdrop 画框填色）。场景已有 scaler 也钉死该模式，
                // 防资产序列化漂移（match 值在 Expand 下不参与缩放，不强行覆写）。
                existingScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
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

            // backdrop 画框兜底确保（常态由 SafeAreaFitter.FrameBackdrop/UIScreen.Mount 惰性维护；
            // 此处保证 manager 存活的再使能路径下它也存在且在最底层）
            EnsureBackdrop((RectTransform)transform);
        }

        // ================= 全屏 backdrop 画框（2026-10-23 UI 适配 Phase A） =================

        /// <summary>backdrop=Canvas 根 sibling 0 的「画框」：设计台矩形外的四条 ScreenBg 边条，
        /// 填充 Expand 留边与安全区避让区。四条而非整面——MainUI/BattleUI 是透明底、
        /// 视觉依赖 3D（黑洞/战场）透出，整面底图会盖死世界相机；四边条只在留边处落色，
        /// 1920×1080 下边条宽为 0（像素级无痕）。Stage 屏的 SafeAreaFitter 每次重算都会
        /// FrameBackdrop；Inset 屏（战场）由 SyncBackdropFor 整体隐藏。</summary>
        internal static void FrameBackdrop(RectTransform canvasRoot, Rect stageRect)
        {
            var frame = EnsureBackdrop(canvasRoot);
            var pr = canvasRoot.rect;
            if (pr.width <= 0f || pr.height <= 0f) return;
            var xMin = (stageRect.xMin - pr.xMin) / pr.width;
            var xMax = (stageRect.xMax - pr.xMin) / pr.width;
            var yMin = (stageRect.yMin - pr.yMin) / pr.height;
            var yMax = (stageRect.yMax - pr.yMin) / pr.height;
            PlaceStrip(frame, "left", 0f, 0f, xMin, 1f);
            PlaceStrip(frame, "right", xMax, 0f, 1f, 1f);
            PlaceStrip(frame, "bottom", xMin, 0f, xMax, yMin);
            PlaceStrip(frame, "top", xMin, yMax, xMax, 1f);
        }

        /// <summary>装屏收口时同步画框可见性：Stage 屏显示画框；Inset 屏（含战场 3D 透出）隐藏。</summary>
        internal static void SyncBackdropFor(RectTransform screenRoot)
        {
            if (screenRoot == null) return;
            var canvasRoot = screenRoot.parent as RectTransform;
            if (canvasRoot == null) return;
            var frame = EnsureBackdrop(canvasRoot);
            var fitter = screenRoot.GetComponent<SafeAreaFitter>();
            frame.gameObject.SetActive(fitter == null || fitter.IsStage);
        }

        private static RectTransform EnsureBackdrop(RectTransform canvasRoot)
        {
            var frame = canvasRoot.Find("backdrop") as RectTransform;
            if (frame == null)
            {
                var go = new GameObject("backdrop", typeof(RectTransform));
                frame = (RectTransform)go.transform;
                frame.SetParent(canvasRoot, false);
                UiKit.Stretch(frame);
                foreach (var side in new[] { "left", "right", "bottom", "top" })
                {
                    var strip = new GameObject(side, typeof(RectTransform));
                    var srt = (RectTransform)strip.transform;
                    srt.SetParent(frame, false);
                    var img = strip.AddComponent<Image>();
                    img.color = UiStyle.ScreenBg;
                    img.raycastTarget = true; // 留边区域拦截点击，防穿到 3D
                }
            }
            frame.SetAsFirstSibling();
            return frame;
        }

        private static void PlaceStrip(RectTransform frame, string side,
            float axMin, float ayMin, float axMax, float ayMax)
        {
            var rt = frame.Find(side) as RectTransform;
            if (rt == null) return;
            rt.anchorMin = new Vector2(axMin, ayMin);
            rt.anchorMax = new Vector2(axMax, ayMax);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
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
            // NetGate 闸关时不可能有服务器托管态，短路直驱引擎（紧急开关位）
            if (!CardCore.Network.NetGate.OnlineEnabled)
            {
                GameCore.Instance?.Update();
                return;
            }

            // P2P 房主（2026-10-21 定案）：会话服务器宿主在本进程——服务器泵独占驱动引擎
            //（IO 收发→房间分派→引擎推进→下行出队；本地帧驱停摆，防双驱动交错管理响应窗口）。
            // 房主自身=回环客户端，同帧随泵（先服务器下行、后客户端消费）。
            var session = CardCore.Network.NetSessionServer.Current;
            if (session != null)
            {
                session.Pump();
                NetClientRuntime.Pump();
                return;
            }

            // 纯客人：只泵联机客户端（本地无引擎可驱——快照/事件流驱动渲染）
            if (NetClientRuntime.Active != null)
            {
                NetClientRuntime.Pump();
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
