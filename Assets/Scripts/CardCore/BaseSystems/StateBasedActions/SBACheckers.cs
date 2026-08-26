using System;
using System.Collections.Generic;
using System.Linq;


namespace CardCore
{
    /// <summary>
    /// 状态动作检查器接口
    /// </summary>
    public interface ISBAChecker
    {
        /// <summary>
        /// 检查并添加状态动作
        /// </summary>
        void Check(StateBasedActions sba, GameCore gameCore);
    }

    /// <summary>
    /// 玩家生命值归零检查器
    /// </summary>
    public class ZeroLifeChecker : ISBAChecker
    {
        public void Check(StateBasedActions sba, GameCore gameCore)
        {
            if (gameCore.Player1.Life <= 0)
            {
                sba.AddAction(new SBAActionRecord
                {
                    Type = SBAActionType.ZeroLife,
                    AffectedEntity = gameCore.Player1
                });
            }
            if (gameCore.Player2.Life <= 0)
            {
                sba.AddAction(new SBAActionRecord
                {
                    Type = SBAActionType.ZeroLife,
                    AffectedEntity = gameCore.Player2
                });
            }
        }
    }

    /// <summary>
    /// 单位防御力归零检查器
    /// </summary>
    public class ZeroToughnessChecker : ISBAChecker
    {
        public void Check(StateBasedActions sba, GameCore gameCore)
        {
            foreach (var player in new[] { gameCore.Player1, gameCore.Player2 })
            {
                var battlefield = gameCore.ZoneManager.GetCards(player, Zone.Battlefield);
                foreach (var card in battlefield)
                {
                    // 按层引擎计算的当前防御判定（连续/静态防御增益参与）
                    if (card is IHasLife && card.IsAlive &&
                        gameCore.LayerEngine.CalculateToughness(card) <= 0)
                    {
                        sba.AddAction(new SBAActionRecord
                        {
                            Type = SBAActionType.ZeroToughness,
                            AffectedEntity = card
                        });
                    }
                }
            }
        }
    }

    /// <summary>
    /// 区域变更检查器：通过区域快照对比侦测卡牌迁移。
    /// ZoneManager.MoveCard 本身不发布事件，故由此 SBA 统一补发 CardZoneChangeEvent，
    /// 使「离开/进入区域」类触发式得以观察到区域变化。
    /// </summary>
    public class ZoneChangeChecker : ISBAChecker
    {
        private static readonly Zone[] AllZones = (Zone[])Enum.GetValues(typeof(Zone));

        // 上一次检查时每张卡的所在（控制者, 区域）。首次见到的卡仅建立基线，不触发事件。
        private readonly Dictionary<Card, (Player owner, Zone zone)> _lastSeen
            = new Dictionary<Card, (Player owner, Zone zone)>();

        public void Check(StateBasedActions sba, GameCore gameCore)
        {
            var current = new Dictionary<Card, (Player owner, Zone zone)>();
            foreach (var player in new[] { gameCore.Player1, gameCore.Player2 })
            {
                if (player == null) continue;
                foreach (var zone in AllZones)
                {
                    foreach (var card in gameCore.ZoneManager.GetCards(player, zone))
                        current[card] = (player, zone);
                }
            }

            foreach (var kv in current)
            {
                if (_lastSeen.TryGetValue(kv.Key, out var prev) &&
                    (prev.zone != kv.Value.zone || prev.owner != kv.Value.owner))
                {
                    sba.AddAction(new SBAActionRecord
                    {
                        Type = SBAActionType.ZoneChange,
                        AffectedEntity = kv.Key,
                        OldValue = prev.zone,
                        NewValue = kv.Value.zone
                    });
                }
            }

            _lastSeen.Clear();
            foreach (var kv in current)
                _lastSeen[kv.Key] = kv.Value;
        }
    }

    /// <summary>
    /// 文本变更检查器。
    /// 当前 Card 数据模型不含规则文本/可变文本字段，无可对比的文本源，故为有意的空实现，
    /// 也不在 GameCore 注册。待模型引入文本字段后再补实现。
    /// </summary>
    public class TextChangeChecker : ISBAChecker
    {
        public void Check(StateBasedActions sba, GameCore gameCore)
        {
        }
    }

    /// <summary>
    /// 特征变更检查器：+1/+1 与 -1/-1 指示物湮灭（MTG 规则 704.5q）。
    /// 同一永久物上同时存在两种指示物时成对抵消。
    /// </summary>
    public class CharacteristicChangeChecker : ISBAChecker
    {
        public void Check(StateBasedActions sba, GameCore gameCore)
        {
            foreach (var player in new[] { gameCore.Player1, gameCore.Player2 })
            {
                if (player == null) continue;
                foreach (var card in gameCore.ZoneManager.GetCards(player, Zone.Battlefield))
                {
                    if (card.GetCounterCount("+1/+1") > 0 && card.GetCounterCount("-1/-1") > 0)
                    {
                        sba.AddAction(new SBAActionRecord
                        {
                            Type = SBAActionType.CharacteristicChange,
                            AffectedEntity = card
                        });
                    }
                }
            }
        }
    }
}
