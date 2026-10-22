using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;

namespace SynergyUI
{
    /// <summary>
    /// 创作管线走查逻辑（2026-10-06，纯逻辑不建 UI；2026-10-17 创作课取消后休眠保留）：
    /// 引导玩家走一遍创作链——新建一个效果 → 新建一张含该效果的卡 → 保存一套含该卡的卡组。
    /// 三步走完=本课完成。
    ///
    /// 检测口径：效果/卡没有创建标记字段，「新建」=以开课快照的 id 基线做差集（内容哈希 id——
    /// 内容变即 id 变，重存旧内容不误判）。三个合成界面在保存成功后调 Notify*（无参薄挂钩；
    /// 本课未开/已完成时零行为）。从卡内「+新建效果」再存卡的流程里效果落库发生在卡保存时——
    /// NotifyCardsChanged 同轮差集可同时达成步①②，两种创作路径（效果库直存/卡内新建）统一覆盖。
    /// 数据源可整体注入（验证器用内存假源，不碰真实 StreamingAssets）。
    ///
    /// 旧「最后一课镜像局」链（StartFinalChallenge/FinalChallengeActive/ReportFinalChallengeResult/
    /// ClearSession）2026-10-19 随三课制整删；ResolveDefaultMirrorDeck 保留=TestStartFlow 测试卡组
    /// 缺卡时的回落链。
    /// </summary>
    public static class TutorialCreationFlow
    {
        /// <summary>默认卡组名（测试链回落用；6 色主体卡组由用户后续制作）。</summary>
        public const string DefaultDeckName = "主体卡组";

        /// <summary>镜像回退卡组名（2026-10-15 定案）：主体卡组待制作期间回落用 红·快攻
        ///（现阶段唯一全量 30 张主题卡组，红+灰双色经济）。此前主体卡组缺失直接随机镜像=
        /// 全卡池彩虹手牌——「卡组文件已改纯、对局仍五颜六色」的根因（文件与对局卡源脱节）。</summary>
        public const string MirrorFallbackDeckName = "红·快攻";

        /// <summary>走查步推进事件（参数=新步号 1/2/3；UI 步骤高亮后续接）。</summary>
        public static event Action<int> OnStepAdvanced;

        /// <summary>本课完成事件（三步走完时触发一次；UI「最后一课就绪」提示后续接）。</summary>
        public static event Action OnLessonCompleted;

        // 数据源（验证器 UseVerificationSources 整体替换；默认接真实序列化器）
        private static Func<List<string>> _effectIdsSource = DefaultEffectIdsSource;
        private static Func<List<(string cardId, List<string> effectIds)>> _cardEntriesSource = DefaultCardEntriesSource;
        private static Func<List<DeckData>> _decksSource = DefaultDecksSource;

        /// <summary>验证口：注入内存假源（任一传 null=该源恢复默认）。编辑器验证器用，
        /// 验证完传三 null 复位。</summary>
        public static void UseVerificationSources(
            Func<List<string>> effectIds,
            Func<List<(string cardId, List<string> effectIds)>> cardEntries,
            Func<List<DeckData>> decks)
        {
            _effectIdsSource = effectIds ?? DefaultEffectIdsSource;
            _cardEntriesSource = cardEntries ?? DefaultCardEntriesSource;
            _decksSource = decks ?? DefaultDecksSource;
        }

        private static List<string> DefaultEffectIdsSource()
            => EffectLibrarySerializer.LoadAll().Select(g => g.id).ToList();

        private static List<(string cardId, List<string> effectIds)> DefaultCardEntriesSource()
            => CardConfigSerializer.LoadAll().Select(cd => (
                   cd.ID,
                   (cd.Effects ?? new List<CardEffectData>())
                       .Where(fx => fx != null)
                       .Select(ContentHasher.HashEffectOf)
                       .ToList()))
               .ToList();

        /// <summary>卡组源（2026-10-22）：DeckCatalog 合并口——玩家存档 + 只读预组（红·快攻已在
        /// TutorialData 预组里，回退链照常命中）。</summary>
        private static List<DeckData> DefaultDecksSource()
            => DeckCatalog.LoadAll();

        // ======================================== 走查 ========================================

        /// <summary>开课（TutorialFlow 对 chuangzuo 的分流口）：快照基线、占进行中标记。
        /// 已完成/进行中调用为幂等（不重拍基线——差集口径必须锚定首次开课时点）。</summary>
        public static void BeginLesson(TutorialProgressManager progress = null)
        {
            var p = progress ?? TutorialProgressManager.Instance;
            if (p.CompletedTutorials.Contains(TutorialProgressManager.CreationLessonId)) return;
            if (p.Creation == null)
            {
                p.WriteCreationState(new CreationLessonData
                {
                    step = 0,
                    baselineEffects = _effectIdsSource(),
                    baselineCards = _cardEntriesSource().Select(e => e.cardId).ToList(),
                });
            }
            p.SetCurrentTutorial(TutorialProgressManager.CreationLessonId);
        }

