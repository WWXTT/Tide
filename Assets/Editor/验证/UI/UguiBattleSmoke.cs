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
    /// UGUI 对战屏冒烟（2026-10-01 UITK→UGUI 迁移 Phase 2 验证工具）：
    /// 主菜单 → 本地对战 → 断言 HUD（信息栏/六行战场/手牌卡/战报）→ 点手牌出牌 →
    /// 结束回合（AI 跑回合+可能的响应窗口跳过）→ 投降 → 终局弹窗返回主菜单。
    /// 用法：进 Play 后执行菜单 Tools/验证/UI对战冒烟，读 Console 输出。
    /// </summary>
    public static class UguiBattleSmoke
    {
        private const string MenuPath = "Tools/验证/UI对战冒烟";

        [MenuItem(MenuPath)]
        public static async void Run()
        {
            // 战场 UI 已删除（2026-10-02 定案：战场改 3D + 部分透明 UI 重做，旧 uGUI 预制体无指导意义，
            // BattleScreen 现为空屏桩）——本冒烟 SKIP；待 3D 战场层就位后按新架构重写。
            if (!System.IO.File.Exists("Assets/Art/UI/BattleUI.prefab"))
            {
                Debug.Log("[UguiBattleSmoke]\n  SKIP：战场 UI 已下线（3D 重做中），对战 HUD 冒烟停用");
                return;
            }
            var sb = new StringBuilder("[UguiBattleSmoke]\n");
            try
            {
                // 1. 进本地对战
                if (!ClickButton(b => b.name == "btn-battle")) { Fail("主菜单对战按钮缺失"); return; }
                await Task.Delay(700);
                if (!AssertBattleHud(sb)) { Fail("对战 HUD 断言失败"); return; }

                // 2. 点第一张手牌卡（CardOverlay 实例的 Button）→ 期望出现选择/模式弹窗
                if (!ClickHandCard())
                {
                    sb.AppendLine("  step2 手牌为空或卡面不可点（跳过出牌步骤）");
                }
                else
                {
                    await Task.Delay(400);
                    var modalRow = FindOverlayRows().FirstOrDefault();
                    if (modalRow == null)
                    {
                        sb.AppendLine("  step2 点卡后无弹窗（直接打出/费用不足 toast 路径）");
                    }
                    else
                    {
                        ClickButton(modalRow);
                        sb.AppendLine($"  step2 点手牌 → 弹窗选项「{TextOf(modalRow)}」已点击");
                        await Task.Delay(400);
                        // 可能还有第二层弹窗（模式→目标）
                        var second = FindOverlayRows().FirstOrDefault();
                        if (second != null)
                        {
                            ClickButton(second);
                            sb.AppendLine($"  step2b 二级弹窗「{TextOf(second)}」已点击");
                            await Task.Delay(400);
                        }
                    }
                }

                // 3. 结束回合（AI 接管；期间可能弹人类响应窗口→跳过）
                if (!ClickButton(b => b.name == "btn-end-turn")) { Fail("结束回合按钮缺失"); return; }
                await Task.Delay(1600);
                for (int i = 0; i < 3; i++)
                {
                    var skip = FindOverlayRows().FirstOrDefault(r => TextOf(r).Contains("跳过"));
                    if (skip == null) break;
                    ClickButton(skip);
                    sb.AppendLine("  step3 响应窗口 → 已跳过");
                    await Task.Delay(700);
                }
                var logCount = CountActiveNamed("line");
                sb.AppendLine($"  step3 结束回合完成（战报行≈{logCount}）");
                if (logCount <= 0) { Fail("战报无任何行——引擎帧驱或事件接线断"); return; }

                // 4. 投降 → 确认 → 终局弹窗返回
                if (!ClickButton(b => b.name == "btn-concede")) { Fail("投降按钮缺失"); return; }
                await Task.Delay(400);
                var confirmRow = FindOverlayRows().FirstOrDefault(r => TextOf(r).Contains("确认投降"));
                if (confirmRow == null) { Fail("投降确认弹窗未出现"); return; }
                ClickButton(confirmRow);
                await Task.Delay(600);
                var cancel = FindButtons(b => b.name == "overlay-cancel").FirstOrDefault();
                if (cancel == null) { Fail("终局弹窗未出现"); return; }
                sb.AppendLine($"  step4 投降 → 终局弹窗（按钮文字=「{TextOf(cancel)}」）");
                ClickButton(cancel);
                await Task.Delay(500);
                if (CountActiveNamed("main-menu") == 0) { Fail("终局返回后主菜单未出现"); return; }
                sb.AppendLine("  step4 返回主菜单 OK");

                sb.Insert(0, "[PASS]\n");
                Debug.Log(sb.ToString());
            }
            catch (System.Exception ex)
            {
                Fail($"异常：{ex}");
            }
        }

        private static bool AssertBattleHud(StringBuilder sb)
        {
            // 信息栏
            var life = ReadTextNamed("label", "生命");
            var phase = FindText(t => t.name == "lbl-phase" && !string.IsNullOrEmpty(t.text));
            // 六行战场容器
            bool rows = CountActiveNamed("opp-lands") == 1 && CountActiveNamed("self-lands") == 1
                && CountActiveNamed("opp-units-near") == 1 && CountActiveNamed("self-units-near") == 1;
            // 手牌槽（起手若干张）
            int handSlots = CountActiveNamed("hand-slot");
            // 战报滚动列
            bool log = CountActiveNamed("log-list") == 1;
            sb.AppendLine($"  HUD: life=\"{life}\" phase=\"{phase?.text}\" 六行={rows} 手牌槽={handSlots} 战报={log}");
            return !string.IsNullOrEmpty(life) && phase != null && rows && log;
        }

        private static bool ClickHandCard()
        {
            // 卡实例=Button 挂在 hand-slot 子级（CardOverlayController 池化实例）
            foreach (var slot in Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (slot.name != "hand-slot") continue;
                var btn = slot.GetComponentInChildren<Button>();
                if (btn == null) continue;
                return ClickButton(btn);
            }
            return false;
        }

        private static System.Collections.Generic.IEnumerable<Button> FindOverlayRows() =>
            FindButtons(b => b.name == "row" && b.transform.IsChildOf(FindActiveNamed("overlay")?.transform ?? b.transform));

        private static bool ClickButton(Button b)
        {
            var ped = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(b.gameObject, ped, ExecuteEvents.pointerClickHandler);
            return true;
        }

        private static bool ClickButton(System.Func<Button, bool> match)
        {
            var btn = FindButtons(match).FirstOrDefault();
            return btn != null && ClickButton(btn);
        }

        private static System.Collections.Generic.IEnumerable<Button> FindButtons(System.Func<Button, bool> match)
        {
            return Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(match);
        }

        private static Transform FindActiveNamed(string name)
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (t.name == name) return t;
            return null;
        }

        private static int CountActiveNamed(string name)
        {
            return Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Count(t => t.name == name);
        }

        private static string ReadTextNamed(string name, string contains)
        {
            var t = FindText(x => x.name == name && x.text.Contains(contains));
            return t?.text;
        }

        private static TMP_Text FindText(System.Func<TMP_Text, bool> match)
        {
            return Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .FirstOrDefault(match);
        }

        private static string TextOf(Button b)
        {
            var lbl = b.GetComponentInChildren<TMP_Text>();
            return lbl != null ? lbl.text : "";
        }

        private static void Fail(string why) => Debug.LogError($"[UguiBattleSmoke][FAIL] {why}");
    }
}
