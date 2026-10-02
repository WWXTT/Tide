using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace Tide.验证
{
    /// <summary>
    /// UGUI 匹配屏冒烟（2026-10-01 UITK→UGUI 迁移 Phase 1 验证工具）：
    /// 主菜单 → 联机大厅 → 连接（本机 Play 自动起的大厅）→ 创建房间 → +AI 填位 →
    /// 自动提交卡组 → Manifest 移交对战界面（当前为 stub 屏）。每步读 toast/状态文本断言。
    /// 用法：进 Play 后执行菜单 Tools/验证/UI匹配冒烟，读 Console 输出。
    /// </summary>
    public static class UguiMatchSmoke
    {
        private const string MenuPath = "Tools/验证/UI匹配冒烟";

        [MenuItem(MenuPath)]
        public static async void Run()
        {
            // 比赛禁联机（NetGate，2026-10-01）：闸关时大厅不自启、入口已屏蔽——联机冒烟整体跳过
            if (!CardCore.Network.NetGate.OnlineEnabled)
            {
                Debug.LogWarning("[UguiMatchSmoke][SKIP] NetGate 关闭（比赛禁联机）——联机流程冒烟跳过");
                return;
            }

            var sb = new StringBuilder("[UguiMatchSmoke]\n");
            try
            {
                // 1. 主菜单 → 匹配屏
                if (!Click("btn-match")) { Fail("进匹配屏失败"); return; }
                if (!AssertNamed("field-host", "连接栏 IP 输入框")) { Fail("匹配屏元素缺失"); return; }
                if (!AssertNamed("list-rooms", "房间列表")) { Fail("匹配屏元素缺失"); return; }
                if (!AssertNamed("status-zone", "状态区")) { Fail("匹配屏元素缺失"); return; }
                sb.AppendLine("  step1 进匹配屏 OK（field-host/list-rooms/status-zone 均在）");

                // 2. 连接（大厅随 Play 自动启动）
                if (!Click("btn-connect")) { Fail("连接按钮缺失"); return; }
                await Task.Delay(900);
                var toast = ReadText("lbl-toast");
                sb.AppendLine($"  step2 连接 → toast=\"{toast}\"");
                if (toast == null || !toast.Contains("已连接")) { Fail("连接失败（toast 无已连接）"); return; }

                // 3. 创建房间
                if (!Click("btn-create")) { Fail("创建按钮缺失"); return; }
                await Task.Delay(900);
                var status = ReadAllStatusText();
                sb.AppendLine($"  step3 建房 → 状态=\"{status}\"");
                if (!status.Contains("房间：")) { Fail("建房后状态无房间行"); return; }

                // 4. +AI 填位 → 自动提交卡组 → Manifest 移交对战（真对战屏）
                if (!Click("btn-ai")) { Fail("AI 按钮缺失"); return; }
                await Task.Delay(2500);
                var handedOff = AnyActive("battle");
                var finalToast = ReadText("lbl-toast");
                sb.AppendLine($"  step4 AI填位 → battle对战屏出现={handedOff} toast=\"{finalToast}\" 状态=\"{ReadAllStatusText()}\"");
                // 对战屏出现为通过（对局移交成功）；未出现=卡组提交/开局链路断——报 FAIL
                if (!handedOff) { Fail("Manifest 未移交对战屏（卡组提交或开局链路断）"); return; }

                sb.Insert(0, "[PASS]\n");
                Debug.Log(sb.ToString());
            }
            catch (System.Exception ex)
            {
                Fail($"异常：{ex}");
            }
        }

        private static bool Click(string buttonName)
        {
            foreach (var b in Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (b.name != buttonName) continue;
                var ped = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
                ExecuteEvents.Execute(b.gameObject, ped, ExecuteEvents.pointerClickHandler);
                return true;
            }
            return false;
        }

        private static bool AssertNamed(string name, string what)
        {
            foreach (var rt in Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (rt.name == name && rt.rect.width > 10f) return true;
            Debug.LogError($"[UguiMatchSmoke] 缺失/零尺寸: {name} ({what})");
            return false;
        }

        private static bool AnyActive(string name)
        {
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (go.name == name) return true;
            return false;
        }

        private static string ReadText(string name)
        {
            foreach (var t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (t.name == name) return t.text;
            return null;
        }

        private static string ReadAllStatusText()
        {
            var sb = new StringBuilder();
            foreach (var t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (t.transform.IsChildOf(Object.FindFirstObjectByType<Canvas>()?.transform) && !string.IsNullOrEmpty(t.text))
                    sb.Append(t.text).Append(" | ");
            return sb.ToString();
        }

        private static void Fail(string why)
        {
            Debug.LogError($"[UguiMatchSmoke][FAIL] {why}");
        }
    }
}
