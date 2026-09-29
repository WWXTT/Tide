using System.Collections.Generic;
using UnityEngine;
namespace CardCore
{
    public class EffectResolutionContext
    {
        public bool IsNegated { get; private set; }

        public EffectLogic CurrentLogic { get; private set; }

        private List<IUndoAction> undoActions = new();

        public EffectResolutionContext()
        {
        }

        public EffectResolutionContext(EffectLogic originalLogic)
        {
            CurrentLogic = originalLogic;
        }

        // 注册回滚点
        public void RegisterUndo(IUndoAction undo)
        {
            undoActions.Add(undo);
        }

        // 效果被无效
        public void Negate()
        {
            IsNegated = true;
        }

        // 效果替代
        public void ReplaceLogic(EffectLogic newLogic)
        {
            CurrentLogic = newLogic;
        }

        // 回滚
        public void Rollback()
        {
            for (int i = undoActions.Count - 1; i >= 0; i--)
            {
                undoActions[i].Undo();
            }
        }
    }

    public interface IUndoAction
    {
        void Undo();
    }

    public class LifeChangeUndo : IUndoAction
    {
        Player player;
        int amount;

        public LifeChangeUndo(Player player, int amount)
        {
            this.player = player;
            this.amount = amount;
        }

        public void Undo()
        {
            player.Life += amount;
        }
    }

    public abstract class EffectLogic : ScriptableObject
    {
        public abstract void Execute(
            EffectInstance effect,
            EffectResolutionContext resolution
        );
    }

    public class DealDamageLogic : EffectLogic
    {
        public int Damage;

        public void SetParameter(float parameter)
        {
            Damage = (int)parameter;
        }

        public override void Execute(
            EffectInstance effect,
            EffectResolutionContext resolution)
        {
            var target = effect.Targets[0] as Player;

            // 记录 Undo
            resolution.RegisterUndo(
                new LifeChangeUndo(target, Damage)
            );

            target.Life -= Damage;
        }
    }

    public class ReplaceWithDrawLogic : EffectLogic
    {
        public EffectLogic NewLogic;

        public override void Execute(
            EffectInstance effect,
            EffectResolutionContext resolution)
        {
            resolution.ReplaceLogic(NewLogic);
        }
    }


}