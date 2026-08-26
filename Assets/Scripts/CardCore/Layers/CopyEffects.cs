using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCore
{
    /// <summary>
    /// 复制源信息
    /// </summary>
    public class CopySource
    {
        public Card OriginalCard { get; set; }
        public Card CopiedCard { get; set; }
        public CopyType Type { get; set; }
        public DateTime CopyTime { get; set; }
        public uint SequenceNumber { get; set; }
        public Effect SourceEffect { get; set; }
        public Player Controller { get; set; }

        /// <summary>
        /// 复制的属性
        /// </summary>
        public bool CopyStats { get; set; } // 是否复制数值
        public bool CopyCost { get; set; } // 是否复制费用
        public bool CopyColors { get; set; } // 是否复制颜色
        public bool CopyText { get; set; } // 是否复制文本
        public bool CopyEffects { get; set; } // 是否复制效果
    }

    /// <summary>
    /// 复制效果系统
    /// 支持克隆/镜像/完全复制，继承原卡时间戳和效果
    /// </summary>
    public class CopyEffectsEngine
    {
        private Dictionary<Card, List<CopySource>> _copySources =
            new Dictionary<Card, List<CopySource>>();

        private List<Card> _allCopies = new List<Card>();

        /// <summary>
        /// 所有复制的卡牌
        /// </summary>
        public List<Card> AllCopies => _allCopies;

        /// <summary>
        /// 创建复制
        /// </summary>
        public Card CreateCopy(Card originalCard, CopyType copyType, Effect sourceEffect, Player controller)
        {
            if (originalCard == null)
                return null;

            var copySource = new CopySource
            {
                OriginalCard = originalCard,
                Type = copyType,
                CopyTime = DateTime.Now,
                SequenceNumber = TimestampSystem.NextSequence,
                SourceEffect = sourceEffect,
                Controller = controller,
                // 默认复制所有属性
                CopyStats = true,
                CopyCost = true,
                CopyColors = true,
                CopyText = true,
                CopyEffects = true
            };

            // 创建复制的卡牌
            Card copiedCard = CreateCopiedCard(originalCard, copySource);

            if (copiedCard == null)
                return null;

            copySource.CopiedCard = copiedCard;

            // 记录复制源
            if (!_copySources.ContainsKey(originalCard))
            {
                _copySources[originalCard] = new List<CopySource>();
            }
            _copySources[originalCard].Add(copySource);

            _allCopies.Add(copiedCard);

            // 触发事件
            PublishEvent(new CardCreateEvent
            {
                Card = copiedCard,
                Controller = controller
            });

            return copiedCard;
        }

        /// <summary>
        /// 创建复制的卡牌
        /// </summary>
        private Card CreateCopiedCard(Card original, CopySource source)
        {
            var copy = new Card { ID = GenerateCopyID(original.ID) };

            // MTG：复制仅拷贝「可复制特征值」（印刷/基础特征），不含指示物、横置、伤害、控制权等运行时状态。
            if (source.CopyStats)
            {
                copy._power = original._power;
                copy._life = original._life;
                copy._maxLife = original._maxLife;
            }
            if (source.CopyCost)
            {
                copy._baseCost = original._baseCost;
            }
            if (source.CopyText || source.CopyEffects)
            {
                // 能力关键词属于可复制特征
                copy._keywords = new HashSet<string>(original._keywords);
            }

            return copy;
        }

        /// <summary>
        /// 生成复制卡牌ID
        /// </summary>
        private string GenerateCopyID(string originalID)
        {
            return $"{originalID}_copy{TimestampSystem.NextSequence}";
        }

        /// <summary>
        /// 获取卡牌的所有复制源
        /// </summary>
        public List<CopySource> GetCopySources(Card originalCard)
        {
            if (!_copySources.ContainsKey(originalCard))
                return new List<CopySource>();

            return _copySources[originalCard];
        }

        /// <summary>
        /// 获取卡牌的所有复制
        /// </summary>
        public List<Card> GetCopies(Card originalCard)
        {
            var result = new List<Card>();

            if (_copySources.ContainsKey(originalCard))
            {
                result.AddRange(_copySources[originalCard].Select(cs => cs.CopiedCard));
            }

            return result;
        }

        /// <summary>
        /// 检查是否为复制
        /// </summary>
        public bool IsCopy(Card card)
        {
            foreach (var copySourceList in _copySources.Values)
            {
                if (copySourceList.Any(cs => cs.CopiedCard == card))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 获取原始卡牌
        /// </summary>
        public Card GetOriginalCard(Card copiedCard)
        {
            foreach (var copySourceList in _copySources.Values)
            {
                var source = copySourceList.FirstOrDefault(cs => cs.CopiedCard == copiedCard);
                if (source != null)
                {
                    return source.OriginalCard;
                }
            }

            return null;
        }

        /// <summary>
        /// 检查复制是否有效
        /// </summary>
        public bool IsCopyValid(CopySource copySource)
        {
            if (copySource == null)
                return false;

            // 原卡仍在场
            if (!IsCardInPlay(copySource.OriginalCard))
                return false;

            // 复制的时间戳继承原卡
            // 这是MTG的核心规则：复制继承原卡的时间戳
            return true;
        }

        /// <summary>
        /// 移除复制
        /// </summary>
        public bool RemoveCopy(Card copiedCard)
        {
            var originalCard = GetOriginalCard(copiedCard);
            if (originalCard == null)
                return false;

            if (_copySources.ContainsKey(originalCard))
            {
                var toRemove = _copySources[originalCard]
                    .Where(cs => cs.CopiedCard == copiedCard)
                    .ToList();

                foreach (var cs in toRemove)
                {
                    _copySources[originalCard].Remove(cs);
                }

                if (_copySources[originalCard].Count == 0)
                {
                    _copySources.Remove(originalCard);
                }
            }

            _allCopies.Remove(copiedCard);

            // TODO: 触发复制离开事件
            return true;
        }

        /// <summary>
        /// 移除原卡的所有复制
        /// </summary>
        public void RemoveAllCopies(Card originalCard)
        {
            if (!_copySources.ContainsKey(originalCard))
                return;

            var copies = _copySources[originalCard];
            foreach (var cs in copies)
            {
                _allCopies.Remove(cs.CopiedCard);
            }

            _copySources.Remove(originalCard);
        }

        /// <summary>
        /// 根据复制类型确定控制者
        /// </summary>
        private Player DetermineController(CopyType type, Card original, Player requestedController)
        {
            switch (type)
            {
                case CopyType.Clone:
                    // 完全克隆：控制者为原卡控制者
                    return GetOriginalController(original);

                case CopyType.Mirror:
                    // 镜像：控制者为原卡控制者
                    return GetOriginalController(original);

                case CopyType.FullCopy:
                    // 完全复制：控制者为发动复制的玩家
                    return requestedController;

                default:
                    return requestedController;
            }
        }

        /// <summary>
        /// 获取原卡控制者
        /// </summary>
        private Player GetOriginalController(Card card)
        {
            return card?.GetController();
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
        /// 获取复制继承的效果：当配置复制文本/效果时，继承原卡的运行时效果。
        /// </summary>
        public List<IEffect> GetInheritedEffects(CopySource source)
        {
            var result = new List<IEffect>();
            if (source == null || source.OriginalCard == null)
                return result;

            if (!source.CopyEffects && !source.CopyText)
                return result;

            if (source.OriginalCard is IHasRuntimeEffects hasEffects && hasEffects.RuntimeEffects != null)
                result.AddRange(hasEffects.RuntimeEffects);

            return result;
        }

        /// <summary>
        /// 清空所有记录
        /// </summary>
        public void ClearAll()
        {
            _copySources.Clear();
            _allCopies.Clear();
        }

        /// <summary>
        /// 发布事件
        /// </summary>
        private void PublishEvent<T>(T e) where T : IGameEvent
        {
            // 优先经 GameCore 统一路由，使 Trigger/Layer 引擎能观察到复制创建事件
            if (GameCore.Instance != null)
                GameCore.Instance.PublishEvent(e);
            else
                EventManager.Instance.Publish(e);
        }
    }

    /// <summary>
    /// 复制效果引擎扩展方法
    /// </summary>
    public static class CopyEffectsExtensions
    {
        /// <summary>
        /// 检查卡牌是否为某卡的复制
        /// </summary>
        public static bool IsCopyOf(this Card card, Card original)
        {
            // TODO: 通过 CopyEffectsEngine 检查
            return false;
        }

        /// <summary>
        /// 获取卡牌的所有来源
        /// </summary>
        public static List<Card> GetSources(this Card copiedCard, CopyEffectsEngine engine)
        {
            var result = new List<Card>();
            // TODO: 从复制引擎获取
            return result;
        }
    }
}
