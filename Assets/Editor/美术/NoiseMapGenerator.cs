/* Ported from Unreal Engine NoiseMapGenerator plugin
   Original: Copyright UNmisterIZE(Sebastien Durocher) 2025
   Unity port: 2026 */

using UnityEngine;
using UnityEditor;
using System;

namespace NoiseMapGenerator
{
    public enum NoiseMode { Perlin, FBM }

    public enum NoiseAlgorithm { Perlin, Worley, RidgedFBM, Curl, Gabor }

    public enum NoiseOutputMode { Grayscale, SplitFirst3Octaves }

    public struct NoiseBakeSettings
    {
        public int Width, Height, TileRepeats, Seed, Octaves;
        public float Frequency, Lacunarity, Gain;
        public bool Normalize;
        public NoiseMode Mode;
        public NoiseAlgorithm Algorithm;
        public NoiseOutputMode OutputMode;
        public string OutputPath, BaseName;
        public bool Overwrite;

        public static NoiseBakeSettings Default => new NoiseBakeSettings
        {
            Width = 1024, Height = 1024, TileRepeats = 1, Seed = 1337, Octaves = 5,
            Frequency = 0.25f, Lacunarity = 2f, Gain = 0.5f, Normalize = true,
            Mode = NoiseMode.FBM, Algorithm = NoiseAlgorithm.Perlin,
            OutputMode = NoiseOutputMode.Grayscale,
            OutputPath = "Assets/Noise", BaseName = "T_Noise", Overwrite = false
        };
    }

    public static class TileableNoise
    {
        static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);
        static float Lerp(float a, float b, float t) => a + t * (b - a);

        public struct Perm
        {
            public int[] P;
            public Perm(int seed)
            {
                P = new int[512];
                int[] b = new int[256];
                for (int i = 0; i < 256; i++) b[i] = i;
                System.Random rng = new System.Random(seed);
                for (int i = 255; i > 0; i--)
                {
                    int j = rng.Next(0, i + 1);
                    (b[i], b[j]) = (b[j], b[i]);
                }
                for (int i = 0; i < 512; i++) P[i] = b[i & 255];
            }
            public int H4(int x, int y, int z, int w) => P[P[P[P[x & 255] + (y & 255)] + (z & 255)] + (w & 255)];
        }

        static float Grad4(int h, float x, float y, float z, float w)
        {
            switch (h & 31)
            {
                case  0: return  x + y + z + w; case  1: return  x + y + z - w;
                case  2: return  x + y - z + w; case  3: return  x + y - z - w;
                case  4: return  x - y + z + w; case  5: return  x - y + z - w;
                case  6: return  x - y - z + w; case  7: return  x - y - z - w;
                case  8: return -x + y + z + w; case  9: return -x + y + z - w;
                case 10: return -x + y - z + w; case 11: return -x + y - z - w;
                case 12: return -x - y + z + w; case 13: return -x - y + z - w;
                case 14: return -x - y - z + w; case 15: return -x - y - z - w;
                case 16: return  x + y + z;     case 17: return  x + y - z;
                case 18: return  x - y + z;     case 19: return  x - y - z;
                case 20: return -x + y + z;     case 21: return -x + y - z;
                case 22: return -x - y + z;     case 23: return -x - y - z;
                case 24: return  x + z + w;     case 25: return  x + z - w;
                case 26: return  x - z + w;     case 27: return  x - z - w;
                case 28: return -x + z + w;     case 29: return -x + z - w;
                case 30: return -x - z + w;     default: return -x - z - w;
            }
        }

