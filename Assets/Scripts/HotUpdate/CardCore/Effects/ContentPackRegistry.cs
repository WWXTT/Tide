// SPDX-FileCopyrightText: 2026 WWXTT
// SPDX-License-Identifier: BUSL-1.1

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CardCore
{
    // ============================================================ G2 · 内容包容器（2026-11-08） ============================================================
    // docs/地基计划_虚拟原子与数值A.md §5：最小包 = pack.json / atoms.json（G3 才消费）/ effects.json（瘦格式）/
    // cards.json（卡表同格式）/ decks.json（可选）/ seal.json（行为封印）。
    //
    // seal 规则（贯穿全计划）：seal = 行为哈希（ContentHasher.SealEffect——跳名字段），只盖行为不盖文案；
    // 存 seal 不存版本号，同 id 可多 seal 并存（历史封印列表）；被引用的 seal 经 NetMatchHandshake 计入
    // AtomicRowsHash 闭包（两端不一致由现有握手拒绝）。
    //
    // 装卸语义：装包=校验通过后整目录拷入 PackDir/<包id>/；卸包=删目录 → 卡从目录消失 → 卡组校验
    // （DeckCompositionValidator）按 MissingIds **整卡断链**——绝不静默剔除单原子降级成弱卡。
    //
    // 防递归铁律：EnsureLoaded 只做裸文件解析（TideJson 原文→DTO），**不得**调用 CardCatalog/EffectsLibrary/
    // CardLoader.LoadCardsFromText（会触发两目录装载链，装载链又回调本注册表 → 环）。卡物化（AllCards）
    // 只在 CardCatalog.MergePackCards 合并位发生——该位已在 EffectsLibrary 装载链之外，闭环安全。

    /// <summary>包清单（pack.json）。</summary>
    [Serializable]
    public class ContentPackManifest
    {
        public string id;            // 包 id：^[A-Za-z0-9_-]{1,32}$——内容命名空间前缀 "@id/"
        public string name;          // 展示名（不入 seal）
        public string author;        // 作者（不入 seal）
        public string version;       // 作者自述版本（仅展示；行为一致性由 seal 承担）
        public string engineVersion; // 依赖引擎版本（预留——G2 只记录不闸）
        public string notes;         // 风味/说明（不入 seal）
    }

    /// <summary>seal.json：行为封印列表（同 id 可多 seal 并存）。</summary>
    [Serializable]
    public class ContentPackSealFile
    {
        public List<ContentPackSealEntry> items;
    }

    [Serializable]
    public class ContentPackSealEntry
    {
        public string id;            // 效果 id（@包id/…）
        public List<string> seals;   // 该 id 的历史行为封印（现装内容计算值须 ∈ 此列）
    }

    /// <summary>decks.json（可选）：随包卡组（只读，name+cardIds 最小面）。</summary>
    [Serializable]
    public class ContentPackDeckFile
    {
        public List<ContentPackDeckEntry> decks;
    }

    [Serializable]
    public class ContentPackDeckEntry
    {
        public string name;
        public List<string> cardIds;
    }

    /// <summary>atoms.json G2 桩（词条定义 G3 才消费——本阶段只记条数，schema 不设闸）。</summary>
    [Serializable]
    public class ContentPackAtomsStub
    {
        public List<ContentPackAtomStub> items;
    }

    [Serializable]
    public class ContentPackAtomStub
    {
        public string id;
    }

    /// <summary>效果表同构包装（effects.json = 瘦格式 { items }）。</summary>
    [Serializable]
    public class ContentPackEffectsWrapper
    {
        public List<EffectSlimDto> items;
    }

    /// <summary>已装包（EnsureLoaded 裸解析形态——无目录链副作用）。</summary>
    public sealed class InstalledContentPack
    {
        public ContentPackManifest Manifest;
        public string Dir;
        public string CardsText;                                  // 卡表原文（物化时经 CardLoader 全链装载）
        public List<EffectSlimDto> Effects = new List<EffectSlimDto>();
        public List<ContentPackDeckEntry> Decks = new List<ContentPackDeckEntry>();
        public int AtomCount;                                     // atoms.json 条数（G3 前仅记账）
        private List<CardData> _materialized;                     // 物化缓存（随注册表失效）

        /// <summary>物化卡（CardLoader 全链装载——含装载校验与效果解析；仅在 CardCatalog 合并位调用）。</summary>
        public List<CardData> MaterializeCards()
        {
            if (_materialized != null) return _materialized;
            _materialized = string.IsNullOrEmpty(CardsText)
                ? new List<CardData>()
                : CardLoader.LoadCardsFromText(CardsText);
            return _materialized;
        }
    }

    /// <summary>装包校验报告（Errors 空 = 通过）。</summary>
    public sealed class ContentPackInstallReport
    {
        public bool Ok => Errors == null || Errors.Count == 0;
        public string PackId;
        public List<string> Errors = new List<string>();
        public int CardCount;
        public int EffectCount;
    }

    /// <summary>内容包容器注册表：目录扫描 / 装 / 卸 / 装包校验 / seal 查询。</summary>
    public static class ContentPackRegistry
    {
        private static List<InstalledContentPack> _packs;
        private static bool _loading;

        // ======================================== 扫描（裸解析——无目录链调用） ========================================

        /// <summary>扫描已装包（惰性；PackDir 缺失=空表）。只读文件+TideJson，零目录链副作用。</summary>
        public static List<InstalledContentPack> EnsureLoaded()
        {
            if (_packs != null) return _packs;
            if (_loading) return new List<InstalledContentPack>(); // 装载中重入：返回空表（CardCatalog 先例）
            _loading = true;
            try
            {
                _packs = new List<InstalledContentPack>();
                string root = SynergyUI.CardDataPaths.PackDir;
                if (!Directory.Exists(root)) return _packs;
                foreach (var dir in Directory.GetDirectories(root).OrderBy(d => d, StringComparer.Ordinal))
                {
                    var pack = LoadPack(dir);
                    if (pack != null) _packs.Add(pack);
                }
                return _packs;
            }
            finally
            {
                _loading = false;
            }
        }

        private static InstalledContentPack LoadPack(string dir)
        {
            try
            {
                string manifestPath = Path.Combine(dir, "pack.json");
                if (!File.Exists(manifestPath))
                {
                    TideLog.Warn($"[ContentPack] 目录缺 pack.json（跳过）: {dir}");
                    return null;
                }
                var manifest = TideJson.FromJson<ContentPackManifest>(File.ReadAllText(manifestPath));
                if (manifest == null || string.IsNullOrEmpty(manifest.id))
                {
                    TideLog.Warn($"[ContentPack] pack.json 无法解析（跳过）: {dir}");
                    return null;
                }
                var pack = new InstalledContentPack { Manifest = manifest, Dir = dir };
                string cardsPath = Path.Combine(dir, "cards.json");
                pack.CardsText = File.Exists(cardsPath) ? File.ReadAllText(cardsPath) : null;
                string fxPath = Path.Combine(dir, "effects.json");
                if (File.Exists(fxPath))
                {
                    var fx = TideJson.FromJson<ContentPackEffectsWrapper>(File.ReadAllText(fxPath));
                    if (fx?.items != null) pack.Effects = fx.items.Where(e => e != null && !string.IsNullOrEmpty(e.id)).ToList();
                }
                string deckPath = Path.Combine(dir, "decks.json");
                if (File.Exists(deckPath))
                {
                    var dk = TideJson.FromJson<ContentPackDeckFile>(File.ReadAllText(deckPath));
                    if (dk?.decks != null) pack.Decks = dk.decks.Where(d => d != null).ToList();
                }
                string atomsPath = Path.Combine(dir, "atoms.json");
                if (File.Exists(atomsPath))
                {
                    var at = TideJson.FromJson<ContentPackAtomsStub>(File.ReadAllText(atomsPath));
                    pack.AtomCount = at?.items?.Count ?? 0; // G3 前仅记账
                }
                return pack;
            }
            catch (Exception e)
            {
                TideLog.Warn($"[ContentPack] 包目录装载失败（跳过）: {dir}——{e.Message}");
                return null;
            }
        }

        /// <summary>强制重扫（装卸后调用；链上两目录缓存由调用方一并失效）。</summary>
        public static void Invalidate()
        {
            _packs = null;
        }

        // ======================================== 合并位出口（CardCatalog/EffectsLibrary 调用） ========================================

        /// <summary>全部包卡物化（CardCatalog.MergePackCards 唯一消费口；此时 EffectsLibrary 装载闭环安全）。</summary>
        public static IEnumerable<CardData> AllCards()
        {
            foreach (var pack in EnsureLoaded())
                foreach (var card in pack.MaterializeCards())
                    yield return card;
        }

        /// <summary>全部包效果条目（EffectsLibrary.MergePackEffects 唯一消费口）。</summary>
        public static IEnumerable<EffectSlimDto> AllEffects()
        {
            foreach (var pack in EnsureLoaded())
                foreach (var dto in pack.Effects)
                    yield return dto;
        }

        // ======================================== seal（握手闭包出口） ========================================

        /// <summary>计算效果条目的行为封印（瘦 DTO → CardEffectData → SealEffect——跳名字段）。
        /// ToCardEffect 失败（结构坏）返回 null；原子闭包由装包校验另行把关。</summary>
        public static string ComputeEffectSeal(EffectSlimDto dto)
        {
            if (dto == null) return null;
            var fx = EffectSlim.ToCardEffect(dto);
            return fx == null ? null : SynergyUI.ContentHasher.SealEffectOf(fx);
        }

        /// <summary>按效果 id 查封印（包外 id → null）。</summary>
        public static (string packId, string seal)? SealOf(string effectId)
        {
            if (string.IsNullOrEmpty(effectId)) return null;
            foreach (var pack in EnsureLoaded())
            {
                var dto = pack.Effects.FirstOrDefault(e => e.id == effectId);
                if (dto == null) continue;
                string seal = ComputeEffectSeal(dto);
                if (seal == null) return null;
                return (pack.Manifest.id, seal);
            }
            return null;
        }

        /// <summary>被引用效果的包封印行（NetMatchHandshake.AtomicRowsHash 闭包）：格式 PK:包id|效果id|seal。
        /// 无包/引用不涉包 → 空表 → 摘要与既有逐字节一致（零漂移）。</summary>
        public static List<string> ReferencedSealLines(IEnumerable<string> effectIds)
        {
            var lines = new List<string>();
            if (effectIds == null) return lines;
            foreach (var eid in effectIds)
            {
                var s = SealOf(eid);
                if (s != null) lines.Add($"PK:{s.Value.packId}|{eid}|{s.Value.seal}");
            }
            lines.Sort(StringComparer.Ordinal);
            return lines;
        }

        // ======================================== 装包 / 卸包 ========================================

        /// <summary>装包：装包校验（§5 验收口径）全过后整目录拷入 PackDir/&lt;包id&gt;/ 并失效三处缓存。
        /// 校验失败不落目录（Errors 点名）。同 id 重装=先删旧目录（更新语义）。</summary>
        public static ContentPackInstallReport InstallFrom(string sourceDir)
        {
            var report = new ContentPackInstallReport();
            try
            {
                Validate(sourceDir, report);
                if (!report.Ok) return report;

                string dest = Path.Combine(SynergyUI.CardDataPaths.PackDir, report.PackId);
                if (Directory.Exists(dest)) Directory.Delete(dest, true); // 重装=更新
                Directory.CreateDirectory(dest);
                foreach (var file in Directory.GetFiles(sourceDir, "*.json"))
                    File.Copy(file, Path.Combine(dest, Path.GetFileName(file)));
                InvalidateCaches();
                TideLog.Info($"[ContentPack] 装包成功：{report.PackId}（卡 {report.CardCount} · 效果 {report.EffectCount}）");
                return report;
            }
            catch (Exception e)
            {
                report.Errors.Add($"装包执行异常：{e.Message}");
                return report;
            }
        }

        /// <summary>卸包：删目录 → 卡组面 MissingIds 整卡断链（DeckCompositionValidator 现成通道）。</summary>
        public static bool Uninstall(string packId, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(packId)) { error = "包 id 为空"; return false; }
            string dir = Path.Combine(SynergyUI.CardDataPaths.PackDir, packId);
            if (!Directory.Exists(dir)) { error = $"未安装: {packId}"; return false; }
            try
            {
                Directory.Delete(dir, true);
                InvalidateCaches();
                TideLog.Info($"[ContentPack] 卸包成功：{packId}（引用卡组将整卡断链）");
                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                return false;
            }
        }

        private static void InvalidateCaches()
        {
            Invalidate();
            SynergyUI.CardCatalog.Invalidate();
            EffectsLibrary.Reload();
        }

        // ======================================== 装包校验（六查——全过才落目录） ========================================

        /// <summary>原子引用单点检查：行在表 + 分支奖励效果引用可解析（装包校验 ⑤ 用）。</summary>
        private static void CheckAtomRef(AtomicEffectEntry atom, string source,
            ContentPackInstallReport report, HashSet<string> packFxIds)
        {
            if (atom == null || string.IsNullOrEmpty(atom.refId)) return;
            if (Attribute.AtomicEffectTable.GetByHashId(atom.refId) == null)
                report.Errors.Add($"效果[{source}] 原子行缺表: {atom.refId}——整卡断链，拒装（不得静默剔除单原子）");
            if (!string.IsNullOrEmpty(atom.branch?.effectId))
            {
                var bid = atom.branch.effectId;
                if (EffectsLibrary.Get(bid) == null && !packFxIds.Contains(bid))
                    report.Errors.Add($"效果[{source}] 分支奖励效果引用缺行: {bid}");
            }
        }

        /// <summary>瘦 DTO 的**裸**原子引用树（不经 ToCardEffect——ToEntry 会静默剔除缺行原子，
        /// 闭包检查用它等于自我豁免）：主干 atom / 分支 then+els 奖励 / 抉择分支递归。</summary>
        private static IEnumerable<AtomicEffectEntry> EnumerateDtoAtomRefs(EffectSlimDto dto)
        {
            if (dto?.steps == null) yield break;
            foreach (var a in WalkStepRefs(dto.steps)) yield return a;
        }

        private static IEnumerable<AtomicEffectEntry> WalkStepRefs(List<StepRef> steps)
        {
            if (steps == null) yield break;
            foreach (var s in steps)
            {
                if (s == null) continue;
                if (s.atom != null)
                {
                    yield return s.atom;
                    if (s.atom.branch?.then != null)
                        foreach (var t in s.atom.branch.then)
                        {
                            if (t == null) continue;
                            yield return t;
                            if (t.branch?.then != null)
                                foreach (var tt in t.branch.then)
                                    if (tt != null) yield return tt;
                        }
                }
                if (s.then != null) foreach (var t in s.then) yield return t;
                if (s.els != null) foreach (var t in s.els) yield return t;
                if (s.choices != null)
                    foreach (var c in s.choices)
                    {
                        if (c?.steps == null) continue;
                        foreach (var a in WalkStepRefs(c.steps)) yield return a;
                    }
            }
        }

        private static void Validate(string sourceDir, ContentPackInstallReport report)
        {
            // ① pack.json：可解析 + id 合法（命名空间前缀）
            string manifestPath = Path.Combine(sourceDir, "pack.json");
            if (!File.Exists(manifestPath)) { report.Errors.Add("缺 pack.json"); return; }
            var manifest = TideJson.FromJson<ContentPackManifest>(File.ReadAllText(manifestPath));
            if (manifest == null || string.IsNullOrEmpty(manifest.id)) { report.Errors.Add("pack.json 无法解析或缺 id"); return; }
            if (manifest.id.Length > 32 || manifest.id.Any(c =>
                    !(c >= 'a' && c <= 'z') && !(c >= 'A' && c <= 'Z') && !(c >= '0' && c <= '9')
                    && c != '-' && c != '_'))
            { report.Errors.Add($"包 id 非法（限 1~32 位字母/数字/-/_）: {manifest.id}"); return; }
            report.PackId = manifest.id;
            string prefix = "@" + manifest.id + "/";

            // ② 效果表（瘦格式，可缺=纯卡包）
            var packFx = new List<EffectSlimDto>();
            string fxPath = Path.Combine(sourceDir, "effects.json");
            if (File.Exists(fxPath))
            {
                var fx = TideJson.FromJson<ContentPackEffectsWrapper>(File.ReadAllText(fxPath));
                if (fx?.items != null) packFx = fx.items.Where(e => e != null && !string.IsNullOrEmpty(e.id)).ToList();
            }
            report.EffectCount = packFx.Count;

            // ③ 卡表：裸解析（不走 LoadCardsFromText——此处可能处于目录装载链外但保持零副作用口径），
            //    逐卡查命名空间/重复/与内置池撞号
            var rawCards = new List<CardConfigEntry>();
            string cardsPath = Path.Combine(sourceDir, "cards.json");
            if (File.Exists(cardsPath))
            {
                var wrapper = TideJson.FromJson<TestCardsConfigWrapper>(File.ReadAllText(cardsPath));
                if (wrapper?.cards != null) rawCards = wrapper.cards.Where(c => c != null).ToList();
            }
            report.CardCount = rawCards.Count;
            var seenCardIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in rawCards)
            {
                if (string.IsNullOrEmpty(entry.id) || !entry.id.StartsWith(prefix, StringComparison.Ordinal))
                { report.Errors.Add($"卡 id 不在包命名空间（须 {prefix}…）: {entry.id ?? "∅"}"); continue; }
                if (!seenCardIds.Add(entry.id)) report.Errors.Add($"卡 id 包内重复: {entry.id}");
                if (SynergyUI.CardCatalog.GetById(entry.id) != null)
                    report.Errors.Add($"卡 id 与已装内容撞号（保内置）: {entry.id}");
            }

            // ④ 效果闭包：卡引用的效果（effectIds/内嵌）必须 内置∪包 可解析；包效果不得与内置撞号
            var packFxIds = new HashSet<string>(packFx.Select(e => e.id), StringComparer.Ordinal);
            foreach (var dto in packFx)
                if (EffectsLibrary.Get(dto.id) != null)
                    report.Errors.Add($"包效果 id 与内置效果库撞号: {dto.id}");
            var fxToCheck = new List<(string source, EffectSlimDto dto, CardEffectData inlineFx)>();
            foreach (var entry in rawCards)
            {
                if (!string.IsNullOrEmpty(entry.id) && report.Errors.Any(e => e.Contains(entry.id))) continue; // 撞号卡不重复查
                if (entry.effectIds != null)
                    foreach (var eid in entry.effectIds)
                    {
                        if (string.IsNullOrEmpty(eid)) continue;
                        var builtin = EffectsLibrary.Get(eid);
                        if (builtin != null) { fxToCheck.Add(("内置:" + eid, builtin, null)); continue; }
                        var own = packFx.FirstOrDefault(d => d.id == eid);
                        if (own != null) { fxToCheck.Add(("包:" + eid, own, null)); continue; }
                        report.Errors.Add($"卡 {entry.id} 引用效果缺行: {eid}（内置与包内均无——整卡断链口径）");
                    }
                if (entry.effects != null)
                    foreach (var inline in entry.effects)
                        if (inline != null) fxToCheck.Add(("内嵌@" + entry.id, null, inline));
            }

            // ⑤ 原子闭包：全部效果的原子 refId（含 branch 载荷/then 奖励/抉择分支）必须在原子表——
            //    任一缺行 = 整卡断链拒装（绝不静默剔除单原子装成弱卡）。
            //    铁律：DTO 来源走**裸 StepRef 树**（EffectSlim.ToCardEffect 会经 ToEntry 静默剔除
            //    缺行原子——用它的产物查闭包等于自我豁免）；内嵌 CardEffectData 直查（无转换损耗）。
            foreach (var (source, dto, inlineFx) in fxToCheck)
            {
                if (dto != null)
                {
                    foreach (var atom in EnumerateDtoAtomRefs(dto))
                        CheckAtomRef(atom, source, report, packFxIds);
                }
                else if (inlineFx != null)
                {
                    foreach (var atom in CardLoader.EnumerateAtomEntries(inlineFx))
                        CheckAtomRef(atom, source, report, packFxIds);
                }
            }

            // ⑥ seal 对账：包效果逐一封印，计算值须 ∈ seal.json 声明列（改 body→seal 变→拒；
            //    改文案→seal 不变→照过）。有效果而无 seal.json = 拒（封印必须随包声明）。
            if (packFx.Count > 0)
            {
                string sealPath = Path.Combine(sourceDir, "seal.json");
                if (!File.Exists(sealPath))
                {
                    report.Errors.Add("包携带效果但缺 seal.json（行为封印必须随包声明）");
                }
                else
                {
                    var sealFile = TideJson.FromJson<ContentPackSealFile>(File.ReadAllText(sealPath));
                    var byId = new Dictionary<string, List<string>>(StringComparer.Ordinal);
                    if (sealFile?.items != null)
                        foreach (var s in sealFile.items)
                            if (s != null && !string.IsNullOrEmpty(s.id)) byId[s.id] = s.seals ?? new List<string>();
                    foreach (var dto in packFx)
                    {
                        string seal = ComputeEffectSeal(dto);
                        if (seal == null) { report.Errors.Add($"效果 {dto.id} 结构无法还原（seal 计算失败）"); continue; }
                        if (!byId.TryGetValue(dto.id, out var declared) || declared.Count == 0)
                            report.Errors.Add($"效果 {dto.id} 在 seal.json 未声明封印");
                        else if (!declared.Contains(seal))
                            report.Errors.Add($"效果 {dto.id} 封印不符：现算 {seal}，声明 [{string.Join(",", declared)}]——body 与封印不一致，拒装");
                    }
                }
            }

            // decks.json（可选）：卡引用须 包∪内置 可解析
            string deckPath = Path.Combine(sourceDir, "decks.json");
            if (File.Exists(deckPath))
            {
                var dk = TideJson.FromJson<ContentPackDeckFile>(File.ReadAllText(deckPath));
                if (dk?.decks != null)
                    foreach (var d in dk.decks)
                    {
                        if (d == null) continue;
                        foreach (var cid in d.cardIds ?? new List<string>())
                            if (!seenCardIds.Contains(cid) && SynergyUI.CardCatalog.GetById(cid) == null)
                                report.Errors.Add($"随包卡组「{d.name}」引用缺卡: {cid}");
                    }
            }
        }
    }
}
