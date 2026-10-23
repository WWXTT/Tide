using System.Collections.Generic;
using System.Text;
using CardCore;
using SynergyUI;
using UnityEditor;
using UnityEngine;

namespace Tide.HotUpdateTools
{
    /// <summary>
    /// BattleVfxCatalog prefab 生成器（Phase 1 2026-10-23；10-23 晚视觉整改=按角色目标尺寸归一化）：
    /// 菜单按清单重建 Assets/Art/VFX/BattleVfxCatalog.prefab——付费包 prefab 只引用不改动。
    ///
    /// 归一化（整改 B 案）：各 prefab 原生尺寸差异巨大（Synty 大爆 nativeR≈257 / PixPlays 弹体
    /// ≈0.17），不能统一 scale=1 靠调用点缩。生成时逐件实例化采样（激活全层级+按角色 Simulate）
    /// 量 Renderer.bounds 最大半轴=nativeR，entry.scale = targetSize/nativeR——entry.scale 即
    /// 绝对目标尺寸，调用点恒传 1f（勿双重缩放）。nativeR≈0（不可见件如 ShadowMissile 0.3s
    /// 无粒子）跳过并告警，不入表。
    ///
    /// 角色映射：六色主链 Cast/Projectile/Hit + Area/Heal + Buff/Shield（Phase 2 预留）；
    /// Beam 三件无角色仍不入表。同 (color,role) 多条=首条为主（调序换件）。
    /// 黑·Cast 用 MysticExplosionDark（Synty 大暗爆 257 单位已废弃）；黑·弹体首条 MysticMissileDark。
    /// 缺件 Debug.LogError 跳过——Find 视 null prefab 为缺配自动降级 BattleEffectFx。
    /// </summary>
    public static class BattleVfxCatalogBuilder
    {
        private const string OutputPath = "Assets/Art/VFX/BattleVfxCatalog.prefab";
        private const string MenuPath = "Tide/VFX/重建 BattleVfxCatalog（Phase 1）";

        /// <summary>角色目标尺寸（世界单位·半轴）：约一个六边形格 1.7 的比例基准。
        /// Cast=施法者脚下法阵；Projectile=飞行弹体（半轴 0.5≈直径 1 格）；Hit=命中爆点；
        /// Area=范围爆（覆盖多格）；Heal/Buff/Shield=驻留氛围件。</summary>
        private static float TargetSize(VfxRole role)
        {
            switch (role)
            {
                case VfxRole.Cast: return 1.0f;
                case VfxRole.Projectile: return 0.5f;
                case VfxRole.Hit: return 1.2f;
                case VfxRole.Area: return 2.0f;
                case VfxRole.Heal: return 1.0f;
                case VfxRole.Buff: return 1.2f;
                case VfxRole.Shield: return 1.2f;
                default: return 1.0f;
            }
        }

        /// <summary>手工缩放覆盖（按 prefab 名）：自动归一化会把 TrailRenderer/Smoke 的包围盒
        /// 算进 nativeR（Fireball 弹体 trail 拉大、FireballHit smoke 拉到 10+），导致本体过小；
        /// 这些件用实测/验收值直接钉死，优先于自动归一化。</summary>
        private static readonly Dictionary<string, float> ScaleOverrides = new Dictionary<string, float>
        {
            { "Fireball", 2.8f },
            { "FireballCast", 3.5f },
            { "FireballHit", 2.5f },
            { "WaterballCast", 1.2f },
            { "WaterballProjectile", 2.5f },
            { "WaterballHit", 1.0f },
            { "MysticExplosionWhite", 0.6f },
            { "MysticExplosionDark/Cast", 0.45f }, // 只覆盖 Cast；Area 仍走自动归一化
            { "ShadowExplosion", 0.6f },
            { "MagicExplosionGreen", 0.5f },
            { "MagicExplosionBlue", 0.5f },
            { "Glowing orb 37", 0.6f },
        };

