using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.RegularExpressions;

// AOT 泛型缺口审计（2026-11-09 阶段B：.ztmp/aotaudit 原型转正，规格=«AOT泛型门禁_实施计划» §3.1/§3.2）。
// 背景：热更代码调用「AOT 未实例化的值类型泛型」时，HybridCLR 真机抛 MissingMethodException
// （常被上层 catch 吞掉，只能真机排障）。本工具静态枚举热更 DLL 的全部泛型实例化
// （MethodSpec/TypeSpec），把「值类型实参 × 定义程序集不在 patchAOTAssemblies」的条目圈入
// 门禁集合，与签入仓库的基线文件对账：
//   - 新增（DLL 有、基线无）→ exit 3，打包门禁拦截；
//   - 删除（基线有、DLL 无）→ 提示可清理，不失败；
//   - anchored 条目做弱校验（warn-only）：锚点目录里 grep API 名 + 类型实参关键字。
// 噪声归一（§3.1.4）：编译器生成条目（async 状态机 <X>d__N / 本地函数 g__ / <>c 闭包）的
// 身份键剥掉 d__ 编号再入基线对账——编号每次重编都会漂移，不归一则改一行代码就假报警。
// 门禁豁免两类：定义在 patchAOTAssemblies（mscorlib/System/System.Core，运行时由补充元数据
// 兜底——集合须与 Assets/Scripts/Launch/HotUpdateLoader.cs 的 PatchAotAssemblies 一致）与
// 定义在热更程序集自身（HybridCLR 解释执行，无 AOT 实例化需求）。
//
// 用法（在仓库根执行）：
//   dotnet run --project tools/AotAudit -- Assets/HotUpdateDlls --baseline Config/aot_generic_baseline.txt
//   可选：--report <path> 全量报告落盘；--emit-baseline <path> 用当前扫描生成基线草稿（首建用）；
//         --target Android（保留位，暂不区分行为）；--anchor-dir <dir>（默认 Assets/Scripts/Launch）；
//         --contains <kw> 过滤输出；--all 输出全部条目（含非门禁）。
// 退出码：0=通过；3=有新增未处置条目；2=环境/参数/基线格式错误。
// 输出确定性：所有清单按签名稳定排序，不含时间戳——同一 DLL 连跑两次字节级一致（可 diff）。

const int ExitOk = 0;
const int ExitNew = 3;
const int ExitEnv = 2;
// 与 HotUpdateLoader.PatchAotAssemblies 保持一致（该列表变更时同步改这里与源码两侧）
string[] patchAssemblies = { "mscorlib", "System", "System.Core" };
// BCL 门面豁免：Unity .NET Framework 档位下热更 DLL 对 mscorlib/System 系类型的 AssemblyRef
// 大多指向 netstandard 门面（编译期引用；il2cpp 裁剪产物里并无此文件），运行时类型统一回
// mscorlib/System——与 patchAOT 同由补充元数据兜底，故并入豁免。若未来切 .NET Standard 档
// （patch 列表同步加 netstandard）本集合自动失效为零。
string[] bclFacadeAssemblies = { "netstandard" };

try
{
    return Run(args, patchAssemblies, bclFacadeAssemblies);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"[AotAudit] 环境错误：{ex.Message}");
    return ExitEnv;
}

