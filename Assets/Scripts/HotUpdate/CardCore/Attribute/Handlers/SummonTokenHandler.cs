using System;
using System.Collections.Generic;
using CardCore.Attribute;

namespace CardCore.Attribute.Handlers
{
    /// <summary>
    /// 召唤衍生物（SummonToken）——时点接线配套原子（原 PutToBattlefield 枚举位收编）。
    ///
    /// 设计定案：
    /// - 落位口径（2026-11-20 计划 §3.1）：**声明期计划** Interaction.SummonCells（人类落位窗/LAE
    ///   计划行写入）逐只按序钉格，竞态被占取最近空格兜底；**无计划**（触发式/奖励/旧路径/AI 默认）
    ///   走 SummonPlacementRules.DefaultCell（旧 2×3 母巢块/贪心聚集算法原样搬入——行为不变）。
    /// - 数量/计价口径（2026-11-20）：**并列位**数量任意正整数；**奖励位**（branch.then 奖励/附加
    ///   祝福载荷）数量恒 1（UI 锁定+保存/装载双闸；本 handler 按 Max(1,Value) 同口径兜底），
    ///   模板单只计价=推导费+2（CardCostService.SummonUnitPrice 单源）须 ≤ 分支奖励上限
    ///   （ComposerCatalog.BranchRewardCapOf；附加诅咒/祝福载荷位无上限）。附加诅咒位收不了
    ///   召唤——极性闸（CursePayloadError/装载校验）拦 +1 行，且载荷控制者=施加方（CurseSystem），
    ///   放行会落施加方场而非「塞给对手」。
    /// - token 有全套卡参数（模板 = CardData），与卡的核心区别是**不唯一**；
    /// - 实例 ID = 模板ID#序号（TimestampSystem.NextSequence，对局中临时赋值作索引，
    ///   先例 CopyEffectsEngine.GenerateCopyID；实例 ID 唯一化避免 TextChangeLayer._cardIdMap 同 ID 互覆）；
    /// - 落区写死战场（2026-10-05 定案：衍生物恒落战场，组合层三档退役；落区系数亦于 2026-10-09
    ///   随之内生化退役——计价=模板费×数量，不再外乘分档系数）；
    /// - 模板解析经静态委托 ResolveTemplate（组合根注入 CardCatalog.GetById，
    ///   仿 MorphSystem.ResolveMorphTarget；未注入时复用变形解析器——同为模板ID→CardData）。
    /// - 在场按生物规则，离场即出局（2026-10-12 推翻 2026-10-10「生成后完全按生物规则」口径——
    ///   当年为让衍生物填平快攻地牌缺口而让渡，地牌经济改版后衍生物不进地牌，理由消失）：
    ///   入场即可用（2026-10-17 取消横置入场）/回合重置/攻守能力/触发式/被指在场期间全同普通生物；每只挂 1 层遗忘
    ///  （TempCounter 生效自减）——离场（死亡/弹回/洗回）后于持有者回合末从游戏中移除，
    ///   不滞留墓地（表行「并赋予一层遗忘指示物」就此兑现）；
    ///   不作地牌（CanServeAsLand 拒绝 IsToken——2026-10-03 反白嫖定案，弹回手牌也不可入元素池）。
    ///
    /// 落战场的进场事件（Source=TokenSpawned）与触发式注册由 TryAddToBattlefield 统一出口承担；
    /// 入手走区域容器（禁止 Player.AddToHand 旧列表），非抽牌入手（IsDraw=false）喂 NonDrawDrawAccum。
    /// </summary>
    public class SummonTokenHandler : AtomicEffectHandlerBase
    {
        /// <summary>token 模板解析器：StringValue（模板卡 ID）→ CardData。由组合根注入；null 时静默跳过。</summary>
        public static Func<string, CardData> ResolveTemplate;

        /// <summary>引用卡槽的生效模板 ID（P9b 2026-10-29 卡组期绑定，召唤/变形两 handler 共用）。
        /// 解析序：① 卡实例绑定——来源卡（Source，法术归因时退 CastCard）持绑定表且绑定所属卡 ID
        /// 与当前数据一致（变形/复制换源即失效），按 context.ResolvingEffect.Id 对位宿主卡
        /// CardData.Effects 下标查表（转换器保序保 Id，装载器按下标盖章——两侧同序）；
        /// ② 回落原子 str（旧数据/教程固定绑定）；③ 都无 → null（调用方记 Error 空转，不静默白板）。
        /// 限制：同卡同效果 ID 出现多序时取首序（绑定粒度=效果序号，见 P9b 汇报）。</summary>
        public static string ResolveBoundTemplateId(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            var host = context?.Source as CardWrapper ?? context?.CastCard as CardWrapper;
            if (host != null && host.HasTokenBindings && context.ResolvingEffect != null)
            {
                var data = host.GetData();
                var effects = data?.Effects;
                if (effects != null && data.ID == host.TokenBindingsCardId)
                {
                    var defId = context.ResolvingEffect.Id;
                    for (int i = 0; i < effects.Count; i++)
                    {
                        if (effects[i] == null || effects[i].Id != defId) continue;
                        if (host.TokenBindings.TryGetValue(i, out var bound) && !string.IsNullOrEmpty(bound))
                            return bound;
                        break; // 命中效果而该槽未绑 → 落回固定绑定
                    }
                }
            }
            return effect?.StringValue;
        }

