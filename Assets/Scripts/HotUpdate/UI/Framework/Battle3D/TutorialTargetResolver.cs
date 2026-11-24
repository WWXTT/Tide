using System.Collections.Generic;
using System.Linq;
using CardCore;
using GameBoard;
using TMPro;
using UnityEngine;

namespace SynergyUI
{
    /// <summary>
    /// 教学表现层目标解析器（2026-11-21 遮罩/高亮/暂停计划 v0.3 §2.1/§3）：3D token →
    /// 世界包围盒（遮罩窗）/ renderer 集合（选择描边）/ 格集合（格环叠加）。
    /// 词表（只收 3D）：
    /// · hero / oppHero——角色柱（模型+读数 TMP 都在 bounds 内）；
    /// · unit:{卡id或实例id}——场上生物（wrapper 下全部 renderer 包围盒；模板 id 命中多实例=多张联包）；
    /// · tmp:{宿主}——宿主身上的 3D TMP 文字（unit 三槽读数 / 角色柱读数）；找不到时回落宿主包围盒；
    /// · cell:{x}:{z} / zone:role|activation|right|mid|p1-land|p1-field / board / units——
    ///   格群（tile mesh bounds；zone 词沿用 BattleRevealDirector.Solo 的分区口径）。
    /// ui:*/card:*/hand 不支持（UI 不参与遮罩高亮——审计在 TideSim --tutorial-guide-audit 拦）。
    /// 解析失败（卡不在场/格无 tile/无视图在役）：verbose=true 记警告，返回 false 由调用方跳过本目标。
    /// 逐帧窗跟新调用 verbose=false（静默重试——镜头/演出位移后目标可能重现）。
    /// </summary>
    public static class TutorialTargetResolver
    {
        // ======================================== 包围盒（遮罩窗底料） ========================================

        /// <summary>token → 世界包围盒（八点投影前的世界侧单源）。</summary>
        public static bool TryResolveBounds(string token, out Bounds bounds, out string error, bool verbose = true)
        {
            bounds = default;
            error = null;
            if (string.IsNullOrEmpty(token)) { error = "空 token"; Warn(verbose, error); return false; }

            if (token == "hud:phase")
            {
                // 中线 HUD 阶段显示格（2026-11-23 验收：basics 步 10 讲解回合阶段只显该地块）——
                // HUD 行=世界空间 3D tile（MeshRenderer 在场），剪影/包围盒与棋盘格同机制
                var tile = BattleHudRow.PhaseTile;
                var r = tile != null ? tile.GetComponentInChildren<Renderer>() : null;
                if (r == null) { error = "阶段显示格未建"; Warn(verbose, error); return false; }
                bounds = r.bounds;
                return true;
            }

            if (token == "hero" || token == "oppHero")
                return HeroBounds(token == "hero" ? 0 : 1, out bounds, out error, verbose);

            if (token.StartsWith("tmp:"))
            {
                var host = token.Substring(4);
                // tmp: 只指单位/角色宿主身上的 TMP；找不到 TMP（未建/死亡隐藏）回落宿主包围盒（计划 §2.1）
                if (TmpBounds(host, out bounds, out error)) return true;
                if (verbose) Debug.LogWarning($"[TutorialTarget] {token} 无 TMP 可指（未建/死亡隐藏）——回落宿主 {host} 包围盒");
                return HostBounds(host, out bounds, out error, verbose);
            }

            if (token.StartsWith("unit:"))
                return UnitBounds(token.Substring(5), out bounds, out error, verbose);

            var cells = CellsOf(token);
            if (cells != null)
            {
                if (cells.Count == 0) { error = $"「{token}」无格"; Warn(verbose, error); return false; }
                return CellsBounds(cells, out bounds, out error, verbose);
            }

            error = $"未知 token：{token}";
            Warn(verbose, error);
            return false;
        }

        // ======================================== renderer 集合（选择描边） ========================================

