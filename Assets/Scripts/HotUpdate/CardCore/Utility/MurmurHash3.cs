using System;

namespace CardCore
{
    /// <summary>
    /// 32位和64位MurmurHash3哈希算法
    /// </summary>
    public static class MurmurHash3
    {
        // Synergy 项目专用种子
        public const uint SYNERGY_TAG_SEED = 0x53796E67;       // "Syng"
        public const ulong SYNERGY_EFFECT_SEED = 0x53796E6765727931UL; // "Syngery1"

        public static uint Hash32(byte[] data, uint seed = 0)
        {
            const uint c1 = 0xcc9e2d51;
            const uint c2 = 0x1b873593;
            const int r1 = 15;
            const int r2 = 13;
            const uint m = 5;
            const uint n = 0xe6546b64;

            int length = data.Length;
            int blocks = length / 4;
            uint hash = seed;

            // 处理4字节块
            for (int i = 0; i < blocks; i++)
            {
                // 从当前字节位置读取4个字节
                uint k = BitConverter.ToUInt32(data, i * 4);

                // 考虑到系统字节序是大端时需要转换4个字节，因为MurmurHash3要求小端字节序
                if (!BitConverter.IsLittleEndian)
                {
                    k = ReverseBytes(k);
                }

                k *= c1;
                k = RotateLeft(k, r1);
                k *= c2;

                hash ^= k;
                hash = RotateLeft(hash, r2);
                hash = hash * m + n;
            }

            // 处理尾部字节
            int tailIndex = blocks * 4;
            uint tail = 0;
            switch (length & 3)
            {
                case 3:
                    tail ^= (uint)data[tailIndex + 2] << 16;
                    goto case 2;
                case 2:
                    tail ^= (uint)data[tailIndex + 1] << 8;
                    goto case 1;
                case 1:
                    tail ^= data[tailIndex];
                    tail *= c1;
                    tail = RotateLeft(tail, r1);
                    tail *= c2;
                    hash ^= tail;
                    break;
            }

            // 最终哈希
            hash ^= (uint)length;
            hash ^= hash >> 16;
            hash *= 0x85ebca6b;
            hash ^= hash >> 13;
            hash *= 0xc2b2ae35;
            hash ^= hash >> 16;

            return hash;
        }

        private static uint RotateLeft(uint x, int r)
        {
            return (x << r) | (x >> (32 - r));
        }

        // 反转字节顺序，适配不同的内存字节序系统
        private static uint ReverseBytes(uint value)
        {
            return (value & 0x000000FFU) << 24 | (value & 0x0000FF00U) << 8 |
                   (value & 0x00FF0000U) >> 8 | (value & 0xFF000000U) >> 24;
        }

        // 字符串扩展方法
        public static uint Hash32(string input, uint seed = 0)
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes(input);
            return Hash32(data, seed);
        }

        // 64位MurmurHash3哈希算法
        public static ulong Hash64(byte[] data, ulong seed = 0)
        {
            const ulong c1 = 0x87c37b91114253d5UL;
            const ulong c2 = 0x4cf5ad432745937fUL;
            const int r1 = 31;
            const int r2 = 27;
            const ulong m = 5;
            const ulong n = 0x52dce729UL;

            int length = data.Length;
            int blocks = length / 8;
            ulong hash = seed;

            // 处理8字节块
            for (int i = 0; i < blocks; i++)
            {
                // 从当前字节位置读取8个字节
                ulong k = BitConverter.ToUInt64(data, i * 8);

                // 考虑到系统字节序是大端时需要转换8个字节，因为MurmurHash3要求小端字节序
                if (!BitConverter.IsLittleEndian)
                {
                    k = ReverseBytes(k);
                }

                k *= c1;
                k = RotateLeft(k, r1);
                k *= c2;
                hash ^= k;

                hash = RotateLeft(hash, r2);
                hash += m;
                hash ^= hash >> 33;
                hash *= n;
                hash ^= hash >> 33;
            }

            // 处理尾部字节
            int tailIndex = blocks * 8;
            ulong tail = 0;
            switch (length & 7)
            {
                case 7:
                    tail ^= (ulong)data[tailIndex + 6] << 48;
                    goto case 6;
                case 6:
                    tail ^= (ulong)data[tailIndex + 5] << 40;
                    goto case 5;
                case 5:
                    tail ^= (ulong)data[tailIndex + 4] << 32;
                    goto case 4;
                case 4:
                    tail ^= (ulong)data[tailIndex + 3] << 24;
                    goto case 3;
                case 3:
                    tail ^= (ulong)data[tailIndex + 2] << 16;
                    goto case 2;
                case 2:
                    tail ^= (ulong)data[tailIndex + 1] << 8;
                    goto case 1;
                case 1:
                    tail ^= data[tailIndex];
                    break;
            }

            // 处理尾部的非完整字节，如果有的话参与到哈希计算
            if (tail != 0)
            {
                tail *= c1;
                tail = RotateLeft(tail, r1);
                tail *= c2;
                hash ^= tail;
            }

            // 最终哈希
            hash ^= (ulong)length;
            hash ^= hash >> 33;
            hash *= 0xff51afd7ed558ccdUL;
            hash ^= hash >> 33;
            hash *= 0xc4ceb9fe1a85ec53UL;
            hash ^= hash >> 33;

            return hash;
        }

        private static ulong RotateLeft(ulong x, int r)
        {
            return (x << r) | (x >> (64 - r));
        }

        // 反转字节顺序，适配不同的内存字节序系统
        private static ulong ReverseBytes(ulong value)
        {
            return (value & 0x00000000000000FFUL) << 56 | (value & 0x000000000000FF00UL) << 40 |
                   (value & 0x0000000000FF0000UL) << 24 | (value & 0x00000000FF000000UL) << 8 |
                   (value & 0x000000FF00000000UL) >> 8 | (value & 0x0000FF0000000000UL) >> 24 |
                   (value & 0x00FF000000000000UL) >> 40 | (value & 0xFF00000000000000UL) >> 56;
        }

        // 字符串扩展方法
        public static ulong Hash64(string input, ulong seed = 0)
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes(input);
            return Hash64(data, seed);
        }

        /// <summary>
        /// 从 "ClassName.PropertyName" 生成稳定的 int 标签，用于 MemoryPackOrder
        /// </summary>
        public static int StableTag(string className, string propertyName)
        {
            return (int)(Hash32($"{className}.{propertyName}", SYNERGY_TAG_SEED) & 0x7FFFFFFF);
        }

        /// <summary>
        /// 从效果描述文本生成 64 位标签，用于效果唯一标识
        /// </summary>
        public static long EffectTag(string description)
        {
            return (long)Hash64(description, SYNERGY_EFFECT_SEED);
        }
    }
}
