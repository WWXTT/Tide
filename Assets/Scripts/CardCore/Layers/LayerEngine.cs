using System;
using System.Collections.Generic;
using System.Linq;


namespace CardCore
{
    /// <summary>
    /// MTG 标准 7 语义层（连续效果按层序应用，层内再按子层/时间戳排序）。
    /// L7（力量/防御）通过 <see cref="PowerToughnessSublayer"/> 细分子层。
    /// </summary>
    public enum LayerType
    {
        /// <summary>L1 复制效果</summary>
        Copy = 1,
        /// <summary>L2 控制变更</summary>
        Control = 2,
        /// <summary>L3 文本变更</summary>
        Text = 3,
        /// <summary>L4 类型变更</summary>
        Type = 4,
        /// <summary>L5 颜色变更</summary>
        Color = 5,
        /// <summary>L6 能力增删</summary>
        Ability = 6,
        /// <summary>L7 力量与防御</summary>
        PowerToughness = 7
    }

    /// <summary>
    /// L7（力量/防御）子层顺序（MTG 613.4）。
    /// </summary>
    public enum PowerToughnessSublayer
    {
        /// <summary>7a 特征定义能力（CDA）</summary>
        CDA = 0,
        /// <summary>7b 设定为固定值</summary>
        Set = 1,
        /// <summary>7c 指示物（+1/+1 等）</summary>
        Counters = 2,
        /// <summary>7d 其他增减效果</summary>
        Modify = 3,
        /// <summary>7e 力量/防御互换</summary>
        Switch = 4
    }

    /// <summary>
    /// 效果影响范围
    /// </summary>
    public enum EffectRange
    {
        /// <summary>
        /// 全局
        /// </summary>
        Global,

        /// <summary>
        /// 局部
        /// </summary>
        Local,

        /// <summary>
        /// 特定目标
        /// </summary>
        Targeted
    }

    #region 特征修饰器类型

    /// <summary>
    /// 特征修饰器基接口
    /// 每种修饰器明确知道自己操作的值类型，避免装箱/拆箱
    /// </summary>
    public interface ICharacteristicModification
    {
        CharacteristicType TargetCharacteristic { get; }
    }

    /// <summary>
    /// 整数加法修饰器（用于 Power/Toughness 的增减）
    /// </summary>
    public sealed class IntAddModification : ICharacteristicModification
    {
        public CharacteristicType TargetCharacteristic { get; }
        public int Delta { get; }

        public IntAddModification(CharacteristicType target, int delta)
        {
            TargetCharacteristic = target;
            Delta = delta;
        }

        public int Apply(int original) => original + Delta;
    }

    /// <summary>
    /// 整数设定修饰器（将值设为固定值）
    /// </summary>
    public sealed class IntSetModification : ICharacteristicModification
    {
        public CharacteristicType TargetCharacteristic { get; }
        public int Value { get; }

        public IntSetModification(CharacteristicType target, int value)
        {
            TargetCharacteristic = target;
            Value = value;
        }

        public int Apply(int original) => Value;
    }

    /// <summary>
    /// 力量/防御互换修饰器（L7e）。值由 LayerEngine 在计算时整体互换处理，
    /// 此类仅作标记，Apply 不改变单值。
    /// </summary>
    public sealed class SwitchPowerToughnessModification : ICharacteristicModification
    {
        // 以 Power 作为登记键；实际互换在 LayerEngine.ComputePowerToughness 中执行。
        public CharacteristicType TargetCharacteristic => CharacteristicType.Power;
    }

    /// <summary>
    /// 费用修饰器（修改费用字典）
    /// </summary>
    public sealed class CostAddModification : ICharacteristicModification
    {
        public CharacteristicType TargetCharacteristic => CharacteristicType.Cost;
        public Dictionary<int, float> CostDelta { get; }

        public CostAddModification(Dictionary<int, float> costDelta)
        {
            CostDelta = costDelta;
        }

        public Dictionary<int, float> Apply(Dictionary<int, float> original)
        {
            var result = new Dictionary<int, float>(original);
            foreach (var kvp in CostDelta)
            {
                if (result.ContainsKey(kvp.Key))
                    result[kvp.Key] += kvp.Value;
                else
                    result[kvp.Key] = kvp.Value;
            }
            return result;
        }
    }

