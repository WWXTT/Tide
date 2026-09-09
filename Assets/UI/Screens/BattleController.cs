using CardCore;

namespace SynergyUI
{
    /// <summary>
    /// 对战编排器：负责组卡 / 初始化 GameCore、每帧驱动结算栈、补齐战斗结算链
    /// （引擎里 CombatSystem.StartCombat 无人调用，此处在 UI 层按顺序补齐），以及驱动 AI 回合。
    /// 不改任何 CardCore 引擎逻辑，仅按正确顺序调用既有 API。
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
        public bool InCombat => Core != null && Core.CombatSystem.InCombat;

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

        // ======================================== 战斗结算链（补缺口） ========================================

        /// <summary>进入战斗：StartCombat 当前在引擎里无人调用，是必补点。</summary>
        public void BeginCombat()
        {
            var tp = TurnPlayer;
            if (tp == null) return;
            Core.CombatSystem.StartCombat(tp, tp.Opponent);
        }

        /// <summary>玩家声明一次攻击（GameActions 内部已校验 CanCombatAction + CanDeclareAttack）。</summary>
        public bool DeclareAttack(Player attacker, Entity attackerUnit, Entity target)
        {
            return GameActions.DeclareAttack(Core, attacker, attackerUnit, target);
        }

        /// <summary>
        /// 结束战斗：先 EndAttackDeclaration（无攻击者则直接 EndCombat），
        /// 若仍在战斗中（进入 SelectBlocker）则 EndBlockDeclaration 触发伤害结算 → EndCombat。
        /// AI 不阻挡，故直接结算。
        /// </summary>
        public void ResolveCombat()
        {
            var combat = Core.CombatSystem;
            if (!combat.InCombat) return;
            combat.EndAttackDeclaration();
            if (combat.InCombat)
                combat.EndBlockDeclaration();
        }

        // ======================================== AI 回合 ========================================

        /// <summary>把 P2 的整个回合交给极简脚本 AI 执行（顺序执行，末尾由调用方刷新界面）。</summary>
        public void RunAiTurn()
        {
            _ai.TakeTurn(this);
        }
    }
}
