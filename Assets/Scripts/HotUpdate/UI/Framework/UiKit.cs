using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Tide.HotUpdate;
using UButton = UnityEngine.UI.Button;
using UInputField = TMPro.TMP_InputField;
using UToggle = UnityEngine.UI.Toggle;

namespace SynergyUI
{
    /// <summary>
    /// UGUI 控件工厂（2026-10-01 预制体化定案）：屏幕静态层级来自 Assets/Art/Prefab/ 预制体
    /// （UIScreen 深度按名绑定，FindDeep），本工厂只服务运行时动态创建——滚动列表行、
    /// 弹窗/下拉、动态表单控件；卡面 ACard.prefab 由 CardOverlayController 加载。
    ///
    /// 约定：
    ///   · 字体统一 SC-Heavy SDF（静态图集，TMP Resources 直取——详见 TmpFont）；
    ///   · 文本 raycastTarget=false（不挡点击），装饰背景不拦截、交互件才拦截；
    ///   · LayoutElement 只允许出现在滚动区 content 子树内（Size 自动判定）——非滚动节点
    ///     由预制体锚点/固定尺寸接管，运行时不再注入布局属性；
    ///   · 圆角=运行时生成的 9-slice sprite（缓存复用）。
    ///
    /// 命名：工厂方法 Button/InputField/Toggle 与 UnityEngine.UI 同名类型在类内冲突，
    /// 类型引用一律走文件顶部的 UButton/UInputField/UToggle 别名。
    /// </summary>
    public static class UiKit
    {
        // ================= 预制体寻址（单一声明点） =================

        /// <summary>UI 预制体统一目录（2026-10-15 美术目录重构 Art/UI → Art/Prefab；YooAsset 收集器 UI 组 CollectPath 同址）。</summary>
        public const string PrefabFolder = "Assets/Art/Prefab/";

        /// <summary>预制体名（不含扩展名）→ 编辑器兜底 Assets 路径。
        /// YooAsset 地址=同名（AddressByFileName）——路径/地址不再各处拼字符串。</summary>
        public static string PrefabPath(string name) => $"{PrefabFolder}{name}.prefab";

        // ================= 帧驱动 =================

        /// <summary>
        /// 屏内帧驱动器（替代 UITK root.schedule）：挂在屏根上，Update 逐帧回调——
        /// 层级销毁（离屏）时随之失效，天然解绑。网络泵/倒计时等周期任务用它节流。
        /// </summary>
        public sealed class Updater : MonoBehaviour
        {
            private Action _onUpdate;

            public static Updater Attach(RectTransform root, Action onUpdate)
            {
                var u = root.gameObject.AddComponent<Updater>();
                u._onUpdate = onUpdate;
                return u;
            }

            private void Update() => _onUpdate?.Invoke();
        }

        // ================= 属性描述条（悬浮/按下才出现） =================

        /// <summary>控件交互时的描述上报（合成器描述条订阅；静态事件——屏 Build 挂/OnExit 摘）。</summary>
        public static event Action<string> DescRequested;

        /// <summary>描述挂载组件：随控件销毁自动释放（替代 UITK ConditionalWeakTable）。
        /// 2026-10-05 触发改版：按下即上报——Selectable 禁用（interactable=false，如锁死的作用次数
        /// 下拉、无效组合下的保存）不拦射线与事件分发，照样显示其功能说明；也不再依赖 onClick/值变更
        /// （禁用态永远不触发那些事件）。
        /// 2026-10-16 悬浮二次定案（当日"退出即隐藏"版被否决——控件间隙/行重建高频显隐=闪烁）：
        /// 进入/按下即上报刷新，离开与失活一律不撤文本，描述条只在进入新讲解区域时换内容。</summary>
        public sealed class DescTag : MonoBehaviour, IPointerEnterHandler, IPointerDownHandler
        {
            public string Text;

            public void OnPointerEnter(PointerEventData eventData) => DescRequested?.Invoke(Text);

            public void OnPointerDown(PointerEventData eventData) => DescRequested?.Invoke(Text);
        }

        /// <summary>给控件挂属性描述：悬浮/按下控件（含禁用态）时经 DescRequested 上报显示；
        /// 离开不隐藏，进入新讲解区域时刷新（2026-10-16 定案）；
        /// 同物体重复挂载幂等（后写覆盖文本——重建重绑场景不叠标签）。</summary>
        public static T Described<T>(T c, string desc) where T : Component
        {
            if (c == null || string.IsNullOrEmpty(desc)) return c;
            var tag = c.GetComponent<DescTag>() ?? c.gameObject.AddComponent<DescTag>();
            tag.Text = desc;
            return c;
        }

        /// <summary>TMP_Dropdown 两段式描述（2026-10-05 定案——档位下拉等直挂 TMP 的场景）：
        /// 悬浮/按下头部＝总述一句话；换选＝选中项介绍（optionDesc 返回 null/空＝保持总述）。</summary>
        public static void DescribedOptions(TMP_Dropdown dd, string overview, Func<int, string> optionDesc)
        {
            if (dd == null) return;
            Described(dd, overview);
            if (optionDesc == null) return;
            dd.onValueChanged.AddListener(i =>
            {
                var d = optionDesc(i);
                if (!string.IsNullOrEmpty(d)) DescRequested?.Invoke(d);
            });
        }

        // ================= 引导高亮框材质（SynergyUI/GuideFrame——shader 自驱闪烁） =================

        private static Material _guideFrameSrc;
        private static bool _guideFrameWarned;

        /// <summary>引导/校验高亮框共享材质源（Assets/Art/Shaders/GuideFrame.shader——红边脉冲由 _Time
        /// 驱动，无 C# 逐帧控制）。逐框 Instantiate 实例后写 _HalfSize；shader 缺失（未导入/未打包）时
        /// 回退 UI/Default 静态红块（可见但不闪）并警告一次。</summary>
        public static Material GuideFrameMaterial()
        {
            if (_guideFrameSrc == null)
            {
                // 2026-10-18 真机修复：同 HandFan 雾光——Shader.Find 摸不到 bundle 里的 shader，走 HotUpdateAssets
                var shader = HotUpdateAssets.Load<Shader>("GuideFrame", "Assets/Art/Shaders/GuideFrame.shader");
                if (shader != null)
                {
                    _guideFrameSrc = new Material(shader);
                }
                else
                {
                    if (!_guideFrameWarned)
                    {
                        _guideFrameWarned = true;
                        Debug.LogWarning("[UiKit] SynergyUI/GuideFrame shader 未找到（未导入/未随包收录）——"
                            + "高亮框回退静态红块；shader 在 Assets/Art/Shaders/GuideFrame.shader");
                    }
                    _guideFrameSrc = new Material(Shader.Find("UI/Default"));
                }
            }
            return _guideFrameSrc;
        }

        // ================= 整型滑条 =================

        /// <summary>带标签的整型滑条（对标 UITK SliderInt）：拖动实时回调。</summary>
        public static SliderInt IntSlider(string name, RectTransform parent, string label,
            int min, int max, int value, Action<int> onChanged, float? width = 260f)
        {
            var row = Node(name, parent);
            Configure(row.gameObject.AddComponent<HorizontalLayoutGroup>(), 8f, 0f, TextAnchor.MiddleLeft);

            var lbl = Label("label", row, label, UiStyle.SmallSize, UiStyle.TextDim);
            Size(lbl, w: 96f, h: 20f);

            var sliderObj = Node("slider", row);
            Size(sliderObj, fw: 1f, h: 20f);

            var bg = sliderObj.gameObject.AddComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, 0.08f);

            var fillArea = Node("fill-area", sliderObj);
            StretchInset(fillArea, 0f, 7f);
            var fill = Node("fill", fillArea);
            var fillImg = fill.gameObject.AddComponent<Image>();
            fillImg.color = UiStyle.BtnPrimary;

            var handleArea = Node("handle-area", sliderObj);
            StretchInset(handleArea, 0f, 2f);
            var handle = Node("handle-slide", handleArea);
            var handleImg = handle.gameObject.AddComponent<Image>();
            handleImg.sprite = CircleSprite;
            handleImg.color = UiStyle.White;
            handle.anchorMin = handle.anchorMax = handle.pivot = new Vector2(0.5f, 0.5f);
            handle.sizeDelta = new Vector2(16f, 16f);

            var slider = sliderObj.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImg;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = true;
            slider.SetValueWithoutNotify(value);
            if (onChanged != null) slider.onValueChanged.AddListener(v => onChanged(Mathf.RoundToInt(v)));

            Size(row, w: width, h: 22f);
            return new SliderInt { Slider = slider, Label = lbl };
        }

