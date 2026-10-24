using System;
using System.Linq;
using System.Reflection;
using SynergyUI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Tide.验证
{
    /// <summary>
    /// 效果合成器观感改版（2026-10-24）Play 数据断言——只走层级/字号/显隐证据，不做截图验收。
    /// 流程：进 Play → Show&lt;EffectComposerScreen&gt;（效果库模式）→
    /// ①新屏基线：保存/删除隐藏·顶栏高 152·行高 64·行宽=列宽·名称 18/描述 16·全屏无 btn-act；
    /// ②点原子行（整行点击=装载）→保存出现（并列 1 原子即合规）；
    /// ③切效果表→删除仍隐；点效果行载入→删除出现+保存仍显。
    /// 结束自动退 Play；全程统计 Error/Exception（应 0）。菜单触发，SessionState 旗标跨域重载。
    /// </summary>
    [InitializeOnLoad]
    public static class EffectComposerPlayAssert
    {
        private const string Menu = "Tools/UI/效果合成器Play断言";
        private const string Flag = "ECPlayAssert_Run";

        private static int _step;
        private static float _waitUntil;
        private static int _errCount;
        private static string _fail;
        private static bool _logging;

        static EffectComposerPlayAssert() => EditorApplication.playModeStateChanged += OnPlayMode;

        [MenuItem(Menu)]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
            { Debug.LogError("[ECPlay] 编辑器已在 Play（并行会话占用？）——不接管"); return; }
            SessionState.SetBool(Flag, true);
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayMode(PlayModeStateChange s)
        {
            if (s != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Flag, false)) return;
            SessionState.SetBool(Flag, false);
            _step = 0; _fail = null; _errCount = 0;
            Application.logMessageReceived += OnLog; _logging = true;
            EditorApplication.update += Tick;
        }

        private static void OnLog(string condition, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception) _errCount++;
        }

        private static UIManager Mgr()
        {
            var boot = UnityEngine.Object.FindFirstObjectByType<UIBootstrap>();
            if (boot == null) return null;
            var f = typeof(UIBootstrap).GetField("_manager", BindingFlags.NonPublic | BindingFlags.Instance);
            return f != null ? f.GetValue(boot) as UIManager : null;
        }

        private static void Tick()
        {
            try
            {
                switch (_step)
                {
                    case 0: // 等 UIBootstrap + 主菜单就绪
                        if (Time.realtimeSinceStartup > 25f) { Done("UIBootstrap/主菜单 25s 未就绪"); return; }
                        var mgr0 = Mgr();
                        if (mgr0 != null && mgr0.Current != null)
                        {
                            mgr0.Show<EffectComposerScreen>();
                            _waitUntil = Time.realtimeSinceStartup + 1.5f; _step = 1;
                        }
                        return;
                    case 1: // 进屏基线断言
                        if (Time.realtimeSinceStartup < _waitUntil) return;
                        if (!Baseline()) return;
                        _step = 2; return;
                    case 2: // 点第一条原子行（整行点击）
                        if (Time.realtimeSinceStartup < _waitUntil) return;
                        if (!ClickRow("list-library")) return;
                        _waitUntil = Time.realtimeSinceStartup + 0.8f; _step = 3; return;
                    case 3: // 保存应已出现（并列 1 原子=合规）
                        if (Time.realtimeSinceStartup < _waitUntil) return;
                        if (!Check("②装原子后保存出现", Visible("btn-save"))) return;
                        if (!Click("btn-switch-table")) return;
                        _waitUntil = Time.realtimeSinceStartup + 0.8f; _step = 4; return;
                    case 4: // 效果表已切、删除应仍隐；载入一条
                        if (Time.realtimeSinceStartup < _waitUntil) return;
                        if (!Check("③切表后删除仍隐", !Visible("btn-delete"))) return;
                        if (!ClickRow("list-effects")) return;
                        _waitUntil = Time.realtimeSinceStartup + 0.8f; _step = 5; return;
                    case 5: // 载入后：删除出现+保存仍显
                        if (Time.realtimeSinceStartup < _waitUntil) return;
                        if (!Check("③载入库效果后删除出现", Visible("btn-delete"))) return;
                        if (!Check("③载入后保存可见（载入档合规）", Visible("btn-save"))) return;
                        Done(null); return;
                }
            }
            catch (Exception e) { Done($"异常：{e}"); }
        }

        private static bool Baseline()
        {
            var screen = Current();
            if (!Check("①进屏：Current=EffectComposerScreen", screen != null)) return false;
            var root = screen.Root;
            if (!Check("①保存默认隐藏", !Visible("btn-save"))) return false;
            if (!Check("①删除默认隐藏", !Visible("btn-delete"))) return false;
            var topbar = Find(root, "topbar");
            if (!Check("①顶栏高=152", topbar != null && Mathf.Abs(topbar.rect.height - 152f) < 1f)) return false;
            var act = FindDeepAll(root, "btn-act");
            if (!Check("①全屏无 btn-act", act.Count == 0)) return false;

            var list = Find(root, "list-library");
            var sr = list != null ? list.GetComponent<ScrollRect>() : null;
            var content = sr != null ? sr.content : null;
            if (!Check("①原子库滚动区就绪", content != null)) return false;
            var row = content.Cast<Transform>()
                .FirstOrDefault(c => c.gameObject.activeSelf && c.GetComponent<Button>() != null) as RectTransform;
            if (!Check("①原子库有数据行", row != null)) return false;
            if (!Check($"①行高=64（实={row.rect.height:0.#}）", Mathf.Abs(row.rect.height - 64f) < 1f)) return false;
            var cvlg = content.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            float padX = cvlg != null ? cvlg.padding.horizontal : 0f;
            float avail = content.rect.width - padX; // 行宽=扣掉 content 内边距的可用列宽
            if (!Check($"①行宽=可用列宽（行={row.rect.width:0.#} 可用={avail:0.#}）",
                    Mathf.Abs(row.rect.width - avail) < 2f)) return false;
            var nameT = Find(row, "name")?.GetComponent<TMPro.TMP_Text>();
            var descT = Find(row, "desc")?.GetComponent<TMPro.TMP_Text>();
            if (!Check("①行名字号=18", nameT != null && Mathf.Abs(nameT.fontSize - 18f) < 0.5f)) return false;
            if (!Check("①行描述字号=16", descT != null && Mathf.Abs(descT.fontSize - 16f) < 0.5f)) return false;
            var tagRt = Find(row, "tag");
            var rc = new Vector3[4]; row.GetWorldCorners(rc);
            var tc = tagRt != null ? new Vector3[4] : null;
            if (tc != null) tagRt.GetWorldCorners(tc);
            float tagGap = tc != null ? rc[2].x - tc[2].x : -1f; // 行右缘-标签右缘（世界系）
            if (!Check($"①标签右对齐（右缘距行右={tagGap:0.#}，应 0~20）",
                    tagGap >= -0.5f && tagGap <= 20f)) return false;
            // 生效次数档位文案（2026-10-24 定案：-1=「无限」非「全部」）
            var takeGroup = Find(root, "take-effect");
            var ddLimit = takeGroup != null ? takeGroup.GetComponentInChildren<TMPro.TMP_Dropdown>(true) : null;
            var opts = ddLimit != null ? ddLimit.options.Select(o => o.text).ToList() : null;
            if (!Check("①生效次数档=[" + (opts != null ? string.Join("/", opts) : "缺") + "]（应 1/2/3/无限·无「全部」）",
                    opts != null && opts.Contains("无限") && !opts.Contains("全部") && opts.Count == 4)) return false;
            return true;
        }

        private static EffectComposerScreen Current() => Mgr()?.Current as EffectComposerScreen;

        private static RectTransform Root => Current()?.Root;

        private static bool Visible(string nodeName)
        {
            var root = Root;
            var n = root != null ? FindDeepAll(root, nodeName).FirstOrDefault() : null;
            return n != null && n.gameObject.activeInHierarchy;
        }

        private static bool Click(string nodeName)
        {
            var root = Root;
            var rt = root != null ? FindDeepAll(root, nodeName).FirstOrDefault() : null;
            var btn = rt != null ? rt.GetComponent<Button>() : null;
            if (btn == null) { Done($"缺可点节点 {nodeName}"); return false; }
            btn.onClick.Invoke();
            return true;
        }

        private static bool ClickRow(string listName)
        {
            var root = Root;
            var list = root != null ? Find(root, listName) : null;
            var sr = list != null ? list.GetComponent<ScrollRect>() : null;
            var content = sr != null ? sr.content : null;
            var row = content != null ? content.Cast<Transform>()
                .FirstOrDefault(c => c.gameObject.activeSelf && c.GetComponent<Button>() != null) : null;
            var btn = row != null ? row.GetComponent<Button>() : null;
            if (btn == null) { Done($"{listName} 无可点数据行"); return false; }
            btn.onClick.Invoke();
            return true;
        }

        private static RectTransform Find(RectTransform scope, string name) =>
            scope.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name) as RectTransform;

        private static System.Collections.Generic.List<RectTransform> FindDeepAll(RectTransform scope, string name) =>
            scope.GetComponentsInChildren<Transform>(true).Where(t => t.name == name)
                .Select(t => t as RectTransform).ToList();

        private static bool Check(string what, bool ok)
        {
            if (ok) Debug.Log($"[ECPlay] PASS {what}");
            else Done($"FAIL {what}");
            return ok;
        }

        private static void Done(string fail)
        {
            _fail = fail;
            Debug.Log(string.IsNullOrEmpty(_fail)
                ? $"[ECPlay] ===== ALL PASS（Error/Exception 计数={_errCount}，应 0）====="
                : $"[ECPlay] ===== 终止：{_fail}（Error/Exception 计数={_errCount}）=====");
            if (_logging) { Application.logMessageReceived -= OnLog; _logging = false; }
            EditorApplication.update -= Tick;
            EditorApplication.isPlaying = false; // 还原编辑器状态
        }
    }
}
