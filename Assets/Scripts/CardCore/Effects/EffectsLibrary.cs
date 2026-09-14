using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CardCore
{
    /// <summary>
    /// 效果库（2026-09-14 效果引用化 v2·大修）：Tide/Effects.json 单文件——**瘦格式**
    /// （EffectSlimDto：原子=表行 ID 引用+增量，见 Effects/EffectSlim.cs 头注）。
    /// 卡表（Cards.json）只存 effectIds 引用。效果 id = ContentHasher.HashEffect（单源化口径：
    /// AE 段仅 steps 空时计入）。旧内嵌格式（TestDecks）向后兼容。
    /// </summary>
    public static class EffectsLibrary
    {
        private const string ConfigRelative = "Tide/Effects.json";

        [Serializable]
        private class Wrapper { public List<EffectSlimDto> items; }

        private static Dictionary<string, EffectSlimDto> _byId;
        private static List<EffectSlimDto> _items;

        private static void EnsureLoaded()
        {
            if (_byId != null) return;
            _byId = new Dictionary<string, EffectSlimDto>();
            _items = new List<EffectSlimDto>();
            try
            {
                string path = Path.Combine(Application.streamingAssetsPath, ConfigRelative);
                if (!File.Exists(path))
                {
                    Debug.LogWarning($"[EffectsLibrary] 效果库不存在: {path}（卡表 effectIds 引用将解析为空）");
                    return;
                }
                var wrapper = JsonUtility.FromJson<Wrapper>(File.ReadAllText(path));
                if (wrapper?.items == null) return;
                foreach (var it in wrapper.items)
                {
                    if (it == null || string.IsNullOrEmpty(it.id)) continue;
                    if (_byId.ContainsKey(it.id)) continue; // 重复 id 保首条
                    _byId[it.id] = it;
                    _items.Add(it);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[EffectsLibrary] 加载 {ConfigRelative} 失败: {e.Message}");
            }
        }

        /// <summary>强制重读（合成器「读取效果表」按钮用）。</summary>
        public static void Reload()
        {
            _byId = null;
            _items = null;
            EnsureLoaded();
        }

        /// <summary>全部效果条目（按文件序）。</summary>
        public static List<EffectSlimDto> GetAll()
        {
            EnsureLoaded();
            return _items;
        }

        /// <summary>按 id 取条目（无则 null）。</summary>
        public static EffectSlimDto Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            EnsureLoaded();
            return _byId.TryGetValue(id, out var it) ? it : null;
        }

        /// <summary>按 id 解析出卡内效果（瘦 DTO → CardEffectData；原子引用逐个还原，缺行告警剔除）。</summary>
        public static CardEffectData Resolve(string id)
        {
            var it = Get(id);
            if (it == null)
            {
                Debug.LogWarning($"[EffectsLibrary] 效果引用缺失: {id}（Effects.json 无此条）");
                return null;
            }
            return EffectSlim.ToCardEffect(it);
        }
    }
}
