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
    /// UGUI 效果合成器冒烟（Phase 5 核心验证工具）：主菜单 → 效果合成器 →
    /// 断言四模式 chip/槽位区/原子库行数（正常≥数十行）→ 点库行落槽（QuickAdd 替换选中槽）
    /// → 自动名/保存态刷新 → 切自由分支（主干下拉+奖励槽）→ 效果表模式（列表行）→ 返回。
    /// 用法：进 Play 后执行菜单 Tools/验证/UI效果合成冒烟。
    /// </summary>
    public static class UguiEffectSmoke
    {
        private const string MenuPath = "Tools/验证/UI效果合成冒烟";

        [MenuItem(MenuPath)]
        public static async void Run()
        {
            var sb = new StringBuilder("[UguiEffectSmoke]\n");
            try
            {
                // 1. 进效果合成器
                if (!ClickNamed("btn-effect")) { Fail("主菜单效果合成按钮缺失"); return; }
                await Task.Delay(600);

                int chips = CountNamed(t => t.name.StartsWith("mode-") && t.name != "mode-bar");
                var nameLabel = ReadTextNamed("lbl-effect-name");
                // 库行=原子库 content 下的 row（祖先链判据——对层级包装增删免疫）
                int libRows = CountNamed(t => t.name == "row" && HasAncestor(t, "list-library") && HasAncestor(t, "content"));
                sb.AppendLine($"  step1 进屏：模式chip={chips} 自动名=\"{nameLabel}\" 原子库行≈{libRows}");
                if (chips != 4) { Fail("模式条 chip 数不对"); return; }
                if (libRows < 10) { Fail($"原子库行过少（{libRows}）——表加载/过滤断"); return; }

                // 2. 点首行库行（QuickAdd=替换选中槽=原子槽1）
                var libRow = FindButtons(b => HasAncestor(b.transform, "list-library")).FirstOrDefault();
                if (libRow == null) { Fail("库行不可点"); return; }
                string rowName = TextOf(libRow);
                ClickButton(libRow);
                await Task.Delay(400);
                var name2 = ReadTextNamed("lbl-effect-name");
                var cost2 = ReadTextNamed("lbl-effect-cost");
                sb.AppendLine($"  step2 点库行「{rowName}」→ 自动名=\"{name2}\" {cost2}");
                if (name2 == nameLabel || name2.Contains("（空）")) { Fail("QuickAdd 未落槽（自动名未变）"); return; }

                // 3. 保存按钮应可用（并列模式+已有原子=校验过）
                var save = FindButtons(b => b.name == "btn-save").FirstOrDefault();
                if (save == null) { Fail("保存按钮缺失"); return; }
                sb.AppendLine($"  step3 保存按钮 interactable={save.interactable}");
                if (!save.interactable) { Fail("校验误判——有原子时保存不应禁用"); return; }

                // 4. 切自由分支：主干下拉 + 奖励槽
                if (!ClickNamed("mode-FreeBranch")) { Fail("自由分支 chip 缺失"); return; }
                await Task.Delay(400);
                bool trunkDd = CountNamed(t => t.name == "dd-trunk") >= 1;
                var name4 = ReadTextNamed("lbl-effect-name");
                sb.AppendLine($"  step4 切自由分支：主干下拉={trunkDd} 自动名=\"{name4}\"");
                if (!trunkDd) { Fail("自由分支主干下拉未出现"); return; }

                // 5. 切光环模式：箭头选择器（6 个 hit）+ 光环库
                if (!ClickNamed("mode-Aura")) { Fail("光环 chip 缺失"); return; }
                await Task.Delay(400);
                int arrows = CountNamed(t => t.name.StartsWith("hit-"));
                sb.AppendLine($"  step5 光环模式：箭头命中格={arrows}");
                if (arrows != 6) { Fail("六向箭头选择器未出现"); return; }
                // 点一支箭头 → 计数标签变化 + 保存仍禁用（无条目）
                var arrowBtn = FindButtons(b => b.name.StartsWith("hit-")).FirstOrDefault();
                ClickButton(arrowBtn);
                await Task.Delay(200);
                var count5 = ReadTextNamed("count");
                sb.AppendLine($"  step5b 点箭头 → 计数=\"{count5}\"");

                // 6. 效果表模式
                if (!ClickNamed("mode-Parallel")) { Fail("并列 chip 缺失"); return; }
                await Task.Delay(300);
                if (!ClickNamed("btn-load-effects")) { Fail("读取效果表按钮缺失"); return; }
                await Task.Delay(400);
                int effectRows = CountNamed(t => HasAncestor(t, "list-effects") && t.name == "row");
                // 原子库面板隐藏=按名找不到（活跃集查不到）或找到但 inactive——两态都算隐藏
                var atomPanel = FindTransform("atom-panel");
                bool atomPanelOff = atomPanel == null || !atomPanel.gameObject.activeInHierarchy;
                sb.AppendLine($"  step6 效果表模式：行={effectRows} 原子库隐藏={atomPanelOff}");

                // 7. 返回
                if (!ClickNamed("btn-back")) { Fail("返回按钮缺失"); return; }
                await Task.Delay(400);
                if (CountNamed(t => t.name == "main-menu") != 1) { Fail("返回后主菜单未出现"); return; }
                sb.AppendLine("  step7 返回主菜单 OK");

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
            var btn = FindButtons(b => b.name == name).FirstOrDefault();
            if (btn == null) return false;
            ClickButton(btn);
            return true;
        }

        private static void ClickButton(Button b)
        {
            var ped = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(b.gameObject, ped, ExecuteEvents.pointerClickHandler);
        }

        private static System.Collections.Generic.IEnumerable<Button> FindButtons(System.Func<Button, bool> match)
        {
            return Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Where(match);
        }

        private static Transform FindTransform(string name)
        {
            return Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == name);
        }

        private static bool HasAncestor(Transform t, string name)
        {
            for (var p = t.parent; p != null; p = p.parent)
                if (p.name == name) return true;
            return false;
        }

        private static int CountNamed(System.Func<Transform, bool> match)
        {
            return Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Count(match);
        }

        private static string TextOf(Button b) => b.GetComponentInChildren<TMP_Text>()?.text ?? "";

        private static string ReadTextNamed(string name)
        {
            return Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == name)?.text;
        }

        private static void Fail(string why) => Debug.LogError($"[UguiEffectSmoke][FAIL] {why}");
    }
}
