using System;
using System.Collections.Generic;
using System.Linq;
using MonsterRig;
using UnityEngine;

namespace YGOImport
{
    /// <summary>
    /// YGOMonstersRig 部位分类器（2026-10-12 美术待办§二·前置调研产出）。
    /// 摸清 YGOMCModels 作者命名习惯并归一为部位类别：词表来自 627 个 prefab 全量实测
    /// （leg 601/arm 544/head 535/tail 322/wing 163/left_arm 92/foreleg 30/tentacle 29…）。
    ///
    /// 规约（实测）：
    /// - 镜像侧 = 尾缀 "2"（arm/arm2）或 left_/right_ 前缀；
    /// - 多足/链段 = 尾号递增（leg3..leg24、tail..tail16、neck..neck6、body2..body12）；
    /// - 拼法混杂（lowerarm/lower_arm、foreleg/lowerforeleg/foreleg_lower、源 typo lower_keg）——
    ///   归一时去下划线、去侧向前缀、剥尾号，再查同义词表；
    /// - 道具/装饰（剑袍帽轮等）黑名单，不参与分类与动画。
    /// </summary>
    public static class RigPartClassifier
    {
        public enum PartKind
        {
            None, Head, Jaw, Eye, Ear, Horn, Neck, Tail, Wing, Fin,
            LegUpper, LegLower, Foot, ArmUpper, ArmLower, HandEnd,
            Body, Abdomen, Soft, Prop
        }

        /// <summary>肢体超族（链归属判定：同超族祖先存在则视为其链内子骨）。</summary>
        public static int SuperFamily(PartKind k)
        {
            switch (k)
            {
                case PartKind.LegUpper:
                case PartKind.LegLower:
                case PartKind.Foot: return 1;
                case PartKind.ArmUpper:
                case PartKind.ArmLower:
                case PartKind.HandEnd: return 2;
                case PartKind.Wing:
                case PartKind.Fin: return 3;
                case PartKind.Soft: return 4;
                default: return 0;
            }
        }