static int Run(string[] args, string[] patchAssemblies, string[] bclFacadeAssemblies)
{
    // ---- 参数 ----
    var inputs = new List<string>();
    string? baselinePath = null, reportPath = null, emitPath = null, contains = null;
    string target = "Android", anchorDir = "Assets/Scripts/Launch";
    bool all = false;
    for (int i = 0; i < args.Length; i++)
    {
        string Take() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"参数 {args[i]} 缺值");
        switch (args[i])
        {
            case "--baseline": baselinePath = Take(); break;
            case "--report": reportPath = Take(); break;
            case "--emit-baseline": emitPath = Take(); break;
            case "--target": target = Take(); break;
            case "--anchor-dir": anchorDir = Take(); break;
            case "--contains": contains = Take(); break;
            case "--all": all = true; break;
            case "-h" or "--help": PrintHelp(); return ExitOk;
            default:
                if (args[i].StartsWith("--")) throw new ArgumentException($"未知参数：{args[i]}（--help 看用法）");
                inputs.Add(args[i]); break;
        }
    }
    if (inputs.Count == 0) { PrintHelp(); return ExitEnv; }

    // ---- 输入展开：目录 → 顶层 *.dll / *.dll.bytes ----
    var files = new List<string>();
    foreach (var p in inputs)
    {
        if (Directory.Exists(p))
            files.AddRange(Directory.EnumerateFiles(p, "*", SearchOption.TopDirectoryOnly)
                .Where(f => f.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                         || f.EndsWith(".dll.bytes", StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f, StringComparer.Ordinal));
        else if (File.Exists(p)) files.Add(p);
        else throw new ArgumentException($"输入不存在：{p}");
    }
    if (files.Count == 0) throw new ArgumentException("没有可扫描的 DLL（目录里无 *.dll / *.dll.bytes）");

    // ---- 扫描 ----
    var entries = new Dictionary<string, Entry>(StringComparer.Ordinal); // sig → 汇总条目（跨程序集去重）
    var selfAssemblies = new HashSet<string>(StringComparer.Ordinal);
    var fileStats = new List<(string File, string Asm, int Total, int Value)>();
    foreach (var f in files) ScanFile(f, entries, selfAssemblies, fileStats);

    // ---- 分类 ----
    var exempt = new HashSet<string>(patchAssemblies.Concat(bclFacadeAssemblies), StringComparer.Ordinal);
    foreach (var e in entries.Values)
    {
        e.Noise = IsCompilerNoise(e.Sig);
        e.Gated = e.ValueHint && !exempt.Contains(e.DefAsm) && !selfAssemblies.Contains(e.DefAsm);
    }
    var gated = entries.Values.Where(e => e.Gated).OrderBy(e => e.Sig, StringComparer.Ordinal).ToList();

    // ---- 报告落盘（全量）----
    if (reportPath != null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# AotAudit 全量报告——kind\tsig\tdefAsm\tvalueArgs\tgated\tnoise\tsources");
        foreach (var e in entries.Values.OrderBy(e => e.Sig, StringComparer.Ordinal))
            sb.AppendLine($"{e.Kind}\t{e.Sig}\t{e.DefAsm}\t{e.ValueHint}\t{e.Gated}\t{e.Noise}\t{string.Join(",", e.Sources)}");
        File.WriteAllText(reportPath, sb.ToString());
        Console.WriteLine($"[AotAudit] 全量报告已写：{reportPath}（{entries.Count} 条）");
    }

    // ---- 基线对账 ----
    Console.WriteLine($"[AotAudit] target={target}；patchAOT 兜底={string.Join("|", patchAssemblies)}；热更自身定义豁免={string.Join("|", selfAssemblies)}");
    foreach (var (f, asm, total, value) in fileStats)
        Console.WriteLine($"[AotAudit] 扫描 {asm}（{Path.GetFileName(f)}）：泛型实例化 {total} 条，其中值类型实参 {value} 条");
    Console.WriteLine($"[AotAudit] 门禁集合（值类型实参 × 定义程序集非 patch 非热更自身）：{gated.Count} 条");

    Dictionary<string, (string Disp, string Note)> baseline = new(StringComparer.Ordinal);
    bool baselineMissing = baselinePath != null && !File.Exists(baselinePath);
    if (baselinePath != null && !baselineMissing) baseline = LoadBaseline(baselinePath);

    var fresh = gated.Where(e => !baseline.ContainsKey(e.Sig)).ToList();
    var removed = baseline.Keys.Where(sig => !entries.TryGetValue(sig, out var e) || !e.Gated)
        .OrderBy(s => s, StringComparer.Ordinal).ToList();
    int a = baseline.Values.Count(v => v.Disp == "anchored");
    int s = baseline.Values.Count(v => v.Disp == "safe");
    int n = baseline.Values.Count(v => v.Disp == "noise");
    Console.WriteLine($"[AotAudit] 基线 {Path.GetFileName(baselinePath ?? "-")}：已处置 {baseline.Count}（anchored {a} / safe {s} / noise {n}）· 新增未处置 {fresh.Count} · 可清理 {removed.Count}");
    if (baselineMissing)
        Console.WriteLine($"[AotAudit] 基线文件不存在：{baselinePath}——首次接入请用 --emit-baseline 生成、逐条处置后签入（本次按全量新增计）");

    // ---- --emit-baseline：草稿生成（noise 自动归类，其余标 safe 待人工复核）----
    if (emitPath != null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# aot_generic_baseline.txt — 由 AotAudit 生成/维护，与热更 DLL 一一对应（«AOT泛型门禁_实施计划» §3.2）");
        sb.AppendLine("# 格式：<签名>\t<处置: anchored|safe|noise>\t<备注>");
        sb.AppendLine($"# 门禁口径：值类型实参 × 定义程序集非 patchAOT（{string.Join("|", patchAssemblies)} + 门面 {string.Join("|", bclFacadeAssemblies)}）非热更自身");
        sb.AppendLine("# anchored=已在 AOT 侧锚点（Assets/Scripts/Launch/HotUpdateLoader.cs 静态构造，弱校验）");
        sb.AppendLine("# safe=评估安全（写理由：共享形态覆盖/补充元数据兜底/该组合不可达等）；noise=编译器生成名（async 状态机/闭包/匿名类型）");
        foreach (var e in gated)
        {
            string disp = e.Noise ? "noise" : "safe";
            string note = e.Noise ? "编译器生成（async 状态机/闭包/匿名类型）" : "初始存量快照（2026-11-09）·未逐条复核";
            sb.AppendLine($"{e.Sig}\t{disp}\t{note}\t# ← {string.Join(",", e.Sources.Select(Path.GetFileName))}");
        }
        File.WriteAllText(emitPath, sb.ToString());
        Console.WriteLine($"[AotAudit] 基线草稿已生成：{emitPath}（{gated.Count} 条；noise 自动归类 {gated.Count(e => e.Noise)}，其余 safe 待人工复核——已锚定的请改 anchored）");
    }

    // ---- 新增清单 + 处置指引 ----
    foreach (var e in fresh.Where(Filter)) 
        Console.WriteLine($"[新增未处置] {e.Kind,-6} {e.Sig}   ← {string.Join(",", e.Sources.Select(Path.GetFileName))}");
    if (fresh.Count > 0)
    {
        Console.WriteLine("[处置指引] ① AOT 侧补锚点（Assets/Scripts/Launch/HotUpdateLoader.cs 静态构造显式引用该组合）→ 基线标 anchored");
        Console.WriteLine("           ② 评估安全（引用类型共享形态/补充元数据兜底/不可达）→ 标 safe 并写理由");
        Console.WriteLine("           ③ 编译器生成名（<X>d__N/<>c/匿名类型）→ 标 noise");
    }

    // ---- 可清理提示 ----
    foreach (var sig in removed.Where(FilterSig))
        Console.WriteLine($"[可清理] 基线有、DLL 已无（或已非门禁）：{sig}");

    // ---- anchored 弱校验（warn-only）----
    if (baseline.Values.Any(v => v.Disp == "anchored"))
        foreach (var warn in WeakAnchorCheck(baseline, gated, anchorDir))
            Console.WriteLine($"[warn][anchored弱校验] {warn}");

    // ---- --all / --contains 辅助查看 ----
    if (all)
        foreach (var e in entries.Values.OrderBy(e => e.Sig, StringComparer.Ordinal).Where(Filter2))
            Console.WriteLine($"[条目] {e.Kind,-6} {(e.Gated ? "门禁" : "豁免")} {(e.ValueHint ? "值参" : "引用")} {e.DefAsm,-12} {e.Sig}");

    bool Filter(Entry e) => contains == null || e.Sig.Contains(contains, StringComparison.OrdinalIgnoreCase);
    bool FilterSig(string s) => contains == null || s.Contains(contains, StringComparison.OrdinalIgnoreCase);
    bool Filter2(Entry e) => contains == null || e.Sig.Contains(contains, StringComparison.OrdinalIgnoreCase);

    int exit = fresh.Count > 0 ? ExitNew : ExitOk;
    Console.WriteLine($"[AotAudit] 结果：{(exit == ExitOk ? "通过（无新增未处置）" : $"拦截（新增未处置 {fresh.Count} 条）")}，exit {exit}");
    return exit;
}

