using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using GameBoard;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SynergyUI
{
    /// <summary>
    /// 开场段四·战场内卡组选择（2026-10-19 掷币演出挪战场定案）：候选卡组以卡堆形式摆在我方
    /// 单位区 z2 行空格（开局无单位无遮挡），点击卡堆选取——普通模式双选两轮（先我方红环、
    /// 再对方 AI 蓝环，语义与旧 PreMatch 双列表一致）；教学三变体=单轮+10 秒倒计时+AI 镜像。
    ///
    /// 卡堆形态复用卡组格正主（BattleZoneProps 共享 mesh/材质——卡背顶面+条纹侧面），名签=
    /// world billboard TMP；恒有「随机卡组」兜底堆（deckName=null，与旧缺省随机行同语义）。
    /// 点击路由：开场指针闸（BattleScreen.PollFieldPointer）按名前缀 deckpick- 转发到本类，
    /// 不进游戏点击分支；选择完成=卡堆逐个飞向卡组格并入（纯装饰收尾）。
    /// 纯表现件：发生在引擎开局前，不触 DeckSerializer/BuildDeck（数据组装归 BattleOpeningDirector）。
    /// </summary>
    public sealed class BattleDeckPickStage : IDisposable
    {
        /// <summary>候选条目：display=堆顶名签文案，deckName=null 表示「随机卡组」。</summary>
        public readonly struct Entry
        {
            public readonly string Display;
            public readonly string DeckName;
            public Entry(string display, string deckName) { Display = display; DeckName = deckName; }
        }

        /// <summary>教学三选卡组倒计时秒数（旧 PreMatchScreen.Teaching3PickSeconds 同值平移）。</summary>
        private const float LunzhuanPickSeconds = 10f;

        private const int RowZ = 2;          // 摆放行：P1 单位区近中线行（BoardLayout 单位行）
        private const int FirstX = 1;        // 起始列（x1..xN，两侧特殊格列不占）
        private const float StackLiftY = 0.02f;
        private const float RingY = 0.012f;  // 色环贴格面（菲涅尔 0.004/图标 0.008 之上）

        private static readonly Color SelfRingColor = new Color(0.92f, 0.28f, 0.24f); // 为我方选=红
        private static readonly Color AiRingColor = new Color(0.3f, 0.52f, 0.95f);    // 为对方选=蓝
        private static readonly Color HoverNameColor = new Color(1f, 0.9f, 0.4f);

        private readonly bool _lunzhuan;
        private readonly List<Entry> _entries;
        private readonly List<Transform> _piles = new List<Transform>();
        private readonly List<TMPro.TextMeshPro> _labels = new List<TMPro.TextMeshPro>();

        private Transform _root;             // deckpick-root（挂 MapRoot）
        private Transform _ring;             // 悬停高亮色环（随所指堆移动，相位决定红/蓝）
        private TMPro.TextMeshPro _prompt;   // 顶部提示签（board 上方 billboard）
        private bool _pickingSelf;           // true=轮到我方（红环），false=轮到对方 AI（蓝环）
        private bool _done;
        private int _hoverIdx = -1;

        public BattleDeckPickStage(bool lunzhuan, List<Entry> entries)
        {
            _lunzhuan = lunzhuan;
            _entries = entries ?? new List<Entry>();
        }

        /// <summary>就绪可交互（开场指针闸据此转发；Mount 前/完成后恒 false）。</summary>
        public bool Active => !_done && _root != null;

        /// <summary>建堆（BattleBoardGrid 就绪后调；依赖缺失降级=零堆——PickAsync 直接随机兜底完成）。</summary>
        public void Mount()
        {
            if (!BattleBoardGrid.Ready || _entries.Count == 0) return;
            _root = new GameObject("deckpick-root").transform;
            if (BattleStageDirector.MapRoot != null) _root.SetParent(BattleStageDirector.MapRoot, true);

            int count = Mathf.Min(_entries.Count, 6); // z2 行 x1..x6 六格上限
            for (int i = 0; i < count; i++)
            {
                var entry = _entries[i];
                var cell = BattleBoardGrid.WorldPositionOf(FirstX + i, RowZ);
                var pile = new GameObject("deckpick-" + i);
                pile.transform.SetParent(_root, false);
                pile.transform.position = cell + Vector3.up * StackLiftY;
                pile.transform.rotation = Quaternion.identity;

                Mesh stackMesh;
                Material side, top;
                if (BattleZoneProps.TryGetStackAssets(out stackMesh, out side, out top))
                {
                    pile.AddComponent<MeshFilter>().sharedMesh = stackMesh;
                    var rend = pile.AddComponent<MeshRenderer>();
                    rend.sharedMaterials = new[] { side, top };
                    rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    var col = pile.AddComponent<MeshCollider>();
                    col.sharedMesh = stackMesh; // 点击路由：开场闸按名 deckpick-{i} 分发
                }

                // 视觉厚度：真实卡组按张数（8..30 钳制），随机堆=15 张观感
                int cards = entry.DeckName != null ? CountOf(entry.DeckName) : 15;
                pile.transform.localScale = new Vector3(1f, Mathf.Clamp(cards, 8, 30) * 0.02f, 1f);

                var label = BattleFieldView.CreateWorldLabel(pile.transform, 0.55f + pile.transform.localScale.y);
                if (label != null)
                {
                    label.fontSize = 4.5f;
                    label.text = entry.Display;
                }
                _piles.Add(pile.transform);
                _labels.Add(label);
            }

            _ring = BuildRing();
            var center = BattleBoardGrid.Root.TransformPoint(BattleBoardGrid.LocalBounds().center);
            _prompt = BattleFieldView.CreateWorldLabel(_root, 0f);
            if (_prompt != null)
            {
                _prompt.transform.position = center + Vector3.up * 2.6f;
                _prompt.fontSize = 5f;
            }
        }

        /// <summary>选卡组主循环：双选两轮（普通）/ 单轮+倒计时（教学三，超时随机选定）。
        /// 返回 (我方卡组名, 对方卡组名)——null=随机兜底。收尾飞堆并入卡组格（纯装饰）。</summary>
        public async UniTask<(string myName, string aiName)> PickAsync(Func<bool> dead)
        {
            _pickingSelf = true;
            SetPrompt(_lunzhuan ? "教学三·10 秒内选定你的卡组（对方镜像）" : "为我方选择卡组（点击卡堆）");

            if (_piles.Count == 0)
            {
                // 依赖缺失/零候选降级：直接随机兜底（StartNewGame null 卡组同语义）
                Debug.LogWarning("[BattleDeckPick] 无候选卡堆——跳过选择，双方随机卡组");
                return (null, null);
            }

            if (_lunzhuan)
            {
                // 教学三倒计时（旧 PreMatchScreen.StartCountdownAsync 同语义）：归零未选=随机一张真实卡组
                float t0 = Time.realtimeSinceStartup;
                while (!dead() && Time.realtimeSinceStartup - t0 < LunzhuanPickSeconds)
                {
                    if (_done) break;
                    int remain = Mathf.CeilToInt(LunzhuanPickSeconds - (Time.realtimeSinceStartup - t0));
                    SetPrompt($"教学三·剩 {remain} 秒选定你的卡组（超时随机·对方镜像）");
                    await UniTask.Yield();
                }
                if (!_done && !dead())
                {
                    // 随机兜底：优先真实卡组任选其一（旧 PreMatchScreen 倒计时口径），无真实卡组=null 随机
                    var realDecks = _entries.Where(e => e.DeckName != null).Select(e => e.DeckName).ToList();
                    string fallback = realDecks.Count > 0
                        ? realDecks[UnityEngine.Random.Range(0, realDecks.Count)] : null;
                    Debug.Log("[BattleDeckPick] 倒计时归零——随机选定 " + (fallback ?? "随机卡组"));
                    Commit(fallback);
                }
            }

            while (!dead() && !_done)
                await UniTask.Yield();
            if (_prompt != null) _prompt.text = "";
            if (dead()) return (_myName, _aiName); // 拆屏：直接返回（调用方随屏销毁不再 Engage）

            await FlyToDeckAsync(dead);
            return (_myName, _aiName);
        }

        private string _myName, _aiName;

        /// <summary>开场指针闸转发：hitName=deckpick-{i} 悬停高亮；clicked=当前轮选取提交。</summary>
        public void HandlePointer(string hitName, bool clicked)
        {
            if (!Active) return;
            int idx = -1;
            if (hitName != null && hitName.StartsWith("deckpick-", StringComparison.Ordinal)
                && int.TryParse(hitName.Substring(9), out var v) && v >= 0 && v < _piles.Count)
                idx = v;

            if (idx != _hoverIdx)
            {
                if (_hoverIdx >= 0 && _hoverIdx < _labels.Count && _labels[_hoverIdx] != null)
                    _labels[_hoverIdx].color = Color.white;
                _hoverIdx = idx;
                if (idx >= 0)
                {
                    if (_labels[idx] != null) _labels[idx].color = HoverNameColor;
                    if (_ring != null)
                    {
                        _ring.gameObject.SetActive(true);
                        _ring.position = _piles[idx].position - Vector3.up * StackLiftY + Vector3.up * RingY;
                    }
                }
                else if (_ring != null) _ring.gameObject.SetActive(false);
            }
            if (clicked && idx >= 0) Commit(_entries[idx].DeckName);
        }

        /// <summary>提交当前轮选择：普通模式两轮（先我方后对方），教学三=单轮+镜像即完成。</summary>
        private void Commit(string deckName)
        {
            if (_done) return;
            if (_pickingSelf)
            {
                _myName = deckName;
                Debug.Log("[BattleDeckPick] 我方卡组=" + (deckName ?? "随机卡组"));
                if (_lunzhuan)
                {
                    _aiName = deckName; // 镜像（幻卡清算前对手用的就是你选的）
                    Complete();
                    return;
                }
                _pickingSelf = false;
                SetRingColor(AiRingColor);
                SetPrompt("为对方 AI 选择卡组（点击卡堆）");
                return;
            }
            _aiName = deckName;
            Debug.Log("[BattleDeckPick] 对方卡组=" + (deckName ?? "随机卡组"));
            Complete();
        }

        private void Complete()
        {
            _done = true;
            if (_ring != null) _ring.gameObject.SetActive(false);
            SetPrompt(_lunzhuan
                ? $"已选 {_myName ?? "随机卡组"}——开战！"
                : $"我方 {_myName ?? "随机卡组"} · 对方 {_aiName ?? "随机卡组"}——开战！");
        }

        // ======================================== 内部 ========================================

        /// <summary>卡组张数（视觉厚度用；读档失败按 30）。衍生物模板不计（额外区配套——
        /// 厚度按牌库正卡数，绿·慢速 32 条显示 30）。</summary>
        private static int CountOf(string deckName)
        {
            var data = DeckCatalog.Load(deckName); // 玩家存档 / 只读预组（2026-10-22）
            if (data?.cardIds == null) return 30;
            return data.cardIds.Count(id => CardCatalog.GetById(id)?.IsTokenTemplate != true);
        }

        private void SetPrompt(string text)
        {
            if (_prompt != null) _prompt.text = text;
        }

        private void SetRingColor(Color c)
        {
            if (_ring == null) return;
            var rend = _ring.GetComponent<Renderer>();
            if (rend != null) rend.sharedMaterial = SolidMaterial("PickRing", c);
        }

        /// <summary>悬停色环：扁圆柱片（Ø1.15×厚 0.012）贴格面，相位决定红/蓝；无 Collider。</summary>
        private Transform BuildRing()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            go.name = "pick-ring";
            go.transform.SetParent(_root, false);
            go.transform.localScale = new Vector3(1.15f, 0.006f, 1.15f);
            go.GetComponent<Renderer>().sharedMaterial = SolidMaterial("PickRing", SelfRingColor);
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.SetActive(false);
            return go.transform;
        }

        /// <summary>纯色材质（URP Lit 模板克隆+BaseColor——HeroPillarMaterial 同源口径）。</summary>
        private static Material SolidMaterial(string name, Color c)
        {
            var template = BattleZoneProps.LitTemplate;
            if (template == null) return null;
            var m = new Material(template) { name = name };
            m.SetColor("_BaseColor", c);
            return m;
        }

        /// <summary>收尾飞堆：候选堆逐个飞向卡组格并入（scale→0.05 渐隐观感），纯装饰不阻塞数据。</summary>
        private async UniTask FlyToDeckAsync(Func<bool> dead)
        {
            var deckCell = BattleBoardGrid.WorldPositionOf(BoardLayout.P1DeckX, BoardLayout.P1DeckZ);
            for (int i = 0; i < _piles.Count; i++)
            {
                var pile = _piles[i];
                if (pile == null) continue;
                float t0 = Time.realtimeSinceStartup;
                var from = pile.position;
                var fromScale = pile.localScale;
                while (!dead() && pile != null && Time.realtimeSinceStartup - t0 < 0.3f)
                {
                    float k = (Time.realtimeSinceStartup - t0) / 0.3f;
                    float e = 1f - (1f - k) * (1f - k);
                    pile.position = Vector3.Lerp(from, deckCell, e);
                    pile.localScale = Vector3.Lerp(fromScale, Vector3.one * 0.05f, e);
                    await UniTask.Yield();
                }
                if (pile != null) Object.Destroy(pile.gameObject);
            }
            _piles.Clear();
        }

        public void Dispose()
        {
            _done = true; // 在途选择立即失效（PickAsync 轮询 dead/_done 双保险）
            if (_root != null)
            {
                Object.Destroy(_root.gameObject);
                _root = null;
            }
        }
    }
}