        // 同义词表：归一化基名（小写、去下划线、去 left/right 前缀、去尾号）→ 部位
        static readonly Dictionary<string, PartKind> Table = new Dictionary<string, PartKind>
        {
            // 头
            { "head", PartKind.Head }, { "face", PartKind.Head }, { "skull", PartKind.Head },
            { "snout", PartKind.Head }, { "muzzle", PartKind.Head }, { "crest", PartKind.Head },
            { "chin", PartKind.Head }, { "horsehead", PartKind.Head },
            { "neck", PartKind.Neck },
            { "jaw", PartKind.Jaw }, { "mandible", PartKind.Jaw }, { "beak", PartKind.Jaw },
            { "tongue", PartKind.Jaw }, { "tounge", PartKind.Jaw }, { "mouth", PartKind.Jaw },
            { "ear", PartKind.Ear }, { "antenna", PartKind.Ear }, { "horn", PartKind.Horn },
            { "eye", PartKind.Eye },
            // 尾
            { "tail", PartKind.Tail },
            // 肢·腿族
            { "leg", PartKind.LegUpper }, { "legs", PartKind.LegUpper },
            { "foreleg", PartKind.LegUpper }, { "hindleg", PartKind.LegUpper },
            { "fore", PartKind.LegUpper }, { "hind", PartKind.LegUpper },
            { "lowerleg", PartKind.LegLower }, { "lowerforeleg", PartKind.LegLower },
            { "lowerhindleg", PartKind.LegLower }, { "lowerkeg", PartKind.LegLower }, // 源 typo：lower_keg
            { "foreleglower", PartKind.LegLower }, { "hindleglower", PartKind.LegLower },
            { "lowerfore", PartKind.LegLower }, { "lowerhind", PartKind.LegLower },
            { "knee", PartKind.LegLower },
            { "foot", PartKind.Foot }, { "feet", PartKind.Foot },
            { "forefoot", PartKind.Foot }, { "hindfoot", PartKind.Foot },
            { "forepaw", PartKind.Foot }, { "hindpaw", PartKind.Foot }, { "paw", PartKind.Foot },
            { "lowerfoot", PartKind.Foot }, { "hoof", PartKind.Foot },
            { "lowerforepaw", PartKind.Foot }, { "lowerhindpaw", PartKind.Foot },
            { "lowerforefoot", PartKind.Foot }, { "lowerhindfoot", PartKind.Foot },
            { "dewclaw", PartKind.Foot }, { "foreclaw", PartKind.Foot }, { "hindclaw", PartKind.Foot },
            { "hindhoof", PartKind.Foot }, { "forehoof", PartKind.Foot }, { "hindhoofupper", PartKind.Foot },
            // 肢·臂族
            { "arm", PartKind.ArmUpper }, { "shoulder", PartKind.ArmUpper },
            { "lowerarm", PartKind.ArmLower },
            { "hand", PartKind.HandEnd }, { "fist", PartKind.HandEnd }, { "claw", PartKind.HandEnd },
            { "claws", PartKind.HandEnd }, { "palm", PartKind.HandEnd },
            { "pincer", PartKind.HandEnd }, { "talon", PartKind.HandEnd },
            { "finger", PartKind.HandEnd }, { "thumb", PartKind.HandEnd }, { "fingers", PartKind.HandEnd },
            // 翅/鳍
            { "wing", PartKind.Wing }, { "lowerwing", PartKind.Wing },
            { "buttwing", PartKind.Wing }, { "flap", PartKind.Wing },
            { "fin", PartKind.Fin }, { "flipper", PartKind.Fin }, { "lowerflipper", PartKind.Fin },
            // 躯干
            { "body", PartKind.Body }, { "upperbody", PartKind.Body }, { "lowerbody", PartKind.Body },
            { "horseupperbody", PartKind.Body }, { "horse", PartKind.Body }, { "flank", PartKind.Body },
            { "thorax", PartKind.Body }, { "torso", PartKind.Body }, { "chest", PartKind.Body },
            { "hip", PartKind.Body }, { "hips", PartKind.Body }, { "shell", PartKind.Body },
            { "ball", PartKind.Body }, // 史莱姆/球身
            { "upper", PartKind.Body }, { "bone", PartKind.Body }, // 骨架躯体骨
            { "abdomen", PartKind.Abdomen }, { "belly", PartKind.Abdomen },
            // 软体
            { "tentacle", PartKind.Soft }, { "vine", PartKind.Soft }, { "feeler", PartKind.Soft },
            { "tendril", PartKind.Soft }, { "whip", PartKind.Soft }, { "cord", PartKind.Soft },
            { "stem", PartKind.Soft }, { "chain", PartKind.Soft }, { "branch", PartKind.Soft },
            { "trunk", PartKind.Soft }, { "danglie", PartKind.Soft }, { "snake", PartKind.Soft },
            // 道具/装饰（黑名单）
            { "sword", PartKind.Prop }, { "staff", PartKind.Prop }, { "axe", PartKind.Prop },
            { "spear", PartKind.Prop }, { "shield", PartKind.Prop }, { "gun", PartKind.Prop },
            { "wand", PartKind.Prop }, { "trident", PartKind.Prop }, { "scythe", PartKind.Prop },
            { "blade", PartKind.Prop }, { "hat", PartKind.Prop }, { "hair", PartKind.Prop },
            { "robe", PartKind.Prop }, { "robes", PartKind.Prop }, { "cape", PartKind.Prop },
            { "cloak", PartKind.Prop }, { "kilt", PartKind.Prop }, { "turret", PartKind.Prop },
            { "wheel", PartKind.Prop }, { "flower", PartKind.Prop }, { "leaf", PartKind.Prop },
            { "crown", PartKind.Prop }, { "halo", PartKind.Prop }, { "saddle", PartKind.Prop },
            { "mane", PartKind.Prop }, { "seat", PartKind.Prop }, { "throne", PartKind.Prop },
            { "tassel", PartKind.Prop }, { "hattail", PartKind.Prop },
            { "pauldron", PartKind.Prop }, { "mace", PartKind.Prop }, { "bow", PartKind.Prop },
            { "lance", PartKind.Prop }, { "knife", PartKind.Prop }, { "jar", PartKind.Prop },
            { "barrel", PartKind.Prop }, { "orb", PartKind.Prop }, { "key", PartKind.Prop },
            { "dress", PartKind.Prop }, { "root", PartKind.Prop },
            { "lid", PartKind.Prop }, { "capeside", PartKind.Prop }, { "cloth", PartKind.Prop },
            { "ponytail", PartKind.Prop }, { "loincloth", PartKind.Prop }, { "spring", PartKind.Prop },
            { "skirt", PartKind.Prop }, { "boot", PartKind.Prop }, { "plate", PartKind.Prop },
            { "broom", PartKind.Prop }, { "bulb", PartKind.Prop }, { "fire", PartKind.Prop },
            { "smoke", PartKind.Prop },
        };

