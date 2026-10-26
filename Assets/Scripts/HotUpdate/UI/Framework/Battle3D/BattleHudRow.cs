using System.Collections.Generic;
using GameBoard;
using Tide.HotUpdate;
using TMPro;
using UnityEngine;

namespace SynergyUI
{
    /// <summary>
    /// 中线状态行（2026-10-13 按钮上盘 → 10-14 中线行落格 → 当晚 8×7 状态行 → 本批镜像纠正+阶段格 →
    /// 10-16 临时元素产量双格）：中线行（z3）7 格 x0..x6——画面左→右（画面左=x0，镜像修正后口径）：
    /// 阶段显示(x0) / 己方剩余操作时间(x1) / 己方临时产出(x2) / 连锁速度(x3) / 对方临时产出(x4) /
    /// 对手剩余操作时间(x5) / 回合结束(x6，贴己方右手边——原 x0 放反，2026-10-14 用户纠正)；
    /// x7 槽=odd-r 居中缺口（BoardLayout.NoTileAt，无实体格）。
    /// 认领 BattleHexBoard 拼好的 tile-{col}-3 改名 hud-tile-{key} + 原位换 tint 材质实例（不增删格，
    /// 55 格口径不变，浮起拼装动画随行同走）；中线行文字**全部平贴格面**（同地牌计数条口径不朝相机：
    /// LookRotation(down, forward) 字向正——相机 (7,8,-4)/(50,0,0) 实测）；阶段格=当前阶段
    /// 【己方主要阶段】式动态文字（SetPhase 喂文）、结束回合格=【回合结束】；状态三格第二行数值
    /// 由 BattleScreen 经 SetStatusValue 喂文（操作计时 2026-10-14 已接：真人对战 MatchClock 掉秒 /
    /// 人机 ∞；连锁速度 2026-10-15 已接：引擎记速器实值，网络快照未承载时占位「—」）。
    /// 返回主菜单地块本行退役（7 格无空位）——改由 BattleScreen.Build 代码补建画布按钮。
    ///
    /// 纯表现层：引擎 BoardLayout/BoardState 不感知本行归属（中线行角色=Middle）。
    /// 地块名 hud-tile-{key}（前缀区别于 tile-{x}-{z} 棋盘格），点击/悬停由 BattleScreen.PollFieldPointer
    /// 射线统一路由。材质：认领格现有分区材质（中线行 Zone20；缺资产回落预制体自带材质）实例化后
    /// 着色；实例与数字条随 Unmount 显式销毁并还原格名/原材质，不进静态长持（域边界 fake-null 教训同 BattleHexBoard）。
    /// </summary>
    public static class BattleHudRow
    {
        // 中线状态行落位（2026-10-14 晚定案·本批镜像纠正：画面左=x0 → 画面右=x6）
        public const int PhaseCol = 0;      // 阶段显示格：tile-0-3（画面左端——平贴当前阶段文字，SetPhase 喂文）
        public const int SelfTimeCol = 1;   // 己方剩余操作时间格：tile-1-3
        public const int SelfTempCol = 2;   // 己方临时元素产量格：tile-2-3（2026-10-16 全类型累计钳记录——
                                            // 本周期累计产出/地牌上限，自己回合开始清零；贴己方计时侧）
        public const int ChainSpeedCol = 3; // 连锁速度格：tile-3-3
        public const int OppTempCol = 4;    // 对方临时元素产量格：tile-4-3（同上——贴对方计时侧）
        public const int OppTimeCol = 5;    // 对手剩余操作时间格：tile-5-3
        public const int EndTurnCol = 6;    // 结束回合格：tile-6-3（画面右端，贴己方右手边——原 x0 放反已纠）

