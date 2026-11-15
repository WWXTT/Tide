using System.Collections.Generic;
using CardCore;
using GameBoard;
using Tide.HotUpdate;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SynergyUI
{
    /// <summary>
    /// 左下临时元素 5 分格（2026-11-15 定案）：万能展示格 (1,0)／P2 镜像 (6,6) 拆成 5 条色片
    ///（灰|红|蓝|绿|彩，玩家自身视角左→右），数字=TempElements（钱包该色余额）；0 →压暗 0.35
    /// 且不显示 0（防五格全 0 噪点）。费用粒子全部从对应色片发出（PlayWalletPayByRef）。
    ///
    /// 几何=BattleStage.prefab 预烘焙（tile-1-0/tile-6-6 下 wallet-cell-{gray|red|blue|green|any}
    /// 5 个子节点，无 collider 不截射线）；代码只按名绑定、填数、脉冲。数字标签=运行时平贴 TMP
    ///（EnsureFlatCellLabel 同口径）。色片材质=运行时克隆实例（Zone02 模板，底色单源=
    /// UiStyle.ManaBlock*）；生命周期随 LandStripFx（DismountBoard→Unmount）。
    /// </summary>
    public static class WalletCellsFx
    {
        public static readonly string[] CellNames = { "wallet-cell-gray", "wallet-cell-red", "wallet-cell-blue", "wallet-cell-green", "wallet-cell-any" };

        private const string ZoneMatPath = "Assets/Art/Materials/BattleTile_Zone02_P1Unit.mat"; // 着色克隆模板（与角色柱/长条同源）
        private static readonly Color DimMul = new Color(0.35f, 0.35f, 0.35f); // 0 余额压暗乘子

        private sealed class CellEntry
        {
            public Transform Root;
            public Renderer Rend;
            public WalletCellView View;
            public TextMeshPro Label;
            public int Shown = -1;
        }

        private static readonly CellEntry[][] _cells = { new CellEntry[5], new CellEntry[5] };
        private static readonly Material[] _mats = new Material[5];   // 常态（余额>0）
        private static readonly Material[] _dimMats = new Material[5]; // 压暗（余额 0）
        private static bool _warned;

        /// <summary>域重载防跨 Play 残留（与 BattleTileFx.ResetStatics 同口径）。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            for (int o = 0; o < 2; o++)
                for (int c = 0; c < 5; c++)
                    _cells[o][c] = null;
            System.Array.Clear(_mats, 0, _mats.Length);
            System.Array.Clear(_dimMats, 0, _dimMats.Length);
            _warned = false;
        }

        // ======================================== 对账（BattleFieldView.ReconcileLands 驱动） ========================================

        /// <summary>幂等填数：walletByColor 下标=ManaType 序 [灰,红,蓝,绿,彩]。数值变化触发脉冲。</summary>
        public static void Reconcile(int owner, int[] walletByColor)
        {
            for (int c = 0; c < 5; c++)
            {
                var e = EnsureCell(owner, c);
                if (e == null) continue;
                int value = walletByColor != null && c < walletByColor.Length ? walletByColor[c] : 0;
                bool bright = value > 0;
                if (e.Rend != null)
                {
                    var m = bright ? MatOf(c, false) : MatOf(c, true);
                    if (m != null) e.Rend.sharedMaterial = m;
                }
                if (e.Label != null)
                {
                    string txt = bright ? value.ToString() : ""; // 0 不显示数字（压暗片本身示意）
                    if (e.Label.text != txt) e.Label.text = txt;
                }
                if (e.Shown != -1 && e.Shown != value && e.View != null)
                    e.View.Pulse(e.Shown < value); // +1 上跳 / -N 下跳（方向暂同款脉冲）
                e.Shown = value;
            }
        }

        // ======================================== 锚点/脉冲（付费粒子消费） ========================================

        /// <summary>色片世界锚点（粒子发射点=片中心上方 0.05；未装格返回 false）。</summary>
        public static bool CellAnchor(int owner, int colorIndex, out Vector3 pos)
        {
            pos = default;
            if (owner < 0 || owner > 1 || colorIndex < 0 || colorIndex > 4) return false;
            var e = _cells[owner][colorIndex];
            if (e?.Root == null) return false;
            pos = e.Root.position + Vector3.up * 0.05f;
            return true;
        }

        /// <summary>付费脉冲（PlayWalletPayByRef 消费）：数字下跳+缩放脉冲。</summary>
        public static void Pulse(int owner, int colorIndex)
        {
            if (owner < 0 || owner > 1 || colorIndex < 0 || colorIndex > 4) return;
            _cells[owner][colorIndex]?.View?.Pulse(false);
        }

        // ======================================== 内部 ========================================

        private static CellEntry EnsureCell(int owner, int c)
        {
            if (_cells[owner][c] is CellEntry cached && cached.Root != null) return cached;
            var any = BoardLayout.AnyElementCell(owner);
            var tile = BattleTileFx.TileOf(any.x, any.z);
            if (tile == null) return null; // 未装台/格名异常：不建，下一事件重试（同 EnsureFlatCellLabel 口径）
            var child = tile.Find(CellNames[c]);
            if (child == null)
            {
                if (!_warned)
                {
                    _warned = true;
                    Debug.LogWarning("[Wallet] BattleStage.prefab 缺 wallet-cell-* 节点（tile-" + any.x + "-" + any.z
                                     + " 下）——5 分格降级为不显示（重跑烘焙菜单恢复）");
                }
                return null;
            }

            var e = new CellEntry { Root = child, Rend = child.GetComponent<Renderer>() };
            if (e.Rend != null)
            {
                e.Rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                e.Rend.receiveShadows = false;
            }
            e.View = child.gameObject.AddComponent<WalletCellView>();
            e.Label = CreateLabel(child);
            _cells[owner][c] = e;
            return e;
        }

        /// <summary>数字标签（平贴 TMP·EnsureFlatCellLabel 同口径）：挂 **tile** 下（非色片——
        /// 色片 localScale 非均匀 (0.378,0.67)，子件会随片压扁），位姿=色片位 + y 0.014（色片
        /// 0.010 之上），localScale=1/TileScale 反缩（标签世界尺寸不随格缩小）。</summary>
        private static TextMeshPro CreateLabel(Transform cell)
        {
            if (TMP_Settings.defaultFontAsset == null)
            {
                if (!_warned) { _warned = true; Debug.LogWarning("[Wallet] TMP 默认字体未配置——分格数字禁用"); }
                return null;
            }
            var go = new GameObject("label-" + cell.name);
            go.transform.SetParent(cell.parent, false); // tile（色片的父）
            go.transform.localPosition = cell.localPosition + new Vector3(0f, 0.004f, 0f);
            go.transform.localRotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
            go.transform.localScale = Vector3.one / BattleBoardGrid.TileScale;
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.fontSize = 0.5f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.rectTransform.sizeDelta = new Vector2(0.3f, 0.44f);
            return tmp;
        }

        private static Material MatOf(int color, bool dim)
        {
            var cache = dim ? _dimMats : _mats;
            if (cache[color] != null) return cache[color];
            var template = HotUpdateAssets.Load<Material>("BattleTile_Zone02", ZoneMatPath);
            if (template == null)
            {
                if (!_warned) { _warned = true; Debug.LogWarning("[Wallet] 着色模板缺失（Zone02）——分格走预制体自带材质"); }
                return null;
            }
            var c = BattleTileFx.ElementColorOf(color);
            if (dim) c *= DimMul;
            var m = new Material(template) { name = "Wallet_" + color + (dim ? "_D" : "") };
            m.SetColor("_BaseColor", c);
            cache[color] = m;
            return m;
        }

        /// <summary>拆台清引用（色片/标签随棋盘根销毁；运行时材质显式销毁——DismountBoard 调用）。</summary>
        public static void Unmount()
        {
            for (int o = 0; o < 2; o++)
                for (int c = 0; c < 5; c++)
                    _cells[o][c] = null;
            for (int c = 0; c < 5; c++)
            {
                DestroyMat(_mats[c]);
                DestroyMat(_dimMats[c]);
                _mats[c] = null;
                _dimMats[c] = null;
            }
        }

        private static void DestroyMat(Material m)
        {
            if (m == null) return;
            if (Application.isPlaying) Object.Destroy(m);
            else Object.DestroyImmediate(m);
        }

        /// <summary>色片脉冲件（数值变化/付费触发）：0.3s 缩放脉冲（1.0→1.22→1.0，回乘基准防漂移）。</summary>
        private sealed class WalletCellView : MonoBehaviour
        {
            private float _pulse;
            private Vector3 _base = Vector3.zero;

            public void Pulse(bool up) => _pulse = up ? 1f : 0.7f;

            private void LateUpdate()
            {
                if (_pulse <= 0f) return;
                if (_base == Vector3.zero) _base = transform.localScale;
                _pulse = Mathf.Max(0f, _pulse - Time.deltaTime * 3.3f);
                float k = 1f + 0.22f * Mathf.Sin(_pulse * Mathf.PI); // 半正弦回弹
                transform.localScale = new Vector3(_base.x * k, _base.y * k, _base.z);
            }
        }
    }
}
