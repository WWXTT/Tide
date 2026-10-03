using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Tide.HotUpdate;
using UButton = UnityEngine.UI.Button;
using UInputField = TMPro.TMP_InputField;
using UToggle = UnityEngine.UI.Toggle;

namespace SynergyUI
{
    /// <summary>
    /// UGUI 控件工厂（2026-10-01 预制体化定案）：屏幕静态层级来自 Assets/Art/UI/ 预制体
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

        /// <summary>UI 预制体统一目录（YooAsset 收集器 UI 组 CollectPath 同址）。</summary>
        public const string PrefabFolder = "Assets/Art/UI/";

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

        // ================= 属性描述条（选中才出现） =================

        /// <summary>控件交互时的描述上报（EffectComposer 描述条订阅；静态事件——屏 OnEnter 挂/OnExit 摘）。</summary>
        public static event Action<string> DescRequested;

        /// <summary>描述挂载组件：随控件销毁自动释放（替代 UITK ConditionalWeakTable）。</summary>
        public sealed class DescTag : MonoBehaviour
        {
            public string Text;
        }

        /// <summary>给控件挂属性描述：交互（点击/输入/拖动）时经 DescRequested 上报显示。</summary>
        public static T Described<T>(T c, string desc) where T : Component
        {
            if (c == null || string.IsNullOrEmpty(desc)) return c;
            var tag = c.gameObject.AddComponent<DescTag>();
            tag.Text = desc;
            void Report() => DescRequested?.Invoke(tag.Text);
            switch (c)
            {
                case UButton b:
                    b.onClick.AddListener(Report);
                    break;
                case UInputField f:
                    f.onValueChanged.AddListener(_ => Report());
                    f.onEndEdit.AddListener(_ => Report());
                    break;
                case Slider s:
                    s.onValueChanged.AddListener(_ => Report());
                    break;
                case TMP_Dropdown d:
                    d.onValueChanged.AddListener(_ => Report());
                    break;
                // 自绘 Dropdown 头=Button（Button 分支即覆盖）；真 TMP_Dropdown 走上面独立分支
            }
            return c;
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

        // ================= 三角 sprite（箭头选择器） =================

        private static Sprite _triangle;

        /// <summary>实心三角（尖朝上，AA 边缘）——光环六向箭头选择器用。</summary>
        public static Sprite TriangleSprite =>
            _triangle != null ? _triangle : (_triangle = MakeTriangle(32));

        private static Sprite MakeTriangle(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            var px = new Color32[size * size];
            // 顶点（uv）：顶 (0.5,0.93)，底左 (0.07,0.1)，底右 (0.93,0.1)
            var a = new Vector2(0.5f, 0.93f);
            var b = new Vector2(0.07f, 0.10f);
            var c = new Vector2(0.93f, 0.10f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
                    // 三条边的符号距离（内正外负），取最小——1px 线性过渡抗锯齿
                    float d = Mathf.Min(Edge(p, a, b), Mathf.Min(Edge(p, b, c), Edge(p, c, a)));
                    float alpha = Mathf.Clamp01(0.5f - d * size);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));

            static float Edge(Vector2 p, Vector2 from, Vector2 to)
            {
                var n = new Vector2(to.y - from.y, from.x - to.x).normalized; // 内法线
                return Vector2.Dot(p - from, n);
            }
        }

        // ================= 字体 =================

        /// <summary>全局 TMP 字体资源路径（SC-Heavy SDF=思源宋体 Heavy 静态图集，3844 常用字形，
        /// 位于 TMP Resources 下可直取；源 otf 已移除——图集为静态模式，图集外字符渲染为空，
        /// 新增生僻文案需重烘焙图集）。</summary>
        public const string TmpFontResourcePath = "Fonts & Materials/SC-Heavy SDF";

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
}
