using System;
using System.Collections.Generic;
using System.Linq;
using CardCore.Attribute;
using UnityEngine;

namespace CardCore
{
    // ================================================================
    // 效果瘦存储 DTO（2026-09-14 效果引用化 v2·大修定案）：
    //
    // 原子不内嵌六字段全量——**引用原子表行 ID**（表首列 8-hex）+ 只存增量：
    //   原子=AtomicEffectEntry { refId(表行ID), value, str, amp, kinds }（本体即引用型）
    // 默认值字段（amp=0/kinds=null/str=""）JsonUtility 仍会写出——字段数恒 5，语义=引用+增量。
    //
    // 步骤单源：steps 形态只存 steps（不再写 header.AtomicEffects 投影——148/155 旧条目的
    // AtomicEffects 即该投影冗余）；引擎形态（engine≠0）AtomicEffects=奖励原子，经
    // rewards 字段存（steps 恒空）。哈希同步单源化（ContentHasher：AE 段仅 steps 空时计入）。
    //
    // 效果级条件/触发条件/Tags 不入瘦格式（当前 155 条全零——出现需求时再加列）。
    // ================================================================

    // AtomRef 已并入 AtomicEffectEntry（2026-09-14 彻底引用化：本体即引用型，无双轨）。

    /// <summary>步骤引用：kind=0 原子 / 1 门分支 / 2 抉择。</summary>
    [Serializable]
    public class StepRef
    {
        public int kind;
        public AtomicEffectEntry atom;                                   // kind=0
        public string gid;                                     // kind=1：产出条件 id（DmgKillsTarget/DeclareHit）
        public int gparam;                                     // kind=1：条件数值参数
        public string gstr;                                    // kind=1：条件字符串参数（预言类型）
        public List<AtomicEffectEntry> then;                             // kind=1：奖励（单原子）
        public List<AtomicEffectEntry> els;                              // kind=1：否则（常规空）
        public List<ChoiceRef> choices;                        // kind=2：≥2 模式
    }

    /// <summary>抉择模式（卡组成阶段产物——效果编辑器只读）。</summary>
    [Serializable]
    public class ChoiceRef
    {
        public string label;
        public List<StepRef> steps;
    }

    /// <summary>效果锚价条目（2026-09-23 定案：效果组合阶段=纯表累加、无减免抵消——
    /// 合成期实时推导随效果落盘；派生数据，不入内容哈希）。</summary>
    [Serializable]
    public class ElementCostRef
    {
        public int mana;   // ManaType
        public int value;  // 元素数
    }

    /// <summary>代价引用：type=7(Payload) 时 payload 为错边原子引用。</summary>
    [Serializable]
    public class CostRef
    {
        public int type;
        public int value;
        public int manaType;
        public int turns;
        public AtomicEffectEntry payload;
    }

    /// <summary>效果条目（瘦格式——Effects.json items 的元素）。</summary>
    [Serializable]
    public class EffectSlimDto
    {
        public string id;              // 效果 id = ContentHasher.HashEffect（单源化口径）
        public string name;            // 显示名（自动名）
        public int timing;             // TriggerTiming
        public int activation;         // 0=强制 1=自动 2=主动
        public int speed;              // BaseSpeed
        public int limit;              // TriggerLimitPerTurn
        public int duration;           // DurationType
        public int selection;          // SelectionMode（-1=None；0-5=六值定案 2026-09-16）
        public int count;              // TargetCount（>0=N；0=全部；-1=任意[玩家自选数量=0费]；-2=未声明）
        public int random;             // RandomTarget（0/1——目标随机正交标志，与"选多少"无关）
        public int dropZone;           // SummonDropZone
        public int engine;             // BranchEngineKind（≠0 时 rewards 有效、steps 恒空）
        public int engineParam;
        public int arrows;             // 光环形态（2026-09-23）：HexDirection Flags——箭头随效果合成，挂卡并集
        public List<LinkAuraData> linkAuras;  // 光环条目（非光环效果 null——空列不写，向后兼容）
        public List<ElementCostRef> cost;     // 效果锚价（2026-09-23）：合成期按表累加实时推导落盘——
                                              // 装载期逐效果还原为 AnchorCost 缓存（启动式/动态效果运行时
                                              // 现付的显示/预检口径）；派生数据不入内容哈希（表变更重算不换 id）
        public List<CostRef> costs;
        public List<StepRef> steps;
        public List<AtomicEffectEntry> rewards;  // 引擎奖励（engine≠0）
    }

