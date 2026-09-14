using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CardCore;
using UnityEngine;

namespace SynergyUI
{
    /// <summary>
    /// 效果库 JSON 读写（2026-09-14 v2·大修）：**单文件瘦格式** StreamingAssets/Tide/Effects.json
    /// （{items:[EffectSlimDto]}——原子=表行 ID 引用+增量，见 EffectSlim.cs）。
    ///
    /// 效果 id = ContentHasher.HashEffect（单源化：AE 段仅 steps 空时计入）。Save=按 id upsert；
    /// EffectIdsOf/EnsureEffectsSaved 供卡牌保存链路写引用。
    /// </summary>
    public static class EffectLibrarySerializer
    {
        private const string FilePathRelative = "Tide/Effects.json";

        private static string FilePath => Path.Combine(Application.streamingAssetsPath, FilePathRelative);

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

        /// <summary>图 → 瘦 DTO（steps 单源；引擎形态奖励进 rewards）。</summary>
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
                dropZone = h.SummonDropZone,
                engine = h.EngineKind,
                engineParam = h.EngineParam,
                drawbacks = h.Drawbacks != null && h.Drawbacks.Count > 0 ? new List<string>(h.Drawbacks) : null,
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
                    SummonDropZone = dto.dropZone,
                    EngineKind = dto.engine,
                    EngineParam = dto.engineParam,
                    Drawbacks = dto.drawbacks != null ? new List<string>(dto.drawbacks) : new List<string>(),
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
    }
}
