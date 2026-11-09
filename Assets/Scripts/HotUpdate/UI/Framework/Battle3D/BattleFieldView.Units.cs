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
        // ======================================== 单位 ========================================

        private sealed class UnitVisual
        {
            public Transform Wrapper;
            public GameObject Model;
            public MonsterRigAnimator Anim;   // 占位胶囊时 null
            // 格面三槽读数（2026-10-26 头顶数值迁格定案）：生物=左攻/右血两槽（旧 Attack/Defense
            // 键位）、无生命单位（结界）=中槽耐久（旧 Activate 键位）——三槽钉 tile 平贴格面，
            // 随对账数据格迁移（箭头同口径），死亡隐藏/复活恢复
            public TextMeshPro StatAtk, StatLife, StatMid;
            public float StatsHeightY = 1.35f; // 头顶锚高（包围盒顶+余量·旧读数位）：浮字基线/动作按钮锚用
            public int X, Z;
            public bool Dead;
            public bool DeathExitStarted;    // 碎裂退场已开演（2026-10-20 时序定案：效果伤害受击判据——
                                             // Dead 会被 HoldForEffectDeath 在死亡事件期早锁，链化后伤害
                                             // 演出执行时 Dead 常 true 但碎裂尚未开演=受击照放）
            public int Owner;                 // 归属 0/1（头注视敌我分辨）
            public float ModelYaw;            // 模型层对敌本地偏航（2026-10-15 定案：朝向归模型层；结界=箭头净朝向 180/0）
            public bool IsCreature = true;    // 生物才作注视目标（结界三件模型不参与对视）
            public float LookHeight = 0.6f;   // 注视点高度（包围盒胸口位估算）
            public Func<Vector3?> LookPin;    // 战斗注视钉（编舞器）：非空优先于待机注视
            public int IdleGazeVer;           // 待机注视缓存版本（_gazeVersion 对账递增——版本不符才重扫最近敌人）
            public UnitVisual IdleGazeEnemy;  // 缓存的最近敌方生物（null=无→回落角色立柱）
            public UnitCardPlate CardPlate;   // 脚下卡背指示牌（2026-11-03 横置视觉：竖直=可用/横置=旋90°；仅生物挂载）

            // —— 头顶数值节拍（2026-10-15 定案：攻击结算同帧改血，展示等命中时刻）——
            // Shown=当前展示值（滚动起点/逐帧推进）；Target=对账目标值（冻结期只缓冲）；
            // StatsVersion=滚动作废令牌（新目标即作废在途滚动，从当前展示值续走）
            public int ShownPower = int.MinValue, ShownLife = int.MinValue;
            public int TargetPower, TargetLife;
            public int ShownArmor = int.MinValue, TargetArmor; // 护甲同步走节拍（2026-10-16：吸收与命中同拍滚动）
            public int? BasePower, BaseLife;
            public int StatsVersion;
            public bool StatsHeld;            // 战斗演出数值冻结：命中节拍释放并滚动落值
            public bool DurabilityOnly;       // 无生命单位（结界，2026-10-16）：只显耐久值（中槽），不显 攻/血 对（侧槽）

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
                while (Wrapper != null && StatsVersion == ver)
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
                // 无生命单位（结界）：Life 即耐久层数，写中槽；生物攻/血写左右侧槽（2026-10-26 迁格）
                if (DurabilityOnly)
                {
                    if (StatMid != null) StatMid.text = StatSpan(l, BaseLife) + ArmorSuffix(a);
                }
                else
                {
                    if (StatAtk != null) StatAtk.text = StatSpan(p, BasePower);
                    if (StatLife != null) StatLife.text = StatSpan(l, BaseLife) + ArmorSuffix(a);
                }
            }

            /// <summary>格面读数显隐：死亡碎裂同帧即隐，复活由对账恢复（2026-10-16 口径随迁）。
            /// 隐期间 WriteStats 照常写 text（组件在失活 GO 上仍可写），恢复显示即最新值。</summary>
            public void SetStatsVisible(bool on)
            {
                if (StatAtk != null && StatAtk.gameObject.activeSelf != on) StatAtk.gameObject.SetActive(on);
                if (StatLife != null && StatLife.gameObject.activeSelf != on) StatLife.gameObject.SetActive(on);
                if (StatMid != null && StatMid.gameObject.activeSelf != on) StatMid.gameObject.SetActive(on);
            }

            /// <summary>数据格迁移：三槽重钉新 tile（演出 hold 只锁 wrapper 位姿——读数与箭头同口径
            /// 不等演出；已在目标 tile=幂等无操作；tile null=跳过待下轮对账）。</summary>
            public void ReparentStats(Transform tile)
            {
                if (tile == null) return;
                if (StatAtk != null && StatAtk.transform.parent != tile) StatAtk.transform.SetParent(tile, false);
                if (StatLife != null && StatLife.transform.parent != tile) StatLife.transform.SetParent(tile, false);
                if (StatMid != null && StatMid.transform.parent != tile) StatMid.transform.SetParent(tile, false);
            }

            /// <summary>销毁格面读数件（RemoveStale/RetireUnit/Dispose：三槽钉 tile 不随 wrapper 销毁，须自清）。</summary>
            public void DestroyStatLabels()
            {
                if (StatAtk != null) { Object.Destroy(StatAtk.gameObject); StatAtk = null; }
                if (StatLife != null) { Object.Destroy(StatLife.gameObject); StatLife = null; }
                if (StatMid != null) { Object.Destroy(StatMid.gameObject); StatMid = null; }
            }
        }

        private void ReconcileUnit(BattleCardView card, int owner,
            HashSet<uint> seen, ref int created, ref int moved, ref string lastModel)
        {
            seen.Add(card.RuntimeId);
            if (card.X < 0 || card.Z < 0)
            {
                // 2026-10-21 段零召唤物无视觉取证：视图数据无格=BoardState._cellOf 缺该卡。
                // 埋点输出卡身份/运行区——下一轮复测据此分辨（钉无效/未Resync/满格/链未达）。
                TideLog.Warn($"[BattleField] 单位 {card.Name}#{card.RuntimeId} 未落格 X={card.X}——对账跳过建视觉");
                return;
            }

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
                if (!IsHeld(card.RuntimeId))
                {
                    v.Wrapper.position = UnitAnchor(card.X, card.Z);
                    v.X = card.X;
                    v.Z = card.Z;
                    moved++;
                }
                // 演出持有（2026-10-13 战斗编舞）：位姿归编舞器独占——不写位置也不改记账，
                // 释放后对账按差量重新落笔
                // 格面读数钉格不跟 wrapper（箭头同口径）：数据格已迁即重钉，不等演出
                v.ReparentStats(BattleTileFx.TileOf(card.X, card.Z));
            }

            // 朝向架构（2026-10-15 用户定案·二次修订）：模型层持对敌朝向（己方0/对手180）、stats 自
            // billboard、wrapper 恒 identity（横置 24° 侧倾同日退役；2026-11-03 起横置由脚下卡背牌
            // 朝向表达（UnitCardPlate：竖直=可用/横置=旋 90°）；走位/演出期 wrapper 转向=期望世界
            // 朝向−ModelYaw，归编舞器）；hold 中归编舞器
            if (!IsHeld(card.RuntimeId))
            {
                v.Wrapper.rotation = Quaternion.identity;
                if (v.Model != null)
                    v.Model.transform.localRotation = Quaternion.Euler(0f, v.ModelYaw, 0f);
            }

            // 横置视觉（2026-11-03 波语义重定义）：旧「未横置=脚下扩散波常亮」整链退役——
            // 横置状态由卡背牌旋转表达；波改为目标选择期的候选区域专属标志（选择器逐帧回补，
            // 常态对账不再点亮，见 BattleTileFx.SetUnitGlow）
            v.CardPlate?.SetTapped(card.IsTapped);

            // 死亡↔复活（MonsterRig 基础态）：死亡翻转只落一次记账（v.Dead 锁存）；
            // 非 hold 的死亡走通用退场演出（生物=碎裂散落原地沉降片刻即收、结界等无 rig
            // 单位整身沉降入板面——2026-10-17 砍掉滚墓+自旋段，退场不再去墓地格）
            if (card.IsDead && !v.Dead)
            {
                v.Dead = true;
                v.CardPlate?.SetVisible(false); // 卡背随死亡收起（复活随 v.Dead 翻转恢复）
                if (!IsHeld(card.RuntimeId))
                    PlayDeathExitAsync(card.RuntimeId, owner).Forget();
                // hold 中：记账已锁存，退场时机归编舞器⑤（数值/体型照常刷新）
            }
            else if (!card.IsDead && v.Dead)
            {
                v.Dead = false;
                v.CardPlate?.SetVisible(true);
                v.SetStatsVisible(true); // 死亡碎裂时隐去的格面读数随复活恢复（2026-10-26 迁格）
                v.Anim?.Revive();
                v.Anim?.Play(MonsterAction.Breath);
            }

            if (card.Runtime != null)
            {
                // 无生命单位（结界/装备，DurabilityOnly 同口径）：生命位喂耐久层数——_life 恒 0，
                // 耐久真源=指示物（2026-10-16 修；持续型结界无层=显 0）
                int statLife = card.Type != Cardtype.Creature
                    ? card.Runtime.Counters?.FirstOrDefault(c => c.Key == CardCore.Attribute.CounterRules.DurabilityCounter)?.Value ?? 0
                    : card.Runtime.Life;
                v.SetStatsValues(card.Runtime.Power, statLife, card.Armor, card.BasePower, card.BaseLife);
            }
                // 数值节拍（2026-10-15）：不再直写——冻结期缓冲、命中节拍释放、变化滚动过渡
                //（2026-10-26 迁格：节拍数学不变，落笔对象=格面三槽）
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
#if UNITY_EDITOR
                Debug.Log($"[BattleField] 结界 {card.Name}({card.CardId}) prefab 缺失→占位胶囊");
