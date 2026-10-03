using System;
using System.Collections.Generic;
using CardCore;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UButton = UnityEngine.UI.Button;
using UInputField = TMPro.TMP_InputField;

namespace SynergyUI
{
    /// <summary>
    /// 所有运行时界面的抽象基类（2026-10-01 预制体化定案）：
    /// 屏幕层级来自 Assets/Art/UI/ 下的预制体——子类 override PrefabName 声明来源
    /// （文件名，不含扩展名；YooAsset 地址同名、兜底路径由 UiKit.PrefabPath 派生，
    /// 2026-10-03 收敛为单一声明点），Mount 时框架实例化预制体为 Root
    /// （实例根重命名为 RootName，保持按名断言口径），
    /// Build 只做"深度按名绑定 + 闭包接线"，不再构建静态层级。未声明预制体的屏
    /// （PrefabName=null）保持旧式纯代码构建：Build 首行自建 Root。
    ///
    /// 生命周期：Activate = Bind（注入管理器与挂载父节点）→ CreateRoot（实例化预制体/
    /// null）→ Build（绑定+接线）→ OnEnter（数据填充/启动逻辑）；Deactivate =
    /// FlushSubscriptions（自动退订）→ OnExit（逻辑清理）→ DestroyRoot（只销毁本屏实例，
    /// 不清根）。实例缓存复用，再进屏时重新实例化+绑定。
    ///
    /// 绑定约定：预制体已剃掉中间包装层——绑定一律用深度按名查找（Find/FindUnder，对层级
    /// 结构免疫），不做路径查找；缺节点 LogError 暴露断线，不静默吞。
    ///
    /// 事件绑定底座不变：Subscribe&lt;T&gt; 订阅 EventManager 广播事件，离屏框架自动退订。
    /// </summary>
    public abstract class UIScreen
    {
        /// <summary>本界面根节点（预制体屏=实例根；纯代码屏=Build 里用 UiKit.Screen 创建）。</summary>
        public RectTransform Root { get; protected set; }

        /// <summary>挂载父节点（bootstrap 的 Canvas 根），实例化/构建时使用。</summary>
        protected RectTransform Parent { get; private set; }

        /// <summary>所属界面管理器，用于导航（Show/Back/Replace）。</summary>
        protected UIManager Manager { get; private set; }

        /// <summary>本屏预制体名（Assets/Art/UI/ 下、不含扩展名）；null=无预制体（纯代码构建）。
        /// 唯一声明点——YooAsset 地址=同名（AddressByFileName），编辑器兜底路径由其派生。</summary>
        protected virtual string PrefabName => null;

        protected string PrefabAddress => PrefabName;

        protected string PrefabAssetPath => PrefabName == null ? null : UiKit.PrefabPath(PrefabName);

        /// <summary>屏根名（预制体实例根的重命名/纯代码屏的根节点名，沿用旧口径便于按名断言）。</summary>
        protected virtual string RootName => "screen";

        // 记录本界面的所有事件退订动作，OnExit 时统一执行。
        private readonly List<Action> _unsubscribers = new List<Action>();

        /// <summary>由 UIManager 在装配时调用，注入运行时依赖。</summary>
        internal void Bind(UIManager manager, RectTransform parent)
        {
            Manager = manager;
            Parent = parent;
        }

        /// <summary>
        /// 订阅 EventManager 广播事件，并登记退订动作。
        /// 界面切走时框架自动退订，子类无需手动管理。
        /// </summary>
        protected void Subscribe<T>(Action<T> handler) where T : IEventData
        {
            EventManager.Instance.Subscribe(handler);
            _unsubscribers.Add(() => EventManager.Instance.Unsubscribe(handler));
        }

        /// <summary>
        /// 构建本界面（每次进屏调用）。预制体屏=绑定既有节点+闭包接线（Root 已由框架
        /// 实例化好）；纯代码屏=首行自建 Root（Root = UiKit.Screen(RootName, Parent)）再构建。
        /// </summary>
        protected abstract void Build();

        /// <summary>框架内部：装配入口——注入依赖 → 建/实例化 Root → Build → OnEnter。</summary>
        internal void Mount(UIManager manager, RectTransform parent)
        {
            Bind(manager, parent);
            CreateRoot();
            Build();
            OnEnter();
        }

        /// <summary>声明了预制体的屏：加载并实例化为 Root；失败回落空屏根（Build 绑定报缺节点，不 NRE）。</summary>
        private void CreateRoot()
        {
            if (PrefabAssetPath == null) return; // 纯代码屏：Build 首行自建 Root

            var prefab = Tide.HotUpdate.HotUpdateAssets.Load<GameObject>(PrefabAddress, PrefabAssetPath);
            if (prefab == null)
            {
                Debug.LogError($"[{GetType().Name}] 预制体缺失：YooAsset[{PrefabAddress}] / {PrefabAssetPath}，回落空屏根");
                Root = UiKit.Screen(RootName, Parent);
                return;
            }

            var instance = UnityEngine.Object.Instantiate(prefab, Parent, false);
            instance.name = RootName; // 实例根统一重命名为屏根名（保持按名断言/查找口径）
            Root = (RectTransform)instance.transform;
            Root.SetAsLastSibling();
            UiKit.Stretch(Root);
        }

        // ================= 预制体绑定助手（深度按名，对剃层免疫） =================

