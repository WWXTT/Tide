using System;
using System.Collections.Generic;
using System.Linq;
using CardCore.Attribute;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CardCore
{
    // ================================================================
    // 效果瘦存储 DTO（2026-09-14 效果引用化 v2·大修定案；2026-10-09 写侧修订）：
    //
    // 原子不内嵌六字段全量——**引用原子表行 ID**（表首列 8-hex）+ 只存增量：
    //   原子=AtomicEffectEntry { refId(表行ID), value, str, amp, kinds }（本体即引用型）
    // 2026-10-09 写侧按值省略定案（修订 09-14「字段数恒 5」条）：落盘只写**偏离默认的增量**——
    //   默认值列（value=1/str=""/amp=0/kinds 空/count=-2/rand=-1/幽灵 branch/各 0 档 header 列）
    //   一律不写；读侧（JsonUtility/TideJson）对缺失字段回落**字段初始值**——哨兵初始值即语义默认，
    //   旧全量文件与新紧凑文件共存互读（EmitCompact 统一发射口）。
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
    /// 合成期实时推导随效果落盘；派生数据，不入内容哈希）。
    /// value=float（2026-10-30 真实小数定案——效果费保留真值，展示直通、支付终点进1；
    /// 唯一消费方 CostSquaresView）。</summary>
    [Serializable]
    public class ElementCostRef
    {
        public int mana;     // ManaType
        public float value;  // 元素数（真实小数）
    }

    /// <summary>代价引用：type=7(Payload) 时 payload 为错边原子引用；
    /// payloads=组装代价原子序列（2026-11-08 代价效果级化——多原子行落库承载，此前只承
    /// legacy 单原子 payload，多原子过 Effects.json 一趟即丢）。读写两形并存：
    /// AllPayloads() 口径 payloads 优先、legacy 折入。</summary>
    [Serializable]
    public class CostRef
    {
        public int type;
        public int value;
        public int manaType;
        public int turns;
        public AtomicEffectEntry payload;
        public List<AtomicEffectEntry> payloads;
    }

    /// <summary>效果条目（瘦格式——Effects.json items 的元素）。</summary>
    [Serializable]
    public class EffectSlimDto
    {
        public string id;              // 效果 id（2026-11-05 冻结：一经分配永不改写；新效果 = ContentHasher.HashEffect）
        public string hash;            // 内容指纹（= ContentHasher.HashEffect，不含 id 自身）——去重/漂移检测，不参与身份
        public string name;            // 显示名（自动名）
        public int timing;             // TriggerTiming
        public int activation;         // 0=强制 1=自动 2=主动
        public int speed;              // BaseSpeed
        public int limit;              // TriggerLimitPerTurn（历史列·无消费方；保留存档/哈希稳定）
        public int duration;           // DurationType
        public int selection = -1;     // SelectionMode（-1=None；0-5=六值定案 2026-09-16）——初始值=省略哨兵
        public int count = -2;         // TargetCount（>0=N；0=全部；-1=任意[玩家自选数量=0费]；-2=未声明）——初始值=省略哨兵
        public int random;             // RandomTarget（0/1——目标随机正交标志，与"选多少"无关）
        public List<int> kinds;        // 效果级作用范围（2026-10-04 相同目标定案）：null=未声明（空列不写，向后兼容）
        public int dropZone;           // SummonDropZone
        public int arrows;             // 光环形态（2026-09-23）：HexDirection Flags——箭头随效果合成，挂卡并集
        public List<LinkAuraData> linkAuras;  // 光环条目（非光环效果 null——空列不写，向后兼容）

        // 效果锚价（2026-09-23；2026-10-04 位置数组化）：**float 位置数组**（下标=ManaType 枚举序号
        // [灰,红,蓝,绿,白,黑]）——合成期按表累加实时推导落盘；参考快照（2026-10-02 口径修正：
        // 运行时无消费者，显示/预检/扣款一律实时重推导）；装载期逐效果还原为 AnchorCost 缓存；
        // 派生数据不入内容哈希（表变更重算不换 id）。旧 [{mana,value}] 对列表形态已随全表重推退役。
        public List<float> cost;
        // 锚价钉死（2026-10-20）：1=cost 列为权威锚价（手作教学行——显示/预检/扣款直读不实时推导；
        // cost 列缺省/空=0 费钉死）；缺省 0=cost 列纯快照（历史口径）。
        public int costPin;
        public List<CostRef> costs;
        public List<StepRef> steps;
        // 引擎通道（engine/engineParam/rewards 头字段）已随 2026-10-05 两槽定案载荷化退役——
        // 引擎条件挂原子 branch 载荷（AtomicEffectEntry.branch，随 StepRef.atom 往返）。
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
                TideLog.Warn($"[EffectSlim] 原子表引用缺失: {ar.refId}（表无此行）——剔除");
                return null;
            }
            ar.amp = Math.Clamp(ar.amp, 0f, 1f);
            StripPhantomBranches(ar); // JsonUtility 物化的 settle=0 幽灵分支——读档剥离（主干/then 奖励原子统一过此口）
            return ar;
        }

        /// <summary>幽灵 branch 递归剥离（2026-11-02 修复）：JsonUtility 反序列化会给**所有**嵌套
        /// AtomicEffectEntry 的 branch 字段物化 settle=0 幽灵实例——含 branch.then 奖励原子。
        /// 旧口只剥顶层不递归 → LoadAll（工具重推）路径的 then 原子带幽灵入 HashEffect，
        /// 与 EffectsLibrary→ToCardEffect（干净）路径**同内容两套 id**——引擎分支效果每次重推
        /// 被「换号+卡保存链路回写旧号」双写成重复条目（训练池 9 条 A/B 双写的根因）。
        /// 本口递归归一：任一路径产出的原子图幽灵全剥，HashEffect 两路径字节一致。</summary>
        private static void StripPhantomBranches(AtomicEffectEntry a)
        {
            if (a?.branch == null) return;
            if (BranchEntryRules.IsPhantom(a.branch)) { a.branch = null; return; }
            if (a.branch.then == null) return;
            foreach (var t in a.branch.then)
            {
                if (t == null) continue;
                if (t.branch != null && BranchEntryRules.IsPhantom(t.branch)) t.branch = null;
                else StripPhantomBranches(t); // 深层嵌套防御（理论不出现——converter 拒深层）
            }
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
                TideLog.Warn($"[EffectSlim] 原子表引用缺失: {a.refId}——不入瘦格式");
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

        /// <summary>CostEntry → CostRef（payload 原子转引用；payloads 列表同转——2026-11-08 效果级代价多原子承载）。</summary>
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
                payloads = c.payloads != null && c.payloads.Count > 0
                    ? c.payloads.Where(p => p != null && !string.IsNullOrEmpty(p.refId)).Select(ToRef).ToList()
                    : null,
            };
        }

        /// <summary>CostRef → CostEntry（payloads 列表还原；legacy 单原子 payload 读兼容）。</summary>
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
                payloads = cr.payloads != null && cr.payloads.Count > 0
                    ? cr.payloads.Where(p => p != null).Select(ToEntry).ToList()
                    : null,
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
                TargetKinds = dto.kinds != null && dto.kinds.Count > 0
                    ? new List<int>(dto.kinds) : null, // 效果级作用范围（2026-10-04）——空列=未声明
                SummonDropZone = dto.dropZone,
                ArrowDirections = dto.arrows,
                LinkAuras = dto.linkAuras != null && dto.linkAuras.Count > 0
                    ? dto.linkAuras.Where(a => a != null
                        && (!string.IsNullOrEmpty(a.stat) || !string.IsNullOrEmpty(a.keyword)
                            || !string.IsNullOrEmpty(a.effectRef))).ToList()
                    : null, // 光环条目（2026-09-23 效果级）——无效条目（三空）装载期即丢弃；
                            // effectRef 条目（2026-11-05 效果光环）第四处三空口径与装载/聚合/发射同源
                AnchorCost = dto.cost != null && dto.cost.Count > 0
                    ? new ElementCost(dto.cost.ToArray())
                    : null, // 锚价缓存（2026-09-23；2026-10-04 位置数组）——装载期逐效果建立
                AnchorCostPinned = dto.costPin, // 锚价钉死（2026-10-20）：1=上行 cost 列为权威
                Costs = ToCostEntries(dto.costs),
                Steps = dto.steps != null
                    ? dto.steps.Select(ToStep).Where(st => st != null).ToList() : null,
            };
            return fx;
        }

        // ======================================== 紧凑发射（2026-10-09 写侧按值省略定案） ========================================

        /// <summary>瘦格式紧凑落盘文本（{"items":[…]} 缩进 JSON）：只写偏离默认的增量列——
        /// 读侧 JsonUtility/TideJson 对缺失字段回落字段初始值（哨兵初始值=语义默认），
        /// 旧全量文件共存互读；内容哈希在内存图上计算（EffectLibrarySerializer.Save 前置），
        /// 发射不触碰内存值——id 零漂移。发射器居 CardCore=Unity/TideServer 双端同源（无头往返可测）。</summary>
        public static string EmitCompact(List<EffectSlimDto> items)
        {
            var arr = new JArray();
            if (items != null)
                foreach (var dto in items)
                {
                    var o = EmitEffect(dto);
                    if (o != null) arr.Add(o);
                }
            return new JObject { ["items"] = arr }.ToString(Formatting.Indented);
        }

        private static JObject EmitEffect(EffectSlimDto d)
        {
            if (d == null) return null;
            var o = new JObject { ["id"] = d.id, ["name"] = d.name };
            if (!string.IsNullOrEmpty(d.hash)) o["hash"] = d.hash; // 内容指纹（2026-11-05 ID 冻结配套）
            if (d.timing != 0) o["timing"] = d.timing;
            if (d.activation != 0) o["activation"] = d.activation;
            if (d.speed != 0) o["speed"] = d.speed;
            if (d.limit != 0) o["limit"] = d.limit;
            if (d.duration != 0) o["duration"] = d.duration;
            if (d.selection != -1) o["selection"] = d.selection;
            if (d.count != -2) o["count"] = d.count;
            if (d.random != 0) o["random"] = d.random;
            if (d.dropZone != 0) o["dropZone"] = d.dropZone;
            if (d.arrows != 0) o["arrows"] = d.arrows;
            if (d.kinds != null && d.kinds.Count > 0) o["kinds"] = new JArray(d.kinds);
            if (d.linkAuras != null && d.linkAuras.Count > 0)
            {
                var auras = new JArray(d.linkAuras.Where(a => a != null).Select(EmitLinkAura).Where(x => x != null));
                if (auras.Count > 0) o["linkAuras"] = auras;
            }
            if (d.cost != null && d.cost.Count > 0)
                o["cost"] = new JArray(d.cost.Select(v => Math.Round((double)v, 3))); // float32 尾噪截断（显示快照列）
            if (d.costPin != 0) o["costPin"] = d.costPin; // 锚价钉死（2026-10-20）——往返保真
            if (d.costs != null && d.costs.Count > 0)
            {
                var costs = new JArray(d.costs.Where(c => c != null).Select(EmitCost).Where(x => x != null));
                if (costs.Count > 0) o["costs"] = costs;
            }
            if (d.steps != null && d.steps.Count > 0)
            {
                var steps = new JArray(d.steps.Where(s => s != null).Select(EmitStep).Where(x => x != null));
                if (steps.Count > 0) o["steps"] = steps;
            }
            return o;
        }

        /// <summary>光环条目：stat/keyword/effectRef 三空=无效条目不写；value/scope 非零才写。
        /// public=卡表发射（CardConfigCompact）共用同源口径（2026-10-13）。
        /// effectRef 非空才写=存量零漂移（P2-2 domain 先例，2026-11-05 效果光环）。</summary>
        public static JObject EmitLinkAura(LinkAuraData a)
        {
            if (string.IsNullOrEmpty(a.stat) && string.IsNullOrEmpty(a.keyword)
                && string.IsNullOrEmpty(a.effectRef)) return null;
            var o = new JObject();
            if (!string.IsNullOrEmpty(a.stat)) o["stat"] = a.stat;
            if (a.value != 0) o["value"] = a.value;
            if (!string.IsNullOrEmpty(a.keyword)) o["keyword"] = a.keyword;
            if (!string.IsNullOrEmpty(a.effectRef)) o["effectRef"] = a.effectRef;
            if (a.scope != 0) o["scope"] = a.scope;
            return o;
        }

        /// <summary>代价条目（稀有列）：type/value/manaType/turns 四整数恒写（0 可能是合法枚举位——不做按值省略）；
        /// payloads 列表非空才写（2026-11-08 效果级代价多原子——空表不写，legacy 单原子 payload 存量零漂移）。</summary>
        private static JObject EmitCost(CostRef c)
        {
            var o = new JObject
            {
                ["type"] = c.type,
                ["value"] = c.value,
                ["manaType"] = c.manaType,
                ["turns"] = c.turns,
            };
            var p = EmitAtom(c.payload);
            if (p != null) o["payload"] = p;
            if (c.payloads != null && c.payloads.Count > 0)
            {
                var arr = new JArray(c.payloads.Where(a => a != null).Select(EmitAtom).Where(x => x != null));
                if (arr.Count > 0) o["payloads"] = arr;
            }
            return o;
        }

        private static JObject EmitStep(StepRef s)
        {
            var o = new JObject { ["kind"] = s.kind };
            switch (s.kind)
            {
                case 0:
                    var a = EmitAtom(s.atom);
                    if (a == null) return null; // 空引用原子（行缺失已被 ToStepRef 拒）——兜底不写
                    o["atom"] = a;
                    break;
                case 1:
                    if (string.IsNullOrEmpty(s.gid)) return null; // 无条件 id 的门步骤=无效载荷
                    o["gid"] = s.gid;
                    if (s.gparam != 0) o["gparam"] = s.gparam;
                    if (!string.IsNullOrEmpty(s.gstr)) o["gstr"] = s.gstr;
                    var then = EmitAtoms(s.then);
                    if (then.Count > 0) o["then"] = then;
                    var els = EmitAtoms(s.els);
                    if (els.Count > 0) o["els"] = els;
                    break;
                case 2:
                    if (s.choices == null || s.choices.Count == 0) return null;
                    var choices = new JArray(s.choices.Where(c => c != null).Select(EmitChoice).Where(x => x != null));
                    if (choices.Count == 0) return null;
                    o["choices"] = choices;
                    break;
                default:
                    return null; // 越界 kind 不落盘
            }
            return o;
        }

        private static JObject EmitChoice(ChoiceRef c)
        {
            var o = new JObject();
            if (!string.IsNullOrEmpty(c.label)) o["label"] = c.label;
            if (c.steps != null && c.steps.Count > 0)
            {
                var steps = new JArray(c.steps.Where(s => s != null).Select(EmitStep).Where(x => x != null));
                if (steps.Count > 0) o["steps"] = steps;
            }
            return o;
        }

        /// <summary>public=卡表发射（CardConfigCompact.payload）共用同源口径（2026-10-13）。</summary>
        public static JArray EmitAtoms(List<AtomicEffectEntry> atoms)
        {
            var arr = new JArray();
            if (atoms == null) return arr;
            foreach (var a in atoms)
            {
                var o = EmitAtom(a);
                if (o != null) arr.Add(o);
            }
            return arr;
        }

        /// <summary>原子紧凑发射：恒写 refId；增量只在偏离默认时写（value≠1 / str 非空 / amp≠0 /
        /// kinds 非空 / domain 非空（2026-11-04 P0-1 域词表）/ count≠-2 / rand≠-1）。
        /// branch=null 或幽灵（settle 越界——JsonUtility 物化残渣）
        /// 一律不写；非幽灵 branch 恒带 then 数组（空数组也写——读侧/UI 就地 Add 不判 null）。
        /// public=卡表发射（CardConfigCompact.payload）共用同源口径（2026-10-13）。</summary>
        public static JObject EmitAtom(AtomicEffectEntry a)
        {
            if (a == null || string.IsNullOrEmpty(a.refId)) return null;
            var o = new JObject { ["refId"] = a.refId };
            if (a.value != 1) o["value"] = a.value;
            if (!string.IsNullOrEmpty(a.str)) o["str"] = a.str;
            if (a.amp != 0f) o["amp"] = a.amp;
            if (a.kinds != null && a.kinds.Count > 0) o["kinds"] = new JArray(a.kinds);
            if (!string.IsNullOrEmpty(a.domain)) o["domain"] = a.domain; // 域词表声明（P0-1）——按值省略：空=不写=旧档缺键回落 null
            if (a.count != -2) o["count"] = a.count;
            if (a.rand != -1) o["rand"] = a.rand;
            if (a.valueMax != 0) o["valueMax"] = a.valueMax; // 数值静态上界（步6：按值省略——0=未声明，存量零漂移）
            if (!string.IsNullOrEmpty(a.valueExpr)) o["valueExpr"] = a.valueExpr; // 动态值表达式（步6：白名单 ref.power/life/cost）
            if (!BranchEntryRules.IsPhantom(a.branch))
            {
                var b = new JObject { ["settle"] = a.branch.settle };
                if (!string.IsNullOrEmpty(a.branch.gateId)) b["gateId"] = a.branch.gateId;
                if (!string.IsNullOrEmpty(a.branch.outcomeId)) b["outcomeId"] = a.branch.outcomeId;
                if (a.branch.condParam != 0) b["condParam"] = a.branch.condParam;
                if (!string.IsNullOrEmpty(a.branch.condStr)) b["condStr"] = a.branch.condStr;
                if (a.branch.engine != 0) b["engine"] = a.branch.engine;
                if (a.branch.engineParam != 0) b["engineParam"] = a.branch.engineParam;
                if (a.branch.tactivate != -1) b["tactivate"] = a.branch.tactivate; // 奖励发动方式（-1=随主干·省略）
                if (!string.IsNullOrEmpty(a.branch.effectId)) b["effectId"] = a.branch.effectId; // 奖励效果引用（2026-11-07 分支奖励效果化——非空才写=存量零漂移）
                b["then"] = EmitAtoms(a.branch.then);
                o["branch"] = b;
            }
            return o;
        }
    }
}