        public static float Perlin4D(float x, float y, float z, float w, Perm perm)
        {
            int X0 = Mathf.FloorToInt(x), Y0 = Mathf.FloorToInt(y), Z0 = Mathf.FloorToInt(z), W0 = Mathf.FloorToInt(w);
            float xf = x - X0, yf = y - Y0, zf = z - Z0, wf = w - W0;
            float u = Fade(xf), v = Fade(yf), s = Fade(zf), t = Fade(wf);
            int X1 = X0 + 1, Y1 = Y0 + 1, Z1 = Z0 + 1, W1 = W0 + 1;

            float n0000 = Grad4(perm.H4(X0, Y0, Z0, W0), xf, yf, zf, wf);
            float n1000 = Grad4(perm.H4(X1, Y0, Z0, W0), xf - 1, yf, zf, wf);
            float n0100 = Grad4(perm.H4(X0, Y1, Z0, W0), xf, yf - 1, zf, wf);
            float n1100 = Grad4(perm.H4(X1, Y1, Z0, W0), xf - 1, yf - 1, zf, wf);
            float n0010 = Grad4(perm.H4(X0, Y0, Z1, W0), xf, yf, zf - 1, wf);
            float n1010 = Grad4(perm.H4(X1, Y0, Z1, W0), xf - 1, yf, zf - 1, wf);
            float n0110 = Grad4(perm.H4(X0, Y1, Z1, W0), xf, yf - 1, zf - 1, wf);
            float n1110 = Grad4(perm.H4(X1, Y1, Z1, W0), xf - 1, yf - 1, zf - 1, wf);

            float n0001 = Grad4(perm.H4(X0, Y0, Z0, W1), xf, yf, zf, wf - 1);
            float n1001 = Grad4(perm.H4(X1, Y0, Z0, W1), xf - 1, yf, zf, wf - 1);
            float n0101 = Grad4(perm.H4(X0, Y1, Z0, W1), xf, yf - 1, zf, wf - 1);
            float n1101 = Grad4(perm.H4(X1, Y1, Z0, W1), xf - 1, yf - 1, zf, wf - 1);
            float n0011 = Grad4(perm.H4(X0, Y0, Z1, W1), xf, yf, zf - 1, wf - 1);
            float n1011 = Grad4(perm.H4(X1, Y0, Z1, W1), xf - 1, yf, zf - 1, wf - 1);
            float n0111 = Grad4(perm.H4(X0, Y1, Z1, W1), xf, yf - 1, zf - 1, wf - 1);
            float n1111 = Grad4(perm.H4(X1, Y1, Z1, W1), xf - 1, yf - 1, zf - 1, wf - 1);

            float nx00 = Lerp(n0000, n1000, u), nx10 = Lerp(n0100, n1100, u);
            float nx01 = Lerp(n0010, n1010, u), nx11 = Lerp(n0110, n1110, u);
            float nxy0 = Lerp(nx00, nx10, v), nxy1 = Lerp(nx01, nx11, v);
            float nxyz0 = Lerp(nxy0, nxy1, s);

            float nx00b = Lerp(n0001, n1001, u), nx10b = Lerp(n0101, n1101, u);
            float nx01b = Lerp(n0011, n1011, u), nx11b = Lerp(n0111, n1111, u);
            float nxy0b = Lerp(nx00b, nx10b, v), nxy1b = Lerp(nx01b, nx11b, v);
            float nxyz1 = Lerp(nxy0b, nxy1b, s);

            return Lerp(nxyz0, nxyz1, t);
        }

        public static float FBM4D(float x, float y, float z, float w, Perm perm, int octaves, float lac, float gain)
        {
            float amp = 1f, sum = 0f, range = 0f;
            float X = x, Y = y, Z = z, W = w;
            for (int o = 0; o < octaves; o++)
            {
                sum += amp * Perlin4D(X, Y, Z, W, perm);
                range += amp;
                X *= lac; Y *= lac; Z *= lac; W *= lac;
                amp *= gain;
            }
            return range > 0f ? sum / range : 0f;
        }

        static uint Hash2D(int x, int y, uint seed)
        {
            uint h = (uint)x * 0x27d4eb2d ^ (uint)y * 0x165667b1u ^ seed * 0x9e3779b9u;
            h ^= (h >> 16); h *= 0x7feb352d; h ^= (h >> 15); h *= 0x846ca68b; h ^= (h >> 16);
            return h;
        }

