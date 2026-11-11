using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace SynergyUI
{
    /// <summary>
    /// 格子按压反馈（2026-11-11 木桩测试需求）：点击战场任意六边形格（tile-{x}-{z}——单位格/
    /// 地牌格/特殊格/角色格/空格不限），整格像按钮按下再弹起：竖直下沉 0.045（格子高 3%~6%·
    /// 世界单位区间中值）→ 谷底短停 → 平滑回位，全程 0.2s 干脆不拖沓。纯视觉 fire-and-forget：
    /// 不改点击路由、不阻塞输入、不进结算 await 链（调用点一行、本类自持循环）。
    ///
    /// 双轨位移（挂件层级现实，2026-11-11 逐一核实）：格面读数三槽/地牌计数条/元素图标/高光环/
    /// 特殊格道具/生物箭头均钉 tile 下随 tile 动；但单位 wrapper（unit-{rid}）、施放占格图标
    ///（cast-{rid}）、角色柱（hero-*）世界坐标锚定挂 _root 不钉 tile——由 BattleFieldView.
    /// CollectPressAnchors 采集，与 tile 同拍同深同步下沉回升，防「只动格子不动挂件」错位。
    /// 位移全程偏移记账：每帧先撤销上帧自身偏移再写新偏移——对账/编舞中途改写挂件位姿不被吞
    ///（撤销的只是本动画的份额）。
    ///
    /// 重入与兜底：同格连点=钉回原位后整段重放（在途循环发现状态易主即静默退场，绝不叠加偏移）；
    /// tile 终态精确钉回按压前采集的原始 localPosition；挂件 fake-null（死亡/回收/拆台）逐件跳过。
    /// 开场不触发：拼装窗（BattleBoardAssemble.Running）直接忽略——基线在飞行位，采了会污染；
    /// 五段式开场期点击本就被 BattleOpeningDirector.Active 挡在 PollFieldPointer 顶部进不来。
    /// 动画不动父子关系（BattleTileFx.TileOf 缓存口径不变）。
    /// </summary>
    public static class BattleTilePress
    {
        // 观感参数（2026-11-11 需求：深度=格子高 3%~6%≈世界 0.03~0.06、总长 0.15~0.25s，留现场微调）
        private const float DepthWorld = 0.045f; // 下沉深度（世界单位，区间中值）
        private const float DownSeconds = 0.06f; // 按下段（easeIn 加速下沉）
        private const float HoldSeconds = 0.04f; // 谷底停留段
        private const float UpSeconds = 0.10f;   // 回弹段（easeOut 减速收停）
        private const float TotalSeconds = DownSeconds + HoldSeconds + UpSeconds; // 0.20s

        /// <summary>在途按压状态（键=格 x&lt;&lt;32|z；同格连点=顶掉重放）。</summary>
        private sealed class PressState
        {
            public Transform Tile;          // tile-{x}-{z} 根
            public Vector3 TileOrigin;      // 按压前原始 localPosition（终态精确钉回锚）
            public Vector3 TileApplied;     // 当前已写入 tile 的偏移（父空间·偏移记账）
            public Transform[] Anchors;     // 世界锚定挂件（单位 wrapper/施放图标/角色柱）
            public Vector3[] AnchorApplied; // 各挂件当前已写入的世界偏移（偏移记账）
        }

        private static readonly Dictionary<long, PressState> _running = new Dictionary<long, PressState>();

        /// <summary>域重载关闭（Enter Play Mode Options）防跨 Play 残留：与 BattleTileFx.ResetStatics 同口径。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _running.Clear();

        /// <summary>播放按压（BattleScreen.HandleFieldClick 在 TryParseTile 成功后调用——各 role 分发前，
        /// 全角色格触发）。field=null（视图未建/异常）只压 tile 本体。拼装窗内静默忽略。</summary>
        public static void Play(BattleFieldView field, int x, int z)
        {
            if (BattleBoardAssemble.Running) return; // 拼装/开场浮起段不触发
            var tile = BattleTileFx.TileOf(x, z);
            if (tile == null) return;

            var key = (long)x << 32 | (uint)z;
            if (_running.TryGetValue(key, out var prev))
            {
                Restore(prev);     // 连点重入：先钉回原位再整段重放（旧循环下帧发现易主静默退场）
                _running.Remove(key);
            }

            var anchors = field != null ? field.CollectPressAnchors(x, z) : null;

            // tile 偏移走父空间：世界深度 ÷ 父级 Y 缩放（棋盘根轴对齐无旋转，Y 轴独立换算即可）
            float parentScaleY = tile.parent != null ? Mathf.Max(0.0001f, Mathf.Abs(tile.parent.lossyScale.y)) : 1f;

            var st = new PressState
            {
                Tile = tile,
                TileOrigin = tile.localPosition,
                Anchors = anchors != null ? anchors.ToArray() : new Transform[0],
            };
            st.AnchorApplied = new Vector3[st.Anchors.Length];
            _running[key] = st;
            RunAsync(key, st, DepthWorld / parentScaleY).Forget();
        }

        /// <summary>还原到按压前：tile 钉回原始 localPosition（精确）；挂件只撤销自身已写偏移
        ///（按压期间对账/编舞可能已合法改写其位姿，钉旧值会吞掉别人的份额）。</summary>
        private static void Restore(PressState st)
        {
            if (st.Tile != null) st.Tile.localPosition = st.TileOrigin;
            if (st.Anchors == null) return;
            for (int i = 0; i < st.Anchors.Length; i++)
            {
                var a = st.Anchors[i];
                if (a != null && st.AnchorApplied[i] != Vector3.zero) a.position -= st.AnchorApplied[i];
            }
        }

        private static async UniTaskVoid RunAsync(long key, PressState st, float depthLocal)
        {
            float t0 = Time.realtimeSinceStartup;
            while (true)
            {
                await UniTask.Yield();
                // 易主（同格连点重放）：新主已接管还原，旧循环静默退场不还原（防双重还原打架）
                if (!_running.TryGetValue(key, out var cur) || !ReferenceEquals(cur, st)) return;
                if (st.Tile == null) { _running.Remove(key); return; } // 拆台：tile 随盘销毁无物可还原

                float t = Time.realtimeSinceStartup - t0;
                float k = PressK(t); // 当前下沉保持量 0..1

                // tile（父空间·偏移记账）
                st.Tile.localPosition -= st.TileApplied;
                st.TileApplied = Vector3.down * (depthLocal * k);
                st.Tile.localPosition += st.TileApplied;

                // 世界锚定挂件（世界空间·同拍同深）
                for (int i = 0; i < st.Anchors.Length; i++)
                {
                    var a = st.Anchors[i];
                    if (a == null) continue; // 挂件销毁（死亡/回收）跳过
                    a.position -= st.AnchorApplied[i];
                    st.AnchorApplied[i] = Vector3.down * (DepthWorld * k);
                    a.position += st.AnchorApplied[i];
                }

                if (t >= TotalSeconds) break;
            }

            // 终态：tile 精确钉回 + 挂件撤销末帧偏移（k=0 偏移已归零，此处兜浮点残差）
            Restore(st);
            _running.Remove(key);
        }

        /// <summary>下沉保持量包络：easeIn 加速按下 → 谷底驻留 → easeOut 减速回弹（「干脆不拖沓」节奏源）。</summary>
        private static float PressK(float t)
        {
            if (t <= 0f) return 0f;
            if (t < DownSeconds) { var u = t / DownSeconds; return u * u; }
            if (t < DownSeconds + HoldSeconds) return 1f;
            var v = Mathf.Clamp01((t - DownSeconds - HoldSeconds) / UpSeconds);
            return (1f - v) * (1f - v); // 1→0 平方缓出（等价 1−easeOut）
        }
    }
}
