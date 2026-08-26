using System;
using System.Collections.Generic;

namespace CardCore
{
    /// <summary>
    /// 效果模板静态注册表
    /// </summary>
    public static class EffectTemplateTable
    {
        private static Dictionary<string, EffectTemplate> _idMap;
        private static List<EffectTemplate> _allTemplates;

        static EffectTemplateTable()
        {
            Initialize();
        }

        private static void Initialize()
        {
            _idMap = new Dictionary<string, EffectTemplate>();
            _allTemplates = new List<EffectTemplate>();

            // --- 简单法术模板 ---
            AddTemplate(new EffectTemplate
            {
                Id = "SimpleSpell",
                DisplayName = "简单法术",
                Description = "一张基础的法术卡效果，包含至少一个原子效果",
                RequiredSlots = new List<EffectTemplateSlot>
                {
                    new EffectTemplateSlot
                    {
                        SlotId = "main_effect",
                        DisplayName = "主效果",
                        IsRequired = true,
                        Description = "至少需要一个原子效果作为主要效果",
                        SuggestedEffectTypes = new List<string>
                        {
                            "DealDamage", "Destroy", "DrawCard", "Heal"
                        }
                    },
                    new EffectTemplateSlot
                    {
                        SlotId = "trigger_timing",
                        DisplayName = "发动时机",
                        IsRequired = true,
                        Description = "选择效果发动的时机",
                        SuggestedEffectTypes = new List<string>()
                    }
                },
                OptionalSlots = new List<EffectTemplateSlot>
                {
                    new EffectTemplateSlot
                    {
                        SlotId = "target_scope",
                        DisplayName = "作用范围",
                        IsRequired = false,
                        Description = "可选：调整效果的作用范围",
                        SuggestedEffectTypes = new List<string>()
                    },
                    new EffectTemplateSlot
                    {
                        SlotId = "extra_cost",
                        DisplayName = "附加代价",
                        IsRequired = false,
                        Description = "可选：添加额外代价来抵扣元素消耗",
                        SuggestedEffectTypes = new List<string>()
                    },
                    new EffectTemplateSlot
                    {
                        SlotId = "condition_branch",
                        DisplayName = "条件分支",
                        IsRequired = false,
                        Description = "可选：添加条件，满足条件时执行额外效果",
                        SuggestedEffectTypes = new List<string>()
                    }
                }
            });

            // --- 触发式能力模板 ---
            AddTemplate(new EffectTemplate
            {
                Id = "TriggeredAbility",
                DisplayName = "触发式能力",
                Description = "特定条件下自动触发的效果，需要指定触发时机",
                RequiredSlots = new List<EffectTemplateSlot>
                {
                    new EffectTemplateSlot
                    {
                        SlotId = "main_effect",
                        DisplayName = "主效果",
                        IsRequired = true,
                        Description = "触发后执行的主要效果",
                        SuggestedEffectTypes = new List<string>
                        {
                            "DealDamage", "DrawCard", "Heal", "ModifyPower", "CreateToken"
                        }
                    },
                    new EffectTemplateSlot
                    {
                        SlotId = "trigger_timing",
                        DisplayName = "触发时机",
                        IsRequired = true,
                        Description = "效果在什么时候触发（如入场时、死亡时、回合结束时等）",
                        SuggestedEffectTypes = new List<string>()
                    }
                },
                OptionalSlots = new List<EffectTemplateSlot>
                {
                    new EffectTemplateSlot
                    {
                        SlotId = "activation_condition",
                        DisplayName = "发动条件",
                        IsRequired = false,
                        Description = "可选：添加条件限制效果的发动",
                        SuggestedEffectTypes = new List<string>()
                    },
                    new EffectTemplateSlot
                    {
                        SlotId = "condition_branch",
                        DisplayName = "条件分支",
                        IsRequired = false,
                        Description = "可选：满足特定条件时执行额外效果",
                        SuggestedEffectTypes = new List<string>()
                    },
                    new EffectTemplateSlot
                    {
                        SlotId = "extra_cost",
                        DisplayName = "附加代价",
                        IsRequired = false,
                        Description = "可选：添加额外代价来抵扣元素消耗",
                        SuggestedEffectTypes = new List<string>()
                    }
                }
            });

            // --- 战斗效果模板 ---
            AddTemplate(new EffectTemplate
            {
                Id = "CombatEffect",
                DisplayName = "战斗效果",
                Description = "与战斗相关的效果，通常在攻击或造成伤害时触发",
                RequiredSlots = new List<EffectTemplateSlot>
                {
                    new EffectTemplateSlot
                    {
                        SlotId = "main_effect",
                        DisplayName = "主效果",
                        IsRequired = true,
                        Description = "战斗中执行的主要效果",
                        SuggestedEffectTypes = new List<string>
                        {
                            "DealDamage", "Destroy", "ModifyPower"
                        }
                    },
                    new EffectTemplateSlot
                    {
                        SlotId = "trigger_timing",
                        DisplayName = "发动时机",
                        IsRequired = true,
                        Description = "选择战斗中的发动时机（攻击时、造成伤害时等）",
                        SuggestedEffectTypes = new List<string>()
                    }
                },
                OptionalSlots = new List<EffectTemplateSlot>
                {
                    new EffectTemplateSlot
                    {
                        SlotId = "condition_branch",
                        DisplayName = "条件分支",
                        IsRequired = false,
                        Description = "可选：如伤害击杀时、伤害超过生命值时等条件分支",
                        SuggestedEffectTypes = new List<string>()
                    },
                    new EffectTemplateSlot
                    {
                        SlotId = "extra_cost",
                        DisplayName = "附加代价",
                        IsRequired = false,
                        Description = "可选：添加额外代价来抵扣元素消耗",
                        SuggestedEffectTypes = new List<string>()
                    }
                }
            });
        }

        private static void AddTemplate(EffectTemplate template)
        {
            _idMap[template.Id] = template;
            _allTemplates.Add(template);
        }

        /// <summary>通过ID获取模板</summary>
        public static EffectTemplate GetById(string id)
        {
            return _idMap.TryGetValue(id, out var template) ? template : null;
        }

        /// <summary>获取所有模板</summary>
        public static List<EffectTemplate> GetAll()
        {
            return _allTemplates;
        }
    }
}
