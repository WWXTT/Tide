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
        // ======================================== 施放占格类型图标（2026-10-15 发动格拆除） ========================================

        /// <summary>施放卡类型图标对账（发动区占格卡 × 单位区空格——整个 2×6 兼任发动区）。
        /// 2026-10-17 时序定案：离区不即清——转正在场（同帧已入 _units，Apply 单位先于本段）=
        /// linger 至登场演出完成（≈1.15s）；其余（法术结算）=1s 等待期；被阻拦失败=
        /// InterruptCasting 即清（CardActivationFailedEvent）；新卡占同格=顶掉旧 linger。
        /// **S3b 收口（2026-11-05）「栈没有实体」**：CastingCards 恒 X/Z=-1（施放卡不落单位区格）
        /// → 本段不再立任何图标（X<0 continue 全滤），施放呈现由链 UI（StackLines）承载；
        /// stale 清扫照常回收存量。管线保留：未来悬浮标记可复用。</summary>
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
            bool arrived = _units.ContainsKey(id); // 成功转正：登场演出档；否则=法术结算档
            // 法术结算档 3s（2026-10-20 时序定案）：覆盖全程 粒子1.05s+弹体0.35s+浮字0.9s+碎裂开演余量
            //——旧 1s 在弹体起飞前就收贴片（法术位裸奔）；生物登场档不动。
            LingerCastingAsync(id, go, arrived ? 1.15f : 3.0f).Forget();
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

        // ---- 声明期贴片预览 + 法术扣费粒子（2026-10-20 法术发动流程改案） ----

        /// <summary>施放贴片预览：法术声明期目标收集的占格贴片（CreateCastingCard 同款外观/落位）。
        /// 预览名 castprev-{rid} 不占 cast-{rid} 射线路由、碰撞体禁用（预览非正片不可点——
        /// 禁用而非销毁：转正只需回启，规避 Destroy 延迟/假 null 重挂一整类坑）。
        /// 返回 false=无空格/缺件——调用方照常走收集（无贴片不阻塞流程）。</summary>
        public bool ShowCastingPreview(Card card, int x, int z)
        {
            HideCastingPreview();
            if (card == null || x < 0 || z < 0) return false;
            var view = BattleView.FromRuntime(
                CardCore.Serialization.SerializableRuntimeCardState.FromCard(card), card, false);
            view.X = x;
            view.Z = z;
            var go = CreateCastingCard(view);
            if (go == null) return false;
            go.name = "castprev-" + card.RuntimeId;
            var col = go.GetComponent<MeshCollider>();
            if (col != null) col.enabled = false;
            _castPreview = go;
            return true;
        }

        /// <summary>预览贴片转正（声明成功后调用）：直接挂入 _castings——正片对账复用同对象零闪烁
        ///（ReconcileCasting 见已存在不重建，仅校正落位）；声明失败竞态由 stale 清扫 1s linger 兜底回收。
        /// 碰撞体回启并补网格（cast-{rid} 射线路由/hover 归位）——用 == 判空（Unity 假 null 语义：
        /// `??` 是 C# 引用判空，销毁包装类非空会跳过重挂，对死件写属性即 MissingComponentException）。</summary>
        public void PromoteCastingPreview(Card card, int x, int z)
        {
            var go = _castPreview;
            _castPreview = null;
            if (go == null || card == null) return;
            go.name = CastingCardPrefix + card.RuntimeId; // 转正占回射线路由名
            var mc = go.GetComponent<MeshCollider>();
            if (mc == null) mc = go.AddComponent<MeshCollider>();
            mc.enabled = true;
            var mf = go.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null && mc.sharedMesh != mf.sharedMesh)
                mc.sharedMesh = mf.sharedMesh;
            _castings[card.RuntimeId] = go;
            _castingCells[card.RuntimeId] = new Vector2Int(x, z);
        }

        /// <summary>销毁声明期预览贴片（取消=撤销发动路径；Dispose 兜底同口）。</summary>
        public void HideCastingPreview()
        {
            if (_castPreview == null) return;
            DestroyCastingIcon(_castPreview);
            _castPreview = null;
        }

        /// <summary>法术扣费粒子（2026-10-20）：实付色粒子自地格汇聚至施放贴片格——复用出场粒子链
        /// 无模型档（BattleCastArrival.Play modelTemplate=null：跳过消融只播地格→目标汇聚段）。
        /// 零费/无落格跳过；生物不动（CastPlayed 入场汇聚 EnqueueArrival 已覆盖，防双份）。
        /// 同时登记该 seat 的粒子到位时刻——法术伤害演出等它后才从法术位起飞（时序定案）。</summary>
        public void PlayCastPay(int seat, Dictionary<int, float> paid, int x, int z)
        {
            if (paid == null || paid.Count == 0 || x < 0 || z < 0) return;
            _castPayArriveAt[seat] = Time.time + CastPayFlightSeconds;
            BattleCastArrival.Play(0u, seat, paid, CastingAnchor(x, z), null, () => { });
        }

        /// <summary>扣费粒子飞行时长（BattleCastArrival.LandPhase=1s 地格→目标 + 到位余量）——
        /// 法术伤害弹体起飞前等待上限。</summary>
        private const float CastPayFlightSeconds = 1.05f;
        private readonly Dictionary<int, float> _castPayArriveAt = new Dictionary<int, float>(); // seat → 粒子到位时刻

        /// <summary>等待该 seat 的扣费粒子到位于法术位（时序链第 1→2 步）：未登记（零费法术）或
        /// 已过期立即返回；等待期间视觉门保持占用（回放导演逐步等待不抢步）。</summary>
        private async UniTask WaitCastPayArrived(int seat)
        {
            if (seat < 0) return;
            if (!_castPayArriveAt.TryGetValue(seat, out float until) || until <= Time.time) return;
            await UniTask.WaitUntil(() => Time.time >= until);
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
#if UNITY_EDITOR
            Debug.Log($"[BattleField] 施放类型图标：{card.Name}({card.CardId}) @({card.X},{card.Z})（类型 {card.Type}）");
#endif
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
                "BattleCardBack_Elem_White", "Assets/Art/Materials/BattleCardBack_Elem_White_Mat.asset");
            var tex = Tide.HotUpdate.HotUpdateAssets.Load<Texture2D>(
                icon, $"Assets/Art/Icons/{icon}.png");
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

    }
}
