using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCore
{
    /// <summary>
    /// 卡牌效果可作用对象
    /// </summary>
    public abstract class Entity : ITimestamped
    {
        private TimestampInfo _timestamp;
        private bool _isAlive = true;

        public TimestampInfo TimestampInfo => _timestamp;
        public DateTime CreationTime => _timestamp.DateTime;
        public uint SequenceNumber => _timestamp.Sequence;

        /// <summary>
        /// 是否存活
        /// </summary>
        public bool IsAlive
        {
            get => _isAlive;
            set => _isAlive = value;
        }

        protected Entity(bool createTimestamp = true)
        {
            if (createTimestamp)
            {
                _timestamp = TimestampSystem.CreateTimestamp();
            }
            else
            {
                _timestamp = default;
            }
        }

        /// <summary>
        /// 更新时间戳（用于复制等情况）
        /// </summary>
        public void UpdateTimestamp(TimestampInfo newTimestamp)
        {
            _timestamp = newTimestamp;
        }
    }

    /// <summary>
    /// 战斗单位基类
    /// </summary>
    public class Unit : Entity
    {
        private int _baseAttack = 0;

        public int BaseAttack
        {
            get => _baseAttack;
            set => _baseAttack = value;
        }
    }

    /// <summary>
    /// 玩家类
    /// </summary>
    public class Player : Entity
    {
        private string _name;
        private int _maxHealth;
        private int _life;
        private List<Card> _deck = new List<Card>();
        private List<Card> _hand = new List<Card>();

        public Player Opponent { get; set; }
        /// <summary>AI 玩家标志。true 时目标选择器跳过弹窗，直接自动选择。</summary>
        public bool IsAI { get; set; } = false;
        public string Name => _name;
        public int MaxHealth => _maxHealth;
        public int Life
        {
            get => _life;
            set => _life = value;
        }
        public List<Card> Deck => _deck;
        public List<Card> Hand => _hand;

        // ===== 单局代价抵消已用「费」计数（按机制；受 CostOffsetConfig 单局上限约束，InitGame 时归零）=====
        /// <summary>流失（无源失命）已抵消的费数。</summary>
        public int OffsetDrainUsed { get; set; }
        /// <summary>弃手牌已抵消的费数。</summary>
        public int OffsetDiscardUsed { get; set; }
        /// <summary>磨本组已抵消的费数。</summary>
        public int OffsetMillUsed { get; set; }
        /// <summary>送额外组已抵消的费数。</summary>
        public int OffsetSendExtraUsed { get; set; }

        /// <summary>重置本局抵消计数（新对局开始时调用）。</summary>
        public void ResetOffsetUsage()
        {
            OffsetDrainUsed = 0;
            OffsetDiscardUsed = 0;
            OffsetMillUsed = 0;
            OffsetSendExtraUsed = 0;
        }


        public Player(string name, int maxHealth)
        {
            _name = name;
            _maxHealth = maxHealth;
            _life = maxHealth;
        }

        public void AddToDeck(Card card)
        {
            _deck.Add(card);
        }

        public void AddToHand(Card card)
        {
            _hand.Add(card);
        }

        public void RemoveFromHand(Card card)
        {
            _hand.Remove(card);
        }

        public void RemoveFromDeck(Card card)
        {
            _deck.Remove(card);
        }

        /// <summary>
        /// 选择连锁响应
        /// </summary>
        public EffectInstance ChooseChainResponse(object currentChain)
        {
            // UI：弹窗
            // AI：规则判断
            return null; // 表示 Pass
        }

        /// <summary>
        /// 检查是否有可行动作
        /// </summary>
        public bool HasAvailableAction()
        {
            // 检查手牌中是否有可发动的卡牌
            return _hand.Any(card => card is IHasRuntimeEffects hasEffects &&
                hasEffects.RuntimeEffects != null &&
                hasEffects.RuntimeEffects.Count > 0);
        }
    }
}
