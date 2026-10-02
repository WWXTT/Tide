using System.Linq;
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
    /// UGUI 卡组构筑冒烟（2026-10-01 UITK→UGUI 迁移 Phase 3 验证工具）：
    /// 主菜单 → 卡组构筑 → 断言三栏 → 点卡牌列表「添加」→ 卡组网格出槽位+卡实例挂载
    /// → 统计/摘要刷新 → 返回主菜单。
    /// 用法：进 Play 后执行菜单 Tools/验证/UI卡组构筑冒烟，读 Console 输出。
    /// </summary>
    public static class UguiDeckSmoke
    {
        private const string MenuPath = "Tools/验证/UI卡组构筑冒烟";

        [MenuItem(MenuPath)]
        public static async void Run()
        {
            var sb = new StringBuilder("[UguiDeckSmoke]\n");
            try
            {
                // 1. 进卡组构筑
                if (!ClickNamed("btn-deck")) { Fail("主菜单卡组构筑按钮缺失"); return; }
                await Task.Delay(500);
                foreach (var el in new[] { "preview-zone", "list-deck", "list-catalog", "stats-zone" })
                    if (CountNamed(el) != 1) { Fail($"元素缺失/重复: {el}"); return; }
                sb.AppendLine($"  step1 进屏 OK（三栏+统计在位；过滤chip={CountNamed(t => t.name.StartsWith("chip-"))}）");

                // 2. 点卡牌列表前两张的「添加」（每次点击后目录全量重建——逐轮重查，不预捕获）
                int added = 0;
                for (int i = 0; i < 2; i++)
                {
                    var add = Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                        .FirstOrDefault(b => b.name == "action" && TextOf(b) == "添加" && b.interactable);
                    if (add == null) break;
                    ClickButton(add);
                    added++;
                    await Task.Delay(300);
                }
                if (added == 0) { Fail("卡牌列表无可用「添加」按钮"); return; }
                await Task.Delay(300);

                int slots = CountNamed("deck-card-slot");
                var summary = ReadTextNamed("lbl-summary");
                sb.AppendLine($"  step2 添加 {added} 张 → 卡组槽={slots} 摘要=\"{summary}\"");
                if (slots < added) { Fail("卡组网格未出现卡槽（ScrollGrid/绑定断）"); return; }

                // 3. 卡实例挂载验证（槽下应有 Button=CardOverlay 实例）
                int cardBtns = 0;
                foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    if (t.name == "deck-card-slot" && t.GetComponentInChildren<Button>() != null) cardBtns++;
                sb.AppendLine($"  step3 槽内卡实例（可点）={cardBtns}");
                if (cardBtns < added) { Fail("卡面实例未挂进槽位（CardOverlayController.Bind 断）"); return; }

                // 4. 返回
                if (!ClickNamed("btn-back")) { Fail("返回按钮缺失"); return; }
                await Task.Delay(400);
                if (CountNamed("main-menu") != 1) { Fail("返回后主菜单未出现"); return; }
                sb.AppendLine("  step4 返回主菜单 OK");

                sb.Insert(0, "[PASS]\n");
                Debug.Log(sb.ToString());
            }
            catch (System.Exception ex)
            {
                Fail($"异常：{ex}");
            }
        }

        private static bool ClickNamed(string name)
        {
            var btn = Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .FirstOrDefault(b => b.name == name);
            if (btn == null) return false;
            var ped = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(btn.gameObject, ped, ExecuteEvents.pointerClickHandler);
            return true;
        }

        private static void ClickButton(Button b)
        {
            var ped = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(b.gameObject, ped, ExecuteEvents.pointerClickHandler);
        }

        private static int CountNamed(string name) => CountNamed(t => t.name == name);

        private static int CountNamed(System.Func<Transform, bool> match)
        {
            return Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Count(match);
        }

        private static string TextOf(Button b) => b.GetComponentInChildren<TMP_Text>()?.text ?? "";

        private static string ReadTextNamed(string name)
        {
            return Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == name)?.text;
        }

        private static void Fail(string why) => Debug.LogError($"[UguiDeckSmoke][FAIL] {why}");
    }
}