static void PrintHelp()
{
    Console.WriteLine("用法：dotnet run --project tools/AotAudit -- <dll或目录…> [选项]");
    Console.WriteLine("  --baseline <path>    基线文件（对账门禁；缺省=仅审计不门禁）");
    Console.WriteLine("  --emit-baseline <p>  用当前扫描生成基线草稿（首次接入用，noise 自动归类其余标 safe）");
    Console.WriteLine("  --report <path>      全量报告落盘（kind/sig/defAsm/value/gated/sources）");
    Console.WriteLine("  --anchor-dir <dir>   anchored 弱校验的锚点目录（默认 Assets/Scripts/Launch）");
    Console.WriteLine("  --target <name>      目标平台（保留位，默认 Android，暂不区分行为）");
    Console.WriteLine("  --contains <kw>      过滤输出（调试用；不影响 exit 判定范围）");
    Console.WriteLine("  --all                输出全部条目（含豁免类）");
    Console.WriteLine("退出码：0=通过；3=有新增未处置；2=环境/参数错误");
}

static void ScanFile(string path, Dictionary<string, Entry> acc, HashSet<string> selfAssemblies, List<(string, string, int, int)> fileStats)
{
    using var fs = File.OpenRead(path);
    using var pe = new PEReader(fs);
    var md = pe.GetMetadataReader();
    string selfName = md.GetTableRowCount(TableIndex.Assembly) > 0
        ? md.GetString(md.GetAssemblyDefinition().Name) : Path.GetFileNameWithoutExtension(path);
    selfAssemblies.Add(selfName);
    var provider = new NameProvider(md, selfName);
    int total = 0, valueCount = 0;

    for (int row = 1; row <= md.GetTableRowCount(TableIndex.MethodSpec); row++)
    {
        var ms = md.GetMethodSpecification(MetadataTokens.MethodSpecificationHandle(row));
        var args = ms.DecodeSignature(provider, null);
        string decl, name, defAsm;
        switch (ms.Method.Kind)
        {
            case HandleKind.MemberReference:
                var mr = md.GetMemberReference((MemberReferenceHandle)ms.Method);
                (decl, defAsm) = ParentOf(md, provider, mr.Parent);
                name = md.GetString(mr.Name);
                break;
            case HandleKind.MethodDefinition:
                var mdef = md.GetMethodDefinition((MethodDefinitionHandle)ms.Method);
                decl = md.GetString(md.GetTypeDefinition(mdef.GetDeclaringType()).Name);
                defAsm = selfName;
                name = md.GetString(mdef.Name);
                break;
            default:
                decl = name = defAsm = "?";
                break;
        }
        string sig = $"{decl}.{name}<{string.Join(",", args.Select(a => a.Display))}>";
        bool valueHint = args.Any(a => a.ValueHint);
        total++; if (valueHint) valueCount++;
        Add(acc, "method", sig, defAsm, valueHint, path);
    }

    for (int row = 1; row <= md.GetTableRowCount(TableIndex.TypeSpec); row++)
    {
        var ts = md.GetTypeSpecification(MetadataTokens.TypeSpecificationHandle(row));
        var info = ts.DecodeSignature(provider, null);
        total++; if (info.ValueHint) valueCount++;
        Add(acc, "type", info.Display, info.Assembly ?? "?", info.ValueHint, path);
    }

    fileStats.Add((path, selfName, total, valueCount));
}

