using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CardCore;
using GameBoard;
using Newtonsoft.Json.Linq;
using SynergyUI;

namespace TideSim
{
    /// <summary>
    /// 教学表现层配置审计（2026-11-21 战场遮罩/高亮/暂停计划 v0.3 §7 静态验收）：
    /// 四档 TutorialConfig{,2,3,4}.json 的 guide/guideSkip 逐步扫描新三块——
    /// · 字段白名单：step 与 mask/highlight/pause 子对象的多余键=非法字段报红（配置手写防拼错）；
    /// · 目标词表（§2.1·只收 3D）：hero/oppHero/board/units/zone:role|activation|right|mid|p1-land|p1-field/
    ///   cell:{x}:{z}/unit:{卡id}/tmp:{宿主token}；ui:*/card:*/hand/all 报红（UI 不参与遮罩高亮）；
    ///   cell 越界、unit 卡 id 不在卡表（#N 剥壳后查）报红；mask.target 必须单 token（逗号=多窗写法，
    ///   v0.3 定案每步一窗）；mask 块存在但 target 空=报红；
    /// · highlight：style 仅 outline（元素池行环已常驻化——LandsHeroes 对账落地，不再走教学
    ///   高亮）、color #RRGGBB、width 1..8、hdr&gt;0.8（bloom 阈值）、pulse none/slow/fast；
    /// · pause：resumeBy confirm/window/windowOrConfirm；自由步（fieldclear/luckuse/deathtoll）与
    ///   aiTurnOnConfirm=true 步 on=true 报红（锁住=死锁，计划 §5.3-1/2 强制无效）。
    /// 旧档缺三块=零报红零变化（缺字段=不启用）。
    /// 用法：dotnet run --project TideSim -- --tutorial-guide-audit
    /// </summary>
    internal static class TutorialGuideAudit
    {
        private static int _fail;

        // step 键白名单（与 SynergyUI.TutorialGuideStepSpec 字段一一对应）
        private static readonly HashSet<string> StepKeys = new HashSet<string>
        {
            "type", "attacker", "target", "guarder", "hint", "card", "land", "color",
            "reveal", "audio", "aiGuard", "aiTurnOnConfirm", "waitTurnStart", "completeOnConfirm",
            "coinToss", "dealOpening", "mask", "highlight", "pause"
        };

        private static readonly HashSet<string> MaskKeys = new HashSet<string> { "target", "pad", "radius", "feather", "strength" };
        private static readonly HashSet<string> HighlightKeys = new HashSet<string> { "target", "style", "color", "width", "hdr", "pulse" };
        private static readonly HashSet<string> PauseKeys = new HashSet<string> { "on", "resumeBy", "showHint" };

        // zone 词表（沿用 BattleRevealDirector.Solo 认识的 3D 格群 token；all/hand 非窗口目标）
        private static readonly HashSet<string> ZoneTokens = new HashSet<string>
        { "zone:role", "zone:activation", "zone:right", "zone:mid", "zone:p1-land", "zone:p1-field" };

        private static readonly HashSet<string> PulseValues = new HashSet<string> { "none", "slow", "fast" };
        private static readonly HashSet<string> ResumeByValues = new HashSet<string> { "confirm", "window", "windowOrConfirm" };
        private static readonly HashSet<string> FreeStepTypes = new HashSet<string> { "fieldclear", "luckuse", "deathtoll" };

        private static void Fail(string what)
        {
            Console.WriteLine($"    ✗ {what}");
            _fail++;
        }

        private static void Info(string what) => Console.WriteLine($"    ✓ {what}");

