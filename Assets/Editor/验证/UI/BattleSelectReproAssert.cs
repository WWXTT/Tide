using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using CardCore;
using GameBoard;
using SynergyUI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Tide.验证
{
    /// <summary>
    /// 临时复现 harness（2026-11-03 Select 窗"不连锁后未彻底关闭"bug）：进 Play→木桩局→
    /// ①真·decision.ShowResponseAsync（合成速度2+选项）→真·btn-notchain onClick；
    /// ②再开窗→点 btn-hide 收起 viewport→OnNotChainClick；③下拉模式（类别三选一）→Cancel。
    /// 每轮快照 IsOpen/busy/viewport/btn-hide/btn-select/region/Dropdown/pending+全量日志捕获，
    /// 退出 Play 后报告落 SessionState（探针读取）。跑完本文件即删。
    /// </summary>
    [InitializeOnLoad]
    public static class BattleSelectReproAssert
    {
        private const string Flag = "SelRepro_Run";
        private const string ReportKey = "SelRepro_Report";
        private static int _step;
        private static float _waitUntil;
        private static readonly StringBuilder Sb = new StringBuilder();
        private static readonly List<string> Logs = new List<string>();
        private static readonly List<string> Fails = new List<string>();
        private static bool _regionActiveBefore;

        private static readonly BindingFlags SF = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly BindingFlags IF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        static BattleSelectReproAssert() => EditorApplication.playModeStateChanged += OnPlayMode;

        [MenuItem("Tools/UI/Select窗不连锁复现(临时)")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("[SelRepro] 已在 Play——不接管"); return; }
            SessionState.SetString(ReportKey, "");
            SessionState.SetBool(Flag, true);
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayMode(PlayModeStateChange s)
        {
            if (s != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Flag, false)) return;
            SessionState.SetBool(Flag, false);
            _step = 0; Sb.Clear(); Logs.Clear(); Fails.Clear();
            Application.logMessageReceived += OnLog;
            EditorApplication.update += Tick;
        }

        private static void OnLog(string condition, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception)
                Logs.Add("E:" + condition);
            else if (condition.Contains("响应窗口") || condition.Contains("RegionTarget") || condition.Contains("不连锁") || condition.Contains("SelRepro"))
                Logs.Add(type.ToString()[0] + ":" + condition);
        }

        private static UIManager Mgr()
        {
            var boot = UnityEngine.Object.FindFirstObjectByType<UIBootstrap>();
            if (boot == null) return null;
            var f = typeof(UIBootstrap).GetField("_manager", IF);
            return f != null ? f.GetValue(boot) as UIManager : null;
        }

        private static object Tss()
        {
            return typeof(GameCore).Assembly.GetTypes().First(t => t.Name == "TargetSelectionService");
        }

        private static object Sel() => TssT().GetProperty("Current", SF).GetValue(null);

        private static System.Type TssT() => typeof(GameCore).Assembly.GetTypes().First(t => t.Name == "TargetSelectionService");
        private static System.Type SelT() => typeof(GameCore).Assembly.GetTypes().First(t => t.Name == "RegionTargetSelector");
        private static System.Type BsT() => typeof(GameCore).Assembly.GetTypes().First(t => t.Name == "BattleScreen");
        private static System.Type HubT() => typeof(GameCore).Assembly.GetTypes().First(t => t.Name == "BattleDecisionHub");
        private static System.Type OptT() => typeof(GameCore).Assembly.GetTypes().First(t => t.Name == "ResponseOption");

        private static RectTransform SelectNode()
        {
            // 从活体选择器取 _region——场景根实例命名不确定（Clone 后缀/RootName 改名），反射最稳
            var sel = Sel();
            return sel == null ? null : SelT().GetField("_region", IF).GetValue(sel) as RectTransform;
        }

        private static string Snap(string tag)
        {
            var sel = Sel(); var node = SelectNode();
            var sb = new StringBuilder();
            sb.Append("[").Append(tag).Append("]");
            if (sel == null) { sb.Append(" sel=null"); return sb.ToString(); }
            var t = SelT();
            sb.Append(" IsOpen=").Append(t.GetProperty("IsOpen").GetValue(sel))
              .Append(" busy=").Append(t.GetField("_busy", IF).GetValue(sel))
              .Append(" pending=").Append(TssT().GetProperty("PendingRequests", SF).GetValue(null));
            if (node != null)
            {
                var vp = node.Find("viewport"); var bh = node.Find("btn-hide"); var bs = node.Find("btn-select"); var dd = node.Find("Dropdown");
                sb.Append(" region=").Append(node.gameObject.activeSelf)
                  .Append("/hier=").Append(node.gameObject.activeInHierarchy)
                  .Append(" viewport=").Append(vp != null && vp.gameObject.activeSelf)
                  .Append(" btnHide=").Append(bh != null && bh.gameObject.activeSelf)
                  .Append(" btnSelect=").Append(bs != null && bs.gameObject.activeSelf)
                  .Append(" dropdown=").Append(dd != null && dd.gameObject.activeSelf)
                  .Append(" viewHidden=").Append(t.GetProperty("ViewportHidden").GetValue(sel));
            }
            else sb.Append(" node=null");
            return sb.ToString();
        }

        private static object Hub()
        {
            var mgr = Mgr();
            if (mgr == null) return null;
            var cur = mgr.GetType().GetProperty("Current").GetValue(mgr);
            return cur != null ? BsT().GetField("_decision", IF).GetValue(cur) : null;
        }

        // ---- R4 Information（2026-11-03 牌组/手牌/标题/卡背改版）----
        private static object _r4Bs, _r4P1;

        private static object InfoCtl()
        {
            var mgr = Mgr();
            var cur = mgr != null ? mgr.GetType().GetProperty("Current").GetValue(mgr) : null;
            return cur != null ? BsT().GetField("_infoCtl", IF).GetValue(cur) : null;
        }

        private static RectTransform InfoNode()
        {
            var ctl = InfoCtl();
            return ctl != null ? ctl.GetType().GetField("_node", IF).GetValue(ctl) as RectTransform : null;
        }

        private static string InfoSnap(string tag)
        {
            var sb = new StringBuilder();
            sb.Append("[").Append(tag).Append("]");
            var ctl = InfoCtl();
            if (ctl == null) { sb.Append(" infoCtl=null"); return sb.ToString(); }
            sb.Append(" open=").Append(ctl.GetType().GetProperty("IsOpen").GetValue(ctl));
            var node = InfoNode();
            if (node == null) { sb.Append(" node=null"); return sb.ToString(); }
            var title = node.Find("label-title");
            var tmp = title != null ? title.GetComponentInChildren<TMPro.TMP_Text>(true) : null;
            sb.Append(" title=").Append(tmp != null ? tmp.text : "null");
            var content = node.Find("viewport/content");
            int shown = 0;
            if (content != null)
                for (int i = 0; i < content.childCount; i++)
                    if (content.GetChild(i).gameObject.activeSelf) shown++;
            int faces = 0, backs = 0;
            if (content != null)
                foreach (var cv in content.GetComponentsInChildren<ACardView>(true))
                {
                    if (!cv.gameObject.activeSelf) continue;
                    if ((bool)typeof(ACardView).GetField("_faceUp", IF).GetValue(cv)) faces++;
                    else backs++;
                }
            sb.Append(" shown=").Append(shown).Append(" faceUp=").Append(faces).Append(" backs=").Append(backs);
            return sb.ToString();
        }

        private static void FireResponseWindow()
        {
            var hub = Hub();
            var core = GameCore.Instance;
            var p1 = core.GetType().GetProperty("Player1").GetValue(core);
            var optT = OptT();
            var kindT = optT.GetField("Kind").FieldType;
            var kindVal = Enum.GetValues(kindT).Cast<object>().First(v => v.ToString() != "GuardAbility");
            var list = Activator.CreateInstance(typeof(List<>).MakeGenericType(optT));
            var add = list.GetType().GetMethod("Add");
            foreach (var lbl in new[] { "响应卡甲", "响应卡乙" })
            {
                var o = Activator.CreateInstance(optT);
                optT.GetField("Kind").SetValue(o, kindVal);
                var lp = optT.GetProperty("Label");
                if (lp != null) lp.SetValue(o, lbl);
                else optT.GetField("Label").SetValue(o, lbl);
                add.Invoke(list, new[] { o });
            }
            HubT().GetMethod("ShowResponseAsync").Invoke(hub, new[] { p1, list });
            Debug.Log("[SelRepro] ShowResponseAsync 已发射 kind=" + kindVal);
        }

        private static void FireChoiceWindow()
        {
            var t = SelT();
            var labels = new List<string> { "类别：生物", "类别：法术", "类别：结界" };
            t.GetMethod("SelectIndicesAsync").Invoke(Sel(), new object[] { labels, 1, 1, "预言", "hint", false, 30f });
            Debug.Log("[SelRepro] 下拉窗已发射");
        }

        private static void NotChainClick()
        {
            var btn = FindSceneButton("btn-notchain");
            if (btn != null) { btn.onClick.Invoke(); Sb.Append("\n[已点击] btn-notchain(真按钮)"); return; }
            var hub = Hub();
            if (hub != null)
            {
                HubT().GetMethod("OnNotChainClick").Invoke(hub, null);
                Sb.Append("\n[已调用] OnNotChainClick(回退——真按钮不可见)");
            }
            else Sb.Append("\n[点击失败] btn-notchain 与决策中枢均不可达（Mgr/Hub null）");
        }

        /// <summary>场景内按钮查找（含未激活——GameObject.Find 只找激活态）。</summary>
        private static Button FindSceneButton(string name)
        {
            var go = GameObject.Find(name);
            if (go != null) return go.GetComponent<Button>();
            foreach (var b in Resources.FindObjectsOfTypeAll<Button>())
                if (b.name == name && b.gameObject.scene.IsValid() && b.gameObject.scene.isLoaded)
                    return b;
            return null;
        }

        private static void ClickSelectChild(string child)
        {
            var node = SelectNode();
            var c = node != null ? node.Find(child) : null;
            var btn = c != null ? c.GetComponent<Button>() : null;
            if (btn != null) { btn.onClick.Invoke(); Sb.Append("\n[已点击] Select/").Append(child); }
            else Sb.Append("\n[点击失败] Select/").Append(child);
        }

        private static void Assert(bool ok, string what)
        {
            if (!ok) Fails.Add(what);
            Sb.Append("\n  断言 ").Append(ok ? "PASS " : "FAIL ").Append(what);
        }

        private static void Tick()
        {
            try { TickInner(); }
            catch (Exception e)
            {
                Sb.Append("\n[EXCEPTION@step").Append(_step).Append("] ").Append(e);
                Done();
            }
        }

        private static void TickInner()
        {
            switch (_step)
            {
                case 0: // 等 UIBootstrap → 配木桩局 → 进战斗屏
                    if (Time.realtimeSinceStartup > 30f) { Done("UIBootstrap 30s 未就绪"); return; }
                    var mgr = Mgr();
                    if (mgr == null || mgr.Current == null) return;
                    var entry = typeof(GameCore).Assembly.GetTypes().First(t => t.Name == "BattleEntry");
                    var modeT = entry.GetField("Mode").FieldType;
                    entry.GetField("Mode").SetValue(null, Enum.Parse(modeT, "LocalAI"));
                    entry.GetField("DummyTest").SetValue(null, true);
                    entry.GetField("StagedOpening").SetValue(null, false);
                    entry.GetField("SkipIntro").SetValue(null, true);
                    entry.GetField("DummyPlayerFirst").SetValue(null, true);
                    mgr.GetType().GetMethods()
                       .First(m => m.Name == "Show" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0)
                       .MakeGenericMethod(BsT()).Invoke(mgr, null);
                    _waitUntil = Time.realtimeSinceStartup + 40f; _step = 1;
                    return;
                case 1: // 等选择器注册（WireLocalUi 完成）
                    if (Time.realtimeSinceStartup > _waitUntil) { Done("战斗屏 40s 未就绪"); return; }
                    if (Sel() == null) return;
                    _waitUntil = Time.realtimeSinceStartup + 2f; _step = 2;
                    return;
                case 2: // 基线 → R1 发射响应窗
                    if (Time.realtimeSinceStartup < _waitUntil) return;
                    var node0 = SelectNode();
                    _regionActiveBefore = node0 == null || node0.gameObject.activeSelf;
                    Sb.Append("\n").Append(Snap("R1基线"));
                    FireResponseWindow();
                    _waitUntil = Time.realtimeSinceStartup + 1.5f; _step = 3;
                    return;
                case 3: // R1 窗开快照 → 不连锁
                    if (Time.realtimeSinceStartup < _waitUntil) return;
                    Sb.Append("\n").Append(Snap("R1窗开"));
                    NotChainClick();
                    _waitUntil = Time.realtimeSinceStartup + 1.5f; _step = 4;
                    return;
                case 4: // R1 收口断言
                    if (Time.realtimeSinceStartup < _waitUntil) return;
                    Sb.Append("\n").Append(Snap("R1不连锁后"));
                    Assert(SelT().GetProperty("IsOpen").GetValue(Sel()).Equals(false), "R1 IsOpen=false");
                    Assert(SelT().GetField("_busy", IF).GetValue(Sel()).Equals(false), "R1 busy=false");
                    var n4 = SelectNode();
                    Assert(n4 != null && n4.Find("btn-hide") != null && !n4.Find("btn-hide").gameObject.activeSelf, "R1 btn-hide已收");
                    Assert(n4 != null && n4.Find("viewport") != null && n4.Find("viewport").gameObject.activeSelf, "R1 viewport还原显示");
                    Assert(n4 != null && n4.gameObject.activeSelf == _regionActiveBefore, "R1 region还原基线");
                    _waitUntil = Time.realtimeSinceStartup + 1f; _step = 5;
                    return;
                case 5: // R2 发射
                    if (Time.realtimeSinceStartup < _waitUntil) return;
                    FireResponseWindow();
                    _waitUntil = Time.realtimeSinceStartup + 1.5f; _step = 6;
                    return;
                case 6: // R2 窗开 → 点 btn-hide 收起
                    if (Time.realtimeSinceStartup < _waitUntil) return;
                    Sb.Append("\n").Append(Snap("R2窗开"));
                    ClickSelectChild("btn-hide");
                    _waitUntil = Time.realtimeSinceStartup + 0.6f; _step = 7;
                    return;
                case 7: // R2 收起快照 → 不连锁
                    if (Time.realtimeSinceStartup < _waitUntil) return;
                    Sb.Append("\n").Append(Snap("R2收起"));
                    Assert(SelT().GetProperty("ViewportHidden").GetValue(Sel()).Equals(true), "R2 ViewportHidden=true");
                    NotChainClick();
                    _waitUntil = Time.realtimeSinceStartup + 1.5f; _step = 8;
                    return;
                case 8: // R2 收口断言
                    if (Time.realtimeSinceStartup < _waitUntil) return;
                    Sb.Append("\n").Append(Snap("R2不连锁后"));
                    Assert(SelT().GetProperty("IsOpen").GetValue(Sel()).Equals(false), "R2 IsOpen=false");
                    Assert(SelT().GetProperty("ViewportHidden").GetValue(Sel()).Equals(false), "R2 ViewportHidden=false");
                    var n8 = SelectNode();
                    Assert(n8 != null && n8.Find("viewport") != null && n8.Find("viewport").gameObject.activeSelf, "R2 viewport还原显示");
                    Assert(n8 != null && n8.Find("btn-hide") != null && !n8.Find("btn-hide").gameObject.activeSelf, "R2 btn-hide已收");
                    _waitUntil = Time.realtimeSinceStartup + 1f; _step = 9;
                    return;
                case 9: // R3 下拉窗
                    if (Time.realtimeSinceStartup < _waitUntil) return;
                    FireChoiceWindow();
                    _waitUntil = Time.realtimeSinceStartup + 1.5f; _step = 10;
                    return;
                case 10: // 下拉开快照 → Cancel
                    if (Time.realtimeSinceStartup < _waitUntil) return;
                    Sb.Append("\n").Append(Snap("R3下拉开"));
                    Assert(SelT().GetProperty("IsOpen").GetValue(Sel()).Equals(true), "R3 IsOpen=true(下拉占用)");
                    var n10 = SelectNode();
                    Assert(n10 != null && n10.Find("Dropdown") != null && n10.Find("Dropdown").gameObject.activeSelf, "R3 Dropdown显示");
                    SelT().GetMethod("Cancel").Invoke(Sel(), null);
                    _waitUntil = Time.realtimeSinceStartup + 1.5f; _step = 11;
                    return;
                case 11: // R3 收口 → 转 R4 信息窗
                    if (Time.realtimeSinceStartup < _waitUntil) return;
                    Sb.Append("\n").Append(Snap("R3取消后"));
                    Assert(SelT().GetProperty("IsOpen").GetValue(Sel()).Equals(false), "R3 IsOpen=false");
                    var n11 = SelectNode();
                    Assert(n11 != null && n11.Find("Dropdown") != null && !n11.Find("Dropdown").gameObject.activeSelf, "R3 Dropdown已收");
                    _waitUntil = Time.realtimeSinceStartup + 1f; _step = 12;
                    return;
                case 12: // R4-1 墓地（反射直调屏侧私有开窗口）
                    if (Time.realtimeSinceStartup < _waitUntil) return;
                    var mgr12 = Mgr();
                    _r4Bs = mgr12.GetType().GetProperty("Current").GetValue(mgr12);
                    var core12 = GameCore.Instance;
                    _r4P1 = core12.GetType().GetProperty("Player1").GetValue(core12);
                    BsT().GetMethod("OpenInformation", IF).Invoke(_r4Bs, new object[] { Zone.Graveyard, _r4P1 });
                    _waitUntil = Time.realtimeSinceStartup + 0.8f; _step = 13;
                    return;
                case 13: // R4-1 快照 → 转 R4-2 牌组
                    if (Time.realtimeSinceStartup < _waitUntil) return;
                    Sb.Append("\n").Append(InfoSnap("R4墓地"));
                    BsT().GetMethod("OpenInformation", IF).Invoke(_r4Bs, new object[] { Zone.Deck, _r4P1 });
                    _waitUntil = Time.realtimeSinceStartup + 0.8f; _step = 14;
                    return;
                case 14: // R4-2 牌组快照（应全卡背）→ 转 R4-3 地牌格
                    if (Time.realtimeSinceStartup < _waitUntil) return;
                    Sb.Append("\n").Append(InfoSnap("R4牌组"));
                    var cell14 = BoardLayout.LandCells(0)[0];
                    BsT().GetMethod("OpenInformationLands", IF).Invoke(_r4Bs, new object[] { 0, cell14.x, cell14.z });
                    _waitUntil = Time.realtimeSinceStartup + 0.8f; _step = 15;
                    return;
                case 15: // R4-3 地牌格标题断言 → btn-hide 关闭
                    if (Time.realtimeSinceStartup < _waitUntil) return;
                    Sb.Append("\n").Append(InfoSnap("R4地牌格"));
                    var info15 = InfoNode();
                    var ttl15 = info15 != null && info15.Find("label-title") != null
                        ? info15.Find("label-title").GetComponentInChildren<TMPro.TMP_Text>(true) : null;
                    Assert(ttl15 != null && ttl15.text.Contains("己方元素池") && ttl15.text.Contains("色地牌格"),
                        "R4 地牌格标题=己方元素池X色地牌格（实际=" + (ttl15 != null ? ttl15.text : "null") + "）");
                    var ib = info15 != null ? info15.Find("btn-hide") : null;
                    var ibtn = ib != null ? ib.GetComponent<Button>() : null;
                    if (ibtn != null) { ibtn.onClick.Invoke(); Sb.Append("\n[已点击] Information/btn-hide"); }
                    _waitUntil = Time.realtimeSinceStartup + 0.5f; _step = 16;
                    return;
                case 16: // 信息窗关闭断言 → 收口
                    if (Time.realtimeSinceStartup < _waitUntil) return;
                    var infoCtl16 = InfoCtl();
                    Assert(infoCtl16 == null || !((bool)infoCtl16.GetType().GetProperty("IsOpen").GetValue(infoCtl16)), "R4 Information已关");
                    Done();
                    return;
            }
        }

        private static void Done(string fail = null)
        {
            if (fail != null) Sb.Append("\n[FAIL] ").Append(fail);
            Sb.Append("\n[汇总] ").Append(Fails.Count == 0 ? "全过" : string.Join("；", Fails));
            Sb.Append("\n[日志] ").Append(string.Join(" | ", Logs.Take(60)));
            SessionState.SetString(ReportKey, Sb.ToString());
            Debug.Log("[SelRepro] 报告已落 SessionState 长度=" + Sb.Length);
            Application.logMessageReceived -= OnLog;
            EditorApplication.update -= Tick;
            EditorApplication.isPlaying = false;
        }
    }
}