static void Add(Dictionary<string, Entry> acc, string kind, string sig, string defAsm, bool valueHint, string source)
{
    // 噪声归一（§3.1.4，2026-11-09 首次出包拦截实录）：async 状态机编号 d__N 随重编漂移，
    // 不归一的话改一行代码就产生「新增+可清理」假报警。归一只剥编号（<X>d__248/249 → <X>d__），
    // 方法改名仍会如实进出（应见）。
    if (IsCompilerNoise(sig)) sig = Regex.Replace(sig, @"d__\d+", "d__");
    if (!acc.TryGetValue(sig, out var e))
    {
        e = new Entry { Kind = kind, Sig = sig, DefAsm = defAsm, ValueHint = valueHint };
        acc[sig] = e;
    }
    e.Sources.Add(source);
    if (e.DefAsm == "?") e.DefAsm = defAsm; // 先「?」后解析到的补上
}

static (string Display, string Asm) ParentOf(MetadataReader md, NameProvider p, EntityHandle parent) => parent.Kind switch
{
    HandleKind.TypeReference => (NameProvider.StripArity(md.GetString(md.GetTypeReference((TypeReferenceHandle)parent).Name)),
                                 p.ResolveAssembly((TypeReferenceHandle)parent)),
    HandleKind.TypeDefinition => (NameProvider.StripArity(md.GetString(md.GetTypeDefinition((TypeDefinitionHandle)parent).Name)), p.SelfName),
    HandleKind.TypeSpecification => md.GetTypeSpecification((TypeSpecificationHandle)parent).DecodeSignature(p, null) is var i
                                    ? (i.Display, i.Assembly ?? "?") : ("?", "?"),
    _ => ("?", "?"),
};