    /// <summary>
    /// 颜色添加修饰器
    /// </summary>
    public sealed class ColorAddModification : ICharacteristicModification
    {
        public CharacteristicType TargetCharacteristic => CharacteristicType.Color;
        public List<ManaType> AddedColors { get; }

        public ColorAddModification(List<ManaType> addedColors)
        {
            AddedColors = addedColors;
        }

        public List<ManaType> Apply(List<ManaType> original)
        {
            var result = new List<ManaType>(original);
            foreach (var color in AddedColors)
            {
                if (!result.Contains(color))
                    result.Add(color);
            }
            return result;
        }
    }

    #endregion

    /// <summary>
    /// 持续效果记录（用于层引擎内部）
    /// </summary>
    public class LayerContinuousEffect
    {
        public Effect SourceEffect { get; set; }
        public Entity SourceEntity { get; set; }
        public Entity TargetEntity { get; set; } // 可以为空（全局效果）
        public LayerType Layer { get; set; }
        /// <summary>仅当 Layer == PowerToughness 时有意义，决定 L7 子层顺序</summary>
        public PowerToughnessSublayer PTSublayer { get; set; } = PowerToughnessSublayer.Modify;
        public DateTime StartTime { get; set; }
        public int SequenceNumber { get; set; }
        public DurationType Duration { get; set; }
        public bool IsActive { get; set; } = true;
        public Dictionary<CharacteristicType, ICharacteristicModification> Modifications { get; set; }
            = new Dictionary<CharacteristicType, ICharacteristicModification>();

        /// <summary>
        /// 检查此效果是否影响指定特征
        /// </summary>
        public bool AffectsCharacteristic(CharacteristicType type)
        {
            return Modifications.ContainsKey(type);
        }

        /// <summary>
        /// 获取指定特征的修饰器
        /// </summary>
        public ICharacteristicModification GetModification(CharacteristicType type)
        {
            return Modifications.TryGetValue(type, out var mod) ? mod : null;
        }

        /// <summary>
        /// 是否为力量/防御互换效果（L7e）
        /// </summary>
        public bool IsSwitchEffect =>
            PTSublayer == PowerToughnessSublayer.Switch ||
            Modifications.Values.Any(m => m is SwitchPowerToughnessModification);

        /// <summary>
        /// 对整数特征应用修饰（Power/Toughness）
        /// </summary>
        public int ApplyIntModification(CharacteristicType type, int original)
        {
            if (!Modifications.TryGetValue(type, out var mod))
                return original;

            return mod switch
            {
                IntAddModification add => add.Apply(original),
                IntSetModification set => set.Apply(original),
                _ => original
            };
        }

        /// <summary>
        /// 对费用特征应用修饰
        /// </summary>
        public Dictionary<int, float> ApplyCostModification(Dictionary<int, float> original)
        {
            if (!Modifications.TryGetValue(CharacteristicType.Cost, out var mod))
                return original;

            return mod is CostAddModification costMod ? costMod.Apply(original) : original;
        }

        /// <summary>
        /// 对颜色特征应用修饰
        /// </summary>
        public List<ManaType> ApplyColorModification(List<ManaType> original)
        {
            if (!Modifications.TryGetValue(CharacteristicType.Color, out var mod))
                return original;

            return mod is ColorAddModification colorMod ? colorMod.Apply(original) : original;
        }
    }

    /// <summary>
    /// 特征定义能力（CDA）
    /// </summary>
    public class CharacteristicDefiningAbility
    {
        public Entity Source { get; set; }
        public Effect SourceEffect { get; set; }
        public CharacteristicDefiningType DefinitionType { get; set; }
        public object DefinedValue { get; set; }
        public DateTime Timestamp { get; set; }
        public int SequenceNumber { get; set; }

        /// <summary>
        /// 定义的目标实体
        /// </summary>
        public Entity DefinedEntity { get; set; }
    }

