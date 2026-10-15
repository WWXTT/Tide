using System;
using System.Collections.Generic;
using System.IO;
using CardCore;
using CardCore.AI.NeuralEnv;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace SynergyUI
{
    /// <summary>
    /// 神经 AI 活体接入（2026-10-14 镜像对决定案）：IAiTurnDriver 适配器——把 NeuralAI
    /// （tide_policy.onnx 整回合策略）接进 BattleController 的 AI 槽位（此前只由编辑器
    /// 对战窗口直调，不接入活体对局）。
    /// 接线序列镜像 AiBattleDeckWindow.ConfigureNeuralIdentity / 训练服务器 HandleReset：
    /// ConfigureManifest（表指纹不符=拒用——embedding 串台比没 AI 更糟）→ 全池+双方卡组
    /// Register（追加式幂等）→ OnnxTidePolicy（进程共享 worker）→ 新对局换脑（GRU rstate 归零）。
    /// 模型/清单缺失或指纹不符：一次性失败标记 + 响亮回落 SimpleAI（对局可继续，Console 留因）。
    /// 注意：战斗时序=训练口径 DrainStack——镜像局里人类的守卫响应窗口不开启
    /// （守卫交互在教学剧本局验证，ScriptedAi 走 SettleResponseWindowAsync）。
    /// </summary>
    public sealed class NeuralAiDriver : IAiTurnDriver
    {
        private static OnnxTidePolicy _sharedPolicy; // 模型加载贵：进程内跨局共享（rstate 归零在换脑时做）
        private static string _brokenReason;         // 一次性失败标记（本进程不再重试）
        private static List<CardData> _poolCache;    // Cards.json 全池（登记用，载一次）

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
                _brain.TakeTurn(ctrl); // 同步整回合（CPU 推理；测试期可接受）
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
                // ① 卡身份清单（进程一次绑定；-1=表指纹不符：游戏内容已变更，旧模型下标全体作废）
                string manifest = Path.Combine(Application.dataPath, "..", "tide_rl", "card_identity_manifest.json");
                if (TideCardIndex.Count == 0 && TideCardIndex.ConfigureManifest(manifest) < 0)
                    return Fail("card_identity_manifest 表指纹与当前原子表不符（重训+重导出后可用）");

                // ② 身份登记（追加式幂等）：全池 + 双方卡组（训练 HandleReset 同序）
                if (_poolCache == null) _poolCache = CardCatalog.LoadAll();
                TideCardIndex.Register(_poolCache);
                TideCardIndex.Register(DeckOf(core, ctrl.P1));
                TideCardIndex.Register(DeckOf(core, ctrl.P2));

                // ③ 模型（进程共享）；④ 新对局换脑（rstate 归零）
                if (_sharedPolicy == null) _sharedPolicy = new OnnxTidePolicy();
                _brain = new NeuralAI(_sharedPolicy);
                _brain.ResetEpisode();
                _lastCore = core;
                Debug.Log($"[NeuralAI] tide_policy 就绪（卡身份 {TideCardIndex.Count} 条）——镜像局开打");
                return true;
            }
            catch (Exception ex)
            {
                return Fail(ex.Message);
            }
        }

        private bool Fail(string reason)
        {
            _brokenReason = reason;
            Debug.LogWarning($"[NeuralAI] 接入失败：{reason}（本进程回落 SimpleAI 启发）");
            return false;
        }

        private static List<CardData> DeckOf(GameCore core, Player p)
        {
            var cards = core?.ZoneManager?.GetCards(p, Zone.Deck) ?? new List<Card>();
            var list = new List<CardData>(cards.Count);
            foreach (var c in cards)
            {
                var d = (c as CardWrapper)?.GetData();
                if (d != null) list.Add(d);
            }
            return list;
        }
    }
}