        /// <summary>token → renderer 集合（后处理选择描边喂料：模型部件+TMP mesh+格面 mesh）。</summary>
        public static bool TryResolveRenderers(string token, out List<Renderer> renderers, out string error, bool verbose = true)
        {
            renderers = null;
            error = null;
            if (string.IsNullOrEmpty(token)) { error = "空 token"; Warn(verbose, error); return false; }

            if (token == "hud:phase")
            {
                var tile = BattleHudRow.PhaseTile;
                if (tile == null) { error = "阶段显示格未建"; Warn(verbose, error); return false; }
                renderers = tile.GetComponentsInChildren<Renderer>().Where(r => r != null).ToList();
                return renderers.Count > 0;
            }

            if (token == "hero" || token == "oppHero")
            {
                var view = BattleFieldView.Current;
                if (view == null) { error = "无 BattleFieldView 在役"; Warn(verbose, error); return false; }
                if (!view.TryGetHeroVisual(token == "hero" ? 0 : 1, out var root, out _))
                { error = $"英雄柱未建（{token}）"; Warn(verbose, error); return false; }
                renderers = root.GetComponentsInChildren<Renderer>().Where(r => r != null).ToList();
                return renderers.Count > 0;
            }

            if (token.StartsWith("tmp:"))
            {
                var host = token.Substring(4);
                renderers = new List<Renderer>();
                foreach (var tmp in TmpLabelsOf(host))
                {
                    var r = tmp.GetComponent<Renderer>();
                    if (r != null) renderers.Add(r);
                }
                if (renderers.Count == 0) { error = $"{token} 无 TMP renderer"; Warn(verbose, error); return false; }
                return true;
            }

            if (token.StartsWith("unit:"))
            {
                renderers = new List<Renderer>();
                foreach (var card in FindBoardCards(token.Substring(5)))
                    if (BattleFieldView.Current != null
                        && BattleFieldView.Current.TryGetUnitVisual(card, out var wrapper))
                        renderers.AddRange(wrapper.GetComponentsInChildren<Renderer>().Where(r => r != null));
                if (renderers.Count == 0) { error = $"{token} 场上无匹配单位"; Warn(verbose, error); return false; }
                return true;
            }

            var cells = CellsOf(token);
            if (cells != null)
            {
                renderers = new List<Renderer>();
                foreach (var (x, z) in cells)
                {
                    var tile = BattleTileFx.TileOf(x, z);
                    var r = tile != null ? tile.GetComponent<Renderer>() : null;
                    if (r != null) renderers.Add(r);
                }
                if (renderers.Count == 0) { error = $"「{token}」无格面 renderer"; Warn(verbose, error); return false; }
                return true;
            }

            error = $"未知 token：{token}";
            Warn(verbose, error);
            return false;
        }

        // ======================================== 格集合（zone/cell 词 → 格） ========================================