        static float Frand01(ref uint state)
        {
            state ^= state << 13; state ^= state >> 17; state ^= state << 5;
            return (state & 0x00FFFFFF) / 16777216f;
        }

        public static float WorleyF1(float u, float v, int cellsU, int cellsV, uint seed)
        {
            float fu = u * cellsU, fv = v * cellsV;
            int iu0 = Mathf.FloorToInt(fu), iv0 = Mathf.FloorToInt(fv);
            float best = 1e9f;

            for (int du = -1; du <= 1; du++)
            for (int dv = -1; dv <= 1; dv++)
            {
                int iu = (iu0 + du + cellsU) % cellsU;
                int iv = (iv0 + dv + cellsV) % cellsV;
                uint st = Hash2D(iu, iv, seed);
                float jx = Frand01(ref st), jy = Frand01(ref st);
                float fx = (iu + jx) / cellsU, fy = (iv + jy) / cellsV;
                float dx = Mathf.Abs(u - fx); dx = Mathf.Min(dx, 1f - dx);
                float dy = Mathf.Abs(v - fy); dy = Mathf.Min(dy, 1f - dy);
                best = Mathf.Min(best, Mathf.Sqrt(dx * dx + dy * dy));
            }
            return best;
        }

        // ── Curl noise ──────────────────────────────────────────
        // 2D curl of a vector field (N1, N2):  curl = ∂N2/∂x − ∂N1/∂y
        // Each component N1,N2 is an independent Perlin4D sample (offset seed).
        // Partial derivatives are computed via central finite differences.
        // Inherits tilability from the torus-mapped 4D Perlin.

        static float PerlinSample(float x, float y, float z, float w, Perm perm)
            => Perlin4D(x, y, z, w, perm);

        public static float CurlScalar(float u, float v, float scale, Perm perm)
        {
            float twoPi = Mathf.PI * 2f;
            float h = 0.0005f; // finite-difference step in UV space

            // N1 uses seed-offset perm (the base perm already encodes the seed)
            // We create a sibling perm with offset by re-using the same perm but
            // shifting the 4D coords by a constant to act as two independent fields.
            (float X, float Y, float Z, float W) Angles(float uu, float vv) {
                float tt = twoPi * uu, pp = twoPi * vv;
                return (Mathf.Cos(tt) * scale, Mathf.Sin(tt) * scale,
                        Mathf.Cos(pp) * scale, Mathf.Sin(pp) * scale);
            }

            // N2 field offset by rotating the 4D torus coordinates slightly
            const float k0 = 1.2345f, k1 = 6.7890f, k2 = 3.1415f, k3 = 2.7182f;
            (float X, float Y, float Z, float W) Angles2(float uu, float vv) {
                float tt = twoPi * uu + k0, pp = twoPi * vv + k1;
                return (Mathf.Cos(tt) * scale + k2,
                        Mathf.Sin(tt) * scale + k3,
                        Mathf.Cos(pp) * scale + k0,
                        Mathf.Sin(pp) * scale + k1);
            }

            // ∂N2/∂u  (central diff in u)
            var c2p = Angles2(u + h, v);  var c2m = Angles2(u - h, v);
            float dN2du = (PerlinSample(c2p.X, c2p.Y, c2p.Z, c2p.W, perm) -
                           PerlinSample(c2m.X, c2m.Y, c2m.Z, c2m.W, perm)) / (2f * h);

            // ∂N1/∂v  (central diff in v)
            var c1p = Angles(u, v + h);   var c1m = Angles(u, v - h);
            float dN1dv = (PerlinSample(c1p.X, c1p.Y, c1p.Z, c1p.W, perm) -
                           PerlinSample(c1m.X, c1m.Y, c1m.Z, c1m.W, perm)) / (2f * h);

            return dN2du - dN1dv;
        }