        /// <summary>来源可读名（Error 文案用）。</summary>
        private static string NameOf(EffectExecutionContext context)
        {
            var card = context?.Source as CardWrapper ?? context?.CastCard as CardWrapper;
            var data = card?.GetData();
            return data != null && !string.IsNullOrEmpty(data.CardName) ? data.CardName : data?.ID ?? "?";
        }

        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.SummonToken;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            if (context.ZoneManager == null || context.Controller == null) return;

            var resolver = ResolveTemplate; // 引用卡ID 唯一解析器（2026-10-13 临摹退役后不再回退变形解析器）
            if (resolver == null) return;

            // P9b：模板 ID 先走卡实例绑定（卡组期），空槽无绑定回落原子 str；两者皆空=显性 Error
            string templateId = ResolveBoundTemplateId(effect, context);
            if (string.IsNullOrEmpty(templateId))
            {
                TideLog.Error($"[SummonTokenHandler] 衍生物槽未绑定（来源 {NameOf(context)}）——"
                              + "本局卡组未提供模板且效果无固定绑定，空转（装载期校验应已拦截，此处运行时兜底）");
                return;
            }
            var template = resolver(templateId);
            if (template == null)
            {
                TideLog.Error($"[SummonTokenHandler] 衍生物模板 {templateId} 解析失败（来源 {NameOf(context)}）——空转");
                return;
            }

            // 2026-09-11 定案：{衍生物} = 指向一张真实卡——任何衍生物都是某张真实卡的实例，
            // 身份/观测/计价全走真实卡数据。2026-11-18 放宽：模板可为生物或结界；
            // 其他类型 → 拒绝（构筑校验另有拦截，此处运行时兜底）。
            // 2026-09-13 修复：真实链路（CardCatalog.GetById）返回裸 CardData——不实现 IHasSupertype
            // （仅 CardWrapper 实现），is 判定恒 false 导致全游戏 SummonToken 静默空转；改属性直判。
            var templateSupertype = template is IHasSupertype ht ? ht.Supertype : template.Supertype;
            if (template.IsField)
            {
                TideLog.Warn($"[SummonTokenHandler] 衍生物模板 {templateId} 是场地结界，跳过（场地卡不能作衍生物模板）");
                return;
            }
            if (templateSupertype != Cardtype.Creature && templateSupertype != Cardtype.Enchantment)
            {
                TideLog.Warn($"[SummonTokenHandler] 衍生物模板 {templateId} 既不是生物也不是结界，跳过");
                return;
            }

            int count = effect.Value <= 0 ? 1 : effect.Value;
            // 2026-10-05 落区写死定案：衍生物恒落战场（组合层 SummonDropZone 三档退役——
            // 临时卡类落手牌、毁灭/除外/弹回/洗回去向原生固定，均不经组合层落区）
            var dropZone = Zone.Battlefield;

