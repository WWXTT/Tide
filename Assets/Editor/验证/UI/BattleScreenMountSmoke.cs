using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Tide.验证
{
    /// <summary>
    /// 战场屏挂载冒烟（2026-10-18 战场UI功能拆分护栏重建——旧 UguiBattleSmoke 批次已在 0d4b56ca
    /// 清理，本文件按其模式重写最小版）：Play 模式下走真实入口（主菜单 btn-teststart →
    /// TestStartFlow 预设测试局 → 吞屏进 BattleScreen），等棋盘装台+对局启动后做纯数据层断言
    ///（截图验收禁令——不截图，只认层级/计数/日志）：
    ///   ① 战场屏根（battle）存在且激活 ② 棋盘就绪（BattleBoardGrid.Ready）
    ///   ③ 己方手牌扇有卡（self-fan-layer 有实例） ④ 战报面板已有行（log-list content ≥2 子节点）
    ///   ⑤ 冒烟期间无新增 Error/Exception。
    /// 用法：进 Play 后执行菜单 Tools/验证/战场UI挂载冒烟，读 Console（[BattleScreenMountSmoke] 前缀）。
    /// 拆分回归口径：BattleScreen 每个功能块外迁步骤跑一次；断言失败=装配链被拆坏。
    /// </summary>
    public static class BattleScreenMountSmoke
    {
        private const string MenuPath = "Tools/验证/战场UI挂载冒烟";

        private static int _errCount;

        [MenuItem(MenuPath)]
        public static async void Run()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning($"[BattleScreenMountSmoke] 需在 Play 模式执行（先进 Play 再跑菜单 {MenuPath}）");
                return;
            }
            _errCount = 0;
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;

            var sb = new StringBuilder("[BattleScreenMountSmoke]\n");
            bool pass = true;

            // 0. 起点校验：主菜单在屏（btn-teststart——可能被新手引导隐藏，冒烟自行激活）
            var testBtn = FindButton("btn-teststart");
            if (testBtn == null) { Fail("主菜单 btn-teststart 缺失（不在主菜单？）"); return; }
            if (!testBtn.gameObject.activeInHierarchy)
            {
                Debug.Log("[BattleScreenMountSmoke] btn-teststart 被新手引导隐藏——冒烟自行激活后点击（用户口径 2026-10-18）");
                ActivateUp(testBtn.transform);
            }

            // 1. 真实入口进战（TestStartFlow 预设局 + SwallowToBattle 过渡）
            testBtn.onClick.Invoke();
            sb.AppendLine("  step1 已点 btn-teststart（预设测试局→吞屏进战）");

            // 2. 等战场屏根出现+棋盘就绪（装台异步≈2s，预算 15s）
            RectTransform battle = null;
            float deadline = Time.realtimeSinceStartup + 15f;
            while (Time.realtimeSinceStartup < deadline)
            {
                battle = FindActiveRoot("battle");
                if (battle != null && SynergyUI.BattleBoardGrid.Ready) break;
                await Task.Delay(100);
            }
            if (battle == null) { Fail("战场屏根 battle 未出现（15s）"); return; }
            if (!SynergyUI.BattleBoardGrid.Ready) { Fail("棋盘未就绪（BattleBoardGrid.Ready=false）"); return; }
            sb.AppendLine("  step2 战场屏已挂载、棋盘就绪");

            // 3. 再等 1s：首刷对账（RefreshFieldWhenBoardReady）+ 首回合战报行落定
            await Task.Delay(1000);

            // 4. 数据层断言
            var fan = FindDescendant(battle, "self-fan-layer");
            int fanCards = fan != null ? fan.childCount : 0;
            sb.AppendLine($"  step3 己方手牌扇实例数={fanCards}（>0 期望）");
            if (fanCards <= 0) pass = false;

            var logList = FindDescendant(battle, "log-list");
            var content = logList != null ? FindDescendant(logList, "content") : null;
            int logChildren = content != null ? content.childCount : 0;
            sb.AppendLine($"  step4 战报面板行数（含隐藏模板）={logChildren}（≥2 期望=至少 1 行）");
            if (logChildren < 2) pass = false;

            sb.AppendLine($"  step5 冒烟期间新增 Error/Exception={_errCount}（=0 期望）");
            if (_errCount != 0) pass = false;

            if (pass)
            {
                sb.AppendLine("  PASS：战场屏装配链完整（屏根/棋盘/手牌扇/战报/无异常）");
                Debug.Log(sb.ToString());
            }
            else
            {
                Debug.LogError(sb.ToString());
            }
        }

        private static void OnLog(string _, string __, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) _errCount++;
        }

        private static void Fail(string why)
        {
            Debug.LogError($"[BattleScreenMountSmoke]  FAIL：{why}");
        }

        private static Button FindButton(string name)
        {
            foreach (var b in Object.FindObjectsOfType<Button>(true))
                if (b.name == name) return b; // 含隐藏——btn-teststart 会被新手引导隐藏（2026-10-18 用户口径）
            return null;
        }

        /// <summary>沿祖先链激活隐藏节点（新手引导隐藏的 test 按钮自行打开——只动 activeSelf，不碰引导逻辑）。</summary>
        private static void ActivateUp(Transform t)
        {
            if (t == null) return;
            var chain = new List<Transform>();
            for (var p = t; p != null; p = p.parent) chain.Add(p);
            chain.Reverse(); // 自根向下激活
            foreach (var p in chain)
                if (!p.gameObject.activeSelf) p.gameObject.SetActive(true);
        }

        /// <summary>按名找激活的屏根（RectTransform——屏根必为 RectTransform）。</summary>
        private static RectTransform FindActiveRoot(string name)
        {
            foreach (var rt in Object.FindObjectsOfType<RectTransform>(true))
                if (rt.name == name && rt.gameObject.activeInHierarchy) return rt;
            return null;
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                var c = root.GetChild(i);
                if (c.name == name) return c;
                var hit = FindDescendant(c, name);
                if (hit != null) return hit;
            }
            return null;
        }
    }
}
