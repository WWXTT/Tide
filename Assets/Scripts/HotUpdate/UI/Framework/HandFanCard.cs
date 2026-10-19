using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Tide.HotUpdate;

namespace SynergyUI
{
    /// <summary>
    /// 手牌扇区卡实例（2026-10-02 手牌扇形原型）：运行时 AddComponent 到 ACard 实例根上
    /// （同 CardOverlayCard 的挂载模式——prefab 本体不带脚本）。承担四件事：
    /// ① IPointerEnter/Exit 悬停上报，转发给所属 HandFanView 重排目标；
    /// ② 布局 tween 状态（当前值/目标值），由 HandFanView.Tick 统一推进；
    /// ③ 悬停显隐「放置/发动」按钮层（ACard/btn，2026-10-14 手牌出牌接线）：
    ///    条件由宿主屏注入的交互钩子（FanInteractions）评估；指针从卡面移到按钮上
    ///    （子孙物体）不收悬停态——Exit 事件按射线落点是否本卡子孙判留；
    /// ④ 可发动边缘雾光（AHandCard/Flowing 节点，2026-10-15 雾气改版）：Flowing 是用户在
    ///    手牌预制体上摆位的常开占位层（垫在嵌套 ACard 卡面之下）；运行时归一为「卡面四边
    ///    外扩 36px」的雾光画布并挂 SynergyUI/FogEdgeGlow 材质（逐卡随机 _Seed——雾形各异）。
    ///    雾带扩散/噪声翻涌/呼吸全在 shader 内 _Time 自驱，C# 只按可用性缓动 _Fade 淡入淡出。
    /// </summary>
    public sealed class HandFanCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        /// <summary>对账键（引擎卡实例运行时 id）。</summary>
        public uint RuntimeId;

        /// <summary>所属扇区（Create 时回填）。</summary>
        [System.NonSerialized] public HandFanView Fan;

        /// <summary>卡面渲染（Create 时回填）。</summary>
        [System.NonSerialized] public ACardView Face;

        /// <summary>发动按钮（ACard/btn/Use，prefab 用户摆位，Create 时接线）。</summary>
        [System.NonSerialized] public Button UseButton;

        /// <summary>放置按钮（ACard/btn/Place——作为地牌放入元素池）。</summary>
        [System.NonSerialized] public Button PlaceButton;

        // ---- tween 状态（anchoredPosition / z 角 / 等比缩放） ----
        public Vector2 Pos;
        public float Rot;
        public float Scale = 1f;
        public Vector2 TargetPos;
        public float TargetRot;
        public float TargetScale = 1f;

        /// <summary>已到位（收敛判定）：扇区 Tick 据此跳过 transform 写入——单 Canvas 下
        /// 恒久微写入会每帧触发整批重排（2026-10-18 性能）；目标被 RebuildTargets/SetCards 改动后自然失效。</summary>
        public bool AtTarget =>
            (TargetPos - Pos).sqrMagnitude < 0.0001f
            && Mathf.Abs(TargetRot - Rot) < 0.05f
            && Mathf.Abs(TargetScale - Scale) < 0.0005f;

        /// <summary>刚取用（池空新建或首次出池）：首帧吸附目标，不从旧位置飞入。</summary>
        public bool Fresh;

        // ---- 抽卡飞入演出（2026-10-17）：新抽卡从卡堆屏幕位置弧线飞向扇位 ----
        // Flying 期间由扇区 Tick 驱动 TickFlight（不参与常规 exp 缓动）；终点每帧取
        // TargetPos——飞行中重排（邻卡让位/悬停抬升）仍准确落位。
        /// <summary>飞入中（对账删除/池回收时复位）。</summary>
        public bool Flying;
        public Vector2 FlyFrom;                        // 起点（挂载层坐标系）
        public float FlyT;                             // 进度 0..1
        private float _flyArcLift;                     // 贝塞尔控制点向屏幕中央的抬升量（带符号，扇区传入）
        private float _flyStartRot;                    // 起始倾角（朝起点一侧轻倾）
        private CanvasGroup _flyGroup;                 // 飞入淡入用（离堆浮现；池复用归位 1）
        private const float FlyDuration = 0.55f;       // 飞入时长（2026-10-17 用户实测 0.38s 太快看不见）
        private const float FlyStartScale = 0.5f;      // 起始缩放（到达即 TargetScale）