        /// <summary>zone/cell/board/units 词 → 格集合（分区口径沿用 BattleRevealDirector.Solo：
        /// zone:role=己方角色格；zone:activation=己方单位区；zone:right=己方卡组/墓/除外；
        /// zone:mid=中线行；zone:p1-land=己方地牌行含万能格；zone:p1-field=己方场地格；
        /// board=全盘；units=双方单位区）。**非格群词（hero/unit:/tmp: 等）返回 null**（调用方分流）。</summary>
        public static List<(int x, int z)> CellsOf(string token)
        {
            switch (token)
            {
                case "board":
                {
                    var list = new List<(int, int)>();
                    for (int x = 0; x < BoardLayout.Width; x++)
                        for (int z = 0; z < BoardLayout.Height; z++)
                            if (!BoardLayout.NoTileAt(x, z)) list.Add((x, z));
                    return list;
                }
                case "units":
                {
                    var list = new List<(int, int)>(BoardLayout.UnitCells(0));
                    list.AddRange(BoardLayout.UnitCells(1));
                    return list;
                }
                case "zone:role":
                    return new List<(int, int)> { BoardLayout.CharacterCell(0) };
                case "zone:activation":
                    return new List<(int, int)>(BoardLayout.UnitCells(0));
                case "zone:right":
                    return new List<(int, int)>
                    {
                        (BoardLayout.P1DeckX, BoardLayout.P1DeckZ),
                        (BoardLayout.P1GraveX, BoardLayout.P1GraveZ),
                        (BoardLayout.P1ExileX, BoardLayout.P1ExileZ),
                    };
                case "zone:mid":
                {
                    var list = new List<(int, int)>();
                    for (int i = 0; i < BoardLayout.MiddleCellCount; i++)
                        list.Add((BoardLayout.MiddleStartX + i, BoardLayout.MiddleZ));
                    return list;
                }
                case "zone:p1-land":
                {
                    var list = new List<(int, int)>(BoardLayout.LandCells(0)) { BoardLayout.AnyElementCell(0) };
                    return list;
                }
                case "zone:p1-field":
                    return new List<(int, int)> { BoardLayout.FieldCell(0) };
            }
            if (token != null && token.StartsWith("cell:"))
            {
                var parts = token.Substring(5).Split(':');
                if (parts.Length == 2 && int.TryParse(parts[0], out int x) && int.TryParse(parts[1], out int z)
                    && x >= 0 && x < BoardLayout.Width && z >= 0 && z < BoardLayout.Height
                    && !BoardLayout.NoTileAt(x, z))
                    return new List<(int, int)> { (x, z) };
                return new List<(int, int)>(); // 非法 cell：空集（调用方报「无格」）
            }
            return null; // 非格群词
        }

        // ======================================== 屏幕矩形（窗） ========================================

        /// <summary>世界包围盒 → 屏幕 AABB（px，**左下原点**）+pad。八点投影抗透视斜摄；
        /// 目标在相机后方（教学镜头恒朝棋盘，理论不可达）由调用方按越界窗兜底。</summary>
        public static Rect ScreenRectOf(Bounds b, Camera cam, float padPx)
        {
            if (cam == null) return Rect.zero;
            var min = new Vector3(b.min.x, b.min.y, b.min.z);
            var max = new Vector3(b.max.x, b.max.y, b.max.z);
            Vector2 lo = new Vector2(float.MaxValue, float.MaxValue), hi = new Vector2(float.MinValue, float.MinValue);
            for (int corner = 0; corner < 8; corner++)
            {
                var p = new Vector3(
                    (corner & 1) == 0 ? min.x : max.x,
                    (corner & 2) == 0 ? min.y : max.y,
                    (corner & 4) == 0 ? min.z : max.z);
                var s = cam.WorldToScreenPoint(p);
                if (s.x < lo.x) lo.x = s.x;
                if (s.y < lo.y) lo.y = s.y;
                if (s.x > hi.x) hi.x = s.x;
                if (s.y > hi.y) hi.y = s.y;
            }
            return Rect.MinMaxRect(lo.x - padPx, lo.y - padPx, hi.x + padPx, hi.y + padPx);
        }

        // ======================================== 内部 ========================================

        /// <summary>引擎战场按 id 找卡（实例 id 精确 / 模板 id 命中全部实例——CardWrapper 模板）。</summary>
        public static List<Card> FindBoardCards(string id)
        {
            var result = new List<Card>();
            var core = GameCore.Instance;
            if (core == null || core.ZoneManager == null) return result;
            foreach (var player in new[] { core.Player1, core.Player2 })
            {
                if (player == null) continue;
                foreach (var c in core.ZoneManager.GetCards(player, Zone.Battlefield))
                {
                    if (c == null || !c.IsAlive) continue;
                    if (c.ID == id) result.Add(c);
                    else if ((c as CardWrapper)?.GetData()?.ID == id) result.Add(c);
                }
            }
            return result;
        }

        private static bool HeroBounds(int owner, out Bounds bounds, out string error, bool verbose)
        {
            bounds = default;
            error = null;
            var view = BattleFieldView.Current;
            if (view == null) { error = "无 BattleFieldView 在役"; Warn(verbose, error); return false; }
            if (!view.TryGetHeroVisual(owner, out var root, out _))
            { error = $"英雄柱未建（owner={owner}）"; Warn(verbose, error); return false; }
            return Encapsulate(root.GetComponentsInChildren<Renderer>(), out bounds);
        }

