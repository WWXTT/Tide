using System.Collections.Generic;
using UnityEngine;

namespace SynergyUI
{
    /// <summary>
    /// 地块三键对账（2026-10-15 用户定案）：单位本体退出射线检测，射线只打地块——落格=展示信息；
    /// 攻击/守卫/发动主动效果拆到格前三键（BattleHex.prefab 手搭子件：Attack/Defense/Activate，
    /// 均带 BoxCollider 供棋盘射线命中，命中名=按钮名→BattleScreen 归并到所属格）。
    /// 白=此刻可用、灰 8D8D8D=在场但不可用（2026-10-15 定案：红/蓝/绿分色退役）。
    /// BattleScreen 每帧差量对账（幂等）：仅己方单位格与角色格显键（2026-10-16 角色格同款三键：
    /// 攻击=弹药、守卫=守卫窗口候选、发动=英雄技能）；编舞选目标/放格选格/网络局=全收。
    /// 深判定（费用/横置代价/耐久）留引擎裁决，高亮不预判，拒绝走点击 toast。
    /// </summary>
    public static class BattleTileButtons
    {
        /// <summary>一帧的显键清单（BattleScreen 组装；不在列的格=收键）。</summary>
        public sealed class Entry
        {
            public int X, Z;
            public bool Attack, Guard, Activate;
        }

        // 2026-10-15 定案：可用=全白 FFFFFF、不可用=8D8D8D 灰（红/蓝/绿分色退役）
        private static readonly Color UsableColor = Color.white;
        private static readonly Color DimColor = new Color(141f / 255f, 141f / 255f, 141f / 255f); // 8D8D8D

        private const string AttackBtn = "Attack";
        private const string DefenseBtn = "Defense"; // 预制体件名=Defense（守卫语义，2026-10-15 手搭）
        private const string ActivateBtn = "Activate";

        private sealed class TileButtons
        {
            public Transform Tile;
            public GameObject Attack, Guard, Activate; // null=该格预制体缺件（手搭遗漏，容错跳过）
            public int LastBits = -1;                  // 上次可用位图（-1=首写强制）
        }

        private static readonly Dictionary<long, TileButtons> _shown = new Dictionary<long, TileButtons>();

        /// <summary>域重载关闭（Enter Play Mode Options）防跨 Play 残留：与 BattleTileFx.ResetStatics 同口径。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _shown.Clear();

        /// <summary>拆台清索引（对象本体随棋盘根销毁；BattleStageDirector.DismountBoard 调用）。</summary>
        public static void Unmount() => _shown.Clear();

        /// <summary>幂等差量对账：entries=本帧应显键的格（null/空=全收）。
        /// 显键按可用位图写色（renderer.material 实例色，件随 tile 销毁自然回收）。</summary>
        public static void Apply(List<Entry> entries)
        {
            var keep = new HashSet<long>();
            if (entries != null)
            {
                foreach (var e in entries)
                {
                    if (e == null || e.X < 0 || e.Z < 0) continue;
                    long key = ((long)e.X << 32) | (uint)e.Z;
                    keep.Add(key);
                    var tb = Ensure(e.X, e.Z, key);
                    if (tb == null) continue; // 格未拼/缺件：不显不计
                    int bits = (e.Attack ? 1 : 0) | (e.Guard ? 2 : 0) | (e.Activate ? 4 : 0);
                    if (bits == tb.LastBits) continue;
                    tb.LastBits = bits;
                    Paint(tb.Attack, e.Attack);
                    Paint(tb.Guard, e.Guard);
                    Paint(tb.Activate, e.Activate);
                }
            }

            List<long> stale = null;
            foreach (var k in _shown.Keys)
                if (!keep.Contains(k)) (stale ?? (stale = new List<long>())).Add(k);
            if (stale == null) return;
            foreach (var k in stale)
            {
                Hide(_shown[k]);
                _shown.Remove(k);
            }
        }

        private static void Paint(GameObject go, bool on)
        {
            if (go == null) return;
            go.SetActive(true);
            go.GetComponent<Renderer>().material.color = on ? UsableColor : DimColor;
        }

        private static void Hide(TileButtons tb)
        {
            if (tb.Attack != null) tb.Attack.SetActive(false);
            if (tb.Guard != null) tb.Guard.SetActive(false);
            if (tb.Activate != null) tb.Activate.SetActive(false);
            tb.LastBits = -1;
        }

        /// <summary>格 → 三键缓存（惰性建；tile fake-null 判死重建——棋盘重建后旧索引复活）。</summary>
        private static TileButtons Ensure(int x, int z, long key)
        {
            if (_shown.TryGetValue(key, out var tb) && tb.Tile != null) return tb;
            var tile = BattleBoardGrid.Root != null ? BattleBoardGrid.Root.transform.Find($"tile-{x}-{z}") : null;
            if (tile == null) return null;
            tb = new TileButtons
            {
                Tile = tile,
                Attack = Btn(tile, AttackBtn),
                Guard = Btn(tile, DefenseBtn),
                Activate = Btn(tile, ActivateBtn),
            };
            if (tb.Attack == null && tb.Guard == null && tb.Activate == null) return null; // 预制体无件
            _shown[key] = tb;
            return tb;
        }

        private static GameObject Btn(Transform tile, string name)
        {
            var t = tile.Find(name);
            return t == null ? null : t.gameObject;
        }
    }
}