        // v6（2026-10-23 §11.2）+ 21:35 DSH 修订：**长驻格面不再满格实心亮黄**——回合结束/时间格
        // 常驻=中立暖灰 #3A3630 + 暗金角标（#6B5930 小方标，持有方亮标）。平贴文字=米白 #E6E0D4 a=0.92
        //（对比 ≥4.5:1 修订：底格压光后更暗，提 alpha 补偿）。
        // 2026-10-26：阶段转换表现改内收（10-24 外扩版退役）：光圈自外向内收拢 0.5s → 停顿 0.5s
        // → 才翻阶段文字（己方蓝/对方红，色源 BattleTileFx.UnitGlow*，网格/shader 复用 BattleTileRing）。
        private static readonly Color EndTurnTint = new Color(0.227f, 0.212f, 0.188f);   // 常驻=中立（角标担识别）
        private static readonly Color StatusTint = new Color(0.227f, 0.212f, 0.188f);
        private static readonly Color FlatTextColor = new Color(0.902f, 0.878f, 0.831f, 0.92f);
        private static readonly Color MarkColor = new Color(0.42f, 0.35f, 0.19f);        // 暗金角标 #6B5930

        // 时间格优先权联动（2026-10-14 MD 式 → 21:35 修订）：持有方=亮暗金角标（格面恒中立灰，
        // 与半场蜂窝亮金同源对齐——蜂窝仍是优先权主显示）。
        private static Material _selfTimeTint, _oppTimeTint; // 两时间格 tint 材质实例（Mount 捕获）
        private static Material _markMat;                    // 角标共享材质实例（Unmount 销毁）
        private static Mesh _markMesh;                       // 角标小方面片（Unmount 销毁）

        // ==== 阶段转换光圈（2026-10-26 用户定案：自外向内收拢 0.5s → 停顿 0.5s → 才翻阶段字，替 10-24 外扩版） ====
        // 节流口径：表现层串行排队，最小间隔=收拢+停顿（1.0s）——引擎侧连续阶段跳变
        //（对手结束→己方准备→己方主要等瞬翻）按序逐拍播出，「刚结束一下又到自己」不再瞬切。
        private const float PhaseRingSeconds = 0.5f;     // 光圈收拢时长（外扩口径 2.6 格径收至格轮廓）
        private const float PhaseRingHoldSeconds = 0.5f; // 收拢后停顿时长——停顿毕才翻阶段文字
        private const float PhaseRingExpand = 2.6f;      // 收拢起始（外扩）倍率（格轮廓外约 2.6 格径）
        private const float PhaseRingPeak = 1.8f;        // 收拢末强度（additive，收拢过程渐入；停顿期驻留）
        private const int PhaseQueueCap = 8;             // 待播队列上限（超长丢最旧——极端连跳防积压）
        private static MeshRenderer _phaseRing;        // 阶段光圈件（挂 hud-tile-phase 下，建一次复用）
        private static MaterialPropertyBlock _phaseMpb;
        private static readonly int RingColorId = Shader.PropertyToID("_RingColor");       // BattleTileRing.shader
        private static readonly int RingIntensityId = Shader.PropertyToID("_RingIntensity");
        private static readonly Queue<(string text, bool myTurn)> _phasePending = new();
        private static bool _phaseRingBusy;            // 在播（队头占用——串行节流的闸）
        private static int _phaseEpoch;                // 作废代次（Unmount++：在播循环/接链守卫）
        private static readonly Dictionary<int, GameObject> _marks = new Dictionary<int, GameObject>(); // key=列

        private sealed class HudTile
        {
            public Transform Tile;
            public string OrigName;
            public Material OrigMat;
            public Material TintMat;
        }

        private static readonly List<HudTile> _claimed = new List<HudTile>();
        private static readonly Dictionary<int, TextMeshPro> _labels = new Dictionary<int, TextMeshPro>(); // key=列（状态格数字条/平贴文字）
        private static readonly Dictionary<int, string> _headers = new Dictionary<int, string>(); // key=列 → 标题行（SetStatusValue 重写第二行用）
        private static TextMeshPro _phaseLabel;        // 阶段显示格平贴文字（SetPhase 改写）
        private static string _phaseText = "【—】";     // 装台前先存文本，Mount 时落字

        /// <summary>结束回合地块（null=未装台/缺件）。</summary>
        public static Transform EndTurnTile { get; private set; }

