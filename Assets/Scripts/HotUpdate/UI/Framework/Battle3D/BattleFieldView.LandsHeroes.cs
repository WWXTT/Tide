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
        // ======================================== 地牌（固定元素格） ========================================

        /// <summary>地牌对账（2026-11-15 地牌长条改版）：4 格视觉合并为一条长条——每张地牌一张
        /// 平贴面片（规格=CardPlateMetrics，与生物横置卡背同源）、长条靠卡组侧数字段「填充/上限」
        ///（LandStripFx）；左下万能格拆 5 分格显示灰红蓝绿彩临时元素数量（WalletCellsFx）。
        /// 旧逐格元素图标/「可用/上限」计数/锁色描边环随本改版退役（逐色详情仍走悬停 hover-info；
        /// 引擎 4 格坐标零改动——BoardLayout.LandCells 硬约束，合并只发生在视觉与命中层）。
        /// 对账消费模型不变：事件级幂等全量，由 Apply 每拍驱动。</summary>
        private void ReconcileLands(List<BattleCardView> lands, BattlePlayerView pv, int owner)
        {
            LandStripFx.Reconcile(owner, lands ?? new List<BattleCardView>(), pv != null ? pv.LandCap : 0);
            WalletCellsFx.Reconcile(owner, pv?.WalletByColor);
        }

        // ======================================== 角色 ========================================

        private sealed class HeroVisual
        {
            public Transform Root;
            public TextMeshPro Stats;
            public string LastStats;
            public Material Mat; // 2026-10-18 着色克隆实例：非资产件随 Dispose 销毁（跨域 fake-null 防口）

            // —— 角色HP读数节拍（2026-10-20：镜像单位 StatsHeld——攻击步快照终值缓冲，命中拍落地）——
            public int ShownLife = int.MinValue; // 当前展示生命（2D 血条节拍取值；首写前 MinValue=未建）
            public string PendingStats;          // 冻结期缓冲的目标文本（null=无缓冲）
            public int PendingLife;              // 缓冲文本对应生命（释放时落 ShownLife）
            public bool StatsHeld;               // 攻击步冻结：对账只缓冲不写屏

            /// <summary>命中节拍/兜底释放：缓冲文本直写落地（角色读数无逐格滚动语义）。</summary>
            public void ReleaseStatsHold()
            {
                if (!StatsHeld) return;
                StatsHeld = false;
                if (PendingStats == null) return;
                var txt = PendingStats;
                PendingStats = null;
                ShownLife = PendingLife;
                if (LastStats == txt) return;
                LastStats = txt;
                if (Stats != null) Stats.text = txt;
            }
        }

        private void ReconcileHero(BattlePlayerView p, int owner)
        {
            if (BattleRevealDirector.SuppressHeroes) return; // 教学介绍段闸（2026-10-17 语音联动显隐）：介绍到角色格前不建柱
            HeroVisual v;
            if (!_heroes.TryGetValue(owner, out v))
            {
                // 开场预置柱收养（2026-10-19 五段式开局段二）：BattleOpeningDirector 已建并演完就位
                // 动画的柱（名字/位姿同规格）直接复用不重建；收养/新建同口补 Stats/着色/登记。
                var adopted = _root != null ? _root.Find("hero-" + (owner == 0 ? "self" : "opp")) : null;
                var pillar = adopted != null ? adopted.gameObject : CreateHeroPillar(owner);
                if (adopted == null) // 新建柱定位定格；预置柱位姿由开场就位动画钉死，不覆写（防打断在途动画）
                {
                    var cell = BoardLayout.CharacterCell(owner);
                    pillar.transform.position = BattleBoardGrid.WorldPositionOf(cell.x, cell.z) + new Vector3(0f, 0.6f, 0f);
                    pillar.transform.localScale = new Vector3(0.55f, 0.6f, 0.55f);
                }
                v = new HeroVisual
                {
                    Root = pillar.transform,
                    Stats = CreateStatsText(pillar.transform, 1.3f),
                    Mat = HeroPillarMaterial(owner),
                };
                if (v.Mat != null) pillar.GetComponent<Renderer>().sharedMaterial = v.Mat;
                else pillar.GetComponent<Renderer>().material.color = owner == 0 ? P1UnitColor : P2UnitColor; // 模板缺件兜底：旧着色链（编辑器可见，真机粉=缺包已告警）

                _heroes[owner] = v;
            }
            // 教学介绍段柱显隐（2026-10-18 对手柱 / 2026-11-09 己方柱）：创建流程不动，建后按标记
            // 隐藏——独占段只显当前讲的区块（basics_03 讲完角色，basics_04 起藏柱至操作预告段 all）；
            // 非教学局标记恒 false 零影响。每次对账幂等同步。
            v.Root.gameObject.SetActive(owner == 1
                ? !BattleRevealDirector.HideOpponentHero
                : !BattleRevealDirector.HideSelfHero);
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
            if (v.StatsHeld) // 攻击步冻结（2026-10-20 角色HP节拍）：快照终值只缓冲，命中拍落地
            {
                v.PendingStats = txt;
                v.PendingLife = p.Life;
                return;
            }
            v.ShownLife = p.Life;
            if (v.LastStats != txt)
            {
                v.LastStats = txt;
                v.Stats.text = txt;
            }
        }

        /// <summary>建角色柱 GameObject（ReconcileHero 新建与开场预置共用规格）：Hero.prefab 实例
        ///（2026-10-18 出包粉红修复：内置默认材质不随包分发，改 prefab 实例走 ArtStatic bundle；
        /// 着色由调用方经 HeroPillarMaterial 模板克隆）+ 拆全部 Collider（柱不截射线，点击归角色格
        /// tile）+ 命名 hero-self/opp + 挂 _root。定位/缩放归调用方。</summary>
        private GameObject CreateHeroPillar(int owner)
        {
            var prefab = Tide.HotUpdate.HotUpdateAssets.Load<GameObject>(
                "Hero", "Assets/Art/Prefab/Hero.prefab");
            GameObject pillar;
            if (prefab != null)
            {
                pillar = Object.Instantiate(prefab, _root, false);
                foreach (var col in pillar.GetComponentsInChildren<Collider>())
                    Object.Destroy(col);
            }
            else
            {
                Debug.LogWarning("[BattleField] Hero.prefab 缺失（Assets/Art/Prefab/Hero.prefab）——"
                    + "角色柱回落内置圆柱（真机需重跑 Tools/打包/YooAsset资源打包（当前平台）——2026-10-18 起替代旧 StreamingAssets 三件套）");
                pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Object.Destroy(pillar.GetComponent<Collider>());
                pillar.transform.SetParent(_root, false);
            }
            pillar.name = "hero-" + (owner == 0 ? "self" : "opp");
            // 建柱即上阵营色（2026-10-24 用户定案）：我方恒蓝/对方恒红·各自视角相对——开场段二柱
            // 升起时即带色，不待掷先后手（旧链=首次对账才着色，掷币后突兀变色）。与 ReconcileHero
            // 收养共用 HeroPillarMaterial 每方唯一实例，不重复克隆。
            var rend = pillar.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = HeroPillarMaterial(owner);
                if (mat != null) rend.sharedMaterial = mat;
                else rend.material.color = owner == 0 ? P1UnitColor : P2UnitColor; // 模板缺件兜底（同对账口径）
            }
            return pillar;
        }

        /// <summary>开场段二预置角色柱（2026-10-19 五段式开局·BattleOpeningDirector）：建柱定格到
        /// 角色格位姿（规格同 ReconcileHero），返回根 Transform 供就位动画驱动；本柱无 Stats/着色
        ///（数值对账期才有），首次对账由 ReconcileHero 按名收养补齐。已收养/已存在返回现柱不重建。</summary>
        public Transform PlaceOpeningHero(int owner)
        {
            if (_heroes.ContainsKey(owner))
            {
                HeroVisual existing;
                return _heroes.TryGetValue(owner, out existing) ? existing.Root : null; // 已对账（重放开场竞态）
            }
            var adopted = _root != null ? _root.Find("hero-" + (owner == 0 ? "self" : "opp")) : null;
            if (adopted != null) return adopted;
            var cell = BoardLayout.CharacterCell(owner);
            var pillar = CreateHeroPillar(owner);
            pillar.transform.position = BattleBoardGrid.WorldPositionOf(cell.x, cell.z) + new Vector3(0f, 0.6f, 0f);
            pillar.transform.localScale = new Vector3(0.55f, 0.6f, 0.55f);
            return pillar.transform;
        }

        /// <summary>world 空间 billboard 文本（2026-10-19 开场件公用）：CreateStatsText 公开转发——
        /// 开场卡组名签/提示签与常规头顶数值同源同规格。</summary>
        public static TextMeshPro CreateWorldLabel(Transform parent, float height)
            => CreateStatsText(parent, height);

        /// <summary>world 浮字公开转发（2026-10-19 开场硬币落定提示）：上浮渐隐 0.9s 一次性建毁。</summary>
        public static void ShowFloatText(Transform parent, float height, string text, Color color)
            => FloatTextAsync(parent, height, text, color).Forget();

        /// <summary>角色柱着色（2026-10-18 出包粉红修复）：分区格材质资产（URP Lit·已入 artstatic
        /// bundle·棋盘出包验证可渲染）克隆实例 + _BaseColor 按 owner 上色——CreatePrimitive/
        /// 内置默认材质链整退。2026-10-24 起建柱即上色（我方恒蓝/对方恒红·各自视角相对，不待
        /// 掷先后手）：每方克隆唯一实例缓存于 _heroMats，建柱与对账收养共用；随 Dispose 销毁。</summary>
        private Material HeroPillarMaterial(int owner)
        {
            if (_heroMats[owner] != null) return _heroMats[owner];
            var template = Tide.HotUpdate.HotUpdateAssets.Load<Material>(
                "BattleTile_Zone02", "Assets/Art/Materials/BattleTile_Zone02_P1Unit.mat");
            if (template == null)
            {
                Debug.LogWarning("[BattleField] 分区格材质模板缺失（BattleTile_Zone02_P1Unit.mat）——角色柱走旧着色兜底");
                return null;
            }
            var m = new Material(template) { name = "HeroPillar_" + (owner == 0 ? "P1" : "P2") };
            m.SetColor("_BaseColor", owner == 0 ? P1UnitColor : P2UnitColor); // URP Lit 主色属性名，显式写免 .color 回退歧义
            _heroMats[owner] = m;
            return m;
        }

        // ======================================== 技能格（2026-10-16 撤销——空段留档） ========================================

        // 地牌格平铺数字条 EnsureFlatCellLabel 已随 2026-11-15 地牌长条改版退役（计数迁长条数字段
        ///LandStripFx.UpdateCapLabel 与 5 分格 WalletCellsFx；平贴字向正解 LookRotation(down,forward)
        // 的口径记载由两处继承）。

    }
}
