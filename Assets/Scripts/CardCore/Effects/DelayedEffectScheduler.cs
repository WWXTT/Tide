using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using CardCore.Attribute;

namespace CardCore
{
    /// <summary>
    /// 延迟效果调度器。
    /// DelayedEffect handler が登録した (controller, source, 子効果, 発火タイミング) を保持し、
    /// 回合终了 / 相位终了 に一致したものを 1 回限り解決する。
    /// GameCore の OnTurnEnded / OnPhaseEnded から OnTurnEnd / OnPhaseEnd を呼び出して駆動する。
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

        /// <summary>延迟效果を登録する</summary>
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

        /// <summary>回合终了で "TurnEnd" タイミングの効果を解決する</summary>
        public UniTask OnTurnEnd(Player turnPlayer)
        {
            return Fire("TurnEnd", e => e.Controller == null || e.Controller == turnPlayer);
        }

        /// <summary>相位终了で "PhaseEnd" タイミングの効果を解決する</summary>
        public UniTask OnPhaseEnd()
        {
            return Fire("PhaseEnd", _ => true);
        }

        private async UniTask Fire(string timing, System.Func<Entry, bool> extra)
        {
            // 解決中の再登録に備えてスナップショットで走査
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

        /// <summary>全延迟效果を破棄する（局終了/リセット用）</summary>
        public void Clear() => _entries.Clear();
    }
}
