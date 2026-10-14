using System;
using System.Collections.Generic;
using System.IO;
using MonsterRig;
using Tide.HotUpdate;
using UnityEngine;

namespace SynergyUI
{
    /// <summary>
    /// 卡牌→怪兽模型解析（2026-10-13 战场重做·占位政策；正式人工绑定表属后续美术决策，另立配置）：
    /// ① 立绘名（Cards.json illustration=模型目录名，卡组合界面画廊点选）精确命中——立绘与模型按名称同源；
    /// ② 卡名与模型目录名精确匹配（忽略大小写）命中；
    /// ③ 未命中按 CardId 稳定哈希（FNV-1a）落目录清单——同卡恒同模型、跨会话确定；
    /// ④ prefab 加载失败/清单空 → null（调用方摆占位胶囊）。
    /// 目录清单 = MonsterRigCatalog 条目按名排序（编辑器下剔除 prefab 目录已不存在的漂移条目）；
    /// 排序 + 稳定哈希保证下标跨会话稳定。解析路径日志留痕（LastPolicy）。
    /// </summary>
    public static class MonsterModelResolver
    {
        private const string ModelRootEditor = "Assets/Art/YGOMonstersRig";

        private static string[] _names; // 排序后目录名（=prefab 名）
        private static readonly Dictionary<string, GameObject> _cache = new Dictionary<string, GameObject>();

        /// <summary>最近一次解析策略（立绘:名 / 同名:名 / 哈希:名 / 占位）——对账日志用。</summary>
        public static string LastPolicy { get; private set; } = "";

        /// <summary>解析模型 prefab；null=调用方回落占位胶囊。illustration=卡立绘名（可空，优先于卡名）。</summary>
        public static GameObject Resolve(string cardId, string cardName, string illustration = null)
        {
            EnsureNames();

            // ① 立绘名精确命中（法术/结界的缺省 Title 不是模型名，自然落空走后续档）
            if (!string.IsNullOrEmpty(illustration))
            {
                var prefab = Load(illustration);
                if (prefab != null) { LastPolicy = "立绘:" + illustration; return prefab; }
            }

            // ② 同名精确命中
            if (!string.IsNullOrEmpty(cardName))
            {
                var prefab = Load(cardName);
                if (prefab != null) { LastPolicy = "同名:" + cardName; return prefab; }
            }

            // ② CardId 稳定哈希落目录
            if (_names.Length > 0 && !string.IsNullOrEmpty(cardId))
            {
                string name = _names[(int)(StableHash(cardId) % (uint)_names.Length)];
                var prefab = Load(name);
                if (prefab != null) { LastPolicy = "哈希:" + name; return prefab; }
            }

            LastPolicy = "占位";
            return null;
        }

        private static GameObject Load(string name)
        {
            GameObject cached;
            if (_cache.TryGetValue(name, out cached)) return cached;
            cached = HotUpdateAssets.Load<GameObject>(name, ModelRootEditor + "/" + name + "/" + name + ".prefab");
            _cache[name] = cached; // 负结果也缓存（缺失目录不重查）
            return cached;
        }

        private static void EnsureNames()
        {
            if (_names != null) return;
            var list = new List<string>();
            foreach (var kv in MonsterRigCatalog.All)
            {
#if UNITY_EDITOR
                // 目录漂移剔除（catalog 438 条 vs 实际 prefab 目录数会随清整漂移）：
                // 2026-10-13 收紧=目录+prefab 文件双校验——防"清单有名无 prefab"（哈希档选中必落占位胶囊）
                var dir = Path.Combine(Application.dataPath, "Art/YGOMonstersRig", kv.Key);
                if (!Directory.Exists(dir) || !File.Exists(Path.Combine(dir, kv.Key + ".prefab")))
                    continue;
#endif
                list.Add(kv.Key);
            }
            list.Sort(StringComparer.Ordinal);
            _names = list.ToArray();
            Debug.Log("[BattleField] 怪兽模型清单：" + _names.Length + " 个可用目录（catalog "
                + MonsterRigCatalog.All.Count + " 条，" + (MonsterRigCatalog.All.Count - _names.Length) + " 条漂移剔除）");
        }

        /// <summary>FNV-1a 32 位（跨会话/平台确定）。</summary>
        private static uint StableHash(string s)
        {
            uint h = 2166136261u;
            for (int i = 0; i < s.Length; i++) { h ^= (uint)s[i]; h *= 16777619u; }
            return h;
        }
    }
}
