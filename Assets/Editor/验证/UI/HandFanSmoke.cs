using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Tide.验证
{
    /// <summary>
    /// 手牌扇形冒烟（2026-10-02 建，2026-10-03 改走本地对战链路）：主菜单 → 本地对战
    /// （BattleScreen 扇形手牌过渡面）→ 断言双扇区卡数（6 起手+回合抽牌=7v7）、
    /// 对手全卡背/己方全正面、扇形位姿（拱形弧：端卡下垂外倾）→ 悬停中间卡（放大回正置顶、
    /// 邻卡让位）→ 点「结束回合」驱动 AI 回合、己方回合 2 抽牌触发重排
    /// → 截图落盘 Temp/HandFanSmoke_*.png → 返回。
    /// 用法：进 Play 后执行菜单 Tools/验证/手牌扇形冒烟。
    /// </summary>
    public static class HandFanSmoke
    {
        private const string MenuPath = "Tools/验证/手牌扇形冒烟";

        [MenuItem(MenuPath)]
        public static async void Run()
        {
            var sb = new StringBuilder("[HandFanSmoke]\n");
            try
            {
                // 0. 回主菜单（上次冒烟中断可能停在对战场）
                for (int i = 0; i < 5 && FindTransform("main-menu") == null; i++)
                {
                    if (!ClickNamed("btn-back")) break;
                    await Task.Delay(600);
                }
                if (FindTransform("main-menu") == null) { Fail("无法回到主菜单（前置态异常）"); return; }

                // 1. 进本地对战（BattleScreen 扇形手牌过渡面）
                if (!ClickNamed("btn-battle")) { Fail("主菜单本地对战按钮缺失（MainUI.prefab？）"); return; }
                await Task.Delay(1200);

                var selfLayer = FindTransform("self-fan-layer");
                var oppLayer = FindTransform("opp-fan-layer");
                if (selfLayer == null || oppLayer == null) { Fail("扇区挂载层缺失（battle 屏未建成）"); return; }
                int selfN = ActiveChildren(selfLayer);
                int oppN = ActiveChildren(oppLayer);
                // 起手 OpeningHandSize=6 + 先手回合立即抽 1 → 进屏稳态 7v7
                sb.AppendLine($"  step1 进对战：己方卡={selfN} 对手卡={oppN}");
                if (selfN != 7 || oppN != 7) { Fail($"进屏稳态应为 7v7（6 起手+回合抽牌），实测 {selfN}v{oppN}"); return; }

                // 2. 正/背面：对手全 back 激活，己方全 back 隐藏
                int oppBacks = 0, selfBacks = 0;
                foreach (var c in ActiveCardList(oppLayer))
                    if (BackActive(c)) oppBacks++;
                foreach (var c in ActiveCardList(selfLayer))
                    if (BackActive(c)) selfBacks++;
                sb.AppendLine($"  step2 正背面：对手背面={oppBacks}/{oppN} 己方误显背面={selfBacks}/{selfN}");
                if (oppBacks != oppN) { Fail("对手手牌未全部显示卡背（SetFaceUp 未生效）"); return; }
                if (selfBacks != 0) { Fail("己方手牌出现卡背（误判背面）"); return; }

                // 3. 扇形位姿（软断言：端卡倾斜 + 两端抬升 + 中心回正）
                var pose = DescribeFan(selfLayer);
                sb.AppendLine($"  step3 扇形位姿：{pose}");
                if (!pose.Contains("OK")) { Fail("扇形位姿异常：" + pose); return; }
                await Shot("Temp/HandFanSmoke_1_fan.png");
                sb.AppendLine("  截图1：Temp/HandFanSmoke_1_fan.png");

                // 4. 悬停中间卡：放大回正、渲染置顶、邻卡让位
                var cards = ActiveCardList(selfLayer);
                var mid = cards[selfN / 2];
                var left = cards[selfN / 2 - 1];
                float leftXBefore = ((RectTransform)left).anchoredPosition.x;
                Hover(mid, true);
                await Task.Delay(450);
                float midScale = mid.localScale.x;
                float midRot = mid.localEulerAngles.z; if (midRot > 180f) midRot -= 360f;
                float leftXAfter = ((RectTransform)left).anchoredPosition.x;
                bool topmost = mid.GetSiblingIndex() == selfLayer.childCount - 1;
                sb.AppendLine(string.Format(
                    "  step4 悬停：放大={0:F2} 回正={1:F1}° 置顶={2} 邻卡位移={3:F0}px",
                    midScale, midRot, topmost, Mathf.Abs(leftXAfter - leftXBefore)));
                await Shot("Temp/HandFanSmoke_2_hover.png");
                sb.AppendLine("  截图2：Temp/HandFanSmoke_2_hover.png");
                if (midScale < 1.2f) { Fail($"悬停卡未放大（{midScale:F2}）"); return; }
                if (Mathf.Abs(midRot) > 3f) { Fail($"悬停卡未回正（{midRot:F1}°）"); return; }
                if (!topmost) { Fail("悬停卡未渲染置顶"); return; }
                if (Mathf.Abs(leftXAfter - leftXBefore) < 10f) { Fail("邻卡未让位（位移过小）"); return; }
                Hover(mid, false);
                await Task.Delay(400);
                float midScaleBack = mid.localScale.x;
                sb.AppendLine($"  step4b 离开悬停：缩放回落={midScaleBack:F2}");
                if (midScaleBack > 1.05f) { Fail("离开悬停后未回落"); return; }

                // 5. 结束回合 → AI 跑完对手回合 → 己方回合 2 抽牌 → 手牌数量变化触发重排（轮询最多 25s）
                if (!ClickNamed("btn-end-turn")) { Fail("结束回合按钮缺失（battle 过渡面未建成）"); return; }
                int grown = -1;
                for (int i = 0; i < 50; i++)
                {
                    await Task.Delay(500);
                    int n = ActiveChildren(selfLayer);
                    if (n > selfN) { grown = n; break; }
                }
                int oppNow = ActiveChildren(oppLayer);
                sb.AppendLine($"  step5 回合流转：己方 {selfN}→{(grown > 0 ? grown.ToString() : "未变化")} 对手 {oppN}→{oppNow}");
                if (grown < 0) { Fail("己方手牌数量未随回合增长（结束回合→AI 回合→回合 2 抽牌链未生效）"); return; }
                await Shot("Temp/HandFanSmoke_3_grow.png");
                sb.AppendLine("  截图3：Temp/HandFanSmoke_3_grow.png");

                // 6. 返回主菜单
                if (!ClickNamed("btn-back")) { Fail("返回按钮缺失"); return; }
                await Task.Delay(500);
                if (FindTransform("main-menu") == null) { Fail("返回后主菜单未出现"); return; }
                sb.AppendLine("  step6 返回主菜单 OK");

                sb.Insert(0, "[PASS]\n");
                Debug.Log(sb.ToString());
            }
            catch (System.Exception ex)
            {
                Fail($"异常：{ex}");
            }
        }

        // ================= helpers（纯 Transform 口径，不依赖热更程序集） =================

        private static int ActiveChildren(Transform layer)
        {
            int n = 0;
            for (int i = 0; i < layer.childCount; i++)
                if (layer.GetChild(i).gameObject.activeSelf) n++;
            return n;
        }

        private static System.Collections.Generic.List<Transform> ActiveCardList(Transform layer)
        {
            var list = new System.Collections.Generic.List<Transform>();
            for (int i = 0; i < layer.childCount; i++)
            {
                var c = layer.GetChild(i);
                if (c.gameObject.activeSelf) list.Add(c);
            }
            // 池化实例兄弟序=手牌序（HandFanView.RebuildTargets 维护），直接按序取
            return list;
        }

        private static bool BackActive(Transform card)
        {
            var back = card.Find("back");
            return back != null && back.gameObject.activeInHierarchy;
        }

        /// <summary>扇形位姿描述（拱形弧·圆心在屏外下方）：左卡逆时针外倾/右卡顺时针外倾、
        /// 端卡低于中心卡、左右对称。</summary>
        private static string DescribeFan(Transform layer)
        {
            var cards = ActiveCardList(layer);
            if (cards.Count < 3) return "卡数不足";
            var first = (RectTransform)cards[0];
            var center = (RectTransform)cards[cards.Count / 2];
            var last = (RectTransform)cards[cards.Count - 1];
            float rFirst = Norm(first.localEulerAngles.z);
            float rLast = Norm(last.localEulerAngles.z);
            float yEdge = Mathf.Max(first.anchoredPosition.y, last.anchoredPosition.y);
            bool tiltOk = rFirst > 1f && rLast < -1f; // 左正（逆时针外倾）右负（顺时针外倾）
            bool dipOk = yEdge < center.anchoredPosition.y - 3f; // 端卡下垂（拱形）
            bool symmetryOk = Mathf.Abs(Mathf.Abs(rFirst) - Mathf.Abs(rLast)) < 4f;
            return string.Format(
                "倾角 左{0:F1}°/右{1:F1}° 高度 边{2:F0}/中{3:F0} → {4}",
                rFirst, rLast, yEdge, center.anchoredPosition.y,
                tiltOk && dipOk && symmetryOk ? "OK" : string.Format("异常(倾角{0} 下垂{1} 对称{2})", tiltOk, dipOk, symmetryOk));
        }

        private static float Norm(float deg) => deg > 180f ? deg - 360f : deg;

        private static void Hover(Transform card, bool enter)
        {
            var ped = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            if (enter)
                ExecuteEvents.Execute(card.gameObject, ped, ExecuteEvents.pointerEnterHandler);
            else
                ExecuteEvents.Execute(card.gameObject, ped, ExecuteEvents.pointerExitHandler);
        }

        private static async Task Shot(string path)
        {
            ScreenCapture.CaptureScreenshot(path);
            await Task.Delay(600); // 等帧尾落盘
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

        private static Transform FindTransform(string name)
        {
            return Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == name);
        }

        private static void Fail(string why) => Debug.LogError($"[HandFanSmoke][FAIL] {why}");
    }
}