    /// <summary>
    /// 层引擎（MTG 7 语义层）
    /// 负责按层/子层/时间戳计算实体的当前特征（力量/防御/费用/颜色），
    /// 使用脏标记缓存，仅在修饰器变更时重算。
    /// 计算结果按需读取，绝不写回实体基础字段（基础值仅由指示物/永久效果改变）。
    /// </summary>
    public class LayerEngine
    {
        // 实体的持续效果集合
        private Dictionary<Entity, List<LayerContinuousEffect>> _entityEffects =
            new Dictionary<Entity, List<LayerContinuousEffect>>();

        // 特征定义能力集合
        private Dictionary<Entity, List<CharacteristicDefiningAbility>> _entityCDAs =
            new Dictionary<Entity, List<CharacteristicDefiningAbility>>();

        // 全局持续效果
        private List<LayerContinuousEffect> _globalEffects = new List<LayerContinuousEffect>();

        // 全局特征定义能力
        private List<CharacteristicDefiningAbility> _globalCDAs = new List<CharacteristicDefiningAbility>();

        // 脏标记缓存
        private HashSet<Entity> _dirtyEntities = new HashSet<Entity>();
        private bool _globalDirty = true;
        private Dictionary<Entity, CharacteristicCache> _cache =
            new Dictionary<Entity, CharacteristicCache>();

        /// <summary>
        /// 特征缓存条目
        /// </summary>
        private class CharacteristicCache
        {
            public bool PTValid;
            public int Power;
            public int Toughness;
            public Dictionary<int, float> Cost;
            public List<ManaType> Colors;
        }

        /// <summary>
        /// 标记指定实体为脏（需要重算）
        /// </summary>
        private void MarkDirty(Entity entity)
        {
            if (entity == null)
            {
                _globalDirty = true;
                // 全局效果变更会使所有缓存失效
                _dirtyEntities.Clear();
                _cache.Clear();
            }
            else
            {
                _dirtyEntities.Add(entity);
                _cache.Remove(entity);
            }
        }

        /// <summary>
        /// 检查实体是否需要重算
        /// </summary>
        private bool IsDirty(Entity entity)
        {
            return _globalDirty || _dirtyEntities.Contains(entity);
        }

        /// <summary>
        /// 获取或创建实体缓存
        /// </summary>
        private CharacteristicCache GetOrCreateCache(Entity entity)
        {
            if (!_cache.TryGetValue(entity, out var cache))
            {
                cache = new CharacteristicCache();
                _cache[entity] = cache;
            }
            return cache;
        }

        private void ClearDirtyIfResolved(Entity entity)
        {
            if (!_globalDirty)
                _dirtyEntities.Remove(entity);
        }

        /// <summary>
        /// 计算实体最终力量（带缓存）。L7 子层联合计算（含力量/防御互换）。
        /// </summary>
        public int CalculatePower(Entity entity)
        {
            if (entity == null) return 0;
            if (!IsDirty(entity) && _cache.TryGetValue(entity, out var cached) && cached.PTValid)
                return cached.Power;

            var cache = GetOrCreateCache(entity);
            ComputePowerToughness(entity, out cache.Power, out cache.Toughness);
            cache.PTValid = true;
            ClearDirtyIfResolved(entity);
            return cache.Power;
        }

        /// <summary>
        /// 计算实体最终防御（带缓存）。
        /// </summary>
        public int CalculateToughness(Entity entity)
        {
            if (entity == null) return 0;
            if (!IsDirty(entity) && _cache.TryGetValue(entity, out var cached) && cached.PTValid)
                return cached.Toughness;

            var cache = GetOrCreateCache(entity);
            ComputePowerToughness(entity, out cache.Power, out cache.Toughness);
            cache.PTValid = true;
            ClearDirtyIfResolved(entity);
            return cache.Toughness;
        }

        /// <summary>
        /// 计算实体最终费用（带缓存）
        /// </summary>
        public Dictionary<int, float> CalculateCost(Entity entity)
        {
            if (entity == null) return new Dictionary<int, float>();
            if (!IsDirty(entity) && _cache.TryGetValue(entity, out var cached) && cached.Cost != null)
                return cached.Cost;

            var value = CalculateCostInternal(entity);

            var cache = GetOrCreateCache(entity);
            cache.Cost = value;
            ClearDirtyIfResolved(entity);
            return value;
        }

