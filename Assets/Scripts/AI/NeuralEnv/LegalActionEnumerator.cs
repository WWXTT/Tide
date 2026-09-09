using System;
using System.Collections.Generic;
using CardCore;
using CardCore.Attribute;

namespace CardCore.AI.NeuralEnv
{
    /// <summary>动作类型（离散下标 → 桥接映射回 GameActions.*）。</summary>
    public enum TideActionType : int
    {
        PlayCard = 0,            // 手牌打出（含抉择模式）
        PlayLand = 1,            // 手牌放入元素池（当地产元素）
        Activate = 3,            // 效果发动：战场卡的激活式（Activate_*）能力
        Attack = 4,              // 攻击宣言（attacker → 对方随从 / 对方玩家）
        EndTurn = 5,             // 结束回合
        // 2 = 墓地出牌（已移除 2026-09-09）：依赖归土仪典仪式光环，RL 标准池不含仪式卡，
        //     枚举出的动作引擎必拒（口径漂移白耗动作配额）。引擎侧 GameActions.PlayCardFromGraveyard
        //     保留（仪式机制/UI 走它），只是不再进动作空间。
    }

    /// <summary>一个可执行动作：模型输出离散下标 → 桥接按此结构调用 GameActions.*。</summary>
    public struct TideAction
    {
        public TideActionType Type;
        public Card Card;                 // 来源卡（PlayCard/PlayLand/Activate 的来源；EndTurn 为 null）
        public Entity Target;             // 攻击目标（对方随从或对方玩家）；其余为 null
        public EffectDefinition Effect;   // Activate 的激活式效果定义
        public int ModeIndex;             // 抉择模式下标（非抉择卡恒 0）

        /// <summary>来源卡在 cards_ 中的槽位（-1 = 无）。</summary>
        public int SourceIndex;
        /// <summary>目标在 cards_ 中的槽位（-1 = 无 / 对方玩家）。</summary>
        public int TargetIndex;

        public override string ToString()
        {
            string t = Target is Card tc ? tc.ID : (Target is Player ? "(player)" : "-");
            return $"{Type}({Card?.ID ?? "-"} → {t}, mode={ModeIndex})";
        }

        /// <summary>
        /// 动作签名（Type|来源卡|效果Id|目标|模式）：跨枚举稳定（EffectDefinition 每次枚举都是新对象，
        /// 引用判等不可用；Id 是转换自卡表的稳定键）。供「本回合摘除无效动作」判重。
        /// </summary>
        public string Signature
        {
            get
            {
                string t = Target is Card tc ? tc.ID : (Target is Player ? "P" : "-");
                return $"{(int)Type}|{Card?.ID ?? "-"}|{Effect?.Id ?? "-"}|{t}|{ModeIndex}";
            }
        }
    }

    /// <summary>
    /// 合法动作枚举器：把 Tide 的动作来源 + 战斗收敛成扁平离散列表（每步枚举 → 固定下标 → 模型输出离散 int）。
    /// 合法性谓词复用引擎权威判定，与 SimpleAI 同口径。
    ///
    /// v1 简化（文档化，后续再收敛）：
    ///  - 横置地牌由 driver 回合初自动完成（复用 SimpleAI.TapAllLands 的色匹配），不建模——v2 可把 tap 也纳入动作空间。
    ///  - 出牌/施放目标由引擎自动解析（PlayCard 传 null targets）——目标枚举留到 v2。
    ///  - 枚举期间惰性 StartCombat（镜像 SimpleAI.BeginCombat），使攻击动作可被枚举；driver 在 EndTurn 前 ResolveCombat。
    /// </summary>
    public sealed class LegalActionEnumerator
    {
        public List<TideAction> Actions = new List<TideAction>();
        /// <summary>动作特征张量（Actions.Count × TideObservation.NAction），供模型输入。</summary>
        public float[] Features = Array.Empty<float>();

        public int Count => Actions.Count;