    /// <summary>瘦 DTO ↔ 运行时模型转换（2026-09-14 彻底引用化后原子零转换——本体即引用；
    /// 步骤/代价/抉择仍是 DTO↔CardCore 结构映射）。反解析失败逐原子告警——可见不炸，
    /// 保存链路（EffectLibrarySerializer）对行缺失直接中止。</summary>
    public static class EffectSlim
    {
        // ======================================== 引用 → 条目 ========================================

        /// <summary>引用直通 + 行校验（表行缺失返回 null+告警）。AtomicEffectEntry 本体即引用型。</summary>
        public static AtomicEffectEntry ToEntry(AtomicEffectEntry ar)
        {
            if (ar == null || string.IsNullOrEmpty(ar.refId)) return null;
            if (AtomicEffectTable.GetByHashId(ar.refId) == null)
            {
                Debug.LogWarning($"[EffectSlim] 原子表引用缺失: {ar.refId}（表无此行）——剔除");
                return null;
            }
            ar.amp = Mathf.Clamp(ar.amp, 0f, 1f);
            return ar;
        }

        /// <summary>StepRef → EffectStepData（kind=0/1/2 全支持）。</summary>
        public static EffectStepData ToStep(StepRef sr)
        {
            if (sr == null) return null;
            switch (sr.kind)
            {
                case 0:
                    var atom = ToEntry(sr.atom);
                    return atom == null ? null : new EffectStepData { kind = 0, atomic = atom };
                case 1:
                    return new EffectStepData
                    {
                        kind = 1,
                        conditionId = sr.gid,
                        conditionParam = sr.gparam,
                        conditionStringParam = sr.gstr,
                        thenSteps = ToEntries(sr.then),
                        elseSteps = ToEntries(sr.els),
                    };
                case 2:
                    return new EffectStepData
                    {
                        kind = 2,
                        choices = (sr.choices ?? new List<ChoiceRef>())
                            .Select(c => new EffectChoiceData
                            {
                                label = c?.label,
                                steps = (c?.steps ?? new List<StepRef>()).Select(ToStep).Where(s => s != null).ToList(),
                            })
                            .ToList(),
                    };
                default:
                    return null;
            }
        }

        public static List<AtomicEffectEntry> ToEntries(List<AtomicEffectEntry> refs)
            => refs?.Select(ToEntry).Where(a => a != null).ToList() ?? new List<AtomicEffectEntry>();

        // ======================================== 条目 → 引用 ========================================

        /// <summary>引用直通 + 行校验（本体即引用型——彻底引用化后无转换）。
        /// 行缺失返回 null（保存链路上层据此中止，绝不静默剔除落盘）。</summary>
        public static AtomicEffectEntry ToRef(AtomicEffectEntry a)
        {
            if (a == null || string.IsNullOrEmpty(a.refId)) return null;
            if (AtomicEffectTable.GetByHashId(a.refId) == null)
            {
                Debug.LogWarning($"[EffectSlim] 原子表引用缺失: {a.refId}——不入瘦格式");
                return null;
            }
            return a;
        }

