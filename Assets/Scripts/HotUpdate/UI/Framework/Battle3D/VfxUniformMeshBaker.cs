// VfxUniformMeshBaker.cs — 网格均匀采样烘焙器（2026-10-15 自 INab Interactive Dissolve 收编，
// 用户定案移除 INab Studio 插件时抽取的运行时依赖；源码逻辑保持原样，仅改命名空间）。
// 用途：把网格三角形按面积加权均匀采样成 TriangleSampling 数组写入 GraphicsBuffer
// （"UniformMeshBuffer"），供 Dissolve Template.vfx 的粒子出生点消费——出场浮现地格发射组
// 与模型侧影子的粒子出生都依赖本烘焙。
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.VFX;

namespace UnityEngine.VFX
{
    /// <summary>
    /// Sampling information structure
    /// - coord is a barycentric coordinate
    /// - index is an index of triangle
    /// （原 INab 版带 [VFXType] 属性——那需要 VSG 包运行时程序集引用；本收编版只走
    /// 裸字节写入 GraphicsBuffer（Marshal.SizeOf 布局），图内类型注册无关紧要，故省略）
    /// </summary>
    [Serializable]
    public struct TideTriangleSampling
    {
        public Vector2 coord;
        public uint index;
    }
}

namespace SynergyUI
{
    /// <summary>
    /// Cache of mesh data
    /// Contains raw attributes extracted from a readable Mesh
    /// </summary>
    public class TideMeshData
    {
        public struct Vertex
        {
            public Vector3 position;
            public Color color;
            public Vector3 normal;
            public Vector4 tangent;
            public Vector4[] uvs;

            public static Vertex operator +(Vertex a, Vertex b)
            {
                if (a.uvs.Length != b.uvs.Length)
                    throw new InvalidOperationException("Adding compatible vertex");

                var r = new Vertex()
                {
                    position = a.position + b.position,
                    color = a.color + b.color,
                    normal = a.normal + b.normal,
                    tangent = a.tangent + b.tangent,
                    uvs = new Vector4[a.uvs.Length]
                };

                for (int i = 0; i < a.uvs.Length; ++i)
                    r.uvs[i] = a.uvs[i] + b.uvs[i];

                return r;
            }

            public static Vertex operator *(float a, Vertex b)
            {
                var r = new Vertex()
                {
                    position = a * b.position,
                    color = a * b.color,
                    normal = a * b.normal,
                    tangent = a * b.tangent,
                    uvs = new Vector4[b.uvs.Length]
                };

                for (int i = 0; i < b.uvs.Length; ++i)
                    r.uvs[i] = a * b.uvs[i];

                return r;
            }
        }

        public struct Triangle
        {
            public uint a, b, c;
        }

        public Vertex[] vertices;
        public Triangle[] triangles;
        public double[] accumulatedTriangleArea;
    }