        public struct NameInfo
        {
            public PartKind Kind;
            public int Side;   // -1 左 / +1 右（显式前缀或镜像尾缀 2）；0 未标
            public int Index;  // 尾号（无=0；"2"=2 即镜像标记）
            public string Base;
        }

        public static NameInfo Parse(string rawName)
        {
            var info = new NameInfo { Kind = PartKind.None, Side = 0, Index = 0 };
            if (string.IsNullOrEmpty(rawName)) return info;
            string s = rawName.Trim().ToLowerInvariant().Replace("_", "").Replace(" ", "");
            if (s.StartsWith("left")) { info.Side = -1; s = s.Substring(4); }
            else if (s.StartsWith("right")) { info.Side = 1; s = s.Substring(5); }

            int digitStart = s.Length;
            while (digitStart > 0 && s[digitStart - 1] >= '0' && s[digitStart - 1] <= '9') digitStart--;
            string digits = s.Substring(digitStart);
            info.Base = s.Substring(0, digitStart);
            if (digits.Length > 0) info.Index = int.Parse(digits);
            if (info.Side == 0 && info.Index == 2) info.Side = -1; // 尾缀 2 = 镜像侧惯例

            PartKind kind;
            if (Table.TryGetValue(info.Base, out kind)) info.Kind = kind;
            // 尾号 ≥3 的肢体 = 独立个体（多足），镜像约定只认 2
            return info;
        }

        // ================= 层级走查 =================

        public class Node
        {
            public Transform T;
            public NameInfo Info;
            public Node Parent;
            public List<Node> Children = new List<Node>();
            /// <summary>肢体链根：本骨为超族且祖先无同超族。</summary>
            public bool IsLimbRoot;
        }

        /// <summary>按 Transform 层级建 Node 树（跳过根物体自身）。</summary>
        public static List<Node> BuildTree(Transform root)
        {
            var roots = new List<Node>();
            var byTransform = new Dictionary<Transform, Node>();
            foreach (var child in root)
            {
                var tr = (Transform)child;
                var node = new Node { T = tr, Info = Parse(tr.name) };
                byTransform[tr] = node;
                roots.Add(node);
                BuildRecursive(tr, node, byTransform);
            }
            // 肢体链根标记
            foreach (var n in AllNodes(roots))
            {
                int sf = SuperFamily(n.Info.Kind);
                if (sf == 0) { n.IsLimbRoot = false; continue; }
                bool ancestorSame = false;
                for (var p = n.Parent; p != null; p = p.Parent)
                    if (SuperFamily(p.Info.Kind) == sf) { ancestorSame = true; break; }
                n.IsLimbRoot = !ancestorSame;
            }
            return roots;
        }

        static void BuildRecursive(Transform parent, Node parentNode, Dictionary<Transform, Node> byTransform)
        {
            foreach (var child in parent)
            {
                var tr = (Transform)child;
                var node = new Node { T = tr, Info = Parse(tr.name), Parent = parentNode };
                byTransform[tr] = node;
                parentNode.Children.Add(node);
                BuildRecursive(tr, node, byTransform);
            }
        }

