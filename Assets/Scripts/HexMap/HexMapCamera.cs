using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HexMap
{
    /// <summary>
    /// 六边形地图相机控制（挂到场景相机上，Play 模式生效）：
    /// - WASD / 方向键平移（方向随相机朝向旋转）
    /// - 滚轮缩放（高度、俯仰角、平移速度三者联动）
    /// - Q / E 旋转
    /// - 中键拖拽平移
    ///
    /// 位置为连续移动（不吸附 cell 中心）+ 指数阻尼平滑过渡，
    /// 按 HexMapConfig 的地图尺寸做边界钳制；每帧 LateUpdate 把位置写入
    /// HexMapCameraData 单例，供 HexChunkStreamingSystem 决定加载范围。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class HexMapCamera : MonoBehaviour
    {
        [Header("Zoom（0 = 远俯视，1 = 近低角）")]
        public float minZoomHeight = 140f;
        public float maxZoomHeight = 22f;
        public float minZoomPitch = 75f;
        public float maxZoomPitch = 45f;
        [Range(0.5f, 5f)] public float zoomSensitivity = 2f;

        [Header("Move")]
        public float moveSpeedMinZoom = 300f;
        public float moveSpeedMaxZoom = 60f;
        public float rotationSpeed = 90f;
        [Tooltip("中键拖拽平移速度（随高度自动缩放）")]
        public float dragSpeed = 1f;

        [Header("Smoothing（指数阻尼，越大越跟手）")]
        public float positionDamping = 8f;
        public float zoomDamping = 10f;
        public float rotationDamping = 12f;

        // 输入累积的目标值
        private float _zoom;
        private float2 _posXZ;
        private float _yaw;

        // 阻尼平滑后的当前值（真正写到 transform 上的）
        private float2 _smoothPos;
        private float _smoothZoom;
        private float _smoothYaw;
        private bool _initialized;

        private World _world;
        private EntityQuery _configQuery;
        private EntityQuery _cameraQuery;

        private void Update()
        {
            if (!_initialized)
            {
                // 首帧继承设计器摆放：只取 XZ 与朝向，高度/俯仰由 zoom 接管
                _posXZ = _smoothPos = new float2(transform.position.x, transform.position.z);
                _yaw = _smoothYaw = transform.eulerAngles.y;
                _zoom = _smoothZoom = Mathf.Clamp01(Mathf.InverseLerp(minZoomHeight, maxZoomHeight, transform.position.y));
                _initialized = true;
            }

            ResolveWorld();

            AdjustZoom();
            AdjustRotation();
            AdjustPosition();
            AdjustDrag();

            _posXZ = ClampToMap(_posXZ);

            float dt = Time.deltaTime;
            _smoothPos = math.lerp(_smoothPos, _posXZ, 1f - math.exp(-positionDamping * dt));
            _smoothZoom = math.lerp(_smoothZoom, _zoom, 1f - math.exp(-zoomDamping * dt));
            _smoothYaw = math.lerp(_smoothYaw, _yaw, 1f - math.exp(-rotationDamping * dt));

            transform.position = new Vector3(
                _smoothPos.x,
                math.lerp(minZoomHeight, maxZoomHeight, _smoothZoom),
                _smoothPos.y);
            transform.rotation = Quaternion.Euler(
                math.lerp(minZoomPitch, maxZoomPitch, _smoothZoom),
                _smoothYaw,
                0f);
        }

        private void LateUpdate()
        {
            if (_world == null || !_world.IsCreated || _cameraQuery == null || _cameraQuery.IsEmpty)
                return;

            _world.EntityManager.SetComponentData(
                _cameraQuery.GetSingletonEntity(),
                new HexMapCameraData { Position = transform.position });
        }

        /// <summary>World 可能在相机启动后才创建（协程 Install），也可能因重进 Play 重建</summary>
        private void ResolveWorld()
        {
            var w = World.DefaultGameObjectInjectionWorld;
            if (w == null || !w.IsCreated)
                return;
            if (w == _world && _configQuery != null)
                return;

            _world = w;
            var em = w.EntityManager;
            _configQuery = em.CreateEntityQuery(ComponentType.ReadOnly<HexMapConfig>());
            _cameraQuery = em.CreateEntityQuery(ComponentType.ReadOnly<HexMapCameraData>());
        }

        private void AdjustZoom()
        {
            float delta = Input.GetAxis("Mouse ScrollWheel");
            if (delta != 0f)
                _zoom = Mathf.Clamp01(_zoom + delta * zoomSensitivity);
        }

        private void AdjustRotation()
        {
            if (Input.GetKey(KeyCode.E))
                _yaw += rotationSpeed * Time.deltaTime;
            if (Input.GetKey(KeyCode.Q))
                _yaw -= rotationSpeed * Time.deltaTime;
            // 不取模：yaw 目标持续累积，阻尼插值不会绕 360° 边界反向转
        }

        private void AdjustPosition()
        {
            float xInput = Input.GetAxis("Horizontal");
            float zInput = Input.GetAxis("Vertical");
            if (Mathf.Approximately(xInput, 0f) && Mathf.Approximately(zInput, 0f))
                return;

            float speed = math.lerp(moveSpeedMinZoom, moveSpeedMaxZoom, _zoom) * Time.deltaTime;
            float3 forward = new float3(math.sin(math.radians(_yaw)), 0f, math.cos(math.radians(_yaw)));
            float3 right = new float3(forward.z, 0f, -forward.x);
            _posXZ += (right * xInput + forward * zInput).xz * speed;
        }

        private void AdjustDrag()
        {
            if (!Input.GetMouseButton(2))
                return;

            float dx = Input.GetAxis("Mouse X");
            float dy = Input.GetAxis("Mouse Y");
            float scale = dragSpeed * math.lerp(minZoomHeight, maxZoomHeight, _zoom) * 0.02f;

            float3 forward = new float3(math.sin(math.radians(_yaw)), 0f, math.cos(math.radians(_yaw)));
            float3 right = new float3(forward.z, 0f, -forward.x);
            _posXZ += (right * -dx + forward * -dy).xz * scale;
        }

        /// <summary>地图边界钳制（连续坐标，不吸附；需要 HexMapConfig 已安装）</summary>
        private float2 ClampToMap(float2 pos)
        {
            if (_configQuery == null || _configQuery.IsEmpty)
                return pos;

            var em = _world.EntityManager;
            var cfg = em.GetComponentData<HexMapConfig>(_configQuery.GetSingletonEntity());
            if (!cfg.Blob.IsCreated)
                return pos;

            ref var blob = ref cfg.Blob.Value;
            var metrics = HexMetrics.FromBlob(ref blob);

            // cell 中心范围 + 一个 cell 的余量（中心公式与 HexChunkStreamingSystem.CreateCell 一致）。
            // xMax 注意：错行偏移是 0/0.5 交替（奇数行 +0.5 cell），不是随行号累加——
            // 用 (Z-1)*0.5 连续近似会把右界放大十几个 cell 宽，相机能开出地图右侧
            float xMax = (blob.CellCount.x - 1 + 0.5f) * (metrics.InnerRadius * 2f)
                         + metrics.InnerRadius;
            float zMax = (blob.CellCount.y - 1) * (metrics.OuterRadius * 1.5f) + metrics.OuterRadius;
            return math.clamp(pos, new float2(-metrics.InnerRadius, -metrics.OuterRadius), new float2(xMax, zMax));
        }
    }
}
