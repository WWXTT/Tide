// BBModelCore.cs — .bbmodel（Blockbench 工程，JSON）解析 + 骨骼局部系数学核心。
// 纯 C#、零依赖：Unity 编辑器导入工具（YGOModelImporter）与外部干跑验证共用同一实现，
// 保证"验证过的解析"与"生成用的解析"是同一份代码。
//
// 格式要点（YGOMCModels 实测，format_version 4.5 / animated_entity_model）：
// - 立方体：from/to 两角点 + origin(轴心) + rotation(欧拉角，度，缺省=无)；
// - 六面 UV 已解析为像素矩形 [x1,y1,x2,y2]（允许 x2<x1 / y2<y1 编码镜像，禁止排序归一）；
// - 贴图内嵌 base64（textures[].source = "data:image/png;base64,..."）；
// - outliner 为嵌套组树（组 origin 为模型空间绝对轴心，rotation 键缺省=0）→ 骨骼；
// - idle.json = GeckoLib 动画格式（1.8.0）：恒定欧拉姿态（待机姿势），骨骼名与组名对应。
//
// 坐标约定：全部数学在 Blockbench 右手系（+X 东 / +Y 上 / +Z 南）内完成；
// 导入器最后一步才映射到 Unity 左手系（绕 Y 180°纯旋转：点/法线 (−x,y,−z)、四元数 (−x,y,−z,w)、
// 三角形整体反向补偿定向——详见 YGOModelImporter 头注释）。

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace YGOImport
{
    // ---------- 极简向量/四元数（BB 右手系） ----------
    public struct BBV3
    {
        public double X, Y, Z;
        public static BBV3 Of(double[] a) { return new BBV3 { X = a[0], Y = a[1], Z = a[2] }; }
        public static BBV3 Zero { get { return new BBV3(); } }
        public static BBV3 Sub(BBV3 a, BBV3 b) { return new BBV3 { X = a.X - b.X, Y = a.Y - b.Y, Z = a.Z - b.Z }; }
        public static BBV3 Add(BBV3 a, BBV3 b) { return new BBV3 { X = a.X + b.X, Y = a.Y + b.Y, Z = a.Z + b.Z }; }
        public double Len { get { return Math.Sqrt(X * X + Y * Y + Z * Z); } }
    }

    public struct BBQuat
    {
        public double X, Y, Z, W;
        public static BBQuat Identity { get { return new BBQuat { W = 1 }; } }
        public bool IsIdentity { get { return X == 0 && Y == 0 && Z == 0 && W == 1; } }
    }

    public static class BBMath
    {
        // Blockbench/THREE 欧拉序 'XYZ'：q = qx * qy * qz（逐元素内层先应用）
        public static BBQuat EulerXyzDeg(double rx, double ry, double rz)
        {
            if (rx == 0 && ry == 0 && rz == 0) return BBQuat.Identity;
            return Mul(AxisAngle(1, 0, 0, rx), Mul(AxisAngle(0, 1, 0, ry), AxisAngle(0, 0, 1, rz)));
        }

        public static BBQuat AxisAngle(double ax, double ay, double az, double deg)
        {
            double half = deg * Math.PI / 360.0, s = Math.Sin(half);
            return new BBQuat { X = ax * s, Y = ay * s, Z = az * s, W = Math.Cos(half) };
        }

        public static BBQuat Mul(BBQuat a, BBQuat b)
        {
            return new BBQuat
            {
                W = a.W * b.W - a.X * b.X - a.Y * b.Y - a.Z * b.Z,
                X = a.W * b.X + a.X * b.W + a.Y * b.Z - a.Z * b.Y,
                Y = a.W * b.Y - a.X * b.Z + a.Y * b.W + a.Z * b.X,
                Z = a.W * b.Z + a.X * b.Y - a.Y * b.X + a.Z * b.W,
            };
        }

        public static BBQuat Conj(BBQuat q) { return new BBQuat { X = -q.X, Y = -q.Y, Z = -q.Z, W = q.W }; }

        public static BBV3 Rotate(BBQuat q, BBV3 v)
        {
            // v + 2q × (q × v)
            double tx = 2 * (q.Y * v.Z - q.Z * v.Y), ty = 2 * (q.Z * v.X - q.X * v.Z), tz = 2 * (q.X * v.Y - q.Y * v.X);
            return new BBV3
            {
                X = v.X + q.W * tx + (q.Y * tz - q.Z * ty),
                Y = v.Y + q.W * ty + (q.Z * tx - q.X * tz),
                Z = v.Z + q.W * tz + (q.X * ty - q.Y * tx),
            };
        }
    }

    // ---------- 数据模型 ----------
    public class BBFace
    {
        public double[] Uv;      // [x1,y1,x2,y2] 像素；null=未指定（回落 box_uv 推算或整图）
        public int Texture = -1; // 纹理索引；-1=未指定
    }

    public class BBCube
    {
        public string Uuid, Name;
        public double[] From, To;
        public double[] Origin, Rotation; // 自身轴心与欧拉角（可能为 null）
        public int FlatAxis = -1;         // 零厚度轴（0=X/1=Y/2=Z；-1=正常立方体）→ 单面片处理
        public bool BoxUv;
        public double[] UvOffset;
        public Dictionary<string, BBFace> Faces = new Dictionary<string, BBFace>();
        public BBGroup OwnerGroup;              // 内层most 组；null=根级
        public List<BBGroup> ChainGroups;       // 内→外；根级为 null
    }

    public class BBGroup
    {
        public string Name;
        public string Uuid;       // outliner 组 UUID——内嵌动画 animators 的索引键（匿名组也有）
        public double[] Origin;    // 模型空间绝对轴心；null=解析期补（质心或零点）
        public double[] Rotation;  // 欧拉角(度)；null=无
        public BBGroup Parent;
        public List<BBGroup> Children = new List<BBGroup>();
        public List<BBCube> Cubes = new List<BBCube>();
        public bool NameWasNull, OriginWasNull;  // 源数据匿名组（约 16% 模型：组只有 uuid/children，idle 骨骼名已断链）
        public bool PivotFromCentroid;           // 轴心为质心推导（非作者标注）

        // Prepare() 填充：
        public BBQuat OwnBindQuat, OwnIdleQuat;     // 自身绑定/待机旋转
        public BBQuat WorldQuat;                    // 绑定世界旋转（含祖先，含自身）
        public BBV3 WorldPivot;                     // 绑定世界轴心
        public BBV3 LocalBindPosV, LocalIdlePosV;   // 骨骼局部位置（绑定/待机，父骨骼局部系）
        public BBQuat LocalBindQuat, LocalIdleQuat; // 骨骼局部旋转（= 自身旋转）
        public int Depth;                           // 根级组=1
    }

    public class BBTexture
    {
        public string Name;
        public byte[] Png;         // 内嵌 PNG；null=无
        public int Width, Height;  // PNG IHDR 实际尺寸
    }

    // 待机姿态（内嵌动画或 idle.json）。内嵌动画为权威口径：数值=Blockbench 预览（截图）原始值，
    // 且按组 UUID 索引（匿名组可达）；idle.json 为 bedrock 导出物——部分批次 X/Y 被取反，仅作兜底。
    public class BBIdlePose
    {
        public string AnimName;
        public bool FromEmbedded;                                      // 来源=bbmodel 内嵌动画（UUID 索引）
        public bool HasKeyframes;
        public List<string> AllAnimNames = new List<string>();
        public Dictionary<string, double[]> Rotations = new Dictionary<string, double[]>();  // 骨骼名→欧拉(度)
        public Dictionary<string, double[]> Positions = new Dictionary<string, double[]>();  // 骨骼名→位移(BB单位)
        public Dictionary<string, double[]> RotByUuid = new Dictionary<string, double[]>();  // 组UUID→欧拉（内嵌）
        public Dictionary<string, double[]> PosByUuid = new Dictionary<string, double[]>();  // 组UUID→位移（内嵌）
        public List<string> UnknownBones = new List<string>();      // 名字/UUID 对不上组的条目
        public string Error;
    }

    public class BBModel
    {
        public string Name, Format;
        public int ResolutionW, ResolutionH;
        public List<BBCube> Cubes = new List<BBCube>();
        public List<BBGroup> Groups = new List<BBGroup>(); // DFS 序全量组
        public List<BBTexture> Textures = new List<BBTexture>();
        public List<string> AnimationNames = new List<string>();
        public List<string> Warnings = new List<string>();
        public int NonCubeCount;
        public BBIdlePose Idle;          // idle.json 兜底（LoadIdle 填充；null=无文件）
        public BBIdlePose IdleEmbedded;  // bbmodel 内嵌动画（权威；Parse 填充）
        public BBIdlePose ActiveIdlePose; // Prepare 选定的姿态源
        public int AnonymousGroups; // 匿名组数量（内嵌动画按 UUID 定位，不受影响）
        public bool IdleApplied;    // Prepare 填充：待机姿态是否实际应用
        public bool Prepared;
    }

    public static class BBModelParser
    {
        public static readonly string[] FaceNames = { "north", "east", "south", "west", "up", "down" };

        public static BBModel ParseFile(string path)
        {
            return Parse(File.ReadAllText(path), Path.GetFileNameWithoutExtension(path));
        }

        public static BBModel Parse(string json, string fallbackName)
        {
            var root = BBJson.Obj(BBJson.Parse(json));
            var m = new BBModel();
            m.Name = BBJson.GetStr(root, "name");
            if (string.IsNullOrEmpty(m.Name)) m.Name = fallbackName;

            var meta = BBJson.GetObj(root, "meta");
            if (meta != null) m.Format = BBJson.GetStr(meta, "model_format");
            var res = BBJson.GetObj(root, "resolution");
            if (res != null)
            {
                m.ResolutionW = (int)BBJson.GetNum(res, "width");
                m.ResolutionH = (int)BBJson.GetNum(res, "height");
            }

            var texs = BBJson.GetArr(root, "textures");
            if (texs != null) foreach (var o in texs)
                {
                    var t = BBJson.Obj(o);
                    if (t == null) continue;
                    var bt = new BBTexture { Name = BBJson.GetStr(t, "name") };
                    var src = BBJson.GetStr(t, "source");
                    if (!string.IsNullOrEmpty(src))
                    {
                        int comma = src.IndexOf(',');
                        try { bt.Png = Convert.FromBase64String(comma >= 0 ? src.Substring(comma + 1) : src); }
                        catch (FormatException) { /* 留空，调用侧按无纹理处理 */ }
                    }
                    ReadPngSize(bt.Png, out bt.Width, out bt.Height);
                    m.Textures.Add(bt);
                }

            var byUuid = new Dictionary<string, BBCube>();
            var els = BBJson.GetArr(root, "elements");
            if (els != null) foreach (var o in els)
                {
                    var e = BBJson.Obj(o);
                    if (e == null) continue;
                    string type = BBJson.GetStr(e, "type");
                    if (type != null && type != "cube") { m.NonCubeCount++; continue; }
                    var c = new BBCube
                    {
                        Uuid = BBJson.GetStr(e, "uuid"),
                        Name = BBJson.GetStr(e, "name"),
                        From = BBJson.VecOrNull(e, "from"),
                        To = BBJson.VecOrNull(e, "to"),
                        Origin = BBJson.VecOrNull(e, "origin"),
                        Rotation = BBJson.VecOrNull(e, "rotation"),
                        BoxUv = BBJson.GetBool(e, "box_uv", false) || BBJson.Get(e, "uv_offset") != null,
                        UvOffset = BBJson.VecOrNull(e, "uv_offset"),
                    };
                    var faces = BBJson.GetObj(e, "faces");
                    if (faces != null) foreach (var kv in faces)
                        {
                            var f = BBJson.Obj(kv.Value);
                            if (f == null) continue;
                            var bf = new BBFace { Uv = BBJson.VecOrNull(f, "uv") };
                            var texRef = BBJson.Get(f, "texture");
                            if (texRef is double) bf.Texture = (int)(double)texRef;
                            c.Faces[kv.Key] = bf;
                        }
                    if (c.From == null || c.To == null)
                    {
                        m.Warnings.Add("元素缺 from/to，跳过：" + c.Name);
                        continue;
                    }
                    // 零厚度面片标记（作者把翅膀膜等做成某轴 from==to 的单面片，全库约 88% 模型命中）：
                    // 立方体两个大面完全共面（一明一暗）会 Z-fighting。不扩厚度（侧边条会破坏膜片轮廓），
                    // 由导入器只发射单一大面 + shader 双面/VFACE 翻转法线实现单面片双视角渲染。
                    c.FlatAxis = -1;
                    for (int a = 0; a < 3; a++)
                        if (Math.Abs(c.To[a] - c.From[a]) < 0.05) { c.FlatAxis = a; break; }
                    m.Cubes.Add(c);
                    if (!string.IsNullOrEmpty(c.Uuid)) byUuid[c.Uuid] = c;
                }

            WalkOutliner(BBJson.Get(root, "outliner"), null, m, byUuid);
            ResolveNamesAndPivots(m);

            var anims = BBJson.GetArr(root, "animations");
            if (anims != null) foreach (var o in anims)
                {
                    var a = BBJson.Obj(o);
                    if (a != null) m.AnimationNames.Add(BBJson.GetStr(a, "name"));
                }
            m.IdleEmbedded = ExtractEmbeddedIdle(root);
            return m;
        }

        // bbmodel 根 animations 数组 = Blockbench 内部动画（预览截图口径，未过导出符号变换）。
        // animators 按组 UUID 索引（匿名组也有 UUID——绕过名字断链）；取 min-time 关键帧首 data point。
        static BBIdlePose ExtractEmbeddedIdle(Dictionary<string, object> root)
        {
            var anims = BBJson.GetArr(root, "animations");
            if (anims == null) return null;
            Dictionary<string, object> anim = null;
            string name = null;
            foreach (var o in anims)
            {
                var a = BBJson.Obj(o);
                if (a == null) continue;
                if (BBJson.GetStr(a, "name") == "idle") { anim = a; break; }
            }
            if (anim == null)
                foreach (var o in anims)
                {
                    var a = BBJson.Obj(o);
                    if (a == null) continue;
                    anim = a;
                    break;
                }
            if (anim == null) return null;

            var pose = new BBIdlePose { AnimName = name, FromEmbedded = true };
            foreach (var o in anims)
            {
                var a = BBJson.Obj(o);
                if (a != null) pose.AllAnimNames.Add(BBJson.GetStr(a, "name"));
            }
            var animators = BBJson.GetObj(anim, "animators");
            if (animators == null) return null;
            var axes = new[] { "x", "y", "z" };
            foreach (var kv in animators)
            {
                var an = BBJson.Obj(kv.Value);
                if (an == null) continue;
                var type = BBJson.GetStr(an, "type");
                if (type != null && type != "bone") continue;
                var kfs = BBJson.GetArr(an, "keyframes");
                if (kfs == null) continue;
                foreach (var k in kfs)
                {
                    var kf = BBJson.Obj(k);
                    if (kf == null) continue;
                    string channel = BBJson.GetStr(kf, "channel");
                    if (channel != "rotation" && channel != "position") continue;
                    var pts = BBJson.GetArr(kf, "data_points");
                    if (pts == null || pts.Count == 0) continue;
                    var dp = BBJson.Obj(pts[0]);
                    if (dp == null) continue;
                    var xyz = new double[3];
                    bool ok = true;
                    for (int i = 0; i < 3; i++)
                    {
                        var v = BBJson.Get(dp, axes[i]);
                        if (v is double d) xyz[i] = d;
                        else if (v is string s && double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var p)) xyz[i] = p;
                        else { ok = false; break; }
                    }
                    if (!ok) continue;
                    double t = BBJson.GetNum(kf, "time");
                    var dict = channel == "rotation" ? pose.RotByUuid : pose.PosByUuid;
                    double[] prev;
                    if (!dict.TryGetValue(kv.Key, out prev) || t < prev[3]) dict[kv.Key] = new[] { xyz[0], xyz[1], xyz[2], t };
                }
            }
            if (pose.RotByUuid.Count == 0 && pose.PosByUuid.Count == 0) return null;
            return pose;
        }

        static void WalkOutliner(object node, BBGroup parent, BBModel m, Dictionary<string, BBCube> byUuid)
        {
            var list = node as List<object>;
            if (list == null) return;
            // 先建本层组再挂立方体（立方体的 ChainGroups 需要完整父链）
            var group = parent;
            if (group == null && list.Count > 0)
            {
                // 根列表可能直接混排组与元素引用；逐项处理时按需构造链
            }
            foreach (var item in list)
            {
                var uuid = item as string;
                if (uuid != null)
                {
                    BBCube c;
                    if (!byUuid.TryGetValue(uuid, out c))
                    {
                        m.Warnings.Add("outliner 引用了不存在的元素 " + uuid);
                        continue;
                    }
                    if (c.OwnerGroup != null || c.ChainGroups != null) continue; // 重复引用只认首个
                    c.OwnerGroup = parent;
                    var chain = new List<BBGroup>();
                    for (var g = parent; g != null; g = g.Parent) chain.Add(g); // 内→外
                    c.ChainGroups = chain;
                    if (parent != null) parent.Cubes.Add(c);
                    continue;
                }
                var g0 = BBJson.Obj(item);
                if (g0 == null) continue;
                var ng = new BBGroup
                {
                    Name = BBJson.GetStr(g0, "name"),
                    Uuid = BBJson.GetStr(g0, "uuid"),
                    Origin = BBJson.VecOrNull(g0, "origin"),
                    Rotation = BBJson.VecOrNull(g0, "rotation"),
                    Parent = parent,
                };
                if (ng.Name == null) ng.NameWasNull = true;
                if (ng.Origin == null) ng.OriginWasNull = true;
                if (ng.Rotation == null) ng.Rotation = new double[3];
                m.Groups.Add(ng);
                if (parent != null) parent.Children.Add(ng);
                WalkOutliner(BBJson.Get(g0, "children"), ng, m, byUuid);
            }
        }

        // 匿名组补名与补轴心：名=首成员立方体名（无则 bone_N）；轴心=成员立方体中心均值
        //（无立方体取已解析子组轴心均值；有旋转的组保持 [0,0,0] 以保几何）。质心轴心仅影响骨骼摆动中心。
        static void ResolveNamesAndPivots(BBModel m)
        {
            int anon = 0;
            // 逆 DFS 序（子先于父）
            for (int i = m.Groups.Count - 1; i >= 0; i--)
            {
                var g = m.Groups[i];
                if (g.OriginWasNull && g.Origin == null)
                {
                    if (g.Rotation[0] != 0 || g.Rotation[1] != 0 || g.Rotation[2] != 0)
                    {
                        g.Origin = new double[3];
                    }
                    else
                    {
                        double sx = 0, sy = 0, sz = 0; int n = 0;
                        foreach (var c in g.Cubes)
                        {
                            sx += (c.From[0] + c.To[0]) / 2; sy += (c.From[1] + c.To[1]) / 2; sz += (c.From[2] + c.To[2]) / 2; n++;
                        }
                        if (n == 0)
                            foreach (var ch in g.Children)
                                if (ch.Origin != null) { sx += ch.Origin[0]; sy += ch.Origin[1]; sz += ch.Origin[2]; n++; }
                        g.Origin = n > 0 ? new[] { sx / n, sy / n, sz / n } : new double[3];
                        g.PivotFromCentroid = n > 0;
                    }
                }
                if (g.NameWasNull)
                {
                    anon++;
                    g.Name = g.Cubes.Count > 0 && !string.IsNullOrEmpty(g.Cubes[0].Name)
                        ? g.Cubes[0].Name
                        : "bone_" + i;
                }
            }
            m.AnonymousGroups = anon;
        }

        // ---------- idle.json（GeckoLib 动画格式） ----------
        public static BBIdlePose LoadIdle(string path)
        {
            if (!File.Exists(path)) return null;
            try
            {
                var root = BBJson.Obj(BBJson.Parse(File.ReadAllText(path)));
                var anims = BBJson.GetObj(root, "animations");
                if (anims == null) return null;
                KeyValuePair<string, object> chosen = default(KeyValuePair<string, object>);
                bool found = false;
                foreach (var kv in anims)
                {
                    if (kv.Key == "idle") { chosen = kv; found = true; break; }
                }
                if (!found)
                    foreach (var kv in anims) { chosen = kv; found = true; break; }
                if (!found) return null;

                var pose = new BBIdlePose { AnimName = chosen.Key };
                foreach (var kv in anims) pose.AllAnimNames.Add(kv.Key);

                var anim = BBJson.Obj(chosen.Value);
                var bones = BBJson.GetObj(anim, "bones");
                if (bones == null) return pose;
                foreach (var kv in bones)
                {
                    var bone = BBJson.Obj(kv.Value);
                    if (bone == null) continue;
                    var rot = Channel(bone, "rotation", pose);
                    var pos = Channel(bone, "position", pose);
                    if (rot != null) pose.Rotations[kv.Key] = rot;
                    if (pos != null) pose.Positions[kv.Key] = pos;
                }
                return pose;
            }
            catch (Exception ex)
            {
                return new BBIdlePose { Error = ex.Message };
            }
        }

        // 通道值：[x,y,z] / {"vector":[...]} / {"pre":[...]} / {"0.0":..., "0.5":...}（关键帧→取最小时间）
        static double[] Channel(Dictionary<string, object> bone, string key, BBIdlePose pose)
        {
            var v = BBJson.Get(bone, key);
            if (v == null) return null;
            var l = v as List<object>;
            if (l != null) return ToVec3(l);
            var d = v as Dictionary<string, object>;
            if (d == null) return null;
            var vec = BBJson.GetArr(d, "vector");
            if (vec != null) return ToVec3(vec);
            var pre = BBJson.GetArr(d, "pre");
            if (pre != null) return ToVec3(pre);
            // 时间关键帧：取最小时间戳的值（post 优先于 pre）
            double bestT = double.PositiveInfinity;
            object best = null;
            foreach (var kv in d)
            {
                double t;
                if (!double.TryParse(kv.Key, NumberStyles.Float, CultureInfo.InvariantCulture, out t)) continue;
                if (t < bestT)
                {
                    bestT = t;
                    var vl = kv.Value as List<object>;
                    if (vl != null) { best = vl; continue; }
                    var vd = kv.Value as Dictionary<string, object>;
                    if (vd != null)
                    {
                        var post = BBJson.GetArr(vd, "post");
                        best = post ?? BBJson.GetArr(vd, "pre");
                    }
                }
            }
            if (best is List<object> bl)
            {
                pose.HasKeyframes = true;
                return ToVec3(bl);
            }
            return null;
        }

        static double[] ToVec3(List<object> l)
        {
            var r = new double[3];
            for (int i = 0; i < 3 && i < l.Count; i++) r[i] = l[i] is double ? (double)l[i] : 0;
            return r;
        }

        // 面的最终 UV 矩形：显式 faces.uv 优先 → box_uv 布局推算 → 整图兜底
        public static double[] ResolveFaceUv(BBCube c, string face, BBModel m)
        {
            BBFace f;
            if (c.Faces.TryGetValue(face, out f) && f.Uv != null && f.Uv.Length == 4) return f.Uv;
            if (c.BoxUv && c.UvOffset != null) return BoxUvRect(c, face);
            int w = m != null ? m.ResolutionW : 0, h = m != null ? m.ResolutionH : 0;
            if (w <= 0) w = 16;
            if (h <= 0) h = 16;
            return new double[] { 0, 0, w, h };
        }

        // bedrock 标准盒式展开：行1 up|down，行2 east|north|west|south
        static double[] BoxUvRect(BBCube c, string face)
        {
            double w = Math.Abs(c.To[0] - c.From[0]), h = Math.Abs(c.To[1] - c.From[1]), d = Math.Abs(c.To[2] - c.From[2]);
            double u = c.UvOffset[0], v = c.UvOffset[1];
            switch (face)
            {
                case "east": return new[] { u, v + d, u + d, v + d + h };
                case "north": return new[] { u + d, v + d, u + d + w, v + d + h };
                case "west": return new[] { u + d + w, v + d, u + d + w + d, v + d + h };
                case "south": return new[] { u + 2 * d + w, v + d, u + 2 * d + 2 * w, v + d + h };
                case "up": return new[] { u + d, v, u + d + w, v + d };
                default: return new[] { u + d + w, v, u + d + 2 * w, v + d }; // down
            }
        }

        static void ReadPngSize(byte[] png, out int w, out int h)
        {
            w = h = 0;
            if (png == null || png.Length < 24) return;
            if (png[0] != 0x89 || png[1] != 'P' || png[2] != 'N' || png[3] != 'G') return;
            w = (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19];
            h = (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23];
        }
    }

    /// <summary>骨骼局部系分解与待机姿态合成（全部在 BB 右手系内完成）。</summary>
    public static class BBRigMath
    {
        // 填充组的 World/Local 变换（绑定 + 待机两套）；待机姿态 = 绑定欧拉 + 姿态增量（分量相加）。
        // 姿态源优先级：内嵌动画（UUID 索引·Blockbench 预览口径·数值未过导出符号变换）> idle.json（名字索引·部分批次 X/Y 反号，仅兜底）。
        public static void Prepare(BBModel m)
        {
            m.ActiveIdlePose = PickPose(m);
            m.IdleApplied = m.ActiveIdlePose != null;
            var pose = m.ActiveIdlePose;
            if (pose != null && pose.UnknownBones.Count == 0 && m.Groups.Count > 0)
            {
                if (pose.FromEmbedded)
                {
                    var uuids = new HashSet<string>();
                    foreach (var g in m.Groups) if (g.Uuid != null) uuids.Add(g.Uuid);
                    foreach (var kv in pose.RotByUuid) if (!uuids.Contains(kv.Key)) pose.UnknownBones.Add(kv.Key);
                    foreach (var kv in pose.PosByUuid) if (!uuids.Contains(kv.Key) && !pose.UnknownBones.Contains(kv.Key)) pose.UnknownBones.Add(kv.Key);
                }
                else
                {
                    var names = new HashSet<string>();
                    foreach (var g in m.Groups) if (g.Name != null && !names.Add(g.Name)) { } // 重名：首个生效
                    foreach (var kv in pose.Rotations) if (!names.Contains(kv.Key)) pose.UnknownBones.Add(kv.Key);
                    foreach (var kv in pose.Positions) if (!names.Contains(kv.Key) && !pose.UnknownBones.Contains(kv.Key)) pose.UnknownBones.Add(kv.Key);
                }
            }
            // 只对根级组递归（组树自带 Parent 链；平铺遍历会把每个组当根，局部位置按绝对轴心算→FK 随深度爆炸）
            foreach (var g in m.Groups)
                if (g.Parent == null) PrepareGroup(m, g, null);
            m.Prepared = true;
        }

        static BBIdlePose PickPose(BBModel m)
        {
            var e = m.IdleEmbedded;
            if (e != null && (e.RotByUuid.Count > 0 || e.PosByUuid.Count > 0)) return e;
            var f = m.Idle;
            if (f != null && f.Error == null && (f.Rotations.Count > 0 || f.Positions.Count > 0)) return f;
            return null;
        }

        // 姿态通道取值：内嵌动画按组 UUID 命中；idle.json 兜底按骨骼名
        static bool TryPoseChannel(BBModel m, BBGroup g, bool rotation, out double[] v)
        {
            v = null;
            var pose = m.ActiveIdlePose;
            if (pose == null) return false;
            if (pose.FromEmbedded && g.Uuid != null)
            {
                var d = rotation ? pose.RotByUuid : pose.PosByUuid;
                if (d.TryGetValue(g.Uuid, out v)) return true;
            }
            if (g.Name == null) return false;
            var dn = rotation ? pose.Rotations : pose.Positions;
            return dn.TryGetValue(g.Name, out v);
        }

        static void PrepareGroup(BBModel m, BBGroup g, BBGroup p)
        {
            if (g.Name == null) g.Name = "bone_" + m.Groups.IndexOf(g); // 兜底（正常已在 ResolveNamesAndPivots 命名）
            double rx = g.Rotation[0], ry = g.Rotation[1], rz = g.Rotation[2];
            g.OwnBindQuat = BBMath.EulerXyzDeg(rx, ry, rz);
            double[] idleRot;
            if (TryPoseChannel(m, g, true, out idleRot))
                // GeckoLib/Bedrock 动画约定：X/Y 增量取反、Z 直加（引擎 rotX = bindX − animX；
                // Blockbench 预览/作者截图同口径）。直加会让 X 主导的盘绕链整体上下翻转（ZoneEater 实证）。
                g.OwnIdleQuat = BBMath.EulerXyzDeg(rx - idleRot[0], ry - idleRot[1], rz + idleRot[2]);
            else
                g.OwnIdleQuat = g.OwnBindQuat;

            var origin = BBV3.Of(g.Origin);
            g.WorldQuat = p == null ? g.OwnBindQuat : BBMath.Mul(p.WorldQuat, g.OwnBindQuat);
            g.WorldPivot = p == null ? origin : BBV3.Add(p.WorldPivot, BBMath.Rotate(p.WorldQuat, BBV3.Sub(origin, BBV3.Of(p.Origin))));
            g.LocalBindQuat = g.OwnBindQuat;
            g.LocalIdleQuat = g.OwnIdleQuat;
            g.LocalBindPosV = p == null ? g.WorldPivot : BBMath.Rotate(BBMath.Conj(p.WorldQuat), BBV3.Sub(g.WorldPivot, p.WorldPivot));
            double[] idlePos;
            if (TryPoseChannel(m, g, false, out idlePos))
                g.LocalIdlePosV = new BBV3 { X = g.LocalBindPosV.X + idlePos[0], Y = g.LocalBindPosV.Y + idlePos[1], Z = g.LocalBindPosV.Z + idlePos[2] };
            else
                g.LocalIdlePosV = g.LocalBindPosV;
            g.Depth = p == null ? 1 : p.Depth + 1;
            foreach (var c in g.Children) PrepareGroup(m, c, g);
        }

        // 立方体角点 → 绑定姿势世界坐标（自身旋转先应用，再沿组链内→外；动画语义见 IdleWorldPoint）
        public static BBV3 CubeWorldPoint(BBCube c, BBV3 p, bool idlePose)
        {
            var v = p;
            if (c.Rotation != null)
            {
                var q = BBMath.EulerXyzDeg(c.Rotation[0], c.Rotation[1], c.Rotation[2]);
                var o = c.Origin == null ? BBV3.Zero : BBV3.Of(c.Origin);
                v = BBV3.Add(o, BBMath.Rotate(q, BBV3.Sub(v, o)));
            }
            if (c.ChainGroups != null)
                foreach (var g in c.ChainGroups)
                {
                    var q = idlePose ? g.OwnIdleQuat : g.OwnBindQuat;
                    var og = BBV3.Of(g.Origin);
                    v = BBV3.Add(og, BBMath.Rotate(q, BBV3.Sub(v, og)));
                }
            return v;
        }

        // 待机姿势世界点（权威口径 = GeckoLib/Blockbench 骨骼语义）：
        // 绑定姿势世界点 → 烘进所属骨骼局部系 → 按 FK 用待机局部变换重建。
        // 注：不能直接在链条里把组旋转换成 idle 四元数——嵌套旋转时父骨骼旋转会带着子骨骼轴心走，
        // 链条式按绝对轴心旋转与骨骼 FK 不等价（ZoneEater 实测差 147 BB 单位）。
        public static BBV3 IdleWorldPoint(BBCube c, BBV3 p)
        {
            var bind = CubeWorldPoint(c, p, false);
            return HierarchyWorldPoint(c.OwnerGroup, LocalizePoint(c.OwnerGroup, bind), true);
        }

        // 待机姿势世界法线（同上，FK 口径）
        public static BBV3 IdleWorldNormal(BBCube c, BBV3 n)
        {
            var bind = CubeWorldNormal(c, n, false);
            return HierarchyWorldNormal(c.OwnerGroup, LocalizeNormal(c.OwnerGroup, bind), true);
        }

        // 立方体面法线 → 指定姿势的世界方向
        public static BBV3 CubeWorldNormal(BBCube c, BBV3 n, bool idlePose)
        {
            var v = n;
            if (c.Rotation != null)
                v = BBMath.Rotate(BBMath.EulerXyzDeg(c.Rotation[0], c.Rotation[1], c.Rotation[2]), v);
            if (c.ChainGroups != null)
                foreach (var g in c.ChainGroups)
                    v = BBMath.Rotate(idlePose ? g.OwnIdleQuat : g.OwnBindQuat, v);
            return v;
        }

        // 世界点 → 骨骼局部（网格按绑定姿势烘进组局部系：conj(WorldQuat)·(p − WorldPivot)）
        public static BBV3 LocalizePoint(BBGroup g, BBV3 worldPoint)
        {
            if (g == null) return worldPoint;
            return BBMath.Rotate(BBMath.Conj(g.WorldQuat), BBV3.Sub(worldPoint, g.WorldPivot));
        }

        public static BBV3 LocalizeNormal(BBGroup g, BBV3 worldNormal)
        {
            if (g == null) return worldNormal;
            return BBMath.Rotate(BBMath.Conj(g.WorldQuat), worldNormal);
        }

        // 骨骼局部点 → 世界（前向运动学；idle=true 用待机局部变换）——外部往返断言/导入器对账用
        public static BBV3 HierarchyWorldPoint(BBGroup g, BBV3 localPoint, bool idlePose)
        {
            var chain = new List<BBGroup>();
            for (var b = g; b != null; b = b.Parent) chain.Add(b); // 内→外
            var q = BBQuat.Identity;
            var pos = BBV3.Zero;
            for (int i = chain.Count - 1; i >= 0; i--) // 根→叶
            {
                var b = chain[i];
                pos = BBV3.Add(pos, BBMath.Rotate(q, idlePose ? b.LocalIdlePosV : b.LocalBindPosV));
                q = BBMath.Mul(q, idlePose ? b.LocalIdleQuat : b.LocalBindQuat);
            }
            return BBV3.Add(pos, BBMath.Rotate(q, localPoint));
        }

        // 骨骼局部方向 → 世界（FK 旋转链，无平移）
        public static BBV3 HierarchyWorldNormal(BBGroup g, BBV3 localNormal, bool idlePose)
        {
            var chain = new List<BBGroup>();
            for (var b = g; b != null; b = b.Parent) chain.Add(b); // 内→外
            var q = BBQuat.Identity;
            for (int i = chain.Count - 1; i >= 0; i--) // 根→叶
            {
                var b = chain[i];
                q = BBMath.Mul(q, idlePose ? b.LocalIdleQuat : b.LocalBindQuat);
            }
            return BBMath.Rotate(q, localNormal);
        }
    }

    /// <summary>最小 JSON 解析（bbmodel 为单行大 JSON，不依赖外部库）。</summary>
    public static class BBJson
    {
        public static object Parse(string s)
        {
            int i = 0;
            var v = ParseValue(s, ref i);
            SkipWs(s, ref i);
            if (i != s.Length) throw new FormatException("JSON 尾部残留字符 @" + i);
            return v;
        }

        public static Dictionary<string, object> Obj(object o)
        {
            var d = o as Dictionary<string, object>;
            if (d == null && o != null) throw new FormatException("期望 JSON 对象，实为 " + o.GetType().Name);
            return d;
        }

        public static List<object> Arr(object o)
        {
            var l = o as List<object>;
            if (l == null && o != null) throw new FormatException("期望 JSON 数组，实为 " + o.GetType().Name);
            return l;
        }

        public static object Get(Dictionary<string, object> d, string key)
        {
            object v;
            return d != null && d.TryGetValue(key, out v) ? v : null;
        }
        public static Dictionary<string, object> GetObj(Dictionary<string, object> d, string key) { return Obj(Get(d, key)); }
        public static List<object> GetArr(Dictionary<string, object> d, string key) { return Arr(Get(d, key)); }
        public static string GetStr(Dictionary<string, object> d, string key) { return Get(d, key) as string; }
        public static double GetNum(Dictionary<string, object> d, string key)
        {
            var o = Get(d, key);
            return o is double ? (double)o : 0;
        }
        public static bool GetBool(Dictionary<string, object> d, string key, bool def)
        {
            var o = Get(d, key);
            return o is bool ? (bool)o : def;
        }
        // 按源数组实际长度返回（坐标 3 元、UV 矩形 4 元共用；长度由调用方校验）
        public static double[] VecOrNull(Dictionary<string, object> d, string key)
        {
            var l = Get(d, key) as List<object>;
            if (l == null) return null;
            var r = new double[l.Count];
            for (int k = 0; k < l.Count; k++) r[k] = l[k] is double ? (double)l[k] : 0;
            return r;
        }
        public static double[] Vec(Dictionary<string, object> d, string key)
        {
            var v = VecOrNull(d, key);
            return v ?? new double[3];
        }

        static void SkipWs(string s, ref int i)
        {
            while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\n' || s[i] == '\r')) i++;
        }

        static object ParseValue(string s, ref int i)
        {
            SkipWs(s, ref i);
            if (i >= s.Length) throw new FormatException("JSON 意外结束 @" + i);
            char c = s[i];
            switch (c)
            {
                case '{': return ParseObj(s, ref i);
                case '[': return ParseArr(s, ref i);
                case '"': return ParseStr(s, ref i);
                case 't': Expect(s, ref i, "true"); return true;
                case 'f': Expect(s, ref i, "false"); return false;
                case 'n': Expect(s, ref i, "null"); return null;
                default: return ParseNum(s, ref i);
            }
        }

        static Dictionary<string, object> ParseObj(string s, ref int i)
        {
            var d = new Dictionary<string, object>();
            i++; SkipWs(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return d; }
            while (true)
            {
                SkipWs(s, ref i);
                if (i >= s.Length || s[i] != '"') throw new FormatException("期望键名 @" + i);
                string k = ParseStr(s, ref i);
                SkipWs(s, ref i);
                if (i >= s.Length || s[i] != ':') throw new FormatException("期望冒号 @" + i);
                i++;
                d[k] = ParseValue(s, ref i);
                SkipWs(s, ref i);
                if (i >= s.Length) throw new FormatException("对象未闭合 @" + i);
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; return d; }
                throw new FormatException("对象格式错 @" + i);
            }
        }

        static List<object> ParseArr(string s, ref int i)
        {
            var l = new List<object>();
            i++; SkipWs(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return l; }
            while (true)
            {
                l.Add(ParseValue(s, ref i));
                SkipWs(s, ref i);
                if (i >= s.Length) throw new FormatException("数组未闭合 @" + i);
                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; return l; }
                throw new FormatException("数组格式错 @" + i);
            }
        }

        static string ParseStr(string s, ref int i)
        {
            i++; // 跳过开头引号
            var sb = new StringBuilder();
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c == '\\')
                {
                    if (i >= s.Length) break;
                    char e = s[i++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (i + 4 <= s.Length)
                            {
                                sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16));
                                i += 4;
                            }
                            break;
                        default: sb.Append(e); break;
                    }
                }
                else sb.Append(c);
            }
            throw new FormatException("字符串未闭合 @" + i);
        }

        static double ParseNum(string s, ref int i)
        {
            int start = i;
            if (i < s.Length && (s[i] == '-' || s[i] == '+')) i++;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.' || s[i] == 'e' || s[i] == 'E' || s[i] == '+' || s[i] == '-')) i++;
            double v;
            if (!double.TryParse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                throw new FormatException("数字格式错 @" + start);
            return v;
        }

        static void Expect(string s, ref int i, string word)
        {
            if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0)
                throw new FormatException("期望 " + word + " @" + i);
            i += word.Length;
        }
    }
}
