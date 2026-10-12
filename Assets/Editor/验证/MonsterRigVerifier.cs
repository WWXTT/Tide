using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MonsterRig;
using UnityEditor;
using UnityEngine;
using YGOImport;

namespace CardCore.Editor
{
    /// <summary>
    /// 怪兽程序动画验证器（2026-10-12 美术待办§二）。数据层证据口径（截图禁令）：
    /// 目录覆盖/骨骼存在性、姿态钳制、整周期确定性、rest 精确还原、反应包络收口、
    /// 头注视钳制与「无目标不写头」。
    ///
    /// 注意：方向对拍（头转向与目标同侧等镜像语义）依赖坐标转换修正落地后人工复核——
    /// 当前坐标转换脚本在修（另一会话），本验证器只做符号无关的幅度/钳制断言。
    /// 菜单 Tools/怪兽程序动画验证；可 -executeMethod；可 RunEmbedded 并入主验证统计。
    /// </summary>
    public static class MonsterRigVerifier
    {
        const string RigRoot = "Assets/Art/YGOMonstersRig";
        const string ProbePath = "Logs/MonsterRigProbe.txt";

        static System.Action<bool, string> _sink;
        static int _pass, _fail;

        [MenuItem("Tools/怪兽程序动画验证")]
        public static void Run()
        {
            _pass = 0; _fail = 0;
            _sink = (ok, label) =>
            {
                if (ok) { _pass++; Debug.Log($"[RigVerify] PASS — {label}"); }
                else
                {
                    _fail++;
                    Debug.LogError($"[RigVerify] FAIL — {label}");
                    try { File.AppendAllText(Path.Combine("Logs", "VerifyFailures.txt"), $"[RigVerify] {label}\n"); } catch { }
                }
            };
            RunAll();
            Debug.Log($"[RigVerify] 完成 — PASS={_pass} FAIL={_fail}");
            try { File.AppendAllText(Path.Combine("Logs", "VerifyStats.txt"),
                $"{DateTime.Now:HH:mm:ss} [RigVerify] PASS={_pass} FAIL={_fail}\n"); } catch { }
            if (_fail == 0) Debug.Log("✅ 怪兽程序动画验证全绿：目录覆盖/钳制/确定性/rest还原/包络收口/头注视");
            else Debug.LogError($"❌ 怪兽程序动画验证存在 {_fail} 项失败");
        }

        public static void RunEmbedded(System.Action<bool, string> sink)
        {
            _sink = sink;
            RunAll();
        }

        static void Check(bool ok, string label)
        {
            if (_sink == null) _sink = (o, l) => { if (o) Debug.Log($"[RigVerify] PASS — {l}"); else Debug.LogError($"[RigVerify] FAIL — {l}"); };
            _sink(ok, label);
        }

        static void RunAll()
        {
            try { Directory.CreateDirectory("Logs"); } catch { }
            float ampBackup = RigMotionTuning.GlobalAmplitude;
            RigMotionTuning.GlobalAmplitude = 1f;
            try
            {
                S1_Catalog();
                S2_Kinematics();
                S3_Impulses();
                S4_LookAt();
                S5_Probe();
                S6_PreviewSmoke();
                S7_Actions();
                S6b_PreviewMarkerFollow();
            }
            finally
            {
                RigMotionTuning.GlobalAmplitude = ampBackup;
            }
        }

        // ================= S6b 预览标记跟随（用户验收项：跟随标记目标） =================

        static void S6b_PreviewMarkerFollow()
        {
            var window = EditorWindow.GetWindow<MonsterRigPreviewWindow>(true, "怪兽动画预览");
            var report = window.TestMarkerFollow();
            try
            {
                bool anyHeaded = false, allFollow = true;
                foreach (var line in report)
                {
                    Debug.Log("[RigVerify] marker: " + line);
                    int idx = line.IndexOf("swing=");
                    if (idx < 0) continue;
                    anyHeaded = true;
                    float swing = float.Parse(line.Substring(idx + 6).TrimEnd('°'));
                    if (swing < 8f) allFollow = false;
                }
                Check(anyHeaded, "标记跟随：预览含 ≥1 个有头代表");
                Check(allFollow, "标记跟随：标记换侧后各代表头部反向跟转（swing≥8°）");
            }
            finally
            {
                if (window != null) window.Close();
            }
        }

