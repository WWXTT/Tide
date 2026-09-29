using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CardCore;
using UnityEngine;

namespace SynergyUI
{
    /// <summary>
    /// 效果库 JSON 读写（2026-09-14 v2·大修）：**单文件瘦格式** StreamingAssets/Card/Effects.json
    /// （{items:[EffectSlimDto]}——原子=表行 ID 引用+增量，见 EffectSlim.cs）。
    /// 效果属用户数据（2026-09-21 定案）：与卡/卡组同住 Card/ 目录（EffectsLibrary.cs 同源同路径）。
    ///
    /// 效果 id = ContentHasher.HashEffect（单源化：AE 段仅 steps 空时计入）。Save=按 id upsert；
    /// EffectIdsOf/EnsureEffectsSaved 供卡牌保存链路写引用。
    /// </summary>
    public static class EffectLibrarySerializer
    {
        private const string FilePathRelative = "Card/Effects.json";

        private static string FilePath => CardDataPaths.FileIn("Effects.json"); // 路径收口（2026-09-24）

        [Serializable]
        private class Wrapper { public List<EffectSlimDto> items; }

        // ======================================== 读 ========================================

        /// <summary>读取全部效果（文件缺失返回空）。</summary>
        public static List<EffectGraphData> LoadAll()
        {
            var result = new List<EffectGraphData>();
            if (!File.Exists(FilePath)) return result;
            var wrapper = JsonUtility.FromJson<Wrapper>(File.ReadAllText(FilePath));
            if (wrapper?.items == null) return result;
            foreach (var dto in wrapper.items)
            {
                var graph = ToGraph(dto);
                if (graph != null) result.Add(graph);
            }
            return result;
        }

        // ======================================== 写 ========================================

        /// <summary>保存（按 id upsert；id=HashEffect 单源化口径，Save 内计算回填）。返回文件路径。</summary>
        public static string Save(EffectGraphData graph)
        {
            if (graph == null) return null;
            graph.header ??= new CardEffectData();
            graph.steps ??= new List<EffectStepData>();
            graph.id = ContentHasher.HashEffect(graph);
            if (string.IsNullOrEmpty(graph.name)) graph.name = graph.id;

            var wrapper = ReadWrapper() ?? new Wrapper { items = new List<EffectSlimDto>() };
            wrapper.items ??= new List<EffectSlimDto>();
            var dto = ToDto(graph);
            dto.id = graph.id;
            int existing = wrapper.items.FindIndex(i => i != null && i.id == graph.id);
            if (existing >= 0) wrapper.items[existing] = dto;
            else wrapper.items.Add(dto);

            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            File.WriteAllText(FilePath, JsonUtility.ToJson(wrapper, true));
            return FilePath;
        }

        /// <summary>按 id 删除条目（内容变更换 id 后清旧档）。</summary>
        public static void DeleteById(string id)
        {
            if (string.IsNullOrEmpty(id) || !File.Exists(FilePath)) return;
            var wrapper = ReadWrapper();
            if (wrapper?.items == null) return;
            wrapper.items.RemoveAll(i => i != null && i.id == id);
            File.WriteAllText(FilePath, JsonUtility.ToJson(wrapper, true));
        }

        /// <summary>效果列表 → 引用 id 列表（逐个 upsert 落库后取 id）。供卡表保存写 effectIds。</summary>
        public static List<string> EffectIdsOf(List<CardEffectData> effects)
        {
            var ids = new List<string>();
            if (effects == null) return ids;
            foreach (var fx in effects)
            {
                if (fx == null) continue;
                var graph = new EffectGraphData(fx.DisplayName) { header = fx, steps = fx.Steps ?? new List<EffectStepData>() };
                Save(graph);
                ids.Add(graph.id);
            }
            return ids;
        }

        /// <summary>卡牌保存链路：批量落库（EffectIdsOf 的底层共用口）。</summary>
        public static void EnsureEffectsSaved(List<CardEffectData> effects)
        {
            if (effects == null) return;
            foreach (var fx in effects)
            {
                if (fx == null) continue;
                var graph = new EffectGraphData(fx.DisplayName) { header = fx, steps = fx.Steps ?? new List<EffectStepData>() };
                Save(graph);
            }
        }

        private static Wrapper ReadWrapper()
        {
            if (!File.Exists(FilePath)) return null;
            try { return JsonUtility.FromJson<Wrapper>(File.ReadAllText(FilePath)); }
            catch { return null; }
        }

        // ======================================== 图 ↔ 瘦 DTO（EffectGraphData 在 UI 层——转换在本地） ========================================

