using System;
using CardCore;
using UnityEngine;

namespace SynergyUI
{
    /// <summary>战斗 VFX 路由角色（Phase 1=Cast/Projectile/Hit 主链 + Area/Heal 可选；
    /// Buff/Shield 预留 Phase 2；Summon 预留——召唤演出由 BattleCastArrival 独占，不经本表）。</summary>
    public enum VfxRole
    {
        Cast = 0,
        Projectile = 1,
        Hit = 2,
        Area = 3,
        Heal = 4,
        Buff = 5,
        Shield = 6,
        Summon = 7,
    }

    /// <summary>路由单条：颜色+角色 → 粒子 prefab（普通 prefab 序列化引用——热更程序集挂
    /// MonoBehaviour 持引用，规避 ScriptableObject 在热更程序集的资产序列化风险）。
    /// scale/life 为演出档（BattleVfxFx 播放时与调用点 scale 相乘 / 定时销毁用）。</summary>
    [Serializable]
    public class VfxEntry
    {
        public ManaType color;
        public VfxRole role;
        public GameObject prefab;
        public float scale = 1f;
        public float life = 0.8f;
    }

    /// <summary>战斗 VFX 路由表（Phase 1 2026-10-23）：按颜色+角色查粒子 prefab。
    /// 资产=Assets/Art/VFX/BattleVfxCatalog.prefab（Assets/Editor/配置/BattleVfxCatalogBuilder
    /// 菜单生成）；运行时由 BattleVfxFx 加载实例化取件，不常驻场景。
    /// Find 精确匹配（缺配/未命中返回 null——调用方兜底 BattleEffectFx 的 MagicOrb+Impact）；
    /// 同 (color,role) 多条时取数组首条（次选条目排在后面，调序即换件）。</summary>
    public class BattleVfxCatalog : MonoBehaviour
    {
        public VfxEntry[] entries;

        /// <summary>查表：prefab 为空的条目视为缺配（生成器缺件时仍占位便于检视）。</summary>
        public VfxEntry Find(ManaType c, VfxRole r)
        {
            if (entries == null) return null;
            for (int i = 0; i < entries.Length; i++)
            {
                var e = entries[i];
                if (e != null && e.color == c && e.role == r && e.prefab != null) return e;
            }
            return null;
        }
    }
}