        public static IEnumerable<Node> AllNodes(List<Node> roots)
        {
            var stack = new Stack<Node>();
            for (int i = roots.Count - 1; i >= 0; i--) stack.Push(roots[i]);
            while (stack.Count > 0)
            {
                var n = stack.Pop();
                yield return n;
                for (int i = n.Children.Count - 1; i >= 0; i--) stack.Push(n.Children[i]);
            }
        }

        /// <summary>肢体链收集：从链根沿同超族子骨下行（道具/无名子树不入链）。</summary>
        public static List<Node> ChainOf(Node limbRoot)
        {
            var chain = new List<Node> { limbRoot };
            int sf = SuperFamily(limbRoot.Info.Kind);
            CollectChain(limbRoot, sf, chain);
            return chain;
        }

        static void CollectChain(Node parent, int sf, List<Node> chain)
        {
            foreach (var c in parent.Children)
            {
                if (SuperFamily(c.Info.Kind) == sf)
                {
                    chain.Add(c);
                    CollectChain(c, sf, chain);
                }
            }
        }

        // ================= 体型判定 =================

        public struct RigFacts
        {
            public int LegRoots, ArmRoots, WingRoots, SoftRoots, FinRoots;
            public int TailCount, NeckCount, BodyCount;
            public bool HasHead, HasThorax, HasAbdomen, HasMandible, HasAntenna;
        }

        public static RigFacts CollectFacts(List<Node> roots)
        {
            var f = new RigFacts();
            foreach (var n in AllNodes(roots))
            {
                switch (n.Info.Kind)
                {
                    case PartKind.Head: f.HasHead = true; break;
                    case PartKind.Neck: f.NeckCount++; break;
                    case PartKind.Tail: f.TailCount++; break;
                    case PartKind.Body: f.BodyCount++; break;
                    case PartKind.Abdomen: f.HasAbdomen = true; break;
                    default: break;
                }
                if (n.Info.Base == "thorax") f.HasThorax = true;
                if (n.Info.Base == "mandible") f.HasMandible = true;
                if (n.Info.Base == "antenna") f.HasAntenna = true;
                if (!n.IsLimbRoot) continue;
                int sf = SuperFamily(n.Info.Kind);
                if (sf == 1) f.LegRoots++;
                else if (sf == 2) f.ArmRoots++;
                else if (sf == 3) { f.WingRoots++; if (n.Info.Kind == PartKind.Fin) f.FinRoots++; }
                else if (sf == 4) f.SoftRoots++;
            }
            return f;
        }

        /// <summary>行走方式判定（2026-10-12 定案：按移动方式分类）：
        /// 有翅→飞行振翅；无翅按解剖推默认（多足≥3对或节肢标记任二→多足爬；无腿且链长/软体→
        /// 蛇形蠕动；腿对≥2→四足；腿对=1→双足；落空→漂浮）。逐怪可手改目录 moveType 覆盖。</summary>
        public static string DecideMoveType(RigFacts f)
        {
            if (f.WingRoots > 0) return MonsterRigCatalog.MoveFlight;
            int legPairs = (f.LegRoots + 1) / 2;
            int arthroMarkers = (f.HasThorax ? 1 : 0) + (f.HasAbdomen ? 1 : 0)
                + (f.HasMandible ? 1 : 0) + (f.HasAntenna ? 1 : 0);
            if (legPairs >= 3 || arthroMarkers >= 2) return MonsterRigCatalog.MoveSpider;
            if (f.LegRoots == 0 && (f.TailCount >= 3 || f.BodyCount >= 3 || f.SoftRoots >= 2 || f.FinRoots >= 2))
                return MonsterRigCatalog.MoveSlither;
            if (legPairs >= 2) return MonsterRigCatalog.MoveQuadruped;
            if (f.LegRoots > 0) return MonsterRigCatalog.MoveBiped;
            return MonsterRigCatalog.MoveFloat;
        }
    }
}
