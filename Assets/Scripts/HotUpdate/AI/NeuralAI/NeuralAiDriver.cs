using System;
using System.Collections.Generic;
using CardCore;
using CardCore.AI.NeuralEnv;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace SynergyUI
{
    /// <summary>
    /// 神经 AI 活体接入（2026-10-14 镜像对决定案）：IAiTurnDriver 适配器——把 NeuralAI
    /// （tide_policy.onnx 逐步策略，2026-11-27 S2 起逐步驱动）接进 BattleController 的
    /// AI 槽位（此前只由编辑器对战窗口直调，不接入活体对局）。
    /// 接线（P-1 2026-11-19 稳定下标后）：身份下标纯派生（无 manifest/Register）→
    /// OnnxTidePolicy（进程共享 worker；ValidateModelLayout 按图输入维度 + pool size
    /// 拒旧代模型）→ 新对局换脑（GRU rstate 归零）。
    /// 模型缺失/布局不符：一次性失败标记 + 响亮回落 SimpleAI（对局可继续，Console 留因）。
    /// 战斗时序（S2 2026-11-27 逐步驱动共享，用户口径）：NeuralAI 经 BattleDecisionLoop 逐步
    /// 停靠（Main/响应窗/目标拾取，与训练同口径）；**人类/非神经座的响应窗维持现状**——
    /// 神经座回合内人类座不弹新窗（自动让过），人类自己回合内的窗口照旧走 HumanResponder；
    /// 神经座在对手回合的响应由 NeuralAI 注册的 AiAsyncResponder 内联决策（随
    /// BattleController.Shutdown 还原）。
    /// </summary>
    public sealed class NeuralAiDriver : IAiTurnDriver
    {
        private static OnnxTidePolicy _sharedPolicy; // 模型加载贵：进程内跨局共享（rstate 归零在换脑时做）
        private static string _brokenReason;         // 一次性失败标记（本进程不再重试）

        private readonly SimpleAI _fallback = new SimpleAI();
        private NeuralAI _brain;
        private GameCore _lastCore;

        public async UniTask TakeTurnAsync(BattleController ctrl)
        {
            if (!EnsureReady(ctrl))
            {
                await _fallback.TakeTurnAsync(ctrl);
                return;
            }
            try
            {
                _brain.TakeTurn(ctrl); // 同步逐步整回合（CPU 推理；测试期可接受——S2 起 loop 逐步停靠）
            }
            catch (Exception ex)
            {
                // 推理期异常兜底（2026-10-14 实测：观测向量随战场改动扩到 49 维，旧导出模型
                // 输入仍 36 维 → ONNX 形状断言炸在 Select 首步；异常若上抛会经 async void
                // 只进 Console，对手回合永久卡死——"结束回合后 AI 不动"）。一次性击碎 +
                // 响亮回落：本回合立即改由 SimpleAI 跑完（回合必然流动），本进程后续同回落。
                Fail($"推理异常（模型与观测编码不匹配？重训+重导出 tide_policy 后可用）：{ex.Message}");
                await _fallback.TakeTurnAsync(ctrl);
                return;
            }
            await UniTask.Yield();
        }

        /// <summary>接线/换脑（新 GameCore 才跑；同对局回合直过）。失败置 _brokenReason。</summary>
        private bool EnsureReady(BattleController ctrl)
        {
            if (_brokenReason != null) return false;
            var core = ctrl.Core;
            if (core == null) return Fail("GameCore 未初始化");
            if (ReferenceEquals(_lastCore, core)) return _brain != null;
            try
            {
                // P-1（2026-11-19 稳定下标）：身份下标纯派生（TideCardIndex.IndexOf / AtomTypeIndex），
                // manifest 绑定/登记已废弃——不再 ConfigureManifest/Register（文件仅存档）。

                // 模型（进程共享）；新对局换脑（rstate 归零）——ValidateModelLayout 按图输入
                // 维度 + pool size（16384/4096 embedding 表）拒旧代模型
                if (_sharedPolicy == null) _sharedPolicy = new OnnxTidePolicy();
                _brain = new NeuralAI(_sharedPolicy);
                _brain.ResetEpisode();
                _lastCore = core;
                Debug.Log($"[NeuralAI] tide_policy 就绪（稳定下标 P-1：cardPool {TideCardIndex.Capacity}"
                          + $" / typePool {CardIdentityService.MaxTypeIndex + 1}"
                          + $" / layout v{TideObservation.LayoutVersion}）——镜像局开打");
                return true;
            }
            catch (Exception ex)
            {
                return Fail(ex.Message);
            }
        }

        private bool Fail(string reason)
        {
            // 2026-11-02 部署加固：回落保留（对局必然能继续打），但日志从 Warning 升为 Error——
            // 模型/布局/清单过期导致的静默降级是「棋力悄悄变弱」的元凶，必须响亮
            _brokenReason = reason;
            Debug.LogError($"[NeuralAI] 接入失败：{reason}（本进程回落 SimpleAI 启发——"
                           + "重训 + 重跑 tide_rl/export_onnx.py 重导出后恢复）");
            return false;
        }
    }
}