        /// <summary>连锁速度格（中线正中 tile-3-3；null=未装台/缺格）——掷币落点等外部取位用
        ///（2026-10-26：掷币「准确落到中央连锁速度那一格」）。</summary>
        public static Transform ChainSpeedTile => TileOf("hud-tile-chain-speed");

        /// <summary>装台幂等（BattleStageDirector.MountBoardAndDawnAsync 在 BattleZoneProps.Mount 后调）：
        /// 须在 BattleHexBoard.Build + BattleBoardGrid.Mount 之后（认领已拼好的中线行格）。</summary>
        public static void Mount()
        {
            // 跨 Play 残留治理（2026-10-14）：本工程关域重载——Play 中止不走 BattleScreen.OnExit/
            // Unmount，_claimed 携上一局已销毁棋盘的僵尸条目跨 Play 存活，「已装台」误判致本轮
            // 中线行整行不装（结束回合/阶段/计时格全缺）。先剔僵尸：全僵尸=清静态重装。
            if (_claimed.Count > 0)
            {
                _claimed.RemoveAll(h => h == null || h.Tile == null); // Transform 销毁=Unity fake-null
                if (_claimed.Count == 0)
                {
                    _labels.Clear();
                    _headers.Clear();
                    _marks.Clear();
                    _markMat = null;
                    _markMesh = null;
                    EndTurnTile = null;
                    _phaseLabel = null;
                    _selfTimeTint = _oppTimeTint = null; // 材质实例随旧棋盘销毁
                    ResetPhaseRingState(); // 光圈件随旧棋盘销毁+待播队列作废（防跨局旧账续播）
                }
            }
            if (_claimed.Count > 0) return;
            var boardRoot = BattleBoardGrid.Root;
            if (boardRoot == null)
            {
                Debug.LogError("[BattleHudRow] 棋盘未装台（BattleBoardGrid.Root=null）——中线状态行不装");
                return;
            }

            // 结束回合落 x6（镜像修正后画面右端=己方右手边，原 x0 放反）+ 平贴【回合结束】+ 暗金角标
            EndTurnTile = ClaimTile(boardRoot.transform, "hud-tile-end-turn", EndTurnCol, EndTurnTint);
            AttachFlatLabel(EndTurnTile, EndTurnCol, "【回合结束】", 3.5f);
            AttachMark(EndTurnTile, EndTurnCol, active: true);
            // 阶段显示落 x0（原结束回合格位改役）：平贴【己方准备阶段】式当前阶段（SetPhase 喂文）
            _phaseLabel = AttachFlatLabel(ClaimTile(boardRoot.transform, "hud-tile-phase", PhaseCol, StatusTint), PhaseCol, _phaseText, 2.6f);
            AttachLabel(ClaimTile(boardRoot.transform, "hud-tile-self-time", SelfTimeCol, StatusTint), SelfTimeCol, "己方时间");
            AttachLabel(ClaimTile(boardRoot.transform, "hud-tile-self-temp", SelfTempCol, StatusTint), SelfTempCol, "己方临时");
            AttachLabel(ClaimTile(boardRoot.transform, "hud-tile-chain-speed", ChainSpeedCol, StatusTint), ChainSpeedCol, "连锁速度");
            AttachLabel(ClaimTile(boardRoot.transform, "hud-tile-opp-temp", OppTempCol, StatusTint), OppTempCol, "对方临时");
            AttachLabel(ClaimTile(boardRoot.transform, "hud-tile-opp-time", OppTimeCol, StatusTint), OppTimeCol, "对手时间");
            _selfTimeTint = TintMatOf("hud-tile-self-time"); // 优先权联动高亮用（材质实例随格留存）
            _oppTimeTint = TintMatOf("hud-tile-opp-time");
            // 时间格角标常建默认熄，持有方亮标（SetTimePriority 对账切换）
            AttachMark(TileOf("hud-tile-self-time"), SelfTimeCol, active: false);
            AttachMark(TileOf("hud-tile-opp-time"), OppTimeCol, active: false);
            Debug.Log("[BattleHudRow][PASS] 中线状态行：hud-tile-end-turn @tile-" + EndTurnCol + "-" + BoardLayout.MiddleZ
                      + "（贴己方右手·平贴【回合结束】）+ 阶段显示@" + PhaseCol
                      + " + 状态格 己方时间@" + SelfTimeCol + "/己方临时@" + SelfTempCol
                      + "/连锁速度@" + ChainSpeedCol + "/对方临时@" + OppTempCol + "/对手时间@" + OppTimeCol
                      + "（操作计时+连锁速度+临时产量已接：m:ss/∞·记速器实值·全类型累计钳 N/上限）");
        }