            var pinnedThisCast = new HashSet<(int x, int z)>(); // 本次召唤已钉格（计划模式竞态兜底排除集）
            for (int i = 0; i < count; i++)
            {
                // 全参数工厂 + 对局临时实例 ID（IsToken：衍生物不作地牌——2026-10-03 地牌资格定案）
                var token = new CardWrapper(template)
                {
                    ID = $"{templateId}#{CardCore.TimestampSystem.NextSequence}",
                    IsToken = true,
                };
                token.SetController(context.Controller);

                // 遗忘 1 层（2026-10-12 表行文案兑现）：在场按生物规则，离场后于持有者回合末
                // 从游戏中移除（CounterRules.OnTurnEnd ② 例程——与回响临时复制同一指示物）
                int actualDelta = token.AddCounters(CounterRules.TempCounter, 1);
                PublishEvent(new CounterChangedEvent
                {
                    Target = token,
                    CounterType = CounterRules.TempCounter,
                    Amount = actualDelta,
                    Source = context.Source,
                });

                switch (dropZone)
                {
                    case Zone.Hand:
                        // 容器 Add 不经 OnCardMoved——补发入手事件（非抽牌，FromZone=None=token 新生）
                        context.ZoneManager.GetZoneContainer(context.Controller)?.Add(token, Zone.Hand);
                        PublishEvent(new CardEnterHandEvent
                        {
                            Player = context.Controller,
                            Card = token,
                            FromZone = Zone.None,
                            IsDraw = false
                        });
                        break;

                    case Zone.Deck:
                        context.ZoneManager.GetZoneContainer(context.Controller)?.Add(token, Zone.Deck);
                        context.ZoneManager.ShuffleDeck(context.Controller);
                        break;

                    default:
                        // 落位计划（2026-11-20 计划 §3.1）：
                        // · 声明期 Interaction.SummonCells（人类落位窗/LAE 计划行）——**逐只按序**钉格
                        //   （第 i 只=SummonCells[i]；竞态被占→UnitCells 序最近可用空格兜底并记日志；
                        //   仍无格→该只不 Pin 走默认入场/满场逻辑）。声明期已 ValidateCells，此处只兜竞态。
                        // · 无计划（触发式/奖励/旧路径/AI 默认）——默认计划 SummonPlacementRules.DefaultCell
                        //   （原 handler 算法 2026-11-16 口径原样搬入单源：5 只母巢 2×3 块/贪心聚集，
                        //   行为逐字节不变；现状=所有只同格钉）。结算期触发的召唤恒走本支
                        //  （口径：预选确认才发动；结算中选择不选就自动选）。
                        var board = GameBoard.MoveUnitRules.Current;
                        int pinX = -1, pinZ = -1;
                        List<(int x, int z)> planCoords = null;
                        if (board != null)
                        {
                            if (context.Interaction != null && context.Interaction.HasSummonCells)
                            {
                                var layout = GameBoard.BoardLayout.UnitCells(board.PlayerIndex(context.Controller));
                                planCoords = new List<(int x, int z)>(count);
                                for (int pi = 0; pi < count; pi++)
                                {
                                    int idx = pi < context.Interaction.SummonCells.Count
                                        ? context.Interaction.SummonCells[pi] : -1;
                                    planCoords.Add(idx >= 0 && idx < layout.Count ? layout[idx] : (-1, -1));
                                }
                            }
                            else
                            {
                                var (dx, dz) = GameBoard.SummonPlacementRules.DefaultCell(board,
                                    context.Controller, context.Source as Card,
                                    context.ZoneManager.GetCards(context.Controller, Zone.Battlefield), count);
                                pinX = dx;
                                pinZ = dz;
                            }
                        }
                        // 战场：满场入墓+失败事件、进场事件(TokenSpawned)、触发式注册 均由统一出口承担
                        context.ZoneManager.TryAddToBattlefield(token, context.Controller, EnterSource.TokenSpawned);
                        // PinCard 必须等 token 真正在战场后再钉（入场前 Resync 会清掉 stale pin）。
                        if (planCoords != null)
                        {
                            // 逐只按序：目标格被占（竞态）→ 排除**本次已钉格**后的 UnitCells 序最近空格兜底
                            //（不排除后续计划格——它们是后面几只的合法落位）。
                            // 「被占」判定须排除**本只自身**：TryAddToBattlefield 的 auto-Resync 会先把
                            // 本只 first-free 放上格（常恰好=本只计划格——首次空格即计划格），若不排除
                            // 会把「自己已就位」误判竞态踢去兜底（2026-11-20 探针 B 实锤：#3 期望(3,2)
                            // 实际(1,1)，格主=自己）——占用者==token 时直接按计划格钉（幂等）。
                            var (tx, tz) = i < planCoords.Count ? planCoords[i] : (-1, -1);
                            int px = -1, pz = -1;
                            var holder = tx >= 0 ? board.CardAt(tx, tz) : null;
                            if (tx >= 0 && (holder == null || ReferenceEquals(holder, token)))
                            {
                                px = tx; pz = tz;
                            }
                            else
                            {
                                if (tx >= 0)
                                    TideLog.Warn($"[SummonTokenHandler] 计划格 ({tx},{tz}) 已被占（竞态）"
                                                 + $"——第 {i + 1} 只取最近空格兜底（来源 {NameOf(context)}）");
                                var layout = GameBoard.BoardLayout.UnitCells(board.PlayerIndex(context.Controller));
                                foreach (var c in layout)
                                {
                                    if (pinnedThisCast.Contains(c) || board.CardAt(c.x, c.z) != null) continue;
                                    px = c.x; pz = c.z;
                                    break;
                                }
                                if (px < 0)
                                    TideLog.Warn($"[SummonTokenHandler] 无空格可兜底——第 {i + 1} 只"
                                                 + "不钉格（走默认入场位；来源 " + NameOf(context) + "）");
                            }
                            if (px >= 0)
                            {
                                board.PinCard(token, px, pz);
                                pinnedThisCast.Add((px, pz));
                            }
                        }
                        else if (pinX >= 0) board.PinCard(token, pinX, pinZ);
                        break;
                }

                PublishEvent(new TokenCreatedEvent
                {
                    TokenTemplateId = templateId,
                    Controller = context.Controller,
                    Source = context.Source,
                    Tapped = token._isTapped,
                    Card = token,
                    DropZone = dropZone == Zone.Hand || dropZone == Zone.Deck ? dropZone : Zone.Battlefield
                });

                context.LastOutcome.AffectedTargets.Add(token);
            }
        }

        // 2026-11-20：默认落位算法（2×3 母巢块/贪心聚集）已原样搬入单源
        // GameBoard.SummonPlacementRules.DefaultCell（计划 §3.1）——本 handler 不再持有副本。

        protected override string DescribeTemplate(AtomicEffectInstance effect) =>
            $"生成 {effect.Value} 个衍生物（各带 1 层遗忘——离场后回合末出局，{effect.StringValue}）";
    }
}
