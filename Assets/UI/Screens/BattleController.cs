using System.Collections.Generic;
using System.Threading.Tasks;
using CardCore;
using Cysharp.Threading.Tasks;

namespace SynergyUI
{
    /// <summary>
    /// 对战编排器：负责组卡 / 初始化 GameCore、棋盘接线，以及驱动 AI 回合。
    /// 2026-09-16 战斗接入栈机器：攻击=速度0栈对象逐攻击开窗（引擎内闭环），
    /// 旧"UI 层补齐战斗结算链"（BeginCombat/ResolveCombat）退役；
    /// 响应窗口（发动弹窗）经 GameActions.SettleResponseWindowAsync 驱动，人类弹窗在此注册。
    /// </summary>
    public sealed class BattleController
    {
        private readonly SimpleAI _ai = new SimpleAI();

        // 棋盘占用层（派生，单向读核心区域）：为碾压关键词提供邻接解析
        private GameBoard.BoardState _board;

        public GameCore Core => GameCore.Instance;
        public Player P1 => Core?.Player1;
        public Player P2 => Core?.Player2;
        public Player TurnPlayer => Core?.TurnEngine?.TurnPlayer;
        public bool IsPlayerTurn => TurnPlayer != null && TurnPlayer == P1;

        /// <summary>组双方卡组并初始化对局（卡组不重复：每种 1 张；起手含仪式占位、StartGame 发 GameStartEvent + 开 P1 回合）。</summary>
        public void StartNewGame()
        {
            var catalog = CardCatalog.LoadAll();
            // 变形目标形态解析器：组合根注入（CardCore 不依赖 UI 层）
            CardCore.Attribute.MorphSystem.ResolveMorphTarget = CardCatalog.GetById;
            // 同一张卡表给双方各建一副（卡组不重复，每种 1 张——构筑规则定案），P2 由极简 AI 操作。
            GameCore.Instance.InitGame(catalog, catalog);
            // 标记 P2 为 AI：目标选择器对 AI 跳过弹窗、即时自动选择。
            if (GameCore.Instance.Player2 != null)
                GameCore.Instance.Player2.IsAI = true;
            AttachBoard();
        }

        /// <summary>棋盘占用层接线：注入碾压 AdjacentResolver + 连接光环 LinkAuraSystem（核心不绑棋盘，由宿主组装）。</summary>
        private void AttachBoard()
        {
            _board?.Dispose();
            GameBoard.LinkAuraSystem.Detach(); // 上一局的接线归零（静态扩展点惯例）
            var core = GameCore.Instance;
            if (core?.Player1 == null || core.Player2 == null) return;
            _board = new GameBoard.BoardState(core, core.Player1, core.Player2,
                GameBoard.HalfFieldData.Flat(), GameBoard.HalfFieldData.Flat());
            _board.EnableAutoResync();
            CombatSystem.AdjacentResolver = _board.Neighbors;
            GameBoard.LinkAuraSystem.Attach(_board); // 连接光环（三轨制）：箭头指向格占据者享受 linkAuras
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