        // ---- 可发动边缘雾光（AHandCard/Flowing + SynergyUI/FogEdgeGlow shader） ----
        private RectTransform _flowRoot;  // Flowing 容器（雾光画布：卡面四边外扩，运行时归一尺寸）
        private Material _flowMat;        // 逐卡实例（_Seed 随机=每卡雾形互不相同）
        private float _fade;              // 当前淡入值（0..1，TickGlow 线性缓动）
        private float _fadeTarget;        // 目标淡入值（SetPlayable 设置）
        private static Texture2D _noiseTex; // 平铺值噪声纹理（shader 滚动雾用，扇区共享，运行时生成）
        private static bool _fogWarned;

        // ---- 离场消融（2026-10-18：放置/使用等结算移卡的对账瞬间起势） ----
        private RectTransform _burnRoot;  // Dissolve 层（卡根下运行时建：Image+逐卡烧蚀材质）
        private Material _burnMat;        // 逐卡实例（_Progress 由 TickDissolve 推进；池复用留用）
        private float _burnT;             // 消融进度 0..1
        private CanvasGroup _cardGroup;   // 卡根 CanvasGroup（ACardView.Bind 必建，飞入淡入共用件——消融期独占 alpha）
        private static bool _burnWarned;
        private const float BurnDuration = 0.5f; // 烧蚀线全程扫完时长

        /// <summary>消融退场中（HandFanView 退场列表驱动；位姿冻结在起势时的扇形位）。</summary>
        public bool Dissolving { get; private set; }

        public void OnPointerEnter(PointerEventData eventData)
        {
            Fan?.NotifyHover(this, true);
            RefreshActionButtons();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // 指针移到本卡子孙（放置/发动按钮）上=仍是本卡悬停态：不收按钮、不退悬停布局
            var over = eventData.pointerCurrentRaycast.gameObject;
            if (over != null && over.transform.IsChildOf(transform)) return;
            HideActionButtons();
            Fan?.NotifyHover(this, false);
        }

        public void SetTarget(Vector2 pos, float rot, float scale)
        {
            TargetPos = pos;
            TargetRot = rot;
            TargetScale = scale;
        }

        /// <summary>当前值直接吸附目标（新建卡就地落位）。</summary>
        public void SnapToTarget()
        {
            Pos = TargetPos;
            Rot = TargetRot;
            Scale = TargetScale;
            ApplyTransform();
        }

        /// <summary>抽卡飞入起势（HandFanView.SetCards 新建卡且带起点时调用）：立即摆到起点
        ///（对账帧不闪在扇位），透明 0 淡入（像从卡堆里抽出），倾角朝起点一侧，缩放 0.5 起步，
        /// 渲染置顶（不被邻卡遮挡）。</summary>
        public void BeginFlyIn(Vector2 origin, float arcLift)
        {
            Flying = true;
            FlyFrom = origin;
            FlyT = 0f;
            _flyArcLift = arcLift;
            Fresh = false; // 飞入接管首帧行为（不再吸附）
            if (_flyGroup == null)
                _flyGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
            _flyGroup.alpha = 0f;
            Pos = origin;
            _flyStartRot = Mathf.Sign(origin.x - TargetPos.x) * 10f;
            Rot = _flyStartRot;
            Scale = TargetScale * FlyStartScale;
            transform.SetAsLastSibling();
            ApplyTransform();
        }

