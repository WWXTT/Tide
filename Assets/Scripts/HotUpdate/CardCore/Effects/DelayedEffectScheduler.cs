using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using CardCore.Attribute;

namespace CardCore
{
    /// <summary>
    /// 延迟效果调度器。
    /// 保存 DelayedEffect handler 登记下来的 (控制者, 来源, 子效果, 触发时机)，
    /// 在回合结束 / 阶段结束 时把匹配的条目结算一次。
    /// 由 GameCore 的 OnTurnEnded / OnPhaseEnded 调用 OnTurnEnd / OnPhaseEnd 驱动。
    /// </summary>
    public class DelayedEffectScheduler
    {
        private class Entry
        {
            public Player Controller;
            public Entity Source;
            public List<AtomicEffectInstance> SubEffects;
            public string FireTiming;       // "TurnEnd" / "PhaseEnd"
            public IGameEvent TriggeringEvent;
            public ZoneManager ZoneManager;
            public ElementPoolSystem ElementPool;
        }

        private readonly List<Entry> _entries = new List<Entry>();

        /// <summary>登记一条延迟效果</summary>
        public void Schedule(Player controller, Entity source, List<AtomicEffectInstance> subEffects,
            string fireTiming, IGameEvent triggeringEvent, ZoneManager zoneManager, ElementPoolSystem elementPool)
        {
            if (subEffects == null || subEffects.Count == 0) return;

            _entries.Add(new Entry
            {
                Controller = controller,
                Source = source,
                SubEffects = subEffects,
                FireTiming = string.IsNullOrEmpty(fireTiming) ? "TurnEnd" : fireTiming,
                TriggeringEvent = triggeringEvent,
                ZoneManager = zoneManager,
                ElementPool = elementPool,
            });
        }

        /// <summary>回合结束时结算 "TurnEnd" 时机的效果</summary>
        public UniTask OnTurnEnd(Player turnPlayer)
        {
            return Fire("TurnEnd", e => e.Controller == null || e.Controller == turnPlayer);
        }

        /// <summary>阶段结束时结算 "PhaseEnd" 时机的效果</summary>
        public UniTask OnPhaseEnd()
        {
            return Fire("PhaseEnd", _ => true);
        }

        private async UniTask Fire(string timing, System.Func<Entry, bool> extra)
        {
            // 结算过程中可能再登记——先取快照再遍历
            var due = _entries.Where(e => e.FireTiming == timing && extra(e)).ToList();
            foreach (var entry in due)
            {
                _entries.Remove(entry);
                foreach (var child in entry.SubEffects)
                {
                    var ctx = new EffectExecutionContext
                    {
                        Source = entry.Source,
                        Controller = entry.Controller,
                        Targets = new List<Entity>(),
                        TriggeringEvent = entry.TriggeringEvent,
                        ZoneManager = entry.ZoneManager,
                        ElementPool = entry.ElementPool,
                    };
                    await EffectHandlerRegistry.ExecuteEffectAsync(child, ctx);
                }
            }
        }

        /// <summary>清空全部延迟效果（局结束/重置用）</summary>
        public void Clear() => _entries.Clear();
    }
}