        // ================= S7 动作 API（呼吸/行走/暂停/攻击/受击/倒地/复活） =================

        static void S7_Actions()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{RigRoot}/BlueEyesWhiteDragon/BlueEyesWhiteDragon.prefab");
            if (prefab == null) { Check(false, "S7: BlueEyesWhiteDragon prefab 缺失"); return; }
            var go = UnityEngine.Object.Instantiate(prefab);
            go.name = "BlueEyesWhiteDragon"; // 去 (Clone) 后缀——目录按根名寻址
            try
            {
                var anim = go.AddComponent<MonsterRigAnimator>();
                anim.EditorTick(0f, 0f);
                var d = anim.Driver;
                var entry = MonsterRigCatalog.Get("BlueEyesWhiteDragon");
                // 步态幅度测量骨：有翅生物=翅膀（移动方式是翅膀）；无翅按体型选步态骨
                string gaitBoneName = null;
                if (entry.wings.Count > 0)
                {
                    gaitBoneName = entry.wings[0].bones[0];
                }
                else switch (entry.moveType)
                {
                    case MonsterRigCatalog.MoveBiped:
                        if (entry.arms.Count > 0) gaitBoneName = entry.arms[0].bones[0];
                        break;
                    case MonsterRigCatalog.MoveQuadruped:
                    case MonsterRigCatalog.MoveSpider:
                        if (entry.legs.Count > 0) gaitBoneName = entry.legs[0].bones[0];
                        break;
                    default:
                        if (entry.bodyChain.Count > 0) gaitBoneName = entry.bodyChain[0];
                        else if (entry.tail.Count > 0) gaitBoneName = entry.tail[0];
                        break;
                }
                var gaitBone = string.IsNullOrEmpty(gaitBoneName) ? null : d.Bones.FirstOrDefault(b => b.T.name == gaitBoneName);

                // 呼吸 → 行走：步态幅度增强（GaitBlend 0→1）
                anim.Play(MonsterAction.Breath);
                Pump(anim, 90f);
                float idleAmp = gaitBone != null ? SampleMaxDelta(anim, gaitBone, 90f) : 0f;
                anim.Play(MonsterAction.Walk);
                Pump(anim, 120f); // 混合过渡完成
                float walkAmp = gaitBone != null ? SampleMaxDelta(anim, gaitBone, 90f) : 0f;
                Check(d.GaitBlend > 0.99f, $"行走混合到位（blend={d.GaitBlend:F2}）");
                if (gaitBone != null)
                    Check(walkAmp > idleAmp * 1.25f, $"行走步态强于呼吸（{gaitBoneName} {idleAmp:F1}°→{walkAmp:F1}°）");

                // 飞行上下起伏（有翅生物）：行走=振翅时根浮动与翅同步且幅度增大
                float walkBobY = SampleMaxAbsY(anim, d, 90f);
                anim.Play(MonsterAction.Breath);
                Pump(anim, 90f);
                float idleBobY = SampleMaxAbsY(anim, d, 90f);
                if (entry.wings.Count > 0)
                    Check(walkBobY > idleBobY * 1.8f, $"飞行振翅上下起伏（Y浮动 {idleBobY:F3}→{walkBobY:F3}）");

                // 暂停：姿态冻结（逐位不变）
                anim.Play(MonsterAction.Walk);
                Pump(anim, 60f);
                var before = d.Bones.Select(b => b.T.localRotation).ToList();
                var beforePos = d.Bones.Select(b => b.T.localPosition).ToList();
                anim.Play(MonsterAction.Pause);
                Pump(anim, 45f);
                bool frozen = true;
                for (int i = 0; i < d.Bones.Count; i++)
                    if (Quaternion.Angle(before[i], d.Bones[i].T.localRotation) > 0.001f
                        || d.Bones[i].T.localPosition != beforePos[i]) { frozen = false; break; }
                Check(frozen, "暂停冻结：45 帧内全部骨骼姿态逐位不变");
                anim.Play(MonsterAction.Breath);
                Pump(anim, 10f);
                bool resumed = false;
                for (int i = 0; i < d.Bones.Count; i++)
                    if (Quaternion.Angle(before[i], d.Bones[i].T.localRotation) > 0.001f) { resumed = true; break; }
                Check(resumed, "恢复播放：暂停解除后姿态继续演化");

                // 攻击：前扑偏移出现且收口回 rest 水平位
                var rootTr = d.Bones[0].T;
                Vector3 restPos = d.Bones[0].RestLocalPos;
                anim.Play(MonsterAction.Attack);
                float maxPush = 0f;
                for (int i = 0; i < 60; i++)
                {
                    anim.EditorTick(0.1f + i / 60f, 1f / 60f);
                    Vector3 off = rootTr.localPosition - restPos;
                    maxPush = Mathf.Max(maxPush, new Vector2(off.x, off.z).magnitude);
                }
                Pump(anim, 60f);
                Vector2 endOff = new Vector2(rootTr.localPosition.x - restPos.x, rootTr.localPosition.z - restPos.z);
                Check(maxPush > 0.1f, $"攻击有前扑（max={maxPush:F2}）");
                Check(endOff.magnitude < 0.005f, $"攻击收口回水平位（{endOff.magnitude:F3}）");

                // 受击（经 Play API）
                anim.Play(MonsterAction.Hit);
                float minY = float.MaxValue;
                for (int i = 0; i < 60; i++)
                {
                    anim.EditorTick(1f + i / 60f, 1f / 60f);
                    minY = Mathf.Min(minY, rootTr.localScale.y);
                }
                Check(minY < 0.95f, $"Play(Hit) 压扁生效（minY={minY:F2}）");

                // 倒地：平铺到根下 + 散落展开 + 落地静止
                var parentsBefore = d.Bones.ToDictionary(b => b.T, b => b.T.parent);
                float spreadBefore = XZSpread(d);
                anim.Play(MonsterAction.FallDown);
                Pump(anim, 360f);
                bool flat = d.Bones.Where(b => b.T != go.transform).All(b => b.T.parent == go.transform);
                float spreadAfter = XZSpread(d);
                bool aboveGround = d.Bones.All(b => b.T.position.y >= go.transform.position.y - 0.6f);
                Check(d.Fallen, "倒地状态置位");
                Check(flat, "倒地后部件全部平铺到根下（无父子关系）");
                Check(spreadAfter > spreadBefore * 1.5f, $"部件随机散落展开（{spreadBefore:F2}→{spreadAfter:F2}）");
                Check(aboveGround, "散落部件未穿地");
                var settledPose = d.Bones.Select(b => b.T.position).ToList();
                Pump(anim, 30f);
                bool still = true;
                for (int i = 0; i < settledPose.Count; i++)
                    if ((settledPose[i] - d.Bones[i].T.position).magnitude > 0.0005f) { still = false; break; }
                Check(still, "散落静止后保持不动（不滑移）");

                // 复活：层级恢复 + 姿态回到 rest 轨道（复活完成后动画立即恢复，
                // 故容差=模块幅度内；层级须逐位恢复）
                anim.Revive();
                Pump(anim, 90f);
                bool parentsRestored = d.Bones.All(b => b.T.parent == parentsBefore[b.T]);
                bool poseOnRestTrack = d.Bones.All(b =>
                    Quaternion.Angle(b.T.localRotation, b.RestLocal) < 15f
                    && (b.T.localPosition - b.RestLocalPos).magnitude < 0.1f);
                Check(parentsRestored, "复活后原父子层级逐位恢复");
                Check(poseOnRestTrack, "复活补间收口回 rest 轨道（层级内、模块幅度容差）");
                anim.EditorTick(2f, 1f / 60f);
                Check(true, "复活后可继续动画（无异常）");

                // 注视供应器跨驱动重建存活（2026-10-12 修复项①：SetLookTarget 在
                // 驱动未构建/Recapture 重建时曾静默丢失→头永远不转）
                Vector3 lookSide = go.transform.position + Vector3.right * 8f;
                anim.SetLookTarget(() => lookSide);
                anim.Recapture();
                d = anim.Driver; // 重建后的新驱动
                Pump(anim, 150f);
                var headBone2 = d.Bones.FirstOrDefault(b => b.T.name == entry.head);
                float lookDelta = headBone2 != null
                    ? Quaternion.Angle(headBone2.T.localRotation, headBone2.RestLocal) : 0f;
                Check(lookDelta > 5f, $"注视供应器跨 Recapture 存活（头Δ={lookDelta:F1}°）");
                anim.SetLookTarget(null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        static void Pump(MonsterRigAnimator anim, float seconds)
        {
            int frames = (int)Mathf.Ceil(seconds * 60f);
            for (int i = 0; i < frames; i++) anim.EditorTick(100f + i / 60f, 1f / 60f);
        }

        static float SampleMaxDelta(MonsterRigAnimator anim, BoneRef bone, float seconds)
        {
            float max = 0f;
            int frames = (int)Mathf.Ceil(seconds * 60f);
            for (int i = 0; i < frames; i++)
            {
                anim.EditorTick(200f + i / 60f, 1f / 60f);
                max = Mathf.Max(max, Quaternion.Angle(bone.T.localRotation, bone.RestLocal));
            }
            return max;
        }

        static float SampleMaxAbsY(MonsterRigAnimator anim, RigDriver d, float seconds)
        {
            float max = 0f;
            int frames = (int)Mathf.Ceil(seconds * 60f);
            for (int i = 0; i < frames; i++)
            {
                anim.EditorTick(300f + i / 60f, 1f / 60f);
                max = Mathf.Max(max, Mathf.Abs(d.Bones[0].T.localPosition.y - d.Bones[0].RestLocalPos.y));
            }
            return max;
        }

        static float XZSpread(RigDriver d)
        {
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var b in d.Bones)
            {
                var p = b.T.position;
                minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
                minZ = Mathf.Min(minZ, p.z); maxZ = Mathf.Max(maxZ, p.z);
            }
            return Mathf.Max(maxX - minX, maxZ - minZ);
        }

        // ================= S6 预览窗口冒烟 =================

        static void S6_PreviewSmoke()
        {
            var window = EditorWindow.GetWindow<MonsterRigPreviewWindow>(true, "怪兽动画预览");
            var report = window.TestSpawnAndTick(120);
            try
            {
                Check(report.Count >= 5, $"预览冒烟：代表 ≥5（实际 {report.Count}）");
                bool allMoved = true, allClean = true;
                foreach (var line in report)
                {
                    Debug.Log("[RigVerify] preview: " + line);
                    if (line.EndsWith("driver=null") || line.EndsWith("moved=0")) allMoved = false;
                }
                // 窗口 Close 后预览根应已销毁
                if (GameObject.Find("MonsterRigPreviewRoot_勿存场景") != null) allClean = false;
                Check(allMoved, "预览冒烟：各代表均有骨骼在动（moved>0）");
                Check(allClean, "预览冒烟：窗口关闭后预览根已销毁");
            }
            finally
            {
                if (window != null) window.Close();
            }
        }

        // ================= S1 目录静态检查 =================

        static void S1_Catalog()
        {
            MonsterRigCatalog.Reload();
            var catalog = MonsterRigCatalog.All;
            int prefabCount = Directory.GetFiles(RigRoot, "*.prefab", SearchOption.AllDirectories).Length;
            Check(catalog.Count == prefabCount, $"目录覆盖：条目 {catalog.Count} == prefab {prefabCount}");
            Check(string.IsNullOrEmpty(MonsterRigCatalog.LoadError), "目录加载无错误：" + MonsterRigCatalog.LoadError);

            var validTypes = new HashSet<string>
            {
                MonsterRigCatalog.MoveSlither, MonsterRigCatalog.MoveBiped, MonsterRigCatalog.MoveQuadruped,
                MonsterRigCatalog.MoveSpider, MonsterRigCatalog.MoveFlight, MonsterRigCatalog.MoveFloat,
            };

            int badType = 0, missingBones = 0, badChain = 0, checkedEntries = 0;
            foreach (var kv in catalog)
            {
                var e = kv.Value;
                if (!validTypes.Contains(e.moveType)) badType++;

                // 骨骼存在性（直接走 prefab 资产，不实例化）
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{RigRoot}/{e.name}/{e.name}.prefab");
                if (prefab == null) { missingBones++; continue; }
                var names = new HashSet<string>(prefab.GetComponentsInChildren<Transform>(true).Select(t => t.name));
                bool all = (IsSetOrEmpty(names, e.head) & IsSetOrEmpty(names, e.jaw) & IsSetOrEmpty(names, e.abdomen)
                    & IsSetOrEmpty(names, e.bodyMain) & ListAllIn(names, e.neck) & ListAllIn(names, e.tail)
                    & ListAllIn(names, e.bodyChain)
                    & e.legs.Concat(e.arms).Concat(e.soft).SelectMany(l => l.bones).All(b => names.Contains(b))
                    & e.wings.SelectMany(w => w.bones).All(b => names.Contains(b)));
                if (!all) missingBones++;
                checkedEntries++;

                // 链序：尾/颈尾号非降且无重名
                if (!ChainMonotone(e.tail) || !ChainMonotone(e.neck)) badChain++;
            }
            Check(badType == 0, $"moveType 全部合法（异常 {badType}）");
            Check(missingBones == 0, $"骨骼存在性：{checkedEntries} 条全命中（缺失 {missingBones}）");
            Check(badChain == 0, $"链序（tail/neck 尾号非降、无重名，异常 {badChain}）");

            var byType = catalog.Values.GroupBy(e => e.moveType)
                .ToDictionary(g => g.Key, g => g.Count());
            Debug.Log("[RigVerify] 体型分布：" + string.Join(", ", byType.OrderBy(k => k.Key).Select(k => $"{k.Key}={k.Value}")));
        }

        static bool IsSetOrEmpty(HashSet<string> names, string bone)
            => string.IsNullOrEmpty(bone) || names.Contains(bone);

        static bool ListAllIn(HashSet<string> names, List<string> list)
            => list == null || list.All(b => names.Contains(b));

        static bool ChainMonotone(List<string> chain)
        {
            if (chain == null || chain.Count < 2) return true;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            int last = int.MinValue;
            foreach (var b in chain)
            {
                if (!seen.Add(b)) return false;
                int digitStart = b.Length;
                while (digitStart > 0 && b[digitStart - 1] >= '0' && b[digitStart - 1] <= '9') digitStart--;
                int idx = 0;
                if (digitStart < b.Length) int.TryParse(b.Substring(digitStart), out idx);
                if (idx < last) return false;
                last = idx;
            }
            return true;
        }

        // ================= S2 运动学（实例化代表） =================

        static List<GameObject> SpawnRepresentatives(out List<RigDriver> drivers)
        {
            drivers = new List<RigDriver>();
            var gos = new List<GameObject>();
            var catalog = MonsterRigCatalog.All;
            var wanted = new List<string>();
            var firstByType = catalog.Keys.OrderBy(k => k, StringComparer.Ordinal)
                .GroupBy(k => catalog[k].moveType)
                .ToDictionary(g => g.Key, g => g.First());
            foreach (var t in MonsterRigCatalog.AllMoves)
            {
                string n; if (firstByType.TryGetValue(t, out n)) wanted.Add(n);
            }
            if (catalog.ContainsKey("BlueEyesWhiteDragon")) wanted.Add("BlueEyesWhiteDragon");

            foreach (var name in wanted.Distinct())
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{RigRoot}/{name}/{name}.prefab");
                if (prefab == null) continue;
                var go = UnityEngine.Object.Instantiate(prefab);
                go.name = name;
                gos.Add(go);
                drivers.Add(RigDriver.Build(go.transform));
            }
            return gos;
        }

