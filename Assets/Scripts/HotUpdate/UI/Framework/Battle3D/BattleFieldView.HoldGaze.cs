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
    public sealed partial class BattleFieldView
    {

        private static int RemoveStale(Dictionary<uint, UnitVisual> map, HashSet<uint> seen, Dictionary<uint, int> held)
        {
            List<uint> stale = null;
            foreach (var kv in map)
                if (!seen.Contains(kv.Key))
                    (stale ?? (stale = new List<uint>())).Add(kv.Key);
            if (stale == null) return 0;
            int removed = 0;
            foreach (var id in stale)
            {
                if (held.TryGetValue(id, out var n) && n > 0)
                    continue; // 演出持有（引用计数>0）：销毁时机归编舞器/吸走演出（下次对账再试）
                var v = map[id];
                // 2026-10-17：wrapper 可能已被外部销毁（重看介绍复现——MissingReferenceException
                // 中断整次对账）：fake-null 视为已退场，只清字典并留痕（owner/模型名供定位来源）。
                if (v.Wrapper != null) Object.Destroy(v.Wrapper.gameObject);
                else Debug.LogWarning($"[BattleField] 对账发现单位 {id % 10000}（owner={v.Owner} model={v.Model?.name}）的 wrapper 已被外部销毁——仅移出字典");
                v.DestroyStatLabels(); // 格面读数钉 tile 不随 wrapper 销毁，须自清（2026-10-26 迁格）
                map.Remove(id);
                removed++;
            }
            return removed;
        }

        // ======================================== 战斗编舞接口（2026-10-13 攻击演出） ========================================

        /// <summary>演出持有（引用计数）：hold 中的单位对账跳过位置/横置侧倾写与销毁（编舞器独占位姿），
        /// 死亡翻转不自动倒地（时机由编舞器在受击时刻驱动）。每次捕获/退场演出各持一票，
        /// 对应 Release 各弃一票（并行演出共享单位互不偷票）；RetireUnit/Dispose 全量清。
        /// hold 泄漏兜底：Dispose 点名日志。</summary>
        public void HoldUnit(uint id)
            => _heldUnits[id] = _heldUnits.TryGetValue(id, out var n) ? n + 1 : 1;

        /// <summary>弃一票持有；计满归零移除。未知 id（已被 RetireUnit 全清/从未持有）静默无操作。</summary>
        public void ReleaseUnit(uint id)
        {
            if (!_heldUnits.TryGetValue(id, out var n)) return;
            if (n <= 1) _heldUnits.Remove(id);
            else _heldUnits[id] = n - 1;
        }

        /// <summary>是否仍被任一持有方 hold（引用计数&gt;0）。</summary>
        private bool IsHeld(uint id) => _heldUnits.TryGetValue(id, out var n) && n > 0;

        /// <summary>单位头顶锚点世界位（2026-10-26 读数迁格后=模型头顶高+0.25：单位动作 3D 按钮组
        /// 取位——按钮落旧读数正上方）。未建/已退场/拆件=null（调用方自行回落）。</summary>
        public Vector3? StatsAnchorOf(uint runtimeId)
            => _units.TryGetValue(runtimeId, out var v) && v.Wrapper != null
                ? v.Wrapper.position + Vector3.up * (v.StatsHeightY + 0.25f)
                : (Vector3?)null;

        /// <summary>角色柱头顶数值锚点世界位（同 StatsAnchorOf 口径；未建柱/拆件=null）。</summary>
        public Vector3? HeroStatsAnchor(int owner)
            => _heroes.TryGetValue(owner, out var h) && h.Stats != null
                ? h.Stats.transform.position + Vector3.up * 0.25f
                : (Vector3?)null;

        // ======================================== 头注视（2026-10-15 战场接线） ========================================

        // 待机注视缓存版本（2026-10-18 性能）：Apply 尾递增——最近敌人选择改事件级重扫，
        // 帧间只取缓存目标的实时位置（单位走位仍逐帧跟踪），全场 O(N²)/帧 → O(N)/事件
        private int _gazeVersion;

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
            if (v.IdleGazeVer != _gazeVersion)
            {
                v.IdleGazeVer = _gazeVersion;
                v.IdleGazeEnemy = null;
                Vector3 self = v.Wrapper.position;
                float bestSqr = float.MaxValue;
                foreach (var kv in _units)
                {
                    var o = kv.Value;
                    if (o == v || !o.IsCreature || o.Owner == v.Owner || o.Dead || o.Wrapper == null) continue;
                    float sqr = (o.Wrapper.position - self).sqrMagnitude;
                    if (sqr < bestSqr) { bestSqr = sqr; v.IdleGazeEnemy = o; }
                }
            }
            var cached = v.IdleGazeEnemy;
            if (cached != null && !cached.Dead && cached.Wrapper != null) // 对账间隙死亡/离场兜底
                return cached.Wrapper.position + Vector3.up * cached.LookHeight;
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

        // ======================================== 单位级表现串行链（2026-10-20 定案） ========================================

        // 用户定案：**同一单位上的表现串行，不同单位并行**。法术施放四段链
        //（贴牌=发动前 → 费用粒子飞到卡位 → 效果粒子到目标=效果处理中串行 → 死亡=效果后）
        // 的③④段靠本链保序：载荷（付费/伤害/死亡）在抑制开启前或倾泻 drain 下同帧爆发、
        // 各自 fire-and-forget，只有"启动顺序"没有"执行顺序"——链把同单位的目标类演出
        //（伤害/回复/碎裂/消灭）排成队，逐个 await 前驱完成后才开演。
        private readonly Dictionary<uint, UniTaskCompletionSource> _unitFxTails = new Dictionary<uint, UniTaskCompletionSource>();

        /// <summary>在单位 rid 的串行链尾追加一个表现操作：await 前驱完成后执行 op；
        /// 无论成败都完成自己的尾票（后续不悬挂）。跨单位天然并行（各持各链）。
        /// 调用方负责在链外层持视觉门（BeginVisual 包住排队等待全程——回放导演逐步等待覆盖整链）。</summary>
        private async UniTask RunUnitFxAsync(uint rid, Func<UniTask> op)
        {
            UniTask prev = default; // default UniTask=同步完成（无前驱）
            if (_unitFxTails.TryGetValue(rid, out var tail)) prev = tail.Task;
            var mine = new UniTaskCompletionSource();
            _unitFxTails[rid] = mine;
            try
            {
                await prev;
                if (op != null) await op();
            }
            finally
            {
                if (_unitFxTails.TryGetValue(rid, out var cur) && ReferenceEquals(cur, mine))
                    _unitFxTails.Remove(rid);
                mine.TrySetResult();
            }
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

        /// <summary>单位退场（吸走演出收尾）：销毁 wrapper+格面读数件+出字典+清 hold（幂等）。</summary>
        public void RetireUnit(uint id)
        {
            _heldUnits.Remove(id);
            if (_units.TryGetValue(id, out var v))
            {
                if (v.Wrapper != null) Object.Destroy(v.Wrapper.gameObject);
                v.DestroyStatLabels();
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

        /// <summary>全量释放（演出收尾/取消/Dispose 兜底）：任何路径不漏冻结（单位+角色柱）。</summary>
        public void ReleaseAllHeldStats()
        {
            foreach (var v in _units.Values) v.ReleaseStatsHold();
            ReleaseAllHeroStats();
        }

        // ======================================== 角色柱HP读数节拍（2026-10-20：镜像单位 StatsHeld——攻击步快照终值缓冲，命中拍落地） ========================================

        /// <summary>2D 血条同拍回调（owner, 展示生命）：释放时显示值真变才触发（BattleScreen 只重写生命标签）。</summary>
        public Action<int, int> HeroLifeShownChanged;

        /// <summary>战斗攻击步冻结双方角色柱HP读数（回放导演在应用攻击步快照前调）：对账照常
        /// 缓冲目标文本但展示不动——编舞器命中节拍释放（角色目标与立柱脉冲同拍，溅射波及角色
        /// 同拍覆盖），演出收尾/回放兜底全量放行。宣言帧不冻：拼点/宣言期奖励伤害照常直显
        ///（2026-10-20 用户定案「先24再23」——中段值宣言帧落地，战斗伤害等命中拍）。</summary>
        public void HoldAllHeroStats()
        {
            foreach (var v in _heroes.Values) v.StatsHeld = true;
        }

        /// <summary>命中节拍/兜底释放全部角色柱HP读数（编舞器主命中/演出收尾/回放兜底调）。</summary>
        public void ReleaseAllHeroStats()
        {
            foreach (var kv in _heroes)
            {
                int before = kv.Value.ShownLife;
                kv.Value.ReleaseStatsHold();
                if (kv.Value.ShownLife != before)
                {
                    Debug.Log($"[HeroStatBeat] 角色{kv.Key} HP读数落地 {before} → {kv.Value.ShownLife}");
                    HeroLifeShownChanged?.Invoke(kv.Key, kv.Value.ShownLife);
                }
            }
        }

        /// <summary>角色柱当前展示生命（FillBar 节拍取值）：未建柱/首写前 false（调用方回落数据值）；
        /// 冻结期返回旧展示值——堵住 ApplyProjection→RefreshBars 的提前直出。</summary>
        public bool TryGetHeroLifeShown(int owner, out int life)
        {
            life = 0;
            return _heroes.TryGetValue(owner, out var v) && (life = v.ShownLife) != int.MinValue;
        }

        // ======================================== 生物箭头（钉地） ========================================

        /// <summary>生物卡面箭头对账（2026-10-15 用户定案）：ArrowDirs≠0 的生物（链接生物）在
        /// 数据格 tile 上铺六向三角（复用结界 prefab 的 arrows 件；锥隐藏、环恒灭）——挂 tile 不挂
        /// wrapper，攻击/守卫拦截跑动不跟走，移动/终局随全量对账重建。镜像口径同结界：根旋转复刻
        /// 旧 wrapper 半场朝向（己方 180/对方 0）+ Apply 的对手 Opposite 镜像。</summary>
        private void ReconcileCreatureArrows(BattleViewData d)
        {
            _arrowSeen.Clear();
            if (BattleBoardGrid.Root != null && !BattleRevealDirector.SuppressUnits)
            {
                ReconcileCreatureArrowSet(d.SelfUnits, 0);
                ReconcileCreatureArrowSet(d.OppUnits, 1);
            } // 单位闸上=箭头全收（_arrowSeen 空→stale 清理兜底；2026-10-19 教学二段一）
            // 离场/失格单位销毁（对账级；留存只为跨事件复用——原全量销毁重建的每事件 Instantiate 风暴根除）
            _arrowStale.Clear();
            foreach (var kv in _creatureArrows)
                if (!_arrowSeen.Contains(kv.Key)) _arrowStale.Add(kv.Key);
            foreach (var rid in _arrowStale)
            {
                var a = _creatureArrows[rid];
                if (a.Inst != null)
                {
                    if (Application.isPlaying) Object.Destroy(a.Inst);
                    else Object.DestroyImmediate(a.Inst);
                }
                _creatureArrows.Remove(rid);
            }
        }

        private void ReconcileCreatureArrowSet(List<BattleCardView> units, int owner)
        {
            var prefab = BattleEnchantModel.Load();
            if (prefab == null) return; // 缺失告警由 Load 落（与结界路径同源一次）
            foreach (var card in units)
            {
                if (card.ArrowDirs == 0 || card.Type == Cardtype.Enchantment) continue; // 结界三件自带
                if (card.IsDead || card.X < 0 || card.Z < 0) continue;
                _arrowSeen.Add(card.RuntimeId);
                if (_creatureArrows.TryGetValue(card.RuntimeId, out var entry) && entry.Inst != null)
                {
                    if (entry.X == card.X && entry.Z == card.Z && entry.Dirs == card.ArrowDirs && entry.Owner == owner)
                        continue; // 未变：留存直用（常见路径=静止在场）
                    var movedTile = BattleTileFx.TileOf(card.X, card.Z);
                    if (movedTile == null) continue;
                    entry.Inst.transform.SetParent(movedTile, false);
                    entry.Inst.transform.localPosition = new Vector3(0f, 0.006f, 0f);
                    entry.Inst.transform.localRotation = owner == 0 ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;
                    entry.Inst.GetComponent<BattleEnchantFx>()?.Apply((HexDirection)card.ArrowDirs, owner == 1, false);
                    entry.X = card.X; entry.Z = card.Z; entry.Dirs = card.ArrowDirs; entry.Owner = owner;
                    continue;
                }

                var tile = BattleTileFx.TileOf(card.X, card.Z);
                if (tile == null) continue;
                var inst = Object.Instantiate(prefab, tile, false);
                inst.name = "fx-creature-arrows";
                inst.transform.localPosition = new Vector3(0f, 0.006f, 0f);
                inst.transform.localRotation = owner == 0 ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;
                inst.GetComponent<BattleEnchantFx>()?.Apply((HexDirection)card.ArrowDirs, owner == 1, false);
                var cone = inst.transform.Find("cone");
                if (cone != null) cone.gameObject.SetActive(false);
                _creatureArrows[card.RuntimeId] = new ArrowEntry { Inst = inst, X = card.X, Z = card.Z, Dirs = card.ArrowDirs, Owner = owner };
            }
        }

        /// <summary>模型层对敌本地偏航（公开：编舞器走位/转面补偿用）：
        /// 模型烘焙前向=本地 +Z（2026-10-15 用户定案：头朝向即 +Z 朝向，以「攻击动作是前拱」为判据
        /// 手工校准，反向个体已删除；与 RigDriver「移动方向恒根前向 +Z」契约一致，推翻同日 −Z 二次定案）。
        /// 己方(P1，低 z 半场) 0 / 对方(P2) 180 正对敌半场。
        /// wrapper 恒 identity，走位期 wrapper 转向=期望世界朝向(Atan2)−本值。</summary>
        public static float FacingYawOf(int owner) => owner == 0 ? 0f : 180f;
    }
}
