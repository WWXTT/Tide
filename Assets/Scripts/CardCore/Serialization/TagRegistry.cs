using System;
using System.Collections.Generic;

namespace CardCore.Serialization
{
    public class EffectTagCollisionException : Exception
    {
        public string Desc1 { get; }
        public string Desc2 { get; }
        public long CollidedTag { get; }

        public EffectTagCollisionException(string desc1, string desc2, long tag)
            : base($"Effect tag collision: '{desc1}' and '{desc2}' both hash to {tag}")
        {
            Desc1 = desc1;
            Desc2 = desc2;
            CollidedTag = tag;
        }
    }

    /// <summary>
    /// 效果标签注册表 —— 运行时管理 64 位效果标签的碰撞检测
    /// 属性标签（int const）由编辑器工具 RefreshMemoryPackOrder 生成到 TagTable.cs
    /// </summary>
    public static class TagRegistry
    {
        private static readonly Dictionary<long, string> _effectTagToDesc = new Dictionary<long, string>();

        /// <summary>
        /// 注册效果标签（64位），碰撞时抛出 EffectTagCollisionException
        /// </summary>
        public static long RegisterEffectTag(string description)
        {
            if (string.IsNullOrEmpty(description))
                return 0;

            long tag = MurmurHash3.EffectTag(description);

            if (_effectTagToDesc.TryGetValue(tag, out string existingDesc) && existingDesc != description)
                throw new EffectTagCollisionException(description, existingDesc, tag);

            _effectTagToDesc[tag] = description;
            return tag;
        }

        public static int EffectTagCount => _effectTagToDesc.Count;
    }
}