    /// <summary>网格→均匀采样池烘焙（面积加权、确定性种子）。原 VFXMeshSamplingHelper 逻辑。</summary>
    public static class TideVfxMeshSamplingHelper
    {
        /// <summary>
        /// Extract and compute the accumulative sum of triangle area needed for uniform sampling
        /// </summary>
        public static TideMeshData ComputeDataCache(Mesh input)
        {
            var positions = input.vertices;
            var normals = input.normals;
            var tangents = input.tangents;
            var colors = input.colors;
            var uvs = new List<Vector4[]>();

            normals = normals.Length == input.vertexCount ? normals : null;
            tangents = tangents.Length == input.vertexCount ? tangents : null;
            colors = colors.Length == input.vertexCount ? colors : null;

            for (int i = 0; i < 8; ++i)
            {
                var uv = new List<Vector4>();
                input.GetUVs(i, uv);
                if (uv.Count == input.vertexCount)
                    uvs.Add(uv.ToArray());
                else
                    break;
            }

            var meshData = new TideMeshData();
            meshData.vertices = new TideMeshData.Vertex[input.vertexCount];
            for (int i = 0; i < input.vertexCount; ++i)
            {
                meshData.vertices[i] = new TideMeshData.Vertex()
                {
                    position = positions[i],
                    color = colors != null ? colors[i] : Color.white,
                    normal = normals != null ? normals[i] : Vector3.up,
                    tangent = tangents != null ? tangents[i] : Vector4.one,
                    uvs = Enumerable.Range(0, uvs.Count).Select(c => uvs[c][i]).ToArray()
                };
            }

            meshData.triangles = new TideMeshData.Triangle[input.triangles.Length / 3];
            var triangles = input.triangles;
            for (uint i = 0; i < meshData.triangles.Length; ++i)
            {
                meshData.triangles[i] = new TideMeshData.Triangle()
                {
                    a = (uint)triangles[i * 3 + 0],
                    b = (uint)triangles[i * 3 + 1],
                    c = (uint)triangles[i * 3 + 2],
                };
            }

            if (meshData.triangles.Length >= 1)
            {
                meshData.accumulatedTriangleArea = new double[meshData.triangles.Length];
                meshData.accumulatedTriangleArea[0] = ComputeTriangleArea(meshData, 0);
                for (uint i = 1; i < meshData.triangles.Length; ++i)
                {
                    meshData.accumulatedTriangleArea[i] = meshData.accumulatedTriangleArea[i - 1] + ComputeTriangleArea(meshData, i);
                }
            }
            else
            {
                meshData.accumulatedTriangleArea = new double[0];
            }

            return meshData;
        }

        /// <summary>
        /// Compute interpolated vertices with triangle index and barycentric coordinates
        /// </summary>
        public static TideMeshData.Vertex GetInterpolatedVertex(TideMeshData meshData, TideTriangleSampling sampling)
        {
            var triangle = meshData.triangles[sampling.index];
            var u = sampling.coord.x;
            var v = sampling.coord.y;
            var w = 1.0f - u - v;

            var A = meshData.vertices[triangle.a];
            var B = meshData.vertices[triangle.b];
            var C = meshData.vertices[triangle.c];

            var r = u * A + v * B + w * C;

            r.normal = r.normal.normalized;
            var tangent = new Vector3(r.tangent.x, r.tangent.y, r.tangent.z).normalized;
            r.tangent = new Vector4(tangent.x, tangent.y, tangent.z, r.tangent.w > 0.0f ? 1.0f : -1.0f);

            return r;
        }

        /// <summary>
        /// Return a new uniform sampled position using the accumulated triangle area
        /// </summary>
        public static TideTriangleSampling GetNextSampling(TideMeshData meshData, System.Random rand)
        {
            var areaPosition = rand.NextDouble() * meshData.accumulatedTriangleArea.Last();
            uint areaIndex = FindIndexOfArea(meshData, areaPosition);

            var randUV = new Vector2((float)rand.NextDouble(), (float)rand.NextDouble());

            // http://inis.jinr.ru/sl/vol1/CMC/Graphics_Gems_1,ed_A.Glassner.pdf
            // p24 uniform distribution from two numbers in triangle generating barycentric coordinate
            float s = randUV.x;
            float t = Mathf.Sqrt(randUV.y);
            float u = 1.0f - t;
            float v = (1 - s) * t;
            float w = s * t; // Not stored, recomputed using 1 - u - v

            return new TideTriangleSampling
            {
                coord = new Vector2(u, v),
                index = areaIndex
            };
        }

        static double ComputeTriangleArea(TideMeshData meshData, uint triangleIndex)
        {
            var t = meshData.triangles[triangleIndex];
            var A = meshData.vertices[t.a].position;
            var B = meshData.vertices[t.b].position;
            var C = meshData.vertices[t.c].position;
            return 0.5f * Vector3.Cross(B - A, C - A).magnitude;
        }

