using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;

namespace CardCore
{
    /// <summary>
    /// 召唤引擎 - 处理融合/同步/超量/链接召唤
    /// </summary>
    public class SummonEngine
    {
        private readonly GameCore _gameCore;

        public SummonEngine(GameCore gameCore)
        {
            _gameCore = gameCore;
        }

        #region 通用规则

        /// <summary>
        /// 检查素材是否有效（额外卡组怪兽不能作为额外卡组召唤的素材）
        /// </summary>
        private bool ValidateMaterialCard(Card card)
        {
            if (card is IIsExtraDeck isExtra)
                return !isExtra.IsExtraDeck;
            return true;
        }

        /// <summary>
        /// 验证所有素材是否有效
        /// </summary>
        private bool ValidateAllMaterials(List<Card> materials)
        {
            return materials.All(ValidateMaterialCard);
        }

        /// <summary>
        /// 将素材送入墓地并从战场移除
        /// </summary>
        private void SendMaterialsToGraveyard(Player player, List<Card> materials)
        {
            foreach (var mat in materials)
            {
                _gameCore.ZoneManager.MoveCard(mat, player, Zone.Battlefield, Zone.Graveyard);
            }
        }

        /// <summary>
        /// 从额外卡组创建卡牌实例并召唤到战场
        /// </summary>
        private Card SummonFromExtraDeck(Player player, CardData cardData)
        {
            // TODO: 实现从 CardData 创建 Card 实例的逻辑
            var card = new Card { ID = cardData.ID };
            _gameCore.ZoneManager.MoveCard(card, player, Zone.ExtraDeck, Zone.Battlefield);
            card.WasFormallySummoned = true; // 从额外组正式召唤

            // 发布召唤事件
            EventManager.Instance.Publish(new CardPutToBattlefieldEvent
            {
                Card = card,
                Controller = player,
                Tapped = false
            });

            return card;
        }

        /// <summary>
        /// 检查是否可以召唤
        /// </summary>
        public bool CanSummon(Player player, CardData card, SummonMethod method)
        {
            if (card == null) return false;

            var extraDeck = _gameCore.ZoneManager.GetCards(player, Zone.ExtraDeck);
            if (!extraDeck.Any(c => c.ID == card.ID))
                return false;

            return true;
        }

        #endregion

        #region 融合召唤

        /// <summary>
        /// 融合召唤
        /// 规则：必须使用至少一个带关键词的生物作为素材
        /// 融合生物继承素材的关键词，重复可叠加
        /// 若素材总费用 < 融合生物费用，支付剩余法力
        /// </summary>
        public Card FusionSummon(Player player, CardData fusionCard, List<Card> materials)
        {
            if (player == null)
                throw new ArgumentNullException(nameof(player));
            if (fusionCard == null)
                throw new ArgumentNullException(nameof(fusionCard));
            if (materials == null || materials.Count == 0)
                throw new GameRuleViolationException("融合召唤需要至少一个素材", "FUSION_NO_MATERIALS");

            // 1. 验证素材
            if (!ValidateFusionMaterials(fusionCard, materials))
                throw new GameRuleViolationException("融合素材验证失败", "FUSION_INVALID_MATERIALS");

            // 2. 收集并叠加关键词
            var keywordStack = CollectAndStackKeywords(materials);

            // 3. 将素材送入墓地
            SendMaterialsToGraveyard(player, materials);

            // 4. 从额外卡组召唤
            var fusionCreature = SummonFromExtraDeck(player, fusionCard);

            // 5. 继承叠加的关键词
            if (fusionCreature is IHasKeywords hasKeywords)
            {
                foreach (var kvp in keywordStack)
                {
                    for (int i = 0; i < kvp.Value; i++)
                    {
                        hasKeywords.AddKeyword(kvp.Key);
                    }
                }
            }

            // 6. 若素材总费用 < 融合生物费用，支付剩余法力
            int totalMaterialCost = materials.Sum(GetCardCost);
            int fusionCost = (int)(fusionCard.TotalCost);
            if (totalMaterialCost < fusionCost)
            {
                int remaining = fusionCost - totalMaterialCost;
                // TODO: 从玩家法力池扣除 remaining 法力
            }

            return fusionCreature;
        }

        /// <summary>
        /// 验证融合素材
        /// 必须使用至少一个带关键词的生物作为素材
        /// </summary>
        private bool ValidateFusionMaterials(CardData card, List<Card> materials)
        {
            if (card.Effects == null)
                return false;

            var materialData = card.Effects;

            // 额外卡组怪兽不能作为素材
            if (!ValidateAllMaterials(materials))
                return false;

            // 必须至少有一个带关键词的素材
            if (!materials.Any(m => m is IHasKeywords hasKw && hasKw.Keywords.Count > 0))
                return false;

            return true;
        }