        private static bool TryGetOverride(string prefabName, VfxRole role, out float value)
        {
            if (ScaleOverrides.TryGetValue(prefabName + "/" + role, out value)) return true;
            return ScaleOverrides.TryGetValue(prefabName, out value);
        }

        /// <summary>采样时刻（秒）：快节点取爆发前中段，驻留节点取成形期。</summary>
        private static float SampleSeconds(VfxRole role)
        {
            switch (role)
            {
                case VfxRole.Cast:
                case VfxRole.Projectile:
                case VfxRole.Hit:
                    return 0.3f;
                default:
                    return 0.8f;
            }
        }

        /// <summary>驻留类节点销毁时长（秒；快节点用 VfxEntry 默认 0.8）。</summary>
        private static float LifeFor(VfxRole role)
            => role == VfxRole.Area || role == VfxRole.Heal ? 1.2f : 0.8f;

        private struct Spec
        {
            public ManaType Color;
            public VfxRole Role;
            public string Path;

            public Spec(ManaType color, VfxRole role, string path)
            { Color = color; Role = role; Path = path; }
        }

        [MenuItem(MenuPath)]
        public static void Rebuild()
        {
            var specs = BuildSpecs();
            var entries = new List<VfxEntry>();
            int missing = 0, invisible = 0;
            var report = new StringBuilder();
            foreach (var s in specs)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(s.Path);
                if (prefab == null)
                {
                    Debug.LogError($"[BattleVfxCatalog] 缺件跳过：{s.Path}（{s.Color}/{s.Role} 将降级 BattleEffectFx）");
                    missing++;
                    continue;
                }
                float scale;
                if (TryGetOverride(prefab.name, s.Role, out float overridden))
                {
                    scale = overridden;
                    report.Append($"\n  {s.Color}/{s.Role} = {prefab.name}  scale={scale:0.00}（手工覆盖）");
                }
                else
                {
                    float nativeR = MeasureNativeRadius(prefab, SampleSeconds(s.Role));
                    if (nativeR < 0.01f)
                    {
                        Debug.LogWarning($"[BattleVfxCatalog] 不可见件跳过：{s.Path}（{s.Color}/{s.Role} 采样 {SampleSeconds(s.Role):0.#}s nativeR={nativeR:0.000}）——不入表，该色该角色降级 BattleEffectFx");
                        invisible++;
                        continue;
                    }
                    scale = TargetSize(s.Role) / Mathf.Max(nativeR, 0.01f);
                    report.Append($"\n  {s.Color}/{s.Role} = {prefab.name}  nativeR={nativeR:0.00} → scale={scale:0.00}（目标 {TargetSize(s.Role):0.0}）");
                }
                entries.Add(new VfxEntry { color = s.Color, role = s.Role, prefab = prefab, scale = scale, life = LifeFor(s.Role) });
            }

            var host = new GameObject("BattleVfxCatalog");
            try
            {
                var catalog = host.AddComponent<BattleVfxCatalog>();
                catalog.entries = entries.ToArray();
                PrefabUtility.SaveAsPrefabAsset(host, OutputPath);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var saved = AssetDatabase.LoadAssetAtPath<GameObject>(OutputPath);
            Selection.activeObject = saved;
            EditorGUIUtility.PingObject(saved);
            Debug.Log($"[BattleVfxCatalog] 重建完成：{entries.Count} 条写入 {OutputPath}"
                      + $"{(missing > 0 ? $"，缺件 {missing} 条" : "")}{(invisible > 0 ? $"，不可见跳过 {invisible} 条" : "")}"
                      + $"{report}");
        }