        /// <summary>认领格名 → tint 材质实例（时间格优先权联动用；未认领=null）。</summary>
        private static Material TintMatOf(string tileName)
        {
            foreach (var h in _claimed)
                if (h != null && h.Tile != null && h.Tile.name == tileName) return h.TintMat;
            return null;
        }

        /// <summary>时间格随优先权（扳机）切换（BattleFieldView.Apply 对账调用，与半场蜂窝同切；
        /// 21:35 修订：格面恒中立灰，持有方亮**暗金角标**——不再满格实心亮黄；其它=双暗。
        /// 棋盘未就绪/缺件静默）。</summary>
        public static void SetTimePriority(int holderSeat)
        {
            if (_selfTimeTint != null) _selfTimeTint.color = StatusTint;
            if (_oppTimeTint != null) _oppTimeTint.color = StatusTint;
            SetMarkActive(SelfTimeCol, holderSeat == 0);
            SetMarkActive(OppTimeCol, holderSeat == 1);
        }

        /// <summary>拆台（BattleStageDirector.DismountBoard 调）：还原格名/原分区材质，销毁 tint 材质实例与数字条。</summary>
        public static void Unmount()
        {
            foreach (var h in _claimed)
            {
                if (h.TintMat != null) Object.Destroy(h.TintMat);
                if (h.Tile != null) // 棋盘整根销毁在即，还原是防御（重装台/冒烟路径下防串名）
                {
                    h.Tile.name = h.OrigName;
                    var rend = h.Tile.GetComponent<MeshRenderer>();
                    if (rend != null && h.OrigMat != null) rend.sharedMaterial = h.OrigMat;
                }
            }
            _claimed.Clear();
            foreach (var kv in _labels)
                if (kv.Value != null) Object.Destroy(kv.Value.gameObject); // 数字条挂 tile 下随盘销毁，此处防御重装台残留
            _labels.Clear();
            _headers.Clear();
            _marks.Clear(); // 角标挂 tile 下随盘销毁，此处清索引
            if (_markMat != null) Object.Destroy(_markMat);
            if (_markMesh != null) Object.Destroy(_markMesh);
            _markMat = null;
            _markMesh = null;
            _phaseLabel = null;
            _selfTimeTint = _oppTimeTint = null; // 材质实例随 _claimed 销毁，清引用
            ResetPhaseRingState(); // 光圈作废（代次++杀在播循环）+队列清空+件销毁（挂 tile 下随盘销毁，防御重装台残留）
            EndTurnTile = null;
        }

        /// <summary>光圈态清算（Unmount/僵尸全清调）：代次++作废在播循环与接链，队列清空，光圈件销毁。</summary>
        private static void ResetPhaseRingState()
        {
            _phaseEpoch++;
            _phaseRingBusy = false;
            _phasePending.Clear();
            if (_phaseRing != null)
            {
                Object.Destroy(_phaseRing.gameObject);
                _phaseRing = null;
            }
        }

        /// <summary>认领格名 → Transform（时间格角标挂载用）。</summary>
        private static Transform TileOf(string tileName)
        {
            foreach (var h in _claimed)
                if (h != null && h.Tile != null && h.Tile.name == tileName) return h.Tile;
            return null;
        }