        /// <summary>
        /// 收集素材的关键词并统计叠加数量
        /// </summary>
        private Dictionary<string, int> CollectAndStackKeywords(List<Card> materials)
        {
            var keywordStack = new Dictionary<string, int>();
            foreach (var mat in materials)
            {
                if (mat is IHasKeywords hasKeywords)
                {
                    foreach (var keyword in hasKeywords.Keywords)
                    {
                        if (!keywordStack.ContainsKey(keyword))
                            keywordStack[keyword] = 0;
                        keywordStack[keyword]++;
                    }
                }
            }
            return keywordStack;
        }

        #endregion

        #region 同步召唤

        /// <summary>
        /// 同步召唤
        /// 规则：至少1个调整(Tuner) + 至少1个非调整生物
        /// 非调整总费用 >= 同步生物费用
        /// 调整生物的速度赋予同步怪兽
        /// </summary>
        public Card SynchroSummon(Player player, CardData synchroCard, Card tuner, List<Card> nonTuners)
        {
            if (player == null)
                throw new ArgumentNullException(nameof(player));
            if (tuner == null)
                throw new ArgumentNullException(nameof(tuner));

            var allMaterials = new List<Card> { tuner };
            allMaterials.AddRange(nonTuners);

            // 1. 验证
            if (!ValidateSynchroMaterials(synchroCard, tuner, nonTuners, allMaterials))
                throw new GameRuleViolationException("同步召唤素材验证失败", "SYNCHRO_INVALID_MATERIALS");

            // 2. 获取调整生物的速度
            int tunerSpeed = GetTunerSpeed(tuner);

            // 3. 将素材送入墓地
            SendMaterialsToGraveyard(player, allMaterials);

            // 4. 从额外卡组召唤
            var synchroCreature = SummonFromExtraDeck(player, synchroCard);

            // 5. 速度继承：同步怪兽的发动速度 = max(基础速度, 调整生物速度)
            // TODO: 将调整生物速度赋予同步怪兽（需要在 Card 上添加速度属性）

            // 6. 发布事件
            EventManager.Instance.Publish(new CardPutToBattlefieldEvent
            {
                Card = synchroCreature,
                Controller = player,
                Tapped = false
            });

            return synchroCreature;
        }

        /// <summary>
        /// 验证同步素材
        /// </summary>
        private bool ValidateSynchroMaterials(CardData card, Card tuner, List<Card> nonTuners, List<Card> allMaterials)
        {
            if (card.Effects == null)
                return false;

            var materialData = card.Effects;

            // 额外卡组怪兽不能作为素材
            if (!ValidateAllMaterials(allMaterials))
                return false;

            // 检查需要协调者
           

            // 检查至少有一个非调整生物
            if (nonTuners.Count == 0)
                return false;

            // 检查非调整总费用 >= 同步生物费用
          

            // 检查基本过滤器
            

            return true;
        }

        /// <summary>
        /// 获取调整生物的效果发动速度
        /// </summary>
        private int GetTunerSpeed(Card tuner)
        {
            // TODO: 从调整生物的属性或效果中获取速度
            return 0;
        }

        #endregion

        #region 超量召唤

        /// <summary>
        /// 超量召唤
        /// 规则：超量怪兽无法力值费用
        /// 任意场上生物可作为素材，攻击力/生命值 = 素材对应属性总和
        /// 发动效果时可选：支付法力 或 拔除超量素材
        /// </summary>
        public Card XyzSummon(Player player, CardData xyzCard, List<Card> materials)
        {
            if (player == null)
                throw new ArgumentNullException(nameof(player));
            if (materials == null || materials.Count == 0)
                throw new GameRuleViolationException("超量召唤需要至少一个素材", "XYZ_NO_MATERIALS");

            // 1. 验证素材
            if (!ValidateXyzMaterials(xyzCard, materials))
                throw new GameRuleViolationException("超量召唤素材验证失败", "XYZ_INVALID_MATERIALS");

            // 2. 计算素材属性总和
            int totalPower = materials.Sum(GetCardPower);
            int totalLife = materials.Sum(GetCardLife);

            // 3. 将素材从战场移除（不送入墓地，叠放在超量怪兽下）
            foreach (var mat in materials)
            {
                _gameCore.ZoneManager.MoveCard(mat, player, Zone.Battlefield, Zone.None);
            }

            // 4. 从额外卡组召唤
            var xyzCreature = SummonFromExtraDeck(player, xyzCard);

            // 5. 设置动态属性
            if (xyzCreature is IHasPower hasPower)
                hasPower.Power = totalPower;
            if (xyzCreature is IHasLife hasLife)
                hasLife.Life = totalLife;

            // 6. 叠放超量素材
            if (xyzCreature is IXyzHolder xyzHolder)
            {
                xyzHolder.XyzMaterials = new List<Card>(materials);
            }

            return xyzCreature;
        }