        /// <summary>在 Root 下深度按名查找节点（含未激活节点）；找不到 LogError 并返回 null。</summary>
        protected RectTransform Find(string name) => FindUnder(Root, name);

        /// <summary>在指定范围内深度按名查找节点（含未激活节点）；找不到 LogError 并返回 null。</summary>
        protected RectTransform FindUnder(RectTransform scope, string name)
        {
            var hit = UiKit.FindDeep(scope, name);
            if (hit == null)
                Debug.LogError($"[{GetType().Name}] 预制体缺节点：{name}（检查 {PrefabAssetPath ?? RootName}）");
            return hit;
        }

        /// <summary>在 Root 下深度按名查找节点；找不到静默返回 null（可选节点用——
        /// 典型如弹层挂载点 overlay/selector-overlay：预制体不烘焙，缺失由 UiKit.Overlay 运行时补建）。</summary>
        protected RectTransform FindOptional(string name) => UiKit.FindDeep(Root, name);

        /// <summary>深度查找 TMP 文本（文本在节点自身或其子级均可——兼容徽标/chip 的"节点+子label"烘焙结构）。</summary>
        protected TMP_Text FindText(string name)
        {
            var rt = Find(name);
            return rt != null ? rt.GetComponentInChildren<TMP_Text>(true) : null;
        }

        /// <summary>深度查找按钮并接线点击（按钮可挂在节点自身或其子级）。</summary>
        protected UButton BindButton(string name, Action onClick)
        {
            var rt = Find(name);
            var btn = rt != null ? rt.GetComponentInChildren<UButton>(true) : null;
            if (btn == null)
            {
                if (rt != null) Debug.LogError($"[{GetType().Name}] 节点无 Button 组件：{name}");
                return null;
            }
            btn.onClick.AddListener(() => { Debug.Log($"[UI点击] {name}"); onClick?.Invoke(); });
            return btn;
        }

        /// <summary>深度查找 TMP 输入框；找不到 LogError 并返回 null。</summary>
        protected UInputField FindInput(string name)
        {
            var rt = Find(name);
            return rt != null ? rt.GetComponentInChildren<UInputField>(true) : null;
        }

        /// <summary>深度查找滚动区并解出 content（ScrollRect.content 未连时回落按名找 content 节点）。</summary>
        protected UiKit.Scroll FindScroll(string name)
        {
            var rt = Find(name);
            var sr = rt != null ? rt.GetComponent<ScrollRect>() : null;
            if (sr == null)
            {
                if (rt != null) Debug.LogError($"[{GetType().Name}] 节点无 ScrollRect：{name}");
                return null;
            }
            var content = sr.content != null
                ? sr.content
                : UiKit.FindDeep(rt, "content");
            if (content == null)
                Debug.LogError($"[{GetType().Name}] 滚动区缺 content：{name}");
            return new UiKit.Scroll { Rect = sr, Content = content };
        }

        /// <summary>绑定预制体烘焙的下拉（头部节点名=预设名）：优先节点上的真 TMP_Dropdown
        /// （2026-10-03 手改预制体定案），其次旧式自绘头部按钮；同名新旧节点并存时取含 TMP 的
        /// 那个；全缺时 LogError 并在 fallbackParent 代码补建。popupLayer=自绘弹层挂载点。</summary>
        protected UiKit.Dropdown BindDropdown(string name, RectTransform popupLayer,
            RectTransform fallbackParent,
            System.Collections.Generic.IEnumerable<string> options, int index,
            Action<int, string> onChanged = null, float? width = null)
        {
            var layer = popupLayer != null ? popupLayer : Root;
            foreach (var rt in UiKit.FindDeepAll(Root, name))
            {
                var tmp = rt.GetComponentInChildren<TMP_Dropdown>(true);
                if (tmp != null)
                    return new UiKit.Dropdown(tmp, options, index, onChanged);
            }

            var headNode = UiKit.FindDeep(Root, name);
            var head = headNode != null ? headNode.GetComponentInChildren<UButton>(true) : null;
            if (head != null)
                return new UiKit.Dropdown(head, layer, options, index, onChanged, width);

            Debug.LogError($"[{GetType().Name}] 预制体缺下拉（{name} 无 TMP_Dropdown 亦无头部按钮）——代码补建");
            return new UiKit.Dropdown(name, fallbackParent != null ? fallbackParent : Root, layer,
                options, index, onChanged, width);
        }

        // ================= 生命周期 =================

        /// <summary>界面进入：Build 完成后调用——填充数据、启动逻辑、订阅之外的初始化。</summary>
        public virtual void OnEnter() { }

        /// <summary>界面离开时调用：框架已自动退订事件，子类可在此释放额外资源。</summary>
        public virtual void OnExit() { }

        /// <summary>框架内部：执行所有登记的事件退订。OnExit 之前调用。</summary>
        internal void FlushSubscriptions()
        {
            foreach (var unsub in _unsubscribers)
            {
                unsub();
            }
            _unsubscribers.Clear();
        }

        /// <summary>销毁本界面层级（只销毁自己的实例，不动根下其他内容；C# 实例保留在 UIManager 缓存）。</summary>
        public void DestroyRoot()
        {
            if (Root != null)
            {
                UnityEngine.Object.Destroy(Root.gameObject);
                Root = null;
            }
        }
    }
}