        /// <summary>图 → 瘦 DTO（steps 单源；引擎形态奖励进 rewards；锚价合成期推导落盘）。</summary>
        private static EffectSlimDto ToDto(EffectGraphData graph)
        {
            if (graph?.header == null) return null;
            var h = graph.header;
            var dto = new EffectSlimDto
            {
                id = graph.id,
                name = graph.name,
                timing = h.TriggerTiming,
                activation = h.ActivationType,
                speed = h.BaseSpeed,
                limit = h.TriggerLimitPerTurn,
                duration = h.Duration,
                selection = h.SelectionMode,
                count = h.TargetCount,
                random = h.RandomTarget,
                dropZone = h.SummonDropZone,
                engine = h.EngineKind,
                engineParam = h.EngineParam,
                arrows = h.ArrowDirections,
                linkAuras = h.LinkAuras != null && h.LinkAuras.Count > 0
                    ? new List<LinkAuraData>(h.LinkAuras) : null, // 空表不写列（2026-09-23 向后兼容）
                cost = DeriveAnchorCost(graph), // 效果锚价（2026-09-23）：纯表累加实时推导——与合成器费用预览同口径
                costs = h.Costs != null && h.Costs.Count > 0
                    ? h.Costs.Select(EffectSlim.ToCostRef).Where(c => c != null).ToList() : null,
                steps = new List<StepRef>(),
                rewards = null,
            };

            if (h.EngineKind != (int)BranchEngineKind.None)
            {
                dto.rewards = h.AtomicEffects != null && h.AtomicEffects.Count > 0
                    ? h.AtomicEffects.Select(EffectSlim.ToRef).Where(r => r != null).ToList() : new List<AtomicEffectEntry>();
            }
            else
            {
                foreach (var st in graph.steps ?? new List<EffectStepData>())
                {
                    var sr = EffectSlim.ToStepRef(st);
                    if (sr != null) dto.steps.Add(sr);
                }
                // 扁平原子投影（2026-09-21 修复：白板卡根因）——Steps 空而 AtomicEffects 非空时
                // 逐原子投影为 kind=0 步骤：此前直接丢弃，凡以扁平形态构建的效果（主题卡构建器等）
                // 落盘后原子全失，打出只付费不结算。读回方向 ToCardEffect 天然支持 Steps 还原。
                if (dto.steps.Count == 0 && h.AtomicEffects != null && h.AtomicEffects.Count > 0)
                {
                    foreach (var atom in h.AtomicEffects)
                    {
                        var ar = EffectSlim.ToRef(atom);
                        if (ar != null) dto.steps.Add(new StepRef { kind = 0, atom = ar });
                    }
                }
            }
            return dto;
        }

        /// <summary>瘦 DTO → 图（引擎形态奖励还原为 header.AtomicEffects、steps 清空）。</summary>
        private static EffectGraphData ToGraph(EffectSlimDto dto)
        {
            if (dto == null) return null;
            var graph = new EffectGraphData(dto.name)
            {
                id = dto.id,
                header = new CardEffectData
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
                        ? new List<LinkAuraData>(dto.linkAuras) : null,
                    AnchorCost = dto.cost != null && dto.cost.Count > 0
                        ? dto.cost.Where(c => c != null)
                            .Select(c => new ElementCostRef { mana = c.mana, value = c.value }).ToList() : null,
                    Costs = EffectSlim.ToCostEntries(dto.costs),
                },
                steps = dto.steps != null
                    ? dto.steps.Select(EffectSlim.ToStep).Where(st => st != null).ToList()
                    : new List<EffectStepData>(),
            };
            if (dto.engine != (int)BranchEngineKind.None)
            {
                graph.header.AtomicEffects = EffectSlim.ToEntries(dto.rewards);
                graph.steps = new List<EffectStepData>();
            }
            return graph;
        }

        /// <summary>效果锚价（2026-09-23 定案）：**效果组合阶段=纯表累加、无减免抵消**——
        /// ConvertOne+DeriveElementCosts 实时推导（与合成器费用预览 AutoCostText 同一口径），
        /// 代价不参与（代价已上移卡组合层，效果层不存在费用减免抵消）。
        /// 派生数据不入内容哈希（HashEffect 不读 cost 列——原子表调价重算不换效果 id）。
        /// 编辑中间态转换失败/无费返回 null（空列不写）。</summary>
        private static List<ElementCostRef> DeriveAnchorCost(EffectGraphData graph)
        {
            try
            {
                var h = graph.header;
                // 与合成器 BuildCardEffect 同构：Steps 优先、引擎形态走 AtomicEffects、并列扁平投影
                var fx = JsonUtility.FromJson<CardEffectData>(JsonUtility.ToJson(h));
                fx.Steps = graph.steps != null && graph.steps.Count > 0 ? graph.steps : null;
                fx.AtomicEffects = h.EngineKind != (int)BranchEngineKind.None
                    ? h.AtomicEffects
                    : ProjectLinear(graph.steps);
                var def = CardEffectConverter.ConvertOne(fx, "ANCHOR_COST_DERIVE");
                var costs = def == null ? null : CostDerivationService.DeriveElementCosts(def, 0);
                if (costs == null || costs.Count == 0) return null;
                return costs
                    .Where(c => c != null && c.Value > 0)
                    .Select(c => new ElementCostRef { mana = (int)c.ManaType, value = (int)c.Value })
                    .ToList();
            }
            catch
            {
                return null; // 推导失败不阻断保存（费用预览同样显示 —）
            }
        }

        /// <summary>并列步骤线性投影（引擎形态除外——与合成器 BuildCardEffect 同构）。</summary>
        private static List<AtomicEffectEntry> ProjectLinear(List<EffectStepData> steps)
        {
            var flat = new List<AtomicEffectEntry>();
            if (steps == null) return flat;
            foreach (var step in steps)
            {
                if (step == null) continue;
                if (step.kind == 0 && step.atomic != null) flat.Add(step.atomic);
                else if (step.kind == 1 && step.thenSteps != null) flat.AddRange(step.thenSteps);
            }
            return flat;
        }
    }
}