        /// <summary>
        /// 验证超量素材（任意场上生物即可）
        /// </summary>
        private bool ValidateXyzMaterials(CardData card, List<Card> materials)
        {
            if (card == null || materials == null || materials.Count == 0)
                return false;

            // 额外卡组怪兽不能作为素材
            if (!ValidateAllMaterials(materials))
                return false;

            // 检查素材数量
         

            return true;
        }

        /// <summary>
        /// 超量效果双重支付选择
        /// 返回 true 表示使用素材支付，false 表示使用法力支付
        /// </summary>
        public bool PayWithMaterialOrMana(Player player, Card xyzCard, int cost)
        {
            if (xyzCard is not IXyzHolder xyzHolder || xyzHolder.XyzMaterials.Count == 0)
                return false;

            // 使用素材替代法力支付
            xyzHolder.XyzMaterials.RemoveAt(0);
            return true;
        }

        #endregion

        #region 链接召唤

        /// <summary>
        /// 链接召唤
        /// 规则：必须有增益和减益效果
        /// 至少一个带箭头方向的生物作为素材
        /// 素材总费用 >= 链接生物费用
        /// 链接值 = 素材箭头方向总数（去重后的方向数）
        /// 六边形战场，继承素材箭头方向
        /// </summary>
        public Card LinkSummon(Player player, CardData linkCard, List<Card> materials)
        {
            if (player == null)
                throw new ArgumentNullException(nameof(player));
            if (materials == null || materials.Count == 0)
                throw new GameRuleViolationException("链接召唤需要至少一个素材", "LINK_NO_MATERIALS");

            // 1. 验证素材
            if (!ValidateLinkMaterials(linkCard, materials))
                throw new GameRuleViolationException("链接召唤素材验证失败", "LINK_INVALID_MATERIALS");

            // 2. 收集素材的箭头方向
            var collectedDirections = HexDirection.None;

            // 3. 将素材送入墓地
            SendMaterialsToGraveyard(player, materials);

            // 4. 从额外卡组召唤
            var linkCreature = SummonFromExtraDeck(player, linkCard);

            // 5. 设置箭头方向
            if (linkCreature is IHasLinkArrows hasArrows)
            {
                hasArrows.ArrowDirections = collectedDirections;
            }

            // 6. 计算链接值
            int linkRating = CountDirections(collectedDirections);

            // TODO: 设置链接值到 linkCreature

            return linkCreature;
        }

        /// <summary>
        /// 验证链接素材
        /// </summary>
        private bool ValidateLinkMaterials(CardData card, List<Card> materials)
        {
            if (card == null || materials == null || materials.Count == 0)
                return false;

            // 额外卡组怪兽不能作为素材
            if (!ValidateAllMaterials(materials))
                return false;

            // 必须至少一个带箭头方向的素材
            if (!materials.Any(m => m is IHasLinkArrows hasArrows && hasArrows.ArrowDirections != HexDirection.None))
                return false;

            // 检查素材总费用 >= 链接生物费用
         

            return true;
        }