#endif
            }

            var prefab = MonsterModelResolver.Resolve(card.CardId, card.Name, card.Illustration);
            if (prefab != null)
            {
#if UNITY_EDITOR
                Debug.Log($"[BattleField] 单位 {card.Name}({card.CardId}) 模型={MonsterModelResolver.LastPolicy}");
#endif
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
#if UNITY_EDITOR
                Debug.Log($"[BattleField] 单位 {card.Name}({card.CardId}) 无模型→占位胶囊（清单空或 prefab 加载失败）");
#endif
                model = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                Object.Destroy(model.GetComponent<Collider>());
                model.transform.SetParent(wrapper.transform, false);
                baseScale = new Vector3(0.7f, 0.9f, 0.7f);
                basePos = new Vector3(0f, 0.9f, 0f);
                model.transform.localScale = baseScale;
                model.transform.localPosition = basePos;
                model.GetComponent<Renderer>().material.color = owner == 0 ? P1UnitColor : P2UnitColor;
            }

            // 生物模型不投真影（2026-10-26 用户定案：光追阴影闪烁收口）——RTAS/阴影图双路按
            // shadowCastingMode=Off 自动排除；脚下以 UnitBlobShadow 贴地软圆片代替（结界/
            // 角色柱/卡堆不动，仅生物——占位胶囊同为生物占位一并处理）
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            UnitBlobShadow.Attach(wrapper.transform, card.X, card.Z);

            // 脚下卡背指示牌（2026-11-03 横置视觉改版）：竖直=可用、横置=原地旋 90°——
            // 仅生物（结界走三件模型早退分支，无横置概念不挂）
            var plate = UnitCardPlate.Attach(wrapper.transform, card.X, card.Z);

            // 头顶数值高度=实际包围盒顶+余量（2026-10-14 体型分档后各模型高低不一，不能再按统一基准估算）
            var uv = FinishUnit(card, wrapper.transform, model, anim, owner);
            uv.ModelYaw = FacingYaw(owner);
            uv.CardPlate = plate;
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
            BattleCastArrival.Play(card.RuntimeId, payload.seat, payload.paid,
                UnitAnchor(card.X, card.Z), model,
                () => { }); // 模型消融自显——回调仅节拍通知（保留契约供外部观察）
        }

        /// <summary>单位装配收尾（结界/生物两路汇流）：格面读数三槽钉数据格 tile（2026-10-26 迁格：
        /// 生物=左攻/右血·结界=中槽耐久，槽位=旧格前三键位）；头顶锚高=实际包围盒顶+余量
        /// （体型分档后各模型高低不一——浮字基线/动作按钮锚仍按头顶口径）；生物挂头注视供应商
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
                StatsHeightY = textY,
            };
            CreateStatSlotLabels(card, v);
            anim?.SetLookTarget(() => ResolveGazeOf(v)); // 供应商在组件层持有，驱动重建自动重挂
            return v;
        }

        // ---- 格面读数三槽（2026-10-26 头顶数值迁格定案）：槽位=旧格前三键位 ----
        //（BattleHex.prefab 三键子件 2026-10-23 退役时实测局部值：Attack 左(-0.6,0.02,-0.45) /
        //  Defense 右(0.6,0.02,-0.45) / Activate 中(0.003,0.02,-0.657)——生物攻/血占两侧、
        //  结界耐久占中间；中槽对生物暂无第三读数，位子留着（用户口径：三个用完·暂时没啥可显）。
        //  2026-10-30 地块 mesh×1.2 联动：三槽局部值同步 ×1.2，与格面相对位置不变）
        private static readonly Vector3 AtkSlotLocal = new Vector3(-0.72f, 0.02f, -0.54f);
        private static readonly Vector3 LifeSlotLocal = new Vector3(0.72f, 0.02f, -0.54f);
        private static readonly Vector3 MidSlotLocal = new Vector3(0.0036f, 0.02f, -0.7884f);

        /// <summary>按单位类型建槽（结界=中槽耐久；生物=左攻右血；tile 缺失/字体缺失=空挂，对账照写 null 安全）。</summary>
        private void CreateStatSlotLabels(BattleCardView card, UnitVisual v)
        {
            var tile = BattleTileFx.TileOf(card.X, card.Z);
            if (tile == null) return;
            if (v.DurabilityOnly)
                v.StatMid = CreateFlatStatLabel(tile, "stat-mid", MidSlotLocal);
            else
            {
                v.StatAtk = CreateFlatStatLabel(tile, "stat-atk", AtkSlotLocal);
                v.StatLife = CreateFlatStatLabel(tile, "stat-life", LifeSlotLocal);
            }
        }

        /// <summary>格面平贴读数槽（EnsureFlatCellLabel 同口径：面朝上·读向 +x·可读面朝相机——
        /// 正解=LookRotation(down, forward)；1/TileScale 反缩保原字号；y=0.02 在箭头/图标/计数条之上）。</summary>
        private static TextMeshPro CreateFlatStatLabel(Transform tile, string name, Vector3 local)
        {
            if (!EnsureTmpFont()) return null;
            var go = new GameObject(name);
            go.transform.SetParent(tile, false);
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
            go.transform.localScale = Vector3.one / BattleBoardGrid.TileScale; // P0 缩格补偿（同地牌计数条）
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.fontSize = 4.2f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.rectTransform.sizeDelta = new Vector2(1.2f, 0.9f);
            return tmp;
        }

    }
}
