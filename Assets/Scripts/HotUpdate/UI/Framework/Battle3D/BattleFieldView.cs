using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using Cysharp.Threading.Tasks;
using GameBoard;
using MonsterRig;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SynergyUI
{
    /// <summary>
    /// 战场单位摆位视图（2026-10-13 战场重做·原型）：消费 BattleViewData（渲染唯一输入，
    /// 本地/网络同轨）做幂等全量对账（键=引擎卡 RuntimeId，手牌扇同口径）。
    ///
    /// 摆位形态：单位区卡（生物/结界/衍生物）→ MonsterRig 模型站格
    /// （结构=wrapper[位姿/头顶数值] → 模型根[MonsterRigAnimator]——wrapper 承载位移，
    /// 不触发 Recapture）；死亡→倒地散落、复活→Revive；横置→仅脚下光波熄灭表达
    ///（2026-10-15 定案：呼吸不随横置停止，可行动性只看 BattleTileFx 光波）。
    /// 地牌→无实体（2026-10-13 卡背退役定案；2026-10-14 按色叠格）：同色叠一格——格面元素图标+
    /// 「可用/上限」计数条（可用=未横置+钱包该色；上限=红蓝绿灰该色已放入地牌张数、黑白=地牌槽上限），
    /// 可用&gt;0 点亮锁色菲涅尔（BattleTileFx）；
    /// 黑白格计数=钱包余额/生成上限；技能格=技能名数字条（无技能空格）（均 BattleFieldView 对账）。
    /// 可行动性（生物未横置）与地牌状态均由脚下地块表达（BattleTileFx 同日改版）。
    /// 角色→占位立柱+生命值（2026-10-16：常态只显 HP，护甲缀「(+x)」、获得攻击力追加「Ac:x」；
    /// 结界等无生命单位头顶只显耐久值）；模型映射走 MonsterModelResolver（同名→稳定哈希→占位胶囊）。
    /// 体型=已按模块数分档烘进资产几何（MonsterRigSizeScaler：&lt;5块→0.7/5~10块→1/&gt;10块→1.5，
    /// 2026-10-14 用户定案取代 10-13 全 1+属性放大）；运行时不再缩放，头顶数值按实际包围盒顶。
    ///
    /// 生命周期：BattleScreen.OnEnter 创建、OnExit Dispose。棋盘未就绪
    /// （BattleBoardGrid.Ready=false）跳过本次对账（节流日志），装台后下一事件即补刷。
    /// 数据层证据：每次对账输出统计日志（新建/移动/销毁计数）。
    /// </summary>
    public sealed class BattleFieldView
    {
        private readonly Transform _root;
        private readonly Dictionary<uint, UnitVisual> _units = new Dictionary<uint, UnitVisual>();
        private readonly Dictionary<int, HeroVisual> _heroes = new Dictionary<int, HeroVisual>(); // key=归属 0/1
        private readonly HashSet<int> _fatigueBusy = new HashSet<int>(); // 疲劳演出在途 owner（重入只补浮字，2026-10-17）
        private readonly Dictionary<long, TextMeshPro> _landCounts = new Dictionary<long, TextMeshPro>(); // key=格(x<<32|z)：地牌格「可用/上限」计数
        private readonly HashSet<uint> _heldUnits = new HashSet<uint>(); // 演出持有（2026-10-13 战斗编舞）
        private readonly List<GameObject> _creatureArrows = new List<GameObject>(); // 生物箭头地面件（钉 tile，全量对账重建）
        private int _lastSkipLogFrame = -1;

        //（2026-10-15 深夜改版）单位【发动效果】头顶按钮退役：单位本体退出射线检测，
        // 攻击/守卫/发动拆到格前三键（BattleTileButtons），本类不再持有可点 3D 件。

        // 施放占格类型图标（2026-10-15 发动格拆除：整个 2×6 兼任发动区；2026-10-17 两改：
        // 立绘立牌→程序描边边框→Element 预制体平贴图标）：Element.prefab 面朝上平躺贴格面，
        // 图标=三张卡类型图（结界 barrier / 生物 biology / 法术 spell·装备兜底，纯白不染色）——
        // 材质以 BattleCardBack_Elem_White_Mat（URP Unlit 透明·纯白）为模板运行时实例换贴图；
        // 完整卡面同帧由 BattleScreen 左上角施放展示（cast-stage）呈现。图标补挂 MeshCollider
        //（Element 无碰撞体）保 cast-{RuntimeId} 射线路由（hover 卡信息/点击开发动区列卡）。
        private readonly Dictionary<uint, GameObject> _castings = new Dictionary<uint, GameObject>(); // key=RuntimeId
        private readonly Dictionary<uint, GameObject> _castingLingers = new Dictionary<uint, GameObject>(); // 离区 linger 期图标（key=RuntimeId；登场演出/等待期窗口，到期或被顶即销）
        private readonly Dictionary<uint, Vector2Int> _castingCells = new Dictionary<uint, Vector2Int>(); // 施放图标占格（key=RuntimeId；随对账同进出，TryGetOnBoardCell 查询用）
        private GameObject _castIconPrefab; // Element 预制体（HotUpdateAssets 惰性加载，地牌元素图标同源）
        private readonly Dictionary<Cardtype, Material> _castTypeMats = new Dictionary<Cardtype, Material>(); // 类型→图标材质（运行时实例，随 Dispose 销毁）

        // 出场粒子汇聚载荷（2026-10-15 用户定案：弃弹出动画改 INab 粒子）：CastPlayed 入场 →
        // BattleCastArrival 粒子演出 1s 后显现模型；paid 键=ManaType 下标、值=该色实付量
        private readonly Dictionary<uint, (int seat, Dictionary<int, float> paid)> _arrivals =
            new Dictionary<uint, (int, Dictionary<int, float>)>();

        /// <summary>施放占格节点名前缀（BattleScreen.PollFieldPointer 射线命中路由——cast-{RuntimeId}）。</summary>
        public const string CastingCardPrefix = "cast-";

        private static readonly Color P1UnitColor = new Color(0.29f, 0.50f, 0.83f);
        private static readonly Color P2UnitColor = new Color(0.83f, 0.35f, 0.35f);

        // 死亡退场（2026-10-17 定案：砍掉滚墓与墓上自旋两段，退场不再去墓地格）——
        // 生物=碎裂散落原地停 DeathRestSeconds 沉降即收；无 rig 单位（结界等）暂无碎裂，
        // 整身原地沉降入板面（深度=模型顶高+余量）
        private const float DeathRestSeconds = 0.5f; // 碎裂落地后的沉降等待
        private const float DeathSinkSeconds = 0.5f; // 无 rig 单位整身下沉时长

        // 属性配色（2026-10-15）：与印刷基础值持平=白（TMP 默认色）、低于=红（受伤/降攻）、
        // 高于=绿（增益）——攻击/生命逐数字独立着色（富文本），查表 miss（无印刷值）恒白。
        private static readonly Color StatDownColor = new Color(1f, 0.38f, 0.32f);
        private static readonly Color StatUpColor = new Color(0.42f, 1f, 0.45f);

        private static string FormatStats(int power, int life, int? basePower, int? baseLife)
            => StatSpan(power, basePower) + "/" + StatSpan(life, baseLife);

        private static string StatSpan(int value, int? baseValue)
        {
            if (!baseValue.HasValue) return value.ToString();
            if (value < baseValue.Value)
                return $"<color=#{ColorUtility.ToHtmlStringRGB(StatDownColor)}>{value}</color>";
            if (value > baseValue.Value)
                return $"<color=#{ColorUtility.ToHtmlStringRGB(StatUpColor)}>{value}</color>";
            return value.ToString();
        }

        /// <summary>护甲后缀（2026-10-16）：护甲指示物 &gt;0 时以「(+x)」缀于生命/耐久之后（角色/单位同式）。</summary>
        private static string ArmorSuffix(int armor) => armor > 0 ? $"(+{armor})" : "";

        public BattleFieldView()
        {
            _root = new GameObject("BattleFieldView").transform;
        }

        public void Dispose()
        {
            if (_heldUnits.Count > 0)
                Debug.LogWarning($"[BattleField] Dispose 时仍有 {_heldUnits.Count} 个演出 hold 未释放（正常应在编舞 finally 清空）——随 Dispose 一并丢弃");
            _heldUnits.Clear();
            foreach (var a in _creatureArrows)
                if (a != null)
                {
                    if (Application.isPlaying) Object.Destroy(a);
                    else Object.DestroyImmediate(a);
                }
            _creatureArrows.Clear(); // 钉在 tile 上不随 _root 销毁，须自清
            _units.Clear();
            _heroes.Clear();
            foreach (var l in _landCounts.Values)
                if (l != null)
                {
                    if (Application.isPlaying) Object.Destroy(l.gameObject);
                    else Object.DestroyImmediate(l.gameObject);
                }
            _landCounts.Clear(); // 2026-10-17 计数条钉在 tile 上不随 _root 销毁，须自清（同 _creatureArrows）
            _castings.Clear(); // 类型图标是 _root 子物体随根销毁；材质实例（CastType_*/CastFallback_*）逐类自清
            _castingLingers.Clear(); // linger 期图标随根销毁（延迟协程对已销毁对象 fake-null 判死退出）
            _castingCells.Clear();
            foreach (var m in _castTypeMats.Values)
                if (m != null)
                {
                    if (Application.isPlaying) Object.Destroy(m);
                    else Object.DestroyImmediate(m);
                }
            _castTypeMats.Clear(); // 运行时实例材质不跨局（跨域 fake-null 防口）
            _arrivals.Clear(); // 未消费的入场载荷不跨局
            if (_root != null) Object.Destroy(_root.gameObject);
        }

        /// <summary>幂等全量对账；返回是否实际应用（数据空/棋盘未就绪=false）。</summary>
        public bool Apply(BattleViewData d)
        {
            if (d == null) return false;
            if (!BattleBoardGrid.Ready)
            {
                if (Time.frameCount - _lastSkipLogFrame > 120)
                {
                    _lastSkipLogFrame = Time.frameCount;
                    Debug.Log("[BattleField] 棋盘未就绪（BattleBoardGrid 未装台），本次对账跳过");
                }
                return false;
            }

            // 收进 Map 总根（2026-10-14 晚定案）：运行时临时对象不堆积场景根目录；
            // ctor 早于装台时首帧对账（棋盘就绪=Map 已存在）一次性重挂。
            if (_root != null && _root.parent == null && BattleStageDirector.MapRoot != null)
                _root.SetParent(BattleStageDirector.MapRoot, true);

            // ---- 地块特效（全熄后随对账按数据重点亮；菲涅尔壳留存复用） ----
            BattleTileFx.ClearAll();

            // ---- 优先权（扳机）指示（2026-10-15 半场逐格泛光退役——与地格玩法高光共用壳互相覆盖）：
            //      战场底部两根长方条随扳机切换亮暗（左=己方蓝/右=对手红）；中线行时间格同步切高亮 ----
            int holderSeat = PrioritySeatOf(d);
            BattlePriorityGlow.Apply(holderSeat);
            BattleHudRow.SetTimePriority(holderSeat);

            // ---- 单位区（生物/结界/衍生物） ----
            var seenUnits = new HashSet<uint>();
            int createdU = 0, movedU = 0, removedU = 0;
            string lastModel = "";
            foreach (var card in d.SelfUnits) ReconcileUnit(card, owner: 0, seenUnits, ref createdU, ref movedU, ref lastModel);
            foreach (var card in d.OppUnits) ReconcileUnit(card, owner: 1, seenUnits, ref createdU, ref movedU, ref lastModel);
            removedU = RemoveStale(_units, seenUnits, _heldUnits);

            // ---- 生物箭头（2026-10-15 用户定案：钉地格不跟 wrapper——攻击/守卫跑动留在原地） ----
            ReconcileCreatureArrows(d);

            // ---- 施放占格类型图标（2026-10-15 发动格拆除：发动区占位卡 × 单位区空格） ----
            ReconcileCasting(d.CastingCards);

            // ---- 地牌（固定元素格；2026-10-14 晚定案：6 格固定元素身份 灰红蓝绿黑白） ----
            ReconcileLands(d.SelfLands, d.Self, 0);
            ReconcileLands(d.OppLands, d.Opp, 1);

            // ---- 角色立柱 ----
            ReconcileHero(d.Self, 0);
            ReconcileHero(d.Opp, 1);

            // ---- 技能格（2026-10-16 撤销）：英雄技能不再占格——介绍=角色格悬停、发动=角色格三键 ----

            // ---- 特殊格道具对账（卡组卡堆高度随牌库数 0..60 钳制；墓地/除外漩涡常驻自转） ----
            BattleZoneProps.SetDeckCount(0, d.Self.DeckCount);
            BattleZoneProps.SetDeckCount(1, d.Opp.DeckCount);

            Debug.Log($"[BattleField] 对账：单位 己{d.SelfUnits.Count}/敌{d.OppUnits.Count}"
                      + $"（新建{createdU} 移动{movedU} 销毁{removedU}{(createdU > 0 ? " 最近模型=" + lastModel : "")}）"
                      + $" 地牌 己{d.SelfLands.Count}/敌{d.OppLands.Count}"
                      + $" 地块高亮{BattleTileFx.GlowOn} 元素图标{BattleTileFx.IconCount} 生物箭头{_creatureArrows.Count}"
                      + $" 施放类型图标{d.CastingCards.Count}"
                      + $" 牌库 己{d.Self.DeckCount}/敌{d.Opp.DeckCount}"
                      + $" 生命 己{d.Self.Life}/敌{d.Opp.Life}"
                      + $" 地牌上限 己{d.Self.LandCap}/敌{d.Opp.LandCap}"
                      + $" 钱包(白黑) 己{d.Self.WalletByColor[(int)ManaType.White]}/{d.Self.WalletByColor[(int)ManaType.Black]}"
                      + $" 敌{d.Opp.WalletByColor[(int)ManaType.White]}/{d.Opp.WalletByColor[(int)ManaType.Black]}");
            return true;
        }

        /// <summary>当前优先权（扳机）持有方（观察者视角 seat：0=己方 1=对手）：栈非空=优先权
        /// 持有方（MyPriority 观察者相对口径），栈空=回合方——对账数据单源推导，本地/网络同式。</summary>
        private static int PrioritySeatOf(BattleViewData d)
            => d.StackNotEmpty ? (d.MyPriority ? 0 : 1) : (d.MyTurn ? 0 : 1);

        // ======================================== 单位 ========================================

        private sealed class UnitVisual
        {
            public Transform Wrapper;
            public GameObject Model;
            public MonsterRigAnimator Anim;   // 占位胶囊时 null
            public TextMeshPro Stats;
            public int X, Z;
            public bool Dead;
            public int Owner;                 // 归属 0/1（头注视敌我分辨）
            public float ModelYaw;            // 模型层对敌本地偏航（2026-10-15 定案：朝向归模型层；结界=箭头净朝向 180/0）
            public bool IsCreature = true;    // 生物才作注视目标（结界三件模型不参与对视）
            public float LookHeight = 0.6f;   // 注视点高度（包围盒胸口位估算）
            public Func<Vector3?> LookPin;    // 战斗注视钉（编舞器）：非空优先于待机注视

            // —— 头顶数值节拍（2026-10-15 定案：攻击结算同帧改血，展示等命中时刻）——
            // Shown=当前展示值（滚动起点/逐帧推进）；Target=对账目标值（冻结期只缓冲）；
            // StatsVersion=滚动作废令牌（新目标即作废在途滚动，从当前展示值续走）
            public int ShownPower = int.MinValue, ShownLife = int.MinValue;
            public int TargetPower, TargetLife;
            public int ShownArmor = int.MinValue, TargetArmor; // 护甲同步走节拍（2026-10-16：吸收与命中同拍滚动）
            public int? BasePower, BaseLife;
            public int StatsVersion;
            public bool StatsHeld;            // 战斗演出数值冻结：命中节拍释放并滚动落值
            public bool DurabilityOnly;       // 无生命单位（结界，2026-10-16）：头顶只显耐久值，不显 攻/血 对

            /// <summary>对账写入口：冻结期只更新目标值；否则值变化滚动过渡（0.4s 缓出，逐格推进）。</summary>
            public void SetStatsValues(int power, int life, int armor, int? basePower, int? baseLife)
            {
                TargetPower = power; TargetLife = life; TargetArmor = armor; BasePower = basePower; BaseLife = baseLife;
                if (StatsHeld) return;
                if (ShownPower == int.MinValue) // 首写直落（无滚动起点）
                {
                    ShownPower = power; ShownLife = life; ShownArmor = armor;
                    WriteStats(power, life, armor);
                    return;
                }
                if (power == ShownPower && life == ShownLife && armor == ShownArmor)
                {
                    WriteStats(power, life, armor);
                    return;
                }
                StatsVersion++;
                RollStatsAsync().Forget();
            }

            /// <summary>命中节拍释放：缓冲目标值滚动落地（编舞器调）。</summary>
            public void ReleaseStatsHold()
            {
                if (!StatsHeld) return;
                StatsHeld = false;
                if (TargetPower == ShownPower && TargetLife == ShownLife && TargetArmor == ShownArmor) return;
                StatsVersion++;
                RollStatsAsync().Forget();
            }

            private async UniTaskVoid RollStatsAsync()
            {
                int ver = StatsVersion;
                int fromP = ShownPower, fromL = ShownLife, fromA = ShownArmor;
                const float dur = 0.4f;
                float t0 = Time.realtimeSinceStartup;
                while (Stats != null && StatsVersion == ver)
                {
                    float t = Mathf.Clamp01((Time.realtimeSinceStartup - t0) / dur);
                    float e = 1f - (1f - t) * (1f - t); // 缓出：命中落值后减速收尾
                    ShownPower = Mathf.RoundToInt(Mathf.Lerp(fromP, TargetPower, e));
                    ShownLife = Mathf.RoundToInt(Mathf.Lerp(fromL, TargetLife, e));
                    ShownArmor = Mathf.RoundToInt(Mathf.Lerp(fromA, TargetArmor, e));
                    WriteStats(ShownPower, ShownLife, ShownArmor);
                    if (t >= 1f) return;
                    await UniTask.Yield();
                }
            }

            private void WriteStats(int p, int l, int a)
            {
                if (Stats == null) return;
                // 无生命单位（结界）：Life 即耐久，只显耐久值（2026-10-16）；生物照旧 攻/血 对
                Stats.text = DurabilityOnly
                    ? StatSpan(l, BaseLife) + ArmorSuffix(a)
                    : FormatStats(p, l, BasePower, BaseLife) + ArmorSuffix(a);
            }

            /// <summary>头顶数值显隐：死亡碎裂同帧即隐，复活由对账恢复（2026-10-16）。
            /// 隐期间 WriteStats 照常写 text（组件在失活 GO 上仍可写），恢复显示即最新值。</summary>
            public void SetStatsVisible(bool on)
            {
                if (Stats != null && Stats.gameObject.activeSelf != on)
                    Stats.gameObject.SetActive(on);
            }
        }

        private void ReconcileUnit(BattleCardView card, int owner,
            HashSet<uint> seen, ref int created, ref int moved, ref string lastModel)
        {
            seen.Add(card.RuntimeId);
            if (card.X < 0 || card.Z < 0) return; // 未落格（异常数据）

            UnitVisual v;
            if (!_units.TryGetValue(card.RuntimeId, out v))
            {
                v = CreateUnit(card, owner);
                _units[card.RuntimeId] = v;
                created++;
                lastModel = MonsterModelResolver.LastPolicy;
            }

            // 入场演出（2026-10-15）：载荷消费放对账级幂等——CardZoneChange 刷新可能先于
            // CardPutToBattlefieldEvent（载荷入队）创建单位，创建时一次性消费会永久错过；
            // 逐对账 TryGetValue 命中即启动（未命中=无载荷/已消费，零成本返回）
            TryBeginArrival(card, v.Model);

            if (v.X != card.X || v.Z != card.Z)
            {
                if (!_heldUnits.Contains(card.RuntimeId))
                {
                    v.Wrapper.position = UnitAnchor(card.X, card.Z);
                    v.X = card.X;
                    v.Z = card.Z;
                    moved++;
                }
                // 演出持有（2026-10-13 战斗编舞）：位姿归编舞器独占——不写位置也不改记账，
                // 释放后对账按差量重新落笔
            }

            // 朝向架构（2026-10-15 用户定案·二次修订）：模型层持对敌朝向（己方0/对手180）、stats 自
            // billboard、wrapper 恒 identity（横置 24° 侧倾同日退役——横置仅由光波熄表达；
            // 走位/演出期 wrapper 转向=期望世界朝向−ModelYaw，归编舞器）；hold 中归编舞器
            if (!_heldUnits.Contains(card.RuntimeId))
            {
                v.Wrapper.rotation = Quaternion.identity;
                if (v.Model != null)
                    v.Model.transform.localRotation = Quaternion.Euler(0f, v.ModelYaw, 0f);
            }

            // 可行动高光（2026-10-13 卡背退役定案·晚改扩散波·2026-10-17 分色）：未横置未死亡=脚下地块
            // 扩散波，己方淡蓝 / 对手红；横置/死亡即熄
            if (!card.IsTapped && !card.IsDead)
                BattleTileFx.SetUnitGlow(card.X, card.Z, owner == 1);

            // 死亡↔复活（MonsterRig 基础态）：死亡翻转只落一次记账（v.Dead 锁存）；
            // 非 hold 的死亡走通用退场演出（生物=碎裂散落原地沉降片刻即收、结界等无 rig
            // 单位整身沉降入板面——2026-10-17 砍掉滚墓+自旋段，退场不再去墓地格）
            if (card.IsDead && !v.Dead)
            {
                v.Dead = true;
                if (!_heldUnits.Contains(card.RuntimeId))
                    PlayDeathExitAsync(card.RuntimeId, owner).Forget();
                // hold 中：记账已锁存，退场时机归编舞器⑤（数值/体型照常刷新）
            }
            else if (!card.IsDead && v.Dead)
            {
                v.Dead = false;
                v.SetStatsVisible(true); // 死亡碎裂时隐去的头顶数值随复活恢复
                v.Anim?.Revive();
                v.Anim?.Play(MonsterAction.Breath);
            }

            if (v.Stats != null && card.Runtime != null)
            {
                // 无生命单位（结界/装备，DurabilityOnly 同口径）：生命位喂耐久层数——_life 恒 0，
                // 耐久真源=指示物（2026-10-16 修；持续型结界无层=显 0）
                int statLife = card.Type != Cardtype.Creature
                    ? card.Runtime.Counters?.FirstOrDefault(c => c.Key == CardCore.Attribute.CounterRules.DurabilityCounter)?.Value ?? 0
                    : card.Runtime.Life;
                v.SetStatsValues(card.Runtime.Power, statLife, card.Armor, card.BasePower, card.BaseLife);
            }
                // 数值节拍（2026-10-15）：不再直写——冻结期缓冲、命中节拍释放、变化滚动过渡
        }

        private UnitVisual CreateUnit(BattleCardView card, int owner)
        {
            var wrapper = new GameObject("unit-" + card.RuntimeId % 10000);
            wrapper.transform.SetParent(_root, false);
            wrapper.transform.position = UnitAnchor(card.X, card.Z);
            wrapper.transform.rotation = Quaternion.identity; // 朝向归模型层（2026-10-15）：wrapper 只承载位移/演出旋转

            GameObject model;
            MonsterRigAnimator anim = null;
            Vector3 baseScale = Vector3.one, basePos = Vector3.zero;

            // 结界（2026-10-14）：专属三件模型（圆锥本体+光环环+六向箭头底座）——不走怪兽模型解析；
            // 箭头/光环环按卡数据对账（圆环=有链接光环效果的结界，箭头=卡面箭头方向的绝对指向）
            if (card.Type == Cardtype.Enchantment)
            {
                var enchantPrefab = BattleEnchantModel.Load();
                if (enchantPrefab != null)
                {
                    model = Object.Instantiate(enchantPrefab, wrapper.transform);
                    model.name = enchantPrefab.name;
                    model.transform.localPosition = Vector3.zero;
                    // 箭头绝对方向（2026-10-15 架构）：wrapper 恒 identity，净朝向全落模型层——
                    // 180/0=结界箭头 10-14 定案时的世界朝向（对手 Opposite 镜像配套）
                    model.transform.localRotation = Quaternion.Euler(0f, owner == 0 ? 180f : 0f, 0f);
                    model.transform.localScale = Vector3.one;
                    var fx = model.GetComponent<BattleEnchantFx>();
                    if (fx != null)
                        fx.Apply((HexDirection)card.ArrowDirs, owner == 1, card.HasAura);
                    Debug.Log($"[BattleField] 结界 {card.Name}({card.CardId}) 三件模型"
                              + $"（箭头={(HexDirection)card.ArrowDirs} 光环环={(card.HasAura ? "亮" : "无")}）");
                    var ve = FinishUnit(card, wrapper.transform, model, null, owner);
                    ve.ModelYaw = owner == 0 ? 180f : 0f;
                    return ve;
                }
                Debug.Log($"[BattleField] 结界 {card.Name}({card.CardId}) prefab 缺失→占位胶囊");
            }

            var prefab = MonsterModelResolver.Resolve(card.CardId, card.Name, card.Illustration);
            if (prefab != null)
            {
                Debug.Log($"[BattleField] 单位 {card.Name}({card.CardId}) 模型={MonsterModelResolver.LastPolicy}");
                model = Object.Instantiate(prefab, wrapper.transform);
                model.name = prefab.name; // 去 (Clone)：目录名/日志一致性
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.Euler(0f, FacingYaw(owner), 0f); // 对敌朝向归模型层（2026-10-15）
                model.transform.localScale = Vector3.one; // 体型已烘进资产（模块数分档 1/1/1.5，小档 2026-10-14 晚并入 1），运行时不缩放
                model.SetActive(false); // catalogName 必须赶在 OnEnable→BuildDriver 前
                anim = model.AddComponent<MonsterRigAnimator>();
                anim.SetCatalogName(prefab.name);
                model.SetActive(true);  // 此刻按正确目录条目捕获 rest
            }
            else
            {
                // 占位胶囊（模型清单空/加载失败的兜底）——不再静默：点名到卡（2026-10-13）
                Debug.Log($"[BattleField] 单位 {card.Name}({card.CardId}) 无模型→占位胶囊（清单空或 prefab 加载失败）");
                model = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                Object.Destroy(model.GetComponent<Collider>());
                model.transform.SetParent(wrapper.transform, false);
                baseScale = new Vector3(0.7f, 0.9f, 0.7f);
                basePos = new Vector3(0f, 0.9f, 0f);
                model.transform.localScale = baseScale;
                model.transform.localPosition = basePos;
                model.GetComponent<Renderer>().material.color = owner == 0 ? P1UnitColor : P2UnitColor;
            }

            // 头顶数值高度=实际包围盒顶+余量（2026-10-14 体型分档后各模型高低不一，不能再按统一基准估算）
            var uv = FinishUnit(card, wrapper.transform, model, anim, owner);
            uv.ModelYaw = FacingYaw(owner);
            return uv;
        }

        // ---- 出场粒子汇聚（2026-10-15）：CastPlayed 入场的模型隐藏至粒子回调显现 ----

        /// <summary>登记入场演出载荷（BattleScreen 在 CardPutToBattlefieldEvent(CastPlayed) 时组装；
        /// 键=RuntimeId。消费点=CreateUnit（一次性取出；未消费残留随 Dispose 清空）。</summary>
        public void EnqueueArrival(uint runtimeId, int seat, Dictionary<int, float> paid)
            => _arrivals[runtimeId] = (seat, paid);

        /// <summary>有载荷才演出：模型全程可见——世界Y消融（MPB 驱动 Tide/YGOMonster shader 分支）
        /// +地格粒子汇聚，t=1s 消融线沉底=完全显现（v2 定案：不再隐藏 wrapper，头顶数值随出）。
        /// model=入场模型根（拼装预制体亦可——影子内部合并网格）。演出异常兜底立即复位（BattleCastArrival 契约）。</summary>
        private void TryBeginArrival(BattleCardView card, GameObject model)
        {
            if (!_arrivals.TryGetValue(card.RuntimeId, out var payload)) return;
            _arrivals.Remove(card.RuntimeId);
            Debug.Log($"[BattleField] 单位 {card.Name}({card.RuntimeId}) 入场演出开始（世界Y消融+粒子汇聚）");
            BattleCastArrival.Play(card.RuntimeId, payload.seat, payload.paid,
                UnitAnchor(card.X, card.Z), model,
                () => { }); // 模型消融自显——回调仅节拍通知（保留契约供外部观察）
        }

        /// <summary>单位装配收尾（结界/生物两路汇流）：头顶数值高度=实际包围盒顶+余量
        /// （2026-10-14 体型分档后各模型高低不一，不能再按统一基准估算）；生物挂头注视供应商
        /// （2026-10-15 战场接线：待机=最近敌方生物→对方角色，战斗注视钉优先——见 ResolveGazeOf）。</summary>
        private UnitVisual FinishUnit(BattleCardView card, Transform wrapper, GameObject model,
            MonsterRigAnimator anim, int owner)
        {
            float textY = 1.35f;
            float lookY = 0.6f;
            var unitRenderers = model.GetComponentsInChildren<Renderer>();
            if (unitRenderers.Length > 0)
            {
                var b = unitRenderers[0].bounds;
                for (int ri = 1; ri < unitRenderers.Length; ri++) b.Encapsulate(unitRenderers[ri].bounds);
                textY = b.max.y - wrapper.transform.position.y + 0.35f;
                lookY = Mathf.Clamp((b.max.y - wrapper.transform.position.y) * 0.55f, 0.3f, 1.6f);
            }

            var v = new UnitVisual
            {
                Wrapper = wrapper,
                Model = model,
                Anim = anim,
                X = card.X,
                Z = card.Z,
                Dead = card.IsDead,
                Owner = owner,
                IsCreature = card.Type != Cardtype.Enchantment,
                DurabilityOnly = card.Type != Cardtype.Creature, // 无生命单位（结界/装备）只显耐久（2026-10-16）
                LookHeight = lookY,
            };
            v.Stats = CreateStatsText(wrapper, textY);
            anim?.SetLookTarget(() => ResolveGazeOf(v)); // 供应商在组件层持有，驱动重建自动重挂
            return v;
        }

        // ======================================== 施放占格类型图标（2026-10-15 发动格拆除） ========================================

        /// <summary>施放卡类型图标对账（发动区占格卡 × 单位区空格——整个 2×6 兼任发动区）。
        /// 2026-10-17 时序定案：离区不即清——转正在场（同帧已入 _units，Apply 单位先于本段）=
        /// linger 至登场演出完成（≈1.15s）；其余（法术结算）=1s 等待期；被阻拦失败=
        /// InterruptCasting 即清（CardActivationFailedEvent）；新卡占同格=顶掉旧 linger。</summary>
        private void ReconcileCasting(List<BattleCardView> cards)
        {
            var seen = new HashSet<uint>();
            foreach (var card in cards)
            {
                if (card.X < 0 || card.Z < 0) continue; // 未落格（满场无空格）不出图标
                seen.Add(card.RuntimeId);
                if (!_castings.TryGetValue(card.RuntimeId, out var go) || go == null)
                {
                    go = CreateCastingCard(card);
                    _castings[card.RuntimeId] = go;
                }
                var anchor = CastingAnchor(card.X, card.Z);
                if ((go.transform.position - anchor).sqrMagnitude > 1e-6f)
                    go.transform.position = anchor; // 施放格迁移（钉格变化）
                _castingCells[card.RuntimeId] = new Vector2Int(card.X, card.Z); // 占格随对账刷新（TryGetOnBoardCell 消费）
            }

            List<uint> stale = null;
            foreach (var kv in _castings)
                if (!seen.Contains(kv.Key)) (stale ?? (stale = new List<uint>())).Add(kv.Key);
            if (stale == null) return;
            foreach (var id in stale)
            {
                var go = _castings[id];
                _castings.Remove(id);
                _castingCells.Remove(id);
                if (go == null) continue;
                _castingLingers[id] = go; // linger 登记（InterruptCasting/同格顶替可提前清）
                bool arrived = _units.ContainsKey(id); // 成功转正：登场演出档；否则=法术结算 1s 等待期
                LingerCastingAsync(id, go, arrived ? 1.15f : 1.0f).Forget();
            }
        }

        /// <summary>施放 linger 到期销毁（离区后延迟清除——登场演出/等待期窗口）。</summary>
        private async UniTaskVoid LingerCastingAsync(uint id, GameObject go, float seconds)
        {
            await UniTask.Delay((int)(seconds * 1000f));
            if (go == null) return; // 已被 InterruptCasting/同格顶替/Dispose 清掉（Unity fake-null 判死）
            _castingLingers.Remove(id);
            DestroyCastingIcon(go);
        }

        /// <summary>施放被阻拦/失败（CardActivationFailedEvent）：图标即清——不等 linger。</summary>
        public void InterruptCasting(uint runtimeId)
        {
            if (_castings.TryGetValue(runtimeId, out var live))
            {
                _castings.Remove(runtimeId);
                _castingCells.Remove(runtimeId);
                if (live != null) DestroyCastingIcon(live);
            }
            if (_castingLingers.TryGetValue(runtimeId, out var ling))
            {
                _castingLingers.Remove(runtimeId);
                if (ling != null) DestroyCastingIcon(ling);
            }
        }

        /// <summary>销毁单枚施放图标（含降级材质实例回收；共享 CastType_* 不动）。</summary>
        private static void DestroyCastingIcon(GameObject go)
        {
            if (go == null) return;
            var rend = go.GetComponent<MeshRenderer>();
            var sm = rend != null ? rend.sharedMaterial : null; // 只读 sharedMaterial——.material getter 会实例化共享件
            if (sm != null && sm.name.StartsWith("CastFallback_", StringComparison.Ordinal))
            {
                if (Application.isPlaying) Object.Destroy(sm); // 降级路径逐卡实例随件销毁防泄漏
                else Object.DestroyImmediate(sm);
            }
            if (Application.isPlaying) Object.Destroy(go);
            else Object.DestroyImmediate(go);
        }

        private GameObject CreateCastingCard(BattleCardView card)
        {
            // 2026-10-17 二改（用户验收）：程序描边退役——Element 预制体平贴卡类型图标
            //（barrier/biology/spell 纯白），缺件回落纯色 Quad（材质名 CastFallback_ 前缀=随件销毁）。
            if (_castIconPrefab == null)
                _castIconPrefab = Tide.HotUpdate.HotUpdateAssets.Load<GameObject>(
                    "Element", "Assets/Art/Prefab/Element.prefab");
            if (_castIconPrefab == null)
            {
                Debug.LogWarning("[BattleField] Element.prefab 缺失——施放类型图标降级纯色 Quad");
                var fb = GameObject.CreatePrimitive(PrimitiveType.Quad); // 自带 MeshCollider
                fb.name = CastingCardPrefix + card.RuntimeId;
                fb.transform.SetParent(_root, false);
                fb.transform.position = CastingAnchor(card.X, card.Z);
                fb.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                fb.transform.localScale = new Vector3(0.8f, 0.8f, 1f);
                var frend = fb.GetComponent<MeshRenderer>();
                frend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var fmat = frend.material; // 逐卡实例（命名标记=stale 清理只杀实例不杀共享）
                fmat.name = "CastFallback_" + card.RuntimeId;
                fmat.color = Color.white;
                return fb;
            }

            var go = Object.Instantiate(_castIconPrefab, _root, false);
            go.name = CastingCardPrefix + card.RuntimeId;
            // 同格旧 linger 顶替（新卡使用了=旧的让位）：先清同格遗留再落位
            var anchorNow = CastingAnchor(card.X, card.Z);
            List<uint> sameCell = null;
            foreach (var kv in _castingLingers)
                if (kv.Value != null && (kv.Value.transform.position - anchorNow).sqrMagnitude < 0.1f)
                    (sameCell ?? (sameCell = new List<uint>())).Add(kv.Key);
            if (sameCell != null)
                foreach (var oldId in sameCell)
                {
                    var oldGo = _castingLingers[oldId];
                    _castingLingers.Remove(oldId);
                    if (oldGo != null) DestroyCastingIcon(oldGo);
                }
            go.transform.position = anchorNow;
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // 面朝上（与 BattleTileFx.AddElement 同防御）
            go.transform.localScale = Vector3.one * 0.6f;
            var rend = go.GetComponent<MeshRenderer>();
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
            var mat = CastTypeMaterialOf(card.Type);
            if (mat != null) rend.sharedMaterial = mat;
            var mf = go.GetComponent<MeshFilter>();
            var col = go.AddComponent<MeshCollider>(); // Element 无碰撞体——补挂保 cast-{rid} 射线路由
            if (mf != null && mf.sharedMesh != null) col.sharedMesh = mf.sharedMesh;
            Debug.Log($"[BattleField] 施放类型图标：{card.Name}({card.CardId}) @({card.X},{card.Z})（类型 {card.Type}）");
            return go;
        }

        /// <summary>卡类型→图标材质：三图（结界 barrier / 生物 biology / 法术 spell·装备兜底）纯白——
        /// 以元素图标白模板（URP Unlit 透明·_BaseColor 恒白）运行时实例换 _BaseMap/_MainTex；
        /// 实例随 BattleFieldView.Dispose 销毁（跨局不缓存——跨域 fake-null 防口）。</summary>
        private Material CastTypeMaterialOf(Cardtype t)
        {
            if (_castTypeMats.TryGetValue(t, out var m)) return m; // 含失败缓存（null）——缺件不逐次重试刷告警
            string icon = t == Cardtype.Enchantment ? "barrier"
                        : t == Cardtype.Creature ? "biology"
                        : "spell";
            var template = Tide.HotUpdate.HotUpdateAssets.Load<Material>(
                "BattleCardBack_Elem_White", "Assets/Art/Battle3D/Materials/BattleCardBack_Elem_White_Mat.asset");
            var tex = Tide.HotUpdate.HotUpdateAssets.Load<Texture2D>(
                icon, $"Assets/Art/Battle3D/Icons/{icon}.png");
            if (template == null || tex == null)
            {
                Debug.LogWarning($"[BattleField] 施放类型图标缺件：{(template == null ? "Elem_White 模板" : icon + ".png")}——该类型降级预制体默认材质");
                _castTypeMats[t] = null; // 失败入缓存：一次告警，之后恒走降级
                return null;
            }
            m = new Material(template) { name = "CastType_" + t };
            m.SetTexture("_BaseMap", tex);
            m.SetTexture("_MainTex", tex); // 模板双贴图口同口径（与元素材质资产一致）
            _castTypeMats[t] = m;
            return m;
        }

        /// <summary>图标锚点：格面上方微浮（+0.012 与 BattlePriorityPanel 同防地块遮挡/防 z-fight）。</summary>
        private static Vector3 CastingAnchor(int x, int z)
            => BattleBoardGrid.WorldPositionOf(x, z) + new Vector3(0f, 0.012f, 0f);

        /// <summary>场上卡占格查询（2026-10-16 目标选择红瞄准面片）：单位（生物/结界/衍生物）
        /// 与施放占格类型图标都算「在场上」——命中返回其数据格坐标；不在场/未落格=false。
        /// 只读对账记账（_units/_castingCells），不触引擎。</summary>
        public bool TryGetOnBoardCell(uint runtimeId, out int x, out int z)
        {
            x = z = -1;
            if (_units.TryGetValue(runtimeId, out var v))
            {
                if (v.Wrapper == null || v.X < 0 || v.Z < 0) return false;
                x = v.X;
                z = v.Z;
                return true;
            }
            if (_castings.TryGetValue(runtimeId, out var go) && go != null
                && _castingCells.TryGetValue(runtimeId, out var cell))
            {
                x = cell.x;
                z = cell.y;
                return x >= 0 && z >= 0;
            }
            return false;
        }

        /// <summary>单位站格锚点：格心 + 少量抬高（防 z-fight）。</summary>
        private static Vector3 UnitAnchor(int x, int z)
            => BattleBoardGrid.WorldPositionOf(x, z) + new Vector3(0f, 0.05f, 0f);

        /// <summary>朝向：FacingYawOf 同源=模型层本地偏航（烘焙前向 +Z：己方 0 / 对手 180）。</summary>
        private static float FacingYaw(int owner) => FacingYawOf(owner);

        // ======================================== 地牌（固定元素格） ========================================

        /// <summary>地牌固定元素格对账（2026-10-14 晚定案）：6 格按 BoardLayout.LandCellElementOrder
        ///（灰红蓝绿黑白，玩家视角左→右）固定元素身份——图标常驻（空格也亮身份）；
        /// 计数=「可用/上限」（2026-10-14 表述定案，当晚上限语义修正）：可用=未横置张+钱包该色余额
        ///（黑白无地牌=钱包余额）；上限=红蓝绿灰该色已放入地牌张数（含横置）；**黑白格只显余量不显上限**
        ///（2026-10-16 全类型累计钳改案：黑白的生成上限语义移交中线产量双格「累计/地牌上限」，
            /// 六色同池后黑白格不再单独挂上限数字）；可用&gt;0 点亮顶面锁色描边环
            /// （2026-10-17 定案：整块菲涅尔壳颜色压住格面文字/图标退役，见 BattleTileFx）。
            /// 计数条=格面平铺 TMP（不看向相机——平贴地块，见 EnsureFlatCellLabel）。</summary>
        private void ReconcileLands(List<BattleCardView> lands, BattlePlayerView pv, int owner)
        {
            // 按色聚合（引擎固定元素格保证同色同格；无锁色异常卡按灰聚拢=同引擎口径）
            var byColor = new Dictionary<int, LandGroup>();
            foreach (var card in lands ?? new List<BattleCardView>())
            {
                if (card == null) continue;
                int color = card.Runtime != null ? card.Runtime.LockedColor : -1;
                if (color < 0) color = (int)ManaType.Gray;
                if (!byColor.TryGetValue(color, out var g))
                {
                    g = new LandGroup { Color = color };
                    byColor[color] = g;
                }
                g.Total++;
                if (!card.IsTapped) g.Untapped++;
            }

            var cells = BoardLayout.LandCells(owner);
            int n = Mathf.Min(cells.Count, BoardLayout.LandCellElementOrder.Length);
            for (int i = 0; i < n; i++)
            {
                int element = (int)BoardLayout.LandCellElementOrder[i];
                var cell = cells[i];
                // 元素标识常驻（空格也亮身份）；+z 0.35 上移让位（屏幕上方向——固定机位自 -z 俯拍，
                // 图标靠格上缘、下方留给平贴计数文字）
                BattleTileFx.AddElement(cell.x, cell.z, element, new Vector2(0f, 0.35f));
                int wallet = pv != null && element >= 0 && element < pv.WalletByColor.Length
                    ? pv.WalletByColor[element] : 0;
                byColor.TryGetValue(element, out var g);
                bool isBw = element == (int)ManaType.White || element == (int)ManaType.Black;
                int remaining = isBw ? wallet : (g != null ? g.Untapped : 0) + wallet; // 黑白：可用即钱包（无地牌）
                if (remaining > 0) BattleTileFx.SetLandGlow(cell.x, cell.z, element);
                var label = EnsureFlatCellLabel(((long)cell.x << 32) | (uint)cell.z, cell.x, cell.z);
                if (label != null)
                {
                    label.gameObject.SetActive(true);
                    // 红蓝绿灰=「可用/上限」（上限=该色已放入地牌张数，含横置）；
                    // 黑白只显余量（2026-10-16 全类型累计钳：上限语义移交中线产量双格，黑白格不挂上限数字）
                    if (isBw)
                        label.text = remaining.ToString();
                    else
                        label.text = $"{remaining}/{(g != null ? g.Total : 0)}";
                }
            }
        }

        private sealed class LandGroup
        {
            public int Color, Total, Untapped;
        }

        // ======================================== 角色 ========================================

        private sealed class HeroVisual
        {
            public Transform Root;
            public TextMeshPro Stats;
            public string LastStats;
        }

        private void ReconcileHero(BattlePlayerView p, int owner)
        {
            if (BattleRevealDirector.SuppressHeroes) return; // 教学介绍段闸（2026-10-17 语音联动显隐）：介绍到角色格前不建柱
            HeroVisual v;
            if (!_heroes.TryGetValue(owner, out v))
            {
                var cell = BoardLayout.CharacterCell(owner);
                var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Object.Destroy(pillar.GetComponent<Collider>());
                pillar.name = "hero-" + (owner == 0 ? "self" : "opp");
                pillar.transform.SetParent(_root, false);
                pillar.transform.position = BattleBoardGrid.WorldPositionOf(cell.x, cell.z) + new Vector3(0f, 0.6f, 0f);
                pillar.transform.localScale = new Vector3(0.55f, 0.6f, 0.55f);
                pillar.GetComponent<Renderer>().material.color = owner == 0 ? P1UnitColor : P2UnitColor;

                v = new HeroVisual
                {
                    Root = pillar.transform,
                    Stats = CreateStatsText(pillar.transform, 1.3f),
                };
                _heroes[owner] = v;
            }
            // 立柱数值（2026-10-16）：常态只显 HP；护甲>0 缀「(+x)」；获得攻击力（HeroAttack 弹药>0）
            // 追加「Ac:x」——无攻击力不占位，弹药打空自动回落纯 HP。
            // 2026-10-17 角色状态展示：非读数层指示物续拼「名×n」摘要（读数层 Ac/护甲已表达）——
            // 与生物悬停状态段同滤法（滤 0 层与 LayerRole==Reading）。
            if (v.Stats == null) return;
            string txt = "HP " + p.Life + ArmorSuffix(p.Armor);
            if (p.HeroAttack > 0) txt += " Ac:" + p.HeroAttack;
            if (p.Counters != null)
            {
                foreach (var c in p.Counters)
                {
                    if (c == null || c.Value == 0) continue;
                    var spec = CardCore.Attribute.CounterRules.Find(c.Key);
                    if (spec != null && spec.LayerRole == CardCore.Attribute.CounterLayerRole.Reading) continue;
                    if (c.Key == CardCore.Attribute.KeywordRules.ArmorCounter) continue; // 护甲已是 (+x) 后缀，不重复
                    txt += " " + CounterText.DisplayNameOf(c.Key) + "×" + c.Value;
                }
            }
            if (v.LastStats != txt)
            {
                v.LastStats = txt;
                v.Stats.text = txt;
            }
        }

        // ======================================== 技能格（2026-10-16 撤销——空段留档） ========================================

        /// <summary>地牌格平铺数字条（2026-10-14 晚定案：这类格子的文字不看向相机，平贴地块显示；
        /// 晚二批=轴对齐 z 旋 0；本批修字向）：面朝上且**读向 +x、字头 +z、可读面（TMP 本地 -Z）朝相机**
        /// ——正解=LookRotation(down, forward)=Euler(90,0,0)，相机 (7,8,-4)/(50,0,0) 自 -z 俯拍实测
        /// （旧 LookRotation(up,…) 三项全反：字镜像且读向右到左，中文整句一眼翻反、1/1 回文数漏检）。
        /// 贴格顶面 y=0.014（顶面 y=0、菲涅尔 0.004、图标 0.008 之上），位置=格心向视角前缘（-z）
        /// 偏移 0.42，与上移 +z 0.35 的居中图标让位。key=格(x&lt;&lt;32|z)。
        /// 2026-10-17 改挂 tile 子物体（原挂 _root）：教学介绍段独占显隐按 tile SetActive 收格，
        /// 文字不再悬空留在盘面上（与元素图标/HudRow 认领件同口径）。</summary>
        private TextMeshPro EnsureFlatCellLabel(long key, int x, int z)
        {
            if (!EnsureTmpFont()) return null;
            if (_landCounts.TryGetValue(key, out var label) && label != null) return label; // fake-null 判死重建
            var tile = BattleBoardGrid.Root != null ? BattleBoardGrid.Root.transform.Find($"tile-{x}-{z}") : null;
            if (tile == null) return null; // 未装台/格名异常：不建，下一事件重试（与 BattleTileFx.TileOf 同法）
            var go = new GameObject("cell-flat-" + x + "-" + z);
            go.transform.SetParent(tile, false); // tile 本体位姿=格心恒等变换，局部值即原世界偏移
            go.transform.localPosition = new Vector3(0f, 0.014f, -0.42f);
            go.transform.localRotation = Quaternion.LookRotation(Vector3.down, Vector3.forward); // 平贴·轴对齐·字向正（2026-10-14 修）
            label = go.AddComponent<TextMeshPro>();
            label.fontSize = 3.5f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.rectTransform.sizeDelta = new Vector2(1.6f, 0.9f);
            _landCounts[key] = label;
            return label;
        }

        // ======================================== 通用 ========================================

        private static bool _tmpFontChecked, _tmpFontOk;

        /// <summary>TMP 默认字体可用性（缺失=true 一次告警；头顶数值/临时钱包条共用）。</summary>
        private static bool EnsureTmpFont()
        {
            if (!_tmpFontChecked)
            {
                _tmpFontChecked = true;
                _tmpFontOk = TMP_Settings.defaultFontAsset != null;
                if (!_tmpFontOk) Debug.LogWarning("[BattleField] TMP 默认字体未配置——单位/角色头顶数值禁用");
            }
            return _tmpFontOk;
        }

        /// <summary>头顶 world-space TMP（默认字体缺失时禁用并告警一次——数字/ASCII 文案）。</summary>
        private static TextMeshPro CreateStatsText(Transform parent, float height)
        {
            if (!EnsureTmpFont()) return null;

            var go = new GameObject("stats");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, height, 0f);
            // 朝向归 stats 自己（2026-10-15 用户定案）：挂 billboard 每帧对相机——wrapper/模型层的
            // 朝向、横置侧倾、走位旋转都不再带歪文字；初始朝向仅作相机未就绪时的兜底
            var cam = BattleStageDirector.WorldCam;
            if (cam != null)
                go.transform.rotation = Quaternion.LookRotation(go.transform.position - cam.transform.position);
            else
                go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            go.AddComponent<StatsBillboard>();
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.fontSize = 5f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            return tmp;
        }

        /// <summary>头顶数值 billboard（2026-10-15 定案）：每帧自转朝世界相机——旋转是 stats 自己的
        /// 事，与 wrapper（位移/演出旋转）/模型层（对敌朝向）解耦。</summary>
        private sealed class StatsBillboard : MonoBehaviour
        {
            private void LateUpdate()
            {
                var cam = BattleStageDirector.WorldCam;
                if (cam == null) return;
                transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
            }
        }

        private static int RemoveStale(Dictionary<uint, UnitVisual> map, HashSet<uint> seen, HashSet<uint> held)
        {
            List<uint> stale = null;
            foreach (var kv in map)
                if (!seen.Contains(kv.Key))
                    (stale ?? (stale = new List<uint>())).Add(kv.Key);
            if (stale == null) return 0;
            int removed = 0;
            foreach (var id in stale)
            {
                if (held.Contains(id)) continue; // 演出持有：销毁时机归编舞器/吸走演出（下次对账再试）
                var v = map[id];
                // 2026-10-17：wrapper 可能已被外部销毁（重看介绍复现——MissingReferenceException
                // 中断整次对账）：fake-null 视为已退场，只清字典并留痕（owner/模型名供定位来源）。
                if (v.Wrapper != null) Object.Destroy(v.Wrapper.gameObject);
                else Debug.LogWarning($"[BattleField] 对账发现单位 {id % 10000}（owner={v.Owner} model={v.Model?.name}）的 wrapper 已被外部销毁——仅移出字典");
                map.Remove(id);
                removed++;
            }
            return removed;
        }

        // ======================================== 战斗编舞接口（2026-10-13 攻击演出） ========================================

        /// <summary>演出持有：hold 中的单位对账跳过位置/横置侧倾写与销毁（编舞器独占位姿），
        /// 死亡翻转不自动倒地（时机由编舞器在受击时刻驱动）。hold 泄漏兜底：Dispose 点名日志。</summary>
        public void HoldUnit(uint id) => _heldUnits.Add(id);

        public void ReleaseUnit(uint id) => _heldUnits.Remove(id);

        // ======================================== 头注视（2026-10-15 战场接线） ========================================

        /// <summary>单位注视解析（每 Tick 求值）：战斗注视钉（编舞器：攻击看攻击对象/被守卫拦截
        /// 看拦截者）优先，钉供应器返回 null（目标已离场）自动回落待机；待机=最近敌方生物→
        /// 对方角色→null（与 MonsterLookTarget 同语义，此处含存活性/结界过滤的战场单源实现）。</summary>
        private Vector3? ResolveGazeOf(UnitVisual v)
        {
            if (v.LookPin != null)
            {
                var pinned = v.LookPin();
                if (pinned.HasValue) return pinned;
            }
            return IdleGazeOf(v);
        }

        private Vector3? IdleGazeOf(UnitVisual v)
        {
            if (v.Wrapper == null) return null;
            Vector3 self = v.Wrapper.position;
            UnitVisual best = null;
            float bestSqr = float.MaxValue;
            foreach (var kv in _units)
            {
                var o = kv.Value;
                if (o == v || !o.IsCreature || o.Owner == v.Owner || o.Dead || o.Wrapper == null) continue;
                float sqr = (o.Wrapper.position - self).sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; best = o; }
            }
            if (best != null) return best.Wrapper.position + Vector3.up * best.LookHeight;
            if (_heroes.TryGetValue(1 - v.Owner, out var hero) && hero.Root != null)
                return hero.Root.position; // 角色立柱位（y≈0.6 胸口高）
            return null;
        }

        /// <summary>战斗注视钉（编舞器攻击演出用）：resolver 每帧求值，返回 null=回落待机注视；
        /// 单位不在场（已被销毁）忽略。攻击者看攻击对象、被守卫拦截看拦截者均走此口。</summary>
        public void SetCombatGaze(uint id, Func<Vector3?> resolver)
        {
            if (_units.TryGetValue(id, out var v)) v.LookPin = resolver;
        }

        public void ClearCombatGaze(uint id)
        {
            if (_units.TryGetValue(id, out var v)) v.LookPin = null;
        }

        /// <summary>单位注视点（包围盒胸口高度）——编舞器给对视双方喂点用。</summary>
        public bool TryGetUnitLookPoint(uint id, out Vector3 point)
        {
            point = Vector3.zero;
            if (!_units.TryGetValue(id, out var v) || v.Wrapper == null) return false;
            point = v.Wrapper.position + Vector3.up * v.LookHeight;
            return true;
        }

        // ======================================== 视觉在途门（2026-10-14 人机节奏定案） ========================================

        private int _visualsInFlight;

        /// <summary>视觉段开始/结束（严格 try/finally 配对）：攻击演出（编舞器 PerformAsync）、
        /// 角色受击脉冲计入——归零=表现层空闲（死亡退场为可忽略表演不占门，2026-10-17）。
        /// 人机对局 AI 经 WaitVisualsIdleAsync 在动作间等门（等一件事演完再做下一件）；
        /// AI-vs-AI/无头不查门（全速，完全不管渲染层）。</summary>
        public void BeginVisual() => _visualsInFlight++;

        /// <summary>表现层是否忙碌（2026-10-17 结束回合守卫）：战斗演出/受击脉冲等
        /// 在途（计数>0）——玩家此时结束回合会把演出和回合状态撕裂，UI 层据此拦截。</summary>
        public bool VisualsBusy => _visualsInFlight > 0;

        public void EndVisual()
        {
            if (_visualsInFlight > 0) _visualsInFlight--;
        }

        /// <summary>等表现层空闲（视觉在途归零）。计数走 try/finally 不泄漏；仅人机节奏路径调用。</summary>
        public async UniTask WaitVisualsIdleAsync()
        {
            while (_visualsInFlight > 0) await UniTask.Yield();
        }

        /// <summary>单位身体访问（编舞器走位/动作驱动用）；wrapper 承载位移（不触发 Recapture）。</summary>
        public bool TryGetUnitBody(uint id, out Transform wrapper, out MonsterRigAnimator anim)
        {
            wrapper = null;
            anim = null;
            if (!_units.TryGetValue(id, out var v) || v.Wrapper == null) return false;
            wrapper = v.Wrapper;
            anim = v.Anim;
            return true;
        }

        /// <summary>角色立柱访问（角色受击脉动/朝向用）。</summary>
        public bool TryGetHeroRoot(int owner, out Transform root)
        {
            root = null;
            return _heroes.TryGetValue(owner, out var v) && v.Root != null && (root = v.Root) != null;
        }

        /// <summary>单位退场（吸走演出收尾）：销毁 wrapper+出字典+清 hold（幂等）。</summary>
        public void RetireUnit(uint id)
        {
            _heldUnits.Remove(id);
            if (_units.TryGetValue(id, out var v))
            {
                if (v.Wrapper != null) Object.Destroy(v.Wrapper.gameObject);
                _units.Remove(id);
            }
        }

        // ======================================== 头顶数值节拍（2026-10-15 定案：攻击结算同帧改血，展示等命中时刻） ========================================

        /// <summary>战斗宣言期冻结全部单位头顶数值：对账照常写入目标值但展示不动——
        /// 攻击/反击/溅射各自在编舞器命中节拍释放（滚动落值），演出收尾全量兜底释放。</summary>
        public void HoldAllUnitStats()
        {
            foreach (var v in _units.Values) v.StatsHeld = true;
        }

        /// <summary>命中节拍释放单个单位（伤害落地时刻滚动显示）；未知 id/未冻结=无操作。</summary>
        public void ReleaseUnitStats(uint id)
        {
            if (_units.TryGetValue(id, out var v)) v.ReleaseStatsHold();
        }

        /// <summary>全量释放（演出收尾/取消/Dispose 兜底）：任何路径不漏冻结。</summary>
        public void ReleaseAllHeldStats()
        {
            foreach (var v in _units.Values) v.ReleaseStatsHold();
        }

        // ======================================== 生物箭头（钉地） ========================================

        /// <summary>生物卡面箭头对账（2026-10-15 用户定案）：ArrowDirs≠0 的生物（链接生物）在
        /// 数据格 tile 上铺六向三角（复用结界 prefab 的 arrows 件；锥隐藏、环恒灭）——挂 tile 不挂
        /// wrapper，攻击/守卫拦截跑动不跟走，移动/终局随全量对账重建。镜像口径同结界：根旋转复刻
        /// 旧 wrapper 半场朝向（己方 180/对方 0）+ Apply 的对手 Opposite 镜像。</summary>
        private void ReconcileCreatureArrows(BattleViewData d)
        {
            foreach (var a in _creatureArrows)
                if (a != null)
                {
                    if (Application.isPlaying) Object.Destroy(a);
                    else Object.DestroyImmediate(a);
                }
            _creatureArrows.Clear();
            if (BattleBoardGrid.Root == null) return;

            AddCreatureArrows(d.SelfUnits, 0);
            AddCreatureArrows(d.OppUnits, 1);
        }

        private void AddCreatureArrows(List<BattleCardView> units, int owner)
        {
            var prefab = BattleEnchantModel.Load();
            if (prefab == null) return; // 缺失告警由 Load 落（与结界路径同源一次）
            foreach (var card in units)
            {
                if (card.ArrowDirs == 0 || card.Type == Cardtype.Enchantment) continue; // 结界三件自带
                if (card.IsDead || card.X < 0 || card.Z < 0) continue;
                var tile = BattleBoardGrid.Root.transform.Find($"tile-{card.X}-{card.Z}");
                if (tile == null) continue;

                var inst = Object.Instantiate(prefab, tile, false);
                inst.name = "fx-creature-arrows";
                inst.transform.localPosition = new Vector3(0f, 0.006f, 0f);
                inst.transform.localRotation = owner == 0 ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;
                inst.GetComponent<BattleEnchantFx>()?.Apply((HexDirection)card.ArrowDirs, owner == 1, false);
                var cone = inst.transform.Find("cone");
                if (cone != null) cone.gameObject.SetActive(false);
                _creatureArrows.Add(inst);
            }
        }

        /// <summary>模型层对敌本地偏航（公开：编舞器走位/转面补偿用）：
        /// 模型烘焙前向=本地 +Z（2026-10-15 用户定案：头朝向即 +Z 朝向，以「攻击动作是前拱」为判据
        /// 手工校准，反向个体已删除；与 RigDriver「移动方向恒根前向 +Z」契约一致，推翻同日 −Z 二次定案）。
        /// 己方(P1，低 z 半场) 0 / 对方(P2) 180 正对敌半场。
        /// wrapper 恒 identity，走位期 wrapper 转向=期望世界朝向(Atan2)−本值。</summary>
        public static float FacingYawOf(int owner) => owner == 0 ? 0f : 180f;

        /// <summary>死亡退场演出（战斗编舞与通用死亡同路径；2026-10-17 定案：砍掉滚墓与墓上
        /// 自旋段，退场不再去墓地格）：生物=倒地碎裂（部件抛物线散落）原地停 DeathRestSeconds
        /// 沉降即收；无 rig 单位（结界等）暂无碎裂，整身原地沉降入板面消失。中途复活
        /// （v.Dead 翻回）/拆屏即中止并释放 hold（体型朝向与站位复原交还对账）；退场完
        /// RetireUnit 自清。2026-10-17 可忽略表演定案：**纯装饰不占视觉在途门**——逻辑上卡早已
        /// 入墓（SBA 结算即完成），退场动画期间玩家/AI 照常行动（立即发动墓地相关效果在墓地里
        /// 找得到目标）；调用方 fire-and-forget。</summary>
        public async UniTask PlayDeathExitAsync(uint id, int owner)
        {
            // 可忽略表演：不 BeginVisual——不阻塞 AI 节奏等待/结束回合守卫（战斗主序列/受击等
            // 信息性表演才占门；纯装饰表演此后按此口径分类，预计还有多种）
            if (!_units.TryGetValue(id, out var v) || v.Wrapper == null) return;
                HoldUnit(id); // 防 RemoveStale 抢销毁

            // 头顶数值同帧消失（中途复活由对账分支恢复显示）
            v.SetStatsVisible(false);
            bool finished;
            if (v.Anim != null)
            {
                // 生物：碎裂散落（部件脱离层级抛物线落地），固定等 0.5s 沉降即收
                v.Anim.Play(MonsterAction.FallDown);
                finished = await DeadHoldAsync(v, DeathRestSeconds);
            }
            else
            {
                // 结界等无 rig 单位：暂无碎裂，整身沉降入板面后收
                finished = await SinkUnderBoardAsync(v, DeathSinkSeconds);
            }
            if (finished)
            {
                Debug.Log($"[BattleField] 单位 {id % 10000} 死亡退场完成（owner={owner}）");
                RetireUnit(id);
            }
            else ReleaseUnit(id);
            // 可忽略表演：无门配对/无 try 包装——异常自然上抛由调用方 Forget 吞记
        }

        /// <summary>死亡等待：固定时长（复活/销毁即提前返回 false 交还对账）。</summary>
        private static async UniTask<bool> DeadHoldAsync(UnitVisual v, float seconds)
        {
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < seconds)
            {
                if (v.Wrapper == null || !v.Dead) return false;
                await UniTask.Yield();
            }
            return v.Wrapper != null && v.Dead;
        }

        /// <summary>整身沉降入板面（无 rig 单位的死亡退场）：wrapper 缓入下沉（渐快），深度=模型
        /// 顶高+余量（整只没入地面下）；复活竞争即停并复原站位，销毁即停。</summary>
        private static async UniTask<bool> SinkUnderBoardAsync(UnitVisual v, float seconds)
        {
            Vector3 start = v.Wrapper.position;
            float depth = ModelHeightAbove(v) + 0.15f;
            float t0 = Time.realtimeSinceStartup;
            while (v.Wrapper != null && v.Dead)
            {
                float t = (Time.realtimeSinceStartup - t0) / seconds;
                if (t >= 1f) break;
                v.Wrapper.position = start + Vector3.down * (depth * t * t); // 缓入：下沉渐快
                await UniTask.Yield();
            }
            if (v.Wrapper != null && !v.Dead) v.Wrapper.position = start; // 复活竞争：复原站位
            return v.Wrapper != null && v.Dead;
        }

        /// <summary>模型顶相对 wrapper 原点的高度（沉降深度用；取模型子树渲染器包围盒）。</summary>
        private static float ModelHeightAbove(UnitVisual v)
        {
            if (v.Model == null) return 2f;
            bool has = false;
            Bounds bounds = default;
            foreach (var r in v.Model.GetComponentsInChildren<Renderer>())
            {
                if (!has) { bounds = r.bounds; has = true; }
                else bounds.Encapsulate(r.bounds);
            }
            if (!has) return 2f;
            return Mathf.Max(0.5f, bounds.max.y - v.Wrapper.position.y);
        }

        /// <summary>角色受击脉动（2026-10-13 裁定②：角色目标无模型，立柱缩放脉冲表受击）。</summary>
        public async UniTask HeroImpactPulseAsync(int owner)
        {
            BeginVisual(); // 视觉在途门：脉冲期间人机局 AI 等待
            try
            {
                if (!_heroes.TryGetValue(owner, out var v) || v.Root == null) return;
                var baseScale = new Vector3(0.55f, 0.6f, 0.55f);
                float t0 = Time.realtimeSinceStartup;
                const float dur = 0.28f;
                while (v.Root != null && Time.realtimeSinceStartup - t0 < dur)
                {
                    float t = (Time.realtimeSinceStartup - t0) / dur;
                    float pulse = 1f + 0.18f * Mathf.Sin(t * Mathf.PI);
                    v.Root.localScale = new Vector3(baseScale.x * pulse, baseScale.y * (1f - 0.08f * Mathf.Sin(t * Mathf.PI)), baseScale.z * pulse);
                    await UniTask.Yield();
                }
                if (v.Root != null) v.Root.localScale = baseScale;
            }
            finally { EndVisual(); }
        }

        /// <summary>疲劳受击演出（2026-10-17）：立柱红闪脉冲（0.36s，同受击脉冲数学）+ 头顶红色
        /// 「疲劳 -N」浮字上浮渐隐（0.9s，world TMP 一次性建毁）。信息性表演占视觉门（口径同
        /// HeroImpactPulse）——节奏 AI 等演完再动。立柱未建（教学介绍段 SuppressHeroes）静默返回；
        /// 同 owner 重入（连续疲劳）跳过脉冲只补浮字（防双写缩放/颜色）。</summary>
        public async UniTask HeroFatigueHitAsync(int owner, int damage)
        {
            BeginVisual();
            try
            {
                if (!_heroes.TryGetValue(owner, out var v) || v.Root == null) return;

                var floatTxt = CreateStatsText(v.Root, 1.9f); // 立柱 stats（1.3）之上的高位浮字
                if (floatTxt != null)
                {
                    floatTxt.fontSize = 6f;
                    floatTxt.text = "疲劳 -" + damage;
                    floatTxt.color = StatDownColor;
                }

                bool pulse = _fatigueBusy.Add(owner);
                Renderer rend = pulse && v.Root != null ? v.Root.GetComponent<Renderer>() : null;
                Color baseColor = owner == 0 ? P1UnitColor : P2UnitColor;
                var baseScale = new Vector3(0.55f, 0.6f, 0.55f);
                var startLocal = floatTxt != null ? (Vector3)floatTxt.transform.localPosition : Vector3.zero;

                float t0 = Time.realtimeSinceStartup;
                const float pulseDur = 0.36f, totalDur = 0.9f, fadeStart = 0.45f, rise = 0.85f;
                while (Time.realtimeSinceStartup - t0 < totalDur)
                {
                    float t = Time.realtimeSinceStartup - t0;
                    if (pulse && t < pulseDur && v.Root != null)
                    {
                        float wave = Mathf.Sin(t / pulseDur * Mathf.PI);
                        v.Root.localScale = new Vector3(
                            baseScale.x * (1f + 0.18f * wave),
                            baseScale.y * (1f - 0.08f * wave),
                            baseScale.z * (1f + 0.18f * wave));
                        if (rend != null) rend.material.color = Color.Lerp(baseColor, StatDownColor, wave * 0.85f);
                    }
                    if (floatTxt != null)
                    {
                        float k = Mathf.Clamp01(t / totalDur);
                        // easeOut 上浮 + 后半程渐隐
                        floatTxt.transform.localPosition = startLocal + Vector3.up * (rise * (1f - (1f - k) * (1f - k)));
                        float alpha = t < fadeStart ? 1f : Mathf.Clamp01(1f - (t - fadeStart) / (totalDur - fadeStart));
                        var c = floatTxt.color; c.a = alpha; floatTxt.color = c;
                    }
                    await UniTask.Yield();
                }
                if (pulse)
                {
                    if (v.Root != null)
                    {
                        v.Root.localScale = baseScale;
                        if (rend != null) rend.material.color = baseColor;
                    }
                    _fatigueBusy.Remove(owner);
                }
                if (floatTxt != null) Object.Destroy(floatTxt.gameObject);
            }
            finally { EndVisual(); }
        }
    }
}