        /// <summary>暗金角标（21:35 修订）：格角小方片 0.3×0.3（未缩格空间，随格 0.82 缩=世界 ~0.25），
        /// 暗金 #6B5930、无碰撞体、共享材质/网格实例随 Unmount 销毁；active 由持有方逻辑切换。</summary>
        private static void AttachMark(Transform tile, int col, bool active)
        {
            if (tile == null) return;
            if (_markMat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) return;
                _markMat = new Material(shader) { color = MarkColor };
                _markMat.SetFloat("_Smoothness", 0.3f);
                _markMat.SetFloat("_Metallic", 0f);
            }
            if (_markMesh == null)
            {
                _markMesh = new Mesh { name = "hud-mark-quad" };
                _markMesh.vertices = new[]
                {
                    new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f),
                    new Vector3(0.5f, 0f, 0.5f), new Vector3(-0.5f, 0f, 0.5f)
                };
                _markMesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
                _markMesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
                _markMesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            }
            var go = new GameObject("hud-mark");
            go.transform.SetParent(tile, false);
            go.transform.localPosition = new Vector3(0.46f, 0.012f, 0.46f); // 格角（未缩格半径 1 空间·内收）
            go.transform.localScale = Vector3.one * 0.3f;
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = _markMesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _markMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.SetActive(active);
            _marks[col] = go;
        }

        /// <summary>角标亮/熄（缺件静默——缺列=未建）。</summary>
        private static void SetMarkActive(int col, bool active)
        {
            if (_marks.TryGetValue(col, out var go) && go != null) go.SetActive(active);
        }

        /// <summary>认领中线行格：改名 hud-tile-{key} + 原位换 tint 材质实例（记原始名/材质供还原）。</summary>
        private static Transform ClaimTile(Transform boardRoot, string name, int col, Color tint)
        {
            var tile = boardRoot.transform.Find($"tile-{col}-{BoardLayout.MiddleZ}");
            if (tile == null)
            {
                Debug.LogError($"[BattleHudRow] 中线行格 tile-{col}-{BoardLayout.MiddleZ} 缺失——功能地块 {name} 不装");
                return null;
            }

            var rend = tile.GetComponent<MeshRenderer>();
            var origMat = rend != null ? rend.sharedMaterial : null;
            Material tintMat = null;
            if (rend != null)
            {
                tintMat = Object.Instantiate(origMat != null ? origMat : rend.material);
                tintMat.color = tint;
                rend.sharedMaterial = tintMat;
            }

            var h = new HudTile { Tile = tile, OrigName = tile.name, OrigMat = origMat, TintMat = tintMat };
            _claimed.Add(h);
            tile.name = name;
            return tile;
        }

        /// <summary>状态格第二行改写（操作计时 2026-10-14：真人对战 m:ss / 人机 ∞；
        /// 连锁速度 2026-10-15：引擎记速器实值）。未装台/缺格静默（装台晚于本屏——调用方周期重写直至就绪）。</summary>
        public static void SetStatusValue(int col, string value)
        {
            if (!_labels.TryGetValue(col, out var label) || label == null) return;
            _headers.TryGetValue(col, out var header);
            label.text = (header ?? "") + "\n" + value;
        }

        /// <summary>阶段显示格喂文（【己方主要阶段】式，BattleScreen.RefreshBars/RefreshView 同频调；
        /// myTurn=行方观侧（光圈配色）。变更即入待播队列：每次变更播一轮光圈内收（0.5s 蓝/红）
        /// +停顿（0.5s），**停顿毕才翻平贴文字**（不瞬切）串行节流——2026-10-26 改案
        ///（替 10-24「扩散即翻」）。未装台（_phaseLabel=null）只记账不播（Mount 落字口径不变）。</summary>
        public static void SetPhase(string text, bool myTurn)
        {
            if (string.IsNullOrEmpty(text) || _phaseText == text) return;
            _phaseText = text;
            if (_phaseLabel == null) return; // 未装台：只存文本（Mount 时 AttachFlatLabel 直填）
            _phasePending.Enqueue((text, myTurn));
            while (_phasePending.Count > PhaseQueueCap) _phasePending.Dequeue(); // 丢最旧
            PumpPhaseRing();
        }

        /// <summary>阶段对账兜底（本地局 RefreshBars/RefreshView 调——事件序喂数为正源，2026-10-26 定案）：
        /// 仅当待播队列空闲（无在播/积压）且文本有变时**直写**平贴字（不播光圈、不入队）——开局首刷种子
        ///（Wire 晚于回合1相位事件，收不到首组 PhaseStart）与教学换场/重铺等无相位事件的状态跳变自愈；
        /// 队列在播期跳过（永不打断/乱序事件序回放——空闲后下一拍对账自会追平）。myTurn 仅保持与
        /// SetPhase 同签名（直写无光圈，不用配色）。</summary>
        public static void ReconcilePhase(string text, bool myTurn)
        {
            if (string.IsNullOrEmpty(text) || _phaseText == text) return;
            if (_phaseRingBusy || _phasePending.Count > 0) return;
            _phaseText = text;
            if (_phaseLabel != null && _phaseLabel.text != text) _phaseLabel.text = text;
        }

        /// <summary>队头开播（串行闸 _phaseRingBusy）：取出队条目——光圈起播（收拢+停顿），
        /// **停顿毕才落字**；在播期后续条目排队（最小间隔=收拢+停顿 1.0s，播毕接链下一条）。</summary>
        private static void PumpPhaseRing()
        {
            if (_phaseRingBusy || _phasePending.Count == 0) return;
            var (text, myTurn) = _phasePending.Dequeue();
            _phaseRingBusy = true;
            PlayPhaseRingAsync(text, myTurn, _phaseEpoch).Forget();
        }

        /// <summary>光圈自外向内收拢一次（0.5s）→停顿（0.5s）→才翻阶段文字（2026-10-26 改案）：
        /// BattleTileRing 描边环网格复用（BattleTileFx 单源），己方蓝/对方红（BattleTileFx.UnitGlow*
        /// 同色族），起始=外扩口径（~2.6 格径）ease-in 收至格轮廓、additive 渐入，停顿期钉格轮廓驻留。
        /// 依赖缺件降级=不显示但节拍照走（Delay 1s——文字仍按演出节拍翻页）。
        /// epoch 守卫：Unmount/僵尸清算作废在播循环（不接链、不落字）。</summary>
        private static async Cysharp.Threading.Tasks.UniTaskVoid PlayPhaseRingAsync(string text, bool myTurn, int epoch)
        {
            var rend = EnsurePhaseRing();
            if (rend != null)
            {
                rend.enabled = true;
                if (_phaseMpb == null) _phaseMpb = new MaterialPropertyBlock();
                _phaseMpb.SetColor(RingColorId, myTurn ? BattleTileFx.UnitGlowColor : BattleTileFx.UnitGlowColorOpp);
                float t = 0f;
                while (t < PhaseRingSeconds && epoch == _phaseEpoch)
                {
                    float k = t / PhaseRingSeconds;
                    float ease = k * k; // ease-in：起收缓、末段收拢快（原外扩 ease-out 的镜像）
                    float s = Mathf.Lerp(PhaseRingExpand, 1f, ease);
                    rend.transform.localScale = new Vector3(s, 1f, s);
                    _phaseMpb.SetFloat(RingIntensityId, PhaseRingPeak * k); // additive 渐入（收拢到位=峰值）
                    rend.SetPropertyBlock(_phaseMpb);
                    await Cysharp.Threading.Tasks.UniTask.Yield();
                    t += Time.deltaTime;
                }
                // ---- 停顿段：光圈已收至本格轮廓，静置 PhaseRingHoldSeconds 后才翻阶段字 ----
                float hold = 0f;
                while (hold < PhaseRingHoldSeconds && epoch == _phaseEpoch)
                {
                    await Cysharp.Threading.Tasks.UniTask.Yield();
                    hold += Time.deltaTime;
                }
                if (rend != null) // fake-null：播途中拆台=件已随棋盘销毁，不再触碰
                {
                    rend.transform.localScale = Vector3.one; // 还原（复用件下次起播同基）
                    rend.enabled = false;
                }
            }
            else
            {
                await Cysharp.Threading.Tasks.UniTask.Delay((int)((PhaseRingSeconds + PhaseRingHoldSeconds) * 1000f));
            }
            if (epoch != _phaseEpoch) return; // 作废：不接链、不落字（防拆台后旧协程再拉起队列）
            if (_phaseLabel != null && _phaseLabel.text != text) _phaseLabel.text = text; // 停顿毕=阶段翻页
            _phaseRingBusy = false;
            PumpPhaseRing(); // 播毕接链（下一条最早此刻起播=最小间隔达标）
        }

        /// <summary>阶段光圈件惰性建（挂 hud-tile-phase 下；网格/材质=BattleTileFx 共享单源，
        /// 色与强度走 MPB 本件覆写；缺件/未认领=null 降级）。建=熄，起播时点亮。</summary>
        private static MeshRenderer EnsurePhaseRing()
        {
            if (_phaseRing != null) return _phaseRing; // fake-null 判死重建
            var mesh = BattleTileFx.SharedRingMesh;
            var mat = BattleTileFx.SharedRingMat;
            var tile = TileOf("hud-tile-phase");
            if (mesh == null || mat == null || tile == null) return null;
            var go = new GameObject("fx-phase-ring");
            go.transform.SetParent(tile, false);
            go.transform.localPosition = new Vector3(0f, mesh.bounds.max.y + 0.006f, 0f); // 顶面上 6mm（同地牌描边环口径）
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var rend = go.AddComponent<MeshRenderer>();
            rend.sharedMaterial = mat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
            rend.enabled = false;
            _phaseRing = rend;
            return rend;
        }

        /// <summary>格面平贴单行文字（2026-10-14 定案：同地牌计数条口径不朝相机——
        /// LookRotation(up, forward) 字头恒指世界 +z；中文整句 8 字用 2.6 号贴格宽，短语 3.5 号）。
        /// 挂认领格下随浮起动画同走；TMP 默认字体未配置时静默跳过；key=列号。</summary>
        private static TextMeshPro AttachFlatLabel(Transform tile, int col, string text, float fontSize)
        {
            if (tile == null || TMP_Settings.defaultFontAsset == null) return null;
            var go = new GameObject("hud-flat");
            go.transform.SetParent(tile, false);
            go.transform.localPosition = new Vector3(0f, 0.014f, 0f); // 贴格顶面（菲涅尔层之上，同地牌计数条）
            go.transform.localRotation = Quaternion.LookRotation(Vector3.down, Vector3.forward); // 平贴·字向正（2026-10-14 修：旧 up 版镜像反字，同 BattleFieldView.EnsureFlatCellLabel）
            go.transform.localScale = Vector3.one / BattleBoardGrid.TileScale; // P0 缩格补偿：文字不随格缩小（2026-10-23）
            var label = go.AddComponent<TextMeshPro>();
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.color = FlatTextColor;
            label.rectTransform.sizeDelta = new Vector2(2.6f, 1.2f);
            label.text = text;
            _labels[col] = label;
            return label;
        }

        /// <summary>状态格数字条（2026-10-14 定案：中线行文字全部平贴格面，同地牌计数条口径
        /// LookRotation(down, forward) 字向正——相机 (7,8,-4)/(50,0,0) 实测读向+x/字头+z/可读面朝上；
        /// 旧版朝相机已退役）。挂认领格下随浮起动画同走；TMP 默认字体未配置时静默跳过；
        /// key=列号，header=标题行——第二行数值由 SetStatusValue 改写。</summary>
        private static void AttachLabel(Transform tile, int col, string header)
        {
            if (tile == null || TMP_Settings.defaultFontAsset == null) return;
            var go = new GameObject("hud-flat");
            go.transform.SetParent(tile, false);
            go.transform.localPosition = new Vector3(0f, 0.014f, 0f); // 贴格顶面（菲涅尔层之上，同地牌计数条）
            go.transform.localRotation = Quaternion.LookRotation(Vector3.down, Vector3.forward); // 平贴·字向正
            go.transform.localScale = Vector3.one / BattleBoardGrid.TileScale; // P0 缩格补偿：文字不随格缩小（2026-10-23）
            var label = go.AddComponent<TextMeshPro>();
            label.fontSize = 3.5f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = FlatTextColor;
            label.rectTransform.sizeDelta = new Vector2(2.4f, 1.4f);
            label.text = header + "\n—";
            _labels[col] = label;
            _headers[col] = header;
        }
    }
}