        /// <summary>飞入推进（扇区 Tick 每帧调用）：二次贝塞尔 + easeInOutCubic（离堆慢起手/
        /// 入扇缓落——easeOut 前段全速实测太快看不见）；终点/终角/终缩放每帧取当前目标（重排
        /// 安全）；到达即交还常规 tween 并精确落位，兄弟序交还布局重建。</summary>
        public void TickFlight(float dt)
        {
            FlyT = Mathf.Clamp01(FlyT + dt / FlyDuration);
            float e = FlyT < 0.5f ? 4f * FlyT * FlyT * FlyT : 1f - Mathf.Pow(-2f * FlyT + 2f, 3f) / 2f; // easeInOutCubic
            Vector2 mid = (FlyFrom + TargetPos) * 0.5f + Vector2.up * _flyArcLift;
            float u = 1f - e;
            Pos = u * u * FlyFrom + 2f * u * e * mid + e * e * TargetPos;
            Rot = Mathf.Lerp(_flyStartRot, TargetRot, e);
            Scale = Mathf.Lerp(TargetScale * FlyStartScale, TargetScale, e);
            if (_flyGroup != null) _flyGroup.alpha = Mathf.Clamp01(e / 0.35f); // 前段淡入
            transform.SetAsLastSibling();
            ApplyTransform();
            if (FlyT >= 1f)
            {
                Flying = false;
                if (_flyGroup != null) _flyGroup.alpha = 1f;
                SnapToTarget();
                Fan?.OnFlyInDone(this); // 兄弟序/位姿交还正典布局
            }
        }

        /// <summary>飞入态复位（池回收/对账移除时调）：归还透明度，布局由对账重建。</summary>
        public void ResetFlyState()
        {
            Flying = false;
            if (_flyGroup != null) _flyGroup.alpha = 1f;
        }

        public void ApplyTransform()
        {
            var rt = (RectTransform)transform;
            rt.anchoredPosition = Pos;
            rt.localEulerAngles = new Vector3(0f, 0f, Rot);
            rt.localScale = new Vector3(Scale, Scale, 1f);
        }

        // ---- 按钮层（放置/发动） ----

        /// <summary>按交互钩子条件刷新按钮层显隐（悬停进入/扇区对账后触发；无钩子=对手扇常态隐藏）。</summary>
        public void RefreshActionButtons()
        {
            var hooks = Fan?.Hooks;
            if (hooks == null || Face == null)
            {
                HideActionButtons();
                return;
            }
            bool canUse = hooks.CanUse != null && hooks.CanUse(this);
            bool canPlace = hooks.CanPlaceLand != null && hooks.CanPlaceLand(this);
            Face.SetActionLayerVisible(canUse || canPlace);
            if (UseButton != null && UseButton.gameObject.activeSelf != canUse)
                UseButton.gameObject.SetActive(canUse);
            if (PlaceButton != null && PlaceButton.gameObject.activeSelf != canPlace)
                PlaceButton.gameObject.SetActive(canPlace);
        }

        /// <summary>收起按钮层（出扇/复用/发起流程时）。</summary>
        public void HideActionButtons() => Face?.SetActionLayerVisible(false);

        /// <summary>按钮点击（Button.onClick 接线）：先收按钮再交宿主屏接管（宿主会隐藏手牌扇）。</summary>
        public void OnUseClicked()
        {
            HideActionButtons();
            Fan?.Hooks?.OnUse?.Invoke(this);
        }

        public void OnPlaceClicked()
        {
            HideActionButtons();
            Fan?.Hooks?.OnPlaceLand?.Invoke(this);
        }

        // ---- 可发动边缘雾光 ----

        /// <summary>雾光层接线（HandFanView.Create 时调用一次）：Flowing 预制体摆位为
        /// 常开占位，接线即收起——显示由 SetPlayable 独占开关（对手扇/不可用卡常态隐藏）。
        /// 画布几何在首次启用时（EnsureFogFx）归一为「卡面四边外扩 36px」，prefab 摆位仅占位。</summary>
        public void WireFlowing()
        {
            _flowRoot = transform.Find("Flowing") as RectTransform;
            if (_flowRoot != null && _flowRoot.gameObject.activeSelf)
                _flowRoot.gameObject.SetActive(false);
        }

        /// <summary>可发动雾光开关（宿主屏对账后逐卡设置）：开启=首次惰性装配并淡入，
        /// 关闭=淡出（TickGlow 在 fade 归零时收尾关层——池复用/对账中转都友好退场）。
        /// gateArmed（2026-10-19 教学二·分支金圈）：卡上有限分支条件已达成时光圈换金色，
        /// 区别于常规「费用可用」绿——色值写进逐卡实例材质的 _Color（shader 属性本就在，
        /// 此前恒用默认绿）；池复用/重评每次覆写，无脏态。</summary>
        public void SetPlayable(bool playable, bool gateArmed = false)
        {
            _fadeTarget = playable ? 1f : 0f;
            if (_flowMat != null)
                _flowMat.SetColor("_Color", gateArmed ? ArmedGlowColor : PlayableGlowColor);
            if (playable)
            {
                EnsureFogFx();
                if (_flowRoot != null && !_flowRoot.gameObject.activeSelf)
                    _flowRoot.gameObject.SetActive(true);
            }
        }

