// SPDX-FileCopyrightText: 2026 WWXTT
// SPDX-License-Identifier: BUSL-1.1

using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCore
{
    // ============================================================ G3 · 词条（虚拟原子）定义与展开器（2026-11-08） ============================================================
    // docs/地基计划_虚拟原子与数值A.md §6：
    // · 唯一展开缝 = EffectSlim.ToCardEffect 的 Steps 投影（词条引用 refId="@包id/词条" 原地展开成
    //   N 个内置原子步骤）——展开发生在**校验与计价之前**（装载链/CardLoader 校验/DeriveElementCosts
    //   看到的都是展开后形态），价格守恒与位预算守恒由此自动成立；展开后的 校验→计价→结算→AI→联机 零改动。
    // · 参数绑定走 **string 载体**（{n} 先解析成 "3" 再落 int；P2 表达式化时展开器结构不动）。
    // · 词条没有 cost 字段——价只能是派生量（= 展开后原子锚价之和）。
    // · 深度恒 1：body 只能引用内置原子行 ID，不得引用另一个词条（定义侧装载即拒；展开器只展
    //   顶层步骤、展开产物不再过展开器——结构上无递归可能）。
    // · seal 盖 body/params/slot，不盖 name（ComputeDefSeal）。

    /// <summary>词条定义（atoms.json items 元素；G2 存桩、G3 起消费）。</summary>
    [Serializable]
    public class CompositeAtomDef
    {
        public string id;                        // "@包id/词条名"——与卡/效果同命名空间规则
        public string name;                      // 展示名（不入 seal）
        public CompositeAtomSlot slot;           // 挂载位声明（trunk=占主干槽数·v1 恒 1；domain=词条声明域）
        public List<CompositeAtomParam> @params; // 参数声明（v1 上限：单 Value 参 + 单 Str 参）
        public List<CompositeAtomBodyAtom> body; // 展开体（内置原子行引用 + string 载体绑定）
        public string seal;                      // 行为封印 = ComputeDefSeal（盖 body/params/slot·不盖 name）
    }

    /// <summary>挂载位声明。</summary>
    [Serializable]
    public class CompositeAtomSlot
    {
        public int trunk = 1;      // 占主干槽数（G4 位预算校验第 1 条读它；v1 恒 1）
        public string domain;      // 词条声明域（G4 域一致校验第 2 条读它；子原子域须 ⊆ 此域）
    }

    /// <summary>参数声明。</summary>
    [Serializable]
    public class CompositeAtomParam
    {
        public string key;         // 绑定名（body 中 {key} 引用）
        public string type;        // "Value"（数值参·绑 usage.value）| "Str"（串参·绑 usage.str）
        public int min;            // 取值下界（G4 参数校验）
        public int max;            // 取值上界（G4 参数校验）
        public int defaultValue;   // Value 参缺省（v1 绑定恒取 usage.value，缺省仅文档/校验基准）
        public string defaultStr;  // Str 参缺省（同上）
    }

    /// <summary>展开体原子（与 AtomicEffectEntry 同形，但 value/str 为 **string 载体**——字面量或 {key} 绑定）。</summary>
    [Serializable]
    public class CompositeAtomBodyAtom
    {
        public string refId;       // 内置原子行 ID（深度恒 1——不得为词条 id）
        public string value;       // "{n}" 绑定 或 字面量数字串
        public string str;         // "{s}" 绑定 或 字面量
        public float amp;
        public List<int> kinds;
        public string domain;
    }

    /// <summary>词条表：全部已装包的词条定义（惰性单源；装卸随注册表失效）。
    /// 表侧再守一道深度/行存在闸（绕过装包校验的手摆目录防御）。
    /// **校验期上下文**（PushContext）：装包校验 ⑥ 效果封印须按「展开后」行为计算——候选包词条
    /// 尚未入表，Get 先查上下文（候选定义）再查已装表；验证器算期望封印同口径。</summary>
    public static class CompositeAtomTable
    {
        private static Dictionary<string, CompositeAtomDef> _byId;
        private static List<CompositeAtomDef> _context; // 校验期候选定义（单层·无并发）

        public static void Invalidate()
        {
            _byId = null;
            _context = null;
        }

        /// <summary>校验期候选上下文（装包校验/期望封印计算用——Pop 必须与 Push 成对）。</summary>
        public static void PushContext(List<CompositeAtomDef> defs) => _context = defs;
        public static void PopContext() => _context = null;

        public static List<CompositeAtomDef> GetAll()
        {
            EnsureLoaded();
            return _byId.Values.ToList();
        }

        /// <summary>按 id 取词条（未装/不可用 → null）。可用 = body 全部引用内置原子行（深度 1）。
        /// 上下文优先（候选定义——校验期视图）；瞬态网络内容兜底（2026-11-09 协议 v11：
        /// 对局期对端词条 add-if-missing，本地已装定义优先）。</summary>
        public static CompositeAtomDef Get(string id)
        {
            if (string.IsNullOrEmpty(id) || !id.StartsWith("@", StringComparison.Ordinal)) return null;
            if (_context != null)
                foreach (var d in _context)
                    if (d != null && d.id == id) return d;
            EnsureLoaded();
            if (_byId.TryGetValue(id, out var def)) return def;
            return Network.TransientNetContent.GetAtomDef(id);
        }

        private static void EnsureLoaded()
        {
            if (_byId != null) return;
            _byId = new Dictionary<string, CompositeAtomDef>(StringComparer.Ordinal);
            foreach (var def in ContentPackRegistry.AllAtomDefs())
            {
                if (def == null || string.IsNullOrEmpty(def.id) || def.body == null) continue;
                bool usable = true;
                foreach (var b in def.body)
                {
                    if (b == null || string.IsNullOrEmpty(b.refId)) { usable = false; break; }
                    if (b.refId.StartsWith("@", StringComparison.Ordinal)
                        || Attribute.AtomicEffectTable.GetByHashId(b.refId) == null)
                    {
                        usable = false; // 深度>1 或 行缺表：定义不可用（装包校验本应拦；手摆目录防御）
                        break;
                    }
                }
                if (!usable)
                {
                    TideLog.Warn($"[CompositeAtom] 词条不可用（body 引用非内置行/嵌套词条）: {def.id}——剔除");
                    continue;
                }
                if (_byId.ContainsKey(def.id))
                {
                    TideLog.Warn($"[CompositeAtom] 词条 id 冲突（保先装包）: {def.id}");
                    continue;
                }
                _byId[def.id] = def;
            }
        }
    }

    /// <summary>展开器：唯一展开缝的实现（EffectSlim.ToCardEffect 调用；顶层步骤逐个替换，产物不再展开）。</summary>
    public static class CompositeExpander
    {
        /// <summary>步骤列预处理：词条引用步骤原地（新列表，不改入参缓存）展开成 N 个内置原子步骤；
        /// 非词条步骤直通。展开失败（定义缺失/绑定坏/字面量坏）→ Warn 点名 + **剔该步骤**（可见不炸；
        /// 硬拒绝（整卡断链）归 G4 展开后校验——本层只保不装弱卡进结算）。</summary>
        public static List<StepRef> ExpandSteps(List<StepRef> steps)
        {
            if (steps == null) return null;
            bool anyComposite = false;
            foreach (var s in steps)
                if (s != null && s.kind == 0 && !string.IsNullOrEmpty(s.atom?.refId)
                    && s.atom.refId.StartsWith("@", StringComparison.Ordinal)) { anyComposite = true; break; }
            if (!anyComposite) return steps; // 快路：无词条引用，零分配零改动

            var result = new List<StepRef>(steps.Count);
            foreach (var s in steps)
            {
                if (s == null) continue;
                var def = s.kind == 0 ? CompositeAtomTable.Get(s.atom?.refId) : null;
                if (def == null) { result.Add(s); continue; } // 普通步骤直通（缺表词条=ToEntry 后续点名剔除）
                var expanded = ExpandOne(def, s.atom);
                if (expanded != null) result.AddRange(expanded);
                // null = Warn 已点名，剔该步骤（G4 硬化前可见不炸）
            }
            return result;
        }

        /// <summary>单词条展开（StepRef 层——ToCardEffect 缝用）：BindBody 绑定体包成步骤列。</summary>
        private static List<StepRef> ExpandOne(CompositeAtomDef def, AtomicEffectEntry usage)
        {
            var entries = BindBody(def, usage);
            return entries == null ? null : entries.Select(e => new StepRef { kind = 0, atom = e }).ToList();
        }

        /// <summary>绑定展开体（G5 投影/编辑面展开共用口）：bindings（Value 参←usage.value·
        /// Str 参←usage.str，多同类参=定义非法装载已剔）→ body 逐原子 string 载体解析
        ///（{key} 绑定/字面量）→ AtomicEffectEntry 列表。失败 Warn 点名返回 null。</summary>
        public static List<AtomicEffectEntry> BindBody(CompositeAtomDef def, AtomicEffectEntry usage)
        {
            var intBinds = new Dictionary<string, int>(StringComparer.Ordinal);
            var strBinds = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var p in def.@params ?? new List<CompositeAtomParam>())
            {
                if (p == null || string.IsNullOrEmpty(p.key)) continue;
                if (p.type == "Str") strBinds[p.key] = usage.str ?? p.defaultStr ?? "";
                else intBinds[p.key] = usage.value;
            }

            var result = new List<AtomicEffectEntry>(def.body.Count);
            for (int i = 0; i < def.body.Count; i++)
            {
                var b = def.body[i];
                if (b == null || string.IsNullOrEmpty(b.refId))
                {
                    TideLog.Warn($"[CompositeExpander] 词条 {def.id} body[{i}] 引用为空——剔该步骤");
                    return null;
                }
                if (!TryResolveInt(b.value, intBinds, out int v) || !TryResolveStr(b.str, strBinds, out string sv))
                {
                    TideLog.Warn($"[CompositeExpander] 词条 {def.id} body[{i}]（{b.refId}）参数绑定解析失败"
                                 + $"（value='{b.value}' str='{b.str}'·失败 token={_lastFailToken}）——剔该步骤");
                    return null;
                }
                result.Add(new AtomicEffectEntry
                {
                    refId = b.refId,
                    value = v,
                    str = sv,
                    amp = b.amp,
                    kinds = b.kinds != null && b.kinds.Count > 0 ? new List<int>(b.kinds) : null,
                    domain = b.domain,
                });
            }
            return result;
        }

        /// <summary>编辑面步骤展开（EffectStepData 层——合成器主序列计价/校验用，G5）：kind0 @词条步
        /// → N 个物化 kind0 步；非词条步直通；词条缺失/绑定失败 → Warn + 剔该步（卡级拦截归 G4 装载层）。
        /// 不改入参（合成器编辑图原样——保存写回 @ 引用步不变形）。</summary>
        public static List<EffectStepData> ExpandEditingSteps(List<EffectStepData> steps, string ctx)
        {
            if (steps == null) return null;
            bool any = false;
            foreach (var s in steps)
                if (s != null && s.kind == 0 && !string.IsNullOrEmpty(s.atomic?.refId)
                    && s.atomic.refId.StartsWith("@", StringComparison.Ordinal)) { any = true; break; }
            if (!any) return steps;

            var result = new List<EffectStepData>(steps.Count);
            foreach (var s in steps)
            {
                if (s == null) continue;
                var def = s.kind == 0 ? CompositeAtomTable.Get(s.atomic?.refId) : null;
                if (def == null) { result.Add(s); continue; }
                var entries = BindBody(def, s.atomic);
                if (entries == null)
                {
                    TideLog.Warn($"[CompositeExpander] [{ctx}] 词条步 {s.atomic.refId} 展开失败——转换期剔该步");
                    continue;
                }
                foreach (var e in entries)
                    result.Add(new EffectStepData { kind = 0, atomic = e });
            }
            return result;
        }

        private static string _lastFailToken; // 最近一次解析失败 token（诊断串用——无并发场景）

        /// <summary>{key} → intBinds；字面量数字串 → int；空 → 1（entry.value 缺省口径·与装包校验
        /// TokenResolvable 同口）。失败 false + _lastFailToken 出账。</summary>
        private static bool TryResolveInt(string raw, Dictionary<string, int> binds, out int value)
        {
            value = 1;
            raw = raw ?? "";
            if (raw.Length == 0) return true;
            if (raw.Length >= 3 && raw[0] == '{' && raw[raw.Length - 1] == '}')
            {
                var k = raw.Substring(1, raw.Length - 2);
                _lastFailToken = "{" + k + "}";
                if (!binds.TryGetValue(k, out value)) return false;
                return true;
            }
            _lastFailToken = raw;
            return int.TryParse(raw, out value);
        }

        /// <summary>{key} → strBinds；字面量/空 → 原样。</summary>
        private static bool TryResolveStr(string raw, Dictionary<string, string> binds, out string value)
        {
            raw = raw ?? "";
            if (raw.Length >= 3 && raw[0] == '{' && raw[raw.Length - 1] == '}')
            {
                var k = raw.Substring(1, raw.Length - 2);
                _lastFailToken = "{" + k + "}";
                return binds.TryGetValue(k, out value);
            }
            value = raw;
            return true;
        }
    }

    /// <summary>词条行为封印（装包校验/握手闭包用）：盖 slot/params/body（string 载体原文），不盖 name。
    /// 同 def 改名 seal 不变；改 body/params/slot seal 变。</summary>
    public static class CompositeAtomSeal
    {
        public static string Compute(CompositeAtomDef def)
        {
            if (def == null) return null;
            var sb = new System.Text.StringBuilder();
            sb.Append("ATM|").Append(def.id ?? "").Append('|');
            sb.Append("SLOT:").Append(def.slot?.trunk ?? 1).Append('/').Append(def.slot?.domain ?? "").Append('|');
            sb.Append("PARAMS:");
            foreach (var p in def.@params ?? new List<CompositeAtomParam>())
                sb.Append(p?.key ?? "").Append(':').Append(p?.type ?? "").Append(':')
                  .Append(p?.min ?? 0).Append(':').Append(p?.max ?? 0).Append(':')
                  .Append(p?.defaultValue ?? 0).Append('/').Append(p?.defaultStr ?? "").Append(';');
            sb.Append('|');
            sb.Append("BODY:");
            foreach (var b in def.body ?? new List<CompositeAtomBodyAtom>())
                sb.Append(b?.refId ?? "").Append('|').Append(b?.value ?? "").Append('|').Append(b?.str ?? "")
                  .Append('|').Append(b?.amp ?? 0f).Append('|')
                  .Append(string.Join(",", (b?.kinds ?? new List<int>()).OrderBy(k => k)))
                  .Append('|').Append(b?.domain ?? "").Append(';');
            return MurmurHash3.Hash32(sb.ToString()).ToString("X8");
        }
    }

    /// <summary>词条库行投影（G5 合成器投影 2026-11-08 · 地基计划 §8）——元数据从**展开体**派生，
    /// **不从词条自述读**（slot.domain 等自述只进 G4 校验，不进显示）：价=Σ展开行单价（与库行
    /// AtomCosts 同口径的锚价和）/域=展开行域并集/占槽数=展开原子数/描述=词条名+展开体行描述串联。</summary>
    public sealed class CompositeAtomRowInfo
    {
        public string Id;
        public string Name;
        public int SlotCount;          // 占几槽（展开原子数——位预算守恒的显示面）
        public ElementCost Cost;       // 派生价（展开行 ManaList 逐色和）
        public string DomainUnion;     // 展开行域并集
        public string Description;     // 「名（展开体·占N槽）：行描述；行描述」
    }

    public static class CompositeAtomProjection
    {
        /// <summary>投影（缺省绑定=参数 defaultValue/defaultStr；行缺表按 0 价/跳描述——正常态
        /// 装包与表闸已保证行在表，此为显示层兜底）。</summary>
        public static CompositeAtomRowInfo Project(CompositeAtomDef def)
        {
            var info = new CompositeAtomRowInfo { Id = def?.id, Name = def?.name ?? "", Cost = new ElementCost() };
            if (def == null) return info;
            var usage = new AtomicEffectEntry { refId = def.id };
            foreach (var p in def.@params ?? new List<CompositeAtomParam>())
            {
                if (p == null) continue;
                if (p.type == "Str") usage.str = p.defaultStr ?? usage.str;
                else if (p.defaultValue > 0) usage.value = p.defaultValue;
            }
            var entries = CompositeExpander.BindBody(def, usage) ?? new List<AtomicEffectEntry>();
            info.SlotCount = entries.Count;
            var domains = new List<string>();
            var descs = new List<string>();
            foreach (var e in entries)
            {
                var row = Attribute.AtomicEffectTable.GetByHashId(e?.refId);
                if (row == null) continue;
                if (row.ManaList != null)
                    for (int i = 0; i < ElementCost.Length; i++)
                        info.Cost[(ManaType)i] = info.Cost[(ManaType)i] + row.ManaList[(ManaType)i];
                if (!string.IsNullOrEmpty(row.Domain) && !domains.Contains(row.Domain)) domains.Add(row.Domain);
                descs.Add(string.IsNullOrEmpty(row.Description) ? row.EnumName : row.Description);
            }
            info.DomainUnion = string.Join(";", domains);
            info.Description = $"{def.name}（展开体·占{info.SlotCount}槽）：{string.Join("；", descs)}";
            return info;
        }
    }
}
