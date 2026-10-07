using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using Cysharp.Threading.Tasks;

namespace SynergyUI
{
    /// <summary>
    /// 对战编排器（本地模式数据源）：负责组卡 / 初始化 GameCore、棋盘接线，以及驱动 AI 回合。
    /// 2026-09-16 战斗接入栈机器：攻击=速度0栈对象逐攻击开窗（引擎内闭环），
    /// 旧"UI 层补齐战斗结算链"（BeginCombat/ResolveCombat）退役；
    /// 响应窗口（发动弹窗）经 GameActions.SettleResponseWindowAsync 驱动，人类弹窗在 BattleScreen 注册。
    /// 2026-09-24 阶段四重做：开局改随机卡组（卡组选定属匹配阶段职责，测试期用
    /// StreamingAssets 卡池随机 30 张不重复；保留注入参数供匹配界面传入）；
    /// 暴露 Board 只读口供 HUD 渲染格位与箭头。
    /// </summary>
    public sealed class BattleController
    {
        private IAiTurnDriver _ai = new SimpleAI(); // 缺省动作耗尽启发；教学局注入 ScriptedAi（剧本驱动）

        // 教学固定局（2026-10-02）：机器人响应窗口全让过的静态接线前值（Shutdown 恢复防泄漏）
        private bool _passiveResponderOn;
        private Func<GameCore, Player, List<ResponseOption>, ResponseOption> _prevAiResponder;

        // 棋盘占用层（派生，单向读核心区域）：为碾压关键词提供邻接解析 + HUD 格位
        private GameBoard.BoardState _board;

        public GameCore Core => GameCore.Instance;
        public Player P1 => Core?.Player1;
        public Player P2 => Core?.Player2;
        public Player TurnPlayer => Core?.TurnEngine?.TurnPlayer;
        public bool IsPlayerTurn => TurnPlayer != null && TurnPlayer == P1;
        public GameBoard.BoardState Board => _board;

        /// <summary>测试期随机卡组张数（卡组构成定案：30 张每卡 1 张不重复）。</summary>
        public const int RandomDeckSize = 30;

        /// <summary>
        /// 组双方卡组并初始化对局。myDeck/aiDeck 缺省时从本地卡池随机抽取
        /// （不重复、每卡 1 张）；P2 由极简 AI 操作。
        /// 教学局参数（2026-10-02）：aiDriver 注入 ScriptedAi（剧本驱动）；lockDeckOrder 锁牌库顺序
        /// （卡组列表序=摸牌序列，起手=前 6 张）；rngSeed 钉效果随机——三者合成完全确定的教学局。
        /// 预设场面（2026-10-06 教学直入）：scenario 非 null=双方卡组（并集）先全部入牌库、跳起手抽取、
        /// 不自动开局，TutorialScenarioSeeder 静默注入场面后再 StartGame——玩家直接站在起跳回合主阶段。
        /// 挑战模式（2026-10-06）：aiLandCapBonus/aiExtraOpeningDraws 直通 InitGame（P2 曲线上移+起手加抽）。
        /// 原子表工坊（2026-10-06）：装载卡池后 EnsureBattleCosts——overlay 启用时全卡池强制重推改价。
        /// </summary>
        public void StartNewGame(List<CardData> myDeck = null, List<CardData> aiDeck = null,
            IAiTurnDriver aiDriver = null, bool lockDeckOrder = false, int? rngSeed = null,
            TutorialScenario scenario = null, int aiLandCapBonus = 0, int aiExtraOpeningDraws = 0,
            string mySkillCardId = null, string aiSkillCardId = null)
        {
            var catalog = CardCatalog.LoadAll();
            // 原子表工坊改价生效点：教学基线在位时内部自动跳过（教学局用声明费）
            AtomicTableWorkshop.EnsureBattleCosts();
            // 变形目标形态解析器：组合根注入（CardCore 不依赖 UI 层）
            CardCore.Attribute.MorphSystem.ResolveMorphTarget = CardCatalog.GetById;

            bool myRandom = myDeck == null, aiRandom = aiDeck == null;
            if (myDeck == null) myDeck = RandomDeckFrom(catalog);
            if (aiDeck == null) aiDeck = RandomDeckFrom(catalog);
            if (aiDriver != null) _ai = aiDriver;

            // 英雄技能标记（2026-10-07 卡牌化）：显式卡组未带标记=无技能；
            // 随机兜底卡组自动挑一张合格结界（与挑战 AI/RL 环境同口径）
            string mySkill = mySkillCardId ?? (myRandom ? HeroSkillSystem.AutoPickSkillCard(myDeck) : null);
            string aiSkill = aiSkillCardId ?? (aiRandom ? HeroSkillSystem.AutoPickSkillCard(aiDeck) : null);

            if (scenario != null)
            {
                GameCore.Instance.InitGame(myDeck, aiDeck, rngSeed: rngSeed, lockDeckOrder: true,
                    skipOpeningDraw: true, deferStart: true,
                    skillCardId1: mySkill, skillCardId2: aiSkill);
                // 标记 P2 为 AI：目标选择器对 AI 跳过弹窗、即时自动选择。
                if (GameCore.Instance.Player2 != null)
                    GameCore.Instance.Player2.IsAI = true;
                AttachBoard(); // 棋盘先于注入：单位显式落位（PinCard 钉子）需要 BoardState 在场
                TutorialScenarioSeeder.Apply(GameCore.Instance, scenario, _board);
                GameCore.Instance.StartGame(); // TurnEngine 已预置起跳回合 → StartNewTurn 落到 startTurn/Main
                return;
            }

            GameCore.Instance.InitGame(myDeck, aiDeck, rngSeed: rngSeed, lockDeckOrder: lockDeckOrder,
                p2LandCapBonus: aiLandCapBonus, p2ExtraOpeningDraws: aiExtraOpeningDraws,
                skillCardId1: mySkill, skillCardId2: aiSkill);
            // 标记 P2 为 AI：目标选择器对 AI 跳过弹窗、即时自动选择。
            if (GameCore.Instance.Player2 != null)
                GameCore.Instance.Player2.IsAI = true;
            AttachBoard();
        }

