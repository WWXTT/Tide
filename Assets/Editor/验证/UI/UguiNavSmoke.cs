using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace Tide.验证
{
    /// <summary>
    /// UGUI 导航冒烟（2026-10-01 UITK→UGUI 迁移验证工具）：
    /// 按名字找按钮并手动派发指针点击（等价真实点击），串起一条导航路径验证 Show/Back 栈。
    /// 用法：进 Play 后执行菜单 Tools/验证/UI导航冒烟，读 Console 输出。
    /// </summary>
    public static class UguiNavSmoke
    {
        private const string MenuPath = "Tools/验证/UI导航冒烟";

        [MenuItem(MenuPath)]
        public static void Run()
        {
            var sb = new StringBuilder("[UguiNavSmoke]\n");

            // 1. 主菜单 → 联机大厅——比赛禁联机（NetGate）闸关时入口已隐藏，整段跳过
            if (CardCore.Network.NetGate.OnlineEnabled)
            {
                if (!Click("btn-match", sb)) { Fail(sb); return; }
                if (!AssertVisible("match", "联机大厅屏", sb)) { Fail(sb); return; }

                // 2. 大厅 → 返回主菜单
                if (!Click("btn-back", sb)) { Fail(sb); return; }
                if (!AssertVisible("main-menu", "返回后的主菜单", sb)) { Fail(sb); return; }
            }
            else
            {
                sb.AppendLine("  step1/2 联机大厅入口已随 NetGate 屏蔽（比赛禁联机）——跳过");
            }

            // 3. 主菜单 → 本地对战（真对战屏）→ 返回
            if (!Click("btn-battle", sb)) { Fail(sb); return; }
            if (!AssertVisible("battle", "对战屏", sb)) { Fail(sb); return; }
            if (!Click("btn-back", sb)) { Fail(sb); return; }
            if (!AssertVisible("main-menu", "再次返回主菜单", sb)) { Fail(sb); return; }

            Debug.Log(sb.ToString());
        }

        /// <summary>全场景按名找 Button 并派发 pointerClick（等价用户点击）。</summary>
        private static bool Click(string buttonName, StringBuilder sb)
        {
            var buttons = Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var b in buttons)
            {
                if (b.name != buttonName) continue;
                var ped = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
                ExecuteEvents.Execute(b.gameObject, ped, ExecuteEvents.pointerClickHandler);
                sb.AppendLine($"  click {buttonName} -> OK");
                return true;
            }
            sb.AppendLine($"  click {buttonName} -> NOT FOUND");
            return false;
        }

        /// <summary>断言名字为 name 的 RectTransform 存在且激活（当前屏）。</summary>
        private static bool AssertVisible(string name, string what, StringBuilder sb)
        {
            var rts = Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var rt in rts)
            {
                if (rt.name != name) continue;
                sb.AppendLine($"  assert {name} ({what}) : {Mathf.RoundToInt(rt.rect.width)}x{Mathf.RoundToInt(rt.rect.height)} OK");
                return true;
            }
            sb.AppendLine($"  assert {name} ({what}) : MISSING");
            return false;
        }

        private static void Fail(StringBuilder sb)
        {
            sb.Insert(0, "[FAIL]\n");
            Debug.LogError(sb.ToString());
        }
    }
}
