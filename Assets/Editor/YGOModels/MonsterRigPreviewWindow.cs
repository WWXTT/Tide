using System;
using System.Collections.Generic;
using System.Linq;
using MonsterRig;
using UnityEditor;
using UnityEngine;

namespace YGOImport
{
    /// <summary>
    /// 生物程序动画预览窗口（2026-10-12 美术待办§二）：edit mode 下用
    /// EditorApplication.update 驱动 MonsterRigAnimator（与游戏内同一套 RigDriver 纯核心）。
    /// 验收口径=数据层（层级/姿态读数），窗口仅供人眼调参；正式断言在 MonsterRigVerifier。
    ///
    /// 用法：Tools/YGO模型/5 动画预览 → 「一排体型代表」或手选模型名生成 → 播放。
    /// 注视三态：最近其他个体（模拟最近敌方单位）/ 拖动标记（模拟对方角色）/ 无（默认不控头）。
    /// 预览对象挂在专用根下并随窗口关闭销毁——勿保存进场景。
    /// </summary>
    public class MonsterRigPreviewWindow : EditorWindow
    {
        const string RigRoot = "Assets/Art/YGOMonstersRig";

        enum LookMode { NearestOther = 0, Marker = 1, None = 2 }

        class Entry
        {
            public GameObject Go;
            public MonsterRigAnimator Anim;
            public string Label;
            public string MoveSel;   // 当前行走方式下拉选择（含覆盖值）
        }

        readonly List<Entry> _entries = new List<Entry>();
        Transform _previewRoot;
        Transform _marker;
        bool _play = true;
        float _timeScale = 1f;
        float _amplitude = 1f;
        LookMode _lookMode = LookMode.NearestOther;
        string _manualName = "BlueEyesWhiteDragon";
        Vector2 _scroll;
        double _lastT;
        float _t;
        string _status = "";

        [MenuItem("Tools/YGO模型/5 动画预览")]
        public static void Open()
        {
            var w = GetWindow<MonsterRigPreviewWindow>("怪兽动画预览");
            w.minSize = new Vector2(360f, 420f);
            w.Show();
        }

        void OnEnable()
        {
            _lastT = EditorApplication.timeSinceStartup;
            EditorApplication.update += OnEditorUpdate;
        }

        void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            DestroyPreviewObjects();
            RigMotionTuning.GlobalAmplitude = 1f;
        }

        void OnDestroy() => DestroyPreviewObjects();

        void OnEditorUpdate()
        {
            if (Application.isPlaying) return;
            double now = EditorApplication.timeSinceStartup;
            float dt = Mathf.Min(0.1f, (float)(now - _lastT));
            _lastT = now;
            if (!_play) return;
            _t += dt * _timeScale;
            for (int i = 0; i < _entries.Count; i++)
                _entries[i].Anim.EditorTick(_t, dt * _timeScale);
            if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.Repaint();
        }

