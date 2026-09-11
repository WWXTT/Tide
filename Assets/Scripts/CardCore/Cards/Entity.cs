using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace CardCore
{
    /// <summary>
    /// 卡牌效果可作用对象。
    /// 世界观定案：角色（玩家化身）就是普通生物单位——关键词存储放在 Entity 层，
    /// Player 与 Card 同构持有（角色默认带神佑状态，见 DeathRules.DivineProtection）。
    /// </summary>
    public abstract class Entity : ITimestamped
    {
        private TimestampInfo _timestamp;
        private bool _isAlive = true;

        // 关键词存储（List 而非 HashSet：融合继承允许重复叠加；唯一性由 AddKeyword 的 Contains 保证）
        internal List<string> _keywords = new List<string>();

        // 指示物存储（上移 Entity：角色/卡牌同构——剧毒/毒素可指向玩家；对齐 _keywords 先例）。
        // 计数模型：_counters 字典存净量（带符号——攻/血/费指示物可 ±）；
        // 回合时钟：_counterClocks 只收 ForTurns 计时的层（毒素每层独立 3 回合时钟），
        // 到期层由 CounterRules.OnTurnEnd 回收并从计数中扣除。
        internal Dictionary<string, int> _counters = new Dictionary<string, int>();
        internal List<CounterInstance> _counterClocks = new List<CounterInstance>();

        // 指示物来源（2026-09-07 定案：增益/减益经指示物承载，指示物也带来源）：
        // 与 _counters 同粒度——每类 id 记最后施加方，计数归零即丢弃（GetCounterSource 查询）。
        // 消费方：剧毒死亡来源、减益致死归因；毒素回合末伤害保持 null（定案：毒素不是伤害来源实体）。
        internal Dictionary<string, Entity> _counterSources = new Dictionary<string, Entity>();

        // 关键词轨别台账（2026-09-09 三轨制定案）：_keywords 仍是运行时唯一真身（HasKeyword/GetKeywordCount
        // 消费面零改动），台账只服务清除口径——换区清（ClearZoneKeywords：Temp）、净化清
        // （PurifyKeywords：Temp+Status+GrantedPermanent，豁免 PurgeProtectedKeywords、保留 Printed+Setting）。
        internal List<KeywordGrant> _keywordGrants = new List<KeywordGrant>();

        public TimestampInfo TimestampInfo => _timestamp;
        public DateTime CreationTime => _timestamp.DateTime;
        public uint SequenceNumber => _timestamp.Sequence;

        // 网络实体身份（M1 协议定案 2026-09-10）：进程内全局唯一自增实例 ID。
        // 不能复用 Card.ID（那是卡表模板 ID，BuildDeck 副本共享）；时间戳路线也不通
        // （Card 构造 createTimestamp:false，Sequence 恒 0）。构造期分配 → token/复制卡
        // 天然有 ID，无惰性登记时序耦合；跨局不重置（同进程多局防串号）。
        // Interlocked 必需：无头桥在后台线程构造实体。
        private static long _nextRuntimeId;
        public uint RuntimeId { get; } = (uint)Interlocked.Increment(ref _nextRuntimeId);

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
    /// 指示物层实例：一个带独立回合时钟的指示物"层"（ toxins 可叠加、每层各自倒计时）。
    /// Amount=该层份数；RemainingTurns=剩余回合末次数（-1=无回合计时，靠换区/消耗移除）。
    /// </summary>
    public class CounterInstance
    {
        public string Id;
        public int Amount;
        public int RemainingTurns = -1;
    }

    /// <summary>
    /// 关键词轨别（三轨制定案 2026-09-09）：同文本赋予按来源分轨后的清除口径。
    /// </summary>
    public enum KeywordLane
    {
        /// <summary>卡面本体（装载期注入；净化/换区都不清）</summary>
        Printed,
        /// <summary>设置类（魔法卡赋予，视同本体；净化/换区都不清）</summary>
        Setting,
        /// <summary>生物赋予的永久关键词（换区不清、净化清）</summary>
        GrantedPermanent,
        /// <summary>临时关键词（换区清、净化清）</summary>
        Temp,
        /// <summary>可移除状态（净化清；神佑另享净化豁免——PurgeProtectedKeywords）</summary>
        Status,
    }

    /// <summary>关键词授予台账条目：_keywords 是真身，本台账只记轨别与来源供清除口径消费。</summary>
    public class KeywordGrant
    {
        public string Keyword;
        public KeywordLane Lane;
        public Entity Source;
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

        /// <summary>
        /// 提升生命值上限（溢出治疗经 LifeUp 指示物转化时调用——层效果，角色不换区→默认持续时间 max=整局）。
        /// 不做负值钳制：调用方语义保证 amount&gt;0。
        /// </summary>
        public void IncreaseMaxHealth(int amount)
        {
            if (amount > 0) _maxHealth += amount;
        }

        /// <summary>
        /// 降低生命值上限（2026-09-11 定案：流失及所有扣命代价改扣上限）。
        /// 超出的当前血一起裁掉；本来就受伤只扣上限（当前血不动）；归零=正常死亡（生命判定收尾）。
        /// </summary>
        public void DecreaseMaxHealth(int amount)
        {
            if (amount <= 0) return;
            _maxHealth -= amount;
            if (_maxHealth < 0) _maxHealth = 0;
            if (_life > _maxHealth) _life = _maxHealth;
        }

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
        /// <summary>送墓（本组）已抵消的费数。</summary>
        public int OffsetMillUsed { get; set; }
        // OpponentHeal/OpponentDraw 抵消计数已删（2026-09-10：机制被 Polarity 错边折价顶替）。
        /// <summary>送额外组已抵消的费数。</summary>
        public int OffsetSendExtraUsed { get; set; }

        /// <summary>疲劳计数：空卡组抽牌次数，第 N 次疲劳造成 N 点递增伤害（炉石式）。</summary>
        // FatigueCount 已进 PlayerState DTO（M1 网络协议 2026-09-10，GetTagDefinitions 已登记）。
        public int FatigueCount { get; set; }

        /// <summary>重置本局抵消计数与疲劳计数（新对局开始时调用）。</summary>
        public void ResetOffsetUsage()
        {
            OffsetDrainUsed = 0;
            OffsetDiscardUsed = 0;
            OffsetMillUsed = 0;
            OffsetSendExtraUsed = 0;
            FatigueCount = 0;
        }


        public Player(string name, int maxHealth)
        {
            _name = name;
            _maxHealth = maxHealth;
            _life = maxHealth;
            // 世界观定案：角色=普通生物单位，默认持有神佑（可被效果移除的真实状态，非硬编码）。
            // 神佑使其免疫一切效果死亡（剧毒/消灭/湮灭…），只接受生命值归零的死亡。
            // 轨别=Status（可净化移除的状态）；但神佑对净化有抗性（2026-09-09 定案：
            // 净化剥神佑+剧毒的组合无法计价平衡——PurgeProtectedKeywords 豁免，移除留给未来专用效果）。
            EntityEffectExtensions.AddKeyword(this, Attribute.DeathRules.DivineProtection, KeywordLane.Status);
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
