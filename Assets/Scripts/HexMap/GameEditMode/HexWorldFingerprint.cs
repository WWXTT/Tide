using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HexMap
{
    /// <summary>
    /// 生成配置指纹（8 位 hex，FNV-1a 32）。
    /// 纳入集 = 决定「噪声 → 高程/地块」重建的全部输入——加载时比对，
    /// 不符说明生成基线漂移（高程仍以存档为准，仅 Detail 扰动类数据可能有视觉级偏差）。
    ///
    /// 刻意排除：featureSeed 与河/路/植被参数（加载不重生成，结果在档）；
    /// meshSettings（只影响视觉变异，不影响数据正确性——纳入会产生误报）。
    /// </summary>
    public static class HexWorldFingerprint
    {
        /// <summary>指纹结果缓存（按 settings 实例——纹理像素哈希另有独立缓存）</summary>
        private static readonly Dictionary<HexMapFeatureSettings, string> Cache = new();

        /// <summary>纹理像素哈希缓存（GetPixels32 一次 ~4MB，缓存后免费；域重载关闭时纹理实例稳定）</summary>
        private static readonly Dictionary<Texture2D, uint> TexCache = new();

        public static string Compute(HexMapFeatureSettings s)
        {
            if (s == null)
                return "FFFFFFFF";
            if (Cache.TryGetValue(s, out var cached))
                return cached;

            uint h = 2166136261u;
            h = Fnv(h, s.cellCountX);
            h = Fnv(h, s.cellCountZ);
            h = Fnv(h, s.maxElevation);
            h = Fnv(h, s.noiseSeed);
            h = Fnv(h, Float(s.noiseSampleRange));
            h = Fnv(h, Float(s.noiseScales.x));
            h = Fnv(h, Float(s.noiseScales.y));
            h = Fnv(h, Float(s.noiseScales.z));
            h = Fnv(h, Float(s.noiseScales.w));
            h = Fnv(h, Float(s.mountainStrength));
            h = Fnv(h, Float(s.curlWarpStrength));
            h = Fnv(h, Float(s.cellPerturbRange.x));
            h = Fnv(h, Float(s.cellPerturbRange.y));
            h = Fnv(h, Float(s.elevationPerturbRange.x));
            h = Fnv(h, Float(s.elevationPerturbRange.y));

            // 分带表（顺序敏感）
            h = Fnv(h, s.terrainBands?.Count ?? 0);
            if (s.terrainBands != null)
            {
                foreach (var b in s.terrainBands)
                {
                    h = Fnv(h, b.maxElevation);
                    h = Fnv(h, b.terrainIndex);
                }
            }

            // 四张噪声图：名字 + 尺寸 + 像素内容
            h = FnvTexture(h, s.heightNoise);
            h = FnvTexture(h, s.mountainNoise);
            h = FnvTexture(h, s.detailNoise);
            h = FnvTexture(h, s.curlNoise);

            string result = h.ToString("X8", CultureInfo.InvariantCulture);
            Cache[s] = result;
            return result;
        }

        /// <summary>清空缓存（settings 字段在运行中被改过后调用，正常流程不需要）</summary>
        public static void InvalidateCache() => Cache.Clear();

        private static uint Fnv(uint h, int v) => (h ^ (uint)v) * 16777619u;

        private static uint Fnv(uint h, string s)
        {
            if (s == null)
                return (h ^ 0u) * 16777619u;
            foreach (char c in s)
                h = (h ^ c) * 16777619u;
            return h;
        }

        private static string Float(float v) => v.ToString("R", CultureInfo.InvariantCulture);

        /// <summary>纹理指纹：名字 + 尺寸 + 像素内容。缺图 = 记名字空串（与生成端缺图语义一致）</summary>
        private static uint FnvTexture(uint h, Texture2D tex)
        {
            if (tex == null)
                return Fnv(h, "");

            h = Fnv(h, tex.name);
            h = Fnv(h, tex.width);
            h = Fnv(h, tex.height);

            if (!TexCache.TryGetValue(tex, out uint pixelsHash))
            {
                uint ph = 2166136261u;
                var px = tex.GetPixels32();
                for (int i = 0; i < px.Length; i++)
                {
                    var c = px[i];
                    ph = (ph ^ c.r) * 16777619u;
                    ph = (ph ^ c.g) * 16777619u;
                    ph = (ph ^ c.b) * 16777619u;
                    ph = (ph ^ c.a) * 16777619u;
                }
                TexCache[tex] = pixelsHash = ph;
            }
            return (h ^ pixelsHash) * 16777619u;
        }
    }
}