        /// <summary>整型滑条包装（Label 可就地改文案）。</summary>
        public sealed class SliderInt
        {
            public Slider Slider;
            public TMP_Text Label;
        }

        // ================= 字体 =================

        /// <summary>全局 TMP 字体资源路径（SC-Heavy SDF=思源宋体 Heavy 静态图集，3844 常用字形，
        /// 位于 TMP Resources 下可直取；源 otf 已移除——图集为静态模式，图集外字符渲染为空，
        /// 新增生僻文案需重烘焙图集）。</summary>
        public const string TmpFontResourcePath = "Fonts & Materials/PZD";

        private static TMP_FontAsset _tmpFont;

        /// <summary>TMP 字体资产（懒加载；缺失回落 TMP 默认 LiberationSans SDF 并报错——不阻断装配）。</summary>
        public static TMP_FontAsset TmpFont
        {
            get
            {
                if (_tmpFont == null)
                {
                    _tmpFont = Resources.Load<TMP_FontAsset>(TmpFontResourcePath);
                    if (_tmpFont == null)
                    {
                        Debug.LogError($"[UiKit] SC-Heavy SDF 缺失：Resources[{TmpFontResourcePath}]，回落 LiberationSans SDF");
                        _tmpFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
                    }
                }
                return _tmpFont;
            }
        }

        /// <summary>TextAnchor → TMP 对齐（九宫格映射）。</summary>
        private static TextAlignmentOptions ToTmpAlignment(TextAnchor a) => a switch
        {
            TextAnchor.UpperLeft => TextAlignmentOptions.TopLeft,
            TextAnchor.UpperCenter => TextAlignmentOptions.Top,
            TextAnchor.UpperRight => TextAlignmentOptions.TopRight,
            TextAnchor.MiddleLeft => TextAlignmentOptions.Left,
            TextAnchor.MiddleCenter => TextAlignmentOptions.Center,
            TextAnchor.MiddleRight => TextAlignmentOptions.Right,
            TextAnchor.LowerLeft => TextAlignmentOptions.BottomLeft,
            TextAnchor.LowerCenter => TextAlignmentOptions.Bottom,
            TextAnchor.LowerRight => TextAlignmentOptions.BottomRight,
            _ => TextAlignmentOptions.TopLeft,
        };

        // ================= 圆角 sprite =================

        private static Sprite _rounded;

        /// <summary>圆角矩形（8px 圆角，9-slice：任意尺寸拉伸不糊）。按钮/面板/列表底座共用。</summary>
        public static Sprite RoundedSprite =>
            _rounded != null ? _rounded : (_rounded = MakeRounded(32, 8));

        private static Sprite _circle;

        /// <summary>实心圆（AA 边缘）。色点/徽章用。</summary>
        public static Sprite CircleSprite =>
            _circle != null ? _circle : (_circle = MakeCircle(32));

