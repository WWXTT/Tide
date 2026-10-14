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

        /// <summary>库效果编辑会话（2026-10-13 卡组合界面右栏「编辑」）：全局编辑库定案——
        /// 跳效果合成器自动载入该 id 的库效果，保存改 Effects.json（所有引用它的卡随之变），
        /// 保存后自动返回卡组合界面。消费端分支在 EffectComposerScreen.OnEnterInternal
        /// （Take() 之后仿 LoadEffectFromLibrary 载入 + OnSave 库模式尾条件 Back）——该文件
        /// 并行会话占用中，分支待解锁接入；接入前为过渡态（跳转+Console 提示手动在效果表载入）。
        /// 注意：Take() 只消费卡编辑两字段；本组字段由消费分支自行读后清（勿依赖 Take）。</summary>
        public static string EditingLibraryEffectId;
        public static bool ReturnToCardComposer;

        /// <summary>进入卡编辑模式。</summary>
        public static void BeginCardEdit(CardData card, int effectIndex)
        {
            EditingCard = card;
            EditingIndex = effectIndex;
            EditingLibraryEffectId = null;
            ReturnToCardComposer = false;
        }

        /// <summary>进入库效果编辑模式（全局编辑库；不动任何卡）。</summary>
        public static void BeginLibraryEffectEdit(string libraryEffectId)
        {
            EditingCard = null;
            EditingIndex = -1;
            EditingLibraryEffectId = libraryEffectId;
            ReturnToCardComposer = true;
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
            EditingLibraryEffectId = null;
            ReturnToCardComposer = false;
        }
    }
}
