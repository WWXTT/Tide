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
    /// UGUI 卡组构筑冒烟（2026-10-01 UITK→UGUI 迁移 Phase 3 验证工具；2026-10-16 三栏改版同步）：
    /// 主菜单 → 卡组构筑 → 断言新三栏（左=list-deck/list-decks+统计滚动区·中=card-slot+skill-slot）→
    /// 点卡池「添加」→ 左栏聚合行出现 → 点行内「展示」→ 卡面实例挂进 card-slot → 返回主菜单。
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
                foreach (var el in new[] { "list-deck", "list-catalog", "list-decks", "stats-zone",
                                           "card-slot", "skill-slot", "preview-stage" })
                    if (CountNamed(el) != 1) { Fail($"元素缺失/重复: {el}"); return; }
                sb.AppendLine($"  step1 进屏 OK（三栏+技能栏在位；过滤chip={CountNamed(t => t.name.StartsWith("chip-"))}）");

                // 2. 点卡池前两张的「添加」（每次点击后列表全量重建——逐轮重查，不预捕获）
                int added = 0;
                for (int i = 0; i < 2; i++)
                {
                    var add = Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                        .FirstOrDefault(b => b.name == "add" && TextOf(b) == "添加" && b.interactable
                                             && UnderNamed(b.transform, "list-catalog"));
                    if (add == null) break;
                    ClickButton(add);
                    added++;
                    await Task.Delay(300);
                }
                if (added == 0) { Fail("卡池列表无可用「添加」按钮"); return; }
                await Task.Delay(300);

                // 同卡聚合：左栏 list-deck 下出现 ≥added 行（含 展示/删除 两钮）
                int deckRows = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                    .Count(t => t.name == "row" && UnderNamed(t, "list-deck"));
                var summary = ReadTextNamed("lbl-summary");
                sb.AppendLine($"  step2 添加 {added} 张 → 左栏行={deckRows} 摘要=\"{summary}\"");
                if (deckRows < 1) { Fail("左栏卡组行未出现（list-deck 绑定断）"); return; }

                // 3. 点行内「展示」→ 卡面实例挂进 card-slot（CardOverlay 给卡实例挂 Button）
                var show = Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                    .FirstOrDefault(b => b.name == "show" && b.interactable && UnderNamed(b.transform, "list-deck"));
                if (show == null) { Fail("左栏行内「展示」按钮缺失"); return; }
                ClickButton(show);
                await Task.Delay(300);
                var slot = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                    .FirstOrDefault(t => t.name == "card-slot");
                int mounted = slot != null ? slot.GetComponentsInChildren<Button>(true).Length : 0;
                sb.AppendLine($"  step3 中栏卡面实例（可点）={mounted}");
                if (mounted < 1) { Fail("卡面实例未挂进 card-slot（CardOverlayController.Bind 断）"); return; }

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

        /// <summary>是否在某祖先（滚动区 Content 内行/按钮的归属判定）下。</summary>
        private static bool UnderNamed(Transform t, string ancestor)
        {
            for (var p = t.parent; p != null; p = p.parent)
                if (p.name == ancestor) return true;
            return false;
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