        /// <summary>
        /// 计算实体最终颜色（带缓存）
        /// </summary>
        public List<ManaType> CalculateColors(Entity entity)
        {
            if (entity == null) return new List<ManaType>();
            if (!IsDirty(entity) && _cache.TryGetValue(entity, out var cached) && cached.Colors != null)
                return cached.Colors;

            var value = CalculateColorsInternal(entity);

            var cache = GetOrCreateCache(entity);
            cache.Colors = value;
            ClearDirtyIfResolved(entity);
            return value;
        }

        /// <summary>
        /// 强制刷新所有缓存（当全局状态变更时调用）
        /// </summary>
        public void InvalidateAllCache()
        {
            _globalDirty = true;
            _dirtyEntities.Clear();
            _cache.Clear();
        }

        #region 内部计算方法（无缓存，由带缓存的公有方法调用）

        /// <summary>
        /// L7 联合计算力量与防御。子层顺序：7a CDA → 7b Set → 7c Counters → 7d Modify → 7e Switch。
        /// 互换（7e）需同时持有两值，故力量与防御必须联合求解。
        /// </summary>
        private void ComputePowerToughness(Entity entity, out int power, out int toughness)
        {
            power = GetBasePower(entity);
            toughness = GetBaseToughness(entity);

            // 7a：特征定义能力（DefinesStats）覆盖基础数值
            var statsCda = GetHighestPriorityCDA(entity, CharacteristicType.Toughness);
            if (statsCda != null)
            {
                switch (statsCda.DefinedValue)
                {
                    case ValueTuple<int, int> pt:
                        power = pt.Item1;
                        toughness = pt.Item2;
                        break;
                    case int p:
                        power = p;
                        break;
                }
            }

            // 7b→7e：按子层 + 时间戳顺序应用 L7 效果
            foreach (var effect in GetSortedPTEffects(entity))
            {
                if (effect.IsSwitchEffect)
                {
                    (power, toughness) = (toughness, power);
                    continue;
                }

                if (effect.AffectsCharacteristic(CharacteristicType.Power))
                    power = effect.ApplyIntModification(CharacteristicType.Power, power);
                if (effect.AffectsCharacteristic(CharacteristicType.Toughness))
                    toughness = effect.ApplyIntModification(CharacteristicType.Toughness, toughness);
            }
        }

        private Dictionary<int, float> CalculateCostInternal(Entity entity)
        {
            var baseValue = GetBaseCost(entity);

            foreach (var effect in GetSortedEffectsForEntity(entity))
            {
                if (effect.AffectsCharacteristic(CharacteristicType.Cost))
                    baseValue = effect.ApplyCostModification(baseValue);
            }

            return baseValue;
        }

        private List<ManaType> CalculateColorsInternal(Entity entity)
        {
            var result = GetBaseColors(entity);

            foreach (var effect in GetSortedEffectsForEntity(entity))
            {
                if (effect.AffectsCharacteristic(CharacteristicType.Color))
                    result = effect.ApplyColorModification(result);
            }

            var cda = GetHighestPriorityCDA(entity, CharacteristicType.Color);
            if (cda != null && cda.DefinedValue is List<ManaType> cdaColors)
                result = cdaColors;

            return result;
        }

        #endregion

        #region 便捷 API（供原子效果 Handler 注册连续效果）

        /// <summary>
        /// 注册一个力量/防御增减的连续效果（L7d）。返回句柄以便后续移除。
        /// 这是连续/静态/持续到回合结束的 P/T 改变的标准入口——
        /// 不要直接改实体的基础 _power/_life（那只用于永久变更，如指示物）。
        /// </summary>
        public LayerContinuousEffect AddPowerToughnessBuff(
            Entity target, int deltaPower, int deltaToughness,
            DurationType duration = DurationType.UntilEndOfTurn,
            Entity source = null,
            PowerToughnessSublayer sublayer = PowerToughnessSublayer.Modify)
        {
            var effect = new LayerContinuousEffect
            {
                SourceEntity = source,
                TargetEntity = target,
                Layer = LayerType.PowerToughness,
                PTSublayer = sublayer,
                Duration = duration,
                StartTime = DateTime.Now,
                SequenceNumber = (int)TimestampSystem.NextSequence,
                IsActive = true
            };

            if (deltaPower != 0)
                effect.Modifications[CharacteristicType.Power] =
                    new IntAddModification(CharacteristicType.Power, deltaPower);
            if (deltaToughness != 0)
                effect.Modifications[CharacteristicType.Toughness] =
                    new IntAddModification(CharacteristicType.Toughness, deltaToughness);

            AddLayerContinuousEffect(effect);
            return effect;
        }