        void OnGUI()
        {
            EditorGUILayout.Space(4);
            _play = EditorGUILayout.Toggle("播放", _play);
            _timeScale = EditorGUILayout.Slider("时间倍率", _timeScale, 0f, 3f);
            _amplitude = EditorGUILayout.Slider("幅度", _amplitude, 0f, 2f);
            RigMotionTuning.GlobalAmplitude = _amplitude;

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("生成", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("一排体型代表")) SpawnRepresentatives();
                if (GUILayout.Button("清空")) ClearEntries();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                _manualName = EditorGUILayout.TextField(_manualName);
                if (GUILayout.Button("生成", GUILayout.Width(50))) Spawn(_manualName.Trim());
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("动作", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("呼吸")) ForEach(a => a.Play(MonsterAction.Breath));
                if (GUILayout.Button("行走")) ForEach(a => a.Play(MonsterAction.Walk));
                if (GUILayout.Button("暂停")) ForEach(a => a.Play(MonsterAction.Pause));
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("攻击")) ForEach(a => a.Play(MonsterAction.Attack));
                if (GUILayout.Button("受击")) ForEach(a => a.Play(MonsterAction.Hit));
                if (GUILayout.Button("倒地")) ForEach(a => a.Play(MonsterAction.FallDown));
                if (GUILayout.Button("复活")) ForEach(a => a.Revive());
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("召唤冒出")) ForEach(a => a.PlaySummonPop());
                if (GUILayout.Button("重置姿态")) ForEach(a => { a.Recapture(); a.EditorTick(_t, 0f); });
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("注视目标", EditorStyles.boldLabel);
            var newMode = (LookMode)GUILayout.Toolbar((int)_lookMode, new[] { "最近其他个体", "标记目标", "无" });
            if (newMode != _lookMode && newMode == LookMode.Marker && _marker == null)
                PlaceMarker(); // 切到标记模式自动放标记（此前缺标记=供应器恒 null，表现为头不跟随）
            _lookMode = newMode;
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("放置标记于原点前方")) PlaceMarker();
                if (_marker != null && GUILayout.Button("删除标记", GUILayout.Width(70))) DestroyMarker();
            }
            ApplyLookMode();

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField($"实例（{_entries.Count}）——下拉切行走方式，[存]写回目录(manual)", EditorStyles.boldLabel);
            if (!string.IsNullOrEmpty(_status)) EditorGUILayout.HelpBox(_status, MessageType.None);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            string[] moveLabels = System.Array.ConvertAll(MonsterRigCatalog.AllMoves, MonsterRigCatalog.MoveLabel);
            foreach (var e in _entries)
            {
                if (e.Go == null || e.Anim == null) continue;
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(e.Go.name, GUILayout.Width(170));
                    int sel = Mathf.Max(0, System.Array.IndexOf(MonsterRigCatalog.AllMoves, e.MoveSel));
                    int next = EditorGUILayout.Popup(sel, moveLabels, GUILayout.Width(90));
                    if (next != sel)
                    {
                        e.MoveSel = MonsterRigCatalog.AllMoves[next];
                        e.Anim.SetMoveType(e.MoveSel);
                        e.Label = e.Anim.DebugSummary;
                    }
                    if (GUILayout.Button("存", GUILayout.Width(34)))
                        SaveMoveToCatalog(e.Go.name, e.MoveSel);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        /// <summary>把当前行走方式写回目录条目并标 manual（重新生成目录时不被覆盖）。</summary>
        void SaveMoveToCatalog(string modelName, string moveType)
        {
            try
            {
                string projRoot = System.IO.Path.GetDirectoryName(Application.dataPath);
                string path = System.IO.Path.Combine(projRoot, "Assets/Configs/MonsterRigCatalog.json");
                var root = CardCore.TideJson.FromJson<MonsterRigCatalogRoot>(System.IO.File.ReadAllText(path));
                var hit = root.models.FirstOrDefault(m => m.name == modelName);
                if (hit == null) { _status = $"目录无条目：{modelName}"; return; }
                hit.moveType = moveType;
                hit.manual = true;
                System.IO.File.WriteAllText(path, CardCore.TideJson.ToJson(root, true));
                MonsterRigCatalog.Reload();
                _status = $"已写回：{modelName} → {MonsterRigCatalog.MoveLabel(moveType)}（manual）";
            }
            catch (System.Exception ex)
            {
                _status = "写回失败：" + ex.Message;
            }
        }

        // ================= 生成/清理 =================

        void SpawnRepresentatives()
        {
            // 每种行走方式各挑一个代表（标志性模型优先）；切行走方式可在实例行下拉试摆
            var byMove = new Dictionary<string, string>();
            foreach (var kv in MonsterRigCatalog.All)
            {
                if (!byMove.ContainsKey(kv.Value.moveType))
                    byMove[kv.Value.moveType] = kv.Key;
            }
            ClearEntries();
            string pick(string move, string prefer)
            {
                var e = MonsterRigCatalog.Get(prefer);
                if (e != null && e.moveType == move) return prefer;
                string name; return byMove.TryGetValue(move, out name) ? name : null;
            }
            TrySpawn(pick(MonsterRigCatalog.MoveFlight, "BlueEyesWhiteDragon"));
            TrySpawn(pick(MonsterRigCatalog.MoveSpider, null));
            TrySpawn(pick(MonsterRigCatalog.MoveSlither, null));
            TrySpawn(pick(MonsterRigCatalog.MoveQuadruped, null));
            TrySpawn(pick(MonsterRigCatalog.MoveBiped, "DarkMagician"));
            TrySpawn(pick(MonsterRigCatalog.MoveFloat, null));
            _status = "已生成各行走方式代表（实例行可切行走方式并写回目录）";
        }

        void Spawn(string name)
        {
            if (!TrySpawn(name)) _status = $"生成失败：{name}（目录或 prefab 缺失）";
        }

        bool TrySpawn(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (MonsterRigCatalog.Get(name) == null) return false;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{RigRoot}/{name}/{name}.prefab");
            if (prefab == null) return false;
            EnsureRoot();
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, _previewRoot);
            var entry = MonsterRigCatalog.Get(name);
            go.name = name;
            go.transform.localPosition = new Vector3(_entries.Count * 3f, 0f, 0f);
            go.transform.localRotation = Quaternion.Euler(0f, entry.yawOffsetDeg, 0f); // 面向 +Z（镜头方向）
            var anim = go.AddComponent<MonsterRigAnimator>();
            anim.EditorTick(0f, 0f); // edit mode 懒构建 + 立即捕获 rest
            _entries.Add(new Entry { Go = go, Anim = anim, Label = anim.DebugSummary, MoveSel = anim.MoveType });
            ApplyLookMode();
            return true;
        }

        void ClearEntries()
        {
            foreach (var e in _entries)
                if (e.Go != null) UnityEngine.Object.DestroyImmediate(e.Go);
            _entries.Clear();
        }

        void EnsureRoot()
        {
            if (_previewRoot == null)
            {
                var rootGo = new GameObject("MonsterRigPreviewRoot_勿存场景");
                rootGo.transform.position = Vector3.zero;
                _previewRoot = rootGo.transform;
            }
        }

        void DestroyPreviewObjects()
        {
            ClearEntries();
            if (_previewRoot != null) UnityEngine.Object.DestroyImmediate(_previewRoot.gameObject);
            _previewRoot = null;
            DestroyMarker();
        }

        // ================= 注视/反应 =================

        void ApplyLookMode()
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                var self = _entries[i];
                switch (_lookMode)
                {
                    case LookMode.NearestOther:
                        self.Anim.SetLookTarget(() =>
                        {
                            var others = _entries.Where(e => e != self && e.Go != null)
                                .Select(e => e.Go.transform.position).ToList();
                            return MonsterLookTarget.Nearest(self.Go.transform.position, others);
                        });
                        break;
                    case LookMode.Marker:
                        self.Anim.SetLookTarget(() => _marker != null ? _marker.position : (Vector3?)null);
                        break;
                    default:
                        self.Anim.SetLookTarget(null);
                        break;
                }
            }
        }

        void PlaceMarker()
        {
            if (_marker == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "注视目标标记_可拖动";
                UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
                _marker = go.transform;
                _marker.localScale = Vector3.one * 0.25f;
            }
            Vector3 center = _entries.Count > 0 ? _entries[0].Go.transform.position : Vector3.zero;
            _marker.position = center + Vector3.forward * 3f + Vector3.up * 0.5f;
            Selection.activeGameObject = _marker.gameObject;
        }

        void DestroyMarker()
        {
            if (_marker != null) UnityEngine.Object.DestroyImmediate(_marker.gameObject);
            _marker = null;
        }

        void ForEach(Action<MonsterRigAnimator> a)
        {
            for (int i = 0; i < _entries.Count; i++) a(_entries[i].Anim);
        }

        // ================= 验证器冒烟入口 =================

        /// <summary>程序化冒烟（MonsterRigVerifier S6 调用）：生成代表 → 泵 N 帧 →
        /// 返回各实例「有动作的骨骼数」（数据层证据，不依赖人眼）。调用后窗口自行关闭清理。</summary>
        public List<string> TestSpawnAndTick(int frames)
        {
            var report = new List<string>();
            SpawnRepresentatives();
            float t = 0f;
            for (int i = 0; i < frames; i++)
            {
                t += 1f / 60f;
                for (int j = 0; j < _entries.Count; j++) _entries[j].Anim.EditorTick(t, 1f / 60f);
            }
            foreach (var e in _entries)
            {
                var d = e.Anim.Driver;
                if (d == null) { report.Add($"{e.Go.name}:driver=null"); continue; }
                int moved = 0;
                foreach (var b in d.Bones)
                    if (Quaternion.Angle(b.T.localRotation, b.RestLocal) > 0.5f) moved++;
                report.Add($"{e.Go.name}:{MonsterRigCatalog.MoveLabel(d.MoveType)}/bones={d.Bones.Count}/moved={moved}");
            }
            Close(); // OnDisable → DestroyPreviewObjects
            return report;
        }

        /// <summary>程序化标记跟随测试（MonsterRigVerifier S6b）：生成代表 → 标记模式 →
        /// 逐个代表把标记放到其正右/正左各泵帧 → 报告头部左右姿态差（应显著&gt;8°）。
        /// 标记须相对各代表自身换侧（排位靠后的代表看公共标记左右位置方向几乎不变）。
        /// 窗口自行关闭。</summary>
        public List<string> TestMarkerFollow()
        {
            var report = new List<string>();
            SpawnRepresentatives();
            _lookMode = LookMode.Marker;
            if (_marker == null) PlaceMarker();
            ApplyLookMode();

            void PumpLocal(float seconds, float tStart)
            {
                int frames = (int)Mathf.Ceil(seconds * 60f);
                for (int i = 0; i < frames; i++)
                {
                    float t = tStart + i / 60f;
                    for (int j = 0; j < _entries.Count; j++) _entries[j].Anim.EditorTick(t, 1f / 60f);
                }
            }

            float clock = 0f;
            foreach (var e in _entries)
            {
                var entry = MonsterRigCatalog.Get(e.Go.name);
                if (entry == null || string.IsNullOrEmpty(entry.head)) continue;
                var head = e.Anim.Driver?.Bones.FirstOrDefault(b => b.T.name == entry.head);
                if (head == null) continue;

                Vector3 self = e.Go.transform.position;
                _marker.position = self + Vector3.right * 8f + Vector3.up * 1f;
                PumpLocal(2f, clock); clock += 2f;
                Quaternion poseRight = head.T.localRotation;

                _marker.position = self - Vector3.right * 8f + Vector3.up * 1f;
                PumpLocal(2f, clock); clock += 2f;
                float swing = Quaternion.Angle(poseRight, head.T.localRotation);
                report.Add($"{e.Go.name}:marker-follow swing={swing:F1}°");
            }
            Close();
            return report;
        }
    }
}