        public static int Run()
        {
            CardCatalog.LoadAll();
            string repo = FindRepoRoot(Directory.GetCurrentDirectory());
            if (repo == null) { Console.WriteLine("✗ 找不到仓库根（向上回溯无 .git）"); return 1; }

            string[] files =
            {
                Path.Combine(repo, "Assets", "Configs", "TutorialConfig.json"),
                Path.Combine(repo, "Assets", "Configs", "TutorialConfig2.json"),
                Path.Combine(repo, "Assets", "Configs", "TutorialConfig3.json"),
                Path.Combine(repo, "Assets", "Configs", "TutorialConfig4.json"),
            };

            Console.WriteLine("【教学表现层审计】四档 guide mask/highlight/pause 静态校验（v0.3 计划 §7）");
            foreach (var file in files)
            {
                if (!File.Exists(file)) { Fail($"缺档：{file}"); continue; }
                string name = Path.GetFileName(file);
                JToken root;
                try { root = JToken.Parse(File.ReadAllText(file)); }
                catch (Exception e) { Fail($"{name} 解析失败：{e.Message}"); continue; }

                int maskCount = 0, highlightCount = 0, pauseCount = 0;
                var tutorials = root["tutorials"] as JArray;
                if (tutorials == null) { Fail($"{name} 无 tutorials 数组"); continue; }
                foreach (var course in tutorials.OfType<JObject>())
                {
                    string courseId = course.Value<string>("id") ?? "?";
                    foreach (var key in new[] { "guide", "guideSkip" })
                    {
                        var steps = course[key] as JArray;
                        if (steps == null) continue;
                        for (int i = 0; i < steps.Count; i++)
                        {
                            var step = steps[i] as JObject;
                            if (step == null) continue;
                            string tag = $"{name}·{courseId}·{key}[{i}]（{step.Value<string>("type")}）";
                            AuditStep(tag, step, ref maskCount, ref highlightCount, ref pauseCount);
                        }
                    }
                }
                Console.WriteLine($"  {name}：mask {maskCount} 步 / highlight {highlightCount} 条 / pause {pauseCount} 步");
            }

            Console.WriteLine();
            Console.WriteLine(_fail == 0 ? "教学表现层审计：通过" : $"教学表现层审计：失败 {_fail} 项");
            return _fail == 0 ? 0 : 1;
        }

        private static void AuditStep(string tag, JObject step, ref int maskCount, ref int highlightCount, ref int pauseCount)
        {
            // 1) step 键白名单
            foreach (var key in step.Properties().Select(p => p.Name))
                if (!StepKeys.Contains(key))
                    Fail($"{tag} 非法字段：{key}（step 白名单外）");

            string type = step.Value<string>("type") ?? "";

            // 2) mask：单窗 + 词表
            var mask = step["mask"];
            if (mask != null && mask.Type != JTokenType.Null)
            {
                var m = mask as JObject;
                if (m == null) Fail($"{tag} mask 非对象");
                else
                {
                    foreach (var key in m.Properties().Select(p => p.Name))
                        if (!MaskKeys.Contains(key)) Fail($"{tag} mask 非法字段：{key}");
                    string target = m.Value<string>("target");
                    if (string.IsNullOrWhiteSpace(target))
                        Fail($"{tag} mask.target 空（有 mask 块必须给单目标）");
                    else if (target.Contains(','))
                        Fail($"{tag} mask.target 多目标写法「{target}」——v0.3 定案每步单窗");
                    else if (!TokenOk(target, out var why))
                        Fail($"{tag} mask.target「{target}」{why}");
                    RangeCheck(m, "pad", 0, 128, tag, "mask");
                    RangeCheck(m, "radius", 0, 64, tag, "mask");
                    RangeCheck(m, "feather", 0, 128, tag, "mask");
                    RangeCheck(m, "strength", 0, 1, tag, "mask");
                    maskCount++;
                }
            }

            // 3) highlight：数组 + 词表 + 样式档
            var hl = step["highlight"];
            if (hl != null && hl.Type != JTokenType.Null)
            {
                var arr = hl as JArray;
                if (arr == null) Fail($"{tag} highlight 非数组");
                else
                {
                    foreach (var entry in arr.OfType<JObject>())
                    {
                        foreach (var key in entry.Properties().Select(p => p.Name))
                            if (!HighlightKeys.Contains(key)) Fail($"{tag} highlight 非法字段：{key}");
                        string target = entry.Value<string>("target");
                        if (string.IsNullOrWhiteSpace(target))
                            Fail($"{tag} highlight.target 空");
                        else if (!TokenOk(target, out var why))
                            Fail($"{tag} highlight.target「{target}」{why}");
                        string style = entry.Value<string>("style") ?? "outline";
                        if (style != "outline") Fail($"{tag} highlight.style 仅支持 outline（现值 {style}）");
                        // color 缺省=C# 默认 #FFD75A（运行时同口径）——只在显式给出时验格式
                        string color = entry.Value<string>("color");
                        if (!string.IsNullOrEmpty(color)
                            && !System.Text.RegularExpressions.Regex.IsMatch(color, "^#[0-9A-Fa-f]{6}$"))
                            Fail($"{tag} highlight.color「{color}」非 #RRGGBB");
                        RangeCheck(entry, "width", 1, 8, tag, "highlight");
                        var hdr = entry["hdr"];
                        if (hdr != null && hdr.Type != JTokenType.Null && (float)hdr <= 0.8f)
                            Fail($"{tag} highlight.hdr ≤0.8（bloom 阈值 0.8——低了晕不开）");
                        string pulse = entry.Value<string>("pulse") ?? "slow";
                        if (!PulseValues.Contains(pulse)) Fail($"{tag} highlight.pulse「{pulse}」∉ none/slow/fast");
                        highlightCount++;
                    }
                }
            }

            // 4) pause：枚举 + 死锁规则（自由步/交回合步强制无效）
            var pause = step["pause"];
            if (pause != null && pause.Type != JTokenType.Null)
            {
                var p = pause as JObject;
                if (p == null) Fail($"{tag} pause 非对象");
                else
                {
                    foreach (var key in p.Properties().Select(x => x.Name))
                        if (!PauseKeys.Contains(key)) Fail($"{tag} pause 非法字段：{key}");
                    bool on = p.Value<bool?>("on") ?? false;
                    string resumeBy = p.Value<string>("resumeBy") ?? "windowOrConfirm";
                    if (!ResumeByValues.Contains(resumeBy))
                        Fail($"{tag} pause.resumeBy「{resumeBy}」∉ confirm/window/windowOrConfirm");
                    if (on)
                    {
                        if (FreeStepTypes.Contains(type))
                            Fail($"{tag} pause.on=true 自由步（{type}）——靠玩家操作推进，锁住=死锁（§5.3-1）");
                        if (step.Value<bool?>("aiTurnOnConfirm") == true)
                            Fail($"{tag} pause.on=true 叠 aiTurnOnConfirm——交回合动作不能停 AI 推进（§5.3-2）");
                        pauseCount++;
                    }
                }
            }
        }