        #endregion

        /// <summary>
        /// 添加持续效果
        /// </summary>
        public void AddLayerContinuousEffect(LayerContinuousEffect effect)
        {
            if (effect == null) return;

            // 先快照旧值，加入后再发状态变更事件（含正确 old/new）
            var entity = effect.TargetEntity;
            var affected = effect.Modifications.Keys.ToList();
            var olds = SnapshotCharacteristics(entity, affected);

            if (entity == null)
            {
                _globalEffects.Add(effect);
            }
            else
            {
                if (!_entityEffects.ContainsKey(entity))
                    _entityEffects[entity] = new List<LayerContinuousEffect>();
                _entityEffects[entity].Add(effect);
            }

            MarkDirty(entity);
            PublishCharacteristicChanges(entity, affected, olds);
        }

        /// <summary>
        /// 移除持续效果
        /// </summary>
        public void RemoveLayerContinuousEffect(LayerContinuousEffect effect)
        {
            if (effect == null) return;

            var entity = effect.TargetEntity;
            var affected = effect.Modifications.Keys.ToList();
            var olds = SnapshotCharacteristics(entity, affected);

            if (entity == null)
            {
                _globalEffects.Remove(effect);
            }
            else if (_entityEffects.ContainsKey(entity))
            {
                _entityEffects[entity].Remove(effect);
            }

            MarkDirty(entity);
            PublishCharacteristicChanges(entity, affected, olds);
        }

        /// <summary>
        /// 添加特征定义能力
        /// </summary>
        public void AddCDA(CharacteristicDefiningAbility cda)
        {
            if (cda == null) return;

            var entity = cda.DefinedEntity;
            var characteristic = CharacteristicFromCDA(cda.DefinitionType);
            var olds = SnapshotCharacteristics(entity, new[] { characteristic });

            if (entity == null)
            {
                _globalCDAs.Add(cda);
            }
            else
            {
                if (!_entityCDAs.ContainsKey(entity))
                    _entityCDAs[entity] = new List<CharacteristicDefiningAbility>();
                _entityCDAs[entity].Add(cda);
            }

            MarkDirty(entity);
            PublishCharacteristicChanges(entity, new[] { characteristic }, olds);
        }

        /// <summary>
        /// 移除特征定义能力
        /// </summary>
        public void RemoveCDA(CharacteristicDefiningAbility cda)
        {
            if (cda == null) return;

            var entity = cda.DefinedEntity;
            var characteristic = CharacteristicFromCDA(cda.DefinitionType);
            var olds = SnapshotCharacteristics(entity, new[] { characteristic });

            if (entity == null)
            {
                _globalCDAs.Remove(cda);
            }
            else if (_entityCDAs.ContainsKey(entity))
            {
                _entityCDAs[entity].Remove(cda);
            }

            MarkDirty(entity);
            PublishCharacteristicChanges(entity, new[] { characteristic }, olds);
        }

        /// <summary>
        /// 获取实体的所有排序效果（按层 → 时间戳；L7 用 <see cref="GetSortedPTEffects"/>）
        /// </summary>
        private List<LayerContinuousEffect> GetSortedEffectsForEntity(Entity entity)
        {
            var result = new List<LayerContinuousEffect>();

            result.AddRange(_globalEffects);
            if (entity != null && _entityEffects.ContainsKey(entity))
                result.AddRange(_entityEffects[entity]);

            result.Sort((a, b) =>
            {
                int layerCompare = a.Layer.CompareTo(b.Layer);
                if (layerCompare != 0) return layerCompare;
                return a.SequenceNumber.CompareTo(b.SequenceNumber);
            });

            return result;
        }