        static void S2_Kinematics()
        {
            List<RigDriver> drivers;
            var gos = SpawnRepresentatives(out drivers);
            try
            {
                Check(drivers.Count >= 5, $"代表实例 ≥5（实际 {drivers.Count}，覆盖各体型+蓝眼）");
                var rng = new System.Random(20261012);

                foreach (var d in drivers)
                {
                    string tag = d.Name;
                    float maxCap = 0f;
                    foreach (var m in d.Modules) maxCap = Mathf.Max(maxCap, m.MaxAngularDeltaDeg);
                    Check(d.Modules.Count >= 2, $"{tag}: 模块 ≥2（实际 {d.Modules.Count}）");

                    // 钳制 + 有动作（非全静止）
                    int movedSamples = 0;
                    bool clampOk = true;
                    for (int s = 0; s < 50; s++)
                    {
                        float t = (float)(rng.NextDouble() * 100.0);
                        d.Tick(t, 1f / 60f);
                        int moved = 0;
                        foreach (var b in d.Bones)
                        {
                            float ang = Quaternion.Angle(b.T.localRotation, b.RestLocal);
                            if (ang > maxCap + 1.5f) clampOk = false;
                            if (ang > 0.5f
                                || (b.T.localPosition - b.RestLocalPos).magnitude > 0.01f) moved++; // 根浮动算动作
                        }
                        if (moved > 0) movedSamples++;
                        var rootTr = d.Bones[0].T;
                        if (rootTr.localScale != d.Bones[0].RestLocalScale
                            && (rootTr.localScale - d.Bones[0].RestLocalScale).magnitude > 0.01f) clampOk = false;
                    }
                    Check(clampOk, $"{tag}: 任意 t 骨骼偏移 ≤ 模块上限+1.5°、根缩放不变");
                    Check(movedSamples >= 45, $"{tag}: 采样大部分有动作（{movedSamples}/50）");

                    // 整周期确定性（无目标/无反应=纯 t 函数）
                    var snapshot = new List<Quaternion>();
                    float t0 = 37.2f;
                    d.Tick(t0, 1f / 60f);
                    foreach (var b in d.Bones) snapshot.Add(b.T.localRotation);
                    d.Tick(t0 + RigMotionTuning.BasePeriod, 1f / 60f);
                    bool periodic = true;
                    for (int i = 0; i < d.Bones.Count; i++)
                        if (Quaternion.Angle(snapshot[i], d.Bones[i].T.localRotation) > 0.001f) { periodic = false; break; }
                    Check(periodic, $"{tag}: t 与 t+BasePeriod 姿态逐位一致（谐波公倍周期）");

                    // rest 精确还原
                    d.Tick(11.7f, 1f / 60f);
                    d.ApplyRestAll();
                    bool restored = true;
                    foreach (var b in d.Bones)
                        if (Quaternion.Angle(b.T.localRotation, b.RestLocal) > 0.0001f
                            || b.T.localPosition != b.RestLocalPos || b.T.localScale != b.RestLocalScale)
                        { restored = false; break; }
                    Check(restored, $"{tag}: ApplyRestAll 逐位还原（旋转/位置/缩放）");
                }
            }
            finally
            {
                foreach (var go in gos) UnityEngine.Object.DestroyImmediate(go);
            }
        }

