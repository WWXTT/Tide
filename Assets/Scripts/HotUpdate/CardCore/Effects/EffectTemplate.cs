using System;
using System.Collections.Generic;

namespace CardCore
{
    /// <summary>
    /// 效果模板槽位
    /// </summary>
    [Serializable]
    public class EffectTemplateSlot
    {
        public string SlotId;
        public string DisplayName;
        public bool IsRequired;
        public string Description;
        public List<string> SuggestedEffectTypes = new List<string>();
    }

    /// <summary>
    /// 效果模板
    /// 引导信息，告诉玩家需要什么和可以用什么
    /// </summary>
    [Serializable]
    public class EffectTemplate
    {
        public string Id;
        public string DisplayName;
        public string Description;
        public List<EffectTemplateSlot> RequiredSlots = new List<EffectTemplateSlot>();
        public List<EffectTemplateSlot> OptionalSlots = new List<EffectTemplateSlot>();
    }
}
