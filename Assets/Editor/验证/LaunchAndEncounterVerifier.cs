// SPDX-FileCopyrightText: 2026 WWXTT
// SPDX-License-Identifier: BUSL-1.1

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CardCore;
using CardCore.Network;
using SynergyUI;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 启动页迁移与对局记录验证器（2026-11-09 协议 v11 定案）：数据层证据链——
/// A. EncounterStore（落盘/回读/封顶/matchId 时序）
/// B. 云存档口径（RootFiles 去 ElementUnlock + TrimToSize 剥最旧战报）
/// C. GameDataReload（进度单例重建）
/// D. 瞬态覆盖层（装/查/玩家池隔离/清）
/// E. ValidateDeckSubmit 覆盖层用例（对端缺卡不再恒拒——先拒后装后过）
/// F. NetDeckPayload（真实卡组闭包载荷构建）
/// G. OpponentDeckImporter（合成记录导入全链 + 清理）
/// 纪律：不走截图；每段断言外部可观察状态（文件/查询口/校验结果）。
/// </summary>
public static class LaunchAndEncounterVerifier
{
    private static int _pass;
    private static int _fail;
    private static readonly List<string> _fails = new List<string>();

    [MenuItem("Tools/验证/启动页与对局记录")]
    public static void Run()
    {
        _pass = 0; _fail = 0; _fails.Clear();
        try
        {
            VerifyEncounterStore();
            VerifyCloudScope();
            VerifyGameDataReload();
            VerifyTransientOverlay();
            VerifyDeckSubmitWithOverlay();
            VerifyPayloadBuilder();
            VerifyImporter();
        }
        catch (Exception ex)
        {
            _fail++;
            _fails.Add($"FAIL: 验证器异常中断：{ex}");
            Debug.LogError($"[启动页与对局记录] FAIL: 验证器异常中断：{ex}");
        }
        finally
        {
            TransientNetContent.Clear();
        }
        Debug.Log($"[启动页与对局记录] PASS={_pass} FAIL={_fail}"
                  + (_fail == 0 ? "" : "\n" + string.Join("\n", _fails)));
        if (_fail > 0) Debug.LogError($"[启动页与对局记录] {_fail} 项失败——详见上方清单");
    }

    private static void Check(bool ok, string what)
    {
        if (ok) { _pass++; return; }
        _fail++;
        _fails.Add($"FAIL: {what}");
        Debug.LogError($"[启动页与对局记录] FAIL: {what}");
    }

    // ============================================================ A. EncounterStore ============================================================