        /// <summary>常规可用光圈色（=FogEdgeGlow shader 默认绿，显式写回防上一张金残留）。</summary>
        private static readonly Color PlayableGlowColor = new Color(0.38f, 1f, 0.58f, 1f);

        /// <summary>分支条件已达成（武装态）光圈色——金黄色（2026-10-19 教学二定案）。</summary>
        private static readonly Color ArmedGlowColor = new Color(1f, 0.82f, 0.25f, 1f);

        /// <summary>淡入淡出推进（扇区 Tick 统一调用）：雾带/噪声翻涌/呼吸全在 shader 内
        /// _Time 自驱，此处只线性缓动 _Fade（4.5/s≈0.22s 过渡）；淡出归零后关层省一张 quad。
        /// phase 参数仅为调用点兼容保留（巡游相位已随旧流光方案退役）。</summary>
        public void TickGlow(float phase)
        {
            if (_flowRoot == null || _flowMat == null) return;
            if (Mathf.Abs(_fade - _fadeTarget) > 0.004f)
            {
                _fade = Mathf.MoveTowards(_fade, _fadeTarget, Time.deltaTime * 4.5f);
                _flowMat.SetFloat("_Fade", _fade);
            }
            else if (_fadeTarget == 0f && _flowRoot.gameObject.activeSelf)
                _flowRoot.gameObject.SetActive(false);
        }

        /// <summary>雾光装配（首次启用惰性执行一次）：Flowing 归一为「卡面+四边外扩 36px」
        /// （中心锚点、与卡面同心中放），Image sprite 清空挂逐卡实例材质（几何常数=卡面半尺寸
        /// +画布尺寸 + 随机 _Seed）。raycastTarget 关——不挡卡面/按钮指针。
        /// shader 缺失（未导入/未随包收录）整段降级为无雾光并警告一次。</summary>
        private void EnsureFogFx()
        {
            if (_flowRoot == null || _flowMat != null) return;
            // 2026-10-18 真机修复：Shader.Find 只搜玩家包本体，美术 shader 只在 artstatic bundle 里
            // （玩家日志实锤找不到）——改走 HotUpdateAssets（编辑器 AssetDatabase → 真机 bundle）。
            var shader = HotUpdateAssets.Load<Shader>("FogEdgeGlow", "Assets/Art/Shaders/FogEdgeGlow.shader");
            if (shader == null)
            {
                if (!_fogWarned)
                {
                    _fogWarned = true;
                    Debug.LogWarning("[HandFan] SynergyUI/FogEdgeGlow shader 未找到"
                        + "（Assets/Art/Shaders/FogEdgeGlow.shader 未导入/未随包收录）——可发动雾光缺省");
                }
                _flowRoot = null; // 后续路径全部静默跳过
                return;
            }

            // 卡面真值：嵌套 ACard 实例（缺=卡根，与 ACardView.Bind 同回落链）；布局未就绪的异常尺寸回落卡根
            var face = transform.Find("ACard") as RectTransform ?? (RectTransform)transform;
            Vector2 faceSize = face.rect.size;
            if (faceSize.x < 1f || faceSize.y < 1f) faceSize = ((RectTransform)transform).rect.size;

            const float margin = 36f; // 雾气外扩边距（shader 在 quad 边前强制归零，完整雾尾不留切口）
            _flowRoot.anchorMin = _flowRoot.anchorMax = _flowRoot.pivot = new Vector2(0.5f, 0.5f);
            _flowRoot.anchoredPosition = Vector2.zero; // 卡面在卡根居中，雾光画布同以卡根为中心
            _flowRoot.sizeDelta = faceSize + new Vector2(margin * 2f, margin * 2f);

            var flowImg = _flowRoot.GetComponent<Image>();
            if (flowImg == null) flowImg = _flowRoot.gameObject.AddComponent<Image>();
            flowImg.sprite = null; // 整幅四边形交 shader 绘制（prefab 旧占位美术一并清退）
            flowImg.color = Color.white;
            flowImg.raycastTarget = false;

            _flowMat = new Material(shader);
            _flowMat.SetTexture("_NoiseTex", EnsureNoiseTexture());
            _flowMat.SetVector("_CardHalf", new Vector4(faceSize.x * 0.5f, faceSize.y * 0.5f, 0f, 0f));
            _flowMat.SetVector("_QuadSize", new Vector4(faceSize.x + margin * 2f, faceSize.y + margin * 2f, 0f, 0f));
            _flowMat.SetFloat("_Margin", margin);
            _flowMat.SetFloat("_Seed", UnityEngine.Random.value * 97f); // 逐卡呼吸错相+噪声域错开
            _flowMat.SetFloat("_Fade", _fade);
            flowImg.material = _flowMat;
        }