        private static Sprite MakeRounded(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            var px = new Color32[size * size];
            float half = size * 0.5f - 1f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 圆角矩形有符号距离（内正外负），1px 线性过渡抗锯齿
                    float dx = Mathf.Max(Mathf.Abs(x - (size - 1) * 0.5f) - (half - radius), 0f);
                    float dy = Mathf.Max(Mathf.Abs(y - (size - 1) * 0.5f) - (half - radius), 0f);
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(radius - dist + 0.5f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius));
        }

        private static Sprite MakeCircle(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            var px = new Color32[size * size];
            float cx = (size - 1) * 0.5f, cy = (size - 1) * 0.5f, r = size * 0.5f - 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    float alpha = Mathf.Clamp01(r - dist + 0.5f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        // ================= 基础几何 =================

        /// <summary>深度按名查找后代节点（含未激活节点；先试单层直查再全树扫描）。
        /// 预制体绑定的唯一查找方式——对中间包装层增删免疫，不做路径查找。</summary>
        public static RectTransform FindDeep(RectTransform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name)) return null;
            var direct = root.Find(name);
            if (direct != null) return (RectTransform)direct;
            foreach (var t in root.GetComponentsInChildren<RectTransform>(true))
            {
                if (t != root && t.name == name) return t;
            }
            return null;
        }

        /// <summary>深度按名收集全部同名后代（含未激活）。预制体手工改造期可能出现新旧同名节点
        /// 并存（如旧自绘下拉头未删+新 TMP 下拉同名），绑定侧按"含 TMP_Dropdown 优先"裁决。</summary>
        public static List<RectTransform> FindDeepAll(RectTransform root, string name)
        {
            var hits = new List<RectTransform>();
            if (root == null || string.IsNullOrEmpty(name)) return hits;
            foreach (var t in root.GetComponentsInChildren<RectTransform>(true))
            {
                if (t != root && t.name == name) hits.Add(t);
            }
            return hits;
        }

        /// <summary>克隆父节点下的隐藏模板（不激活的直属子节点 tpl-XXX），克隆体去掉 tpl- 前缀命名并激活；
        /// 模板不存在、或处于激活态（防误克隆活行）返回 null。动态子物体的布局样式由模板接管，代码只填数据。</summary>
        public static RectTransform CloneTemplate(string templateName, RectTransform parent) =>
            CloneTemplateFrom(parent, templateName, parent);

        /// <summary>跨级克隆隐藏模板（2026-10-05）：模板按名在 templateRoot 直属查找（tpl-XXX 隐藏约定同上），
        /// 克隆体实例化到 parent 下——用于模板与克隆目标不同父的场景（如 content 直属模板克隆进槽内）。</summary>
        public static RectTransform CloneTemplateFrom(RectTransform templateRoot, string templateName, RectTransform parent)
        {
            if (parent == null || string.IsNullOrEmpty(templateName)) return null;
            var tpl = templateRoot != null ? templateRoot.Find(templateName) : null;
            if (tpl == null || tpl.gameObject.activeSelf) return null;
            var go = UnityEngine.Object.Instantiate(tpl.gameObject, parent, false);
            go.name = templateName.StartsWith("tpl-", StringComparison.Ordinal)
                ? templateName.Substring(4) : templateName;
            go.SetActive(true);
            return (RectTransform)go.transform;
        }

        /// <summary>空节点（默认拉伸铺满父节点；进布局组后由布局接管，无需关心锚点）。</summary>
        public static RectTransform Node(string name, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            Stretch(rt);
            return rt;
        }

        /// <summary>拉伸铺满父节点。</summary>
        public static void Stretch(RectTransform t)
        {
            t.anchorMin = Vector2.zero;
            t.anchorMax = Vector2.one;
            t.offsetMin = Vector2.zero;
            t.offsetMax = Vector2.zero;
        }

        /// <summary>拉伸铺满并四周内缩（x/y 像素）。</summary>
        public static void StretchInset(RectTransform t, float x, float y)
        {
            t.anchorMin = Vector2.zero;
            t.anchorMax = Vector2.one;
            t.offsetMin = new Vector2(x, y);
            t.offsetMax = new Vector2(-x, -y);
        }

        /// <summary>
        /// 子项尺寸驱动。LE 策略（2026-10-01 预制体化定案）：LayoutElement 只在结构上被需要时
        /// 才挂——① 滚动区 content 子树内（列表行由布局组+ContentSizeFitter 按 preferred 排布）；
        /// ② 父布局组控制该轴（childControl=true 时直写 rect 会被布局覆盖，LE 是唯一通路）。
        /// 其余（预制体静态节点/自由锚点区）不挂 LE——w/h 直接写入 RectTransform，
        /// fw/fh/min 忽略（"无意义 LE"清理范围，静态布局由预制体锚点/固定尺寸接管）。
        /// LE 未提供的维度显式置 -1（语义=忽略该维——代码 AddComponent 默认 0 会把槽区压塌）。
        /// </summary>
        public static LayoutElement Size(Component c,
            float? w = null, float? h = null,
            float? fw = null, float? fh = null,
            float? minW = null, float? minH = null)
        {
            var rt = c.transform as RectTransform;
            if (rt == null) return null;

            bool ctrlW = ParentControlsAxis(rt, horizontal: true);
            bool ctrlH = ParentControlsAxis(rt, horizontal: false);
            bool needLe = IsInsideScrollContent(rt)
                || (w.HasValue && ctrlW) || (fw.HasValue && ctrlW) || (minW.HasValue && ctrlW)
                || (h.HasValue && ctrlH) || (fh.HasValue && ctrlH) || (minH.HasValue && ctrlH);

            if (!needLe)
            {
                if (w.HasValue) rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, w.Value);
                if (h.HasValue) rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, h.Value);
                return rt.GetComponent<LayoutElement>();
            }

            var le = c.GetComponent<LayoutElement>();
            if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
            le.minWidth = -1f;
            le.minHeight = -1f;
            le.preferredWidth = -1f;
            le.preferredHeight = -1f;
            le.flexibleWidth = -1f;
            le.flexibleHeight = -1f;
            if (w.HasValue) le.preferredWidth = w.Value;
            if (h.HasValue) le.preferredHeight = h.Value;
            if (fw.HasValue) le.flexibleWidth = fw.Value;
            if (fh.HasValue) le.flexibleHeight = fh.Value;
            if (minW.HasValue) le.minWidth = minW.Value;
            if (minH.HasValue) le.minHeight = minH.Value;
            return le;
        }

        /// <summary>直接父节点的布局组是否控制指定轴（childControl=true/网格——决定 LE 是否结构必需）。</summary>
        private static bool ParentControlsAxis(RectTransform rt, bool horizontal)
        {
            var p = rt.parent as RectTransform;
            if (p == null) return false;
            var h = p.GetComponent<HorizontalLayoutGroup>();
            if (h != null && h.enabled) return horizontal ? h.childControlWidth : h.childControlHeight;
            var v = p.GetComponent<VerticalLayoutGroup>();
            if (v != null && v.enabled) return horizontal ? v.childControlWidth : v.childControlHeight;
            var g = p.GetComponent<GridLayoutGroup>();
            return g != null && g.enabled;
        }

        /// <summary>节点是否位于某 ScrollRect 的 content 子树内（列表行判定——LE 恒必需：布局组+ContentSizeFitter 读 preferred）。</summary>
        public static bool IsInsideScrollContent(RectTransform rt)
        {
            for (var p = rt.parent as RectTransform; p != null; p = p.parent as RectTransform)
            {
                var sr = p.GetComponent<ScrollRect>();
                if (sr != null && sr.content != null && rt.IsChildOf(sr.content))
                    return true;
            }
            return false;
        }

        // ================= 布局容器 =================

        /// <summary>屏根：全屏深色底 + 纵向布局（对标旧 USS .screen）。</summary>
        public static RectTransform Screen(string name, RectTransform parent, float pad = UiStyle.Pad, float spacing = 12f)
        {
            var rt = Node(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = UiStyle.ScreenBg; // 底层兜底拦截点击，防止穿到 3D
            img.raycastTarget = true;
            Configure(rt.gameObject.AddComponent<VerticalLayoutGroup>(), spacing, pad, TextAnchor.UpperLeft);
            return rt;
        }

        /// <summary>横向排列容器（对标 USS flex-direction: row）。</summary>
        public static RectTransform Row(string name, RectTransform parent, float spacing = 8f, float pad = 0f, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var rt = Node(name, parent);
            Configure(rt.gameObject.AddComponent<HorizontalLayoutGroup>(), spacing, pad, align);
            return rt;
        }

        /// <summary>纵向排列容器（对标 USS flex-direction: column）。</summary>
        public static RectTransform Column(string name, RectTransform parent, float spacing = 8f, float pad = 0f, TextAnchor align = TextAnchor.UpperLeft)
        {
            var rt = Node(name, parent);
            Configure(rt.gameObject.AddComponent<VerticalLayoutGroup>(), spacing, pad, align);
            return rt;
        }

        /// <summary>面板：圆角底+描边+纵向布局（对标 .panel/.battle-zone）。</summary>
        public static RectTransform Panel(string name, RectTransform parent,
            float pad = 12f, float spacing = 8f, Color? bg = null, Color? border = null)
        {
            var rt = Node(name, parent);
            Bg(rt, bg ?? UiStyle.PanelBg, border ?? UiStyle.Border);
            Configure(rt.gameObject.AddComponent<VerticalLayoutGroup>(), spacing, pad, TextAnchor.UpperLeft);
            return rt;
        }

        /// <summary>全屏弹层（不参与父布局，置顶兄弟序）——下拉弹窗/模态框挂这里逃逸裁剪。
        /// ignoreLayout 的 LE 是语义必需（逃逸父布局的唯一手段），不属"无意义 LE"清理范围。</summary>
        public static RectTransform Overlay(string name, RectTransform parent)
        {
            var rt = Node(name, parent);
            rt.SetAsLastSibling();
            rt.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            return rt;
        }

        private static void Configure(HorizontalOrVerticalLayoutGroup g, float spacing, float pad, TextAnchor align)
        {
            g.spacing = spacing;
            int p = Mathf.RoundToInt(pad);
            g.padding = new RectOffset(p, p, p, p);
            g.childAlignment = align;
            // control=true：布局组读取子项 LayoutElement/Graphic 的 preferred/flexible 并驱动其
            // 尺寸（flexbox 语义——UiKit.Size 对应 flexGrow/preferred）。control=false 时布局组
            // 忽略 LayoutElement、拿子项当前 rect 兜底——代码新建节点初始为 0，会整树锁死 0 尺寸。
            g.childControlWidth = true;
            g.childControlHeight = true;
            g.childForceExpandWidth = false;
            g.childForceExpandHeight = false;
        }

        /// <summary>装饰背景（圆角+可选描边）。raycastTarget=false——只挡视觉不挡点击。</summary>
        private static Image Bg(RectTransform rt, Color color, Color? border = null)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = RoundedSprite;
            img.type = Image.Type.Sliced;
            img.color = color;
            img.raycastTarget = false;
            if (border.HasValue)
            {
                var ol = rt.gameObject.AddComponent<Outline>();
                ol.effectColor = border.Value;
                ol.effectDistance = Vector2.one;
            }
            return img;
        }

        // ================= 控件 =================

        /// <summary>文本（对标 USS Label；全量 TMP 化——动态图集中文字形按需光栅）。
        /// wrap=false 单行不折行；wrap=true 宽度受容器约束时折行。</summary>
        public static TMP_Text Label(string name, RectTransform parent, string text,
            int size = UiStyle.BodySize, Color? color = null,
            TextAnchor align = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Normal,
            bool wrap = false)
        {
            var rt = Node(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = TmpFont;
            t.text = text ?? "";
            t.fontSize = size;
            t.fontStyle = (FontStyles)style; // 枚举位同值（Normal/Bold/Italic/BoldAndItalic）
            t.color = color ?? UiStyle.TextBody;
            t.alignment = ToTmpAlignment(align);
            t.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow; // 高度交给容器/自适应
            t.raycastTarget = false;
            t.richText = false;
            return t;
        }

        /// <summary>提示条文本（绿色，对标 .toast）。</summary>
        public static TMP_Text Toast(string name, RectTransform parent, string initial = "") =>
            Label(name, parent, initial, 13, UiStyle.ToastGreen);

        /// <summary>
        /// 按钮（对标 .btn 变体：bg=BtnPrimary/BtnDanger 得主色/危险态）。
        /// 未定宽时按文本自适应（+28px 内边距）；高度默认 36。
        /// </summary>
        public static UButton Button(string name, RectTransform parent, string text,
            Action onClick = null, Color? bg = null, Color? fg = null,
            float? width = null, float? height = UiStyle.BtnHeight, int fontSize = UiStyle.BodySize)
        {
            var rt = Node(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = RoundedSprite;
            img.type = Image.Type.Sliced;
            img.color = bg ?? UiStyle.BtnBg;
            img.raycastTarget = true;

            var btn = rt.gameObject.AddComponent<UButton>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.22f, 1.22f, 1.22f, 1f); // 对标 .btn:hover 提亮
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);     // 对标 .btn:active 压暗
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.5f);
            colors.fadeDuration = 0.08f;
            btn.colors = colors;

            var label = Label("label", rt, text, fontSize,
                fg ?? (Equals(bg, UiStyle.BtnPrimary) || Equals(bg, UiStyle.BtnDanger) ? UiStyle.White : UiStyle.TextBody),
                TextAnchor.MiddleCenter, FontStyle.Bold);
            StretchInset(label.rectTransform, 8f, 2f);

            if (onClick != null) btn.onClick.AddListener(() => { Debug.Log($"[UI点击] {name}"); onClick.Invoke(); });
            Size(rt, w: width ?? (label.preferredWidth + 28f), h: height, minW: 30f);
            return btn;
        }

        /// <summary>迷你按钮（对标 .btn--mini：26 高、12 号字）。</summary>
        public static UButton MiniButton(string name, RectTransform parent, string text,
            Action onClick = null, Color? bg = null, Color? fg = null, float? width = null) =>
            Button(name, parent, text, onClick, bg, fg, width, UiStyle.MiniBtnHeight, UiStyle.SmallSize);

        /// <summary>绑定/克隆/新建三段式按钮（2026-10-03 手改预制体约定）：
        /// ① parent 子树深度按名找烘焙节点（不要求直接子级——嵌套包装层免疫）——接线点击+更新文本/态色，
        /// 尺寸样式以节点为准；② 隐藏模板 tpl-&lt;name&gt;（parent 直属）克隆后同上；③ 都无走 Button 工厂
        /// （建在 fallbackParent ?? parent，并 LogWarning 暴露断线）。
        /// 刷新重入安全：先清运行时监听再挂（烘焙按钮勿带序列化 onClick，会被覆盖语义）。</summary>
        /// <summary>烘焙按钮单名精确绑定（2026-10-07 兜底退役）：按名深找节点→绑 UButton/回写文案与配色；
        /// 缺节点 LogError 返回 null——不再克隆 tpl- 模板、不再代码补建。</summary>
        public static UButton BindableButton(string name, RectTransform parent, string text,
            Action onClick = null, Color? bg = null, Color? fg = null)
        {
            RectTransform node = parent != null ? UiKit.FindDeep(parent, name) : null;
            if (node == null)
            {
                Debug.LogError($"[UiKit] BindableButton 未找到烘焙节点：{name}（不补建——检查预制体）");
                return null;
            }

            var btn = node.GetComponent<UButton>();
            if (btn == null) btn = node.gameObject.AddComponent<UButton>();
            btn.onClick.RemoveAllListeners();
            if (onClick != null)
                btn.onClick.AddListener(() => { Debug.Log($"[UI点击] {name}"); onClick(); });
            var t = node.GetComponentInChildren<TMP_Text>(true);
            if (t != null)
            {
                if (text != null) t.text = text;
                if (fg != null) t.color = fg.Value;
            }
            if (bg != null)
            {
                var img = node.GetComponent<Image>() ?? node.GetComponentInChildren<Image>(true);
                if (img != null) img.color = bg.Value;
            }
            return btn;
        }

        /// <summary>绑定/克隆/新建三段式文本：parent 子树深度找同名烘焙节点/tpl- 模板——只更新文本
        /// （样式以节点为准），都无走 Label 工厂。用于常驻提示位（editing-tag、切换钮标签位等）。</summary>
        public static TMP_Text BindableLabel(string name, RectTransform parent, string text,
            int size = UiStyle.BodySize, Color? color = null,
            TextAnchor align = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Normal, bool wrap = false)
        {
            RectTransform node = parent != null ? UiKit.FindDeep(parent, name) : null;
            if (node == null) node = CloneTemplate("tpl-" + name, parent);
            if (node == null) return Label(name, parent, text, size, color, align, style, wrap);
            var t = node.GetComponentInChildren<TMP_Text>(true);
            if (t == null) return Label("text", node, text, size, color, align, style, wrap);
            t.text = text ?? "";
            return t;
        }

        /// <summary>列表行底座（对标 .list-row）：圆角深底——挂在已是 Row 的节点上（不挡点击）。</summary>
        public static Image BgRow(RectTransform row, Color? bg = null)
        {
            var img = row.gameObject.AddComponent<Image>();
            img.sprite = RoundedSprite;
            img.type = Image.Type.Sliced;
            img.color = bg ?? UiStyle.RowBg;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>色点（对标 .color-dot 提亮版；14px 圆）。</summary>
        public static Image Dot(string name, RectTransform parent, Color color, float size = 14f)
        {
            var rt = Node(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = CircleSprite;
            img.color = color;
            img.raycastTarget = false;
            Size(rt, w: size, h: size);
            return img;
        }

        /// <summary>开关（对标 UITK Toggle）。</summary>
        public static UToggle Toggle(string name, RectTransform parent, string label,
            bool initial, Action<bool> onChanged = null)
        {
            var rt = Node(name, parent);
            Configure(rt.gameObject.AddComponent<HorizontalLayoutGroup>(), 6f, 0f, TextAnchor.MiddleLeft);

            var box = Node("box", rt);
            var boxImg = Bg(box, UiStyle.RowBg);
            boxImg.raycastTarget = true;
            var check = Node("check", box);
            var checkImg = check.gameObject.AddComponent<Image>();
            checkImg.color = UiStyle.TextPrimary;
            checkImg.raycastTarget = false;
            check.anchorMin = check.anchorMax = check.pivot = new Vector2(0.5f, 0.5f);
            check.sizeDelta = new Vector2(12f, 12f);
            Size(box, w: 22f, h: 22f);

            var txt = Label("label", rt, label, UiStyle.BodySize, UiStyle.TextBody);
            Size(txt, w: txt.preferredWidth + 4f, h: 22f);

            var toggle = rt.gameObject.AddComponent<UToggle>();
            toggle.targetGraphic = boxImg;
            toggle.graphic = checkImg;
            toggle.isOn = initial;
            if (onChanged != null) toggle.onValueChanged.AddListener(onChanged.Invoke);
            Size(rt, h: 24f);
            return toggle;
        }

        /// <summary>单行输入框（对标 .text-input；TMP 版）。onChanged=每次按键；onSubmit=回车/失焦。</summary>
        public static UInputField InputField(string name, RectTransform parent, string placeholder,
            string initial = "", Action<string> onChanged = null, Action<string> onSubmit = null,
            float? width = 200f, int fontSize = UiStyle.BodySize, bool integerOnly = false)
        {
            var rt = Node(name, parent);
            var bg = rt.gameObject.AddComponent<Image>();
            bg.sprite = RoundedSprite;
            bg.type = Image.Type.Sliced;
            bg.color = UiStyle.FieldBg;

            var txt = Label("text", rt, "", fontSize, UiStyle.TextBody);
            StretchInset(txt.rectTransform, 8f, 4f);
            var ph = Label("placeholder", rt, placeholder, fontSize, UiStyle.TextFaint);
            StretchInset(ph.rectTransform, 8f, 4f);

            var field = rt.gameObject.AddComponent<UInputField>();
            field.textComponent = txt;
            field.placeholder = ph;
            field.targetGraphic = bg;
            field.contentType = integerOnly
                ? TMP_InputField.ContentType.IntegerNumber
                : TMP_InputField.ContentType.Standard;
            if (onChanged != null) field.onValueChanged.AddListener(onChanged.Invoke);
            if (onSubmit != null) field.onEndEdit.AddListener(onSubmit.Invoke);
            field.SetTextWithoutNotify(initial ?? "");
            Size(rt, w: width, h: UiStyle.FieldHeight);
            return field;
        }

        /// <summary>整数控件（对标 UITK IntegerField）：非法输入不回调。</summary>
        public static UInputField IntField(string name, RectTransform parent, string placeholder,
            int initial, Action<int> onChanged, float? width = 90f)
        {
            return InputField(name, parent, placeholder, initial.ToString(),
                onChanged: s => { if (int.TryParse(s, out var v)) onChanged(v); },
                width: width, integerOnly: true);
        }

        // ================= 滚动列表 =================

        /// <summary>纵向滚动列（对标 USS ScrollView）：wheel/拖拽滚动，无滚动条。</summary>
        public sealed class Scroll
        {
            public ScrollRect Rect;
            public RectTransform Content;

            /// <summary>滚动到底部（追加日志后调用）。</summary>
            public void ScrollToBottom() => Rect.verticalNormalizedPosition = 0f;
        }

        /// <summary>
        /// 纵向滚动列（对标 .list/.card-row 容器）：深色圆角底 + RectMask2D 裁剪 +
        /// 内容自适应高度。子项默认横向铺满（childControlWidth+forceExpand）。
        /// </summary>
        public static Scroll ScrollColumn(string name, RectTransform parent,
            float spacing = 4f, float pad = 4f, Color? bg = null)
        {
            var root = Node(name, parent);
            var img = root.gameObject.AddComponent<Image>();
            img.sprite = RoundedSprite;
            img.type = Image.Type.Sliced;
            img.color = bg ?? UiStyle.ListBg;
            img.raycastTarget = true; // 接收滚轮/拖拽

            var scroll = root.gameObject.AddComponent<ScrollRect>();
            var viewport = Node("viewport", root);
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = Node("content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;

            var g = content.gameObject.AddComponent<VerticalLayoutGroup>();
            g.spacing = spacing;
            int p = Mathf.RoundToInt(pad);
            g.padding = new RectOffset(p, p, p, p);
            g.childControlWidth = true;        // 列表行横向铺满
            g.childForceExpandWidth = true;
            g.childControlHeight = false;
            g.childForceExpandHeight = false;
            g.childAlignment = TextAnchor.UpperLeft;

            content.gameObject.AddComponent<ContentSizeFitter>()
                .verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;
            return new Scroll { Rect = scroll, Content = content };
        }

        // ================= 回收式虚拟列表 =================

        /// <summary>TMP 文本量高（虚拟列表提示行等变高条目用）：按给定宽度/字号测 preferred 高度。
        /// 隐藏量高件常驻（DontDestroyOnLoad），与 Label 同默认字体——测量口径与实际渲染一致。</summary>
        private static TextMeshProUGUI _textMeasurer;

        /// <summary>量文本 preferred 高度（px）。width=可用宽（自动换行口径），fontSize=字号。</summary>
        public static float MeasureTextHeight(string text, float width, float fontSize)
        {
            if (string.IsNullOrEmpty(text)) return fontSize + 4f;
            if (_textMeasurer == null)
            {
                var go = new GameObject("uikit-text-measurer", typeof(RectTransform));
                if (Application.isPlaying) UnityEngine.Object.DontDestroyOnLoad(go); // 编辑态 DDoL 抛 InvalidOperationException（量高件只在编辑态冒烟时会碰到）
                _textMeasurer = go.AddComponent<TextMeshProUGUI>();
                go.SetActive(false);
            }
            return _textMeasurer.GetPreferredValues(text, Mathf.Max(50f, width), fontSize).y;
        }

        /// <summary>
        /// 回收式虚拟滚动列表（2026-10-21 性能定案）：接管 Scroll 的 content——读取并停用其上的
        /// VLG/GLG/CSF（布局参数 spacing/padding 被本类吸收），行位置/内容高度改手工计算。
        /// 任意时刻只有可视区（含缓冲带）的行是激活态；滚动/刷新时出界行回池、入界行从池按
        /// 「行种类」取用复用——结构建一次（make），数据反复绑（bind，先 RemoveAllListeners 再挂）。
        /// 行种类（kindOf→makeOf 分池）：表头/数据行/提示行等不同结构互不混用。
        /// 两形态：变高列表（SetItems+heightOf，单列满宽）；网格（SetGrid，等格多列，列数随宽自算）。
        /// 前提：行的激活态子节点须全由 make 建（含按钮等子控件），bind 只填数据/换监听。
        /// </summary>
        /// <summary>
        /// 回收式虚拟滚动列表（2026-10-22 引擎换血：自制 Driver 在真实滚动下不补行——改接
        /// LoopScrollRect 1.1.5，qiankanglai/MIT，Assets/Plugins/LoopScrollRect）：
        /// 引擎组件 = LoopVerticalScrollRectMulti（接管原 ScrollRect：原组件仅禁用保留序列化态），
        /// 可见行回收/补行由引擎 LateUpdate 驱动（真实滚动/惯性/边界全覆盖）。
        /// 本类=行池+行种类路由：GetObject 按 kindOf(index) 分池出 makeOf 建的行（LoopScrollPrefabSource），
        /// ProvideData 转 bind（LoopScrollMultiDataSource）；变高列表走 sizeHelper（前缀和）。
        /// content 上的 VLG/GLG 保持启用（引擎靠它排版可见行）；克隆模板被搬去滚动根下
        /// tpl-park（content 只装活行——引擎的池化/边界数学要求），工厂克隆模板用 TemplatePark 定位。
        /// 行结构建一次（make），数据反复绑（bind，先 RemoveAllListeners 再挂）。
        /// </summary>
        public sealed class RecycledList : UnityEngine.UI.LoopScrollPrefabSource, UnityEngine.UI.LoopScrollDataSource, UnityEngine.UI.LoopScrollMultiDataSource
        {
            /// <summary>行工厂：新建一条完整结构的行（只在池空时调用）。</summary>
            public delegate RectTransform MakeFn();
            /// <summary>行绑定：填数据+重挂监听（池化复用反复调用——先清旧监听）。</summary>
            public delegate void BindFn(RectTransform row, int index);

            private UnityEngine.UI.LoopScrollRectBase _loop;          // 回收引擎（null=Attach 失败，各口自守；网格=单kind变体/列表=Multi变体）
            private RectTransform _viewport;
            private RectTransform _content;
            private RectTransform _park;                 // 克隆模板停靠点（scroll 根下 inactive）
            private GridLayoutGroup _gridGeom;           // content 的 GLG（网格模式行预置尺寸用；列表模式 null）

            private Func<int, int> _kindOf;
            private Func<int, MakeFn> _makeOf;
            private BindFn _bind;

            private float _spacing = 4f;                 // VLG 吸收（sizeHelper 前缀和口径用）
            private float[] _offsets;                    // 前缀和：条目高+后置 spacing（长度 count+1；网格=null）
            private int _count;

            private readonly Dictionary<int, List<RectTransform>> _pools = new Dictionary<int, List<RectTransform>>();
            private readonly Dictionary<RectTransform, int> _rowKind = new Dictionary<RectTransform, int>(); // 行→结构种类（回池归属）

            /// <summary>数据条数（外部空态/诊断用——content.childCount 不再等于条数）。</summary>
            public int Count => _loop != null ? _loop.totalCount : 0;
            /// <summary>接管中的 content（空态提示行等外部节点若直接挂入须自行定位）。</summary>
            public RectTransform Content => _content;

            /// <summary>当前激活行数（引擎窗口 itemTypeEnd−itemTypeStart；编辑态引擎不铺行恒 0）。</summary>
            public int LiveCount
            {
                get { var (start, end) = ItemWindow(); return Mathf.Max(0, end - start); }
            }

            /// <summary>激活窗口内的行（窗口外已回池=null）——断言/测试探针口。</summary>
            public RectTransform RowAt(int index)
            {
                var (start, end) = ItemWindow();
                if (index < start || index >= end) return null;
                int child = index - start;
                return _content != null && child < _content.childCount ? _content.GetChild(child) as RectTransform : null;
            }

            private static readonly System.Reflection.FieldInfo FiStart = typeof(UnityEngine.UI.LoopScrollRectBase)
                .GetField("itemTypeStart", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            private static readonly System.Reflection.FieldInfo FiEnd = typeof(UnityEngine.UI.LoopScrollRectBase)
                .GetField("itemTypeEnd", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            private (int start, int end) ItemWindow()
            {
                if (_loop == null || FiStart == null || FiEnd == null) return (0, 0);
                return ((int)FiStart.GetValue(_loop), (int)FiEnd.GetValue(_loop));
            }

            /// <summary>接管滚动区：禁用原 ScrollRect、挂 LoopVerticalScrollRectMulti、模板搬去 tpl-park。
            /// content 既有激活子（旧路径遗留行）清场销毁——接入方不再自清。</summary>
            public static RecycledList Attach(Scroll scroll)
            {
                if (scroll?.Rect == null || scroll.Content == null) return null;
                var sr = scroll.Rect;
                var content = scroll.Content;
                var viewport = sr.viewport != null ? (RectTransform)sr.viewport.transform : content.parent as RectTransform;
                if (viewport == null) return null;

                var list = new RecycledList { _content = content, _viewport = viewport };

                // 布局组保持启用：引擎靠 VLG/GLG 排版可见行（spacing/padding 也由引擎惰性读取）
                var vlg = content.GetComponent<VerticalLayoutGroup>();
                if (vlg != null) { list._spacing = vlg.spacing; vlg.enabled = true; }
                var glg = content.GetComponent<GridLayoutGroup>();
                if (glg != null)
                {
                    list._spacing = glg.spacing.y;
                    glg.enabled = true;
                    list._gridGeom = glg;
                    // 库 Awake 断言 Flexible 不支持——约束必须在 SetActive(恢复) 前就位（列数 SetGrid 再精算）
                    glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                }
                var csf = content.GetComponent<ContentSizeFitter>();
                if (csf == null) csf = content.gameObject.AddComponent<ContentSizeFitter>();
                // 引擎边界数学依赖 content.rect 随可见行长高（GetBounds 量 content 自身角点）——
                // CSF 垂直 Preferred 是官方接法，停用会让引擎判"恒缺行"无限追加+弹性把滚动拉回顶
                csf.enabled = true;
                csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                // 克隆模板搬出 content（引擎的池化/边界数学要求 content 只装活行）；
                // 旧路径遗留的激活子一并清场
                var parkGo = new GameObject("tpl-park", typeof(RectTransform));
                list._park = parkGo.transform as RectTransform;
                list._park.SetParent(sr.transform, false);
                list._park.gameObject.SetActive(false);
                for (int i = content.childCount - 1; i >= 0; i--)
                {
                    var child = content.GetChild(i);
                    if (!child.gameObject.activeSelf) { child.SetParent(list._park, false); continue; }
                    if (Application.isPlaying) UnityEngine.Object.Destroy(child.gameObject);
                    else UnityEngine.Object.DestroyImmediate(child.gameObject);
                }

                // 引擎交换：原 ScrollRect 禁用（序列化监听不丢、可回退），Loop 组件复制滚动参数接管。
                // 库默认 vertical/horizontal 双 true：AddComponent 会同步跑 Awake 断言——先临时禁用 GO
                // 把 Awake 顺延到参数配好、重新激活时（demo 是预制体作者手改的，代码挂组件必踩）。
                // 变体选择：网格走单 kind 的 LoopVerticalScrollRect（GetSize 走 GLG cellSize——
                // Multi 变体的 GetSize 恒走 preferred，代码现建的首行布局前 preferred=0 会触发库断言）；
                // 变高列表（多 kind 行）走 Multi（temp-pool 不跨种类复用）。
                sr.enabled = false;
                bool wasActive = sr.gameObject.activeSelf;
                sr.gameObject.SetActive(false);
                UnityEngine.UI.LoopScrollRectBase loop;
                if (list._gridGeom != null)
                {
                    var single = sr.gameObject.AddComponent<UnityEngine.UI.LoopVerticalScrollRect>();
                    single.dataSource = list; // 单 kind 数据源（与 Multi 接口同签名，本类双实现）
                    loop = single;
                }
                else
                {
                    var multi = sr.gameObject.AddComponent<UnityEngine.UI.LoopVerticalScrollRectMulti>();
                    multi.dataSource = list;
                    loop = multi;
                }
                loop.content = content;
                loop.viewport = viewport;
                loop.horizontal = false;
                loop.vertical = true;
                loop.movementType = (UnityEngine.UI.LoopScrollRectBase.MovementType)(int)sr.movementType;
                loop.elasticity = sr.elasticity;
                loop.inertia = sr.inertia;
                loop.decelerationRate = sr.decelerationRate;
                loop.scrollSensitivity = sr.scrollSensitivity;
                loop.verticalScrollbar = sr.verticalScrollbar;
                loop.horizontalScrollbar = sr.horizontalScrollbar;
                sr.gameObject.SetActive(wasActive);

                // content 顶部对齐归一（引擎编辑器断言：pivot/锚点 Y=1）
                content.pivot = new Vector2(content.pivot.x, 1f);
                content.anchorMin = new Vector2(content.anchorMin.x, 1f);
                content.anchorMax = new Vector2(content.anchorMax.x, 1f);
                content.anchoredPosition = Vector2.zero;

                loop.prefabSource = list;
                list._loop = loop;
                return list;
            }

            /// <summary>克隆模板停靠点（Attach 搬迁目的地）——行工厂克隆模板时的查找范围（找不到回落 content 兼容未接管滚动区）。</summary>
            public static RectTransform TemplatePark(Scroll scroll)
            {
                var root = scroll?.Rect;
                if (root == null) return scroll != null ? scroll.Content : null;
                return root.transform.Find("tpl-park") as RectTransform ?? scroll.Content;
            }

            /// <summary>配置行种类与工厂/绑定（kindOf 决定分池，makeOf 按种类建行，bind 填数据）。</summary>
            public RecycledList Configure(Func<int, int> kindOf, Func<int, MakeFn> makeOf, BindFn bind)
            {
                _kindOf = kindOf; _makeOf = makeOf; _bind = bind;
                return this;
            }

            /// <summary>变高列表数据源（单列满宽行）：count 条目 + 逐条目高度。
            /// 换数据即整体重置（回顶重铺）；行高经 sizeHelper 前缀和供引擎滚动条/跳位数学。</summary>
            public void SetItems(int count, Func<int, float> heightOf)
            {
                _count = Mathf.Max(0, count);
                _offsets = new float[_count + 1];
                float acc = 0f;
                for (int i = 0; i < _count; i++)
                {
                    _offsets[i] = acc;
                    acc += Mathf.Max(1f, heightOf?.Invoke(i) ?? 60f) + _spacing;
                }
                _offsets[_count] = acc; // 含末位 spacing（sizeHelper 计算时扣除）
                if (_loop == null) return;
                _loop.sizeHelper = _count > 0 ? new SizeHelper { Owner = this } : null;
                _loop.totalCount = _count;
                if (Application.isPlaying) _loop.RefillCells(); // 引擎仅 Play 铺行；编辑态 Count 已就位（空态诊断可用）
            }

            /// <summary>网格数据源（等格多列）：格几何取 content 的 GLG（cellW/cellH 可覆写），
            /// 列数按内容宽自算并锁进 GLG（引擎只认 FixedColumnCount）。</summary>
            public void SetGrid(int count, float cellW = 0f, float cellH = 0f)
            {
                _count = Mathf.Max(0, count);
                _offsets = null;
                if (_loop == null) return;
                var glg = _content != null ? _content.GetComponent<GridLayoutGroup>() : null;
                if (glg == null || glg.cellSize.x <= 0f || glg.cellSize.y <= 0f)
                {
                    if (glg != null)
                    {
                        if (cellW > 0f) glg.cellSize = new Vector2(cellW, glg.cellSize.y);
                        if (cellH > 0f) glg.cellSize = new Vector2(glg.cellSize.x, cellH);
                    }
                    Debug.LogError("[RecycledList] SetGrid 缺格几何（content 无 GridLayoutGroup 且未传 cellW/cellH）");
                    _loop.totalCount = 0;
                    return;
                }
                int cols = Mathf.Max(1, Mathf.FloorToInt(
                    (_content.rect.width - glg.padding.left - glg.padding.right + glg.spacing.x)
                    / (glg.cellSize.x + glg.spacing.x)));
                glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                glg.constraintCount = cols;
                _loop.sizeHelper = null; // 等格：引擎原生估算
                _loop.totalCount = _count;
                if (Application.isPlaying) _loop.RefillCells();
            }

            /// <summary>条数/高度不变、数据内容变：只重绑当前激活行（如选中态变化）。
            /// 2026-10-27 点选回顶修复：RefreshCells 内 UpdateBounds 重排版会把滚动窗口吸回顶
            /// （卡池/图库点选载入场景实测），重绑前后保存/还原 content 滚动位兜底。</summary>
            public void RebindVisible()
            {
                if (_loop == null || !Application.isPlaying) return;
                Vector2 scrollPos = _content != null ? _content.anchoredPosition : Vector2.zero;
                _loop.RefreshCells();
                if (_content != null) _content.anchoredPosition = scrollPos;
            }

            /// <summary>滚回顶重铺（SetItems/SetGrid 已内含——外部显式复位用）。</summary>
            public void ResetToTop()
            {
                if (_loop != null && Application.isPlaying) _loop.RefillCells();
            }

            // ---------------- LoopScrollRect 回调 ----------------

            /// <summary>引擎取行（LoopScrollPrefabSource）：按 kind 分池出/建行。</summary>
            public GameObject GetObject(int index)
            {
                int kind = (_kindOf != null && index >= 0 && index < _count) ? _kindOf(index) : 0;
                if (!_pools.TryGetValue(kind, out var pool)) _pools[kind] = pool = new List<RectTransform>();
                while (pool.Count > 0)
                {
                    var pooled = pool[pool.Count - 1];
                    pool.RemoveAt(pool.Count - 1);
                    if (pooled != null) return pooled.gameObject; // 池行可能随屏销毁，这里防御
                }
                var row = _makeOf?.Invoke(kind)?.Invoke();
                if (row == null) row = Node("row-miss", _content); // 工厂缺行兜底：绝不还 null 给引擎
                row.name = "row-p"; // 不与停靠模板撞名
                // 新建行预置尺寸：引擎量高（新建/回收均经 GetSize→GetPreferredHeight）常发生在
                // 首次布局前——行内布局组缓存 preferred=0 会盖过 sizeDelta 触发库断言。
                // 一律垫 LayoutElement（优先级最高）：网格=cellSize、变高列表=前缀和行高（与 sizeHelper 同口径）。
                if (_gridGeom != null)
                {
                    row.sizeDelta = _gridGeom.cellSize;
                    var le = row.GetComponent<LayoutElement>() ?? row.gameObject.AddComponent<LayoutElement>();
                    le.preferredWidth = _gridGeom.cellSize.x;
                    le.preferredHeight = _gridGeom.cellSize.y;
                }
                else if (_offsets != null && index >= 0 && index < _count)
                {
                    float h = _offsets[index + 1] - _offsets[index] - _spacing;
                    row.sizeDelta = new Vector2(row.sizeDelta.x, h);
                    var le = row.GetComponent<LayoutElement>() ?? row.gameObject.AddComponent<LayoutElement>();
                    le.preferredHeight = h;
                }
                _rowKind[row] = kind;
                return row.gameObject;
            }

            /// <summary>引擎还行（LoopScrollPrefabSource）：按行记录的 kind 回池；外来节点（模板等）不动。</summary>
            public void ReturnObject(Transform trans)
            {
                if (!(trans is RectTransform row) || !_rowKind.TryGetValue(row, out int kind)) return;
                row.gameObject.SetActive(false);
                row.SetParent(_park != null ? _park : _content, false); // 离开 content：引擎边界数学只认活行
                if (!_pools.TryGetValue(kind, out var pool)) _pools[kind] = pool = new List<RectTransform>();
                pool.Add(row);
            }

            /// <summary>引擎绑行（LoopScrollMultiDataSource）→ bind。</summary>
            public void ProvideData(Transform transform, int index) => _bind?.Invoke(transform as RectTransform, index);

            /// <summary>变高前缀和（库口径：纯条目高，不含 spacing）。</summary>
            private sealed class SizeHelper : UnityEngine.UI.LoopScrollSizeHelper
            {
                public RecycledList Owner;
                public float GetItemsSize(int itemStart, int itemEnd)
                {
                    var o = Owner;
                    if (o == null || o._offsets == null || o._count == 0) return 0f;
                    int a = Mathf.Clamp(itemStart, 0, o._count);
                    int b = Mathf.Clamp(itemEnd, a, o._count);
                    return o._offsets[b] - o._offsets[a] - o._spacing * (b - a);
                }
            }
        }

        // ================= 模态框 =================

        /// <summary>网格滚动区（卡组卡槽 wrap 网格等）：定宽格子纵向铺排+滚动。</summary>
        public static Scroll ScrollGrid(string name, RectTransform parent,
            float cellW, float cellH, float spacing = 4f, Color? bg = null)
        {
            var root = Node(name, parent);
            var img = root.gameObject.AddComponent<Image>();
            img.sprite = RoundedSprite;
            img.type = Image.Type.Sliced;
            img.color = bg ?? UiStyle.ListBg;
            img.raycastTarget = true;

            var scroll = root.gameObject.AddComponent<ScrollRect>();
            var viewport = Node("viewport", root);
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = Node("content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;

            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(cellW, cellH);
            grid.spacing = new Vector2(spacing, spacing);
            grid.padding = new RectOffset(4, 4, 4, 4);
            grid.constraint = GridLayoutGroup.Constraint.Flexible;
            grid.childAlignment = TextAnchor.UpperLeft;

            content.gameObject.AddComponent<ContentSizeFitter>()
                .verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;
            return new Scroll { Rect = scroll, Content = content };
        }

        /// <summary>模态弹窗（对标 .overlay/.overlay__panel）：半透明遮罩+居中面板。</summary>
        public sealed class Modal
        {
            public RectTransform Dim;   // 遮罩全屏容器
            public RectTransform Panel; // 内容面板（纵向布局）

            public void Close()
            {
                if (Dim == null) return;
                UnityEngine.Object.Destroy(Dim.gameObject);
                Dim = null;
                Panel = null;
            }
        }

        /// <summary>居中模态框：遮罩挡下层点击；面板高度自适应（指定 height 则固定）。</summary>
        public static Modal ModalBox(RectTransform overlay, string title = null,
            float width = 460f, float? height = null)
        {
            var dim = Overlay("modal", overlay);
            var dimImg = dim.gameObject.AddComponent<Image>();
            dimImg.color = UiStyle.OverlayDim;
            dimImg.raycastTarget = true;

            var panel = Node("panel", dim);
            var pimg = panel.gameObject.AddComponent<Image>();
            pimg.sprite = RoundedSprite;
            pimg.type = Image.Type.Sliced;
            pimg.color = UiStyle.PanelBg;
            var ol = panel.gameObject.AddComponent<Outline>();
            ol.effectColor = UiStyle.Border;
            ol.effectDistance = Vector2.one;
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(width, height ?? 0f);

            var g = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            g.spacing = 10f;
            g.padding = new RectOffset(16, 16, 16, 16);
            g.childAlignment = TextAnchor.UpperLeft;
            g.childControlWidth = true;   // 同 Configure 口径：control=true 读 preferred/flexible
            g.childControlHeight = true;
            if (height.HasValue == false)
                panel.gameObject.AddComponent<ContentSizeFitter>()
                    .verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            if (!string.IsNullOrEmpty(title))
                Size(Label("title", panel, title, 18, UiStyle.TextPrimary,
                    TextAnchor.LowerLeft, FontStyle.Bold), fw: 1f);

            return new Modal { Dim = dim, Panel = panel };
        }

        /// <summary>确认弹窗（2026-10-16 强制删除机制：删卡/删效果遇引用时提示+允许强制）：
        /// ModalBox 底座+右对齐按钮行。confirm=主行动（danger 默认红底）；cancel 只关闭
        /// （可选 onCancel 回调，如「保留旧版」走正面语义）。</summary>
        public static void ConfirmBox(RectTransform overlay, string title, string message,
            string confirmLabel, Action onConfirm,
            string cancelLabel = "取消", Action onCancel = null, bool danger = true)
        {
            var modal = ModalBox(overlay, title);
            Size(Label("msg", modal.Panel, message ?? "", UiStyle.BodySize, UiStyle.TextBody, wrap: true), fw: 1f);
            var bar = Row("bar", modal.Panel, spacing: 10f);
            bar.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleRight;
            Button("btn-cancel", bar, cancelLabel ?? "取消",
                () => { modal.Close(); onCancel?.Invoke(); });
            Button("btn-confirm", bar, confirmLabel ?? "确认",
                () => { modal.Close(); onConfirm?.Invoke(); },
                bg: danger ? UiStyle.BtnDanger : UiStyle.BtnPrimary, fg: UiStyle.White);
        }

        // ================= 下拉（自绘，替代 GenericDropdownMenu） =================

        /// <summary>
        /// 自绘下拉：头部按钮 + 弹层列表（弹层挂 Overlay 逃逸 ScrollRect 裁剪/布局）。
        /// 对标旧 USS .unity-generic-menu 覆写规则：min-width 280、限高 300 纵向滚动。
        /// </summary>
        public sealed class Dropdown
        {
            public RectTransform Root { get; }
            public int Index { get; private set; } = -1;
            public string Value { get; private set; } = "";

            private readonly List<string> _options = new List<string>();
            private readonly UButton _head;
            private readonly TMP_Dropdown _tmp;
            private readonly RectTransform _popupLayer;
            private readonly float? _fixedWidth;
            private GameObject _open;

            /// <summary>选中项变化（Index, Value）。</summary>
            public event Action<int, string> Changed;

            /// <summary>禁用/启用整个下拉（2026-10-09 引擎效果头锁定配套）：TMP 头=TMP_Dropdown.interactable；
            /// 自绘头=头部按钮 interactable（禁用后仍显示当前值，不可展开改选）。</summary>
            public bool Interactable
            {
                get => _tmp != null ? _tmp.interactable : _head != null && _head.interactable;
                set
                {
                    if (_tmp != null) _tmp.interactable = value;
                    else if (_head != null) _head.interactable = value;
                }
            }

            public Dropdown(string name, RectTransform parent, RectTransform popupLayer,
                IEnumerable<string> options, int index, Action<int, string> onChanged = null,
                float? width = null)
            {
                _popupLayer = popupLayer;
                _fixedWidth = width;
                _head = Button(name, parent, "", ToggleOpen, width: width, height: 30f);
                Root = (RectTransform)_head.transform;
                if (onChanged != null) Changed += onChanged;
                SetOptions(options, index);
            }

            /// <summary>绑定既有按钮为下拉头部（预制体屏：head 已烘焙，不再新建）。</summary>
            public Dropdown(UButton head, RectTransform popupLayer,
                IEnumerable<string> options, int index, Action<int, string> onChanged = null,
                float? width = null)
            {
                _popupLayer = popupLayer;
                _fixedWidth = width;
                _head = head;
                Root = (RectTransform)_head.transform;
                if (onChanged != null) Changed += onChanged;
                _head.onClick.AddListener(ToggleOpen);
                SetOptions(options, index);
            }

            /// <summary>驱动预制体烘焙的真 TMP_Dropdown（2026-10-03 手改预制体定案：下拉以
            /// TMP_Dropdown 形态挂在节点本体，选项/取值经本包装统一进出，弹层由 TMP 自理）。</summary>
            public Dropdown(TMP_Dropdown tmp, IEnumerable<string> options, int index,
                Action<int, string> onChanged = null)
            {
                _tmp = tmp;
                Root = (RectTransform)_tmp.transform;
                if (onChanged != null) Changed += onChanged;
                _tmp.onValueChanged.AddListener(OnTmpChanged);
                SetOptions(options, index);
            }

            private void OnTmpChanged(int i)
            {
                Index = i;
                Value = i >= 0 && i < _options.Count ? _options[i] : "";
                Debug.Log($"[UI点击] 下拉选择：{_tmp.name} → {Value}");
                Changed?.Invoke(Index, Value);
            }

            /// <summary>挂属性描述（两种头通用：TMP 头挂 TMP_Dropdown，自绘头挂头部按钮）。</summary>
            public void Describe(string text) =>
                Described(_tmp != null ? (Component)_tmp : (Component)_head, text);

            /// <summary>两段式描述（2026-10-05 定案：总述＋选项介绍；2026-10-16 悬浮化）：悬浮/按下头部＝总述一句话；
            /// 换选选项＝该项介绍（optionDesc 返回 null/空＝保持总述不覆盖）。</summary>
            public void Describe(string overview, Func<int, string> optionDesc)
            {
                Describe(overview);
                if (optionDesc == null) return;
                Changed += (i, _) =>
                {
                    var d = optionDesc(i);
                    if (!string.IsNullOrEmpty(d)) DescRequested?.Invoke(d);
                };
            }

            public void SetOptions(IEnumerable<string> options, int index, bool notify = false)
            {
                _options.Clear();
                _options.AddRange(options);
                if (_tmp != null)
                {
                    _tmp.ClearOptions();
                    _tmp.AddOptions(new List<string>(_options));
                }
                SetIndex(index, notify);
            }

            public void SetIndex(int index, bool notify = false)
            {
                if (_options.Count == 0)
                {
                    Index = -1;
                    Value = "";
                }
                else
                {
                    Index = Mathf.Clamp(index, 0, _options.Count - 1);
                    Value = _options[Index];
                }
                if (_tmp != null)
                {
                    _tmp.SetValueWithoutNotify(Index);
                    _tmp.RefreshShownValue();
                    if (notify) Changed?.Invoke(Index, Value);
                    return;
                }
                var label = _head.GetComponentInChildren<TMP_Text>();
                if (label != null)
                {
                    label.text = (Index >= 0 ? Value : "—") + " ▾";
                    // 未定宽时头部随文本自适应（最宽选项可能晚到，每次重取）——
                    // 滚动行内改 LE preferred，非滚动节点直写 rect（LE 策略见 Size）
                    var le = Root.GetComponent<LayoutElement>();
                    if (le != null && !_fixedWidth.HasValue)
                        le.preferredWidth = label.preferredWidth + 40f;
                    else if (!_fixedWidth.HasValue)
                        Root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, label.preferredWidth + 40f);
                }
                if (notify) Changed?.Invoke(Index, Value);
            }

            private void ToggleOpen()
            {
                if (_open != null) Close();
                else Open();
            }

            private void Open()
            {
                if (_options.Count == 0 || _popupLayer == null) return;
                Debug.Log($"[UI点击] 下拉展开：{(_head != null ? _head.name : "?")}");

                // 捕获层：全屏透明，点它=收起
                var catcher = Node("dropdown", _popupLayer);
                catcher.SetAsLastSibling();
                var catcherImg = catcher.gameObject.AddComponent<Image>();
                catcherImg.color = Color.clear; // raycastTarget 默认 true
                var catcherBtn = catcher.gameObject.AddComponent<UButton>();
                catcherBtn.transition = UnityEngine.UI.Selectable.Transition.None;
                catcherBtn.onClick.AddListener(Close);

                // 面板：圆角+描边，锚定头部左下角
                var panel = Node("panel", catcher);
                var panelImg = panel.gameObject.AddComponent<Image>();
                panelImg.sprite = RoundedSprite;
                panelImg.type = Image.Type.Sliced;
                panelImg.color = UiStyle.PanelBg;
                var ol = panel.gameObject.AddComponent<Outline>();
                ol.effectColor = UiStyle.Border;
                ol.effectDistance = Vector2.one;

                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _popupLayer,
                    RectTransformUtility.WorldToScreenPoint(null, Root.position),
                    null, out var local);
                panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0f, 1f);
                // local 是以弹层 pivot（居中）为原点的坐标；anchoredPosition 以锚参考点（层左上角）为基准，
                // 两者差半宽高——直接用 local 会把面板甩到屏幕左下角（2026-10-02 实测修正）
                var half = _popupLayer.rect.size * 0.5f;
                panel.anchoredPosition = new Vector2(local.x + half.x, local.y - half.y - 32f);
                const float itemH = 30f;
                float listW = Mathf.Max(280f, Root.rect.width + 48f);
                float listH = Mathf.Min(300f, _options.Count * (itemH + 4f) + 12f);
                panel.sizeDelta = new Vector2(listW, listH);

                var list = ScrollColumn("list", panel, spacing: 4f, pad: 6f);
                for (int i = 0; i < _options.Count; i++)
                {
                    int idx = i;
                    var opt = _options[i];
                    Button("item-" + i, list.Content, opt,
                        () => { Close(); SetIndex(idx, notify: true); },
                        bg: idx == Index ? UiStyle.SelectedRowBg : UiStyle.RowBg,
                        height: itemH, fontSize: 13);
                }

                _open = catcher.gameObject;
            }

            public void Close()
            {
                if (_open == null) return;
                UnityEngine.Object.Destroy(_open);
                _open = null;
            }
        }
    }

    /// <summary>TipPanel 提示弹窗宿主（2026-10-18 统一定案）：所有提示类弹窗一律复用
    /// TipPanel.prefab——美术换皮只改预制体一处全局生效；对局中选择目标卡仍走 Region 列卡
    ///（独立形态，明确不并入）。每屏一实例（ctor 注入 Root 惰性取值），同拍在途提示唯一
    ///（新提示顶替旧）。返回 true=btn-ok / false=btn-no / null=未点即清（拆屏 Close/顶替/
    /// 预制体缺失——调用方按取消口径处理）。</summary>
    public sealed class TipPromptHost
    {
        private readonly Func<RectTransform> _root;
        private GameObject _prefab; // 惰性加载（TipPanel.prefab，进程级句柄保活同 BattleScreen 口径）
        private GameObject _panel; // 在途面板实例
        private TaskCompletionSource<bool?> _tcs;

        public TipPromptHost(Func<RectTransform> root) => _root = root;

        /// <summary>弹提示：msg=正文（无独立标题位——文案自带语境），两键文案运行时填
        ///（noText=null 隐藏 btn-no 单键形态）。</summary>
        public async UniTask<bool?> ShowAsync(string msg, string okText, string noText = null)
        {
            if (_prefab == null)
                _prefab = HotUpdateAssets.Load<GameObject>("TipPanel", UiKit.PrefabPath("TipPanel"));
            var root = _root();
            if (_prefab == null || root == null)
            {
                Debug.LogWarning($"[TipPrompt] TipPanel.prefab 加载失败——提示弹窗不可用：{msg}");
                return null;
            }
            Close();
            var panel = UnityEngine.Object.Instantiate(_prefab, root, false);
            panel.name = "tip-prompt";
            var rt = panel.transform as RectTransform;
            var msgTmp = UiKit.FindDeep(rt, "msg")?.GetComponent<TMP_Text>();
            if (msgTmp != null) msgTmp.text = msg;
            var tcs = new TaskCompletionSource<bool?>();
            Bind(rt, "btn-ok", okText, true, tcs);
            var noBtn = UiKit.FindDeep(rt, "btn-no");
            if (noText == null) noBtn?.gameObject.SetActive(false); // 单键形态
            else Bind(rt, "btn-no", noText, false, tcs);
            _panel = panel;
            _tcs = tcs;
            return await tcs.Task;
        }

        /// <summary>关面板（点击收尾/拆屏 OnExit 清理/新提示顶替共用）：未点即关=结果置 null。</summary>
        public void Close()
        {
            if (_panel != null)
            {
                UnityEngine.Object.Destroy(_panel);
                _panel = null;
            }
            var tcs = _tcs;
            _tcs = null;
            tcs?.TrySetResult(null);
        }

        /// <summary>提示键接线：文案写按钮 label（TMP 子节点），点击=记结果并关面板。</summary>
        private void Bind(RectTransform panel, string btnName, string text, bool result,
            TaskCompletionSource<bool?> tcs)
        {
            var btnT = UiKit.FindDeep(panel, btnName);
            if (btnT == null) return;
            var lbl = btnT.GetComponentInChildren<TMP_Text>(true);
            if (lbl != null) lbl.text = text;
            var button = btnT.GetComponent<Button>();
            if (button != null)
                button.onClick.AddListener(() => { tcs.TrySetResult(result); Close(); });
        }
    }
}