        /// <summary>当前走查步（-1=未开课；0-3 见 CreationLessonData.step）。</summary>
        public static int CurrentStep(TutorialProgressManager progress = null)
            => (progress ?? TutorialProgressManager.Instance).Creation?.step ?? -1;

        /// <summary>效果库保存成功后（合成界面效果库模式薄挂钩）：差集检测走查步。</summary>
        public static void NotifyEffectsChanged(TutorialProgressManager progress = null)
            => Evaluate(progress ?? TutorialProgressManager.Instance);

        /// <summary>卡表保存成功后：差集检测走查步（卡内新建效果在卡保存时落库——步①②可同轮达成）。</summary>
        public static void NotifyCardsChanged(TutorialProgressManager progress = null)
            => Evaluate(progress ?? TutorialProgressManager.Instance);

        /// <summary>卡组保存成功后：差集检测走查步③。</summary>
        public static void NotifyDecksChanged(TutorialProgressManager progress = null)
            => Evaluate(progress ?? TutorialProgressManager.Instance);

        private static void Evaluate(TutorialProgressManager p)
        {
            var c = p.Creation;
            if (c == null || c.step >= 3) return; // 未开课/已完成：零行为
            if (p.CompletedTutorials.Contains(TutorialProgressManager.CreationLessonId)) return;

            var newEffects = NewEffectsOf(c);
            bool changed = false;

            // 步①：效果库出现基线外的新效果
            if (c.step < 1 && newEffects.Count > 0)
            {
                c.step = 1;
                c.effectId = newEffects.First();
                changed = true;
                OnStepAdvanced?.Invoke(1);
            }

            // 步②：基线外的新卡，且其效果引用含新建效果（覆盖替换产生的新 id 同样算新建）
            if (c.step == 1 && newEffects.Count > 0)
            {
                var first = QualifyingCards(c, newEffects).FirstOrDefault();
                if (first.cardId != null)
                {
                    c.step = 2;
                    c.cardId = first.cardId;
                    changed = true;
                    OnStepAdvanced?.Invoke(2);
                }
            }

            // 步③：任一已存卡组含合格新卡 → 本课完成
            if (c.step == 2 && newEffects.Count > 0)
            {
                var qualifyingIds = QualifyingCards(c, newEffects).Select(e => e.cardId).ToHashSet();
                var deck = _decksSource().FirstOrDefault(d =>
                    d?.cardIds != null && d.cardIds.Any(qualifyingIds.Contains));
                if (deck != null)
                {
                    c.step = 3;
                    c.deckName = deck.name;
                    changed = true;
                    OnStepAdvanced?.Invoke(3);
                }
            }

            if (!changed) return;
            p.WriteCreationState(c);
            if (c.step >= 3)
            {
                p.MarkTutorialCompleted(TutorialProgressManager.CreationLessonId);
                OnLessonCompleted?.Invoke();
            }
        }

        private static HashSet<string> NewEffectsOf(CreationLessonData c)
            => _effectIdsSource()
                .Where(id => !string.IsNullOrEmpty(id) && !c.baselineEffects.Contains(id))
                .ToHashSet();

        private static List<(string cardId, List<string> effectIds)> QualifyingCards(
            CreationLessonData c, HashSet<string> newEffects)
            => _cardEntriesSource().Where(e =>
                   !string.IsNullOrEmpty(e.cardId)
                   && !c.baselineCards.Contains(e.cardId)
                   && e.effectIds != null && e.effectIds.Any(newEffects.Contains))
               .ToList();

        // ======================================== 默认镜像卡组（测试链回落） ========================================

        /// <summary>默认卡组镜像解析链（2026-10-15 定案，2026-10-19 起唯一消费方=TestStartFlow
        /// 测试卡组缺卡回落）：主体卡组（待制作）→ 红·快攻（镜像回退卡组）→ 随机镜像（最后兜底）。
        /// 双方同用一份卡组（保住「镜像」语义）；发牌走 InitGame 正常洗牌抽卡。</summary>
        public static List<CardData> ResolveDefaultMirrorDeck()
        {
            var decks = _decksSource();
            foreach (var name in new[] { DefaultDeckName, MirrorFallbackDeckName })
            {
                var deckData = decks.FirstOrDefault(d => d != null && d.name == name);
                if (deckData?.cardIds == null)
                {
                    TideLog.Warn($"[TutorialCreation] 镜像卡组 {name} 不存在——继续回落");
                    continue;
                }
                var cards = TutorialLibrary.BuildDeck(deckData.cardIds);
                if (cards.Count >= GameCore.OpeningHandSize) return cards;
                TideLog.Warn($"[TutorialCreation] 镜像卡组 {name} 张数不足（{cards.Count}）——继续回落");
            }
            TideLog.Warn("[TutorialCreation] 主体卡组/红·快攻 均不可用——回落随机镜像（三套预组随机其一，仅保镜像语义）");
            return BattleController.RandomDeck();
        }

        // ResolveChallengeDeck/DeckContainsQualifying（走查记名卡组解析）已随创作课取消删除（2026-10-17）
    }
}