        /// <summary>枚举当前回合玩家的全部合法动作（含 EndTurn 兜底）。</summary>
        public void Enumerate(GameCore core, Player me)
        {
            Actions.Clear();
            Features = Array.Empty<float>();
            if (core == null || me == null || core.TurnEngine.TurnPlayer != me) return;
            if (core.IsGameOver) return;

            if (core.TurnEngine.CurrentPhase?.Phase != PhaseType.Main)
            {
                // 非主阶段：driver 不应在此刻询问，兜底只给 EndTurn。
                Actions.Add(EndTurnAction());
                BuildFeatures();
                return;
            }

            var opp = me.Opponent;
            var zm = core.ZoneManager;

            EnumeratePlayLand(core, me, zm);
            EnumeratePlayCard(core, me, zm);
            EnumerateActivate(core, me, zm);
            EnumerateAttack(core, me, opp, zm);
            Actions.Add(EndTurnAction());

            BuildFeatures();
        }

        /// <summary>把动作映射回引擎调用。返回 false 表示引擎拒绝（枚举口径漂移时的保守兜底，driver 据此重观测）。</summary>
        public static bool Apply(GameCore core, Player me, TideAction a)
        {
            switch (a.Type)
            {
                case TideActionType.PlayCard:
                    return GameActions.PlayCard(core, me, a.Card, null, Zone.Hand, a.ModeIndex);
                case TideActionType.PlayLand:
                    return GameActions.AddToElementPool(core, me, a.Card);
                case TideActionType.Activate:
                    return GameActions.ActivateEffect(core, me, a.Effect, a.Card);
                case TideActionType.Attack:
                    return GameActions.DeclareAttack(core, me, a.Card, a.Target);
                case TideActionType.EndTurn:
                    return GameActions.EndTurn(core, me);
                default:
                    return false;
            }
        }

        /// <summary>
        /// 按签名摘除动作（本回合内引擎已拒绝/无进展的动作不再出现在选项表里——「只给可以操作的选项」），
        /// 特征张量同步重建。EndTurn 永远不会被摘除（driver 只对非 EndTurn 动作记签名）。
        /// </summary>
        public void RemoveAll(HashSet<string> banned)
        {
            if (banned == null || banned.Count == 0 || Actions.Count == 0) return;
            Actions.RemoveAll(a => a.Type != TideActionType.EndTurn && banned.Contains(a.Signature));
            BuildFeatures();
        }

        // =====================================================

        private static TideAction EndTurnAction() => new TideAction { Type = TideActionType.EndTurn };

        private void EnumeratePlayLand(GameCore core, Player me, ZoneManager zm)
        {
            int cap = core.ElementPool.GetLandCap(me);
            if (core.ElementPool.GetPooledCards(me).Count >= cap) return;
            var hand = zm.GetCards(me, Zone.Hand);
            for (int i = 0; i < hand.Count; i++)
            {
                var c = hand[i];
                if (!ElementPoolSystem.CanServeAsLand(c)) continue;
                Actions.Add(new TideAction
                {
                    Type = TideActionType.PlayLand,
                    Card = c,
                    SourceIndex = TideObservation.CardIndex(me, me, Zone.Hand, i),
                    TargetIndex = -1,
                });
            }
        }

        private void EnumeratePlayCard(GameCore core, Player me, ZoneManager zm)
        {
            var hand = zm.GetCards(me, Zone.Hand);
            for (int i = 0; i < hand.Count; i++)
            {
                var c = hand[i];
                if (!RuleHooks.CanPlay(core, me, c, Zone.Hand)) continue;

                int modes = ModeCount(c);
                for (int m = 0; m < modes; m++)
                {
                    if (!CanAfford(core, me, GameActions.GetCardCost(c, m))) continue;
                    if (!IsSpell(c) && !zm.HasBattlefieldSpace(me)) continue;

                    Actions.Add(new TideAction
                    {
                        Type = TideActionType.PlayCard,
                        Card = c,
                        ModeIndex = m,
                        SourceIndex = TideObservation.CardIndex(me, me, Zone.Hand, i),
                        TargetIndex = -1,
                    });
                }
            }
        }

