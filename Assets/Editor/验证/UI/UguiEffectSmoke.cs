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
    /// UGUI 效果合成器冒烟（Phase 5 核心验证工具；2026-10-05 两槽定案重锚）：
    /// 主菜单 → 效果合成器（并列/光环两形态芯片）→ 断言槽位区/原子库行数（正常≥数十行）
    /// → 点库行落槽（QuickAdd 替换选中槽）→ 自动名/保存态刷新 → 切光环形态（arrow-picker 预制体
    /// +共用原子槽+光环库，2026-10-06 重锚：arrows 显隐/设置盒隐藏/三角纯白）→ 返回并列 → 效果表切换（btn-switch-table）→ 返回。
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
                // 1. 进效果合成器：两形态芯片（并列/光环——两槽定案，自由/有限分支折叠进并列的槽级载荷编辑）
                if (!ClickNamed("btn-effect")) { Fail("主菜单效果合成按钮缺失"); return; }
                await Task.Delay(600);

                int chips = CountNamed(t => t.name.StartsWith("mode-") && t.name != "mode-bar");
                var nameLabel = ReadTextNamed("lbl-effect-name");
                // 库行=原子库 content 下的 row（祖先链判据——对层级包装增删免疫）
                int libRows = CountNamed(t => t.name == "row" && HasAncestor(t, "list-library") && HasAncestor(t, "content"));
                sb.AppendLine($"  step1 进屏：模式chip={chips} 自动名=\"{nameLabel}\" 原子库行≈{libRows}");
                if (chips != 2) { Fail($"模式条 chip 数不对（期望 2=并列/光环，实际 {chips}）"); return; }
                if (CountNamed(t => t.name == "mode-Parallel") != 1
                    || CountNamed(t => t.name == "mode-Aura") != 1) { Fail("两形态芯片应为 mode-Parallel/mode-Aura"); return; }
                if (libRows < 10) { Fail($"原子库行过少（{libRows}）——表加载"); return; }

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

                // 3b. 两流程定案（2026-10-05 晚间）：通常原子=单 gate-row 默认「无」，旧四选一下拉退役
                if (CountNamed(t => t.name == "dd-gate-row") != 1)
                { Fail("通常原子槽应出现 gate-row（dd-gate-row）"); return; }
                if (CountNamed(t => t.name == "dd-branch-way") != 0)
                { Fail("旧「分支方式」四选下拉应已退役（dd-branch-way）"); return; }
                var gateRowT = FindTransform("dd-gate-row");
                bool gateNone = gateRowT != null
                    && gateRowT.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "无");
                sb.AppendLine($"  step3b gate-row 默认无={gateNone}");
                if (!gateNone) { Fail("gate-row 默认应为「无」（有限分支默认无门槛）"); return; }

                // 3c. 引擎主干行回表：点库行「倒计时」→ 无 gate-row、引擎参数行直出
                var engineRow = FindButtons(b => HasAncestor(b.transform, "list-library"))
                    .FirstOrDefault(b => b.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "倒计时"));
                if (engineRow == null) { Fail("原子库无引擎主干行「倒计时」（回表缺失）"); return; }
                ClickButton(engineRow);
                await Task.Delay(400);
                if (CountNamed(t => t.name == "dd-gate-row") != 0)
                { Fail("引擎主干槽不应出现 gate-row（填入即自由分支）"); return; }
                if (CountNamed(t => t.name == "field-engine-param") != 1)
                { Fail("引擎主干槽应直出参数行（field-engine-param）"); return; }
                if (CountNamed(t => t.name == "branch-engine") != 0)
                { Fail("引擎标识行应已退役（branch-engine）"); return; }
                sb.AppendLine("  step3c 引擎主干落槽：无 gate-row、参数行直出、标识行已退役 OK");

                // 4. 切光环形态（2026-10-06 箭头预制体化+槽共用重锚）：arrows 盒（arrow-picker 实例·
                //    6 按钮 R-T/R/R-D/L-D/L/L-T）+ 两共用原子槽 + 设置盒不克隆
                if (!ClickNamed("mode-Aura")) { Fail("光环 chip 缺失"); return; }
                await Task.Delay(400);
                var pickerT = FindTransform("arrow-picker");
                int arrowBtns = 0;
                if (pickerT != null)
                    arrowBtns = new[] { "R-T", "R", "R-D", "L-D", "L", "L-T" }
                        .Count(n => pickerT.Find(n) != null);
                int auraSlots = CountNamed(t => t.name == "slot" && HasAncestor(t, "slot-area"));
                bool settingsOff = FindTransform("settings") == null; // 光环不克隆设置盒（tpl-settings 恒隐藏）
                sb.AppendLine($"  step4 光环形态：箭头按钮={arrowBtns} 原子槽={auraSlots} 设置盒隐藏={settingsOff}");
                if (arrowBtns != 6) { Fail("arrow-picker 六按钮未齐（R-T/R/R-D/L-D/L/L-T）"); return; }
                if (auraSlots != 2) { Fail($"光环形态应共用两原子槽（实际 {auraSlots}）"); return; }
                if (!settingsOff) { Fail("光环形态不应显示设置盒"); return; }
                // 4b. 点一支箭头（R-T=右上）→ 三角变纯白 + 计数标签「已选 1 支」+ 保存仍禁用（无条目）
                var rtBtn = pickerT != null ? pickerT.Find("R-T")?.GetComponent<Button>() : null;
                if (rtBtn == null) { Fail("R-T 箭头按钮缺失"); return; }
                ClickButton(rtBtn);
                await Task.Delay(200);
                var triImg = pickerT.Find("R-T")?.Find("tri")?.GetComponent<Image>();
                bool triWhite = triImg != null && triImg.color == Color.white;
                var count4 = ReadTextNamed("count");
                sb.AppendLine($"  step4b 点 R-T → tri纯白={triWhite} 计数=\"{count4}\"");
                if (!triWhite) { Fail("选中箭头三角应变纯白（未选=A1A1A1）"); return; }
                if (count4 == null || !count4.Contains("1")) { Fail("箭头计数应显示已选 1 支"); return; }

                // 5. 返回并列形态 → arrows 隐藏（非光环不显箭头——2026-10-06 显隐定案）
                //    → 效果表（原 btn-load-effects/btn-reload-atoms 已合并为 btn-switch-table）
                if (!ClickNamed("mode-Parallel")) { Fail("并列 chip 缺失"); return; }
                await Task.Delay(300);
                bool arrowsOff = FindTransform("arrow-picker") == null;
                sb.AppendLine($"  step5 并列形态：arrows隐藏={arrowsOff}");
                if (!arrowsOff) { Fail("并列形态不应显示 arrows 盒"); return; }
                if (!ClickNamed("btn-switch-table")) { Fail("效果表切换按钮缺失"); return; }
                await Task.Delay(400);
                int effectRows = CountNamed(t => HasAncestor(t, "list-effects") && t.name == "row");
                // 原子库面板隐藏=按名找不到（活跃集查不到）或找到但 inactive——两态都算隐藏
                var atomList = FindTransform("list-library");
                bool atomListOff = atomList == null || !atomList.gameObject.activeInHierarchy;
                sb.AppendLine($"  step5 效果表模式：行={effectRows} 原子库隐藏={atomListOff}");

                // 6. 返回
                if (!ClickNamed("btn-back")) { Fail("返回按钮缺失"); return; }
                await Task.Delay(400);
                if (CountNamed(t => t.name == "main-menu") != 1) { Fail("返回后主菜单未出现"); return; }
                sb.AppendLine("  step6 返回主菜单 OK");

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