        // ================= S3 反应包络 =================

        static void S3_Impulses()
        {
            List<RigDriver> drivers;
            var gos = SpawnRepresentatives(out drivers);
            try
            {
                var d = drivers.FirstOrDefault(x => x.Name == "BlueEyesWhiteDragon") ?? drivers[0];
                string tag = d.Name;

                // 召唤起落（2026-10-12 用户规格二修）：原点上方升 2 单位，0.3s 速升+0.6s 缓落，
                // 单起落无回弹（不下冲）+「从小到大」缩放过渡
                d.ApplyRestAll();
                d.PlaySummonPop();
                float maxY = 0f, minScale = float.MaxValue;
                bool noDip = true;
                for (int i = 0; i < 90; i++)
                {
                    d.Tick(i / 60f, 1f / 60f);
                    float offY = d.Bones[0].T.localPosition.y - d.Bones[0].RestLocalPos.y;
                    maxY = Mathf.Max(maxY, offY);
                    if (offY < -0.03f) noDip = false; // 只允许呼吸浮动幅度内的负值
                    minScale = Mathf.Min(minScale, d.Bones[0].T.localScale.x);
                }
                d.Tick(1.5f, 1f / 60f);
                float endOffY = d.Bones[0].T.localPosition.y - d.Bones[0].RestLocalPos.y;
                Vector3 endScale = d.Bones[0].T.localScale;
                Check(maxY > 1.9f && maxY < 2.1f, $"{tag}: 召唤升起至原点上方≈2单位（max={maxY:F2}）");
                Check(noDip, $"{tag}: 落下无回弹下冲");
                Check(minScale < 0.3f, $"{tag}: 召唤从小到大（minScale={minScale:F2}）");
                Check((endScale - d.Bones[0].RestLocalScale).magnitude < 0.01f
                    && Mathf.Abs(endOffY) < 0.03f, $"{tag}: 召唤结束收口（缩放/位置回 rest）");

                // 受击压扁：y 压下、结束后回 1
                d.ApplyRestAll();
                d.PlayHit();
                float minY = float.MaxValue;
                for (int i = 0; i < 90; i++)
                {
                    d.Tick(i / 60f, 1f / 60f);
                    minY = Mathf.Min(minY, d.Bones[0].T.localScale.y);
                }
                d.Tick(1.5f, 1f / 60f);
                Vector3 endScale2 = d.Bones[0].T.localScale;
                Check(minY < 0.95f, $"{tag}: 受击有压扁帧（minY={minY:F2}）");
                Check((endScale2 - Vector3.one).magnitude < 0.01f, $"{tag}: 受击结束缩放收口 1（{endScale2}）");

                // 反应只碰根：同 t 对拍，非根骨骼不受影响
                d.ApplyRestAll();
                d.Tick(5f, 1f / 60f);
                var bonesSnapshot = d.Bones.Skip(1).Select(b => b.T.localRotation).ToList();
                d.ApplyRestAll();
                d.PlayHit();
                d.PlaySummonPop();
                d.Tick(5f, 1f / 60f);
                bool onlyRoot = true;
                var rest = d.Bones.Skip(1).ToList();
                for (int i = 0; i < rest.Count; i++)
                    if (Quaternion.Angle(bonesSnapshot[i], rest[i].T.localRotation) > 0.001f) { onlyRoot = false; break; }
                Check(onlyRoot, $"{tag}: 反应包络只影响根，非根骨骼同 t 不变");
            }
            finally
            {
                foreach (var go in gos) UnityEngine.Object.DestroyImmediate(go);
            }
        }