    private static void VerifyEncounterStore()
    {
        var written = new List<string>();
        try
        {
            // matchId：等长定长 + 唯一 + 字典序=时间序
            var ids = new HashSet<string>();
            for (int i = 0; i < 50; i++) ids.Add(EncounterStore.NewMatchId());
            Check(ids.Count == 50, "NewMatchId 50 次唯一");
            Check(ids.All(id => id.Length == ids.First().Length && id.StartsWith("m")),
                "NewMatchId 等长定长（字典序=时间序，云打包裁旧依赖）");

            // 落盘/回读 round-trip
            var rec = new EncounterRecord
            {
                matchId = EncounterStore.NewMatchId(),
                finishedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                opponentName = "验证对手",
                opponentOpenId = "op-verify",
                win = true,
                ownDeckName = "我方",
                opponentDeckName = "对手卡组",
                opponentCardIds = new[] { "C_A", "C_A", "C_B" },
                opponentSkillCardId = null,
                opponentTokenCardIds = Array.Empty<string>(),
                opponentTokenBindings = Array.Empty<MsgDeckTokenBinding>(),
                cards = new[] { new MsgCardPayload { CardId = "C_A", CardJson = "{\"id\":\"C_A\"}" } },
                effects = new[] { new MsgEffectPayload { EffectId = "E1", EffectJson = "{\"id\":\"E1\"}" } },
                atomRows = Array.Empty<MsgAtomRowPayload>(),
                log = "# 对局战报\n- 验证行",
            };
            written.Add(rec.matchId);
            EncounterStore.Append(rec);
            var loaded = EncounterStore.Load(rec.matchId);
            Check(loaded != null && loaded.opponentName == "验证对手" && loaded.win && loaded.cards?.Length == 1
                  && loaded.log == "# 对局战报\n- 验证行", "EncounterStore 落盘/回读 round-trip");

            // LoadAll：最新在前（时间序倒序）
            var rec2 = new EncounterRecord { matchId = EncounterStore.NewMatchId(), finishedAtUnix = 1, win = false, log = null };
            written.Add(rec2.matchId);
            EncounterStore.Append(rec2);
            var all = EncounterStore.LoadAll();
            Check(all.Count >= 2 && all[0].matchId == rec2.matchId, "LoadAll 最新在前（matchId 倒序）");

            // 封顶：灌 KeepRecords+5 条 → 文件数 ≤ KeepRecords
            var before = CountEncounterFiles();
            for (int i = 0; i < EncounterStore.KeepRecords + 5; i++)
            {
                var r = new EncounterRecord { matchId = EncounterStore.NewMatchId(), finishedAtUnix = 2, win = true };
                written.Add(r.matchId);
                EncounterStore.Append(r);
            }
            int after = CountEncounterFiles();
            Check(after <= EncounterStore.KeepRecords && after >= before,
                $"封顶裁旧（灌 {EncounterStore.KeepRecords + 5} 条后文件 {after} ≤ {EncounterStore.KeepRecords}）");
        }
        finally
        {
            foreach (string id in written)
            {
                string path = Path.Combine(EncounterStore.Dir, id + ".json");
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }

    private static int CountEncounterFiles()
        => Directory.Exists(EncounterStore.Dir) ? Directory.GetFiles(EncounterStore.Dir, "*.json").Length : 0;

    // ============================================================ B. 云存档口径 ============================================================

    private static void VerifyCloudScope()
    {
        var codec = typeof(CloudSave.SaveBundleCodec);
        var rootFiles = (string[])codec.GetField("RootFiles", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
        Check(rootFiles != null && rootFiles.Length == 2
              && rootFiles.Contains("TutorialProgress.json") && rootFiles.Contains("ChallengeProgress.json")
              && !rootFiles.Contains("ElementUnlock.json"),
            "云存档根文件=教学+挑战（ElementUnlock 已出云）");

        // TrimToSize：小上限驱动——两条记录（老的大战报/新的小战报），只应剥最旧的
        var trim = codec.GetMethod("TrimToSize", BindingFlags.NonPublic | BindingFlags.Static);
        Check(trim != null, "TrimToSize 反射可达");
        if (trim != null)
        {
            string bigLog = new string('战', 4000);
            var files = new Dictionary<string, string>
            {
                { "TutorialProgress.json", "{}" },
                { "Encounters/m0000000000001AAAA.json",
                  TideJson.ToJson(new EncounterRecord { matchId = "m0000000000001AAAA", finishedAtUnix = 1, log = bigLog }) },
                { "Encounters/m0000000000002BBBB.json",
                  TideJson.ToJson(new EncounterRecord { matchId = "m0000000000002BBBB", finishedAtUnix = 2, log = "短" }) },
            };
            bool ok = (bool)trim.Invoke(null, new object[] { files, 8000 });
            Check(ok, "TrimToSize 剥后收进上限");
            var oldRec = TideJson.FromJson<EncounterRecord>(files["Encounters/m0000000000001AAAA.json"]);
            var newRec = TideJson.FromJson<EncounterRecord>(files["Encounters/m0000000000002BBBB.json"]);
            Check(string.IsNullOrEmpty(oldRec.log), "TrimToSize 只剥最旧战报（老记录 log 已剥）");
            Check(!string.IsNullOrEmpty(newRec.log), "TrimToSize 新记录战报保留");
        }
    }

    // ============================================================ C. GameDataReload ============================================================

    private static void VerifyGameDataReload()
    {
        var before = TutorialProgressManager.Instance;
        GameDataReload.ReloadAllCaches();
        var after = TutorialProgressManager.Instance;
        Check(!ReferenceEquals(before, after), "GameDataReload 重建教学进度单例（云恢复读新数据）");
        _ = ChallengeProgressManager.Instance.ToString(); // 触发重建无异常
        _ = ElementUnlockManager.Instance.ToString();
        _ = CardCatalog.LoadAll();
        _ = EffectsLibrary.GetAll();
        _pass++;
    }

    // ============================================================ D/E. 瞬态覆盖层 + 握手校验 ============================================================

    private const string FakeCardId = "C_V11_OVERLAY_TEST";
    private const string FakeEffectSuffix = "V11OVERLAYTEST";

    private static MsgCardPayload BuildFakeCardPayload()
    {
        // 编辑器主表是空壳（卡池三分家）——从教学/训练副表取模板，克隆换号并剥隔离 tag
        //（保留 effectIds 指向本地已有效果；剥 tag 保证玩家池隔离断言只考覆盖层本身）
        foreach (var card in CardCatalog.LoadAll())
        {
            if (card == null || string.IsNullOrEmpty(card.ID) || card.Effects == null || card.Effects.Count == 0) continue;
            if (ComposerCatalog.DeckUniqueSlots(card).Count > 0) continue; // 唯一门/命运一抽卡不进 1 张卡组用例
            var entry = CardConfigSerializer.EntryOf(card);
            entry.id = FakeCardId;
            entry.cardName = "覆盖层验证卡";
            entry.tags?.RemoveAll(t => t == "教学" || t == "训练池" || t == "衍生物");
            return new MsgCardPayload { CardId = FakeCardId, CardJson = TideJson.ToJson(entry) };
        }
        return null;
    }

    private static void VerifyTransientOverlay()
    {
        TransientNetContent.Clear();
        Check(CardCatalog.GetById(FakeCardId) == null, "覆盖层前置：测试卡不在本地表");

        var payload = BuildFakeCardPayload();
        Check(payload != null, "找到覆盖层测试模板卡（带效果且无唯一槽）");
        if (payload == null) return;

        // 同卡效果载荷（id 加后缀成「本地缺行」效果，验证效果覆盖层）——模板同样放宽到副表
        string templateEffectId = null;
        var tplCard = CardCatalog.LoadAll().FirstOrDefault(c => c != null && c.Effects != null && c.Effects.Count > 0);
        foreach (var fx in tplCard?.Effects ?? new List<CardEffectData>())
            if (!string.IsNullOrEmpty(fx.Id) && EffectsLibrary.Get(fx.Id) != null) { templateEffectId = fx.Id; break; }
        MsgEffectPayload effectPayload = null;
        if (templateEffectId != null)
        {
            var dtoJson = TideJson.ToJson(EffectsLibrary.Get(templateEffectId));
            dtoJson = dtoJson.Replace($"\"id\":\"{templateEffectId}\"", $"\"id\":\"{templateEffectId}{FakeEffectSuffix}\"");
            effectPayload = new MsgEffectPayload { EffectId = templateEffectId + FakeEffectSuffix, EffectJson = dtoJson };
        }

        bool installed = TransientNetContent.Install(
            new[] { payload }, effectPayload != null ? new[] { effectPayload } : null, null);
        Check(installed, "Install 报告新增内容");
        Check(CardCatalog.GetById(FakeCardId) != null, "覆盖层卡经 GetById 可解析（校验/渲染口）");
        Check(!CardCatalog.LoadPlayPool().Any(c => c.ID == FakeCardId), "覆盖层卡不进玩家卡池（默认不显示）");
        if (effectPayload != null)
            Check(EffectsLibrary.Get(templateEffectId + FakeEffectSuffix) != null, "覆盖层效果经 EffectsLibrary.Get 可解析");
        Check(!TransientNetContent.IsTransientCard("不存在的卡"), "IsTransientCard 对无关卡 false");

        TransientNetContent.Clear();
        Check(CardCatalog.GetById(FakeCardId) == null, "Clear 后覆盖层卡出索引");
        if (effectPayload != null)
            Check(EffectsLibrary.Get(templateEffectId + FakeEffectSuffix) == null, "Clear 后覆盖层效果出解析面");
        Check(!CardCatalog.LoadPlayPool().Any(c => c.ID == FakeCardId), "Clear 后玩家池无残留");
    }

    private static void VerifyDeckSubmitWithOverlay()
    {
        TransientNetContent.Clear();
        var payload = BuildFakeCardPayload();
        if (payload == null) { Check(false, "覆盖层用例：无模板卡"); return; }

        var submit = new MsgDeckSubmit
        {
            DeckName = "覆盖层验证",
            CardIds = new[] { FakeCardId },
            Digest = null, // 下面分两轮填
            TokenCardIds = Array.Empty<string>(),
            TokenBindings = Array.Empty<MsgDeckTokenBinding>(),
            AddedCards = new[] { payload },
        };

        // ① 无载荷（旧行为）：本地缺卡 → 拒
        submit.Digest = new NetDeckDigest { AtomicRowsHash = "", AtomicRowCount = 0, TokenSig = "", ContentSig = "x" };
        bool rejected = !NetMatchHandshake.ValidateDeckSubmit(submit, out var reason1);
        Check(rejected && (reason1?.Contains("卡表缺失") == true), $"无载荷缺卡被拒（{reason1?.Split('；').FirstOrDefault()}）");

        // ② 装载荷 → 校验（按「本地表+覆盖层」）→ 过；摘要两端（同内容覆盖层）一致
        TransientNetContent.Install(submit.AddedCards, null, null);
        submit.Digest = NetMatchHandshake.ComputeDeckDigest(new[] { FakeCardId });
        bool accepted = NetMatchHandshake.ValidateDeckSubmit(submit, out var reason2);
        Check(accepted, $"载荷装覆盖层后校验通过（{(accepted ? "" : reason2)}）");

        TransientNetContent.Clear();
    }

    // ============================================================ F. 载荷构建 ============================================================

    private static void VerifyPayloadBuilder()
    {
        // 任意真实卡组（玩家档优先，回落预组）：闭包载荷非空且卡数≤闭包引用数
        var deck = DeckSerializer.LoadAll().FirstOrDefault(d => d?.cardIds != null && d.cardIds.Count > 0)
            ?? DeckCatalog.LoadPresets().FirstOrDefault(d => d?.cardIds != null && d.cardIds.Count > 0);
        Check(deck != null, "找到载荷构建测试卡组");
        if (deck == null) return;
        NetDeckPayload.BuildForDeck(deck.cardIds, deck.tokenCardIds, deck.skillCardId,
            out var cards, out var effects, out var atomRows);
        Check(cards != null && cards.Length > 0 && cards.Length <= deck.cardIds.Count + 1,
            $"闭包卡载荷 {cards?.Length ?? 0} 张（0 < ≤ 引用数 {deck.cardIds.Count + 1}）");
        Check(effects != null && effects.Length >= 0, "闭包效果载荷构建无异常");
        Check(atomRows != null, "闭包词条载荷构建无异常");
        // 载荷行可被 CardLoader 物化（接收端解析链）
        var first = cards.FirstOrDefault(c => !string.IsNullOrEmpty(c?.CardJson));
        if (first != null)
        {
            var materialized = CardLoader.LoadCardsFromText("{\"cards\":[" + first.CardJson + "]}");
            Check(materialized != null && materialized.Count == 1 && materialized[0].ID == first.CardId,
                $"载荷行物化 round-trip（{first.CardId}）");
        }
    }

    // ============================================================ G. 导入器 ============================================================

    private const string ImportCardId = "C_V11_IMPORT_TEST";

    private static void VerifyImporter()
    {
        TransientNetContent.Clear();
        var template = BuildImportTemplate();
        if (template.Item2 == null) { Check(false, "导入用例：无模板卡"); return; }

        var entry = template.Item2; // CardConfigEntry（id 已换 ImportCardId）
        var record = new EncounterRecord
        {
            matchId = EncounterStore.NewMatchId(),
            finishedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            opponentName = "导入验证对手",
            opponentOpenId = "",
            win = false,
            ownDeckName = "我方",
            opponentDeckName = "导入验证卡组",
            opponentCardIds = new[] { ImportCardId, ImportCardId },
            opponentSkillCardId = null,
            opponentTokenCardIds = Array.Empty<string>(),
            opponentTokenBindings = Array.Empty<MsgDeckTokenBinding>(),
            cards = new[] { new MsgCardPayload { CardId = ImportCardId, CardJson = TideJson.ToJson(entry) } },
            effects = Array.Empty<MsgEffectPayload>(), // 效果本地已有（克隆模板的引用）——按跳过路径走
            atomRows = Array.Empty<MsgAtomRowPayload>(),
            log = null,
        };

        string error = OpponentDeckImporter.Import(record, out var summary);
        Check(error == null, $"导入器全链成功（{error ?? summary}）");
        if (error == null)
        {
            Check(CardCatalog.GetById(ImportCardId) != null, "导入后卡入 CardCatalog（重载生效）");
            string deckName = summary != null && summary.Contains("「") ? Between(summary, "「", "」") : null;
            Check(deckName != null && DeckSerializer.Load(deckName) != null, $"导入卡组落盘可读（{deckName}）");
            // 清理（回到验证前状态）
            if (deckName != null) DeckSerializer.Delete(deckName);
            CardConfigSerializer.Remove(ImportCardId);
            GameDataReload.ReloadAllCaches();
            Check(CardCatalog.GetById(ImportCardId) == null, "清理后测试卡出表");
        }
        TransientNetContent.Clear();
    }

    private static (CardData, CardConfigEntry) BuildImportTemplate()
    {
        foreach (var card in CardCatalog.LoadAll())
        {
            if (card == null || string.IsNullOrEmpty(card.ID) || card.Effects == null || card.Effects.Count == 0) continue;
            if (ComposerCatalog.DeckUniqueSlots(card).Count > 0) continue;
            var entry = CardConfigSerializer.EntryOf(card);
            entry.id = ImportCardId;
            entry.cardName = "导入验证卡";
            entry.tags?.RemoveAll(t => t == "教学" || t == "训练池" || t == "衍生物");
            return (card, entry);
        }
        return (null, null);
    }

    private static string Between(string s, string a, string b)
    {
        int i = s.IndexOf(a, StringComparison.Ordinal);
        if (i < 0) return null;
        i += a.Length;
        int j = s.IndexOf(b, i, StringComparison.Ordinal);
        return j < 0 ? null : s.Substring(i, j - i);
    }
}
