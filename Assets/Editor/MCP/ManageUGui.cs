using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace Tide.Editor.Mcp
{
    /// <summary>
    /// uGUI (Canvas) manipulation tool for MCP for Unity.
    /// Creates/edits Canvas hierarchies with layout groups, semantic anchor presets,
    /// persistent event listeners and inline Canvas screenshots for visual feedback.
    /// Registered into the "ui" tool group; discovered via [McpForUnityTool] reflection.
    /// </summary>
    [McpForUnityTool(
        "manage_ugui",
        Group = "ui",
        Description = "Create and edit uGUI (Canvas) hierarchies: elements (panel/button/text/image/inputfield/toggle/slider/dropdown/scrollview), semantic rect anchors, text styles, layout groups (vertical/horizontal/grid), persistent event listeners, hierarchy query, and inline Canvas screenshots for visual iteration.")]
    public static class ManageUGui
    {
        // ------------------------------------------------------------------
        // Command dispatch
        // ------------------------------------------------------------------

        /// <summary>Root of a prefab opened via LoadPrefabContents, when operating in prefab mode.</summary>
        private static GameObject _prefabRoot;

        public static object HandleCommand(JObject p)
        {
            string action = Str(p, "action")?.ToLowerInvariant();
            if (string.IsNullOrEmpty(action))
                return new ErrorResponse("'action' is required.");

            string prefabPath = Str(p, "prefab_path") ?? Str(p, "prefabPath");
            if (string.IsNullOrEmpty(prefabPath))
                return Dispatch(action, p);

            // Prefab mode: load the asset's contents, run the action against the
            // isolated hierarchy, then save back. Path lookups fall through to the
            // prefab root (see FindByPath).
            if (!System.IO.File.Exists(prefabPath))
                return new ErrorResponse($"prefab_path '{prefabPath}' does not exist.");
            if (action == "capture")
                return new ErrorResponse(
                    "'capture' targets scene objects; do not pass prefab_path (use instantiate to bring a prefab into the scene).");

            _prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                object result = Dispatch(action, p);
                PrefabUtility.SaveAsPrefabAsset(_prefabRoot, prefabPath);
                return result;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(_prefabRoot);
                _prefabRoot = null;
            }
        }

        private static object Dispatch(string action, JObject p)
        {
            try
            {
                switch (action)
                {
                    case "ping":
                        return new SuccessResponse("pong", new { tool = "manage_ugui" });

                    case "create_element":
                        return CreateElement(p);

                    case "set_rect":
                        return SetRect(p);

                    case "set_text":
                        return SetText(p);

                    case "set_style":
                        return SetStyle(p);

                    case "set_layout":
                        return SetLayout(p);

                    case "add_listener":
                        return AddListener(p);

                    case "remove_listener":
                        return RemoveListener(p);

                    case "get_hierarchy":
                        return GetHierarchy(p);

                    case "capture":
                        return Capture(p);

                    case "delete":
                        return DeleteElement(p);

                    case "rename":
                        return RenameElement(p);

                    case "remove_component":
                        return RemoveComponent(p);

                    case "instantiate":
                        return InstantiatePrefab(p);

                    case "invoke_button":
                        return InvokeButton(p);

                    default:
                        return new ErrorResponse(
                            $"Unknown action '{action}'. Valid: create_element, set_rect, set_text, set_style, " +
                            "set_layout, add_listener, remove_listener, get_hierarchy, capture, delete, rename, " +
                            "remove_component, instantiate, ping.");
                }
            }
            catch (Exception ex)
            {
                return new ErrorResponse(ex.Message, new { stackTrace = ex.StackTrace });
            }
        }

        // ------------------------------------------------------------------
        // create_element
        // ------------------------------------------------------------------

        private static readonly HashSet<string> ElementTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "canvas", "panel", "button", "text", "image", "rawimage", "inputfield",
            "dropdown", "toggle", "slider", "scrollview"
        };

        private static object CreateElement(JObject p)
        {
            // MCP 桥接层会把 snake_case 参数键统一转为 camelCase——一律双写读取保持两种拼写可用
            string type = (Str(p, "element_type") ?? Str(p, "elementType"))?.ToLowerInvariant();
            if (string.IsNullOrEmpty(type) || !ElementTypes.Contains(type))
                return new ErrorResponse(
                    $"element_type must be one of: {string.Join(", ", ElementTypes)}.");

            string name = Str(p, "name") ?? type;
            Transform parent = ResolveParent(p, type);

            GameObject go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "MCP Create " + name);
            if (parent != null)
                go.transform.SetParent(parent, false);

            RectTransform rt = (RectTransform)go.transform;

            // Sensible per-type defaults so the first render is already usable.
            switch (type)
            {
                case "canvas":
                    BuildCanvas(go);
                    break;

                case "panel":
                    go.AddComponent<Image>().color = new Color(0.13f, 0.13f, 0.16f, 0.92f);
                    ApplyAnchor(rt, "stretch");
                    break;

                case "button":
                    BuildButton(go, p);
                    break;

                case "text":
                    BuildText(go, p, "stretch");
                    break;

                case "image":
                    var img = go.AddComponent<Image>();
                    img.color = ParseColor(Str(p, "color"), Color.white);
                    ApplyAnchor(rt, "center");
                    rt.sizeDelta = new Vector2(Val(p, "width", 160f), Val(p, "height", 160f));
                    break;

                case "rawimage":
                    go.AddComponent<RawImage>().color = ParseColor(Str(p, "color"), Color.white);
                    ApplyAnchor(rt, "center");
                    rt.sizeDelta = new Vector2(Val(p, "width", 256f), Val(p, "height", 256f));
                    break;

                case "inputfield":
                    BuildInputField(go, p);
                    break;

                case "dropdown":
                    BuildDropdown(go, p);
                    break;

                case "toggle":
                    BuildToggle(go, p);
                    break;

                case "slider":
                    BuildSlider(go, p);
                    break;

                case "scrollview":
                    BuildScrollView(go, p);
                    break;
            }

            // Optional per-call overrides.
            string anchor = Str(p, "anchor");
            if (!string.IsNullOrEmpty(anchor) && type != "canvas" && type != "panel" && type != "text")
                ApplyAnchor(rt, anchor);
            if (p["width"] != null || p["height"] != null)
            {
                Vector2 sd = rt.sizeDelta;
                rt.sizeDelta = new Vector2(Val(p, "width", sd.x), Val(p, "height", sd.y));
            }
            Vector2? pos = ParseVec2(p["anchored_position"] ?? p["anchoredPosition"]);
            if (pos.HasValue && type != "canvas")
                rt.anchoredPosition = pos.Value;

            // 背景类元素需垫在既有子级之下（默认追加在最后会盖住兄弟）。
            if (Bool(p, "first_sibling", Bool(p, "firstSibling", false)))
                rt.SetAsFirstSibling();

            EditorUtility.SetDirty(go);
            return new SuccessResponse($"Created {type} '{GetPath(rt)}'.", new
            {
                path = GetPath(rt),
                type,
                rect = RectSummary(rt),
            });
        }

        /// <summary>模拟点击（与冒烟同事件路径：ExecuteEvents.pointerClickHandler）——
        /// Play 态导航取证用；按钮是否生效看运行时点击日志。</summary>
        private static object InvokeButton(JObject p)
        {
            RectTransform rt = FindRect(p);
            var btn = rt.GetComponentInChildren<UnityEngine.UI.Button>(true);
            if (btn == null)
                return new ErrorResponse($"'{GetPath(rt)}' has no Button component.");
            var ped = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(btn.gameObject, ped, ExecuteEvents.pointerClickHandler);
            return new SuccessResponse($"Invoked click on '{GetPath(rt)}'.", new { path = GetPath(rt) });
        }

        private static Transform ResolveParent(JObject p, string type)
        {
            string parentPath = Str(p, "parent_path") ?? Str(p, "parentPath");
            if (!string.IsNullOrEmpty(parentPath))
            {
                Transform t = FindByPath(parentPath);
                if (t == null)
                    throw new ArgumentException($"parent_path '{parentPath}' not found in hierarchy.");
                return t;
            }

            if (type == "canvas")
                return null;

            Canvas canvas = UnityEngine.Object.FindObjectsByType<Canvas>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .OrderBy(c => c.transform.GetSiblingIndex())
                .FirstOrDefault();
            if (canvas == null)
            {
                GameObject canvasGo = new GameObject("Canvas",
                    typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                Undo.RegisterCreatedObjectUndo(canvasGo, "MCP Create Canvas");
                BuildCanvas(canvasGo);
                EnsureEventSystem();
                canvas = canvasGo.GetComponent<Canvas>();
            }
            return canvas.transform;
        }

        private static void BuildCanvas(GameObject go)
        {
            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = go.GetComponent<CanvasScaler>() ?? go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            if (go.GetComponent<GraphicRaycaster>() == null)
                go.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();
        }

        private static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(
                    FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length > 0)
                return;

            GameObject esGo = new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem));
            Undo.RegisterCreatedObjectUndo(esGo, "MCP Create EventSystem");
            // Prefer the Input System module when the package is present;
            // StandaloneInputModule errors under "New" active input handling.
            Type isuim = Type.GetType(
                "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (isuim != null)
                esGo.AddComponent(isuim);
            else
                esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        private static void BuildButton(GameObject go, JObject p)
        {
            Image bg = go.AddComponent<Image>();
            bg.color = ParseColor(Str(p, "color"), new Color(0.16f, 0.19f, 0.27f, 1f));
            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = bg;

            GameObject label = new GameObject("Text", typeof(RectTransform));
            label.transform.SetParent(go.transform, false);
            BuildText(label, p, "stretch");
            var labelRt = (RectTransform)label.transform;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;

            RectTransform rt = (RectTransform)go.transform;
            ApplyAnchor(rt, "center");
            rt.sizeDelta = new Vector2(Val(p, "width", 280f), Val(p, "height", 72f));
        }

        private static TMP_Text BuildText(GameObject go, JObject p, string anchor)
        {
            TMP_Text text = go.AddComponent<TextMeshProUGUI>();
            text.text = Str(p, "text") ?? go.name;
            text.fontSize = Val(p, "font_size", 36f);
            text.color = ParseColor(Str(p, "color"), Color.white);
            text.alignment = ParseTextAnchor(Str(p, "alignment")) ?? TextAlignmentOptions.Center;
            ApplyAnchor((RectTransform)go.transform, anchor);
            return text;
        }

        private static TMP_Text AddTextComponent(GameObject go) =>
            go.AddComponent<TextMeshProUGUI>();

        private static void BuildInputField(GameObject go, JObject p)
        {
            Image bg = go.AddComponent<Image>();
            bg.color = new Color(0.09f, 0.09f, 0.12f, 1f);

            GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image));
            viewport.transform.SetParent(go.transform, false);
            var vpRt = (RectTransform)viewport.transform;
            ApplyAnchor(vpRt, "stretch");
            vpRt.offsetMin = Vector2.zero;
            vpRt.offsetMax = Vector2.zero;
            viewport.GetComponent<Image>().color = Color.clear;

            GameObject label = new GameObject("Text", typeof(RectTransform));
            label.transform.SetParent(viewport.transform, false);
            var labelRt = (RectTransform)label.transform;
            ApplyAnchor(labelRt, "stretch");
            labelRt.offsetMin = new Vector2(16f, 4f);
            labelRt.offsetMax = new Vector2(-16f, -4f);
            TMP_Text labelTxt = AddTextComponent(label);
            labelTxt.fontSize = 30;
            labelTxt.color = Color.white;
            labelTxt.alignment = TextAlignmentOptions.MidlineLeft;
            labelTxt.text = Str(p, "text") ?? string.Empty;
            labelTxt.raycastTarget = false;

            GameObject phGo = new GameObject("Placeholder", typeof(RectTransform));
            phGo.transform.SetParent(viewport.transform, false);
            var phRt = (RectTransform)phGo.transform;
            ApplyAnchor(phRt, "stretch");
            phRt.offsetMin = labelRt.offsetMin;
            phRt.offsetMax = labelRt.offsetMax;
            TMP_Text ph = AddTextComponent(phGo);
            ph.fontSize = 30;
            ph.fontStyle = FontStyles.Italic;
            ph.color = new Color(1f, 1f, 1f, 0.35f);
            ph.alignment = TextAlignmentOptions.MidlineLeft;
            ph.text = Str(p, "placeholder") ?? "Enter text...";
            ph.raycastTarget = false;

            TMP_InputField input = go.AddComponent<TMP_InputField>();
            input.textViewport = (RectTransform)viewport.transform;
            input.placeholder = ph;
            input.textComponent = labelTxt;

            RectTransform rt = (RectTransform)go.transform;
            ApplyAnchor(rt, "center");
            rt.sizeDelta = new Vector2(Val(p, "width", 420f), Val(p, "height", 64f));
        }

        private static void BuildDropdown(GameObject go, JObject p)
        {
            TMP_Dropdown dd = go.AddComponent<TMP_Dropdown>();

            Image bg = go.AddComponent<Image>();
            bg.color = new Color(0.13f, 0.15f, 0.2f, 1f);

            GameObject labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var labelRt = (RectTransform)labelGo.transform;
            ApplyAnchor(labelRt, "stretch");
            labelRt.offsetMin = new Vector2(16f, 6f);
            labelRt.offsetMax = new Vector2(-48f, -6f);
            TMP_Text label = AddTextComponent(labelGo);
            label.fontSize = 30;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.text = "Option A";
            dd.captionText = label;

            GameObject arrowGo = new GameObject("Arrow", typeof(RectTransform), typeof(Image));
            arrowGo.transform.SetParent(go.transform, false);
            var arrowRt = (RectTransform)arrowGo.transform;
            ApplyAnchor(arrowRt, "rightright");
            arrowRt.sizeDelta = new Vector2(28f, 28f);
            arrowRt.anchoredPosition = new Vector2(-16f, 0f);
            arrowGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.7f);

            GameObject template = BuildDropdownTemplate(go.transform, dd);
            dd.template = template.GetComponent<RectTransform>();

            RectTransform rt = (RectTransform)go.transform;
            ApplyAnchor(rt, "center");
            rt.sizeDelta = new Vector2(Val(p, "width", 360f), Val(p, "height", 64f));
        }

        private static GameObject BuildDropdownTemplate(Transform parent, TMP_Dropdown dd)
        {
            GameObject template = new GameObject("Template", typeof(RectTransform), typeof(Image));
            template.transform.SetParent(parent, false);
            template.SetActive(false);
            var tRt = (RectTransform)template.transform;
            ApplyAnchor(tRt, "top");
            tRt.pivot = new Vector2(0.5f, 1f);
            tRt.sizeDelta = new Vector2(0f, 180f);
            tRt.anchoredPosition = Vector2.zero;
            template.GetComponent<Image>().color = new Color(0.16f, 0.18f, 0.24f, 1f);

            GameObject scrollGo = new GameObject("Scroller", typeof(RectTransform), typeof(ScrollRect));
            scrollGo.transform.SetParent(template.transform, false);
            var sRt = (RectTransform)scrollGo.transform;
            ApplyAnchor(sRt, "stretch");
            sRt.offsetMin = new Vector2(4f, 4f);
            sRt.offsetMax = new Vector2(-4f, -4f);

            GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(scrollGo.transform, false);
            var vRt = (RectTransform)viewport.transform;
            ApplyAnchor(vRt, "stretch");
            vRt.offsetMin = Vector2.zero;
            vRt.offsetMax = Vector2.zero;
            viewport.GetComponent<Image>().color = Color.clear;

            GameObject content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            var cRt = (RectTransform)content.transform;
            ApplyAnchor(cRt, "stretch");
            cRt.pivot = new Vector2(0.5f, 1f);
            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.viewport = vRt;
            scroll.content = cRt;

            GameObject item = new GameObject("Item", typeof(RectTransform), typeof(Image));
            item.transform.SetParent(content.transform, false);
            var iRt = (RectTransform)item.transform;
            ApplyAnchor(iRt, "topstretch");
            iRt.sizeDelta = new Vector2(0f, 56f);
            item.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.08f);

            GameObject itemLabel = new GameObject("Item Label", typeof(RectTransform));
            itemLabel.transform.SetParent(item.transform, false);
            var ilRt = (RectTransform)itemLabel.transform;
            ApplyAnchor(ilRt, "stretch");
            ilRt.offsetMin = new Vector2(16f, 4f);
            ilRt.offsetMax = new Vector2(-16f, -4f);
            TMP_Text il = AddTextComponent(itemLabel);
            il.fontSize = 28;
            il.color = Color.white;
            il.alignment = TextAlignmentOptions.MidlineLeft;
            il.text = "Option A";
            dd.itemText = il;

            return template;
        }

        private static void BuildToggle(GameObject go, JObject p)
        {
            Toggle toggle = go.AddComponent<Toggle>();

            GameObject bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(go.transform, false);
            var bgRt = (RectTransform)bgGo.transform;
            ApplyAnchor(bgRt, "leftcenter");
            bgRt.sizeDelta = new Vector2(40f, 40f);
            bgRt.anchoredPosition = new Vector2(20f, 0f);
            bgGo.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.14f, 1f);

            GameObject checkGo = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
            checkGo.transform.SetParent(bgGo.transform, false);
            var ckRt = (RectTransform)checkGo.transform;
            ApplyAnchor(ckRt, "center");
            ckRt.sizeDelta = new Vector2(24f, 24f);
            checkGo.GetComponent<Image>().color = new Color(0.35f, 0.85f, 0.5f, 1f);

            GameObject labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var lbRt = (RectTransform)labelGo.transform;
            ApplyAnchor(lbRt, "stretch");
            lbRt.offsetMin = new Vector2(52f, 4f);
            lbRt.offsetMax = new Vector2(-8f, -4f);
            TMP_Text label = AddTextComponent(labelGo);
            label.fontSize = 30;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.text = Str(p, "text") ?? "Toggle";

            toggle.targetGraphic = bgGo.GetComponent<Image>();
            toggle.graphic = checkGo.GetComponent<Image>();

            RectTransform rt = (RectTransform)go.transform;
            ApplyAnchor(rt, "left");
            rt.sizeDelta = new Vector2(Val(p, "width", 260f), Val(p, "height", 48f));
        }

        private static void BuildSlider(GameObject go, JObject p)
        {
            Slider slider = go.AddComponent<Slider>();

            GameObject bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(go.transform, false);
            var bgRt = (RectTransform)bgGo.transform;
            ApplyAnchor(bgRt, "stretch");
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            bgGo.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.14f, 1f);

            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(go.transform, false);
            var faRt = (RectTransform)fillArea.transform;
            ApplyAnchor(faRt, "stretch");
            faRt.offsetMin = new Vector2(6f, 6f);
            faRt.offsetMax = new Vector2(-6f, -6f);

            GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            var flRt = (RectTransform)fill.transform;
            ApplyAnchor(flRt, "stretch");
            flRt.offsetMin = Vector2.zero;
            flRt.offsetMax = Vector2.zero;
            fill.GetComponent<Image>().color = new Color(0.35f, 0.55f, 0.95f, 1f);

            GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(go.transform, false);
            var haRt = (RectTransform)handleArea.transform;
            ApplyAnchor(haRt, "stretch");
            haRt.offsetMin = new Vector2(10f, 0f);
            haRt.offsetMax = new Vector2(-10f, 0f);

            GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(handleArea.transform, false);
            var hdRt = (RectTransform)handle.transform;
            ApplyAnchor(hdRt, "center");
            hdRt.sizeDelta = new Vector2(24f, 40f);
            handle.GetComponent<Image>().color = Color.white;

            slider.fillRect = flRt;
            slider.handleRect = hdRt;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.direction = Slider.Direction.LeftToRight;

            RectTransform rt = (RectTransform)go.transform;
            ApplyAnchor(rt, "center");
            rt.sizeDelta = new Vector2(Val(p, "width", 420f), Val(p, "height", 32f));
        }

        private static void BuildScrollView(GameObject go, JObject p)
        {
            Image bg = go.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.08f, 0.11f, 0.6f);

            GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(go.transform, false);
            var vpRt = (RectTransform)viewport.transform;
            ApplyAnchor(vpRt, "stretch");
            vpRt.offsetMin = Vector2.zero;
            vpRt.offsetMax = Vector2.zero;
            viewport.GetComponent<Image>().color = Color.clear;
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            GameObject content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            var ctRt = (RectTransform)content.transform;
            ApplyAnchor(ctRt, "topstretch");
            ctRt.pivot = new Vector2(0.5f, 1f);
            ctRt.sizeDelta = new Vector2(0f, 0f);

            ScrollRect scroll = go.AddComponent<ScrollRect>();
            scroll.viewport = vpRt;
            scroll.content = ctRt;
            scroll.horizontal = Bool(p, "horizontal", false);
            scroll.vertical = Bool(p, "vertical", true);

            RectTransform rt = (RectTransform)go.transform;
            ApplyAnchor(rt, "stretch");
        }

        // ------------------------------------------------------------------
        // set_rect — semantic anchors
        // ------------------------------------------------------------------

        private static readonly Dictionary<string, (Vector2 min, Vector2 max, Vector2 pivot, Vector2 pos)>
            AnchorPresets = new(StringComparer.OrdinalIgnoreCase)
        {
            { "topleft", (new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 0)) },
            { "top", (new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, 0)) },
            { "topright", (new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(0, 0)) },
            { "left", (new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0)) },
            { "leftcenter", (new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0)) },
            { "center", (new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0)) },
            { "right", (new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 0)) },
            { "rightright", (new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 0)) },
            { "bottomleft", (new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0)) },
            { "bottom", (new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 0)) },
            { "bottomright", (new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(0, 0)) },
            { "stretch", (new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(0, 0)) },
            { "stretch_all", (new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(0, 0)) },
            { "topstretch", (new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, 0)) },
            { "bottomstretch", (new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 0)) },
            { "leftstretch", (new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(0, 0)) },
            { "rightstretch", (new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(0, 0)) },
        };

        private static void ApplyAnchor(RectTransform rt, string preset)
        {
            if (!AnchorPresets.TryGetValue(preset, out var a))
                throw new ArgumentException(
                    $"Unknown anchor preset '{preset}'. Valid: {string.Join(", ", AnchorPresets.Keys)}.");
            rt.anchorMin = a.min;
            rt.anchorMax = a.max;
            rt.pivot = a.pivot;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.anchoredPosition = a.pos;
            rt.sizeDelta = preset.Contains("stretch")
                ? new Vector2(
                    Mathf.Approximately(a.min.x, a.max.x) ? rt.sizeDelta.x : 0f,
                    Mathf.Approximately(a.min.y, a.max.y) ? rt.sizeDelta.y : 0f)
                : rt.sizeDelta;
        }

        private static object SetRect(JObject p)
        {
            RectTransform rt = FindRect(p);
            Undo.RecordObject(rt, "MCP SetRect");

            string anchor = Str(p, "anchor");
            if (!string.IsNullOrEmpty(anchor))
                ApplyAnchor(rt, anchor);

            Vector2? pos = ParseVec2(p["anchored_position"] ?? p["anchoredPosition"]);
            if (pos.HasValue) rt.anchoredPosition = pos.Value;

            Vector2? size = ParseVec2(p["size_delta"] ?? p["sizeDelta"]);
            if (size.HasValue) rt.sizeDelta = size.Value;

            Vector2? offMin = ParseVec2(p["offset_min"] ?? p["offsetMin"]);
            if (offMin.HasValue) rt.offsetMin = offMin.Value;
            Vector2? offMax = ParseVec2(p["offset_max"] ?? p["offsetMax"]);
            if (offMax.HasValue) rt.offsetMax = offMax.Value;

            Vector2? pivot = ParseVec2(p["pivot"]);
            if (pivot.HasValue) rt.pivot = pivot.Value;

            EditorUtility.SetDirty(rt);
            return new SuccessResponse($"Rect updated on '{GetPath(rt)}'.", new { path = GetPath(rt), rect = RectSummary(rt) });
        }

        // ------------------------------------------------------------------
        // set_text
        // ------------------------------------------------------------------

        private static object SetText(JObject p)
        {
            RectTransform rt = FindRect(p);
            TMP_Text text = rt.GetComponent<TMP_Text>();
            if (text == null)
                return new ErrorResponse(
                    $"'{GetPath(rt)}' has no TextMeshProUGUI component.");

            Undo.RecordObject(text, "MCP SetText");
            if (p["text"] != null) text.text = Str(p, "text");
            if (p["font_size"] != null || p["fontSize"] != null)
                text.fontSize = Val(p, "font_size", text.fontSize);
            if (p["color"] != null)
                text.color = ParseColor(Str(p, "color"), text.color);
            if (p["alignment"] != null)
                text.alignment = ParseTextAnchor(Str(p, "alignment")) ?? text.alignment;
            if (p["bold"] != null)
                text.fontStyle = Bool(p, "bold", false)
                    ? text.fontStyle | FontStyles.Bold
                    : text.fontStyle & ~FontStyles.Bold;
            if (p["italic"] != null)
                text.fontStyle = Bool(p, "italic", false)
                    ? text.fontStyle | FontStyles.Italic
                    : text.fontStyle & ~FontStyles.Italic;

            EditorUtility.SetDirty(text);
            return new SuccessResponse($"Text updated on '{GetPath(rt)}'.",
                new { path = GetPath(rt), text = text.text, fontSize = text.fontSize, color = ToHex(text.color) });
        }

        // ------------------------------------------------------------------
        // set_style
        // ------------------------------------------------------------------

        private static object SetStyle(JObject p)
        {
            RectTransform rt = FindRect(p);
            Graphic graphic = rt.GetComponent<Graphic>();
            if (graphic == null)
                return new ErrorResponse($"'{GetPath(rt)}' has no Graphic (Image/Text) component.");

            Undo.RecordObject(graphic, "MCP SetStyle");
            if (p["color"] != null)
                graphic.color = ParseColor(Str(p, "color"), graphic.color);
            if (p["alpha"] != null)
            {
                Color c = graphic.color;
                c.a = Val(p, "alpha", c.a);
                graphic.color = c;
            }
            if (p["raycast_target"] != null || p["raycastTarget"] != null)
                graphic.raycastTarget = Bool(p, "raycast_target", graphic.raycastTarget);

            // TMP 文本专属：字号与加粗（排版还原用——autosize 开启时 fontSize 同时是上限基准）
            if (graphic is TMP_Text tmp)
            {
                if (p["font_size"] != null || p["fontSize"] != null)
                    tmp.fontSize = Val(p, "font_size", tmp.fontSize);
                if (p["font_bold"] != null || p["fontBold"] != null)
                {
                    bool bold = Bool(p, "font_bold", false);
                    tmp.fontStyle = bold
                        ? tmp.fontStyle | FontStyles.Bold
                        : tmp.fontStyle & ~FontStyles.Bold;
                }
            }

            if (graphic is Image image)
            {
                string spritePath = Str(p, "sprite_path") ?? Str(p, "spritePath");
                if (!string.IsNullOrEmpty(spritePath))
                {
                    Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                    if (sprite == null)
                        return new ErrorResponse($"Sprite not found at '{spritePath}'.");
                    image.sprite = sprite;
                }
                string itype = Str(p, "image_type") ?? Str(p, "imageType");
                if (!string.IsNullOrEmpty(itype))
                    image.type = itype.ToLowerInvariant() switch
                    {
                        "simple" => Image.Type.Simple,
                        "sliced" => Image.Type.Sliced,
                        "tiled" => Image.Type.Tiled,
                        "filled" => Image.Type.Filled,
                        _ => throw new ArgumentException($"Unknown image_type '{itype}'."),
                    };
                if (p["fill_amount"] != null)
                    image.fillAmount = Mathf.Clamp01(Val(p, "fill_amount", image.fillAmount));
            }

            EditorUtility.SetDirty(graphic);
            return new SuccessResponse($"Style updated on '{GetPath(rt)}'.",
                new { path = GetPath(rt), color = ToHex(graphic.color) });
        }

        // ------------------------------------------------------------------
        // set_layout
        // ------------------------------------------------------------------

        private static object SetLayout(JObject p)
        {
            RectTransform rt = FindRect(p);
            string layoutType = (Str(p, "layout_type") ?? Str(p, "layoutType") ?? "").ToLowerInvariant();
            if (string.IsNullOrEmpty(layoutType))
                return new ErrorResponse("layout_type is required (vertical|horizontal|grid|none).");

            // Remove existing layout group when switching or clearing.
            LayoutGroup existing = rt.GetComponent<LayoutGroup>();
            if (existing != null && (layoutType == "none" ||
                !existing.GetType().Name.ToLowerInvariant().Contains(layoutType)))
            {
                Undo.DestroyObjectImmediate(existing);
            }
            if (layoutType == "none")
                return new SuccessResponse($"Removed layout group from '{GetPath(rt)}'.");

            LayoutGroup group;
            if (layoutType == "grid")
            {
                GridLayoutGroup grid = rt.GetComponent<GridLayoutGroup>() ?? Undo.AddComponent<GridLayoutGroup>(rt.gameObject);
                grid.cellSize = ParseVec2(p["cell_size"] ?? p["cellSize"]) ?? grid.cellSize;
                grid.spacing = ParseVec2(p["spacing"]) ?? grid.spacing;
                group = grid;
            }
            else if (layoutType == "vertical" || layoutType == "horizontal")
            {
                HorizontalOrVerticalLayoutGroup hv =
                    layoutType == "vertical"
                        ? (VerticalLayoutGroup)rt.GetComponent<VerticalLayoutGroup>() ?? Undo.AddComponent<VerticalLayoutGroup>(rt.gameObject)
                        : (HorizontalLayoutGroup)rt.GetComponent<HorizontalLayoutGroup>() ?? Undo.AddComponent<HorizontalLayoutGroup>(rt.gameObject);
                hv.spacing = ParseVec2(p["spacing"])?.y ?? Val(p, "spacing", hv.spacing);
                hv.childForceExpandWidth = Bool(p, "child_force_expand_width", hv.childForceExpandWidth);
                hv.childForceExpandHeight = Bool(p, "child_force_expand_height", hv.childForceExpandHeight);
                hv.childControlWidth = Bool(p, "child_control_width", true);
                hv.childControlHeight = Bool(p, "child_control_height", true);
                group = hv;
            }
            else
            {
                return new ErrorResponse($"Unknown layout_type '{layoutType}'. Use vertical|horizontal|grid|none.");
            }

            Undo.RecordObject(group, "MCP SetLayout");
            string pad = Str(p, "padding");
            if (!string.IsNullOrEmpty(pad))
            {
                float[] v = ParseFloatArray(pad);
                if (v.Length == 4)
                    group.padding = new RectOffset(
                        Mathf.RoundToInt(v[0]), Mathf.RoundToInt(v[1]),
                        Mathf.RoundToInt(v[2]), Mathf.RoundToInt(v[3]));
                else if (v.Length == 1)
                    group.padding = new RectOffset(
                        Mathf.RoundToInt(v[0]), Mathf.RoundToInt(v[0]),
                        Mathf.RoundToInt(v[0]), Mathf.RoundToInt(v[0]));
            }
            TextAnchor? align = ParseUnityAnchor(Str(p, "alignment"));
            if (align.HasValue) group.childAlignment = align.Value;

            // Common: content auto-sizing for scroll-driven content.
            if (Bool(p, "content_size_fitter", false) && rt.GetComponent<ContentSizeFitter>() == null)
            {
                ContentSizeFitter fitter = Undo.AddComponent<ContentSizeFitter>(rt.gameObject);
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }

            EditorUtility.SetDirty(group);
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            return new SuccessResponse($"Layout '{layoutType}' applied to '{GetPath(rt)}'.",
                new { path = GetPath(rt), layoutType });
        }

        // ------------------------------------------------------------------
        // add_listener / remove_listener
        // ------------------------------------------------------------------

        private static object AddListener(JObject p)
        {
            RectTransform target = FindRect(p, "target_path");
            string componentType = Str(p, "component_type") ?? Str(p, "componentType") ?? "Button";
            string eventName = Str(p, "event_name") ?? Str(p, "eventName") ?? "onClick";

            Component source = target.GetComponent(componentType)
                ?? throw new ArgumentException($"Component '{componentType}' not found on '{GetPath(target)}'.");
            PropertyInfo evProp = source.GetType().GetProperty(eventName,
                BindingFlags.Public | BindingFlags.Instance)
                ?? throw new ArgumentException($"Event '{eventName}' not found on {componentType}.");
            UnityEventBase evt = evProp.GetValue(source) as UnityEventBase
                ?? throw new ArgumentException($"Property '{eventName}' is not a UnityEvent.");

            RectTransform listenerTarget = FindRect(p, "target_gameobject_path") ?? target;
            string listenerComponentType = Str(p, "target_component_type") ?? Str(p, "targetComponentType") ?? componentType;
            Component listenerComp = listenerTarget.GetComponent(listenerComponentType)
                ?? throw new ArgumentException(
                    $"Component '{listenerComponentType}' not found on '{GetPath(listenerTarget)}'.");
            string methodName = Str(p, "method_name") ?? Str(p, "methodName")
                ?? throw new ArgumentException("method_name is required.");
            MethodInfo method = listenerComp.GetType().GetMethod(methodName,
                BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            if (method == null)
                throw new ArgumentException(
                    $"Public parameterless method '{methodName}' not found on {listenerComponentType}.");

            Undo.RecordObject(source, "MCP AddListener");
            // Persistent listeners are serialized state; append via SerializedObject
            // because UnityEventTools overloads only accept compiled delegates.
            SerializedObject so = new SerializedObject(source);
            SerializedProperty calls = so.FindProperty($"{eventName}.m_PersistentCalls.m_Calls");
            if (calls == null)
                return new ErrorResponse(
                    $"Cannot serialize event '{eventName}' on {componentType}; bind at runtime instead.");
            int idx = calls.arraySize;
            calls.InsertArrayElementAtIndex(idx);
            SerializedProperty call = calls.GetArrayElementAtIndex(idx);
            call.FindPropertyRelative("m_CallState").enumValueIndex = 2; // RuntimeOnly
            call.FindPropertyRelative("m_Mode").intValue = 1;            // Void
            call.FindPropertyRelative("m_Target").objectReferenceValue = listenerComp;
            call.FindPropertyRelative("m_MethodName").stringValue = methodName;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(source);
            return new SuccessResponse(
                $"Bound {componentType}.{eventName} -> {listenerComponentType}.{methodName}().",
                new { path = GetPath(target), listenerIndex = idx });
        }

        private static object RemoveListener(JObject p)
        {
            RectTransform target = FindRect(p, "target_path");
            string componentType = Str(p, "component_type") ?? Str(p, "componentType") ?? "Button";
            string eventName = Str(p, "event_name") ?? Str(p, "eventName") ?? "onClick";
            int index = (int)Val(p, "listener_index", -1);

            Component source = target.GetComponent(componentType)
                ?? throw new ArgumentException($"Component '{componentType}' not found on '{GetPath(target)}'.");
            UnityEventBase evt = source.GetType().GetProperty(eventName,
                BindingFlags.Public | BindingFlags.Instance)?.GetValue(source) as UnityEventBase;
            if (evt == null)
                return new ErrorResponse($"Event '{eventName}' not found on {componentType}.");
            int count = evt.GetPersistentEventCount();
            if (count == 0)
                return new ErrorResponse("No persistent listeners to remove.");
            if (index < 0 || index >= count)
                return new ErrorResponse($"listener_index out of range 0..{count - 1}.");

            Undo.RecordObject(source, "MCP RemoveListener");
            UnityEventTools.RemovePersistentListener(evt, index);
            EditorUtility.SetDirty(source);
            return new SuccessResponse($"Removed listener {index} from {componentType}.{eventName}.");
        }

        private static int CountListeners(UnityEventBase evt) => evt.GetPersistentEventCount();

        // ------------------------------------------------------------------
        // get_hierarchy
        // ------------------------------------------------------------------

        private static object GetHierarchy(JObject p)
        {
            string canvasPath = Str(p, "canvas_path") ?? Str(p, "canvasPath");
            int maxDepth = (int)Val(p, "max_depth", 12);

            Canvas[] canvases;
            if (!string.IsNullOrEmpty(canvasPath))
            {
                Transform t = FindByPath(canvasPath)
                    ?? throw new ArgumentException($"canvas_path '{canvasPath}' not found.");
                Canvas c = t.GetComponent<Canvas>();
                if (c != null)
                {
                    canvases = new[] { c };
                }
                else
                {
                    // Canvas-stripped screen prefab roots are still valid roots:
                    // report the subtree with a synthesized entry instead of failing.
                    return new SuccessResponse(
                        $"Hierarchy of '{canvasPath}' (no Canvas — plain RectTransform root).",
                        new
                        {
                            canvases = new List<JObject>
                            {
                                new JObject
                                {
                                    ["path"] = GetPath(t),
                                    ["renderMode"] = "(none)",
                                    ["children"] = BuildChildTree(t, 0, maxDepth),
                                },
                            },
                        });
                }
            }
            else
            {
                canvases = UnityEngine.Object.FindObjectsByType<Canvas>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
            }

            var trees = canvases.Select(c => new JObject
            {
                ["path"] = GetPath(c.transform),
                ["renderMode"] = c.renderMode.ToString(),
                ["children"] = BuildChildTree(c.transform, 0, maxDepth),
            }).ToList();

            return new SuccessResponse(
                $"Canvas hierarchy ({canvases.Length} canvas(es)).",
                new { canvases = JToken.FromObject(trees) });
        }

        private static JArray BuildChildTree(Transform parent, int depth, int maxDepth)
        {
            var arr = new JArray();
            if (depth >= maxDepth) return arr;
            foreach (Transform child in parent)
            {
                var node = new JObject
                {
                    ["name"] = child.name,
                    ["active"] = child.gameObject.activeSelf,
                    ["rect"] = JObject.FromObject(RectSummary((RectTransform)child)),
                };
                TMP_Text text = child.GetComponent<TMP_Text>();
                if (text != null && !string.IsNullOrEmpty(text.text))
                    node["text"] = text.text.Length > 40 ? text.text.Substring(0, 40) + "..." : text.text;
                LayoutGroup lg = child.GetComponent<LayoutGroup>();
                if (lg != null)
                    node["layout"] = lg.GetType().Name;
                var children = BuildChildTree(child, depth + 1, maxDepth);
                if (children.Count > 0)
                    node["children"] = children;
                arr.Add(node);
            }
            return arr;
        }

        private static object RectSummary(RectTransform rt) => new
        {
            anchor = $"{rt.anchorMin.x:0.##},{rt.anchorMin.y:0.##}~{rt.anchorMax.x:0.##},{rt.anchorMax.y:0.##}",
            pos = $"{rt.anchoredPosition.x:0.#},{rt.anchoredPosition.y:0.#}",
            size = $"{rt.rect.width:0.#}x{rt.rect.height:0.#}",
        };

        // ------------------------------------------------------------------
        // capture — render a Canvas to an inline base64 PNG
        // ------------------------------------------------------------------

        private static object Capture(JObject p)
        {
            string canvasPath = Str(p, "canvas_path") ?? Str(p, "canvasPath");
            int maxWidth = (int)Val(p, "max_width", 768);
            Color bg = ParseColor(Str(p, "background"), new Color(0.1f, 0.1f, 0.12f, 1f));

            Canvas canvas;
            if (!string.IsNullOrEmpty(canvasPath))
            {
                Transform t = FindByPath(canvasPath)
                    ?? throw new ArgumentException($"canvas_path '{canvasPath}' not found.");
                canvas = t.GetComponent<Canvas>()
                    ?? throw new ArgumentException($"'{canvasPath}' has no Canvas component.");
            }
            else
            {
                canvas = UnityEngine.Object.FindObjectsByType<Canvas>(
                    FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                    .OrderByDescending(c => c.sortingOrder)
                    .FirstOrDefault()
                    ?? throw new InvalidOperationException("No Canvas found in the scene.");
            }

            RenderMode oldMode = canvas.renderMode;
            Camera oldCam = canvas.worldCamera;

            GameObject camGo = new GameObject("__ugui_capture__")
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            Camera cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = bg;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 1000f;
            cam.enabled = false;

            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            float oldPlane = canvas.planeDistance;
            canvas.planeDistance = 100f;

            // Isolate the Canvas from scene geometry: move its whole subtree to the
            // built-in UI layer and mask the camera to it, so 3D objects that happen
            // to sit inside the frustum cannot render over the UI.
            var layerBackup = new List<Transform>();
            foreach (Transform t in canvas.GetComponentsInChildren<Transform>(true))
            {
                layerBackup.Add(t);
            }
            int[] oldLayers = layerBackup.Select(t => t.gameObject.layer).ToArray();
            foreach (Transform t in layerBackup)
                t.gameObject.layer = LayerMask.NameToLayer("UI");
            cam.cullingMask = 1 << LayerMask.NameToLayer("UI");

            float oldScaleFactor = canvas.scaleFactor;
            try
            {
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                float refW = scaler != null ? scaler.referenceResolution.x : 1920f;
                float refH = scaler != null ? scaler.referenceResolution.y : 1080f;
                int w = Mathf.Clamp(maxWidth, 128, 1920);
                int h = Mathf.RoundToInt(w * refH / refW);

                // CanvasScaler never ticks outside Play mode; drive the canvas scale
                // manually so the reference-resolution layout maps onto the RT size.
                if (scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
                    canvas.scaleFactor = w / refW;

                RenderTexture rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
                // Refresh layout before shooting — outside Play mode neither the
                // Canvas scaler nor the layout groups tick, and a manual cam.Render()
                // skips the player loop entirely. Force the canvas system to sync
                // its ScreenSpaceCamera geometry, then rebuild layout, then sync
                // again: without this ordering the capture renders collapsed.
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(canvas.transform as RectTransform);
                Canvas.ForceUpdateCanvases();
                cam.targetTexture = rt;
                cam.Render();

                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = rt;
                Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;

                byte[] png = tex.EncodeToPNG();
                string base64 = Convert.ToBase64String(png);

                UnityEngine.Object.DestroyImmediate(tex);
                cam.targetTexture = null;
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);

                return new SuccessResponse(
                    $"Captured Canvas '{canvas.name}' ({w}x{h}).",
                    new { imageBase64 = base64, imageWidth = w, imageHeight = h, canvas = GetPath(canvas.transform) });
            }
            finally
            {
                for (int i = 0; i < layerBackup.Count; i++)
                    layerBackup[i].gameObject.layer = oldLayers[i];
                canvas.scaleFactor = oldScaleFactor;
                canvas.renderMode = oldMode;
                canvas.worldCamera = oldCam;
                canvas.planeDistance = oldPlane;
                UnityEngine.Object.DestroyImmediate(camGo);
            }
        }

        // ------------------------------------------------------------------
        // delete / rename
        // ------------------------------------------------------------------

        private static object DeleteElement(JObject p)
        {
            RectTransform rt = FindRect(p);
            if (rt.GetComponent<Canvas>() != null)
                return new ErrorResponse("Refusing to delete a Canvas; remove it manually if intended.");
            string path = GetPath(rt);
            Undo.DestroyObjectImmediate(rt.gameObject);
            return new SuccessResponse($"Deleted '{path}'.");
        }

        private static object RenameElement(JObject p)
        {
            RectTransform rt = FindRect(p);
            string newName = Str(p, "new_name") ?? Str(p, "newName")
                ?? throw new ArgumentException("new_name is required.");
            Undo.RecordObject(rt.gameObject, "MCP Rename");
            rt.name = newName;
            EditorUtility.SetDirty(rt.gameObject);
            return new SuccessResponse($"Renamed to '{GetPath(rt)}'.", new { path = GetPath(rt) });
        }

        private static object RemoveComponent(JObject p)
        {
            RectTransform rt = FindRect(p);
            string componentName = Str(p, "component_type") ?? Str(p, "componentType") ?? Str(p, "component")
                ?? throw new ArgumentException("component_type is required.");

            Component match = rt.GetComponents<Component>()
                .FirstOrDefault(c => c != null && c.GetType().Name.Equals(componentName, StringComparison.OrdinalIgnoreCase))
                ?? throw new ArgumentException(
                    $"Component '{componentName}' not found on '{GetPath(rt)}'. " +
                    $"Present: {string.Join(", ", rt.GetComponents<Component>().Where(c => c != null).Select(c => c.GetType().Name))}");
            if (match is RectTransform)
                return new ErrorResponse("Cannot remove RectTransform from a UI element.");

            // Undo teardown does not reliably reach components on LoadPrefabContents
            // hierarchies (Canvas survived undo-destroy); prefab mode destroys
            // directly — asset history is covered by version control anyway.
            if (_prefabRoot != null)
                UnityEngine.Object.DestroyImmediate(match);
            else
                Undo.DestroyObjectImmediate(match);
            EditorUtility.SetDirty(rt.gameObject);
            return new SuccessResponse($"Removed {componentName} from '{GetPath(rt)}'.");
        }

        private static object InstantiatePrefab(JObject p)
        {
            string path = Str(p, "prefab_path") ?? Str(p, "prefabPath")
                ?? throw new ArgumentException("prefab_path is required.");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                return new ErrorResponse($"No prefab found at '{path}'.");

            Transform parent = ResolveParent(p, "panel");
            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            inst.name = prefab.name; // drop "(Clone)" so FindByPath sees the canonical name
            Undo.RegisterCreatedObjectUndo(inst, "MCP Instantiate " + inst.name);
            RectTransform rt = (RectTransform)inst.transform;
            ApplyAnchor(rt, "stretch");
            // LayoutGroups only run in Play mode by default; force a rebuild so the
            // editor-state hierarchy (and capture) reflects the real layout.
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            return new SuccessResponse($"Instantiated '{path}' under '{GetPath(parent)}'.",
                new { path = GetPath(rt), prefab = path, rect = RectSummary(rt) });
        }

        // ------------------------------------------------------------------
        // Editor self-test (menu-driven; used when no MCP bridge is attached)
        // ------------------------------------------------------------------

        [MenuItem("Tools/MCP uGUI/Self Test")]
        private static void SelfTest()
        {
            var sb = new StringBuilder();
            try
            {
                sb.AppendLine("ping -> " + JObject.FromObject(HandleCommand(new JObject { ["action"] = "ping" })));
                sb.AppendLine("panel -> " + JObject.FromObject(HandleCommand(new JObject
                {
                    ["action"] = "create_element",
                    ["element_type"] = "panel",
                    ["name"] = "TestPanel",
                })));
                sb.AppendLine("text -> " + JObject.FromObject(HandleCommand(new JObject
                {
                    ["action"] = "create_element",
                    ["element_type"] = "text",
                    ["name"] = "Title",
                    ["text"] = "Tide Main Menu",
                    ["font_size"] = 64,
                    ["parent_path"] = "Canvas/TestPanel",
                })));
                sb.AppendLine("button -> " + JObject.FromObject(HandleCommand(new JObject
                {
                    ["action"] = "create_element",
                    ["element_type"] = "button",
                    ["name"] = "StartBtn",
                    ["text"] = "Start",
                    ["parent_path"] = "Canvas/TestPanel",
                    ["anchored_position"] = "0,-160",
                })));
                sb.AppendLine("hierarchy -> " + JObject.FromObject(HandleCommand(new JObject
                {
                    ["action"] = "get_hierarchy",
                    ["canvas_path"] = "Canvas",
                })));
                object cap = HandleCommand(new JObject { ["action"] = "capture", ["max_width"] = 640 });
                JObject cr = JObject.FromObject(cap);
                string b64 = (string)cr["data"]?["imageBase64"];
                if (!string.IsNullOrEmpty(b64))
                {
                    System.IO.File.WriteAllBytes("Temp/manage_ugui_test.png", Convert.FromBase64String(b64));
                    sb.AppendLine("capture -> Temp/manage_ugui_test.png (" + b64.Length + " chars)");
                }
                else
                {
                    sb.AppendLine("capture FAILED -> " + cr.ToString());
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine("EXCEPTION: " + ex);
            }
            System.IO.File.WriteAllText("Temp/manage_ugui_test.log", sb.ToString());
            Debug.Log("[ManageUGui] Self test finished. See Temp/manage_ugui_test.log");
        }

        // ------------------------------------------------------------------
        // Shared helpers
        // ------------------------------------------------------------------

        private static RectTransform FindRect(JObject p, string key = "target_path")
        {
            string path = Str(p, key) ?? Str(p, "targetPath") ?? Str(p, "path");
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException($"'{key}' is required.");
            Transform t = FindByPath(path)
                ?? throw new ArgumentException($"'{path}' not found in hierarchy.");
            return t as RectTransform
                ?? throw new ArgumentException($"'{path}' is not a UI element (no RectTransform).");
        }

        private static Transform FindByPath(string path)
        {
            string[] parts = path.Split('/');
            Transform current = null;

            // Prefab mode: the loaded prefab root participates in lookup by name.
            if (_prefabRoot != null && _prefabRoot.name == parts[0])
                current = _prefabRoot.transform;

            if (current == null)
            {
                foreach (Transform root in UnityEngine.SceneManagement.SceneManager
                    .GetActiveScene().GetRootGameObjects().Select(go => go.transform))
                {
                    if (root.name == parts[0])
                    {
                        current = root;
                        break;
                    }
                }
            }
            if (current == null) return null;

            for (int i = 1; i < parts.Length; i++)
            {
                current = current.Find(parts[i]);
                if (current == null) return null;
            }
            return current;
        }

        private static string GetPath(Transform t)
        {
            var sb = new StringBuilder(t.name);
            while (t.parent != null)
            {
                t = t.parent;
                sb.Insert(0, t.name + "/");
            }
            return sb.ToString();
        }

        private static string Str(JObject p, string key)
        {
            JToken tok = p[key];
            return tok?.Type == JTokenType.Null ? null : tok?.ToString();
        }

        private static float Val(JObject p, string key, float def)
        {
            JToken tok = p[key] ?? p[Camel(key)];
            if (tok == null) return def;
            if (tok.Type == JTokenType.Integer || tok.Type == JTokenType.Float)
                return tok.Value<float>();
            return float.TryParse(tok.ToString(), out float v) ? v : def;
        }

        private static bool Bool(JObject p, string key, bool def)
        {
            JToken tok = p[key] ?? p[Camel(key)];
            if (tok == null) return def;
            if (tok.Type == JTokenType.Boolean) return tok.Value<bool>();
            return bool.TryParse(tok.ToString(), out bool v) ? v : def;
        }

        private static string Camel(string snake) =>
            string.IsNullOrEmpty(snake) ? snake :
            snake.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries)
                .Select((s, i) => i == 0 ? s : char.ToUpperInvariant(s[0]) + s.Substring(1))
                .Aggregate(string.Concat);

        private static Vector2? ParseVec2(JToken tok)
        {
            if (tok == null) return null;
            string s = tok.ToString().Trim('(', ')', ' ');
            float[] v = ParseFloatArray(s);
            if (v.Length == 2) return new Vector2(v[0], v[1]);
            return null;
        }

        private static float[] ParseFloatArray(string s)
        {
            string[] parts = s.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var list = new List<float>();
            foreach (string part in parts)
                if (float.TryParse(part, out float v))
                    list.Add(v);
            return list.ToArray();
        }

        private static Color ParseColor(string s, Color def)
        {
            if (string.IsNullOrEmpty(s)) return def;
            s = s.Trim();
            if (s.StartsWith("#")) s = s.Substring(1);
            if (s.Length == 6) s += "FF";
            if (s.Length != 8
                || !uint.TryParse(s,
                    System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out uint hex))
                throw new ArgumentException($"Invalid color '{s}'. Use RRGGBB or RRGGBBAA hex.");
            return new Color(
                ((hex >> 24) & 0xFF) / 255f,
                ((hex >> 16) & 0xFF) / 255f,
                ((hex >> 8) & 0xFF) / 255f,
                (hex & 0xFF) / 255f);
        }

        private static string ToHex(Color c) =>
            $"#{Mathf.RoundToInt(c.r * 255):X2}{Mathf.RoundToInt(c.g * 255):X2}{Mathf.RoundToInt(c.b * 255):X2}{Mathf.RoundToInt(c.a * 255):X2}";

        private static TextAlignmentOptions? ParseTextAnchor(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            return s.ToLowerInvariant().Replace("-", "").Replace("_", "").Replace(" ", "") switch
            {
                "topleft" => TextAlignmentOptions.TopLeft,
                "top" or "topcenter" => TextAlignmentOptions.Top,
                "topright" => TextAlignmentOptions.TopRight,
                "left" or "midlineleft" => TextAlignmentOptions.Left,
                "center" or "middle" => TextAlignmentOptions.Center,
                "right" or "midlineright" => TextAlignmentOptions.Right,
                "bottomleft" => TextAlignmentOptions.BottomLeft,
                "bottom" or "bottomcenter" => TextAlignmentOptions.Bottom,
                "bottomright" => TextAlignmentOptions.BottomRight,
                _ => throw new ArgumentException($"Unknown alignment '{s}'."),
            };
        }

        private static TextAnchor? ParseUnityAnchor(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            return s.ToLowerInvariant().Replace("-", "").Replace("_", "").Replace(" ", "") switch
            {
                "topleft" => TextAnchor.UpperLeft,
                "top" or "topcenter" => TextAnchor.UpperCenter,
                "topright" => TextAnchor.UpperRight,
                "left" or "middleleft" => TextAnchor.MiddleLeft,
                "center" or "middle" => TextAnchor.MiddleCenter,
                "right" or "middleright" => TextAnchor.MiddleRight,
                "bottomleft" => TextAnchor.LowerLeft,
                "bottom" or "bottomcenter" => TextAnchor.LowerCenter,
                "bottomright" => TextAnchor.LowerRight,
                _ => throw new ArgumentException($"Unknown alignment '{s}'."),
            };
        }
    }
}
