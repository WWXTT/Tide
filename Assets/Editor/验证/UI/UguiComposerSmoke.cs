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
    /// UGUI 卡牌编辑器冒烟（Phase 4 验证工具）：主菜单 → 卡牌编辑器 → 断言三栏 →
    /// 点卡池行载入编辑（toast）→ 新建复位 → 返回主菜单。
    /// 用法：进 Play 后执行菜单 Tools/验证/UI卡牌编辑冒烟。
    /// </summary>
    public static class UguiComposerSmoke
    {
        private const string MenuPath = "Tools/验证/UI卡牌编辑冒烟";

        [MenuItem(MenuPath)]
        public static async void Run()
        {
            var sb = new StringBuilder("[UguiComposerSmoke]\n");
            try
            {
                if (!ClickNamed("btn-card")) { Fail("主菜单卡牌编辑按钮缺失"); return; }
                await Task.Delay(500);
                foreach (var el in new[] { "list-pool", "list-attached", "list-breakdown", "dynamic-form", "payload-zone", "keyword-zone" })
                    if (CountNamed(el) != 1) { Fail($"元素缺失/重复: {el}"); return; }
                sb.AppendLine("  step1 进屏 OK（三栏+表单区在位）");

                // 点第一张卡池行（行=Column+Button；向上找 list-pool 祖先）
                var poolRow = Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                    .FirstOrDefault(b => HasAncestor(b.transform, "list-pool"));
                if (poolRow == null) { Fail("卡池无可点行"); return; }
                ClickButton(poolRow);
                await Task.Delay(400);
                var toast1 = ReadTextNamed("lbl-toast");
                sb.AppendLine($"  step2 点卡池行 → toast=\"{toast1}\"");
                if (toast1 == null || !toast1.Contains("编辑")) { Fail("载入编辑 toast 未出现"); return; }

                // 新建复位
                if (!ClickNamed("btn-new")) { Fail("新建按钮缺失"); return; }
                await Task.Delay(300);
                var toast2 = ReadTextNamed("lbl-toast");
                sb.AppendLine($"  step3 新建 → toast=\"{toast2}\"");

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

        private static bool HasAncestor(Transform t, string name)
        {
            for (var p = t.parent; p != null; p = p.parent)
                if (p.name == name) return true;
            return false;
        }

        private static bool ClickNamed(string name)
        {
            var btn = Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .FirstOrDefault(b => b.name == name);
            if (btn == null) return false;
            ClickButton(btn);
            return true;
        }

        private static void ClickButton(Button b)
        {
            var ped = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(b.gameObject, ped, ExecuteEvents.pointerClickHandler);
        }

        private static int CountNamed(string name)
        {
            return Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Count(t => t.name == name);
        }

        private static string ReadTextNamed(string name)
        {
            return Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == name)?.text;
        }

        private static void Fail(string why) => Debug.LogError($"[UguiComposerSmoke][FAIL] {why}");
    }
}