        /// <summary>随机卡组：卡池洗牌取前 N 张不重复（Guid 序——非对拍用途，无需钉种子）。
        /// 教学专用卡不进普通随机局/联机测试局（CardCatalog.IsTeachingCard 统一口径，2026-10-06 隔离）
        /// ——教学局按 id 组卡（TutorialLibrary.BuildDeck），不经本过滤。</summary>
        public static List<CardData> RandomDeckFrom(List<CardData> catalog)
        {
            return catalog.Where(c => !CardCatalog.IsTeachingCard(c))
                .OrderBy(_ => Guid.NewGuid()).Take(RandomDeckSize).ToList();
        }

        /// <summary>棋盘占用层接线：注入碾压 AdjacentResolver + 连接光环 LinkAuraSystem（核心不绑棋盘，由宿主组装）。</summary>
        private void AttachBoard()
        {
            DetachBoard();
            var core = GameCore.Instance;
            if (core?.Player1 == null || core.Player2 == null) return;
            _board = new GameBoard.BoardState(core, core.Player1, core.Player2,
                GameBoard.HalfFieldData.Flat(), GameBoard.HalfFieldData.Flat());
            _board.EnableAutoResync();
            CombatSystem.AdjacentResolver = _board.FlankNeighbors; // 碾压=左右同排生物（2026-10-04 语义修订）
            GameBoard.LinkAuraSystem.Attach(_board); // 连接光环（三轨制）：箭头指向格占据者享受 linkAuras
        }

        /// <summary>
        /// 棋盘接线归零（退出对局时调用，对齐 NetRoom.TeardownMatch 惯例）：
        /// 静态扩展点不清理会指向已过期的占用层——同进程后续开验证器/headless 局时
        /// 碾压/光环会按旧棋盘结算（BattlefieldVerifier S5 生命周期断言锁定此口径）。
        /// </summary>
        public void Shutdown()
        {
            DetachBoard();
            // 教学态静态接线还原（AiResponder 是全局委托——不还原会泄漏到后续普通局：机器人永不让守卫）
            if (_passiveResponderOn)
            {
                _passiveResponderOn = false;
                ResponseWindowService.AiResponder = _prevAiResponder;
                _prevAiResponder = null;
            }
        }

        /// <summary>教学局开关：机器人响应窗口全让过（永不反制/拦截玩家操作——教学必需）。
        /// AiResponder 返回 null 会回落内置守卫启发，故走 PassOption 哨兵；Shutdown 恢复前值。</summary>
        public void EnablePassiveAiResponder()
        {
            if (_passiveResponderOn) return;
            _passiveResponderOn = true;
            _prevAiResponder = ResponseWindowService.AiResponder;
            ResponseWindowService.AiResponder = (core, holder, options) => ResponseWindowService.PassOption;
        }

        private void DetachBoard()
        {
            _board?.Dispose();
            _board = null;
            CombatSystem.AdjacentResolver = null;
            GameBoard.LinkAuraSystem.Detach();
        }

        // ======================================== 攻击与响应窗口（2026-09-16 逐攻击开窗） ========================================

        /// <summary>玩家声明一次攻击（速度0上栈开响应窗口）——窗口/结算由调用方接 SettleResponseWindowAsync。</summary>
        public bool DeclareAttack(Player attacker, Entity attackerUnit, Entity target)
        {
            return GameActions.DeclareAttack(Core, attacker, attackerUnit, target);
        }

        /// <summary>响应窗口泵（人类候选→弹窗由 HumanResponder 承担；AI→守卫启发；无候选→自动双 Pass）。</summary>
        public UniTask SettleResponseWindow()
            => GameActions.SettleResponseWindowAsync(Core);

        // ======================================== AI 回合 ========================================

        /// <summary>把 P2 的整个回合交给极简脚本 AI 执行（异步：战斗窗口处可暂停等人类响应弹窗）。</summary>
        public async UniTask RunAiTurnAsync()
        {
            await _ai.TakeTurnAsync(this);
        }
    }
}
