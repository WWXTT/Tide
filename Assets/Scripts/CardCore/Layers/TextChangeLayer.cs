using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCore
{
    /// <summary>
    /// 文本修改
    /// </summary>
    public class TextModification
    {
        public Card TargetCard { get; set; }
        public string OriginalText { get; set; }
        public string ModifiedText { get; set; }
        public Effect SourceEffect { get; set; }
        public DurationType Duration { get; set; }
        public DateTime ModificationTime { get; set; }
        public int SequenceNumber { get; set; }
        public bool IsActive { get; set; }
        public object EndCondition { get; set; } // 条件结束时失效
    }

    /// <summary>
    /// 文本变更层
    /// 负责追踪卡牌文本的动态变更
    /// </summary>
    public class TextChangeLayer
    {
        private Dictionary<Card, List<TextModification>> _cardTextChanges =
            new Dictionary<Card, List<TextModification>>();

        private Dictionary<string, Card> _cardIdMap = new Dictionary<string, Card>();

        /// <summary>
        /// 初始化文本变更层
        /// </summary>
        public TextChangeLayer()
        {
        }

        /// <summary>
        /// 注册卡牌
        /// </summary>
        public void RegisterCard(Card card)
        {
            if (card != null && card.ID != null)
            {
                _cardIdMap[card.ID] = card;

                if (!_cardTextChanges.ContainsKey(card))
                {
                    _cardTextChanges[card] = new List<TextModification>();
                }
            }
        }

        /// <summary>
        /// 注销卡牌
        /// </summary>
        public void UnregisterCard(Card card)
        {
            if (card != null)
            {
                _cardTextChanges.Remove(card);
                _cardIdMap.Remove(card.ID);
            }
        }

        /// <summary>
        /// 修改卡牌文本
        /// </summary>
        public bool ModifyText(Card card, string newText, Effect source, DurationType duration = DurationType.Permanent, object endCondition = null)
        {
            if (card == null)
                return false;

            if (!_cardTextChanges.ContainsKey(card))
            {
                _cardTextChanges[card] = new List<TextModification>();
            }

            var modification = new TextModification
            {
                TargetCard = card,
                OriginalText = GetCurrentText(card),
                ModifiedText = newText,
                SourceEffect = source,
                Duration = duration,
                ModificationTime = DateTime.Now,
                SequenceNumber = (int)TimestampSystem.NextSequence,
                IsActive = true,
                EndCondition = endCondition
            };

            _cardTextChanges[card].Add(modification);

            // 触发文本变更事件
            PublishEvent(new StateChangeEvent
            {
                Type = StateChangeType.Text,
                Target = card,
                OldValue = modification.OriginalText,
                NewValue = modification.ModifiedText
            });

            return true;
        }

        /// <summary>
        /// 移除文本修改
        /// </summary>
        public bool RemoveTextModification(Card card, Effect source)
        {
            if (!_cardTextChanges.ContainsKey(card))
                return false;

            var toRemove = _cardTextChanges[card]
                .Where(m => m.SourceEffect == source)
                .ToList();

            if (toRemove.Count == 0)
                return false;

            foreach (var modification in toRemove)
            {
                modification.IsActive = false;
            }

            // 触发文本变更事件
            var finalText = GetCurrentText(card);
            PublishEvent(new StateChangeEvent
            {
                Type = StateChangeType.Text,
                Target = card,
                OldValue = toRemove[0].ModifiedText,
                NewValue = finalText
            });

            return true;
        }

        /// <summary>
        /// 获取卡牌当前文本
        /// </summary>
        public string GetCurrentText(Card card)
        {
            if (!_cardTextChanges.ContainsKey(card))
            {
                return GetBaseText(card);
            }

            // 获取所有激活的修改
            var activeModifications = _cardTextChanges[card]
                .Where(m => m.IsActive)
                .ToList();

            if (activeModifications.Count == 0)
            {
                return GetBaseText(card);
            }

            // 应用所有文本修改（后应用的覆盖先应用的）
            string result = GetBaseText(card);
            foreach (var mod in activeModifications)
            {
                result = mod.ModifiedText;
            }

            return result;
        }

        /// <summary>
        /// 获取卡牌基础文本
        /// </summary>
        private string GetBaseText(Card card)
        {
            // TODO: 从卡牌获取基础文本
            // 如果卡牌有 IHasName 接口，则返回 CardName
            if (card.TryGetName(out string name))
            {
                return name;
            }

            return "";
        }

        /// <summary>
        /// 检查文本修改是否仍然有效
        /// </summary>
        public void CheckTextModifications()
        {
            var expiredModifications = new List<TextModification>();

            foreach (var kvp in _cardTextChanges)
            {
                var card = kvp.Key;
                var modifications = kvp.Value;

                foreach (var mod in modifications)
                {
                    if (!mod.IsActive)
                        continue;

                    // 检查持续时间
                    bool expired = false;

                    switch (mod.Duration)
                    {
                        case DurationType.Permanent:
                            // 永久：只要卡牌仍在战场即有效，离场则失效
                            expired = !IsCardInPlay(card);
                            break;

                        case DurationType.UntilEndOfTurn:
                        case DurationType.UntilLeaveBattlefield:
                            // 事件驱动失效（OnTurnEnd / OnPhaseEnd）；此处仅在卡牌离场时即时失效
                            expired = !IsCardInPlay(card);
                            break;

                        case DurationType.WhileCondition:
                            expired = !CheckCondition(mod.EndCondition, card);
                            break;
                    }

                    if (expired)
                    {
                        expiredModifications.Add(mod);
                    }
                }
            }

            // 移除过期的修改
            foreach (var mod in expiredModifications)
            {
                mod.IsActive = false;

                // 触发文本变更事件
                var card = mod.TargetCard;
                var finalText = GetCurrentText(card);
                PublishEvent(new StateChangeEvent
                {
                    Type = StateChangeType.Text,
                    Target = card,
                    OldValue = mod.ModifiedText,
                    NewValue = finalText
                });
            }
        }

        /// <summary>
        /// 检查条件。
        /// EndCondition 约定为「结束谓词」Func&lt;bool&gt;：返回 true 表示结束条件已满足 → 修改失效。
        /// 无显式谓词时视为条件持续成立。
        /// </summary>
        private bool CheckCondition(object condition, Card card)
        {
            if (condition is Func<bool> endPredicate)
                return !endPredicate();
            return true;
        }

        /// <summary>
        /// 检查卡牌是否在场（位于其控制者的战场区域）
        /// </summary>
        private bool IsCardInPlay(Card card)
        {
            if (card == null) return false;

            var controller = card.GetController();
            var zm = GameCore.Instance?.ZoneManager;
            if (controller == null || zm == null)
                return card.IsAlive;

            return zm.IsCardInZone(card, controller, Zone.Battlefield);
        }

        /// <summary>
        /// 回合结束：失效本回合玩家的「直到回合结束」文本修改
        /// </summary>
        public void OnTurnEnd(Player player)
        {
            ExpireByDuration(DurationType.UntilEndOfTurn, player);
        }

        /// <summary>
        /// 阶段结束：失效「直到离场/阶段结束」文本修改
        /// </summary>
        public void OnPhaseEnd(PhaseType phase)
        {
            ExpireByDuration(DurationType.UntilLeaveBattlefield, null);
        }

        private void ExpireByDuration(DurationType duration, Player ownerFilter)
        {
            foreach (var kvp in _cardTextChanges)
            {
                foreach (var mod in kvp.Value)
                {
                    if (!mod.IsActive || mod.Duration != duration)
                        continue;

                    if (ownerFilter != null)
                    {
                        //var controller = mod.SourceEffect?.Controller
                        //    ?? mod.SourceEffect?.Source?.GetController();
                        //if (controller != null && controller != ownerFilter)
                        //    continue;
                    }

                    mod.IsActive = false;

                    var card = mod.TargetCard;
                    PublishEvent(new StateChangeEvent
                    {
                        Type = StateChangeType.Text,
                        Target = card,
                        OldValue = mod.ModifiedText,
                        NewValue = GetCurrentText(card)
                    });
                }
            }
        }

        /// <summary>
        /// 获取卡牌的所有文本修改
        /// </summary>
        public List<TextModification> GetTextModifications(Card card)
        {
            if (!_cardTextChanges.ContainsKey(card))
                return new List<TextModification>();

            return _cardTextChanges[card].ToList();
        }

        /// <summary>
        /// 检查卡牌是否有文本修改
        /// </summary>
        public bool HasTextModification(Card card)
        {
            if (!_cardTextChanges.ContainsKey(card))
                return false;

            return _cardTextChanges[card].Any(m => m.IsActive);
        }

        /// <summary>
        /// 获取指定来源的文本修改
        /// </summary>
        public List<TextModification> GetModificationsBySource(Card card, Effect source)
        {
            if (!_cardTextChanges.ContainsKey(card))
                return new List<TextModification>();

            return _cardTextChanges[card]
                .Where(m => m.SourceEffect == source && m.IsActive)
                .ToList();
        }

        /// <summary>
        /// 清空所有文本修改
        /// </summary>
        public void ClearAll()
        {
            _cardTextChanges.Clear();
            _cardIdMap.Clear();
        }

        /// <summary>
        /// 发布事件
        /// </summary>
        private void PublishEvent<T>(T e) where T : IGameEvent
        {
            // 优先经 GameCore 统一路由，使 Trigger/Layer 引擎能观察到文本变更事件
            if (GameCore.Instance != null)
                GameCore.Instance.PublishEvent(e);
            else
                EventManager.Instance.Publish(e);
        }
    }

    /// <summary>
    /// 文本变更层扩展方法
    /// </summary>
    public static class TextChangeExtensions
    {
        /// <summary>
        /// 检查文本是否被修改
        /// </summary>
        public static bool IsTextModified(this Card card)
        {
            // TODO: 通过 TextChangeLayer 检查
            return false;
        }

        /// <summary>
        /// 获取实际显示的文本
        /// </summary>
        public static string GetDisplayText(this Card card)
        {
            // TODO: 通过 TextChangeLayer 获取
            return "";
        }
    }
}