        // ---- 离场消融（2026-10-18） ----

        /// <summary>离场消融起势（HandFanView 对账移除时调用＝引擎开始结算移卡的瞬间；确认
        /// 使用/选格阶段不经过对账移除，天然不触发）：叠噪声烧蚀层+卡面同步淡出。按钮/雾光
        /// 同拍收起（悬停中的卡离场不残留交互件）、指针射线关闭、渲染置顶（邻卡合拢期烧蚀
        /// 光盖在其上）。shader 缺失返回 false＝调用方即时回收（无消融不阻断对账）。</summary>
        public bool BeginDissolve()
        {
            if (Dissolving) return true;
            if (!EnsureBurnFx()) return false;
            Dissolving = true;
            _burnT = 0f;
            Fresh = false;
            Flying = false;
            HideActionButtons();
            SetPlayable(false); // 雾光目标转淡出（TickGlow 由退场驱动链继续推）
            _cardGroup = GetComponent<CanvasGroup>(); // ACardView.Bind 必建；极端缺件兜底
            if (_cardGroup == null) _cardGroup = gameObject.AddComponent<CanvasGroup>();
            _cardGroup.blocksRaycasts = false; // 退场卡不再接指针
            _cardGroup.alpha = 1f;
            transform.SetAsLastSibling();
            return true;
        }

        /// <summary>消融推进（HandFanView.Tick 退场列表驱动）：烧蚀线 0.5s 自下而上扫完；
        /// 卡面在 10%~75% 进度间同步淡出（线扫到哪儿卡面隐到哪儿）。返回 true＝播完可回池。</summary>
        public bool TickDissolve(float dt)
        {
            _burnT = Mathf.Clamp01(_burnT + dt / BurnDuration);
            if (_burnMat != null) _burnMat.SetFloat("_Progress", _burnT);
            if (_cardGroup != null) _cardGroup.alpha = 1f - Mathf.SmoothStep(0.1f, 0.75f, _burnT);
            return _burnT >= 1f;
        }

        /// <summary>消融态复位（播完回池/中途回手两种出口都过这里）：alpha/射线归位+烧蚀层
        /// 收起（材质留用——_Progress 回零，池内复用零装配；卡面 alpha 由对账 Apply 重写无冲突）。</summary>
        public void ResetDissolve()
        {
            Dissolving = false;
            _burnT = 0f;
            if (_burnMat != null) _burnMat.SetFloat("_Progress", 0f);
            if (_burnRoot != null && _burnRoot.gameObject.activeSelf)
                _burnRoot.gameObject.SetActive(false);
            if (_cardGroup != null)
            {
                _cardGroup.alpha = 1f;
                _cardGroup.blocksRaycasts = true;
            }
        }

