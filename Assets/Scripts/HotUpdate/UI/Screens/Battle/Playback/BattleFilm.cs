using System;
using System.Collections.Generic;
using CardCore;

namespace SynergyUI
{
    /// <summary>帧种类（2026-11-11 战斗演出统一重构 S1·方案 §2.1）：Step=栈对象结算步；
    /// View=纯局面帧（回放帧 #0 开局 / 联机快照帧）；Input=玩家决定（v1 只记录展示）；
    /// RoundEnd=结算轮收尾；End=终局。</summary>
    public enum BattleFrameKind { Step, View, Input, RoundEnd, End }

    /// <summary>剧本帧：局面快照 + 战报行 + 演出指令（纯数据，可录制可跨端回放）。
    /// 三种生产者（LocalFilmSource/NetFilmSource/ReplayFilmSource）产出同一种帧，
    /// 由唯一播放器 BattlePlayhead 按帧解释（方案 §2）。
    /// Step 帧填 Kind/Label/CardRid/IsAttack/Remaining/View/LastEventId；
    /// Lines/PreOps/Ops/Combat 自 S2 演出指令数据化起填（二期 A 起本地实时同填）；Input/End 自 S2/S4 起用。</summary>
    public sealed class BattleFrame
    {
        public BattleFrameKind Kind;
        public string Label;          // 步标签：卡名 / 攻击宣言 / 守卫宣言 / 状态动作 / 阶段窗口
        public uint CardRid;          // 卡牌步的 RuntimeId（0=非卡步）
        public bool IsAttack;         // 攻击步（战斗演出驱动位）
        public int Remaining;         // 步后余栈（日志证据链用）
        public BattleViewData View;   // 本帧要应用的局面（可空：联机事件帧常常没有快照）
        public uint LastEventId;      // 建帧时最后一条日志事件的 EventId（S2 行→帧映射：第一个 LastEventId >= line.EventId 的帧）
        public List<BattleLogLine> Lines = new();   // 本帧放行的战报行（二期 A 起建帧期填充——LiveData 随帧入列/回放时间轴预铺）
        public List<BattleFxOp> PreOps = new();     // 应用 View 之前（死亡预持等）
        public List<BattleFxOp> Ops = new();        // 应用 View 之后（伤害/回复/拼点/飞行/浮字…）
        public CombatShot Combat;     // 攻击步的战斗演出（可空）
        public BattleInputRecord Input; // Input 帧载荷（v1 只记录/展示）
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

    // ==== 演出指令数据化（2026-11-11 S2·方案 §2.2：闭包→数据，可录制可跨端回放） ====

    /// <summary>演出指令种类（与投影器各演出钩子一一对应；DrawFlyIn v1 未录制=已知限制，视图对账覆盖）。</summary>
    public enum BattleFxKind
    {
        Damage,        // 效果伤害（AtomicDamageEvent·非战斗）
        Heal,          // 回复（HealEvent·扣溢后实效）
        Flight,        // 效果飞行（EffectFlightEvent——效果无效/不适用，发动照播）
        Death,         // 效果死亡演出（碎裂+消灭族飞行）
        DeathHold,     // 效果死亡预持（PreOps——防对账抢先销毁尸体）
        Arrival,       // 入场粒子汇聚（CastPlayed）
        Paid,          // 法术扣费粒子（打出实付→施放者角色格）
        Fatigue,       // 疲劳受击（红闪脉冲+浮字）
        CounterFloat,  // 属性指示物浮字（白名单在执行侧过滤）
        CounterBurst,  // 赋予指示物爆发粒子
        KeywordBurst,  // 赋予关键词爆发粒子
        DeckMark,      // 诅咒/祝福牌库标记
        Clash,         // 拼点结果浮字
        LuckRoll,      // 运势掷骰浮字
        PhaseAnnounce, // 阶段播报（HUD 阶段格）
        DrawFlyIn,     // 抽卡飞入（v1 未录制——轮外事件，视图对账覆盖；已知限制）
    }

