using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCore
{
    /// <summary>候选分组：归属×区域（规格 docs/目标候选区域信息_共享规格_v0.2.md §2——
    /// 2026-11-15 从 RegionTargetSelector 本地实现上移为引擎单一数据源）。
    /// Indices=候选下标（升序）；**组内保持引擎候选原序、不重排 Candidates**——
    /// 任何消费方（UI/AI/脚本/LAE/联机）回传选择都必须用原下标，MapIndices 语义不变。</summary>
    public sealed class TargetZoneGroup
    {
        public int SideRank;        // 0=己方（Chooser 视角） 1=对方
        public int ZoneRank;        // 角色0→战场1→元素池2→手牌3→墓地4→牌库5→除外6→技能栏7→发动区8→其他9
        public string Label;        // 「己方战场」等（归属字+区域字单源）
        public List<int> Indices;   // 候选下标（升序；组内保持引擎原序）
    }

    /// <summary>归属×区域分组唯一权威（2026-11-15 规格 §2）：判定口公开单源，
    /// UI/AI/脚本/LAE/联机共用，不得各写一份。规则与「目标选择窗·区域分区与隔断」
    /// v0.1 完全一致：组序 (SideRank, ZoneRank, 组内首候选下标) 升序；Player 候选=角色组
    ///（「角色格即区域格」既有口径，牌库折叠格也是 Player 不另造组）；未知实体防御性归对方末组。</summary>
    public static class TargetZoneGrouping
    {
        /// <summary>请求口径：候选+Chooser（null Chooser 按 P1 为己方——旧 UI 口径）。</summary>
        public static List<TargetZoneGroup> Build(TargetSelectionRequest req)
            => Build(req?.Candidates, req?.Chooser);

        /// <summary>按 Chooser 视角分组；不重排 candidates（Indices 指原下标）。</summary>
        public static List<TargetZoneGroup> Build(IReadOnlyList<Entity> candidates, Player chooser)
        {
            var groups = new List<TargetZoneGroup>();
            if (candidates == null) return groups;
            for (int i = 0; i < candidates.Count; i++)
            {
                var e = candidates[i];
                if (e == null) continue;
                int sideRank = SideRankOf(e, chooser);
                int zoneRank;
                string zoneWord;
                if (e is Player) { zoneRank = 0; zoneWord = "角色"; } // 角色格即区域格（含牌库折叠格）
                else if (e is Card c) { zoneRank = ZoneRankOf(c.GetZone()); zoneWord = ZoneWord(c.GetZone()); }
                else { zoneRank = 9; zoneWord = "其他"; } // 防御性兜底（规格 §4.1）：未知实体归对方末组
                string label = (sideRank == 0 ? "己方" : "对方") + zoneWord;
                var g = groups.Find(x => x.SideRank == sideRank && x.ZoneRank == zoneRank && x.Label == label);
                if (g == null)
                {
                    g = new TargetZoneGroup { SideRank = sideRank, ZoneRank = zoneRank, Label = label, Indices = new List<int>() };
                    groups.Add(g);
                }
                g.Indices.Add(i);
            }
            // 组间固定序（规格 §4.2），不按组首次出现先后；组内 Indices 已按下标升序（追加序）
            groups.Sort((a, b) => a.SideRank != b.SideRank ? a.SideRank.CompareTo(b.SideRank)
                : a.ZoneRank != b.ZoneRank ? a.ZoneRank.CompareTo(b.ZoneRank)
                : a.Indices[0].CompareTo(b.Indices[0]));
            return groups;
        }

        /// <summary>区域组序（规格 §4.2）：同归属内 角色→战场→元素池→手牌→墓地→牌库→除外→
        /// 技能栏→发动区→其他；None/Extra 等未知落「其他」末组（标签由 ZoneZh 直出）。</summary>
        public static int ZoneRankOf(Zone zone)
            => zone switch
            {
                Zone.Battlefield => 1,
                Zone.ElementPool => 2,
                Zone.Hand => 3,
                Zone.Graveyard => 4,
                Zone.Deck => 5,
                Zone.Exile => 6,
                Zone.FieldZone => 7,
                Zone.Activation => 8,
                _ => 9,
            };

        /// <summary>区域字单源：复用 MatchLogRenderer.ZoneZh（不得另写一份）；「角色」无 Zone、
        /// 由分组处直接写死（Player 候选专属）。</summary>
        public static string ZoneWord(Zone zone) => MatchLogRenderer.ZoneZh(zone);

        /// <summary>归属判侧（Chooser 视角）：Player=自身；Card=GetController()??GetOwner()；
        /// anchor（chooser）null 按 P1；未注入/无主/未知实体一律按对方（与角色候选文案同口径）。</summary>
        public static int SideRankOf(Entity e, Player chooser)
        {
            var anchor = chooser ?? GameCore.Instance?.Player1;
            if (anchor == null || e == null) return 1;
            if (e is Player p) return ReferenceEquals(p, anchor) ? 0 : 1;
            if (e is Card c)
            {
                var owner = c.GetController() ?? c.GetOwner();
                return owner != null && ReferenceEquals(owner, anchor) ? 0 : 1;
            }
            return 1;
        }
    }

    /// <summary>目标令牌串解析（2026-11-15 共享规格 v0.2 §5——放引擎侧供脚本/验证器共用）：
    /// 逗号分隔令牌混排——区域令牌 `<归属><区域>[×N]` 走 TargetZoneGrouping 单一源
    ///（候选=效果 def 的 spec 计划候选，与人类窗口同源；×N 取组内前 N，无 ×N 取全组；
    /// 组不存在=该令牌失败记日志，不静默换区）；其余令牌交 legacyResolver（卡 ID/hero 等
    /// 调用方私有口径）。null/"auto"=返回 null（引擎自动解析）。去重保序；全区域令牌失败
    /// 且无实体产出时 zoneFailed=true——调用方据此判动作失败，不得回落代选。</summary>
    public static class TargetZoneTokens
    {
        /// <summary>解析目标串；返回目标列表（null=自动解析；空表+zoneFailed=显式失败）。</summary>
        public static List<Entity> Resolve(GameCore core, Player me, string spec,
            Card sourceCard, EffectDefinition def, Func<string, Entity> legacyResolver, out bool zoneFailed)
        {
            zoneFailed = false;
            if (string.IsNullOrEmpty(spec) || spec == "auto") return null;
            var list = new List<Entity>();
            foreach (var raw in spec.Split(','))
            {
                var token = raw.Trim();
                if (TryResolveZoneToken(core, me, token, sourceCard, def, list, ref zoneFailed)) continue;
                var entity = legacyResolver != null ? legacyResolver(token) : null;
                if (entity == null) TideLog.Warn($"[目标令牌] 不可解析：{raw}");
                else if (!list.Contains(entity)) list.Add(entity);
            }
            return list.Count > 0 ? list : null;
        }

        /// <summary>区域令牌解析：`<归属><区域>[×N]`——归属∈{己方,对方}（me 视角）；区域∈{角色,
        /// 战场,元素池,手牌,墓地,牌库,除外}（与组标签同字串；前缀匹配——「除外」命中「除外区」）。
        /// 返回 true=本令牌已按区域处理（无论成败）；false=不是区域令牌（走 legacy 路径）。</summary>
        private static bool TryResolveZoneToken(GameCore core, Player me, string token,
            Card sourceCard, EffectDefinition def, List<Entity> into, ref bool zoneFailed)
        {
            int sideRank;
            if (token.StartsWith("己方", StringComparison.Ordinal)) sideRank = 0;
            else if (token.StartsWith("对方", StringComparison.Ordinal)) sideRank = 1;
            else return false;

            bool failed = false;
            string failReason = null;
            void Fail(string why) { failed = true; failReason = why; }

            var rest = token.Substring(2);
            int take = -1;
            int x = rest.LastIndexOf('×');
            if (x >= 0)
            {
                if (!int.TryParse(rest.Substring(x + 1), out take) || take <= 0)
                {
                    Fail("×N 非法");
                    zoneFailed = true;
                    TideLog.Warn($"[目标令牌] 区域令牌失败：{token}（{failReason}）");
                    return true;
                }
                rest = rest.Substring(0, x);
            }
            int zoneRank = -1;
            if (rest == "角色") zoneRank = 0;
            else
            {
                foreach (Zone z in Enum.GetValues(typeof(Zone)))
                {
                    var zh = TargetZoneGrouping.ZoneWord(z);
                    if (zh == rest || zh.StartsWith(rest, StringComparison.Ordinal))
                    {
                        zoneRank = TargetZoneGrouping.ZoneRankOf(z);
                        break;
                    }
                }
            }
            if (zoneRank < 0) Fail("区域词不在词表");
            else if (def == null) Fail("动作无效果定义（def 缺失）");
            if (failed)
            {
                zoneFailed = true;
                TideLog.Warn($"[目标令牌] 区域令牌失败：{token}（{failReason}）");
                return true;
            }

            // 候选=spec 计划（与人类窗口同源）；ctx 来源归因镜像 SimpleAI.BuildTargetCtx
            //（法术效果来源=角色、场上卡效果来源=该卡）
            var ctx = new EffectExecutionContext
            {
                Source = sourceCard != null && sourceCard.IsSpellCard() ? (Entity)me : (Entity)sourceCard,
                Controller = me,
                ZoneManager = core.ZoneManager,
                ElementPool = core.ElementPool,
                ModeIndex = 0,
            };
            var candidates = new List<Entity>();
            foreach (var specEntry in Attribute.EffectHandlerRegistry.CollectSpecPlan(def, ctx))
            {
                if (specEntry?.Candidates == null) continue;
                foreach (var e in specEntry.Candidates)
                    if (e != null && !candidates.Contains(e)) candidates.Add(e);
            }
            var groups = TargetZoneGrouping.Build(candidates, me);
            TargetZoneGroup group = null;
            foreach (var g in groups)
                if (g.SideRank == sideRank && g.ZoneRank == zoneRank) { group = g; break; }
            if (group == null)
            {
                zoneFailed = true;
                TideLog.Warn($"[目标令牌] 区域令牌失败：{token}（组不存在——候选 {candidates.Count} 个，"
                    + $"现有组：{string.Join("/", groups.Select(g0 => g0.Label))}）");
                return true;
            }
            int added = 0;
            foreach (var i in group.Indices)
            {
                if (i < 0 || i >= candidates.Count) continue;
                var e = candidates[i];
                if (e != null && !into.Contains(e))
                {
                    into.Add(e);
                    added++;
                }
                if (take > 0 && added >= take) break;
            }
            return true;
        }
    }
}