        /// <summary>烧蚀层装配（BeginDissolve 惰性执行；池复用只校准几何）。几何与雾光同口径：
        /// 嵌套 ACard 卡面实值、卡根居中、外扩 8px（SDF 卡形在 quad 边内收，辉光贴边不裁切）。
        /// shader 缺失（未导入/未随包收录）整段降级=无消融并警告一次。</summary>
        private bool EnsureBurnFx()
        {
            var shader = HotUpdateAssets.Load<Shader>("CardDissolve", "Assets/Art/Shaders/CardDissolve.shader");
            if (shader == null)
            {
                if (!_burnWarned)
                {
                    _burnWarned = true;
                    Debug.LogWarning("[HandFan] SynergyUI/CardDissolve shader 未找到"
                        + "（Assets/Art/Shaders/CardDissolve.shader 未导入/未随包收录）——手牌离场消融缺省");
                }
                return false;
            }

            var face = transform.Find("ACard") as RectTransform ?? (RectTransform)transform;
            Vector2 faceSize = face.rect.size;
            if (faceSize.x < 1f || faceSize.y < 1f) faceSize = ((RectTransform)transform).rect.size;

            const float margin = 8f;
            if (_burnRoot == null)
            {
                _burnRoot = new GameObject("Dissolve", typeof(RectTransform), typeof(Image)).transform as RectTransform;
                _burnRoot.SetParent(transform, false);
            }
            _burnRoot.anchorMin = _burnRoot.anchorMax = _burnRoot.pivot = new Vector2(0.5f, 0.5f);
            _burnRoot.anchoredPosition = Vector2.zero;
            _burnRoot.sizeDelta = faceSize + new Vector2(margin * 2f, margin * 2f);
            _burnRoot.SetAsLastSibling();

            var burnImg = _burnRoot.GetComponent<Image>();
            burnImg.sprite = null; // 整幅四边形交 shader 绘制
            burnImg.color = Color.white;
            burnImg.raycastTarget = false;
            if (_burnMat == null)
            {
                _burnMat = new Material(shader);
                _burnMat.SetTexture("_NoiseTex", EnsureNoiseTexture()); // 与雾光共享平铺噪声
                _burnMat.SetFloat("_Seed", UnityEngine.Random.value * 89f); // 逐卡烧蚀毛边各异
            }
            _burnMat.SetVector("_CardHalf", new Vector4(faceSize.x * 0.5f, faceSize.y * 0.5f, 0f, 0f));
            _burnMat.SetVector("_QuadSize", new Vector4(faceSize.x + margin * 2f, faceSize.y + margin * 2f, 0f, 0f));
            _burnMat.SetFloat("_Progress", 0f);
            burnImg.material = _burnMat;
            _burnRoot.gameObject.SetActive(true);
            return true;
        }

        /// <summary>平铺值噪声纹理（128px，R 通道；8/16 格双倍频——格点确定性哈希+平滑插值，
        /// 边界取模=无缝平铺）。扇区间静态共享（无资产文件，运行时生成）；shader 两层滚动雾
        /// 以不同尺度/方向采样同一张。</summary>
        private static Texture2D EnsureNoiseTexture()
        {
            if (_noiseTex != null) return _noiseTex;
            const int s = 128;
            var tex = new Texture2D(s, s, TextureFormat.ARGB32, false)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
            };
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float v = 0.65f * TileValueNoise(x, y, s, 8) + 0.35f * TileValueNoise(x, y, s, 16);
                    byte b = (byte)(Mathf.Clamp01(v) * 255f);
                    px[y * s + x] = new Color32(b, b, b, 255);
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            _noiseTex = tex;
            return tex;
        }

        /// <summary>平铺值噪声采样（lattice 周期数=cells）：双线性+smoothstep 插值，
        /// 索引按格数取模——左右/上下边界无缝衔接。</summary>
        private static float TileValueNoise(int x, int y, int size, int cells)
        {
            float fx = x * (cells / (float)size), fy = y * (cells / (float)size);
            int x0 = (int)fx, y0 = (int)fy;
            float u = fx - x0, v = fy - y0;
            u = u * u * (3f - 2f * u);
            v = v * v * (3f - 2f * v);
            float a = Lattice(x0 % cells, y0 % cells);
            float b = Lattice((x0 + 1) % cells, y0 % cells);
            float c = Lattice(x0 % cells, (y0 + 1) % cells);
            float d = Lattice((x0 + 1) % cells, (y0 + 1) % cells);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        /// <summary>格点确定性哈希→[0,1)（uint 回绕乘法混淆，不占 Random 全局状态）。</summary>
        private static float Lattice(int x, int y)
        {
            uint h = ((uint)x * 374761393u + (uint)y * 668265263u) * 1274126177u + 1013904223u;
            h ^= h >> 13;
            h *= 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / 16777215f;
        }
    }
}