        public static float CurlFBM(float u, float v, float scale, Perm perm,
            int octaves, float lac, float gain)
        {
            float amp = 1f, sum = 0f, range = 0f;
            float U = u, V = v, S = scale;
            for (int o = 0; o < octaves; o++)
            {
                sum += amp * CurlScalar(U, V, S, perm);
                range += amp;
                U *= lac; V *= lac; S *= lac;
                amp *= gain;
            }
            return range > 0f ? sum / range : 0f;
        }

        // ── Gabor noise ─────────────────────────────────────────
        // Grid of anisotropic Gabor wavelets: exp(-πσ²|r|²)·cos(2πF₀⟨dir,r⟩+φ)
        // Each grid cell has a randomly-oriented kernel; pixels blend from neighbours.
        // Uses torus wrapping for seamless tiling.

        struct GaborKernel
        {
            public float cx, cy;   // kernel centre in [0,1)
            public float dx, dy;   // unit direction vector
            public float freq;     // spatial frequency
            public float phase;    // phase offset  [0,2π)
            public float sigma2;   // Gaussian variance (controls envelope width)
        }

        static GaborKernel MakeKernel(float uu, float vv, float cellSize, float density, uint seed)
        {
            uint h = Hash2D((int)(uu * density * 1000f), (int)(vv * density * 1000f), seed);
            float jx = (Frand01(ref h) - 0.5f) * cellSize;
            float jy = (Frand01(ref h) - 0.5f) * cellSize;
            float ang = Frand01(ref h) * Mathf.PI * 2f;
            float fr  = Mathf.Lerp(2f, 12f, Frand01(ref h));
            float ph  = Frand01(ref h) * Mathf.PI * 2f;
            float sig = Mathf.Lerp(0.004f, 0.025f, Frand01(ref h));
            return new GaborKernel
            {
                cx = uu + jx, cy = vv + jy,
                dx = Mathf.Cos(ang), dy = Mathf.Sin(ang),
                freq = fr, phase = ph,
                sigma2 = sig * sig
            };
        }

        static float TorusDist(float a, float b)
        {
            float d = Mathf.Abs(a - b);
            return Mathf.Min(d, 1f - d);
        }

        public static float GaborNoise(float u, float v, uint seed, float freqScale)
        {
            // Cell count scales with frequency
            int res = Mathf.Clamp(Mathf.RoundToInt(freqScale * 12f), 3, 64);
            float invRes = 1f / res;
            float cellU = u * res, cellV = v * res;
            int cu0 = Mathf.FloorToInt(cellU), cv0 = Mathf.FloorToInt(cellV);

            float sum = 0f;

            for (int du = -1; du <= 1; du++)
            for (int dv = -1; dv <= 1; dv++)
            {
                int cu = (cu0 + du + res) % res;
                int cv = (cv0 + dv + res) % res;
                float gu = (cu + 0.5f) * invRes;
                float gv = (cv + 0.5f) * invRes;

                var k = MakeKernel(gu, gv, invRes, freqScale, seed);

                float rx = TorusDist(u, k.cx), ry = TorusDist(v, k.cy);
                float r2 = rx * rx + ry * ry;

                // Gaussian envelope
                float env = Mathf.Exp(-Mathf.PI * r2 / k.sigma2);

                // Cosine carrier: directional projection
                float proj = rx * k.dx + ry * k.dy;
                float wave = Mathf.Cos(2f * Mathf.PI * k.freq * proj + k.phase);

                sum += env * wave;
            }

            return Mathf.Clamp(sum * 0.25f, -1f, 1f);
        }