    /// <summary>视图空间引用（不持有引擎对象）：单位=Rid（0=无效）；角色=IsPlayer+Seat。
    /// Name/CardId 供日志/兜底展示；捕获时从引擎实体解析，回放按 Rid/Seat 查场上单位/立柱。</summary>
    public sealed class FxRef
    {
        public uint Rid;
        public int Seat = -1;
        public string CardId;
        public bool IsPlayer;
        public string Name;

        /// <summary>引擎实体 → 视图引用（捕获时求值——回放期实体已亡）：卡记 Rid/CardId/名+Controller
        /// 座位（非场上来源=施法者立柱锚回放归属；场上单位锚优先走 Rid 查 _units，Seat 备而不用）；
        /// 角色记 IsPlayer/Seat；null 实体返回 null（调用方跳过该引用）。</summary>
        public static FxRef Of(Entity e, Func<Player, int> seatOf)
        {
            if (e is Player p)
                return new FxRef { IsPlayer = true, Seat = seatOf != null ? seatOf(p) : -1, Name = p.Name };
            if (e is Card c)
            {
                var ctrl = c.GetController();
                return new FxRef
                {
                    Rid = c.RuntimeId,
                    CardId = c.ID,
                    Name = MatchLogRenderer.Name(c),
                    Seat = ctrl != null && seatOf != null ? seatOf(ctrl) : -1,
                };
            }
            return null;
        }
    }

    /// <summary>演出指令（数据而非闭包，方案 §2.2）：投影器/屏侧各演出钩子在捕获时构造
    /// （双轨录制——闭包照旧执行），挂到当前步帧的 PreOps/Ops；回放由 BattleFxOpExecutor 按帧解释。</summary>
    public sealed class BattleFxOp
    {
        public BattleFxKind Kind;
        public FxRef Source, Target;
        public int Amount;
        public int Tint = -1;                 // ManaType，-1=无（捕获时已解析好颜色）
        public int CastX = -1, CastZ = -1;    // 法术位（-1=无）
        public int Reason;                    // DestroyReason / GameOverReason / PhaseType 等
        public bool Flag;                     // Success / blessing / debuff / IsAdd
        public int D1, D2, Threshold;         // 拼点=双方战力 / 运势=骰值
        public int Seat = -1;                 // 疲劳/抽牌/阶段播报归属
        public string Text;                   // counterType / 备注文案
        public Dictionary<int, float> Paid;   // 入场/扣费粒子实付账单（色→量；可空）

        /// <summary>演出色（捕获时写入 Tint 的 ManaType 还原；-1=null 无基准）。</summary>
        public ManaType? TintOf => Tint >= 0 ? (ManaType?)Tint : null;
    }

    /// <summary>攻击步战斗演出载荷（方案 §2.2 CombatShot）：编舞器攻击步捕获时写入帧（本地实时），
    /// 回放按 shot 播「攻击者飞向目标→命中/受击→伤害浮字→死亡退场」；守卫闪现/反击/溅射的
    /// 精细走位 v1 不还原（已知差异——战报行照常）。</summary>
    public sealed class CombatShot
    {
        public FxRef Attacker, Target, Blocker;   // Blocker=守卫拦截者（可空——v1 只记不改演）
        public int TargetX, TargetZ;              // 攻击临格参照（目标格）
        public List<CombatHit> Hits = new();      // 逐笔命中（含反击）
    }

    /// <summary>单笔战斗命中：Target/Amount 必有；Lethal v1 恒 false（死亡归 Death 指令/视图）；
    /// IsCounter=该笔是守方反击（Attacker==原目标）。</summary>
    public sealed class CombatHit
    {
        public FxRef Target;
        public int Amount;
        public bool Lethal;
        public bool IsCounter;
    }

    /// <summary>Input 帧载荷（v1 只记录/展示）：Kind 0=Intent 1=Select 2=Text 3=Response；
    /// Text=玩家选了什么（卡名/目标/让过…）。回放不弹窗不等待。</summary>
    public sealed class BattleInputRecord
    {
        public int Kind;
        public int Seat;
        public string Text;
    }
}