        private void EnumerateActivate(GameCore core, Player me, ZoneManager zm)
        {
            var executor = core.StackEngine.GetExecutor();
            var phase = core.TurnEngine.CurrentPhase.Phase;
            int turn = core.TurnEngine.TurnNumber;

            var bf = zm.GetCards(me, Zone.Battlefield);
            for (int i = 0; i < bf.Count; i++)
            {
                var src = bf[i];
                foreach (var def in GetEffectDefinitions(src))
                {
                    if (!def.IsActivatedEffect) continue;
                    if (!executor.CanActivate(def, src, me, me, phase, turn)) continue;

                    Actions.Add(new TideAction
                    {
                        Type = TideActionType.Activate,
                        Card = src,
                        Effect = def,
                        SourceIndex = TideObservation.CardIndex(me, me, Zone.Battlefield, i),
                        TargetIndex = -1,
                    });
                }
            }
        }

        private void EnumerateAttack(GameCore core, Player me, Player opp, ZoneManager zm)
        {
            // 惰性进入战斗（镜像 SimpleAI.BeginCombat）：主阶段随时可攻击，进战斗不阻挡出牌/发动。
            if (!core.CombatSystem.InCombat)
                core.CombatSystem.StartCombat(me, opp);

            var bf = zm.GetCards(me, Zone.Battlefield);
            var oppBf = zm.GetCards(opp, Zone.Battlefield);
            for (int i = 0; i < bf.Count; i++)
            {
                var atk = bf[i];
                if (!core.CombatSystem.CanDeclareAttack(atk, me)) continue;

                for (int j = 0; j < oppBf.Count; j++)
                {
                    var t = oppBf[j];
                    if (!core.CombatSystem.CanAttackTarget(atk, t)) continue;
                    Actions.Add(new TideAction
                    {
                        Type = TideActionType.Attack,
                        Card = atk,
                        Target = t,
                        SourceIndex = TideObservation.CardIndex(me, me, Zone.Battlefield, i),
                        TargetIndex = TideObservation.CardIndex(me, opp, Zone.Battlefield, j),
                    });
                }

                if (core.CombatSystem.CanAttackTarget(atk, opp))
                {
                    Actions.Add(new TideAction
                    {
                        Type = TideActionType.Attack,
                        Card = atk,
                        Target = opp, // 对方玩家（无卡槽 → TargetIndex = -1）
                        SourceIndex = TideObservation.CardIndex(me, me, Zone.Battlefield, i),
                        TargetIndex = -1,
                    });
                }
            }
        }

        private void BuildFeatures()
        {
            int n = Actions.Count;
            Features = new float[n * TideObservation.NAction];
            for (int k = 0; k < n; k++)
            {
                var a = Actions[k];
                int b = k * TideObservation.NAction;
                Features[b + 0] = 1f; // valid
                Features[b + 1] = (int)a.Type;
                Features[b + 2] = a.SourceIndex;
                Features[b + 3] = a.TargetIndex;
                Features[b + 4] = a.ModeIndex;
                Features[b + 5] = ActionCost(a);
            }
        }

        private static float ActionCost(TideAction a)
        {
            switch (a.Type)
            {
                case TideActionType.PlayCard:
                    float s = 0f;
                    foreach (var v in GameActions.GetCardCost(a.Card, a.ModeIndex).Values) s += v;
                    return s;
                default:
                    return 0f;
            }
        }

        // ===================================================== 辅助谓词 =====================================================

        private static int ModeCount(Card c)
            => c is CardWrapper w ? Math.Max(1, CostDerivationService.GetModeCount(w.GetData())) : 1;

        private static bool IsSpell(Card c)
            => c is IHasSupertype s && s.Supertype == Cardtype.Spell;

        /// <summary>费用可付（对齐 GameActions.CanAfford 的两条门槛：总额 ≤ 地牌槽上限 + 分色充足）。</summary>
        private static bool CanAfford(GameCore core, Player me, Dictionary<int, float> cost)
        {
            float total = 0f;
            foreach (var v in cost.Values) total += v;
            if (total > core.ElementPool.GetLandCap(me)) return false;
            return core.ElementPool.CanPayCost(cost, me);
        }

        private static List<EffectDefinition> GetEffectDefinitions(Card card)
        {
            if (!(card is CardWrapper wrapper)) return new List<EffectDefinition>();
            var data = wrapper.GetData();
            if (data?.Effects == null || data.Effects.Count == 0) return new List<EffectDefinition>();
            return CardEffectConverter.ConvertAll(data.Effects, data.ID);
        }
    }
}