        private static void RangeCheck(JObject o, string key, float min, float max, string tag, string block)
        {
            var v = o[key];
            if (v == null || v.Type == JTokenType.Null) return; // 缺省=C# 默认值，合法
            if (v.Type != JTokenType.Float && v.Type != JTokenType.Integer) { Fail($"{tag} {block}.{key} 非数值"); return; }
            float f = (float)v;
            if (f < min || f > max) Fail($"{tag} {block}.{key}={f} 越界 [{min},{max}]");
        }

        /// <summary>目标 token 词表校验（§2.1·只收 3D）。返回 false 时 why 给原因。</summary>
        private static bool TokenOk(string token, out string why)
        {
            why = null;
            if (token == "hero" || token == "oppHero" || token == "board" || token == "units") return true;
            if (token == "hud:phase") return true; // 中线 HUD 阶段格（2026-11-23：3D tile 剪影同机制）
            if (token == "hand") return true; // 2026-11-23 特例：无剪影全屏模糊+手牌层抬升（「只显手牌」）
            if (token == "all" || token.StartsWith("ui:") || token.StartsWith("card:"))
            { why = "是 UI/全局词（词表只收 3D 与 hud:phase/hand 两个特例）"; return false; }
            if (token.StartsWith("zone:"))
            {
                if (!ZoneTokens.Contains(token)) why = "未知 zone 词（词表=role/activation/right/mid/p1-land/p1-field）";
                return why == null;
            }
            if (token.StartsWith("tmp:"))
            {
                var inner = token.Substring(4);
                // tmp: 只指宿主身上的 3D TMP——宿主必须是单位/角色（cell/zone 无 TMP 可指）
                if (inner == "hero" || inner == "oppHero") return true;
                if (inner.StartsWith("unit:")) return UnitIdOk(inner.Substring(5), out why);
                why = "tmp: 宿主必须是单位/角色（如 tmp:unit:C_xxx / tmp:hero）";
                return false;
            }
            if (token.StartsWith("unit:"))
                return UnitIdOk(token.Substring(5), out why);
            if (token.StartsWith("cell:"))
            {
                var parts = token.Substring(5).Split(':');
                if (parts.Length != 2 || !int.TryParse(parts[0], out int x) || !int.TryParse(parts[1], out int z)
                    || x < 0 || x >= BoardLayout.Width || z < 0 || z >= BoardLayout.Height
                    || BoardLayout.NoTileAt(x, z))
                { why = "cell 坐标非法/越界/无格"; return false; }
                return true;
            }
            why = "未知 token";
            return false;
        }

        private static bool UnitIdOk(string id, out string why)
        {
            why = null;
            if (string.IsNullOrWhiteSpace(id)) { why = "unit: 后卡 id 空"; return false; }
            // 实例 id（模板#N）剥壳查模板（KeywordTutorialCheck 同口径）
            string baseId = id.Contains('#') ? id.Substring(0, id.IndexOf('#')) : id;
            if (CardCatalog.GetById(baseId) == null) why = $"卡 id「{id}」不在卡表";
            return why == null;
        }

        private static string FindRepoRoot(string start)
        {
            var dir = new DirectoryInfo(start);
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, ".git"))
                    || File.Exists(Path.Combine(dir.FullName, "TideSim", "TideSim.csproj")))
                    return dir.FullName;
                dir = dir.Parent;
            }
            return null;
        }
    }
}