        static uint FindIndexOfArea(TideMeshData meshData, double area)
        {
            uint min = 0;
            uint max = (uint)meshData.accumulatedTriangleArea.Length - 1;
            uint mid = max >> 1;
            while (max >= min)
            {
                if (mid > meshData.accumulatedTriangleArea.Length)
                    throw new InvalidOperationException("Cannot Find FindIndexOfArea");

                if (meshData.accumulatedTriangleArea[mid] >= area &&
                    (mid == 0 || (meshData.accumulatedTriangleArea[mid - 1] < area)))
                {
                    return mid;
                }
                else if (area < meshData.accumulatedTriangleArea[mid])
                {
                    max = mid - 1;
                }
                else
                {
                    min = mid + 1;
                }
                mid = (min + max) >> 1;
            }
            throw new InvalidOperationException("Cannot Find FindIndexOfArea");
        }
    }

    /// <summary>网格烘焙器（原 INab VFXUniformMeshBaker）：采样数=网格三角形数×倍率（上限 10 万），
    /// 写入 "UniformMeshBuffer" GraphicsBuffer 并绑定到 VisualEffect。</summary>
    [Serializable]
    public class TideVfxUniformMeshBaker
    {
        public static string GraphicsBufferName = "UniformMeshBuffer";

        public int SampleCount => Mathf.Min((int)(_sampleCount * SampleCountMultiplier), 100000);

        [Tooltip("Amount of points from which particles would be spawned.")]
        [SerializeField]
        private int _sampleCount = 2048;

        [Range(0.01f, 10f), SerializeField]
        [Tooltip("Multiply sample count by this value to control density of the particles. Keep this as low as possible.")]
        public float SampleCountMultiplier = 1f;

        [SerializeField]
        private TideTriangleSampling[] m_BakedSampling;

        private GraphicsBuffer m_Buffer;

        private void ComputeBakedSampling(VisualEffect visualEffect, Mesh mesh)
        {
            if (visualEffect == null)
            {
                Debug.LogWarning("[TideVfxBaker] expects a VisualEffect on the shared game object.");
                return;
            }

            if (!visualEffect.HasGraphicsBuffer(GraphicsBufferName))
            {
                Debug.LogWarningFormat("[TideVfxBaker] Graphics Buffer property '{0}' is invalid.", GraphicsBufferName);
                return;
            }

            var meshData = TideVfxMeshSamplingHelper.ComputeDataCache(mesh);

            _sampleCount = meshData.triangles.Length;

            var rand = new System.Random(123);
            m_BakedSampling = new TideTriangleSampling[SampleCount];
            for (int i = 0; i < SampleCount; ++i)
            {
                m_BakedSampling[i] = TideVfxMeshSamplingHelper.GetNextSampling(meshData, rand);
            }
        }

        private void UpdateGraphicsBuffer()
        {
            if (m_BakedSampling == null) return;

            if (SampleCount != m_BakedSampling.Length)
                return;

            if (m_Buffer != null)
            {
                m_Buffer.Release();
                m_Buffer = null;
            }

            m_Buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, SampleCount, Marshal.SizeOf(typeof(TideTriangleSampling)));
            m_Buffer.SetData(m_BakedSampling);
        }

        private void BindGraphicsBuffer(VisualEffect vfx)
        {
            if (vfx.HasGraphicsBuffer(GraphicsBufferName)) vfx.SetGraphicsBuffer(GraphicsBufferName, m_Buffer);
        }

        private Mesh RendererToMesh(Renderer meshRenderer)
        {
            Mesh mesh;
            if (meshRenderer is SkinnedMeshRenderer)
                mesh = (meshRenderer as SkinnedMeshRenderer).sharedMesh;
            else
                mesh = (meshRenderer as MeshRenderer).GetComponent<MeshFilter>().sharedMesh;
            return mesh;
        }

        public void Bake(VisualEffect visualEffect, Renderer renderer)
        {
            ComputeBakedSampling(visualEffect, RendererToMesh(renderer));
            UpdateGraphicsBuffer();
            BindGraphicsBuffer(visualEffect);
        }

        public void OnDisable()
        {
            if (m_Buffer != null)
            {
                m_Buffer.Release();
                m_Buffer = null;
            }
        }
    }
}
