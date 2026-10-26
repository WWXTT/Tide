using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.Attribute;
using GameBoard;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SynergyUI
{
    /// <summary>
    /// 战场悬停文案族（2026-10-18 BattleScreen 功能拆分第三块外迁）：hover-info 面板 + 全部
    /// 悬停/格位文案构建（四段卡信息/格前三键解说/特殊格/角色格/地牌格/HUD 功能格）。
    /// 纯 C# 类：输入=_view 快照+引擎侧判（Func 注入·CombatChoreographer 先例），输出=文本——
    /// 不触引擎。源键缓存（hit+btn+视图版本）防逐帧重建；ViewVersion 由 RefreshField 递增
    ///（投影侧所有权），经 Func 拉取。TryParseTile/ZoneCountOf/ExposedDeckCardsOf 为跨簇
    /// 单源（屏内留转发桩）。
    /// </summary>
    internal sealed class BattleHoverInfoProvider
    {
        private readonly Func<BattleViewData> _view;
        private readonly Func<GameCore> _core;
        private readonly Func<Player> _p1;
        private readonly Func<Player> _p2;
        private readonly Func<int> _viewVersion;
        private readonly Func<BattleCardView, CardData> _dataOf;

        private RectTransform _hoverInfo;          // 悬停信息位（场上卡四段信息与区域/功能格说明共用）
        private TMP_Text _hoverInfoLabel;
        private string _hoverKey;                  // 当前悬停内容键（防逐帧重绘）
        private string _hoverSrcKey;               // 悬停源键（hit+btn+视图版本，2026-10-18 性能）：未变跳过整段文本构建

        private BattleViewData View => _view();
        private GameCore Core => _core();
        private Player P1 => _p1();
        private Player P2 => _p2();
        private int ViewVersion => _viewVersion();

        public BattleHoverInfoProvider(Func<BattleViewData> view, Func<GameCore> core,
            Func<Player> p1, Func<Player> p2, Func<int> viewVersion, Func<BattleCardView, CardData> dataOf)
        {
            _view = view;
            _core = core;
            _p1 = p1;
            _p2 = p2;
            _viewVersion = viewVersion;
            _dataOf = dataOf;
        }

        /// <summary>控件绑定（Build 期）：hover-info 节点（富文本/初始隐藏；缺节点=悬停文本静默不显）。</summary>
        public void Bind(RectTransform panel)
        {
            _hoverInfo = panel;
            if (_hoverInfo == null) return;
            _hoverInfoLabel = _hoverInfo.GetComponentInChildren<TMP_Text>(true);
            if (_hoverInfoLabel != null) _hoverInfoLabel.richText = true;
            _hoverInfo.gameObject.SetActive(false);
        }

        /// <summary>临时元素产量双格悬停（2026-10-16 全类型累计钳）：周期口径+双方读数。</summary>
        private string TempGainTileInfo(bool self)
        {
            var pv = self ? View?.Self : View?.Opp;
            int gained = pv?.TempGainedThisTurn ?? 0;
            int cap = pv?.LandCap ?? 0;
            return $"{(self ? "己方" : "对方")}临时元素产出\n"
                 + $"本周期（自己回合开始清零，跨对方回合保留）累计 {gained}/{cap}\n"
                 + "全类型六色同池共一配额：代价补偿黑白、错边转化、生物产色等一切临时元素生成都计入；"
                 + "配额耗尽后再生成不入账（支付不受影响——钱包照付）";
        }

        /// <summary>时间双格悬停（2026-10-17 定案；2026-10-18 网络簇整删后恒人机=恒不限时）。</summary>
        private string TimeTileInfo(bool self)
        {
            var title = self ? "己方剩余操作时间" : "对手剩余操作时间";
            return title + "\n（人机对战时，不限制操作时间）";
        }

        /// <summary>连锁速度格悬停（记速器实值+双方发动门槛，2026-09-13 引擎口径）。</summary>
        private string ChainSpeedTileInfo()
        {
            var d = View;
            if (d == null || d.ChainSpeed < 0)
                return "连锁速度（记速器）\n（网络对局快照未承载此数值——联机复活时随协议补）";
            string gate = d.MyTurn
                ? $"我方回合：我方再发动需速度 ≥ {d.ChainSpeed}，对手响应需速度 > {d.ChainSpeed}"
                : $"对手回合：对手再发动需速度 ≥ {d.ChainSpeed}，我方响应需速度 > {d.ChainSpeed}";
            string state = d.ChainResolving ? "\n（连锁结算中——禁止新的速度发动）" : "";
            return $"连锁速度（记速器）：{d.ChainSpeed}{state}\n{gate}\n连锁结算完毕后归 0";
        }

        /// <summary>悬停展示：功能地块/特殊格=信息文本；单位格（生物+结界，双方）与角色格（角色+技能卡）
        /// =四段悬停信息（CardHoverInfo：主动/自动/关键词/状态）。（2026-10-23 v4 §10.2：格前三键退役，
        /// 键解说口随删——动作可用性 2026-10-26 起由点单位弹头顶 3D 钮组 BattleUnitActionButtons 表达。）</summary>
        internal void Handle(string hitName)
        {
            if (string.IsNullOrEmpty(hitName)) { Clear(); return; }
            // 源缓存（2026-10-18 性能）：同命中+同视图版本=文本必未变——跳过四段全文构建与去重键拼接
            // （悬停数据源全部随 RefreshField 重建：View/引擎事件触发刷新；时间格文案不含活秒数）
            var srcKey = hitName + "#" + ViewVersion;
            if (srcKey == _hoverSrcKey) return;
            _hoverSrcKey = srcKey;
            if (hitName == "hud-tile-end-turn") { SetHoverInfo(EndTurnTileInfo()); return; }
            if (hitName == "hud-tile-phase") { SetHoverInfo(PhaseTileInfo()); return; }
            if (hitName == "hud-tile-opp-time") { SetHoverInfo(TimeTileInfo(false)); return; }
            if (hitName == "hud-tile-chain-speed") { SetHoverInfo(ChainSpeedTileInfo()); return; }
            if (hitName == "hud-tile-self-temp") { SetHoverInfo(TempGainTileInfo(true)); return; }
            if (hitName == "hud-tile-opp-temp") { SetHoverInfo(TempGainTileInfo(false)); return; }
            if (hitName == "hud-tile-self-time") { SetHoverInfo(TimeTileInfo(true)); return; }

            // 施放占格类型图标（2026-10-15 发动格拆除）：发动中的卡=公开信息——四段卡信息直出
            if (hitName.StartsWith(BattleFieldView.CastingCardPrefix, System.StringComparison.Ordinal))
            {
                var casting = CastingCardAt(hitName);
                if (casting != null) SetHoverInfo(CardHoverInfo(casting));
                else Clear();
                return;
            }

            int hx, hz;
            if (!TryParseTile(hitName, out hx, out hz)) { Clear(); return; }

            var role = BoardLayout.RoleOf(hx, hz);
            int ownerIdx = BoardLayout.OwnerOf(hx, hz);
            if (role == CellRole.Character)
            {
                // 角色格（2026-10-16 技能格撤销）：角色信息（生命/弹药）；
                // 技能介绍已迁【发动】键悬停（对手格无键=介绍留此兜底）
                SetHoverInfo(HeroHoverInfo(ownerIdx));
                return;
            }
            if (role == CellRole.Land)
            {
                SetHoverInfo(LandCellInfo(hx, hz, ownerIdx));
                return;
            }
            if (role != CellRole.Unit)
            {
                SetHoverInfo(ZoneCellInfo(role, ownerIdx));
                return;
            }

            var unit = UnitAt(hx, hz);
            if (unit != null && IsPreviewableUnit(unit))
            {
                SetHoverInfo(CardHoverInfo(unit));
                return;
            }
            Clear(); // 地牌/空格不出信息（2026-10-13 用户定案范围=生物+结界）
        }

        internal void Clear()
        {
            _hoverSrcKey = null; // 源键随清（重进同格也走一次完整构建，防漏版）
            if (_hoverKey == null) return;
            _hoverKey = null;
            if (_hoverInfo != null && _hoverInfo.gameObject.activeSelf) _hoverInfo.gameObject.SetActive(false);
        }

        /// <summary>场上卡悬停信息（2026-10-15 定案，ACard 预览随 prefab 节点删除退役）：只展示四段——
        /// 主动效果（带发动费用）/自动效果（带本回合生效次数）/关键词（带完整解说）/状态（指示物完整解说
        /// +受到的连接光环清单），空段整段省略。数据口径与手牌卡面（CardOverlayItem.FromHand）同链：
        /// 主动=TriggerTiming∈{Activate_Active, Activate_Instant}，其余（含响应式）归自动；
        /// 解说=KeywordEffectMapper/CounterText；光环=LinkAuraSystem.GetApplicableAuras 反查。</summary>
        private string CardHoverInfo(BattleCardView card)
        {
            var data = _dataOf(card);
            string title = string.IsNullOrEmpty(card.Name)
                ? (string.IsNullOrEmpty(data?.CardName) ? card.CardId ?? "?" : data.CardName)
                : card.Name;
            var sections = new List<string> { title };

            var executor = card.CoreCard != null && GameCore.Instance != null
                ? GameCore.Instance.StackEngine.GetExecutor() : null;

            List<string> actives = null, autos = null;
            if (data?.Effects != null)
            {
                foreach (var fx in data.Effects)
                {
                    if (fx == null) continue;
                    var timing = (TriggerTiming)fx.TriggerTiming;
                    var text = AtomText.RenderEffectSummary(new EffectGraphData(fx.DisplayName) { header = fx, steps = fx.Steps }, data);
                    if (string.IsNullOrEmpty(text)) continue;
                    if (timing == TriggerTiming.Activate_Active || timing == TriggerTiming.Activate_Instant)
                    {
                        string cost = fx.AnchorCost != null && !fx.AnchorCost.IsZero
                            ? "（费用 " + BattleView.CostTextOf(fx.AnchorCost) + "）" : "";
                        (actives ??= new List<string>()).Add(text + cost);
                    }
                    else
                    {
                        (autos ??= new List<string>())
                            .Add(text + CardOverlayItem.UsageSuffix(fx, executor, card.CoreCard, "本回合 "));
                    }
                }
            }
            if (actives != null) sections.Add("【主动效果】\n" + string.Join("\n", actives));
            if (autos != null) sections.Add("【自动效果】\n" + string.Join("\n", autos));

            // 关键词：运行态优先（含授予/净化合并），回落印刷；完整解说=关键词表 description
            var keywords = card.Runtime?.Keywords ?? data?.Keywords?.ToArray();
            if (keywords != null && keywords.Length > 0)
            {
                var lines = new List<string>();
                foreach (var kw in keywords)
                {
                    if (string.IsNullOrEmpty(kw)) continue;
                    string desc = KeywordEffectMapper.GetDescription(kw);
                    lines.Add(string.IsNullOrEmpty(desc)
                        ? KeywordEffectMapper.GetNameZh(kw)
                        : $"{KeywordEffectMapper.GetNameZh(kw)}：{desc}");
                }
                if (lines.Count > 0) sections.Add("【关键词】\n" + string.Join("\n", lines));
            }

            // 状态：运行态指示物（层≠0 且剔内部读数层；完整解说=CounterText 含生命周期）+ 受到的连接光环
            //（2026-10-15 补：live-query 反查来源，与加成计算同判定链；光环关键词不物化不入 _keywords，
            // 故与上方关键词段不重复；同源同增益叠加计 ×N——箭头叠加计数语义）
            var statusLines = new List<string>();
            // 守护/舍身配对（2026-10-16 定案）：发动结成后在状态段书写当前绑定对象——
            // 守护=FindWard 正查（配对在即展示，被守护者死亡不清链）；舍身=FindVictim 活性口
            //（目标死亡期间伤害不改写，状态同样不报，复活即复显）。
            if (card.CoreCard != null)
            {
                var ward = GuardianRules.FindWard(card.CoreCard);
                if (ward != null) statusLines.Add("当前在守护：" + HoverEntityName(ward));
                var victim = MartyrdomRules.FindVictim(card.CoreCard);
                if (victim != null) statusLines.Add("当前在舍身：" + HoverEntityName(victim));
            }
            if (card.Runtime?.Counters != null)
            {
                foreach (var c in card.Runtime.Counters)
                {
                    if (c == null || c.Value == 0) continue;
                    var spec = CardCore.Attribute.CounterRules.Find(c.Key);
                    if (spec != null && spec.LayerRole == CardCore.Attribute.CounterLayerRole.Reading) continue;
                    // 倒计时状态行（2026-10-20 固定触发定案）：恒「倒计时【x】」——归零即自动直发并重挂，
                    // 无待发完成态（tactivate 拆分对倒计时收回）。
                    if (c.Key == CardCore.Attribute.CounterRules.CountdownCounter)
                    {
                        statusLines.Add($"倒计时【{c.Value}】");
                        continue;
                    }
                    statusLines.Add(CounterText.Line(c.Key, c.Value));
                }
            }
            if (card.CoreCard != null)
            {
                var grants = LinkAuraSystem.GetApplicableAuras(card.CoreCard)
                    .Select(a => (name: CardNameOf(a.source), grant: AuraGrantText(a.aura)))
                    .Where(t => !string.IsNullOrEmpty(t.grant))
                    .GroupBy(t => (t.name, t.grant))
                    .Select(g => g.Count() > 1 ? $"{g.Key.name}：{g.Key.grant}×{g.Count()}" : $"{g.Key.name}：{g.Key.grant}")
                    .ToList();
                if (grants.Count > 0) statusLines.Add("受光环｜" + string.Join("；", grants));
            }
            if (statusLines.Count > 0) sections.Add("【状态】\n" + string.Join("\n", statusLines));

            return string.Join("\n", sections);
        }

        /// <summary>引擎卡显示名（光环来源行用）：CardWrapper 数据面卡名，查表兜底。</summary>
        private static string CardNameOf(Card core)
        {
            var name = (core as CardWrapper)?.GetData()?.CardName;
            if (!string.IsNullOrEmpty(name)) return name;
            var data = string.IsNullOrEmpty(core.ID) ? null : CardCatalog.GetByInstanceId(core.ID);
            return string.IsNullOrEmpty(data?.CardName) ? core.ID : data.CardName;
        }

        /// <summary>状态行实体名（守护/舍身绑定对象）：卡=CardNameOf 同链；角色=【己方/对方角色】
        ///（RegionTargetSelector 候选同口径：本地局人类恒 P1）。</summary>
        private string HoverEntityName(Entity e)
        {
            if (e is Card c) return CardNameOf(c);
            if (e is Player p) return p == P1 ? "【己方角色】" : "【对方角色】";
            return e != null ? e.ToString() : "?";
        }

        /// <summary>光环条目授予文案（与计价明细 CardCostService 同词表）：属性±N/生命±N/攻击±N/关键词中文短名。</summary>
        private static string AuraGrantText(LinkAuraData aura)
        {
            if (!string.IsNullOrEmpty(aura.stat))
            {
                bool isLife = aura.stat.Equals("Life", StringComparison.OrdinalIgnoreCase);
                bool isBoth = aura.stat.Equals("Both", StringComparison.OrdinalIgnoreCase);
                return $"{(isBoth ? "属性" : isLife ? "生命" : "攻击")}{(aura.value >= 0 ? "+" : "")}{aura.value}";
            }
            if (!string.IsNullOrEmpty(aura.keyword))
                return KeywordEffectMapper.GetNameZh(aura.keyword);
            return null;
        }

        /// <summary>悬停信息文本（文本未变不重写）。替换文字后在显示态强排自动布局——隐藏状态下刷新无效，须等 SetActive 之后。</summary>
        private void SetHoverInfo(string text)
        {
            if (string.IsNullOrEmpty(text)) { Clear(); return; }
            if (_hoverKey == "info:" + text) return;
            _hoverKey = "info:" + text;
            if (_hoverInfo == null) return;
            bool replaced = _hoverInfoLabel.text != text;
            if (replaced) _hoverInfoLabel.text = text;
            if (!_hoverInfo.gameObject.activeSelf) _hoverInfo.gameObject.SetActive(true);
            if (replaced) LayoutRebuilder.ForceRebuildLayoutImmediate(_hoverInfo);
        }

        internal static bool TryParseTile(string hitName, out int x, out int z)
        {
            x = z = -1;
            if (string.IsNullOrEmpty(hitName) || !hitName.StartsWith("tile-")) return false;
            var parts = hitName.Split('-');
            return parts.Length == 3 && int.TryParse(parts[1], out x) && int.TryParse(parts[2], out z);
        }

        /// <summary>格位反查战场单位（双方）。</summary>
        internal BattleCardView UnitAt(int x, int z)
        {
            return (View.SelfUnits ?? new List<BattleCardView>()).FirstOrDefault(c => c.X == x && c.Z == z && !c.IsDead)
                   ?? (View.OppUnits ?? new List<BattleCardView>()).FirstOrDefault(c => c.X == x && c.Z == z && !c.IsDead);
        }

        /// <summary>悬停信息范围（2026-10-13 用户定案）：生物+结界；查表 miss（无类型数据）保守放行。</summary>
        private bool IsPreviewableUnit(BattleCardView unit)
        {
            var data = _dataOf(unit);
            return data == null || data.Supertype == Cardtype.Creature || data.Supertype == Cardtype.Enchantment;
        }

        private string EndTurnTileInfo()
        {
            if (View == null) return "结束回合";
            string state = View.MyTurn
                ? (View.Phase == PhaseType.Main ? "当前可结束——点击推进回合" : $"当前为{BattleView.PhaseZh(View.Phase)}阶段（主要阶段才可操作）")
                : "对手回合进行中…";
            return "结束回合\n" + View.PhaseText + "\n" + state;
        }

        /// <summary>阶段显示格悬停（2026-10-14 定案：中线 x0 平贴当前阶段文字——本格纯展示，点击只收弹窗）。</summary>
        private string PhaseTileInfo()
        {
            if (View == null) return "阶段显示";
            return "阶段显示\n" + View.PhaseText + "\n回合结构：准备 → 主要 → 结束";
        }

        /// <summary>特殊格悬停信息（数量+点击引导；技能格/上限格/中线=2026-10-14 改版新增）。</summary>
        private string ZoneCellInfo(CellRole role, int ownerIdx)
        {
            var owner = ownerIdx == 0 ? P1 : P2;
            string who = owner == P1 ? "我方" : "对手";
            var pv = owner == P1 ? View?.Self : View?.Opp;
            switch (role)
            {
                case CellRole.Graveyard:
                {
                    int n = pv?.GraveyardCount ?? 0;
                    return n > 0 ? $"{who}墓地 · {n} 张\n点击查看" : $"{who}墓地 · 空";
                }
                case CellRole.Exile:
                {
                    int n = ZoneCountOf(Core, owner, Zone.Exile);
                    return n > 0 ? $"{who}除外区 · {n} 张\n点击查看" : $"{who}除外区 · 空";
                }
                case CellRole.Deck:
                {
                    // 点击门槛同款（2026-10-17）：有被展示的卡才可点开列表，否则只有本悬停提示；
                    // 2026-10-26 标记补充：诅咒/祝福标记在库时报计数（点击开预览条——优先于被展示口径）
                    int n = pv?.DeckCount ?? ZoneCountOf(Core, owner, Zone.Deck);
                    var marked = MarkedDeckCardsOf(Core, owner);
                    if (marked.Count > 0)
                    {
                        int curse = marked.Count(c => c.GetCounterCount(CounterRules.CurseCounter) > 0);
                        return $"{who}卡组 · 剩 {n} 张（含诅咒 {curse} · 祝福 {marked.Count - curse}，点击查看）";
                    }
                    int exposed = ExposedDeckCardsOf(Core, owner).Count;
                    return exposed > 0
                        ? $"{who}卡组 · 剩 {n} 张（被展示 {exposed} 张，点击查看）"
                        : $"{who}卡组 · 剩 {n} 张（隐藏）";
                }
                // CellRole.Skill 已随技能格撤销（2026-10-16）退役：技能介绍=己方【发动】键悬停（对手=角色格悬停）
                case CellRole.Extra:
                {
                    // 任意元素格叠展于此（2026-10-23：BoardLayout.AnyElementCell）——余额读数随悬停补齐；
                    // 额外区入口文案顺带补上（此前该格悬停无文案）
                    int anyN = pv != null && (int)ManaType.Any < pv.WalletByColor.Length
                        ? pv.WalletByColor[(int)ManaType.Any] : 0;
                    int extraN = ZoneCountOf(Core, owner, Zone.Extra);
                    string anyLine = $"任意元素 · 钱包余额 {anyN}（可代替任何元素支付，回合结束消失；不由地牌产出）";
                    return extraN > 0
                        ? $"{who}格 · {anyLine}\n{who}额外区 · 衍生物模板 {extraN} 张（点击查看）"
                        : $"{who}格 · {anyLine}";
                }
                case CellRole.Middle:
                    return null; // 中线格全数认领为功能格（2026-10-16 产量双格补 lx2/lx4 后无空格）——已改名 hud-tile-*，走各自悬停分支
                default:
                    return null;
            }
        }

        /// <summary>角色格悬停信息（2026-10-16 技能格撤销改版；当晚迁移）：角色本体（生命/护甲/攻击弹药）。
        /// 己方技能四段介绍已迁到格前【发动】键悬停（TileButtonHoverInfo）；对手角色格无键可悬，
        /// 技能是公开信息——介绍留在此处兜底。无技能局=占位行。
        /// 2026-10-17 角色状态展示（参考生物）：补【状态】段——指示物完整解说（CounterText）+
        /// 受到的连接光环清单（LinkAuraSystem.GetApplicableAuras(Player) 新口，与生物同格式）。</summary>
        private string HeroHoverInfo(int ownerIdx)
        {
            var owner = ownerIdx == 0 ? P1 : P2;
            var pv = ownerIdx == 0 ? View?.Self : View?.Opp;
            string who = ownerIdx == 0 ? "我方" : "对手";
            int life = pv?.Life ?? owner?.Life ?? 0;
            int armor = pv?.Armor ?? 0;
            int ammo = owner != null ? owner.GetPower() : 0;
            var lines = new List<string>
            {
                $"{who}角色 · {owner?.Name ?? "?"}　生命 {life}{(armor > 0 ? $"（护甲+{armor}）" : "")}　攻击弹药 {ammo}",
            };

            // 状态段（2026-10-17，与 CardHoverInfo 状态段同构）：指示物全量解说（滤 0 层与读数层）
            // + 受光环清单（本地 live-query；网络局与生物侧同判跳过——快照不承载光环命中）。
            var statusLines = new List<string>();
            if (pv?.Counters != null)
            {
                foreach (var c in pv.Counters)
                {
                    if (c == null || c.Value == 0) continue;
                    var spec = CardCore.Attribute.CounterRules.Find(c.Key);
                    if (spec != null && spec.LayerRole == CardCore.Attribute.CounterLayerRole.Reading) continue;
                    statusLines.Add(CounterText.Line(c.Key, c.Value));
                }
            }
            if (owner != null && GameCore.Instance != null)
            {
                var grants = GameBoard.LinkAuraSystem.GetApplicableAuras(owner)
                    .Select(a => (name: CardNameOf(a.source), grant: AuraGrantText(a.aura)))
                    .Where(t => !string.IsNullOrEmpty(t.grant))
                    .GroupBy(t => (t.name, t.grant))
                    .Select(g => g.Count() > 1 ? $"{g.Key.name}：{g.Key.grant}×{g.Count()}" : $"{g.Key.name}：{g.Key.grant}")
                    .ToList();
                if (grants.Count > 0) statusLines.Add("受光环｜" + string.Join("；", grants));
            }
            if (statusLines.Count > 0) lines.Add("【状态】\n" + string.Join("\n", statusLines));

            if (ownerIdx == 0)
                lines.Add("操作=格前三键：攻击/守卫（需攻击弹药）、发动技能（技能详情=【发动】键悬停）");
            else
            {
                var skill = pv?.Skill;
                if (skill != null) lines.Add(CardHoverInfo(skill));
                else lines.Add("本局无技能卡");
            }
            return string.Join("\n", lines);
        }

        internal static int ZoneCountOf(GameCore core, Player owner, Zone zone)
            => core != null ? (core.ZoneManager.GetCards(owner, zone)?.Count ?? 0) : 0;

        /// <summary>卡组中被展示的卡（RevealRules 信息轴单源：Exposed 指示物=对双方公开，
        /// 换区清除）。卡组 Region 面板的列卡内容与点击门槛共用此口。</summary>
        internal static List<Card> ExposedDeckCardsOf(GameCore core, Player owner)
            => core != null ? RevealRules.GetExposedCards(core.ZoneManager, owner, Zone.Deck) : new List<Card>();

        /// <summary>卡组中被诅咒/祝福标记的卡（2026-10-26 显示层补充）：牌库区卡按 Curse/Blessing
        /// 层数>0 直读引擎态（诅咒在前祝福在后）。牌库预览条/点击门槛/悬停计数共用此单源。</summary>
        internal static List<Card> MarkedDeckCardsOf(GameCore core, Player owner)
        {
            var r = new List<Card>();
            var cards = core != null && owner != null ? core.ZoneManager.GetCards(owner, Zone.Deck) : null;
            if (cards == null) return r;
            List<Card> blessed = null;
            foreach (var c in cards)
            {
                if (c == null) continue;
                if (c.GetCounterCount(CounterRules.CurseCounter) > 0) r.Add(c);
                else if (c.GetCounterCount(CounterRules.BlessingCounter) > 0) (blessed ??= new List<Card>()).Add(c);
            }
            if (blessed != null) r.AddRange(blessed);
            return r;
        }

        /// <summary>地牌格悬停信息（2026-10-14 晚固定元素格）：格身份=LandCellElementOrder 序（灰红蓝绿黑白）；
        /// 计数报「可用/上限」（可用=未横置+钱包该色；黑白=钱包余额；
        /// 上限=红蓝绿灰该色已放入地牌张数、黑白格只显余量——2026-10-16 全类型累计钳改案）。</summary>
        private string LandCellInfo(int x, int z, int ownerIdx)
        {
            var pv = ownerIdx == 0 ? View?.Self : View?.Opp;
            var lands = ownerIdx == 0 ? View?.SelfLands : View?.OppLands;
            // 固定元素身份（玩家视角左→右 灰红蓝绿黑白）
            var cells = BoardLayout.LandCells(ownerIdx);
            ManaType element = ManaType.Gray;
            for (int i = 0; i < cells.Count && i < BoardLayout.LandCellElementOrder.Length; i++)
                if (cells[i].x == x && cells[i].z == z)
                    element = BoardLayout.LandCellElementOrder[i];
            bool isBw = element == ManaType.White || element == ManaType.Black;
            string head = $"{(ownerIdx == 0 ? "我方" : "对手")}地牌 · {BattleView.ManaZh(element)}元素格";
            var toks = new List<string>();
            if (lands != null)
            {
                foreach (var g in lands.Where(c => c.X == x && c.Z == z && c.Runtime != null)
                                       .GroupBy(c => c.Runtime.LockedColor))
                {
                    int untapped = g.Count(c => !c.IsTapped);
                    int wallet = pv != null && g.Key >= 0 && g.Key < pv.WalletByColor.Length ? pv.WalletByColor[g.Key] : 0;
                    string color = g.Key >= 0 ? BattleView.ManaZh((ManaType)g.Key) : "?";
                    toks.Add($"{color} {untapped + wallet}/{g.Count()}");
                }
            }
            // 地牌上限显示迁移（2026-10-14 晚：上限格退役）——地牌格悬停就地报上限（本批措辞定案：【】值域）
            string capLine = $"当前地牌上限【{pv?.LandCap ?? 0}】（每回合开始双方同步加一，封顶10；效果可再提升）";
            if (isBw)
                return $"{head}（黑白不产地牌，只进钱包）\n使用对自己有害的代价产生\n"
                     + $"全类型累计生成钳：所有颜色临时元素每周期（自己回合开始清零）合计不超过当前地牌上限【{pv?.LandCap ?? 0}】——产量看中线「己方/对方临时」格\n"
                     + $"钱包余额 {(pv != null ? pv.WalletByColor[(int)element] : 0)}（黑白格面数字=余量，不显上限）";
            return toks.Count == 0
                ? $"{head}（暂无该色地牌）\n{capLine}"
                : $"{head}\n{string.Join("\n", toks)}\n可用=未横置+钱包该色，上限=该色地牌张数；付费时自动横置\n{capLine}";
        }

        /// <summary>施放类型图标反查（cast-{RuntimeId} 命中名 → 视图数据施放占格卡；未命中=null）。</summary>
        internal BattleCardView CastingCardAt(string hitName)
        {
            if (string.IsNullOrEmpty(hitName)
                || !hitName.StartsWith(BattleFieldView.CastingCardPrefix, System.StringComparison.Ordinal))
                return null;
            if (!uint.TryParse(hitName.Substring(BattleFieldView.CastingCardPrefix.Length), out uint rid))
                return null;
            var cards = View?.CastingCards;
            if (cards == null) return null;
            foreach (var c in cards)
                if (c != null && c.RuntimeId == rid) return c;
            return null;
        }
    }
}