/// <summary>编译器生成名归 noise（§3.1.4：只打标不丢弃，避免每次新增 async 方法都报警）。
/// d__ 匹配不要求尾随数字——归一化后的键（&lt;X&gt;d__）也要能判中。</summary>
static bool IsCompilerNoise(string sig)
    => sig.Contains("<>")                                    // <>c / <>c__DisplayClass / <>f__AnonymousType
    || Regex.IsMatch(sig, @"<\w+>d__")                       // <Xxx>d__N async/迭代状态机（含归一化后无编号形态）
    || sig.Contains("g__")                                   // 本地函数生成名 g__Xxx|0（闭包/状态机尾巴）
    || sig.Contains("__AnonymousType");

/// <summary>载入基线：sig\t处置\t备注（# 注释行跳过）。格式/处置词非法 → 抛错（exit 2）。
/// 键同扫侧做噪声归一（d__N 剥编号），存量带编号的行载入时自动并入归一键。</summary>
static Dictionary<string, (string Disp, string Note)> LoadBaseline(string path)
{
    var map = new Dictionary<string, (string, string)>(StringComparer.Ordinal);
    foreach (var raw in File.ReadAllLines(path))
    {
        var line = raw.Trim();
        if (line.Length == 0 || line.StartsWith("#")) continue;
        var parts = line.Split('\t');
        if (parts.Length < 2) throw new FormatException($"基线行缺列（须 签名\\t处置\\t备注）：{line}");
        string sig = parts[0].Trim(), disp = parts[1].Trim();
        string note = parts.Length > 2 ? string.Join("\t", parts.Skip(2)).Trim() : "";
        if (disp is not ("anchored" or "safe" or "noise"))
            throw new FormatException($"基线处置非法（{disp}，须 anchored|safe|noise）：{line}");
        if (IsCompilerNoise(sig)) sig = Regex.Replace(sig, @"d__\d+", "d__");
        map[sig] = (disp, note);
    }
    return map;
}

