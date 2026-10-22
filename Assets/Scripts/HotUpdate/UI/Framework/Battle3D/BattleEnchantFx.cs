using System.Collections.Generic;
using GameBoard;
using Tide.HotUpdate;
using UnityEngine;

namespace SynergyUI
{
    /// <summary>
    /// 结界模型演出组件（2026-10-14）：BattleEnchant.prefab 根挂载——
    /// ring=光环指示环（有链接光环效果的结界才显示，BattleCardView.HasAura 判定），
    /// 绕圆锥腰上下浮动+缓自转；arrows=底座六向三角（arrow-NE/E/SE/SW/W/NW），
    /// 按卡面箭头 HexDirection 逐位映射 BoardDirection 点亮（scope=0 箭头模式才有箭头，
    /// 对手归属取 Opposite 镜像，与 ScreenArrowBits/LinkAuraSystem 同规——3D 棋盘为绝对方向）。
    /// </summary>
    public sealed class BattleEnchantFx : MonoBehaviour
    {
        [SerializeField] private Transform _ring; // 光环环（生成器 SerializedObject 注入）

        private float _ringBaseY = 0.55f;

        private static readonly string[] ArrowNames = { "NE", "E", "SE", "SW", "W", "NW" };
        // CardCore.HexDirection 六单值（枚举位）——MapArrow 入参须单值（switch 语义，组合落 default）
        private static readonly CardCore.HexDirection[] HexSingles =
        {
            CardCore.HexDirection.Up, CardCore.HexDirection.UpperRight, CardCore.HexDirection.LowerRight,
            CardCore.HexDirection.Down, CardCore.HexDirection.LowerLeft, CardCore.HexDirection.UpperLeft,
        };

        private void Awake()
        {
            if (_ring != null) _ringBaseY = _ring.localPosition.y;
        }

        private void Update()
        {
            if (_ring == null) return;
            float t = Time.time * (Mathf.PI * 2f / 2.6f); // 周期 2.6s
            var p = _ring.localPosition;
            p.y = _ringBaseY + 0.07f * Mathf.Sin(t);
            _ring.localPosition = p;
            _ring.Rotate(0f, 45f * Time.deltaTime, 0f, Space.Self);
        }

        /// <summary>结界视觉对账：箭头方向（卡面 HexDirection 位）/ 光环环显隐。
        /// ownerIsOpponent=对手归属（箭头绝对方向镜像，同 ScreenArrowBits 口径）。</summary>
        public void Apply(CardCore.HexDirection dirs, bool ownerIsOpponent, bool hasAura)
        {
            if (_ring != null) _ring.gameObject.SetActive(hasAura);

            var arrows = transform.Find("arrows");
            if (arrows == null) return;

            // 卡面位 → 棋盘绝对方向位（逐单值过 MapArrow；对手侧 Opposite）
            var lit = new HashSet<BoardDirection>();
            foreach (var single in HexSingles)
            {
                if ((dirs & single) == 0) continue;
                var board = BoardMath.MapArrow(single);
                if (ownerIsOpponent) board = BoardMath.Opposite(board);
                lit.Add(board);
            }
            foreach (var name in ArrowNames)
            {
                var child = arrows.Find("arrow-" + name);
                if (child == null) continue;
                BoardDirection dir;
                if (!System.Enum.TryParse(name, out dir)) continue;
                child.gameObject.SetActive(lit.Contains(dir));
            }
        }
    }

    /// <summary>结界模型 prefab 加载（缓存+fake-null 防御；路径锚定 Assets/Art/Prefab）。</summary>
    public static class BattleEnchantModel
    {
        private const string PrefabAddress = "BattleEnchant";
        private const string PrefabPath = "Assets/Art/Prefab/BattleEnchant.prefab";

        private static GameObject _cache;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _cache = null; // 域重载关闭（Enter Play Mode Options）防跨 Play 残留

        /// <summary>结界 prefab；null=缺失（调用方回落占位胶囊并告警一次）。</summary>
        public static GameObject Load()
        {
            if (_cache != null) return _cache;
            _cache = HotUpdateAssets.Load<GameObject>(PrefabAddress, PrefabPath);
            if (_cache == null)
                Debug.LogWarning("[BattleEnchant] BattleEnchant.prefab 缺失（" + PrefabPath + "）——结界回落占位胶囊（该预制体已资产化，缺失时从版本库恢复，无生成菜单）");
            return _cache;
        }
    }
}