        /// <summary>EffectStepData → StepRef（kind=0/1/2 全支持）。</summary>
        public static StepRef ToStepRef(EffectStepData st)
        {
            if (st == null) return null;
            switch (st.kind)
            {
                case 0:
                    var ar = ToRef(st.atomic);
                    return ar == null ? null : new StepRef { kind = 0, atom = ar };
                case 1:
                    return new StepRef
                    {
                        kind = 1,
                        gid = st.conditionId,
                        gparam = st.conditionParam,
                        gstr = st.conditionStringParam,
                        then = st.thenSteps?.Select(ToRef).Where(r => r != null).ToList(),
                        els = st.elseSteps != null && st.elseSteps.Count > 0
                            ? st.elseSteps.Select(ToRef).Where(r => r != null).ToList() : null,
                    };
                case 2:
                    return new StepRef
                    {
                        kind = 2,
                        choices = st.choices?.Select(c => new ChoiceRef
                        {
                            label = c?.label,
                            steps = (c?.steps ?? new List<EffectStepData>()).Select(ToStepRef).Where(x => x != null).ToList(),
                        }).ToList(),
                    };
                default:
                    return null;
            }
        }

        /// <summary>CostEntry → CostRef（payload 原子转引用）。</summary>
        public static CostRef ToCostRef(CostEntry c)
        {
            if (c == null) return null;
            return new CostRef
            {
                type = c.CostType,
                value = c.Value,
                manaType = c.ManaType,
                turns = c.TurnDuration,
                payload = c.payload != null && !string.IsNullOrEmpty(c.payload.refId) ? ToRef(c.payload) : null,
            };
        }

        /// <summary>CostRef → CostEntry。</summary>
        public static CostEntry ToCostEntry(CostRef cr)
        {
            if (cr == null) return null;
            return new CostEntry
            {
                CostType = cr.type,
                Value = cr.value,
                ManaType = cr.manaType,
                TurnDuration = cr.turns,
                payload = cr.payload != null ? ToEntry(cr.payload) : null,
            };
        }

        public static List<CostEntry> ToCostEntries(List<CostRef> refs)
            => refs?.Select(ToCostEntry).Where(c => c != null).ToList();

        /// <summary>瘦 DTO → 卡内效果（CardLoader 引用解析出口：Steps 走步骤通道；
        /// 引擎形态奖励还原为 AtomicEffects）。</summary>
        public static CardEffectData ToCardEffect(EffectSlimDto dto)
        {
            if (dto == null) return null;
            var fx = new CardEffectData
            {
                Id = dto.id,
                DisplayName = dto.name,
                TriggerTiming = dto.timing,
                ActivationType = dto.activation,
                BaseSpeed = dto.speed,
                TriggerLimitPerTurn = dto.limit,
                Duration = dto.duration,
                SelectionMode = dto.selection,
                TargetCount = dto.count,
                RandomTarget = dto.random,
                SummonDropZone = dto.dropZone,
                EngineKind = dto.engine,
                EngineParam = dto.engineParam,
                ArrowDirections = dto.arrows,
                LinkAuras = dto.linkAuras != null && dto.linkAuras.Count > 0
                    ? dto.linkAuras.Where(a => a != null
                        && (!string.IsNullOrEmpty(a.stat) || !string.IsNullOrEmpty(a.keyword))).ToList()
                    : null, // 光环条目（2026-09-23 效果级）——无效条目装载期即丢弃
                AnchorCost = dto.cost != null && dto.cost.Count > 0
                    ? dto.cost.Where(c => c != null).Select(c => new ElementCostRef { mana = c.mana, value = c.value }).ToList()
                    : null, // 锚价缓存（2026-09-23）——装载期逐效果建立
                Costs = ToCostEntries(dto.costs),
            };
            if (dto.engine != (int)BranchEngineKind.None)
            {
                fx.AtomicEffects = ToEntries(dto.rewards);
                fx.Steps = null;
            }
            else
            {
                fx.Steps = dto.steps != null
                    ? dto.steps.Select(ToStep).Where(st => st != null).ToList() : null;
            }
            return fx;
        }
    }
}