        /// <summary>
        /// 获取实体的 L7 力量/防御效果，按子层(7a-7e) → 时间戳排序。
        /// </summary>
        private List<LayerContinuousEffect> GetSortedPTEffects(Entity entity)
        {
            var result = new List<LayerContinuousEffect>();

            foreach (var e in _globalEffects)
                if (e.Layer == LayerType.PowerToughness) result.Add(e);
            if (entity != null && _entityEffects.ContainsKey(entity))
                foreach (var e in _entityEffects[entity])
                    if (e.Layer == LayerType.PowerToughness) result.Add(e);

            result.Sort((a, b) =>
            {
                int sub = a.PTSublayer.CompareTo(b.PTSublayer);
                if (sub != 0) return sub;
                return a.SequenceNumber.CompareTo(b.SequenceNumber);
            });

            return result;
        }

        /// <summary>
        /// 获取最高优先级的CDA（同特征多 CDA 时取最新时间戳，MTG 时间戳规则）
        /// </summary>
        private CharacteristicDefiningAbility GetHighestPriorityCDA(Entity entity, CharacteristicType type)
        {
            var defType = GetCDAFromCharacteristicType(type);
            List<CharacteristicDefiningAbility> candidates = new List<CharacteristicDefiningAbility>();

            candidates.AddRange(_globalCDAs.Where(cda => cda.DefinitionType == defType));

            if (entity != null && _entityCDAs.ContainsKey(entity))
                candidates.AddRange(_entityCDAs[entity].Where(cda => cda.DefinitionType == defType));

            if (candidates.Count == 0)
                return null;

            // 最新时间戳（最大序列号）的 CDA 生效
            candidates.Sort((a, b) => b.SequenceNumber.CompareTo(a.SequenceNumber));
            return candidates[0];
        }

        /// <summary>
        /// 获取基础力量（印刷值 + 永久变更，连续效果不写入此处）
        /// </summary>
        private int GetBasePower(Entity entity)
        {
            if (entity is IHasPower hasPower)
                return hasPower.Power;
            return 0;
        }

        /// <summary>
        /// 获取基础防御/生命
        /// </summary>
        private int GetBaseToughness(Entity entity)
        {
            if (entity is IHasLife hasLife)
                return hasLife.Life;
            return 0;
        }

        /// <summary>
        /// 获取基础费用
        /// </summary>
        private Dictionary<int, float> GetBaseCost(Entity entity)
        {
            if (entity is IHasCost hasCost && hasCost.Cost != null)
                return new Dictionary<int, float>(hasCost.Cost);
            return new Dictionary<int, float>();
        }

        /// <summary>
        /// 获取基础颜色：由费用中出现的有色法力（灰=无色/通用除外）推导。
        /// </summary>
        private List<ManaType> GetBaseColors(Entity entity)
        {
            var result = new List<ManaType>();
            if (entity is IHasCost hasCost && hasCost.Cost != null)
            {
                foreach (var key in hasCost.Cost.Keys)
                {
                    var mt = (ManaType)key;
                    if (mt != ManaType.Gray && !result.Contains(mt))
                        result.Add(mt);
                }
            }
            return result;
        }

        /// <summary>
        /// 将特征类型转换为CDA类型
        /// </summary>
        private CharacteristicDefiningType GetCDAFromCharacteristicType(CharacteristicType type)
        {
            switch (type)
            {
                case CharacteristicType.Color:
                    return CharacteristicDefiningType.DefinesColor;
                case CharacteristicType.Supertype:
                    return CharacteristicDefiningType.DefinesType;
                case CharacteristicType.Subtype:
                    return CharacteristicDefiningType.DefinesSubtype;
                case CharacteristicType.Power:
                case CharacteristicType.Toughness:
                    return CharacteristicDefiningType.DefinesStats;
                default:
                    return CharacteristicDefiningType.DefinesStats;
            }
        }

