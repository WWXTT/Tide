using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace HexMap
{
    /// <summary>
    /// 网格生成与渲染交接系统（主线程，Phase D 最后一步）。
    ///
    /// 流程：
    /// 1. 查询标记 CellDirty 的 cell
    /// 2. 为每个脏 cell 调度 HexMeshJob（Burst 并行生成顶点数据）
    /// 3. 主线程创建/更新 UnityEngine.Mesh
    /// 4. 注册到 entities.graphics（RegisterMesh/RegisterMaterial + MaterialMeshInfo）
    /// 5. 清除 CellDirty
    ///
    /// 帧预算：每帧最多重建 N 个 cell（config.MaxMeshBuildsPerFrame）。
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(HexTerrainGenerationSystem))]
    public partial class HexMeshWriteSystem : SystemBase
    {
        private EntityQuery _dirtyCellsQuery;

        protected override void OnCreate()
        {
            _dirtyCellsQuery = GetEntityQuery(
                ComponentType.ReadOnly<HexCellData>(),
                ComponentType.ReadOnly<Neighbors>(),
                ComponentType.ReadOnly<CellDirty>());

            RequireForUpdate<HexMapConfig>();
        }

        protected override void OnUpdate()
        {
            var configEntity = SystemAPI.GetSingletonEntity<HexMapConfig>();
            var config = SystemAPI.GetComponentRO<HexMapConfig>(configEntity).ValueRO;

            if (_dirtyCellsQuery.IsEmpty)
                return;

            var dirtyCells = _dirtyCellsQuery.ToEntityArray(Allocator.Temp);
            int processedThisFrame = 0;

            foreach (var cellEntity in dirtyCells)
            {
                if (processedThisFrame >= config.MaxMeshBuildsPerFrame)
                    break;

                RebuildCellMesh(cellEntity, config);
                EntityManager.SetComponentEnabled<CellDirty>(cellEntity, false);
                processedThisFrame++;
            }

            dirtyCells.Dispose();
        }

        /// <summary>
        /// 为单个 cell 重建网格：Job 生成 → Mesh 上传 → entities.graphics 注册
        /// </summary>
        private void RebuildCellMesh(Entity cellEntity, HexMapConfig config)
        {
            var cellData = EntityManager.GetComponentData<HexCellData>(cellEntity);

            // 准备 Job 输出缓冲
            var positions = new NativeList<float3>(128, Allocator.TempJob);
            var triangles = new NativeList<int>(256, Allocator.TempJob);
            var colors = new NativeList<float4>(128, Allocator.TempJob);
            var cellIndices = new NativeList<float3>(128, Allocator.TempJob);

            var job = new HexMeshJob
            {
                Blob = config.Blob,
                CellEntity = cellEntity,
                CellData = cellData,
                AllCellData = GetComponentLookup<HexCellData>(isReadOnly: true),
                AllNeighbors = GetBufferLookup<Neighbors>(isReadOnly: true),
                Positions = positions,
                Triangles = triangles,
                Colors = colors,
                CellIndices = cellIndices,
            };

            job.Execute();

            // 如果没有顶点数据，跳过
            if (positions.Length == 0)
            {
                positions.Dispose();
                triangles.Dispose();
                colors.Dispose();
                cellIndices.Dispose();
                return;
            }

            // 创建/复用 Mesh
            Mesh mesh;
            if (EntityManager.HasComponent<MeshReference>(cellEntity))
            {
                var meshRef = EntityManager.GetComponentData<MeshReference>(cellEntity);
                mesh = meshRef.Mesh;
                mesh.Clear();
            }
            else
            {
                mesh = new Mesh { name = $"HexCell_{cellEntity.Index}" };
                EntityManager.AddComponentData(cellEntity, new MeshReference { Mesh = mesh });
            }

            // 组装完整交错顶点流：布局必须与下方 VertexAttributeDescriptor 声明严格一致，
            // 否则未填满的顶点字节会是未初始化的显存垃圾（NaN 顶点 / 异常 bounds）
            var vertices = new NativeArray<TerrainVertex>(positions.Length, Allocator.Temp);
            for (int i = 0; i < positions.Length; i++)
            {
                vertices[i] = new TerrainVertex
                {
                    Position = positions[i],
                    Color = colors[i],
                    UV1 = cellIndices[i],
                };
            }

            // 上传顶点数据（单条交错流：Position + Color + TexCoord1）
            mesh.SetVertexBufferParams(vertices.Length,
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float32, 4),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 3));

            mesh.SetVertexBufferData(vertices, 0, 0, vertices.Length);
            mesh.SetIndexBufferParams(triangles.Length, IndexFormat.UInt32);
            mesh.SetIndexBufferData(triangles.AsArray(), 0, 0, triangles.Length);
            mesh.SetSubMesh(0, new SubMeshDescriptor(0, triangles.Length));
            vertices.Dispose();

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);

            // 注册到 entities.graphics
            var egs = World.GetExistingSystemManaged<EntitiesGraphicsSystem>();
            if (egs != null)
            {
                var meshID = egs.RegisterMesh(mesh);
                var material = config.TerrainMaterial.Value;
                if (material == null)
                {
                    Debug.LogWarning($"[HexMap] Cell {cellEntity.Index} 材质为空，跳过渲染注册");
                }
                else
                {
                    var matID = egs.RegisterMaterial(material);

                    // 添加渲染组件（首次）或更新 MaterialMeshInfo
                    if (!EntityManager.HasComponent<MaterialMeshInfo>(cellEntity))
                    {
                        var desc = new RenderMeshDescription(ShadowCastingMode.Off, receiveShadows: false);
                        RenderMeshUtility.AddComponents(cellEntity, EntityManager, desc, new MaterialMeshInfo(matID, meshID));

                        // 强制设置 LocalToWorld 为单位矩阵（RenderMeshUtility 可能会添加零矩阵）
                        EntityManager.SetComponentData(cellEntity, new LocalToWorld { Value = float4x4.identity });

                        // 设置 RenderBounds（使用 mesh 的实际 bounds）
                        var bounds = mesh.bounds;
                        EntityManager.SetComponentData(cellEntity, new RenderBounds
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
                        EntityManager.SetComponentData(cellEntity, new MaterialMeshInfo(matID, meshID));

                        // 更新 RenderBounds
                        var bounds = mesh.bounds;
                        EntityManager.SetComponentData(cellEntity, new RenderBounds
                        {
                            Value = new Unity.Mathematics.AABB
                            {
                                Center = new float3(bounds.center.x, bounds.center.y, bounds.center.z),
                                Extents = new float3(bounds.extents.x, bounds.extents.y, bounds.extents.z),
                            }
                        });
                    }
                }
            }
            else
            {
                Debug.LogWarning("[HexMap] EntitiesGraphicsSystem 不存在，无法注册网格");
            }

            // 清理
            positions.Dispose();
            triangles.Dispose();
            colors.Dispose();
            cellIndices.Dispose();
        }
    }

    /// <summary>
    /// 地形 mesh 的交错顶点布局：字段顺序/偏移与 RebuildCellMesh 中
    /// VertexAttributeDescriptor 的声明一致（Position offset 0 / Color offset 12 / UV1 offset 28）
    /// </summary>
    public struct TerrainVertex
    {
        public float3 Position;
        public float4 Color;
        /// <summary>UV1：splat 地形索引三元组</summary>
        public float3 UV1;
    }

    /// <summary>
    /// 托管组件：持有 UnityEngine.Mesh 引用（用于复用 Mesh 对象，避免每帧重建）
    /// </summary>
    public class MeshReference : IComponentData
    {
        public Mesh Mesh;
    }
}
