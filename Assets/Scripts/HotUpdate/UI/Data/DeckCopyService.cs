using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;

namespace SynergyUI
{
    /// <summary>
    /// 预组卡组复制服务（2026-10-22 预组只读配套）：把只读预组（TutorialData）深拷贝成玩家自有副本——
    /// 卡组引用的卡（含衍生物模板与技能卡）逐张深克隆进玩家主表，副本卡组另存新名后随意修改，
    /// 预组原件零触碰。
    ///
    /// id 分叉口径（内容哈希机制决定，见 ContentHasher）：
    /// - 卡 id = HashCard（含 CardName，**不含 tags/费用**）。副本内容若与原件全同则同 id——落玩家
    ///   主表会按 MergeTutorialCards「保正式表」遮蔽教学原件，之后改动会让预组断链。因此复制时
    ///   逐卡 `CardName += CopySuffix` 强制分叉出新 id（最小且不污染功能内容的唯一分叉点）。
    ///   ⚠ 后缀不可去掉：复原名字会把 id 撞回教学原件再次触发遮蔽；改成其他名字没问题。
    /// - 效果行是内容寻址共享行（2026-10-26 复制零写入定案）：**复制不重写效果、不落效果库**——
    ///   副本卡 effectIds 与原件同 id 直引（主∪教学合并视图均可解析；EffectIdsOf 见同内容条目即
    ///   跳过落盘）。玩家日后在合成器改副本效果时按内容寻址自然分叉新 id，副本与预组互不影响。
    /// - 「教学」tag 剥除（副本进玩家卡池可编辑可添加）；「衍生物」tag 保留（模板照常开局摘额外区、
    ///   不计构筑张数、不进卡池）。
    /// - 卡内跨引用 str（SummonToken/GrantMorph 的模板卡 id 等）**照抄原件不再重写**（2026-10-26
    ///   定案取代 10-22 旧案）：旧案重写 str→副本模板会让效果内容变→新效果 id→玩家 Effects.json
    ///   落分叉行（实测绿套复制即产生 4 条倒计时分叉）。代价：副本的召唤/变形仍指预组原件模板卡，
    ///   玩家改副本组内克隆模板不影响召唤物——要改召唤物请直接改副本卡效果（自然分叉）。
    /// </summary>
    public static class DeckCopyService
    {
        /// <summary>副本卡名后缀（id 分叉单点；去掉会把 id 撞回教学原件）。</summary>
        public const string CopySuffix = "·副本";

        /// <summary>
        /// 复制预组为玩家副本。成功返回新卡组名（已落盘并载入缓存就绪），失败返回 null 并给出 error。
        /// 卡组名=预组名+·副本（重名追加 2/3…序号）。
        /// </summary>
        public static string CopyPreset(string presetName, out string error)
        {
            error = null;
            if (!DeckCatalog.IsPreset(presetName))
            {
                error = $"「{presetName}」不是预组卡组";
                return null;
            }
            var deck = DeckCatalog.Load(presetName);
            if (deck == null || deck.cardIds == null || deck.cardIds.Count == 0)
            {
                error = $"预组「{presetName}」缺失或为空";
                return null;
            }

            // ---- ① 解析+去重：卡组引用的全部唯一卡（含技能卡）----
            var sourceIds = new List<string>(deck.cardIds);
            if (!string.IsNullOrEmpty(deck.skillCardId)) sourceIds.Add(deck.skillCardId);
            var uniqueIds = sourceIds.Distinct().ToList();
            var missing = uniqueIds.Where(id => CardCatalog.GetById(id) == null).ToList();
            if (missing.Count > 0)
            {
                error = $"预组有 {missing.Count} 张卡在卡表查不到（如 {missing[0]}）——先重建主题卡组";
                return null;
            }

            // ---- ② 逐张深克隆（TideJson 往返——变形/回响同款先例，不与原件共享可变引用）+分叉----
            var clones = new List<CardData>(uniqueIds.Count);
            foreach (var id in uniqueIds)
            {
                var src = CardCatalog.GetById(id);
                var clone = TideJson.FromJson<CardData>(TideJson.ToJson(src));
                if (clone == null)
                {
                    error = $"卡 {src.CardName}（{id}）深克隆失败";
                    return null;
                }
                clone.CardName += CopySuffix; // id 分叉单点
                clone.Tags.RemoveAll(t => t == "教学"); // 剥「教学」进玩家卡池（口径同 CardCatalog.IsTeachingCard）
                CardCostService.EnsureCost(clone); // 刷新推导费显示缓存（恒推导定案；费不入哈希）
                clones.Add(clone);
            }

            // ---- ③ 旧id→新id 映射（单段——2026-10-26 复制零效果定案：不再重写卡内跨引用，
            // 克隆即终版，一次哈希即终版 id；卡组引用/技能查找同用）----
            var idMap = new Dictionary<string, string>(uniqueIds.Count);
            for (int i = 0; i < uniqueIds.Count; i++)
                idMap[uniqueIds[i]] = "C_" + ContentHasher.HashCard(clones[i]);

            // ---- ④ 技能卡资格（名后缀不影响 CanBeSkillCard；失败=副本不带技能照常可用）----
            string newSkillId = null;
            if (!string.IsNullOrEmpty(deck.skillCardId) && idMap.TryGetValue(deck.skillCardId, out var mapped))
            {
                var skillClone = clones.First(c => "C_" + ContentHasher.HashCard(c) == mapped);
                if (HeroSkillSystem.CanBeSkillCard(skillClone)) newSkillId = mapped;
            }

            // ---- ⑤ 落盘：卡进玩家主表（效果随之 upsert 玩家 Effects.json）→ 卡组另存新名 ----
            foreach (var clone in clones)
                CardConfigSerializer.Save(clone);

            string newName = UniqueDeckName(presetName + CopySuffix);
            var copy = new DeckData(newName)
            {
                skillCardId = newSkillId,
            };
            copy.cardIds.AddRange(deck.cardIds.Select(id => idMap[id]));
            DeckSerializer.Save(copy);

            // ---- ⑥ 读侧缓存失效（同合成器保存尾部口径）----
            EffectsLibrary.Reload();
            CardCatalog.Invalidate();
            TutorialCreationFlow.NotifyCardsChanged();
            return newName;
        }

        /// <summary>卡组名去重：重名追加 2/3…序号（预组名/玩家档名都不撞）。</summary>
        private static string UniqueDeckName(string baseName)
        {
            if (!NameTaken(baseName)) return baseName;
            for (int i = 2; i < 100; i++)
            {
                var candidate = baseName + i;
                if (!NameTaken(candidate)) return candidate;
            }
            return baseName + System.DateTime.Now.Ticks % 1000; // 理论不可达兜底
        }

        private static bool NameTaken(string name)
            => DeckCatalog.IsPreset(name) || DeckSerializer.Load(name) != null;
    }
}
