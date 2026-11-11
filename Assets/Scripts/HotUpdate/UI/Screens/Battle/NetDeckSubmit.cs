using System;
using System.Collections.Generic;
using CardCore;
using CardCore.Network;
using UnityEngine;

namespace SynergyUI
{
    /// <summary>网络局 DeckSubmit 公共提交器（S7a 2026-11-10）：payload+digest+DeckSubmit 单源——
    /// LobbyScreen（相位到达提前提交，跳过战斗选卡组）与 BattleScreen.NetEngageStaged
    ///（旧面板路径兜底）共用。Last* 三件随 Send 记录，BattleScreen.NetOnManifest 消费转
    /// _netSubmitted* 后清空（manifest 回显校验用；跨局残留由消费清零兜底）。</summary>
    public static class NetDeckSubmit
    {
        /// <summary>最近一次提交的卡组主列表（manifest 校验消费后置 null）。</summary>
        public static string[] LastCardIds { get; private set; }
        /// <summary>最近一次提交的衍生物区（P9b；同上）。</summary>
        public static string[] LastTokenIds { get; private set; }
        /// <summary>最近一次提交的槽绑定（P9b；同上）。</summary>
        public static List<DeckTokenBinding> LastTokenBindings { get; private set; }

        /// <summary>提交一套卡组（DeckData 便捷口）：装散参后走 SendRaw。</summary>
        public static bool Send(NetGameClient client, DeckData deck)
        {
            if (client == null || deck?.cardIds == null || deck.cardIds.Count == 0) return false;
            return SendRaw(client, deck.name, deck.cardIds.ToArray(), deck.skillCardId,
                deck.tokenCardIds != null && deck.tokenCardIds.Count > 0 ? deck.tokenCardIds.ToArray() : null,
                deck.tokenBindings);
        }

        /// <summary>提交核心（S7a 自 BattleScreen.NetEngageStaged 平移——两处共用单源）：
        /// digest+Added* 三载荷+DeckSubmit 一次组齐发送；Last* 三件随发记录（manifest 校验消费）。
        /// cardIds 空=no-op 返回 false。</summary>
        public static bool SendRaw(NetGameClient client, string deckName, string[] cardIds,
            string skillCardId, string[] tokenCardIds, List<DeckTokenBinding> tokenBindings)
        {
            if (client == null || cardIds == null || cardIds.Length == 0) return false;
            // 2026-11-11 顺序定案：先组载荷（只读）再算摘要——摘要必须与随提交下发的内容同源同一刻；
            // 反过来（旧序：先算摘要后组载荷）一旦载荷侧有任何归一化/落盘，两端摘要必分叉。
            NetDeckPayload.BuildForDeck(cardIds, tokenCardIds, skillCardId,
                out var addedCards, out var addedEffects, out var addedRows);
            var digest = NetMatchHandshake.ComputeDeckDigest(cardIds, tokenCardIds, tokenBindings); // 主线程调用约定
            client.Send(NetworkMessageType.DeckSubmit, new MsgDeckSubmit
            {
                DeckName = string.IsNullOrEmpty(deckName) ? "未命名卡组" : deckName,
                CardIds = cardIds,
                Digest = digest,
                SkillCardId = skillCardId,
                TokenCardIds = tokenCardIds ?? Array.Empty<string>(),
                TokenBindings = NetMatchHandshake.ToWireBindings(tokenBindings),
                AddedCards = addedCards,
                AddedEffects = addedEffects,
                AddedRows = addedRows,
            });
            LastCardIds = cardIds;
            LastTokenIds = tokenCardIds;
            LastTokenBindings = tokenBindings;
            Debug.Log($"[NetDeckSubmit] 已提交卡组「{deckName}」（{cardIds.Length}张，载荷卡{addedCards.Length}/效果{addedEffects.Length}/词条{addedRows.Length}）");
            return true;
        }

        /// <summary>取走最近一次提交快照（manifest 校验用；取后清空防跨局残留）。</summary>
        public static bool TakeLast(out string[] cardIds, out string[] tokenIds,
            out List<DeckTokenBinding> tokenBindings)
        {
            cardIds = LastCardIds;
            tokenIds = LastTokenIds;
            tokenBindings = LastTokenBindings;
            LastCardIds = null;
            LastTokenIds = null;
            LastTokenBindings = null;
            return cardIds != null;
        }
    }
}