        private CharacteristicType CharacteristicFromCDA(CharacteristicDefiningType type)
        {
            switch (type)
            {
                case CharacteristicDefiningType.DefinesColor: return CharacteristicType.Color;
                case CharacteristicDefiningType.DefinesType: return CharacteristicType.Supertype;
                case CharacteristicDefiningType.DefinesSubtype: return CharacteristicType.Subtype;
                default: return CharacteristicType.Power;
            }
        }

        #region 状态变更事件

        /// <summary>
        /// 读取指定特征的当前计算值（用于事件 old/new 快照）
        /// </summary>
        private object GetCurrentCharacteristic(Entity entity, CharacteristicType characteristic)
        {
            if (entity == null) return null;
            switch (characteristic)
            {
                case CharacteristicType.Power: return CalculatePower(entity);
                case CharacteristicType.Toughness: return CalculateToughness(entity);
                case CharacteristicType.Color: return CalculateColors(entity);
                case CharacteristicType.Cost: return CalculateCost(entity);
                default: return null;
            }
        }

        private Dictionary<CharacteristicType, object> SnapshotCharacteristics(
            Entity entity, IReadOnlyList<CharacteristicType> characteristics)
        {
            var snap = new Dictionary<CharacteristicType, object>();
            if (entity == null) return snap;
            foreach (var c in characteristics)
                snap[c] = GetCurrentCharacteristic(entity, c);
            return snap;
        }

        private void PublishCharacteristicChanges(
            Entity entity, IReadOnlyList<CharacteristicType> characteristics,
            Dictionary<CharacteristicType, object> oldValues)
        {
            if (entity == null) return;
            foreach (var c in characteristics)
            {
                if (!TryMapStateChangeType(c, out var stateType))
                    continue;

                oldValues.TryGetValue(c, out var oldVal);
                var newVal = GetCurrentCharacteristic(entity, c);

                EventManager.Instance.Publish(new StateChangeEvent
                {
                    Type = stateType,
                    Target = entity,
                    OldValue = oldVal,
                    NewValue = newVal
                });
            }
        }

        private bool TryMapStateChangeType(CharacteristicType c, out StateChangeType type)
        {
            switch (c)
            {
                case CharacteristicType.Power: type = StateChangeType.Power; return true;
                case CharacteristicType.Toughness: type = StateChangeType.Toughness; return true;
                case CharacteristicType.Color: type = StateChangeType.Color; return true;
                case CharacteristicType.Text: type = StateChangeType.Text; return true;
                case CharacteristicType.Supertype:
                case CharacteristicType.Subtype: type = StateChangeType.Type; return true;
                default: type = StateChangeType.Power; return false; // Cost 等无对应事件类型
            }
        }

        #endregion

        /// <summary>
        /// 重新计算所有实体状态（失效缓存，值按需经 Calculate* 读取）
        /// </summary>
        public void RecalculateAll()
        {
            InvalidateAllCache();
        }

        /// <summary>
        /// 重新计算指定实体状态：仅失效其缓存，绝不把计算结果写回基础字段
        /// （写回会把连续效果烘焙进基础值，导致每次重算叠加——已修复）。
        /// </summary>
        private void RecalculateEntity(Entity entity)
        {
            MarkDirty(entity);
        }

        /// <summary>
        /// 检查是否有指定类型的持续效果
        /// </summary>
        public bool HasEffectType(Effect effectType)
        {
            return _globalEffects.Any(e => e.SourceEffect == effectType) ||
                   _entityEffects.Values.Any(list => list.Any(e => e.SourceEffect == effectType));
        }

        /// <summary>
        /// 清空所有效果
        /// </summary>
        public void ClearAll()
        {
            _entityEffects.Clear();
            _entityCDAs.Clear();
            _globalEffects.Clear();
            _globalCDAs.Clear();
            InvalidateAllCache();
        }

        /// <summary>
        /// 处理游戏事件
        /// </summary>
        public void OnEvent<T>(T e) where T : IGameEvent
        {
            switch (e)
            {
                case StateChangeEvent stateChange:
                    RecalculateEntity(stateChange.Target);
                    break;
                case EffectResolveEvent resolveEvent:
                    // 效果结算后失效全部缓存，下次读取重算
                    RecalculateAll();
                    break;
            }
        }
    }
}