        /// <summary>
        /// 计算去重后的方向数
        /// </summary>
        private static int CountDirections(HexDirection directions)
        {
            int count = 0;
            while (directions != HexDirection.None)
            {
                var lowest = directions & ~(directions - 1);
                directions &= lowest;
                count++;
            }
            return count;
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 获取卡牌的费用
        /// </summary>
        private static int GetCardCost(Card card)
        {
            if (card is IHasCost hasCost && hasCost.Cost != null)
            {
                int total = 0;
                foreach (var kvp in hasCost.Cost)
                    total += (int)kvp.Value;
                return total;
            }
            return 0;
        }

        /// <summary>
        /// 获取卡牌的攻击力
        /// </summary>
        private static int GetCardPower(Card card)
        {
            return card is IHasPower hasPower ? hasPower.Power : 0;
        }

        /// <summary>
        /// 获取卡牌的生命值
        /// </summary>
        private static int GetCardLife(Card card)
        {
            return card is IHasLife hasLife ? hasLife.Life : 0;
        }

        /// <summary>
        /// 获取卡牌的等级值
        /// </summary>
        public static int GetLevel(Card card)
        {
            if (card is IHasLevel hasLevel)
                return hasLevel.Level ?? hasLevel.Rank ?? 0;
            return 0;
        }

        #endregion

        #region 素材选择（交互）

        // 注：以下交互包装仅负责「让玩家选素材」并委托既有召唤方法。
        // 「触发召唤的 trigger 本身」与各特召规则细节（速度继承/箭头/超量素材叠放等）
        // 仍为 UI/规则后续补齐项，本批次不在范围内。

        /// <summary>本方战场上可作素材的候选（排除额外卡组怪兽）。</summary>
        private List<Entity> GetMaterialCandidates(Player player)
        {
            var field = _gameCore.ZoneManager.GetCards(player, Zone.Battlefield);
            if (field == null) return new List<Entity>();
            return field.Where(ValidateMaterialCard).Cast<Entity>().ToList();
        }

        private async UniTask<List<Card>> ChooseMaterialsAsync(Player player, string title, int min, int max)
        {
            var candidates = GetMaterialCandidates(player);
            if (candidates.Count == 0) return new List<Card>();

            var chosen = await TargetSelectionService.RequestAsync(new TargetSelectionRequest
            {
                Candidates = candidates,
                MinCount = Math.Min(min, candidates.Count),
                MaxCount = Math.Min(max, candidates.Count),
                Chooser = player,
                Title = title,
                Hint = "选择召唤素材",
                AllowCancel = true,
            });
            return chosen.OfType<Card>().ToList();
        }

        /// <summary>融合召唤（交互）：玩家从战场选素材后委托 FusionSummon。失败返回 null。</summary>
        public async UniTask<Card> FusionSummonInteractiveAsync(Player player, CardData fusionCard)
        {
            var materials = await ChooseMaterialsAsync(player, "融合素材", 1, int.MaxValue);
            if (materials.Count == 0) return null;
            try { return FusionSummon(player, fusionCard, materials); }
            catch (GameRuleViolationException) { return null; }
        }

        /// <summary>超量召唤（交互）。失败返回 null。</summary>
        public async UniTask<Card> XyzSummonInteractiveAsync(Player player, CardData xyzCard)
        {
            var materials = await ChooseMaterialsAsync(player, "超量素材", 1, int.MaxValue);
            if (materials.Count == 0) return null;
            try { return XyzSummon(player, xyzCard, materials); }
            catch (GameRuleViolationException) { return null; }
        }

        /// <summary>链接召唤（交互）。失败返回 null。</summary>
        public async UniTask<Card> LinkSummonInteractiveAsync(Player player, CardData linkCard)
        {
            var materials = await ChooseMaterialsAsync(player, "链接素材", 1, int.MaxValue);
            if (materials.Count == 0) return null;
            try { return LinkSummon(player, linkCard, materials); }
            catch (GameRuleViolationException) { return null; }
        }

        /// <summary>
        /// 同步召唤（交互）：两段选择——先选 1 个调整(Tuner)，再选 N 个非调整。
        /// 注：缺少可靠的 Tuner 判定，本实现把第一段选中的素材当作 Tuner（UI 后续补判定）。
        /// </summary>
        public async UniTask<Card> SynchroSummonInteractiveAsync(Player player, CardData synchroCard)
        {
            var tunerPick = await ChooseMaterialsAsync(player, "选择调整(Tuner)", 1, 1);
            if (tunerPick.Count == 0) return null;
            var tuner = tunerPick[0];

            // 非调整候选：战场素材去掉已选 tuner
            var nonTunerCandidates = GetMaterialCandidates(player)
                .OfType<Card>()
                .Where(c => c != tuner)
                .Cast<Entity>()
                .ToList();
            if (nonTunerCandidates.Count == 0) return null;

            var picked = await TargetSelectionService.RequestAsync(new TargetSelectionRequest
            {
                Candidates = nonTunerCandidates,
                MinCount = 1,
                MaxCount = nonTunerCandidates.Count,
                Chooser = player,
                Title = "选择非调整素材",
                Hint = "选择非调整素材",
                AllowCancel = true,
            });
            var nonTuners = picked.OfType<Card>().ToList();
            if (nonTuners.Count == 0) return null;

            try { return SynchroSummon(player, synchroCard, tuner, nonTuners); }
            catch (GameRuleViolationException) { return null; }
        }

        #endregion
    }

    #region 链接系统接口

    /// <summary>
    /// 具有链接箭头的接口
    /// </summary>
    public interface IHasLinkArrows
    {
        HexDirection ArrowDirections { get; set; }
    }

    /// <summary>
    /// 超量素材持有者接口
    /// </summary>
    public interface IXyzHolder
    {
        List<Card> XyzMaterials { get; set; }
    }

    #endregion
}
