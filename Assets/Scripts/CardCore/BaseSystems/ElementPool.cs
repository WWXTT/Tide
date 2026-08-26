using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCore
{
    /// <summary>
    /// 元素池中的卡牌（地牌，带指示物）
    /// 基于卡牌的费用构成放置对应数量的元素指示物
    /// 主阶段可手动横置产 1 个自选颜色元素；回合结束未横置的自动横置产 1 个灰色元素
    /// 每回合周期恰好消耗 1 个指示物；耗尽后卡牌进入墓地
    /// </summary>
    public class PooledCard
    {
        public Card SourceCard { get; }

        /// <summary>剩余指示物 {Red:2, Gray:1}</summary>
        public Dictionary<ManaType, int> Tokens { get; private set; }

        /// <summary>初始指示物数量（用于显示）</summary>
        public Dictionary<ManaType, int> TotalTokens { get; }

        /// <summary>是否已横置（本回合周期已产过 1 个元素）。己方回合开始时恢复（解除横置）。</summary>
        public bool IsTapped { get; set; }

        public PooledCard(Card card, Dictionary<ManaType, int> tokens)
        {
            SourceCard = card;
            Tokens = new Dictionary<ManaType, int>(tokens);
            TotalTokens = new Dictionary<ManaType, int>(tokens);
        }

        public bool HasToken(ManaType type)
        {
            return Tokens.ContainsKey(type) && Tokens[type] > 0;
        }

        /// <summary>是否所有指示物已耗尽</summary>
        public bool IsDepleted => Tokens.Values.Sum() == 0;

        /// <summary>获取指示物总数</summary>
        public int TotalTokenCount => Tokens.Values.Sum();

        /// <summary>
        /// 移除一个指定颜色的指示物
        /// </summary>
        /// <returns>true 表示该卡已耗尽</returns>
        public bool RemoveToken(ManaType type)
        {
            if (!HasToken(type))
                throw new InvalidOperationException($"PooledCard 没有颜色 {type} 的指示物");

            Tokens[type]--;
            return IsDepleted;
        }

        /// <summary>获取所有有剩余指示物的颜色</summary>
        public List<ManaType> GetAvailableColors()
        {
            return Tokens.Where(kv => kv.Value > 0).Select(kv => kv.Key).ToList();
        }
    }

    /// <summary>
    /// 玩家元素池数据
    /// </summary>
    public class PlayerElementPool
    {
        /// <summary>场上地牌（带指示物）。张数受地牌槽上限约束（见 GetLandCap）。</summary>
        public List<PooledCard> PooledCards { get; } = new List<PooledCard>();

        /// <summary>积攒的费用（bank）：地牌产出（主阶段手动自选色 / 结束阶段自动灰色）+ 临时效果添加。跨回合保留，无上限。</summary>
        public Dictionary<ManaType, int> AvailableMana { get; } = new Dictionary<ManaType, int>();

        // ===== 地牌槽曲线（Cap 管场上地牌张数，按全局回合数索引；bank 无上限地积累）=====

        /// <summary>该玩家的资源曲线（由 GameCore.InitGame 注入固定地牌槽曲线；变速效果经 SetCurve 修改）</summary>
        public ResourceCurve Curve { get; set; }

        /// <summary>最近的全局回合数（地牌槽曲线 CapAt 的索引：上限 = min(全局回合数, 9)，双方同步推进）</summary>
        public int GlobalTurnIndex { get; set; }

        /// <summary>个人回合数（自己回合开始时 +1；台账行标识）</summary>
        public int PersonalTurnIndex { get; set; }

        /// <summary>本回合产出次数（手动自选色 + 结束阶段自动灰色；每个全局回合重置）</summary>
        public int TapsThisTurn { get; set; }

        public PlayerElementPool()
        {
            foreach (ManaType mana in Enum.GetValues(typeof(ManaType)))
            {
                AvailableMana[mana] = 0;
            }
        }
    }

    /// <summary>
    /// 元素池系统
    /// 核心费用体系（地牌 = 场上有限电池，bank = 积攒的费用）：
    /// 1. 地牌槽上限 = min(全局回合数, 9)：先手首个回合为 1，此后每个回合开始 +1（对手首回合即 2），最大 9；只限场上地牌张数
    /// 2. 准备阶段：己方横置地牌全部自动恢复（解除横置）
    /// 3. 主阶段：以可用（未横置）状态补充地牌，不限张数/次数（受槽上限）；手动横置地牌产 1 个自选颜色元素入 bank
    ///    —— 自选颜色范围 = 该地牌剩余指示物的颜色集合（由卡本身费用构成决定）
    /// 4. 结束阶段：未横置的地牌自动横置，产 1 个灰色元素入 bank（消耗剩余最多颜色的 1 个指示物）
    /// 5. bank（AvailableMana）跨回合保留、无上限；出牌时自动从 bank 支付
    /// 6. 出牌门槛：卡总费用不得超过当前地牌槽上限（费用上限 9 由此隐含）
    /// 7. 指示物耗尽 → 地牌进墓地 → 可用手牌补充到当前上限
    /// </summary>
    public class ElementPoolSystem
    {
        private const int HAND_SIZE_LIMIT = 7;

        /// <summary>地牌槽上限的最大值（曲线末端值；卡费用上限同为 9）</summary>
        public const int MAX_POOL_SIZE = 9;

        private Dictionary<Player, PlayerElementPool> _playerPools =
            new Dictionary<Player, PlayerElementPool>();

        /// <summary>耗尽卡牌时用于移动到墓地</summary>
        public event Action<Card, Player> OnCardDepleted;

        // ===== 曲线事件路由（P0b）=====
        // 新事件（CurveShiftEvent）经 GameCore 统一路由发布，保证 Trigger/Layer 引擎可见；
        // 旧元素池事件仍直发 EventManager（历史路径，保持兼容）。
        private Action<IGameEvent> _eventRouter;

        // ======================================== 初始化 ========================================

        /// <summary>
        /// 注入事件路由（GameCore.Initialize 调用，指向 GameCore 的统一发布路径）。
        /// </summary>
        public void AttachEventRouter(Action<IGameEvent> router)
        {
            _eventRouter = router;
        }

        private void PublishRouted(IGameEvent e)
        {
            if (_eventRouter != null)
                _eventRouter(e);
            else
                EventManager.Instance.PublishDynamic(e);
        }

        public void InitializePlayer(Player player)
        {
            if (!_playerPools.ContainsKey(player))
            {
                _playerPools[player] = new PlayerElementPool();
            }
        }

        public PlayerElementPool GetPool(Player player)
        {
            if (!_playerPools.ContainsKey(player))
                InitializePlayer(player);
            return _playerPools[player];
        }

        // ======================================== 放卡进元素池 ========================================

        /// <summary>
        /// 将手牌作为地牌放入元素池（主阶段调用，可用/未横置状态入场，不限张数/次数）。
        /// 张数受地牌槽上限约束（min(全局回合数, 9)）；指示物基于卡牌费用构成。
        /// 状态跟随卡：耗尽过的卡（WasDepletedAsLand）永久拒绝；
        /// 离池未耗尽的卡再入池按其记录的余量生成指示物，而非重新按费用满额生成。
        /// </summary>
        public bool AddCardToPool(Card card, Player owner)
        {
            if (card == null || owner == null) return false;

            // 资源枯竭不可逆：耗尽进过墓地的卡不可再次作为地牌
            if (card.WasDepletedAsLand)
                return false;

            // 含动态数量原子的卡不可作地牌产元素（灵活使用的代价：其费用计 0）。
            if (card is CardWrapper wrapper && CostDerivationService.HasDynamicTargetEffect(wrapper.GetData()))
                return false;

            var pool = GetPool(owner);

            // 地牌槽上限：只限场上地牌张数（起始 1，回合开始 +1，最大 9）
            if (pool.PooledCards.Count >= GetLandCap(owner))
                return false;

            // 指示物来源：优先用地牌余量状态（回手再入池保留剩余），否则按费用构成
            var cost = card.HasLandTokenState
                ? card.GetRemainingLandTokens()
                : GetCardCostAsTokens(card);
            if (cost.Values.Sum() == 0)
                return false;

            // 创建带指示物的池卡
            var pooledCard = new PooledCard(card, cost);
            pool.PooledCards.Add(pooledCard);

            // 状态已转移到 PooledCard，清除卡上的余量记录
            card.SetRemainingLandTokens(null);

            PublishEvent(new ElementPoolAddEvent
            {
                Player = owner,
                AddedCard = card,
                Tokens = cost
            });

            return true;
        }

        /// <summary>
        /// 将未耗尽的池内地牌移出元素池（弹回手等效果使用）。
        /// 剩余指示物写回卡上的地牌余量状态，再入池时按余量继续，不重新满额。
        /// 只移除池内记录，不动区域 —— 区域移动由调用方完成。
        /// </summary>
        public bool RemoveCardFromPool(Card card, Player owner)
        {
            if (card == null || owner == null) return false;

            var pool = GetPool(owner);
            var pooledCard = pool.PooledCards.FirstOrDefault(pc => pc.SourceCard == card);
            if (pooledCard == null)
                return false;

            // 未耗尽 → 余量写回卡；耗尽态由耗尽路径处理，不会走到这里
            card.SetRemainingLandTokens(pooledCard.IsDepleted
                ? null
                : new Dictionary<ManaType, int>(pooledCard.Tokens));

            pool.PooledCards.Remove(pooledCard);
            return true;
        }

        // ======================================== 地牌槽曲线管理 ========================================

        /// <summary>
        /// 设置玩家的资源曲线（GameCore.InitGame 注入固定地牌槽曲线；变速效果经此修改）。
        /// 发布 CurveShiftEvent（经路由，Trigger/Layer 可见）。
        /// </summary>
        public void SetCurve(Player player, ResourceCurve curve, string reason = null)
        {
            if (player == null) return;
            var pool = GetPool(player);
            pool.Curve = curve;

            PublishRouted(new CurveShiftEvent
            {
                Player = player,
                NewCurve = curve,
                Reason = reason
            });
        }

        /// <summary>
        /// 玩家当前的地牌槽上限（场上地牌张数上限，也是本回合可出卡的费用上限）。
        /// 标准曲线 = min(全局回合数, 9)（先手首回合 1，对手首回合 2）；无曲线时按同一公式兜底。
        /// </summary>
        public int GetLandCap(Player player)
        {
            var pool = GetPool(player);
            if (pool.Curve != null)
                return pool.Curve.CapAt(pool.GlobalTurnIndex);
            return Math.Max(1, Math.Min(pool.GlobalTurnIndex, MAX_POOL_SIZE));
        }

        // ======================================== 产出元素（主阶段手动 / 结束阶段自动） ========================================

        /// <summary>
        /// 主阶段手动横置地牌：产 1 个自选颜色元素入 bank。
        /// 自选颜色范围 = 该地牌剩余指示物的颜色集合（卡本身费用构成决定）；
        /// 每地牌每回合周期一次；回合/阶段校验在 GameActions 层。
        /// 若因此耗尽，由随后的 CheckDepletedCards 统一移入墓地。
        /// </summary>
        public bool GainElementFromToken(PooledCard land, ManaType type, Player player)
        {
            if (land == null || player == null) return false;
            var pool = GetPool(player);
            if (!pool.PooledCards.Contains(land)) return false;
            if (land.IsTapped) return false;          // 本回合周期已产出
            if (!land.HasToken(type)) return false;   // 只能取该地牌自身构成的颜色

            land.RemoveToken(type);
            pool.AvailableMana[type]++;
            pool.TapsThisTurn++;
            land.IsTapped = true;

            PublishEvent(new ElementPoolGainEvent
            {
                Player = player,
                GainedType = type,
                FromCard = land.SourceCard
            });

            return true;
        }

        /// <summary>
        /// 回合结束阶段：未横置的地牌自动横置，产 1 个灰色元素入 bank。
        /// 消耗「剩余最多颜色」的 1 个指示物（并列取枚举序靠前者，确定性）；
        /// 由此每张地牌每回合周期恰好消耗 1 个指示物（手动产色或结束产灰）。
        /// 耗尽地牌随后统一移入墓地。
        /// </summary>
        public void OnTurnEnd(Player turnPlayer, ZoneManager zoneManager = null)
        {
            if (turnPlayer == null) return;
            var pool = GetPool(turnPlayer);

            foreach (var pc in pool.PooledCards.ToList())
            {
                if (pc.IsTapped) continue;
                var type = PickProduceColor(pc);
                if (type == null) continue;           // 无剩余指示物，交给耗尽清理

                pc.RemoveToken(type.Value);
                pool.AvailableMana[ManaType.Gray]++;
                pool.TapsThisTurn++;
                pc.IsTapped = true;

                PublishEvent(new ElementPoolGainEvent
                {
                    Player = turnPlayer,
                    GainedType = ManaType.Gray,
                    FromCard = pc.SourceCard
                });
            }

            if (zoneManager != null)
                CheckDepletedCards(turnPlayer, zoneManager);
            else
                CheckDepletedCards(turnPlayer);
        }

        /// <summary>自动消耗选色：剩余指示物最多的颜色（并列取枚举序靠前者）</summary>
        private static ManaType? PickProduceColor(PooledCard land)
        {
            ManaType? best = null;
            int bestCount = 0;
            foreach (ManaType type in Enum.GetValues(typeof(ManaType)))
            {
                if (land.Tokens.TryGetValue(type, out int n) && n > bestCount)
                {
                    bestCount = n;
                    best = type;
                }
            }
            return best;
        }

        // ======================================== 支付费用 ========================================

        /// <summary>
        /// 检查是否可以支付指定费用
        /// </summary>
        public bool CanPayCost(Dictionary<int, float> cost, Player player)
        {
            var pool = GetPool(player);

            foreach (var kvp in cost)
            {
                ManaType type = (ManaType)kvp.Key;
                int amount = (int)kvp.Value;
                if (!pool.AvailableMana.ContainsKey(type) || pool.AvailableMana[type] < amount)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 支付费用（出牌时调用）
        /// </summary>
        public bool PayCost(Dictionary<int, float> cost, Player player)
        {
            var pool = GetPool(player);

            // 先检查是否够
            if (!CanPayCost(cost, player))
                return false;

            // 扣减可用元素
            foreach (var kvp in cost)
            {
                ManaType type = (ManaType)kvp.Key;
                int amount = (int)kvp.Value;
                pool.AvailableMana[type] -= amount;
            }

            PublishEvent(new ElementPoolPayEvent
            {
                Player = player,
                PaidCost = cost
            });

            return true;
        }

        // ======================================== 回合管理 ========================================

        /// <summary>
        /// 全局回合开始（GameCore.OnTurnStarted 调用，每回合一次，无论轮到谁）：
        /// 1. 地牌槽曲线按全局回合数推进（先手首回合 1，此后每回合开始 +1，最大 9 —— 对手首回合即为 2）
        /// 2. 所有玩家的本回合产出计数清零
        /// 3. 回合玩家（准备阶段）地牌全部解除横置
        /// </summary>
        public void OnTurnStart(Player turnPlayer, int globalTurnNumber)
        {
            if (turnPlayer == null) return;

            var turnPool = GetPool(turnPlayer);
            turnPool.PersonalTurnIndex++;

            foreach (var pool in _playerPools.Values)
            {
                if (globalTurnNumber > pool.GlobalTurnIndex)
                    pool.GlobalTurnIndex = globalTurnNumber;
                pool.TapsThisTurn = 0;
            }

            foreach (var pc in turnPool.PooledCards)
                pc.IsTapped = false;
        }

        /// <summary>
        /// 检查并移除所有耗尽的卡牌到墓地
        /// </summary>
        public void CheckDepletedCards(Player player)
        {
            var pool = GetPool(player);
            var depleted = pool.PooledCards.Where(pc => pc.IsDepleted).ToList();

            foreach (var pc in depleted)
            {
                MoveDepletedCard(pc, player);
            }
        }

        /// <summary>
        /// 检查并移除所有耗尽的卡牌到墓地（含区域移动）
        /// </summary>
        public void CheckDepletedCards(Player player, ZoneManager zoneManager)
        {
            var pool = GetPool(player);
            var depleted = pool.PooledCards.Where(pc => pc.IsDepleted).ToList();

            foreach (var pc in depleted)
            {
                pool.PooledCards.Remove(pc);

                // 资源枯竭不可逆：耗尽标记永久跟随卡（回收后不可再作地牌）
                pc.SourceCard.WasDepletedAsLand = true;
                pc.SourceCard.SetRemainingLandTokens(null);

                PublishEvent(new ElementPoolDepleteEvent
                {
                    Player = player,
                    DepletedCard = pc.SourceCard
                });

                // 将耗尽卡牌移到墓地
                if (zoneManager != null)
                    zoneManager.MoveCard(pc.SourceCard, player, Zone.ElementPool, Zone.Graveyard);

                OnCardDepleted?.Invoke(pc.SourceCard, player);
            }
        }

        // ======================================== 查询 ========================================

        public int GetAvailableManaCount(ManaType type, Player player)
        {
            var pool = GetPool(player);
            return pool.AvailableMana.ContainsKey(type) ? pool.AvailableMana[type] : 0;
        }

        public int GetTotalAvailableMana(Player player)
        {
            return GetPool(player).AvailableMana.Values.Sum();
        }

        public List<PooledCard> GetPooledCards(Player player)
        {
            return GetPool(player).PooledCards.ToList();
        }

        public int GetTotalTokensInPool(Player player)
        {
            return GetPool(player).PooledCards.Sum(pc => pc.TotalTokenCount);
        }

        // ======================================== 内部方法 ========================================

        private void MoveDepletedCard(PooledCard pooledCard, Player player)
        {
            var pool = GetPool(player);
            pool.PooledCards.Remove(pooledCard);

            // 资源枯竭不可逆：耗尽标记永久跟随卡（回收后不可再作地牌）
            pooledCard.SourceCard.WasDepletedAsLand = true;
            pooledCard.SourceCard.SetRemainingLandTokens(null);

            PublishEvent(new ElementPoolDepleteEvent
            {
                Player = player,
                DepletedCard = pooledCard.SourceCard
            });

            OnCardDepleted?.Invoke(pooledCard.SourceCard, player);
        }

        /// <summary>
        /// 从卡牌读取费用，转换为指示物
        /// </summary>
        private Dictionary<ManaType, int> GetCardCostAsTokens(Card card)
        {
            var tokens = new Dictionary<ManaType, int>();

            // 从 IHasCost 接口读取费用
            if (card is IHasCost hasCost && hasCost.Cost != null)
            {
                foreach (var kvp in hasCost.Cost)
                {
                    ManaType type = (ManaType)kvp.Key;
                    int amount = (int)kvp.Value;
                    if (amount > 0)
                    {
                        tokens[type] = amount;
                    }
                }
            }

            // 如果卡牌没有费用（灰色1点），给一个默认灰色指示物
            if (tokens.Values.Sum() == 0)
            {
                tokens[ManaType.Gray] = 1;
            }

            return tokens;
        }

        // ======================================== 重置/工具 ========================================

        public void Reset()
        {
            foreach (var pool in _playerPools.Values)
            {
                pool.PooledCards.Clear();
                pool.Curve = null;
                pool.GlobalTurnIndex = 0;
                pool.PersonalTurnIndex = 0;
                pool.TapsThisTurn = 0;
                foreach (ManaType mana in Enum.GetValues(typeof(ManaType)))
                {
                    pool.AvailableMana[mana] = 0;
                }
            }
        }

        public string GetStatusString(Player player)
        {
            var pool = GetPool(player);
            var result = $"元素池 (地牌 {pool.PooledCards.Count}/{GetLandCap(player)}):\n";

            foreach (var pc in pool.PooledCards)
            {
                var name = (pc.SourceCard as IHasName)?.CardName ?? pc.SourceCard.ID;
                var tokenStr = string.Join(", ", pc.Tokens.Where(kv => kv.Value > 0).Select(kv => $"{kv.Key}:{kv.Value}"));
                var tapStr = pc.IsTapped ? "（已横置）" : "";
                result += $"  [{name}]{tapStr} 指示物: {tokenStr}\n";
            }

            var manaStr = string.Join(", ", pool.AvailableMana.Where(kv => kv.Value > 0).Select(kv => $"{kv.Key}:{kv.Value}"));
            result += $"积攒费用(bank): {manaStr}\n";
            result += $"本回合产出: {pool.TapsThisTurn}";

            return result;
        }

        private void PublishEvent<T>(T e) where T : IGameEvent
        {
            EventManager.Instance.Publish(e);
        }
    }

    // ======================================== 元素池事件 ========================================

    public class ElementPoolAddEvent : GameEventBase
    {
        public Player Player { get; set; }
        public Card AddedCard { get; set; }
        public Dictionary<ManaType, int> Tokens { get; set; }
    }

    public class ElementPoolGainEvent : GameEventBase
    {
        public Player Player { get; set; }
        public ManaType GainedType { get; set; }
        public Card FromCard { get; set; }
    }

    public class ElementPoolPayEvent : GameEventBase
    {
        public Player Player { get; set; }
        public Dictionary<int, float> PaidCost { get; set; }
    }

    public class ElementPoolDepleteEvent : GameEventBase
    {
        public Player Player { get; set; }
        public Card DepletedCard { get; set; }
    }

    /// <summary>
    /// 资源曲线变更事件（设置/变速效果修改曲线时发布）。
    /// 经 GameCore 统一路由，Trigger/Layer 引擎可见 —— 未来变速卡牌的挂点。
    /// </summary>
    public class CurveShiftEvent : GameEventBase
    {
        public Player Player { get; set; }
        public ResourceCurve NewCurve { get; set; }
        /// <summary>变更原因（InitGame / 变速效果 ID 等，调试用）</summary>
        public string Reason { get; set; }
    }

    // 保留旧事件名的兼容性
    public class ElementPoolConsumeEvent : GameEventBase
    {
        public Player Player { get; set; }
        public ManaType ManaType { get; set; }
        public int Amount { get; set; }
        public Effect Source { get; set; }
    }
}