        public static float GaborFBM(float u, float v, uint seed, int octaves,
            float lac, float gain)
        {
            float amp = 1f, sum = 0f, range = 0f;
            float U = u, V = v, freq = 1f;
            for (int o = 0; o < octaves; o++)
            {
                sum += amp * GaborNoise(U, V, seed + (uint)(o * 7919), freq);
                range += amp;
                U *= lac; V *= lac;
                freq *= lac;
                amp *= gain;
            }
            return range > 0f ? sum / range : 0f;
        }
    }

    public static class NoiseMapCore
    {
        public static bool GeneratePixels(NoiseBakeSettings s, int repeatsUV, out Color32[] outPixels, out int outW, out int outH)
        {
            outW = s.Width; outH = s.Height;
            outPixels = null;
            if (outW <= 0 || outH <= 0) return false;

            outPixels = new Color32[outW * outH];
            float invW = 1f / outW, invH = 1f / outH;
            float twoPi = Mathf.PI * 2f;
            float scale = 1f / Mathf.Max(s.Frequency, 1e-6f);
            var perm = new TileableNoise.Perm(s.Seed);

            (float X, float Y, float Z, float W) AnglesFromUV(float u, float v)
            {
                float th = twoPi * u, ph = twoPi * v;
                return (Mathf.Cos(th) * scale, Mathf.Sin(th) * scale, Mathf.Cos(ph) * scale, Mathf.Sin(ph) * scale);
            }

            float GetNormalized(float u, float v)
            {
                var c = AnglesFromUV(u, v);
                float r = TileableNoise.Perlin4D(c.X, c.Y, c.Z, c.W, perm);
                return 0.5f * (r + 1f);
            }

            float SampleBase(float u, float v)
            {
                switch (s.Algorithm)
                {
                    case NoiseAlgorithm.Perlin:
                    {
                        var c = AnglesFromUV(u, v);
                        return s.Mode == NoiseMode.FBM
                            ? TileableNoise.FBM4D(c.X, c.Y, c.Z, c.W, perm, s.Octaves, s.Lacunarity, s.Gain)
                            : TileableNoise.Perlin4D(c.X, c.Y, c.Z, c.W, perm);
                    }
                    case NoiseAlgorithm.RidgedFBM:
                    {
                        float amp = 0.5f, sum = 0f, range = 0f, freqMul = 1f;
                        for (int o = 0; o < s.Octaves; o++)
                        {
                            float ou = (u * freqMul) - Mathf.Floor(u * freqMul);
                            float ov = (v * freqMul) - Mathf.Floor(v * freqMul);
                            var c = AnglesFromUV(ou, ov);
                            float p = TileableNoise.Perlin4D(c.X, c.Y, c.Z, c.W, perm);
                            float r = 1f - Mathf.Abs(p);
                            sum += amp * r; range += amp;
                            freqMul *= s.Lacunarity; amp *= s.Gain;
                        }
                        return range > 0f ? (sum / range) * 2f - 1f : 0f;
                    }
                    case NoiseAlgorithm.Worley:
                    {
                        float cellsF = Mathf.Clamp((1f / Mathf.Max(s.Frequency, 1e-6f)) * 0.75f, 1f, 512f);
                        int cellsU = Mathf.Max(1, Mathf.RoundToInt(cellsF) * repeatsUV);
                        int cellsV = Mathf.Max(1, Mathf.RoundToInt(cellsF) * repeatsUV);
                        float d = TileableNoise.WorleyF1(u, v, cellsU, cellsV, (uint)s.Seed);
                        float v01 = 1f - Mathf.Clamp(d * 2f, 0f, 1f);
                        return v01 * 2f - 1f;
                    }
                    case NoiseAlgorithm.Curl:
                    {
                        float curlScale = 1f / Mathf.Max(s.Frequency, 1e-6f);
                        return s.Mode == NoiseMode.FBM
                            ? TileableNoise.CurlFBM(u, v, curlScale, perm, s.Octaves, s.Lacunarity, s.Gain)
                            : TileableNoise.CurlScalar(u, v, curlScale, perm);
                    }
                    case NoiseAlgorithm.Gabor:
                    {
                        return s.Mode == NoiseMode.FBM
                            ? TileableNoise.GaborFBM(u, v, (uint)s.Seed, s.Octaves, s.Lacunarity, s.Gain)
                            : TileableNoise.GaborNoise(u, v, (uint)s.Seed, s.Frequency);
                    }
                }
                return 0f;
            }

            if (s.OutputMode == NoiseOutputMode.Grayscale)
            {
                float minV = 1e9f, maxV = -1e9f;
                float[] tmp = new float[outW * outH];

                for (int y = 0, i = 0; y < outH; y++)
                for (int x = 0; x < outW; x++, i++)
                {
                    float u = repeatsUV * x * invW, v = repeatsUV * y * invH;
                    float raw = SampleBase(u, v);
                    tmp[i] = raw;
                    if (raw < minV) minV = raw;
                    if (raw > maxV) maxV = raw;
                }

                float invRange = (s.Normalize && maxV > minV) ? 1f / (maxV - minV) : 0.5f;

                for (int y = 0, i = 0; y < outH; y++)
                for (int x = 0; x < outW; x++, i++)
                {
                    float v = s.Normalize ? (tmp[i] - minV) * invRange : 0.5f * (tmp[i] + 1f);
                    byte g = (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);
                    outPixels[y * outW + x] = new Color32(g, g, g, 255);
                }
            }
            else // SplitFirst3Octaves → RGB
            {
                for (int y = 0; y < outH; y++)
                for (int x = 0; x < outW; x++)
                {
                    float u = repeatsUV * x * invW, v = repeatsUV * y * invH;

                    float SampleOctave(int octIdx)
                    {
                        float fu = u * Mathf.Pow(s.Lacunarity, octIdx);
                        float fv = v * Mathf.Pow(s.Lacunarity, octIdx);
                        return s.Algorithm switch
                        {
                            NoiseAlgorithm.Curl =>
                                (TileableNoise.CurlScalar(fu, fv, 1f / Mathf.Max(s.Frequency, 1e-6f), perm) + 1f) * 0.5f,
                            NoiseAlgorithm.Gabor =>
                                (TileableNoise.GaborNoise(fu, fv, (uint)s.Seed + (uint)(octIdx * 7919), s.Frequency) + 1f) * 0.5f,
                            _ => GetNormalized(fu, fv) // Perlin / Worley / RidgedFBM
                        };
                    }

                    float r = SampleOctave(0);
                    float g = SampleOctave(1);
                    float b = SampleOctave(2);
                    outPixels[y * outW + x] = new Color32(
                        (byte)Mathf.Clamp(Mathf.RoundToInt(r * 255f), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(g * 255f), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(b * 255f), 0, 255),
                        255);
                }
            }
            return true;
        }

        public static Texture2D CreatePreview(NoiseBakeSettings s, int repeatsUV)
        {
            if (!GeneratePixels(s, repeatsUV, out var pixels, out int w, out int h)) return null;

            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat };
            tex.SetPixels32(pixels);
            tex.Apply(false);
            return tex;
        }

        public static void BakeToAsset(NoiseBakeSettings s)
        {
            if (!GeneratePixels(s, 1, out var pixels, out int w, out int h)) return;

            string basePath = s.OutputPath.TrimEnd('/');
            if (!basePath.StartsWith("Assets/")) basePath = "Assets/" + basePath.TrimStart('/');
            if (!System.IO.Directory.Exists(basePath))
                System.IO.Directory.CreateDirectory(basePath);

            string assetName = s.BaseName;
            string fullPath = $"{basePath}/{assetName}.asset";

            if (!s.Overwrite)
            {
                int suffix = 1;
                while (System.IO.File.Exists(fullPath))
                    fullPath = $"{basePath}/{assetName}_{suffix++:D3}.asset";
            }

            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Repeat,
                alphaIsTransparency = false
            };
            tex.SetPixels32(pixels);
            tex.Apply(false);

            AssetDatabase.CreateAsset(tex, fullPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Noise Map Generator", $"Saved to: {fullPath}", "OK");
            EditorGUIUtility.PingObject(tex);
        }
    }
}
