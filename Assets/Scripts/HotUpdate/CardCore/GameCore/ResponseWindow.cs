using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace CardCore
{
    /// <summary>
    /// 响应窗口候选（发动弹窗条目，2026-09-16 战斗接入栈机器定案）：
    /// 速度系统收集的可发主动效果——守卫能力（速度1响应）/待发自愿效果/可打手牌卡/主动能力。
    /// 收集时**双过滤**：元素费用（CanAfford 含 pending 承诺）+ 特殊代价（CostHandlerRegistry），
    /// 付不起的不进弹窗；**候选=0 跳过弹窗直接 Pass**。
    /// </summary>
    public class ResponseOption
    {
        public enum ResponseKind { GuardAbility, VoluntaryEffect, HandCard, ActivatedAbility }

        public ResponseKind Kind;
        /// <summary>弹窗显示文本（名称+费用摘要）。</summary>
        public string Label;

        /// <summary>守卫者/能力宿主/手牌卡。</summary>
        public Card SourceCard;
        /// <summary>守卫者=角色（2026-10-16 角色守卫定案）：Kind=GuardAbility 且本字段非空时，
        /// SourceCard 恒 null——角色守卫无关键词/横置概念，资格=有攻击弹药（见 PushGuardDeclaration）。</summary>
        public Player SourcePlayer;
        /// <summary>待发自愿效果（Kind=VoluntaryEffect）。</summary>
        public PendingEffect Pending;
        /// <summary>主动定义（Kind=ActivatedAbility）。</summary>
        public EffectDefinition Definition;
        /// <summary>拦截目标（Kind=GuardAbility：栈上的攻击宣言对象）。</summary>
        public EffectInstance AttackInstance;

        /// <summary>费用摘要（收集时已过可付性检查，此为显示用）。</summary>
        public string CostSummary;

        /// <summary>手牌响应卡（Kind=HandCard）：施放抉择模式序号（收集期已过该模式门禁；
        /// ApplyResponse 经 PlayCardInResponse 消费——2026-10-20 起声明期预选目标经
        /// CollectSpellDeclarationTargetsAsync 收集后随本字段下发；取消=撤销发动）。</summary>
        public int ModeIndex;

        /// <summary>手牌响应卡（Kind=HandCard）声明期预选目标（2026-10-20）：
        /// null=未收集（无交互原子/AI——结算期照常反问代选）。</summary>
        public List<Entity> Targets;
    }

    /// <summary>
    /// 响应窗口服务（2026-09-16）：人类弹窗处理器与 AI 决策器的注册点。
    /// UI 注册 HumanResponder（ShowChoiceOverlay 简版弹窗）；AI 侧注册/内置启发决策；
    /// 均未注册（无头/训练）→ 候选自动放弃（等效旧行为：双 Pass 排干）。
    /// </summary>
    public static class ResponseWindowService
    {
        /// <summary>人类响应弹窗处理器：返回所选候选，null=跳过（Pass）。
        /// 弹窗由 UI 层负责（筛选 0 时收集口直接 Pass，不会调用本处理器）。</summary>
        public static Func<Player, List<ResponseOption>, UniTask<ResponseOption>> HumanResponder;

        /// <summary>AI 响应决策器：返回所选候选或 null（Pass）。缺省内置守卫启发（SimpleAI 同款）。</summary>
        public static Func<GameCore, Player, List<ResponseOption>, ResponseOption> AiResponder;

        /// <summary>显式放弃响应哨兵（2026-10-02 教学固定局）：AiResponder 返回本对象=让过——
        /// 返回 null 会回落内置守卫启发（GameActions 里 ?? DefaultAiRespond），"永不反制"的教学机器人
        /// 必须走本哨兵；ApplyResponse 对未知类别回落 false → PassPriority，语义即 Pass。</summary>
        public static readonly ResponseOption PassOption = new ResponseOption
        {
            Kind = (ResponseOption.ResponseKind)(-1),
            Label = "让过",
        };

        // ---- 本轮窗口撤销记忆（2026-10-20 法术发动流程改案·用户定案） ----
        // 连锁（响应窗）内取消声明期目标选择=撤销该次发动，且**本轮连锁不再询问该卡是否发动**
        // （防取消→重弹循环）；生命周期=响应窗泵一次会话（SettleResponseWindowAsync 进入时清空）。
        // 栈空主阶段路径无此机制（撤销即完，无重弹语境）。
        private static readonly HashSet<Card> _declinedThisWindow = new HashSet<Card>();

        /// <summary>标记该卡本轮响应窗内已撤销发动（CollectAvailableResponses 剔除其手牌卡候选）。</summary>
        public static void DeclineForThisWindow(Card card)
        {
            if (card != null) _declinedThisWindow.Add(card);
        }

        /// <summary>该卡本轮响应窗内是否已撤销发动（True=不再进候选）。</summary>
        public static bool IsDeclined(Card card) => card != null && _declinedThisWindow.Contains(card);

        /// <summary>响应窗会话开启时清空（SettleResponseWindowAsync 进入口调用=一条连锁会话）。</summary>
        public static void ResetDeclinedWindow() => _declinedThisWindow.Clear();
    }
}
