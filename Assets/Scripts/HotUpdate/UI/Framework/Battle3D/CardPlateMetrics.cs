using UnityEngine;

namespace SynergyUI
{
    /// <summary>平贴卡面片规格单源（2026-11-15 地牌长条定案）：UnitCardPlate（生物脚下横置卡背）
    /// 与 LandStripFx（地牌面片）共读——宽 0.55（世界单位）、竖版高=宽×512/344≈0.819、
    /// 贴地抬升 0.012、转向 0.12s。调尺寸只改本类（UnitCardPlate 旧公共静态字段已迁入；
    /// Play 中 execute_code 直改 CardPlateMetrics.Width 仍即时生效）。</summary>
    public static class CardPlateMetrics
    {
        /// <summary>竖版卡面高宽比（Cardbg-b 344×512）。</summary>
        public const float Aspect = 512f / 344f;

        /// <summary>卡宽（世界单位；高=宽×Aspect）。缺省 0.55（格宽 1.1 的五成）。</summary>
        public static float Width = 0.55f;
        /// <summary>抬升（地块顶面之上，米）。缺省 0.012。</summary>
        public static float LiftY = 0.012f;
        /// <summary>旋转到位时间（秒，0=瞬切）。缺省 0.12。</summary>
        public static float TurnTime = 0.12f;

        /// <summary>可用姿态：竖版平贴（面朝上，长边沿 Z）。</summary>
        public static readonly Quaternion Portrait = Quaternion.Euler(90f, 0f, 0f);
        /// <summary>横置姿态：原地绕 Y 旋 90°（占 X 0.819 × Z 0.55）。</summary>
        public static readonly Quaternion Landscape = Quaternion.Euler(90f, 90f, 0f);
    }
}