        // ================= S4 头注视 =================

        static void S4_LookAt()
        {
            List<RigDriver> drivers;
            var gos = SpawnRepresentatives(out drivers);
            try
            {
                var d = drivers.FirstOrDefault(x => x.Name == "BlueEyesWhiteDragon")
                    ?? drivers.FirstOrDefault(x => x.HasHead) ?? drivers[0];
                string tag = d.Name;
                if (!d.HasHead)
                {
                    Check(true, $"{tag}: 无头模型（跳过头注视断言组）");
                    return;
                }

                var entry = MonsterRigCatalog.Get(d.Name);
                var headTr = d.Bones.FirstOrDefault(b => b.T.name == entry.head);
                if (headTr == null) { Check(false, $"{tag}: 头骨 {entry.head} 未解析"); return; }
                var restRot = headTr.RestLocal;

                // 侧方目标 10m：应转动且被钳制
                Vector3 side = d.Bones[0].T.position + Vector3.right * 10f;
                d.SetLookTarget(() => side);
                d.ApplyRestAll();
                for (int i = 0; i < 240; i++) d.Tick(i / 60f, 1f / 60f);
                float sideDelta = Quaternion.Angle(headTr.T.localRotation, restRot);
                float cap = 0f;
                foreach (var m in d.Modules) cap = Mathf.Max(cap, m.MaxAngularDeltaDeg);
                Check(sideDelta > 5f, $"{tag}: 侧方目标头有转动（Δ={sideDelta:F1}°）");
                Check(sideDelta <= cap + 1.5f, $"{tag}: 侧方目标头偏移被钳制（Δ={sideDelta:F1}° ≤ {cap:F1}°+1.5）");

                // 目标移动追踪：目标换到另一侧，头应反向转（左右两姿态互比≈2×钳制份额；
                // 对 rest 的夹角是无符号的，测不出方向）
                Vector3 otherSide = d.Bones[0].T.position - Vector3.right * 10f;
                Quaternion poseAtRight = headTr.T.localRotation;
                d.SetLookTarget(() => otherSide);
                for (int i = 0; i < 240; i++) d.Tick(i / 60f + 5f, 1f / 60f);
                Quaternion poseAtLeft = headTr.T.localRotation;
                float swing = Quaternion.Angle(poseAtRight, poseAtLeft);
                Check(swing > 16f, $"{tag}: 目标换侧后头反向跟转（左右姿态差 {swing:F1}°）");

                // 背后目标：同样不超过钳制上限（不出现甩头 180°）
                Vector3 behind = d.Bones[0].T.position - Vector3.forward * 10f;
                d.SetLookTarget(() => behind);
                d.ApplyRestAll();
                for (int i = 0; i < 240; i++) d.Tick(i / 60f, 1f / 60f);
                float backDelta = Quaternion.Angle(headTr.T.localRotation, restRot);
                Check(backDelta <= cap + 1.5f, $"{tag}: 背后目标不越钳制（Δ={backDelta:F1}°）");

                // 目标消失：平滑回 rest 且此后不再写头（默认不控头）
                d.SetLookTarget(null);
                for (int i = 0; i < 300; i++) d.Tick(i / 60f + 10f, 1f / 60f);
                float settled = Quaternion.Angle(headTr.T.localRotation, restRot);
                Check(settled < 0.01f, $"{tag}: 目标消失后头回到 rest（Δ={settled:F3}°）");
                var junk = Quaternion.Euler(33f, 21f, 7f) * restRot;
                headTr.T.localRotation = junk; // 外部扰动
                for (int i = 0; i < 30; i++) d.Tick(i / 60f + 20f, 1f / 60f);
                Check(Quaternion.Angle(junk, headTr.T.localRotation) < 0.0001f,
                    $"{tag}: 默认态不写头（外部扰动 30 帧后保持不变）");
                d.ApplyRestAll();
            }
            finally
            {
                foreach (var go in gos) UnityEngine.Object.DestroyImmediate(go);
            }
        }