/// <summary>anchored 弱校验（§3.2：warn-only）：锚点目录全量 .cs 里须能 grep 到「API 名 + 类型关键字」。
/// 先跑一周稳定后再升级硬失败（计划 §5）。</summary>
static IEnumerable<string> WeakAnchorCheck(Dictionary<string, (string Disp, string Note)> baseline, List<Entry> gated, string anchorDir)
{
    if (!Directory.Exists(anchorDir)) { yield return $"锚点目录不存在（{anchorDir}），跳过弱校验"; yield break; }
    var content = new StringBuilder();
    foreach (var f in Directory.EnumerateFiles(anchorDir, "*.cs", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
        content.AppendLine(File.ReadAllText(f));
    string text = content.ToString();

    var sharpNames = new Dictionary<string, string[]>
    {
        ["Boolean"] = new[] { "bool", "Boolean" }, ["Byte"] = new[] { "byte" }, ["SByte"] = new[] { "sbyte" },
        ["Int16"] = new[] { "short" }, ["UInt16"] = new[] { "ushort" }, ["Int32"] = new[] { "int", "Int32" },
        ["UInt32"] = new[] { "uint" }, ["Int64"] = new[] { "long" }, ["UInt64"] = new[] { "ulong" },
        ["Single"] = new[] { "float" }, ["Double"] = new[] { "double", "Double" }, ["Char"] = new[] { "char" },
        ["Decimal"] = new[] { "decimal" }, ["Object"] = new[] { "object", "Object" }, ["String"] = new[] { "string", "String" },
        ["ValueTuple"] = new[] { "ValueTuple", ", " },
    };

    foreach (var (sig, (disp, _)) in baseline.OrderBy(kv => kv.Key, StringComparer.Ordinal))
    {
        if (disp != "anchored") continue;
        int lt = sig.IndexOf('<');
        if (lt < 0) { yield return $"{sig}：签名无泛型段，anchored 标记可疑"; continue; }
        string head = sig[..lt];
        string api = head[(head.LastIndexOf('.') + 1)..];
        string argPart = sig[lt..];
        var keys = sharpNames.Where(kv => argPart.Contains(kv.Key)).SelectMany(kv => kv.Value).Distinct().ToArray();
        bool apiHit = text.Contains(api);
        bool keyHit = keys.Length == 0 || keys.Any(k => text.Contains(k));
        if (!apiHit || !keyHit)
            yield return $"{sig}：锚点目录（{anchorDir}）未检出「{api}」+「{string.Join("/", keys)}」组合——基线标了 anchored 但没找到对应锚点（补锚点或改处置）";
    }
}

sealed class Entry
{
    public string Kind = "";
    public string Sig = "";
    public string DefAsm = "?";
    public bool ValueHint;
    public bool Gated;
    public bool Noise;
    public readonly SortedSet<string> Sources = new(StringComparer.Ordinal);
}

/// <summary>结构化签名信息：Display=去 arity 的展示名；Assembly=定义程序集（null=内置/匿名）；
/// ValueHint=值类型命中（primitive/Nullable/ValueTuple/递归实参；数组→引用=false，自定义 struct 未解析=下界）。</summary>
sealed record TypeInfo(string Display, string? Assembly, bool ValueHint);

sealed class NameProvider(MetadataReader md, string selfName) : ISignatureTypeProvider<TypeInfo, object?>
{
    public string SelfName => selfName;

    internal static string StripArity(string name)
    {
        int i = name.IndexOf('`');
        return i < 0 ? name : name[..i];
    }

    public TypeInfo GetPrimitiveType(PrimitiveTypeCode code) => new(code.ToString(), null, true);

    public TypeInfo GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
        => new(StripArity(reader.GetString(reader.GetTypeDefinition(handle).Name)), selfName, false);

    public TypeInfo GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
    {
        var tr = reader.GetTypeReference(handle);
        return new TypeInfo(StripArity(reader.GetString(tr.Name)), ResolveAssembly(handle), false);
    }

    public TypeInfo GetTypeFromSpecification(MetadataReader reader, object? context, TypeSpecificationHandle handle, byte rawTypeKind)
        => reader.GetTypeSpecification(handle).DecodeSignature(this, context);

    public TypeInfo GetSZArrayType(TypeInfo elementType) => new(elementType.Display + "[]", elementType.Assembly, false);
    public TypeInfo GetArrayType(TypeInfo elementType, ArrayShape shape)
        => new(elementType.Display + "[" + new string(',', shape.Rank - 1) + "]", elementType.Assembly, false);
    public TypeInfo GetByReferenceType(TypeInfo elementType) => new(elementType.Display + "&", elementType.Assembly, elementType.ValueHint);
    public TypeInfo GetPointerType(TypeInfo elementType) => new(elementType.Display + "*", elementType.Assembly, elementType.ValueHint);

    public TypeInfo GetGenericInstantiation(TypeInfo genericType, ImmutableArray<TypeInfo> typeArguments)
    {
        bool hint = typeArguments.Any(a => a.ValueHint)
                    || genericType.Display is "Nullable" or "ValueTuple"; // Nullable<T>/ValueTuple<…> 本身即值类型（§3.1.3）
        return new TypeInfo(genericType.Display + "<" + string.Join(",", typeArguments.Select(a => a.Display)) + ">",
                            genericType.Assembly, hint);
    }

    public TypeInfo GetGenericMethodParameter(object? context, int index) => new("!!" + index, null, false);
    public TypeInfo GetGenericTypeParameter(object? context, int index) => new("!" + index, null, false);
    public TypeInfo GetFunctionPointerType(MethodSignature<TypeInfo> signature) => new("fnptr", null, false);
    public TypeInfo GetModifiedType(TypeInfo modifier, TypeInfo unmodifiedType, bool isRequired) => unmodifiedType;
    public TypeInfo GetPinnedType(TypeInfo elementType) => elementType;

    /// <summary>TypeRef 的定义程序集：巢类沿 ResolutionScope 走到外层再取 AssemblyRef；
    /// Module/未定义 scope=本模块（热更自身）。</summary>
    public string ResolveAssembly(TypeReferenceHandle handle)
    {
        var scope = md.GetTypeReference(handle).ResolutionScope;
        while (scope.Kind == HandleKind.TypeReference)
            scope = md.GetTypeReference((TypeReferenceHandle)scope).ResolutionScope;
        if (scope.Kind == HandleKind.AssemblyReference)
            return md.GetString(md.GetAssemblyReference((AssemblyReferenceHandle)scope).Name);
        return selfName;
    }
}
