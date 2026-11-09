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

        /// <summary>地牌固定元素格对账（2026-10-14 晚定案·2026-10-28 元素缩编）：4 格按 BoardLayout.LandCellElementOrder
        ///（灰红蓝绿，玩家视角左→右）固定元素身份——图标常驻（空格也亮身份）；
        /// 计数=「可用/上限」：可用=未横置张+钱包该色余额；上限=该色已放入地牌张数（含横置）；
        /// 可用&gt;0 点亮顶面锁色描边环（2026-10-17 定案：整块菲涅尔壳颜色压住格面文字/图标退役，
            /// 见 BattleTileFx；黑白格与中线产量双格已随 2026-10-28 元素缩编退役）。
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
                // 元素标识常驻（空格也亮身份）。对手（owner=1）=图标 +z 0.42 靠格上缘、数字 -z 0.504
                // 靠下缘；己方（owner=0）2026-10-21 调换定案：底部手牌收缩态压住格下缘——
                // 数字上移 +z 0.42、图标下沉 -z 0.504（被手牌遮的只剩图标，读数不再被挡）。
                // 2026-10-30 地块 mesh×1.2 联动：图标/数字 z 偏移同步 ×1.2（原 ±0.35/0.42）。
                bool selfSide = owner == 0;
                float iconZ = selfSide ? -0.504f : 0.42f;
                BattleTileFx.AddElement(cell.x, cell.z, element, new Vector2(0f, iconZ));
                int wallet = pv != null && element >= 0 && element < pv.WalletByColor.Length
                    ? pv.WalletByColor[element] : 0;
                byColor.TryGetValue(element, out var g);
                int remaining = (g != null ? g.Untapped : 0) + wallet;
                if (remaining > 0) BattleTileFx.SetLandGlow(cell.x, cell.z, element);
                var label = EnsureFlatCellLabel(((long)cell.x << 32) | (uint)cell.z, cell.x, cell.z,
                    selfSide ? 0.42f : -0.504f);
                if (label != null)
                {
                    label.gameObject.SetActive(true);
                    // 「可用/上限」（上限=该色已放入地牌张数，含横置；黑白格已随 2026-10-28 元素缩编退役）
                    label.text = $"{remaining}/{(g != null ? g.Total : 0)}";
                }
            }

            // 万能元素格（2026-10-29 对座镜像迁位 (1,0)/(6,6)·Any 角色只做展示）：图标常驻+余额数字+
            // 余额>0 描边环（「可用即钱包」口径：Any 不由地牌产出，永不叠地牌卡）；
            // 无点击行为（旧「点击开额外区面板」已随 P11 额外区格整删退役）
            var anyCell = BoardLayout.AnyElementCell(owner);
            float anyIconZ = owner == 0 ? -0.504f : 0.42f; // 2026-10-30 地块×1.2 联动（原 ±0.42/0.35）
            BattleTileFx.AddElement(anyCell.x, anyCell.z, (int)ManaType.Any, new Vector2(0f, anyIconZ));
            int anyWallet = pv != null && (int)ManaType.Any < pv.WalletByColor.Length
                ? pv.WalletByColor[(int)ManaType.Any] : 0;
            if (anyWallet > 0) BattleTileFx.SetLandGlow(anyCell.x, anyCell.z, (int)ManaType.Any);
            var anyLabel = EnsureFlatCellLabel(((long)anyCell.x << 32) | (uint)anyCell.z, anyCell.x, anyCell.z,
                owner == 0 ? 0.42f : -0.504f);
            if (anyLabel != null)
            {
                anyLabel.gameObject.SetActive(true);
                anyLabel.text = anyWallet.ToString(); // 只显余量（不挂上限数字）
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
            // 教学介绍段对手柱显隐（2026-10-18 用户定案：创建流程不动，建后隐藏）：讲角色/右侧区
            // 只显自己，读到操作预告段（Solo("all")）才显露；非教学局标记恒 false 零影响。每次对账幂等同步。
            if (owner == 1)
                v.Root.gameObject.SetActive(!BattleRevealDirector.HideOpponentHero);
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

        /// <summary>地牌格平铺数字条（2026-10-14 晚定案：这类格子的文字不看向相机，平贴地块显示；
        /// 晚二批=轴对齐 z 旋 0；本批修字向）：面朝上且**读向 +x、字头 +z、可读面（TMP 本地 -Z）朝相机**
        /// ——正解=LookRotation(down, forward)=Euler(90,0,0)，相机 (7,8,-4)/(50,0,0) 自 -z 俯拍实测
        /// （旧 LookRotation(up,…) 三项全反：字镜像且读向右到左，中文整句一眼翻反、1/1 回文数漏检）。
        /// 贴格顶面 y=0.014（顶面 y=0、菲涅尔 0.004、图标 0.008 之上），zOffset=格心 z 向偏移
        ///（默认 -0.504=视角前缘靠下缘；己方 2026-10-21 调换=+0.42 与图标换位——缓存复用路径
        /// 同步校位，防实例跨局残留旧 z。2026-10-30 地块 mesh×1.2 联动：z 偏移整体 ×1.2，原 ±0.35/0.42）。
        /// key=格(x&lt;&lt;32|z)。
        /// 2026-10-17 改挂 tile 子物体（原挂 _root）：教学介绍段独占显隐按 tile SetActive 收格，
        /// 文字不再悬空留在盘面上（与元素图标/HudRow 认领件同口径）。</summary>
        private TextMeshPro EnsureFlatCellLabel(long key, int x, int z, float zOffset = -0.504f)
        {
            if (!EnsureTmpFont()) return null;
            if (_landCounts.TryGetValue(key, out var label) && label != null)
            {
                label.transform.localPosition = new Vector3(0f, 0.014f, zOffset); // 防御性回写（同 AddElement 口径）
                return label; // fake-null 判死重建
            }
            var tile = BattleBoardGrid.Root != null ? BattleBoardGrid.Root.transform.Find($"tile-{x}-{z}") : null;
            if (tile == null) return null; // 未装台/格名异常：不建，下一事件重试（与 BattleTileFx.TileOf 同法）
            var go = new GameObject("cell-flat-" + x + "-" + z);
            go.transform.SetParent(tile, false); // tile 本体位姿=格心恒等变换，局部值即原世界偏移
            go.transform.localPosition = new Vector3(0f, 0.014f, zOffset);
            go.transform.localRotation = Quaternion.LookRotation(Vector3.down, Vector3.forward); // 平贴·轴对齐·字向正（2026-10-14 修）
            go.transform.localScale = Vector3.one / BattleBoardGrid.TileScale; // P0 缩格补偿：计数条不随格缩小（2026-10-23）
            label = go.AddComponent<TextMeshPro>();
            label.fontSize = 3.5f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.rectTransform.sizeDelta = new Vector2(1.6f, 0.9f);
            _landCounts[key] = label;
            return label;
        }

    }
}
