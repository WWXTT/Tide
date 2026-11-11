using System.Collections.Generic;

namespace SynergyUI
{
    /// <summary>帧种类（2026-11-11 战斗演出统一重构 S1·方案 §2.1）：Step=栈对象结算步；
    /// View=纯局面帧（回放帧 #0 开局 / 联机快照帧）；Input=玩家决定（v1 只记录展示）；
    /// RoundEnd=结算轮收尾；End=终局。</summary>
    public enum BattleFrameKind { Step, View, Input, RoundEnd, End }

    /// <summary>剧本帧：局面快照 + 战报行 + 演出指令（纯数据，可录制可跨端回放）。
    /// 三种生产者（LocalFilmSource/NetFilmSource/ReplayFilmSource）产出同一种帧，
    /// 由唯一播放器 BattlePlayhead 按帧解释（方案 §2）。
    /// S1 本地 Live 只填 Step 帧的 Kind/Label/CardRid/IsAttack/Remaining/View/LogBoundary/LastEventId；
    /// Lines/PreOps/Ops/Combat 自 S2 演出指令数据化起填；Input/End 自 S2/S4 起用。</summary>
    public sealed class BattleFrame
    {
        public BattleFrameKind Kind;
        public string Label;          // 步标签：卡名 / 攻击宣言 / 守卫宣言 / 状态动作 / 阶段窗口
        public uint CardRid;          // 卡牌步的 RuntimeId（0=非卡步）
        public bool IsAttack;         // 攻击步（战斗演出驱动位）
        public int Remaining;         // 步后余栈（日志证据链用）
        public BattleViewData View;   // 本帧要应用的局面（可空：联机事件帧常常没有快照）
        public uint LastEventId;      // 建帧时最后一条日志事件的 EventId（S2 行→帧映射：第一个 LastEventId >= line.EventId 的帧）
        public List<BattleLogLine> Lines = new();   // S2 起：本帧放行的战报行（Live 旧路径仍走 Hold/ReleaseUpTo）
        public List<BattleFxOp> PreOps = new();     // S2 起：应用 View 之前（死亡预持等）
        public List<BattleFxOp> Ops = new();        // S2 起：应用 View 之后（伤害/回复/拼点/飞行/浮字…）
        public CombatShot Combat;     // S2 起：攻击步的战斗演出（可空）
        public BattleInputRecord Input; // Input 帧载荷（v1 只记录/展示）
        public int LogBoundary;       // 过渡字段（S1/S2 本地 Live 沿用日志 Hold 水位=MatchLogService.Entries.Count；S5 Hold/ReleaseUpTo 退役后删除）
    }

    /// <summary>战报行（帧内）（2026-11-11 方案 §2.1·对接 C 案行流）：Kind/Flags 沿用
    /// CardCore.MatchLogLineKind/MatchLogLineFlags 的 1B 编码；Text=当前视角文本
    /// （对战=遮蔽版；回放=无遮蔽版）、FullText=无遮蔽版（S2 本地=同 Text；联机 S4=终局全量行按 EventId 回填）。</summary>
    public sealed class BattleLogLine
    {
        public uint EventId;      // 与 MatchLogLine/NetLogLine 对齐（遮蔽版↔全量版合并、行↔帧映射）
        public int FrameIndex;   // 归属帧（时间轴跳转目标；-1=帧前历史如开局块）
        public string Text;      // 当前视角文本
        public string FullText;  // 无遮蔽版
        public byte Kind;        // MatchLogLineKind（C 案 1B 色编码）
        public byte Flags;       // MatchLogLineFlags（ChainStart/ChainEnd/GameOver/TurnStart/FirstDraw）
        public string Class;     // log-line--*（UI 直用；由 Kind 反查）
    }

    /// <summary>剧本头（录制元信息）：回放装台与摘要窗展示用（S2 起 BattleFilmStore 落盘）。</summary>
    public sealed class BattleFilmHeader
    {
        public int Version;        // 格式版本，从 1 起
        public string MatchId;
        public int Mode;           // BattleMode（参考用）
        public int ViewerSeat;     // 录制者视角（本地局=0）
        public string SelfName, OppName;
        public int Seed;
        public long CreatedAtUnix;
    }

    /// <summary>战斗剧本：Header + 帧序列。帧 #0 恒为 View（开局局面）——回放先把棋盘/角色/手牌
    /// 落到开局态再播第一步（S2 LocalFilmSource 在 GameStartEvent 补帧；联机 S4=首快照）。</summary>
    public sealed class BattleFilm
    {
        public BattleFilmHeader Header;
        public List<BattleFrame> Frames = new();
    }

    // ==== S2 演出指令数据化占位（方案 §2.2 本阶段空壳，S2 补 BattleFxKind/FxRef/BattleFxOp 全字段/CombatShot/CombatHit） ====

    /// <summary>演出指令（数据而非闭包）：S1 空壳占位——帧模型先立形状，S2 由投影器各事件钩子
    /// 在捕获时构造、回放执行器按帧解释。</summary>
    public sealed class BattleFxOp { }

    /// <summary>攻击步战斗演出载荷：S1 空壳占位——S2 编舞器捕获时写入攻击帧，回放按 shot 播
    /// 「飞行→命中→伤害浮字→死亡退场」。</summary>
    public sealed class CombatShot { }

    /// <summary>Input 帧载荷（v1 只记录/展示）：Kind 0=Intent 1=Select 2=Text 3=Response；
    /// Text=玩家选了什么（卡名/目标/让过…）。回放不弹窗不等待。</summary>
    public sealed class BattleInputRecord
    {
        public int Kind;
        public int Seat;
        public string Text;
    }
}