        // ================= S5 探针留痕 =================

        static void S5_Probe()
        {
            List<RigDriver> drivers;
            var gos = SpawnRepresentatives(out drivers);
            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"== MonsterRig 动画探针 {DateTime.Now:HH:mm:ss}（240 帧 @60fps 采样） ==");
                foreach (var d in drivers)
                {
                    var entry = MonsterRigCatalog.Get(d.Name);
                    var head = d.Bones.FirstOrDefault(b => b.T.name == entry.head);
                    var tail0 = d.Bones.FirstOrDefault(b => b.T.name == (entry.tail.Count > 0 ? entry.tail[0] : null));
                    sb.AppendLine($"[{d.Name}] {MonsterRigCatalog.MoveLabel(d.MoveType)} bones={d.Bones.Count} modules={d.Modules.Count}"
                        + (head != null ? " head=√" : "") + (tail0 != null ? " tail=√" : ""));
                    for (int i = 0; i <= 240; i += 40)
                    {
                        d.Tick(i / 60f, 1f / 60f);
                        string hs = head != null ? Quaternion.Angle(head.T.localRotation, head.RestLocal).ToString("F1") : "-";
                        string ts = tail0 != null ? Quaternion.Angle(tail0.T.localRotation, tail0.RestLocal).ToString("F1") : "-";
                        sb.AppendLine($"  t={i / 60f,5:F1}s headΔ={hs}° tailΔ={ts}° rootY={d.Bones[0].T.localPosition.y:F3}");
                    }
                    d.ApplyRestAll();
                }
                File.WriteAllText(ProbePath, sb.ToString());
                Check(true, "探针落盘 " + ProbePath);
            }
            finally
            {
                foreach (var go in gos) UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
