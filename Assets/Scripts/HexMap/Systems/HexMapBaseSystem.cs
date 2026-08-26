using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace HexMap
{
    /// <summary>
    /// 地图统一底面系统：在整张地图下方生成一个矩形底面（-ElevationStep 高度），
    /// 配合边界 cell 的竖直侧面构成封闭长方体，消除漏光。
    ///
    /// 在第一次有 cell 被加载后执行一次，生成底面实体并注册渲染。
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(HexMeshWriteSystem))]
    public partial class HexMapBaseSystem : SystemBase
    {
        private bool _baseCreated;

        protected override void OnCreate()
        {
            RequireForUpdate<HexMapConfig>();
        }

        protected override void OnUpdate()
        {
            if (_baseCreated)
            {
                Enabled = false; // 底面只创建一次，之后禁用系统
                return;
            }

            var configEntity = SystemAPI.GetSingletonEntity<HexMapConfig>();
            var config = SystemAPI.GetComponentRO<HexMapConfig>(configEntity).ValueRO;
            ref var blob = ref config.Blob.Value;

            CreateBaseMesh(config, ref blob);
            _baseCreated = true;
        }

        private void CreateBaseMesh(HexMapConfig config, ref HexMapConfigBlob blob)
        {
            var metrics = HexMetrics.FromBlob(ref blob);
            float bottomY = HexMeshJob.GetBottomY(ref metrics);
            float4 rect = HexBoundary.GetMapRect(metrics.OuterRadius, metrics.InnerRadius, blob.CellCount);

            // 底面矩形四角
            float3 v0 = new float3(rect.x, bottomY, rect.y); // xMin, zMin
            float3 v1 = new float3(rect.z, bottomY, rect.y); // xMax, zMin
            float3 v2 = new float3(rect.x, bottomY, rect.w); // xMin, zMax
            float3 v3 = new float3(rect.z, bottomY, rect.w); // xMax, zMax

            // winding 使法线朝下（-Y）：Unity 的 RecalculateNormals 用 cross(b-a, c-a)，
            // (v0,v1,v2) 得 (0,-dx*dz,0)，(v1,v3,v2) 同样得 -Y。
            // 之前的 (0,2,1)/(1,2,3) 算出来是 +Y，从下方看是背面，会被剔除。
            var triangles = new[]
            {
                0, 1, 2,
                1, 3, 2
            };

            // 顶点流布局与 cell 网格严格一致（Position + Color + TexCoord1 单条交错流），
            // 否则地形 shader 读到的属性格式对不上。底面用单一地形 0、权重 (1,0,0)。
            var vertices = new[]
            {
                MakeVertex(v0), MakeVertex(v1), MakeVertex(v2), MakeVertex(v3)
            };

            var mesh = new Mesh { name = "HexMapBase" };
            mesh.SetVertexBufferParams(vertices.Length,
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float32, 4),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 3));
            mesh.SetVertexBufferData(vertices, 0, 0, vertices.Length);
            mesh.SetIndexBufferParams(triangles.Length, IndexFormat.UInt32);
            mesh.SetIndexBufferData(triangles, 0, 0, triangles.Length);
            mesh.SetSubMesh(0, new SubMeshDescriptor(0, triangles.Length));

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);

            // 创建实体并注册到 entities.graphics
            var em = EntityManager;
            var baseEntity = em.CreateEntity();

            var egs = World.GetExistingSystemManaged<EntitiesGraphicsSystem>();
            if (egs != null)
            {
                var meshID = egs.RegisterMesh(mesh);
                var material = config.TerrainMaterial.Value;
                if (material == null)
                {
                    Debug.LogWarning("[HexMap] 底面材质为空，跳过渲染注册");
                    return;
                }
                var matID = egs.RegisterMaterial(material);

                var desc = new RenderMeshDescription(ShadowCastingMode.Off, receiveShadows: false);
                RenderMeshUtility.AddComponents(baseEntity, em, desc, new MaterialMeshInfo(matID, meshID));

                em.SetComponentData(baseEntity, new LocalToWorld { Value = float4x4.identity });

                var bounds = mesh.bounds;
                em.SetComponentData(baseEntity, new RenderBounds
                {
                    Value = new Unity.Mathematics.AABB
                    {
                        Center = new float3(bounds.center.x, bounds.center.y, bounds.center.z),
                        Extents = new float3(bounds.extents.x, bounds.extents.y, bounds.extents.z),
                    }
                });
            }
            else
            {
                Debug.LogWarning("[HexMap] EntitiesGraphicsSystem 不存在，无法注册底面网格");
            }
        }

        private static TerrainVertex MakeVertex(float3 position)
        {
            return new TerrainVertex
            {
                Position = position,
                Color = new float4(1f, 0f, 0f, 1f), // splat 权重 (1,0,0)
                UV1 = new float3(0f, 0f, 0f)        // 地形索引 (0,0,0)
            };
        }
    }
}