        /// <summary>采样原生半径（prefab 自身 scale=1 下的世界半轴）：临时实例化→禁脚本→激活全层级
        /// →逐系统 Simulate 到采样时刻→取所有 Renderer.bounds 最大半轴。实例化在原点，局部=世界。</summary>
        private static float MeasureNativeRadius(GameObject prefab, float sampleSeconds)
        {
            var go = Object.Instantiate(prefab);
            try
            {
                foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
                    mb.enabled = false; // 演示脚本不自毁/不自驱（编辑态本不 tick，双保险）
                foreach (var t in go.GetComponentsInChildren<Transform>(true))
                    t.gameObject.SetActive(true); // 付费包根节点常未激活（FireballHit 根=容器）
                foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
                    ps.Simulate(sampleSeconds, false, true, true); // 各系统独立推到同一时刻（restart+定步长）
                float r = 0f;
                foreach (var rend in go.GetComponentsInChildren<Renderer>(true))
                {
                    // Trail/Line 的 bounds 沿飞行轨迹拉长，不代表本体尺寸（会把弹体缩到不可见）
                    if (rend is TrailRenderer || rend is LineRenderer) continue;
                    var e = rend.bounds.extents;
                    r = Mathf.Max(r, e.x, e.y, e.z);
                }
                return r;
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>Phase 1 清单（全 Version_URP/现有路径）。分组注释=分工：火水 PixPlays、
        /// 绿白黑 Epic Toon、灰 Synty+Hovl；Hovl 光球做弹体/Buff 氛围件。</summary>
        private static List<Spec> BuildSpecs()
        {
            const string pix = "Assets/Packages/PixPlays";
            const string etf = "Assets/Packages/Epic Toon FX/Prefabs";
            const string syn = "Assets/Packages/Synty/PolygonParticleFX/Prefabs";
            const string hov = "Assets/Packages/Hovl Studio/Glowing orbs Vol 3/Prefabs";
            var l = new List<Spec>();

            // ---- 红·火（PixPlays 独占）----
            l.Add(new Spec(ManaType.Red, VfxRole.Cast, $"{pix}/ElementalProjectiles/Fireball/Version_URP/FireballCast/FireballCast.prefab"));
            l.Add(new Spec(ManaType.Red, VfxRole.Projectile, $"{pix}/ElementalProjectiles/Fireball/Version_URP/FireballProjectile/Fireball.prefab"));
            l.Add(new Spec(ManaType.Red, VfxRole.Hit, $"{pix}/ElementalProjectiles/Fireball/Version_URP/FireballHit/FireballHit.prefab"));
            l.Add(new Spec(ManaType.Red, VfxRole.Area, $"{pix}/ElementalAOE/FireAOE/Version_URP/FireAoeVFX.prefab"));

            // ---- 蓝·水（PixPlays 独占）----
            l.Add(new Spec(ManaType.Blue, VfxRole.Cast, $"{pix}/ElementalProjectiles/Waterball/Version_URP/WaterballCast/WaterballCast.prefab"));
            l.Add(new Spec(ManaType.Blue, VfxRole.Projectile, $"{pix}/ElementalProjectiles/Waterball/Version_URP/WaterballProjectile/WaterballProjectile.prefab"));
            l.Add(new Spec(ManaType.Blue, VfxRole.Hit, $"{pix}/ElementalProjectiles/Waterball/Version_URP/WaterExplosion/WaterballHit.prefab"));
            l.Add(new Spec(ManaType.Blue, VfxRole.Area, $"{pix}/ElementalAOE/WaterAOE/Version_URP/WaterAoeVFX.prefab"));

            // ---- 绿·木（Epic Toon + Hovl 光球作范围标记）----
            l.Add(new Spec(ManaType.Green, VfxRole.Cast, $"{etf}/Combat/Magic/Circle/MagicCircleGreen.prefab"));
            l.Add(new Spec(ManaType.Green, VfxRole.Projectile, $"{etf}/Combat/Missiles/Magic/MagicMissileGreen.prefab"));
            l.Add(new Spec(ManaType.Green, VfxRole.Hit, $"{etf}/Combat/Explosions/MagicExplosion/MagicExplosionGreen.prefab"));
            l.Add(new Spec(ManaType.Green, VfxRole.Heal, $"{etf}/Interactive/Healing/HealBig.prefab"));
            l.Add(new Spec(ManaType.Green, VfxRole.Buff, $"{etf}/Combat/Magic/Aura/MagicAuraGreen.prefab"));
            l.Add(new Spec(ManaType.Green, VfxRole.Shield, $"{etf}/Combat/Magic/Shield/MagicShieldGreen.prefab"));
            l.Add(new Spec(ManaType.Green, VfxRole.Area, $"{hov}/Glowing orb 24.prefab"));

            // ---- 白·光（Epic Toon + Hovl 光球作增益浮游）----
            l.Add(new Spec(ManaType.White, VfxRole.Cast, $"{etf}/Combat/Magic/Circle/MagicCircleYellow.prefab"));
            l.Add(new Spec(ManaType.White, VfxRole.Projectile, $"{etf}/Combat/Missiles/Mystic/MysticMissileWhite.prefab"));
            l.Add(new Spec(ManaType.White, VfxRole.Hit, $"{etf}/Combat/Explosions/MysticExplosion/MysticExplosionWhite.prefab"));
            l.Add(new Spec(ManaType.White, VfxRole.Area, $"{etf}/Combat/Explosions/SparkleExplosion/SparkleExplosionYellow.prefab"));
            l.Add(new Spec(ManaType.White, VfxRole.Heal, $"{etf}/Interactive/Healing/HealNova.prefab"));
            l.Add(new Spec(ManaType.White, VfxRole.Shield, $"{etf}/Combat/Magic/Shield/MagicShieldYellow.prefab"));
            l.Add(new Spec(ManaType.White, VfxRole.Buff, $"{hov}/Glowing orb 21.prefab"));

            // ---- 黑·暗（Cast=MysticExplosionDark 起手爆；弹体首条 MysticMissileDark——ShadowMissile
            // 0.3s 采样无粒子（nativeR=0），留作次选由不可见守卫自动跳过并告警）----
            l.Add(new Spec(ManaType.Black, VfxRole.Cast, $"{etf}/Combat/Explosions/MysticExplosion/MysticExplosionDark.prefab"));
            l.Add(new Spec(ManaType.Black, VfxRole.Projectile, $"{etf}/Combat/Missiles/Mystic/MysticMissileDark.prefab"));
            l.Add(new Spec(ManaType.Black, VfxRole.Projectile, $"{etf}/Combat/Missiles/Shadow/ShadowMissile.prefab")); // 次选（预期被守卫跳过）
            l.Add(new Spec(ManaType.Black, VfxRole.Hit, $"{etf}/Combat/Explosions/ShadowExplosion/ShadowExplosion.prefab"));
            l.Add(new Spec(ManaType.Black, VfxRole.Area, $"{etf}/Combat/Explosions/MysticExplosion/MysticExplosionDark.prefab"));
            l.Add(new Spec(ManaType.Black, VfxRole.Buff, $"{hov}/Glowing orb 23.prefab"));

            // ---- 灰·中性（Synty 主体 + Epic 中性魔法爆 + Hovl 光球弹体；石击/通用爆为次选）----
            l.Add(new Spec(ManaType.Gray, VfxRole.Cast, $"{syn}/FX_MagicBlast_02.prefab"));
            l.Add(new Spec(ManaType.Gray, VfxRole.Projectile, $"{hov}/Glowing orb 37.prefab"));
            l.Add(new Spec(ManaType.Gray, VfxRole.Hit, $"{etf}/Combat/Explosions/MagicExplosion/MagicExplosionBlue.prefab"));
            l.Add(new Spec(ManaType.Gray, VfxRole.Hit, $"{syn}/FX_Impact_Stone_01.prefab")); // 次选
            l.Add(new Spec(ManaType.Gray, VfxRole.Area, $"{syn}/FX_ShardRock_Explosion_01.prefab"));
            l.Add(new Spec(ManaType.Gray, VfxRole.Area, $"{syn}/FX_Explosion_01.prefab")); // 次选

            return l;
        }
    }
}
