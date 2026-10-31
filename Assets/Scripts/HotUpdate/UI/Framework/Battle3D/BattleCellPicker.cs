using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace SynergyUI
{
    /// <summary>
    /// 通用 3D 选格器（2026-10-14 放地/出牌接线）：复用攻击箭头（BattleAttackArrow）的
    /// 「箭头随鼠标拉长 + 悬停合法变绿 + 确认吸附渐隐」交互骨架，但合法目标=宿主传入的
    /// 候选格集合（放地=候选元素色格；出牌永久物=己方单位区空格）。
    /// 候选格高亮每帧重应用（BattleTileFx 是全量对账模型，引擎事件 ClearAll 会清标记——
    /// 逐帧补点亮最稳）；选择结束由宿主 RefreshLocal 全量对账恢复正常高亮。
    ///（2026-10-18 晚修订：瞄准贴片只挂攻击选目标——放置/发动的候选高亮美术已够，选格不上贴片。）
/// 交互语义与 CombatChoreographer.TargetingLoopAsync 同源（右键/ESC/点无效格=取消；
/// 2026-10-18 触摸口径：长按滑动、箭头终点=手指点位（不偏置）、抬起确定——滑动落非法格
/// 只收滑动不取消）；与攻击选择的互斥由宿主屏保证（进入前取消在途选择）。纯表现层，不触引擎。
    /// </summary>
    public sealed class BattleCellPicker
    {
        /// <summary>候选格：ColorIndex=高亮色档（ManaType 序直下标 BattleTileFx.GlowColors）；
        /// -1=波纹档（单位格式样）。确认回调原样回传命中项。</summary>
        public struct CellOption
        {
            public int X;
            public int Z;
            public int ColorIndex;
        }

        private readonly BattleAttackArrow _arrow = new BattleAttackArrow();
        private int _epoch; // Begin/Dispose 代际：在途循环检测即弃
        private bool _active;
        private Vector3 _origin;
        private List<CellOption> _options;
        private Action<CellOption> _onConfirm;
        private Action _onCancel;

        /// <summary>选格进行中（宿主屏指针路由据此让路——与 _choreo.IsTargeting 同款）。</summary>
        public bool Active => _active;

        /// <summary>进入选格：箭头自 originWorld 拉向鼠标；确认回调在箭头渐隐后触发；
        /// 取消（含外部 Cancel）触发 onCancel（一次）。重复 Begin 自动顶掉上一轮。</summary>
        public void Begin(Vector3 originWorld, IReadOnlyList<CellOption> options,
            Action<CellOption> onConfirm, Action onCancel)
        {
            if (options == null || options.Count == 0) return;
            _epoch++;
            _origin = originWorld;
            _options = new List<CellOption>(options);
            _onConfirm = onConfirm;
            _onCancel = onCancel;
            _active = true;
            Debug.Log($"[CellPicker] 选格开始：起点({_origin.x:F2},{_origin.z:F2}) 候选{_options.Count}格"
                      + "——箭头随鼠标，悬停合法格变绿，左键确认，右键/ESC 取消；触摸=长按滑动（箭头终点=手指点位），抬起确定");
            LoopAsync(_epoch).Forget();
        }

        /// <summary>外部取消（回合切换/互斥接管等）：同用户取消语义走 onCancel。</summary>
        public void Cancel()
        {
            if (!_active) return;
            Finish(active: false);
            _onCancel?.Invoke();
        }

        public void Dispose()
        {
            _epoch++;
            _active = false;
            _onConfirm = null;
            _onCancel = null;
            _arrow.Dispose();
        }

        private async UniTaskVoid LoopAsync(int epoch)
        {
            bool openingFrame = true; // 首帧吞点击（同 CombatChoreographer：开启点击若非 UI 来源，
            while (_active && epoch == _epoch)                     // 同帧 MouseButtonDown 恒真会自取消）
            {
                if (!BattleBoardGrid.Ready) { Cancel(); return; } // 拆台即取消
                if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
                {
                    Debug.Log("[CellPicker] 选格取消（右键/ESC）");
                    Cancel();
                    return;
                }

                var cam = BattleStageDirector.WorldCam;
                if (cam == null) { _arrow.Hide(); await UniTask.Yield(); continue; }

                HighlightCandidates(); // 每帧重应用（对账 ClearAll 会清，逐帧补点亮）

                // 鼠标/触摸统一（2026-10-18 安卓滑动输入·BattlePointer）：裁决/箭头终点=指针落点
                // （触摸不偏置——手指盖住箭头时合法变绿经箭身可观察）；确认=左键按下/触摸抬起
                BattlePointer.Sample();
                bool touch = BattlePointer.TouchTracked;
                bool overUI = BattlePointer.OverUI;
                var aim = BattlePointer.ConfirmPressed ? BattlePointer.ConfirmPoint : BattlePointer.AimPoint;
                bool hasCell = RaycastCell(cam, aim, out int x, out int z);
                bool valid = !overUI && hasCell && IndexOf(x, z) >= 0;

                if ((!touch || BattlePointer.SlideActive)
                    && CombatChoreographer.MouseGroundPoint(cam, aim, out Vector3 visPt))
                    _arrow.Show(_origin, visPt, valid
                        ? BattleAttackArrow.ValidSkin : BattleAttackArrow.PlaceSkin); // 放地/入场=黄，选对=绿
                else
                    _arrow.Hide();

                if (!openingFrame && BattlePointer.ConfirmPressed && !overUI)
                {
                    if (valid)
                    {
                        await ConfirmAsync(x, z, epoch);
                        return;
                    }
                    if (!BattlePointer.ConfirmWasSlide)
                    {
                        Debug.Log("[CellPicker] 选格取消（点击无效格）");
                        Cancel();
                        return;
                    }
                    Debug.Log("[CellPicker] 滑动收在非法格——只收滑动，选格继续（触摸探索口径）");
                }
                openingFrame = false;
                await UniTask.Yield();
            }
        }

        /// <summary>确认：先退选择态（Active 即刻 false，宿主路由立刻恢复），箭头吸附目标格
        /// 冻结渐隐后回调（与攻击确认同节奏）。</summary>
        private async UniTask ConfirmAsync(int x, int z, int epoch)
        {
            var opt = _options[IndexOf(x, z)];
            var cb = _onConfirm;
            Finish(active: false);
            _arrow.Show(_origin, BattleBoardGrid.WorldPositionOf(x, z), BattleAttackArrow.ValidSkin);
            await _arrow.FadeOutAsync(0.35f);
            if (epoch != _epoch) return; // 渐隐期间被顶掉/Dispose：不再回调
            Debug.Log($"[CellPicker] 选格确认：({opt.X},{opt.Z}) 色档{opt.ColorIndex}");
            cb?.Invoke(opt);
        }

        private void Finish(bool active)
        {
            _active = active;
            if (!active) _arrow.Hide();
        }

        private int IndexOf(int x, int z)
        {
            for (int i = 0; i < _options.Count; i++)
                if (_options[i].X == x && _options[i].Z == z) return i;
            return -1;
        }

        /// <summary>候选格高亮：色档>=0=锁色描边环档（放地候选色，2026-10-17 起环样式），
        /// -1=蓝环档（单位空格候选，2026-10-30 起逐格波纹壳退役、环为逐格提示统一载体）。</summary>
        private void HighlightCandidates()
        {
            for (int i = 0; i < _options.Count; i++)
            {
                var o = _options[i];
                if (o.ColorIndex >= 0) BattleTileFx.SetLandGlow(o.X, o.Z, o.ColorIndex);
                else BattleTileFx.SetCellRing(o.X, o.Z, BattleTileFx.UnitGlowColor);
            }
        }

        /// <summary>tile-{x}-{z} BoxCollider 射线（与 CombatChoreographer.RaycastCell/HandleFieldClick 同口径同源；点=裁决落点）。</summary>
        private static bool RaycastCell(Camera cam, Vector2 screenPoint, out int x, out int z)
        {
            x = z = -1;
            RaycastHit hit;
            if (!Physics.Raycast(cam.ScreenPointToRay(screenPoint), out hit, 300f)) return false;
            var name = hit.collider != null ? hit.collider.gameObject.name : "";
            if (!name.StartsWith("tile-")) return false;
            var parts = name.Split('-');
            return parts.Length == 3 && int.TryParse(parts[1], out x) && int.TryParse(parts[2], out z)
                   && GameBoard.BoardMath.InBounds(x, z);
        }
    }
}
