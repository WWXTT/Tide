using System.Collections.Generic;
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
    /// （结构=wrapper[位姿/横置倾斜/头顶数值] → 模型根[MonsterRigAnimator]——wrapper 承载位移，
    /// 不触发 Recapture）；死亡→倒地散落、复活→Revive；横置→wrapper 倾斜提示。
    /// 地牌→无实体（2026-10-13 卡背退役定案）：地块锁色菲涅尔+格面元素图标（BattleTileFx）；
    /// 可行动性（生物未横置）与地牌状态均由脚下地块表达（BattleTileFx 同日改版）。
    /// 角色→占位立柱+生命值；模型映射走 MonsterModelResolver（同名→稳定哈希→占位胶囊）。
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
        private readonly Dictionary<int, TextMeshPro> _tempWallets = new Dictionary<int, TextMeshPro>(); // key=归属 0/1
        private readonly HashSet<uint> _heldUnits = new HashSet<uint>(); // 演出持有（2026-10-13 战斗编舞）
        private int _lastSkipLogFrame = -1;

        private static readonly Color P1UnitColor = new Color(0.29f, 0.50f, 0.83f);
        private static readonly Color P2UnitColor = new Color(0.83f, 0.35f, 0.35f);

        public BattleFieldView()
        {
            _root = new GameObject("BattleFieldView").transform;
        }

        public void Dispose()
        {
            if (_heldUnits.Count > 0)
                Debug.LogWarning($"[BattleField] Dispose 时仍有 {_heldUnits.Count} 个演出 hold 未释放（正常应在编舞 finally 清空）——随 Dispose 一并丢弃");
            _heldUnits.Clear();
            _units.Clear();
            _heroes.Clear();
            _tempWallets.Clear();
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

            // ---- 地块特效（全熄后随对账按数据重点亮；菲涅尔壳留存复用） ----
            BattleTileFx.ClearAll();

            // ---- 单位区（生物/结界/衍生物） ----
            var seenUnits = new HashSet<uint>();
            int createdU = 0, movedU = 0, removedU = 0;
            string lastModel = "";
            foreach (var card in d.SelfUnits) ReconcileUnit(card, owner: 0, seenUnits, ref createdU, ref movedU, ref lastModel);
            foreach (var card in d.OppUnits) ReconcileUnit(card, owner: 1, seenUnits, ref createdU, ref movedU, ref lastModel);
            removedU = RemoveStale(_units, seenUnits, _heldUnits);

            // ---- 地牌（元素池；无实体——地块菲涅尔+元素图标，BattleTileFx） ----
            var seenLands = new HashSet<uint>();
            foreach (var card in d.SelfLands) ReconcileLand(card, seenLands);
            foreach (var card in d.OppLands) ReconcileLand(card, seenLands);

            // ---- 角色立柱 ----
            ReconcileHero(d.Self, 0);
            ReconcileHero(d.Opp, 1);

            // ---- 临时元素格钱包（2026-10-13 临时元素上盘：地牌行左端格就地显示余额） ----
            ReconcileTempWallet(d.Self, 0);
            ReconcileTempWallet(d.Opp, 1);

            // ---- 特殊格道具对账（卡组卡堆高度随牌库数 0..60 钳制；墓地/除外漩涡常驻自转） ----
            BattleZoneProps.SetDeckCount(0, d.Self.DeckCount);
            BattleZoneProps.SetDeckCount(1, d.Opp.DeckCount);

            Debug.Log($"[BattleField] 对账：单位 己{d.SelfUnits.Count}/敌{d.OppUnits.Count}"
                      + $"（新建{createdU} 移动{movedU} 销毁{removedU}{(createdU > 0 ? " 最近模型=" + lastModel : "")}）"
                      + $" 地牌 己{d.SelfLands.Count}/敌{d.OppLands.Count}"
                      + $" 地块高亮{BattleTileFx.GlowOn} 元素图标{BattleTileFx.IconCount}"
                      + $" 牌库 己{d.Self.DeckCount}/敌{d.Opp.DeckCount}"
                      + $" 生命 己{d.Self.Life}/敌{d.Opp.Life}"
                      + $" 临时钱包 己{d.Self.BankText}/敌{d.Opp.BankText}");
            return true;
        }

        // ======================================== 单位 ========================================

        private sealed class UnitVisual
        {
            public Transform Wrapper;
            public GameObject Model;
            public MonsterRigAnimator Anim;   // 占位胶囊时 null
            public TextMeshPro Stats;
            public int X, Z;
            public bool Dead;
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

            // 横置=wrapper 侧倾提示（回合开始恢复直立——对账幂等直写；hold 中归编舞器）
            if (!_heldUnits.Contains(card.RuntimeId))
                v.Wrapper.rotation = Quaternion.Euler(0f, FacingYaw(owner), card.IsTapped ? 24f : 0f);

            // 可行动高光（2026-10-13 卡背退役定案·晚改扩散波）：未横置未死亡=脚下地块淡蓝扩散波，横置/死亡即熄
            if (!card.IsTapped && !card.IsDead)
                BattleTileFx.SetUnitGlow(card.X, card.Z);

            // 死亡↔复活（MonsterRig 基础态）：死亡翻转只落一次记账（v.Dead 锁存），
            // hold 中不自动倒地（战斗编舞在受击时刻驱动 FallDown 时机）；非 hold 的死亡
            // 走通用死亡演出（碎裂→墓地漩涡吸走，2026-10-13 定案③）
            if (card.IsDead && !v.Dead)
            {
                v.Dead = true;
                if (!_heldUnits.Contains(card.RuntimeId))
                {
                    v.Anim?.Play(MonsterAction.FallDown);
                    PlayDeathSuctionAsync(card.RuntimeId, owner, playFall: false).Forget();
                }
                // hold 中：记账已锁存，倒地时机归编舞器（数值/体型照常刷新）
            }
            else if (!card.IsDead && v.Dead)
            {
                v.Dead = false;
                v.Anim?.Revive();
                v.Anim?.Play(MonsterAction.Breath);
            }

            if (v.Stats != null && card.Runtime != null)
                v.Stats.text = card.Runtime.Power + "/" + card.Runtime.Life;
        }

        private UnitVisual CreateUnit(BattleCardView card, int owner)
        {
            var wrapper = new GameObject("unit-" + card.RuntimeId % 10000);
            wrapper.transform.SetParent(_root, false);
            wrapper.transform.position = UnitAnchor(card.X, card.Z);
            wrapper.transform.rotation = Quaternion.Euler(0f, FacingYaw(owner), 0f);

            GameObject model;
            MonsterRigAnimator anim = null;
            Vector3 baseScale = Vector3.one, basePos = Vector3.zero;
            var prefab = MonsterModelResolver.Resolve(card.CardId, card.Name, card.Illustration);
            if (prefab != null)
            {
                Debug.Log($"[BattleField] 单位 {card.Name}({card.CardId}) 模型={MonsterModelResolver.LastPolicy}");
                model = Object.Instantiate(prefab, wrapper.transform);
                model.name = prefab.name; // 去 (Clone)：目录名/日志一致性
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one; // 体型已烘进资产（模块数分档 0.7/1/1.5），运行时不缩放
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
            float textY = 1.35f;
            var unitRenderers = model.GetComponentsInChildren<Renderer>();
            if (unitRenderers.Length > 0)
            {
                var b = unitRenderers[0].bounds;
                for (int ri = 1; ri < unitRenderers.Length; ri++) b.Encapsulate(unitRenderers[ri].bounds);
                textY = b.max.y - wrapper.transform.position.y + 0.35f;
            }

            var v = new UnitVisual
            {
                Wrapper = wrapper.transform,
                Model = model,
                Anim = anim,
                X = card.X,
                Z = card.Z,
                Dead = card.IsDead,
            };
            if (card.IsDead) anim?.Play(MonsterAction.FallDown);
            v.Stats = CreateStatsText(wrapper.transform, textY);
            return v;
        }

        /// <summary>单位站格锚点：格心 + 少量抬高（防 z-fight）。</summary>
        private static Vector3 UnitAnchor(int x, int z)
            => BattleBoardGrid.WorldPositionOf(x, z) + new Vector3(0f, 0.05f, 0f);

        /// <summary>朝向：模型默认面 +Z；己方(P1，高 z 半场)面向 -Z、对方(P2)面向 +Z。</summary>
        private static float FacingYaw(int owner) => FacingYawOf(owner);

        // ======================================== 地牌 ========================================

        /// <summary>地牌无实体（2026-10-13 卡背退役定案）：状态全由脚下地块表达——
        /// 未横置=锁色菲涅尔+格面元素图标（叠层错位两张）；横置（已产元素）即全熄
        /// （Apply 开头 ClearAll 已清，此卡不点亮）。无逐卡状态，幂等无需字典。</summary>
        private static void ReconcileLand(BattleCardView card, HashSet<uint> seen)
        {
            seen.Add(card.RuntimeId);
            if (card.X < 0 || card.Z < 0) return; // 未落格（异常数据）
            if (card.IsTapped) return;

            int locked = card.Runtime != null ? card.Runtime.LockedColor : -1;
            BattleTileFx.SetLandGlow(card.X, card.Z, locked);
            BattleTileFx.AddElement(card.X, card.Z, locked, card.Stacked);
        }

        // ======================================== 角色 ========================================

        private sealed class HeroVisual
        {
            public Transform Root;
            public TextMeshPro Stats;
            public int LastLife = -1;
        }

        private void ReconcileHero(BattlePlayerView p, int owner)
        {
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
            if (v.Stats != null && v.LastLife != p.Life)
            {
                v.LastLife = p.Life;
                v.Stats.text = "HP " + p.Life;
            }
        }

        // ======================================== 临时元素格 ========================================

        /// <summary>临时元素格钱包余额（2026-10-13 临时元素上盘定案）：临时元素格上方世界 TMP，
        /// 文本=BattlePlayerView.BankText（与 HUD「临时元素」条同源同格式），空（"—"）即整条隐藏；
        /// 钱包入账/支付/回合清零事件已接 RefreshField，对账幂等直写。</summary>
        private void ReconcileTempWallet(BattlePlayerView p, int owner)
        {
            if (!EnsureTmpFont()) return;
            if (!_tempWallets.TryGetValue(owner, out var label) || label == null)
            {
                var cell = BoardLayout.TempElementCell(owner);
                var go = new GameObject("temp-wallet-" + (owner == 0 ? "self" : "opp"));
                go.transform.SetParent(_root, false);
                go.transform.position = BattleBoardGrid.WorldPositionOf(cell.x, cell.z) + new Vector3(0f, 0.3f, 0f);
                // 朝向=世界相机（同 CreateStatsText 口径；相机未就绪回落固定朝向）
                var cam = BattleStageDirector.WorldCam;
                go.transform.rotation = cam != null
                    ? Quaternion.LookRotation(go.transform.position - cam.transform.position)
                    : Quaternion.Euler(0f, 180f, 0f);
                label = go.AddComponent<TextMeshPro>();
                label.fontSize = 3.5f;
                label.alignment = TextAlignmentOptions.Center;
                label.color = Color.white;
                _tempWallets[owner] = label;
            }

            bool any = !string.IsNullOrEmpty(p.BankText) && p.BankText != "—";
            label.gameObject.SetActive(any);
            if (any) label.text = "临时 " + p.BankText;
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
            // 朝向=世界相机（2026-10-13 用户反馈：固定朝 -Z 斜俯拍下侧视不可读；固定机位一次性朝向即可，
            // 相机未就绪回落旧朝向）
            var cam = BattleStageDirector.WorldCam;
            if (cam != null)
                go.transform.rotation = Quaternion.LookRotation(go.transform.position - cam.transform.position);
            else
                go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.fontSize = 5f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            return tmp;
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
                Object.Destroy(map[id].Wrapper.gameObject);
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

        /// <summary>朝向（公开：编舞器回程复原用）：模型默认面 +Z；己方(P1)面向 -Z、对方(P2)面向 +Z。</summary>
        public static float FacingYawOf(int owner) => owner == 0 ? 180f : 0f;

        /// <summary>死亡演出（战斗编舞与通用死亡同路径，2026-10-13 定案③）：倒地散落→停稳→
        /// 螺旋吸入己方墓地漩涡→销毁。playFall=false=倒地已播过（对账翻转路径）。
        /// 中途复活（v.Dead 翻回）/拆屏即中止并释放 hold；吸完 RetireUnit 自清。</summary>
        public async UniTask PlayDeathSuctionAsync(uint id, int owner, bool playFall,
            float settleSeconds = 1f, float suctionSeconds = 0.75f)
        {
            if (!_units.TryGetValue(id, out var v) || v.Wrapper == null) return;
            HoldUnit(id); // 防 RemoveStale 抢销毁
            if (playFall) v.Anim?.Play(MonsterAction.FallDown);

            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < settleSeconds)
            {
                if (v.Wrapper == null || !v.Dead) { ReleaseUnit(id); return; }
                await UniTask.Yield();
            }
            if (v.Wrapper == null) { ReleaseUnit(id); return; }
            if (!v.Dead) { ReleaseUnit(id); return; } // 复活竞争：交还对账

            var graveCell = BattleZoneProps.GraveCellOf(owner);
            var grave = BattleBoardGrid.WorldPositionOf(graveCell.x, graveCell.z);
            await SuckToVortexAsync(v, grave, suctionSeconds);
            if (v.Wrapper != null && v.Dead)
            {
                Debug.Log($"[BattleField] 单位 {id % 10000} 已被墓地漩涡吸走（owner={owner}）");
                RetireUnit(id);
            }
            else ReleaseUnit(id);
        }

        /// <summary>螺旋吸入：绕吸入口加速盘旋收缩、整体翻滚缩小到 0；复活竞争/销毁即停并复原体型。</summary>
        private static async UniTask SuckToVortexAsync(UnitVisual v, Vector3 gravePos, float seconds)
        {
            Vector3 start = v.Wrapper.position;
            Vector3 startScale = v.Wrapper.localScale;
            const float startRadius = 0.55f;
            float t0 = Time.realtimeSinceStartup;
            while (v.Wrapper != null && v.Dead)
            {
                float t = (Time.realtimeSinceStartup - t0) / seconds;
                if (t >= 1f) break;
                float k = t * t; // 加速吸入
                Vector3 to = Vector3.Lerp(start, gravePos, k);
                float ang = t * Mathf.PI * 2f * 2.2f; // 约 2.2 圈
                float r = startRadius * (1f - k);
                v.Wrapper.position = to + new Vector3(Mathf.Cos(ang) * r, 0.08f + 0.3f * (1f - k), Mathf.Sin(ang) * r);
                v.Wrapper.rotation = Quaternion.Euler(t * 380f, ang * Mathf.Rad2Deg, t * 520f); // 翻滚
                v.Wrapper.localScale = startScale * Mathf.Max(1f - k, 0.02f);
                await UniTask.Yield();
            }
            if (v.Wrapper != null) v.Wrapper.localScale = startScale; // 复活竞争复原（销毁路径无谓）
        }

        /// <summary>角色受击脉动（2026-10-13 裁定②：角色目标无模型，立柱缩放脉冲表受击）。</summary>
        public async UniTask HeroImpactPulseAsync(int owner)
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
    }
}
