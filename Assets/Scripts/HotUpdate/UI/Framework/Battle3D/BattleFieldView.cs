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
    /// （结构=wrapper[位姿] → 模型根[MonsterRigAnimator]——wrapper 承载位移，
    /// 不触发 Recapture）；死亡→倒地散落、复活→Revive；横置→仅脚下光波熄灭表达
    ///（2026-10-15 定案：呼吸不随横置停止，可行动性只看 BattleTileFx 光波）。
    /// 数值读数（2026-10-26 迁格定案）：不再上头顶——格面三槽平贴数据格 tile，
    /// 槽位=旧格前三键位（生物=左攻/右血两侧、结界=中间耐久；中槽对生物暂无第三读数留着），
    /// 节拍（冻结/滚动）/属性配色（低于印刷红·高于绿）随迁不变，箭头同口径钉格随迁。
    /// 地牌→无实体（2026-10-13 卡背退役定案；2026-10-14 按色叠格）：同色叠一格——格面元素图标+
    /// 「可用/上限」计数条（可用=未横置+钱包该色；上限=红蓝绿灰该色已放入地牌张数、黑白=地牌槽上限），
    /// 可用&gt;0 点亮锁色菲涅尔（BattleTileFx）；
    /// 黑白格计数=钱包余额/生成上限；技能格=技能名数字条（无技能空格）（均 BattleFieldView 对账）。
    /// 可行动性（生物未横置）与地牌状态均由脚下地块表达（BattleTileFx 同日改版）。
    /// 角色→占位立柱+生命值（2026-10-16：常态只显 HP，护甲缀「(+x)」、获得攻击力追加「Ac:x」；
    /// 角色读数仍在柱顶——迁格仅单位；模型映射走 MonsterModelResolver（同名→稳定哈希→占位胶囊）。
    /// 体型=已按模块数分档烘进资产几何（MonsterRigSizeScaler：&lt;5块→0.7/5~10块→1/&gt;10块→1.5，
    /// 2026-10-14 用户定案取代 10-13 全 1+属性放大）；运行时不再缩放，头顶锚高（浮字/动作按钮）按实际包围盒顶。
    ///
    /// 生命周期：BattleScreen.OnEnter 创建、OnExit Dispose。棋盘未就绪
    /// （BattleBoardGrid.Ready=false）跳过本次对账（节流日志），装台后下一事件即补刷。
    /// 数据层证据：每次对账输出统计日志（新建/移动/销毁计数）。
    /// </summary>
    public sealed partial class BattleFieldView
    {
        private readonly Transform _root;
        private readonly Dictionary<uint, UnitVisual> _units = new Dictionary<uint, UnitVisual>();
        private readonly Dictionary<int, HeroVisual> _heroes = new Dictionary<int, HeroVisual>(); // key=归属 0/1
        private readonly Material[] _heroMats = new Material[2]; // 每方着色克隆唯一实例（建柱即上色+对账收养共用，随 Dispose 销毁）
        private readonly HashSet<int> _fatigueBusy = new HashSet<int>(); // 疲劳演出在途 owner（重入只补浮字，2026-10-17）
        private readonly Dictionary<long, TextMeshPro> _landCounts = new Dictionary<long, TextMeshPro>(); // key=格(x<<32|z)：地牌格「可用/上限」计数
        // 演出持有（2026-10-13 战斗编舞）——引用计数（2026-10-18 并行演出修复）：同一单位可同时
        // 被在途演出与新一轮攻击捕获共同持有（连续攻击共享目标），各持一票，全弃才放行对账销毁；
        // 旧 HashSet 单票制下第一场演出 finally 会偷走第二场的持有 → 死亡目标被 RemoveStale 提前销毁
        private readonly Dictionary<uint, int> _heldUnits = new Dictionary<uint, int>();
        /// <summary>效果死亡预持登记（2026-10-19 消灭表现）：CardDestroyEvent 引擎时点持票的尸体——
        /// 快照对账只枚举 Battlefield，效果致死卡已入墓=不进快照，RemoveStale 会抢先销毁 wrapper
        /// 致碎裂退场永不播（战斗击杀有编舞器持有兜底）。持票防抢，回放步放出时开演并弃票。</summary>
        private readonly HashSet<uint> _effectDeathHeld = new HashSet<uint>();
        /// <summary>生物箭头留存条目（2026-10-18 性能差量对账）：位次/朝向/方向集未变则整帧零操作。</summary>
        private sealed class ArrowEntry
        {
            public GameObject Inst;
            public int X, Z, Dirs, Owner;
        }

        private readonly Dictionary<uint, ArrowEntry> _creatureArrows = new Dictionary<uint, ArrowEntry>(); // 生物箭头地面件（钉 tile，按单位 id 差量对账）
        private readonly HashSet<uint> _arrowSeen = new HashSet<uint>();   // 本轮对账在场集合（stale 判定）
        private readonly List<uint> _arrowStale = new List<uint>();        // 离场单位回收清单（复用缓冲）
        private int _lastSkipLogFrame = -1;

        //（2026-10-15 深夜改版）单位【发动效果】头顶按钮退役：单位本体退出射线检测，
        // 攻击/守卫/发动入口走格前 →（2026-10-23 v4 §10.2 点单位弹 BattleUnitActionPopup）
        // →（2026-10-26 定案）动作入口回归头顶 3D：BattleUnitActionButtons 竖直按钮组落
        // 旧读数位（本类只供 StatsAnchorOf 头顶锚），本类仍不持有可点 3D 件。

        // 施放占格类型图标（2026-10-15 发动格拆除：整个 2×6 兼任发动区；2026-10-17 两改：
        // 立绘立牌→程序描边边框→Element 预制体平贴图标）：Element.prefab 面朝上平躺贴格面，
        // 图标=三张卡类型图（结界 barrier / 生物 biology / 法术 spell·装备兜底，纯白不染色）——
        // 材质以 BattleCardBack_Elem_White_Mat（URP Unlit 透明·纯白）为模板运行时实例换贴图；
        // 材质以 BattleCardBack_Elem_White_Mat（URP Unlit 透明·纯白）为模板运行时实例换贴图；
        // 完整卡面呈现归连锁链舞台（BattleChainStage·2026-10-19：cast-stage 左上角卡面退役）。图标补挂 MeshCollider
        //（Element 无碰撞体）保 cast-{RuntimeId} 射线路由（hover 卡信息/点击开发动区列卡）。
        private readonly Dictionary<uint, GameObject> _castings = new Dictionary<uint, GameObject>(); // key=RuntimeId
        private readonly Dictionary<uint, GameObject> _castingLingers = new Dictionary<uint, GameObject>(); // 离区 linger 期图标（key=RuntimeId；登场演出/等待期窗口，到期或被顶即销）
        private readonly Dictionary<uint, Vector2Int> _castingCells = new Dictionary<uint, Vector2Int>(); // 施放图标占格（key=RuntimeId；随对账同进出，TryGetOnBoardCell 查询用）
        private GameObject _castIconPrefab; // Element 预制体（HotUpdateAssets 惰性加载，地牌元素图标同源）
        private readonly Dictionary<Cardtype, Material> _castTypeMats = new Dictionary<Cardtype, Material>(); // 类型→图标材质（运行时实例，随 Dispose 销毁）

        // 声明期贴片预览（2026-10-20 法术发动流程改案）：法术目标收集期的占格贴片——
        // 与正片同款外观；确认=PromoteCastingPreview 转正（无缝衔接），取消=HideCastingPreview 销毁。
        private GameObject _castPreview;

        // 出场粒子汇聚载荷（2026-10-15 用户定案：弃弹出动画改 INab 粒子）：CastPlayed 入场 →
        // BattleCastArrival 粒子演出 1s 后显现模型；paid 键=ManaType 下标、值=该色实付量
        private readonly Dictionary<uint, (int seat, Dictionary<int, float> paid)> _arrivals =
            new Dictionary<uint, (int, Dictionary<int, float>)>();

        /// <summary>施放占格节点名前缀（BattleScreen.PollFieldPointer 射线命中路由——cast-{RuntimeId}）。</summary>
        public const string CastingCardPrefix = "cast-";

        // v6 §11.2（2026-10-23）：阵营色换低饱和族并拉开亮度（原蓝红等亮度对撞）——
        // 己方=青蓝族 #5E7E94（luma≈121）·对方=锈红族 #8A5148（luma≈96），差≥20
        private static readonly Color P1UnitColor = new Color(0.369f, 0.494f, 0.580f);
        private static readonly Color P2UnitColor = new Color(0.541f, 0.318f, 0.282f);

        // 死亡退场（2026-10-17 定案：砍掉滚墓与墓上自旋两段，退场不再去墓地格）——
        // 生物=碎裂散落原地停 DeathRestSeconds 沉降即收；无 rig 单位（结界等）暂无碎裂，
        // 整身原地沉降入板面（深度=模型顶高+余量）
        private const float DeathRestSeconds = 0.5f; // 碎裂落地后的沉降等待
        private const float DeathSinkSeconds = 0.5f; // 无 rig 单位整身下沉时长

        // 属性配色（2026-10-15）：与印刷基础值持平=白（TMP 默认色）、低于=红（受伤/降攻）、
        // 高于=绿（增益）——攻击/生命逐数字独立着色（富文本），查表 miss（无印刷值）恒白。
        private static readonly Color StatDownColor = new Color(1f, 0.38f, 0.32f);
        private static readonly Color StatUpColor = new Color(0.42f, 1f, 0.45f);

        // 拼点配色（2026-10-20 教学二表现）：胜=金（引擎奖励语义——区别于回复绿/伤害红/消灭紫，
        // 与运势等引擎成功族共用色的候选锚点）；败=灰（无彩=无奖励；正差额未达门槛同判败侧灰）。
        private static readonly Color ClashWinColor = new Color(1f, 0.78f, 0.28f);
        private static readonly Color ClashLoseColor = new Color(0.62f, 0.62f, 0.62f);
        // 拼点/运势结果句字号：2026-10-20 用户实测整句糊满屏，双双减半（拼点 4.5→2.25·运势 6→3），
        // ±N 浮字缺省 6 不动（美术口径后置）。
        private const float ClashFontSize = 2.25f;
        private const float LuckRollFontSize = 3f;

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
            _effectDeathHeld.Clear();
            foreach (var a in _creatureArrows.Values)
                if (a != null && a.Inst != null)
                {
                    if (Application.isPlaying) Object.Destroy(a.Inst);
                    else Object.DestroyImmediate(a.Inst);
                }
            _creatureArrows.Clear(); // 钉在 tile 上不随 _root 销毁，须自清
            foreach (var v in _units.Values) v.DestroyStatLabels(); // 格面读数三槽钉 tile，同口径自清（2026-10-26 迁格）
            _units.Clear();
            foreach (var h in _heroes.Values)
                if (h != null && h.Mat != null)
                {
                    if (Application.isPlaying) Object.Destroy(h.Mat);
                    else Object.DestroyImmediate(h.Mat);
                }
            _heroes.Clear(); // 角色柱随 _root 销毁；着色克隆实例（HeroPillar_*）逐柱自清（2026-10-18）
            _heroMats[0] = null; _heroMats[1] = null; // 缓存引用随实例销毁同步清（防 fake-null 复用）
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
            _castPayArriveAt.Clear(); // 扣费粒子到位时刻不跨局（Time.time 基准——陈旧值只会误等）
            foreach (var t in _unitFxTails.Values) t.TrySetResult(); // 链尾收口防悬挂
            _unitFxTails.Clear(); // 单位串行链不跨局
            HideCastingPreview(); // 声明期预览贴片不跨局
            if (_root != null) // 编辑器（验证器冒烟）DestroyImmediate——Destroy 在非 Play 是告警+永久残留
            {
                if (Application.isPlaying) Object.Destroy(_root.gameObject);
                else Object.DestroyImmediate(_root.gameObject);
            }
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
            //      2026-10-26 锚侧定案双口径：半场光条亮**发动方**（栈顶效果归属——对手触发的效果亮对手侧，
            //      人机 AI 同帧让过持有者恒翻己方致旧锚法恒误亮）；中线行时间格角标仍亮**可响应方**
            //      （轮到谁响应/行动）——两信息并存；空栈/网络快照未携带（-1）=光条回落旧口径 ----
            BattlePriorityGlow.Apply(GlowSeatOf(d));
            BattleHudRow.SetTimePriority(PrioritySeatOf(d));

            // ---- 单位区（生物/结界/衍生物） ----
            // 教学介绍段单位闸（2026-10-19 教学二「生物出场」）：闸上=单位不出场（seen 空→存量全清，
            // 重置回介绍段同口径）；解闸后首次对账建单位=生物出场（入场演出随建触发）
            var seenUnits = new HashSet<uint>();
            int createdU = 0, movedU = 0, removedU = 0;
            string lastModel = "";
            if (!BattleRevealDirector.SuppressUnits)
            {
                foreach (var card in d.SelfUnits) ReconcileUnit(card, owner: 0, seenUnits, ref createdU, ref movedU, ref lastModel);
                foreach (var card in d.OppUnits) ReconcileUnit(card, owner: 1, seenUnits, ref createdU, ref movedU, ref lastModel);
            }
            removedU = RemoveStale(_units, seenUnits, _heldUnits);

            // ---- 生物箭头（2026-10-15 用户定案：钉地格不跟 wrapper——攻击/守卫跑动留在原地） ----
            ReconcileCreatureArrows(d);

            // ---- 施放占格类型图标（2026-10-15 发动格拆除：发动区占位卡 × 单位区空格） ----
            ReconcileCasting(d.CastingCards);

            // ---- 地牌（固定元素格；2026-10-28 元素缩编：4 格固定元素身份 灰红蓝绿——黑白退役） ----
            ReconcileLands(d.SelfLands, d.Self, 0);
            ReconcileLands(d.OppLands, d.Opp, 1);

            // ---- 角色立柱 ----
            ReconcileHero(d.Self, 0);
            ReconcileHero(d.Opp, 1);

            // ---- 技能格（2026-10-16 撤销）：英雄技能不再占格——介绍=角色格悬停、发动=角色格三键 ----

            // ---- 特殊格道具对账（卡组卡堆高度随牌库数 0..60 钳制；墓地/除外漩涡常驻自转） ----
            BattleZoneProps.SetDeckCount(0, d.Self.DeckCount);
            BattleZoneProps.SetDeckCount(1, d.Opp.DeckCount);

#if UNITY_EDITOR
            Debug.Log($"[BattleField] 对账：单位 己{d.SelfUnits.Count}/敌{d.OppUnits.Count}"
                      + $"（新建{createdU} 移动{movedU} 销毁{removedU}{(createdU > 0 ? " 最近模型=" + lastModel : "")}）"
                      + $" 地牌 己{d.SelfLands.Count}/敌{d.OppLands.Count}"
                      + $" 地块高亮{BattleTileFx.GlowOn} 元素图标{BattleTileFx.IconCount} 生物箭头{_creatureArrows.Count}"
                      + $" 施放类型图标{d.CastingCards.Count}"
                      + $" 牌库 己{d.Self.DeckCount}/敌{d.Opp.DeckCount}"
                      + $" 生命 己{d.Self.Life}/敌{d.Opp.Life}"
                      + $" 地牌上限 己{d.Self.LandCap}/敌{d.Opp.LandCap}"
                      + $" 钱包(任意) 己{d.Self.WalletByColor[(int)ManaType.Any]}/敌{d.Opp.WalletByColor[(int)ManaType.Any]}");
// 白/黑钱包读段已随 2026-10-28 P2 席位收缩删除（数组现 5 位）
#endif
            _gazeVersion++; // 待机注视缓存作废：单位集合/位次可能已变（事件级重扫）
            return true;
        }

        /// <summary>半场光条座位（2026-10-26 扳机锚侧定案）：栈非空=栈顶效果归属方（**发动方**——
        /// 对手触发的效果亮对手侧）；空栈/网络快照未携带（-1）=沿用旧口径（持有方/回合方）。</summary>
        private static int GlowSeatOf(BattleViewData d)
            => d.StackTopOwnerSeat >= 0 ? d.StackTopOwnerSeat : PrioritySeatOf(d);

        /// <summary>时间格角标座位（可响应方，2026-10-14 旧语义保持）：栈非空=优先权
        /// 持有方（MyPriority 观察者相对口径），栈空=回合方——对账数据单源推导，本地/网络同式。</summary>
        private static int PrioritySeatOf(BattleViewData d)
            => d.StackNotEmpty ? (d.MyPriority ? 0 : 1) : (d.MyTurn ? 0 : 1);

        // ==== 格子按压反馈供料（2026-11-11 木桩测试·BattleTilePress 消费） ====

        /// <summary>收集格面世界锚定挂件（2026-11-11 木桩测试·按压反馈）：单位 wrapper（unit-{rid}）、
        /// 施放占格图标（cast-{rid}）、角色柱（hero-*）世界坐标锚定挂 _root 不钉 tile，按压动画须
        /// 与 tile 同拍同深下沉回升（钉 tile 的挂件——读数三槽/地牌计数条/元素图标/高光环/特殊格
        /// 道具/生物箭头——天然随 tile 动，不在此列）。编舞持有中的单位跳过（位姿归编舞器独占，
        /// 抢写打架）。返回新列表（可 null=该格无世界锚定件）。</summary>
        internal List<Transform> CollectPressAnchors(int x, int z)
        {
            List<Transform> list = null;
            foreach (var kv in _units)
            {
                var v = kv.Value;
                if (v == null || v.Wrapper == null || v.X != x || v.Z != z) continue;
                if (IsHeld(kv.Key)) continue; // 演出持有：位姿归编舞器
                (list ?? (list = new List<Transform>())).Add(v.Wrapper);
            }
            foreach (var kv in _castings)
            {
                var go = kv.Value;
                if (go == null) continue;
                if (!_castingCells.TryGetValue(kv.Key, out var cell) || cell.x != x || cell.y != z) continue;
                (list ?? (list = new List<Transform>())).Add(go.transform);
            }
            foreach (var kv in _heroes)
            {
                var h = kv.Value;
                if (h == null || h.Root == null) continue;
                var heroCell = BoardLayout.CharacterCell(kv.Key);
                if (heroCell.x != x || heroCell.z != z) continue;
                (list ?? (list = new List<Transform>())).Add(h.Root);
            }
            return list;
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
            // 朝向归 stats 自己（2026-10-15 定案）与父层旋转解耦；2026-10-26 用户定案改竖直：
            // 不再俯仰对相机（45° 俯拍机位下 billboard 文字斜躺），只按相机方位角定 yaw——
            // 竖直面直立可读；初始朝向仅作相机未就绪时的兜底。
            var cam = BattleStageDirector.WorldCam;
            if (cam != null)
                go.transform.rotation = StatsBillboard.UprightFacing(go.transform.position, cam.transform.position);
            else
                go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            go.AddComponent<StatsBillboard>();
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.fontSize = 5f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            return tmp;
        }

        /// <summary>头顶文本竖直朝向（2026-10-15 billboard 定案 → 2026-10-26 用户改竖直·不朝向
        /// 相机）：每帧只按相机方位角回正 yaw、俯仰恒 0——文字始终竖立可读；旋转仍是 stats 自己
        /// 的事，与 wrapper（位移/演出旋转）/模型层（对敌朝向）解耦。</summary>
        private sealed class StatsBillboard : MonoBehaviour
        {
            private void LateUpdate()
            {
                var cam = BattleStageDirector.WorldCam;
                if (cam == null) return;
                transform.rotation = UprightFacing(transform.position, cam.transform.position);
            }

            /// <summary>竖直朝向：可读面（TMP 本地 -Z）背向相机方位（与旧 billboard 同口径，仅
            /// 抹平俯仰分量），退化（正上方）时兜底 180°。</summary>
            public static Quaternion UprightFacing(Vector3 fromPos, Vector3 camPos)
            {
                var flat = fromPos - camPos;
                flat.y = 0f;
                return flat.sqrMagnitude < 1e-6f
                    ? Quaternion.Euler(0f, 180f, 0f)
                    : Quaternion.LookRotation(flat.normalized);
            }
        }
    }
}
