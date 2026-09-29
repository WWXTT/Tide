using CardCore;

namespace SynergyUI
{
    /// <summary>
    /// 卡牌合成 ↔ 效果合成 的跨屏会话（2026-09-14 合成器重做）。
    /// UIManager.Show&lt;T&gt;() 无法传参且界面实例按类型缓存——用静态会话对象携带编辑上下文：
    ///   CardComposer「编辑」/「+ 新建效果」→ 填会话 → Show&lt;EffectComposerScreen&gt;；
    ///   EffectComposer.OnEnter 读会话进入卡编辑模式（保存写回 card.Effects 并 Back，不落效果库文件）。
    /// 读写后即清（一次性消费，防残留串局）。
    /// </summary>
    public static class ComposerSession
    {
        /// <summary>正在编辑其效果的卡（null = 效果库模式——保存走 EffectLibrarySerializer）。</summary>
        public static CardData EditingCard;

        /// <summary>编辑的效果下标（-1 = 新建效果，保存时 Append）。</summary>
        public static int EditingIndex = -1;

        /// <summary>进入卡编辑模式。</summary>
        public static void BeginCardEdit(CardData card, int effectIndex)
        {
            EditingCard = card;
            EditingIndex = effectIndex;
        }

        public static bool IsCardEditMode => EditingCard != null;

        /// <summary>消费会话（EffectComposer.OnEnter 取走上下文后调用）。</summary>
        public static (CardData card, int index) Take()
        {
            var result = (EditingCard, EditingIndex);
            EditingCard = null;
            EditingIndex = -1;
            return result;
        }

        /// <summary>放弃会话（返回卡界面未保存时）。</summary>
        public static void Clear()
        {
            EditingCard = null;
            EditingIndex = -1;
        }
    }
}
