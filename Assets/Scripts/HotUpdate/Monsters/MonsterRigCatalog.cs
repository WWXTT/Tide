using System;
using System.Collections.Generic;
using CardCore;
using UnityEngine;

namespace MonsterRig
{
    /// <summary>
    /// 生物模型程序动画目录（2026-10-12 美术待办§二）：627 个 YGOMonstersRig prefab 的
    /// 体型分类 + 部位骨骼映射的唯一真相源，运行时只读。原生成菜单已删（2026-10-12，
    /// 编辑器工具只留动画预览），条目改手工维护；手改须置 manual=true。
    /// 预览窗口实例行「存」按钮写回本文件。
    ///
    /// 命名规约（YGOMCModels 作者习惯，实测 627 模型）：
    /// - 镜像侧 = 同名尾部加 "2"（arm/arm2）或 left_/right_ 前缀；
    /// - 多足 = 尾号 3+（leg3..leg24）；链段 = 序号递增（tail..tail16、neck..neck6、body2..body12）；
    /// - 同义词三种拼法并存（lowerarm/lower_arm、foreleg/lowerforeleg/foreleg_lower）——编辑期归一，目录存原名。
    /// </summary>
    public static class MonsterRigCatalog
    {
        public const string ConfigRelativePath = "Configs/MonsterRigCatalog.json";

        /// <summary>行走方式（2026-10-12 定案：目录按移动方式分类，不按解剖体型；
        /// 每只怪可手改 moveType+manual 单独指定，预览窗口可切换并写回）。</summary>
        public const string MoveFlight = "Flight";       // 飞行：振翅+同频起伏（有翅默认）
        public const string MoveSpider = "Spider";       // 多足爬行：逐腿相位涟漪
        public const string MoveSlither = "Slither";     // 蛇形蠕动：沿躯干/尾链行波
        public const string MoveBiped = "Biped";         // 双足行走：反相摆臂+重心摆
        public const string MoveQuadruped = "Quadruped"; // 四足行走：对角踏步+重心摆
        public const string MoveFloat = "Float";         // 漂浮：仅根浮动+呼吸

        public static string MoveLabel(string moveType)
        {
            switch (moveType)
            {
                case MoveFlight: return "飞行振翅";
                case MoveSpider: return "多足爬行";
                case MoveSlither: return "蛇形蠕动";
                case MoveBiped: return "双足行走";
                case MoveQuadruped: return "四足行走";
                default: return "漂浮";
            }
        }

        public static readonly string[] AllMoves =
        {
            MoveFlight, MoveSpider, MoveSlither, MoveBiped, MoveQuadruped, MoveFloat,
        };

        private static Dictionary<string, MonsterRigEntry> _byName;
        private static string _loadError;

        public static bool Loaded => _byName != null;
        public static string LoadError => _loadError;

        public static IReadOnlyDictionary<string, MonsterRigEntry> All
        {
            get { EnsureLoaded(); return _byName; }
        }

        /// <summary>按模型名（= prefab 根物体名）取目录条目；无目录/无条目返回 null（调用方优雅降级）。</summary>
        public static MonsterRigEntry Get(string modelName)
        {
            if (string.IsNullOrEmpty(modelName)) return null;
            EnsureLoaded();
            MonsterRigEntry e;
            return _byName.TryGetValue(modelName, out e) ? e : null;
        }

        /// <summary>重载目录（编辑器直改 JSON 后免重启；验证器亦用）。</summary>
        public static void Reload()
        {
            _byName = null;
            _loadError = null;
            EnsureLoaded();
        }

        static void EnsureLoaded()
        {
            if (_byName != null) return;
            var map = new Dictionary<string, MonsterRigEntry>(StringComparer.OrdinalIgnoreCase);
            try
            {
                string raw = TidePaths.ReadConfigText(ConfigRelativePath);
                if (!string.IsNullOrEmpty(raw))
                {
                    var root = TideJson.FromJson<MonsterRigCatalogRoot>(raw);
                    if (root != null && root.models != null)
                        foreach (var e in root.models)
                            if (e != null && !string.IsNullOrEmpty(e.name))
                                map[e.name.Trim()] = e;
                }
                else
                {
                    _loadError = "配置文件缺失（先跑菜单 Tools/YGO模型/4 生成体型分类目录）";
                }
            }
            catch (Exception ex)
            {
                _loadError = ex.Message;
            }
            _byName = map;
            if (_loadError == null && map.Count == 0)
                _loadError = "目录为空（先跑菜单 Tools/YGO模型/4 生成体型分类目录）";
            if (_loadError != null)
                TideLog.Warn("[MonsterRigCatalog] " + _loadError);
        }
    }

    [Serializable]
    public class MonsterRigCatalogRoot
    {
        public List<MonsterRigEntry> models = new List<MonsterRigEntry>();
        public string generatedNote = "";
    }

    /// <summary>单模型条目。所有骨骼名 = prefab 内 GameObject 名（原名，未归一化）。</summary>
    [Serializable]
    public class MonsterRigEntry
    {
        public string name = "";
        public string moveType = MonsterRigCatalog.MoveFloat;
        /// <summary>手工修正条目：目录再生成时原样保留。</summary>
        public bool manual;
        public string note = "";

        // ---- 部位（空串/空表 = 无该部位，对应模块自动跳过） ----
        public string head = "";          // 主头骨（无 = 不做头部注视）
        public string jaw = "";           // 颌/喙（头朝向估算参考，运行时不驱动）
        public string abdomen = "";       // 腹部（缩放脉动呼吸）
        public string bodyMain = "";      // 躯干主骨（重心/呼吸参考位）
        public List<string> neck = new List<string>();      // 近头在前（注视链分配用）
        public List<string> tail = new List<string>();      // 根→尾尖（序号升序）
        public List<string> bodyChain = new List<string>(); // 躯干链，近头在前（蛇形波用）
        public List<LimbRef> legs = new List<LimbRef>();    // 腿肢体根（含链内子骨）
        public List<LimbRef> arms = new List<LimbRef>();    // 臂肢体根
        public List<WingLimb> wings = new List<WingLimb>(); // 翅/鳍（含铰链轴）
        public List<LimbRef> soft = new List<LimbRef>();    // 触手/藤蔓类软体链

        // ---- 姿态参考（编辑期从 idle 姿态估算；重烘焙后须重新生成目录） ----
        public Vector3 headForward = new Vector3(0f, 0f, 1f); // 根空间头部朝向
        public float yawOffsetDeg;                            // 根前向(+Z)到 headForward 的水平角
    }

    /// <summary>肢体（腿/臂/软体）：bones 从根到叶；phase=步态相位（弧度）；side=左右镜像侧（±1）。</summary>
    [Serializable]
    public class LimbRef
    {
        public List<string> bones = new List<string>();
        public float phase;
        public int side = 1;
    }

    /// <summary>翅/鳍：hingeAxis=根空间挥动铰链轴（编辑期估），amplitudeDeg=呼吸收展幅度。</summary>
    [Serializable]
    public class WingLimb
    {
        public List<string> bones = new List<string>();
        public float phase;
        public int side = 1;
        public Vector3 hingeAxis = new Vector3(0f, 0f, 1f);
        public float amplitudeDeg = 10f;
    }
}