        private static bool UnitBounds(string id, out Bounds bounds, out string error, bool verbose)
        {
            bounds = default;
            error = null;
            var cards = FindBoardCards(id);
            if (cards.Count == 0) { error = $"unit:{id} 场上无匹配单位"; Warn(verbose, error); return false; }
            var view = BattleFieldView.Current;
            if (view == null) { error = "无 BattleFieldView 在役"; Warn(verbose, error); return false; }
            var all = new List<Renderer>();
            foreach (var card in cards)
                if (view.TryGetUnitVisual(card, out var wrapper))
                    all.AddRange(wrapper.GetComponentsInChildren<Renderer>());
            return Encapsulate(all, out bounds) ? true : Fail($"{id} 无 renderer", out error, verbose);
        }

        private static bool TmpBounds(string host, out Bounds bounds, out string error)
        {
            bounds = default;
            error = null;
            var labels = TmpLabelsOf(host);
            if (labels.Count == 0) return false;
            var renderers = new List<Renderer>();
            foreach (var tmp in labels)
            {
                var r = tmp.GetComponent<Renderer>();
                if (r != null) renderers.Add(r);
            }
            return Encapsulate(renderers, out bounds);
        }

        /// <summary>宿主（unit:/hero/oppHero）整体包围盒——tmp: 解析失败的回落。</summary>
        private static bool HostBounds(string host, out Bounds bounds, out string error, bool verbose)
        {
            if (host == "hero") return HeroBounds(0, out bounds, out error, verbose);
            if (host == "oppHero") return HeroBounds(1, out bounds, out error, verbose);
            if (host.StartsWith("unit:")) return UnitBounds(host.Substring(5), out bounds, out error, verbose);
            bounds = default;
            error = $"tmp: 宿主非法：{host}";
            Warn(verbose, error);
            return false;
        }

        /// <summary>宿主身上的 3D TMP 读数集合（unit 三槽 / 角色柱读数）。</summary>
        private static List<TMP_Text> TmpLabelsOf(string host)
        {
            var labels = new List<TMP_Text>();
            var view = BattleFieldView.Current;
            if (view == null) return labels;
            if (host == "hero" || host == "oppHero")
            {
                if (view.TryGetHeroVisual(host == "hero" ? 0 : 1, out _, out var stats) && stats != null)
                    labels.Add(stats);
                return labels;
            }
            if (host.StartsWith("unit:"))
                foreach (var card in FindBoardCards(host.Substring(5)))
                    if (view.TryGetUnitTmpLabels(card, out var slots))
                        labels.AddRange(slots);
            return labels;
        }

        private static bool CellsBounds(List<(int x, int z)> cells, out Bounds bounds, out string error, bool verbose)
        {
            bounds = default;
            error = null;
            var renderers = new List<Renderer>();
            foreach (var (x, z) in cells)
            {
                var tile = BattleTileFx.TileOf(x, z);
                var r = tile != null ? tile.GetComponent<Renderer>() : null;
                if (r != null) renderers.Add(r);
            }
            return Encapsulate(renderers, out bounds) ? true : Fail("格群无 renderer", out error, verbose);
        }

        private static bool Encapsulate(IEnumerable<Renderer> renderers, out Bounds bounds)
        {
            bounds = default;
            bool first = true;
            foreach (var r in renderers)
            {
                if (r == null) continue;
                if (first) { bounds = r.bounds; first = false; }
                else bounds.Encapsulate(r.bounds);
            }
            return !first;
        }

        private static bool Fail(string what, out string error, bool verbose)
        {
            error = what;
            Warn(verbose, error);
            return false;
        }

        private static void Warn(bool verbose, string what)
        {
            if (verbose) Debug.LogWarning($"[TutorialTarget] {what}");
        }
    }
}
