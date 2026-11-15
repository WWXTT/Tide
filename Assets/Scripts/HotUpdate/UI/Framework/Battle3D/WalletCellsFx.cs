using CardCore;
using GameBoard;
using TMPro;
using UnityEngine;

namespace SynergyUI
{
    /// <summary>
    /// 临时元素六分饼图（2026-11-15 用户预制体 land-pie.prefab）：**代码只写文本**——
    /// pie-1..pie-5=灰/红/蓝/绿/彩各扇区 label 写临时元素数量（初始 "0"），pie-0=地牌上限说明
    /// （cap-text，"n/上限" 初始 "0/0"，归 LandStripFx 写）。外观（扇区材质/图标/摆位/动画）
    /// 全归预制体，代码不改材质、不建件、不做显隐/脉冲。
    /// 本类仅另提供付费粒子锚点（CellAnchor=扇区世界位，只读）。
    /// </summary>
    public static class WalletCellsFx
    {
        private const float SliceMidR = 0.60f; // 扇区中弧半径（锚点取位，tile 局部）

        private static readonly TextMeshPro[][] _labels = { new TextMeshPro[5], new TextMeshPro[5] };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            for (int o = 0; o < 2; o++)
                for (int c = 0; c < 5; c++)
                    _labels[o][c] = null;
        }

        /// <summary>幂等填数（只写 text）：walletByColor 下标=ManaType 序 [灰,红,蓝,绿,彩]。
        /// 初始/0 值显示 "0"（预制体初始口径），扇区=pie-(元素序+1)。</summary>
        public static void Reconcile(int owner, int[] walletByColor)
        {
            for (int c = 0; c < 5; c++)
            {
                var label = EnsureLabel(owner, c);
                if (label == null) continue;
                int value = walletByColor != null && c < walletByColor.Length ? walletByColor[c] : 0;
                string txt = value.ToString(); // 0 也显示（初始 "0"）
                if (label.text != txt) label.text = txt;
            }
        }

        /// <summary>扇区世界锚点（付费粒子发射点=扇区中弧上方；只读，不改预制体）。</summary>
        public static bool CellAnchor(int owner, int colorIndex, out Vector3 pos)
        {
            pos = default;
            if (owner < 0 || owner > 1 || colorIndex < 0 || colorIndex > 4) return false;
            var any = BoardLayout.AnyElementCell(owner);
            var tile = BattleTileFx.TileOf(any.x, any.z);
            var pie = tile != null ? tile.Find("land-pie/pie-" + (colorIndex + 1)) : null;
            if (pie == null) return false;
            pos = pie.TransformPoint(new Vector3(SliceMidR, 0.05f, 0f)) + Vector3.up * 0.05f;
            return true;
        }

        private static TextMeshPro EnsureLabel(int owner, int c)
        {
            if (_labels[owner][c] is TextMeshPro cached && cached != null) return cached;
            var any = BoardLayout.AnyElementCell(owner);
            var tile = BattleTileFx.TileOf(any.x, any.z);
            var labelT = tile != null ? tile.Find("land-pie/pie-" + (c + 1) + "/label") : null;
            var label = labelT != null ? labelT.GetComponent<TextMeshPro>() : null;
            if (label != null) _labels[owner][c] = label; // 缺件不建不告警轰炸（一次性告警）
            return label;
        }

        /// <summary>拆台清引用（DismountBoard 调用；文本件随棋盘根销毁，无运行时资产可清）。</summary>
        public static void Unmount()
        {
            for (int o = 0; o < 2; o++)
                for (int c = 0; c < 5; c++)
                    _labels[o][c] = null;
        }
    }
}
