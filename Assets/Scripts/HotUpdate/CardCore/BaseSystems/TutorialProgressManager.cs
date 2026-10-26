using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CardCore
{
    /// <summary>
    /// 教学进度管理器（2026-10-06 教学逻辑层）：首次登录在 persistentDataPath 建档（仿
    /// ElementUnlockManager 范式：惰性单例 + TideJson + 每次变更即写、缺档回落默认并落盘），
    /// 记录已完成教学关卡。UI 层（主菜单按钮显隐）后续只读本类，不再自持进度状态。
    /// 状态机（2026-10-19 三课制定案）：建档零进度 → 按 CourseOrder 逐课推进（basics 攻防 →
    /// advanced 进阶机制 → lunzhuan 末日降临 Boss）→ 三课全胜=教学整体完成（IsTutorialDone，
    /// 主菜单教学按钮隐藏/联机入口外露——2026-10-25 本地对战按钮退役）。无任何跳过/免教学路径——教学按钮纯进度路由。
    /// （旧「结业镜像局」环节 2026-10-19 随三课制整删：finalChallengeDone 字段/MarkFinalChallengeDone/
    ///   ShouldShowFinalChallenge 全链退役，旧档多余键反序列化自动忽略，天然兼容。）
    /// </summary>
    public sealed class TutorialProgressManager
    {
        /// <summary>基础教学关卡 id（与教学配置档 TutorialConfig.json 条目、BattleEntry.TutorialId 缺省一致）。
        /// 2026-10-16 教学一改版：两段式基础教学回炉重造（介绍段+攻防段），重新进序列第一位；
        /// 旧长教学的「不进序列」口径随之退役。</summary>
        public const string BasicsId = "basics";

        /// <summary>第三课·创作管线关卡 id（无战斗剧本——TutorialCreationFlow 走查制，
        /// 不是 TutorialConfig 条目；StartTutorialBattle 对本 id 分流到 BeginLesson）。</summary>
        public const string CreationLessonId = "chuangzuo";

        /// <summary>第二课·进阶机制关卡 id（2026-10-19 三课制）：脚本化教学局教四大进阶机制——
        /// 代价生成黑白元素资源、全时点卡的自动效果、不同速度主动效果连锁、规则光环唯一性
        /// （TutorialConfig2.json 条目，教学卡在 TutorialData）。2026-10-21 进度标识由拼音 jinjie
        /// 改英文 advanced——旧档 completed 残留 jinjie 失效（需重上第二课）；教学卡组/语音资产
        /// 仍按 jinjie 文件名寻址，与本 id 无耦合。</summary>
        public const string AdvancedId = "advanced";

        /// <summary>第三课·末日降临 Boss 挑战关卡 id（2026-10-18 真实对局流程版，2026-10-19 挪入
        /// 第三课位）：走战场五段式开局教学三变体（掷币+战场选卡组·BattleOpeningDirector），AI 镜像
        /// 玩家卡组后被幻卡清算、召唤末日降临。课序挪位不改 id——旧档 completed 天然兼容
        ///（旧档残留 lunzhuanAttempted 键 2026-10-21 随判据退役，反序列化自动忽略）。</summary>
        public const string LunzhuanId = "lunzhuan";

        /// <summary>新手课程序列（2026-10-19 三课制定案）：basics 攻防 → advanced 进阶机制 →
        /// lunzhuan 末日降临 Boss，三课全胜=教学整体完成（IsTutorialDone）。
        /// chuangzao（创作管线走查）已取消（2026-10-17）——走查机制休眠保留，旧档 completed 残留惰性、
        /// currentTutorial=chuangzuo 序列外自动作废（NextTutorialId 现行口径）。</summary>
        public static readonly string[] CourseOrder = { BasicsId, AdvancedId, LunzhuanId };

        private const string SAVE_FILE = "TutorialProgress.json";

        private static TutorialProgressManager _instance;

        /// <summary>运行时单例（真实存档路径；首次访问即建档——"首次登录创建教学进程"）。</summary>
        public static TutorialProgressManager Instance
            => _instance ??= new TutorialProgressManager(Path.Combine(TidePaths.PersistentDataPath, SAVE_FILE));

        private readonly string _savePath;
        private TutorialProgressData _data;

        /// <summary>进度变更事件（UI 按钮显隐/换文案的后续接线点）。</summary>
        public event Action OnProgressChanged;

        private TutorialProgressManager(string savePath)
        {
            _savePath = savePath;
            LoadOrCreate();
        }

        /// <summary>验证口：按指定存档路径构造独立实例（编辑器验证器用临时文件，
        /// 不碰真实存档；本类其余 API 均为实例方法，天然可注入）。</summary>
        public static TutorialProgressManager CreateForVerification(string savePath)
            => new TutorialProgressManager(savePath);

        /// <summary>云存档还原后重置单例（2026-10-23 云存档接入）：下次访问按新落盘数据重建。
        /// 旧实例的事件订阅随之失效——订阅方（主菜单等）须在 CloudSyncService.OnRestored 重订。</summary>
        public static void ResetForCloudRestore() => _instance = null;

        // ======================================== 查询 ========================================

        /// <summary>已完成的教学关卡 id 集。</summary>
        public IReadOnlyList<string> CompletedTutorials => _data.completedTutorials;

        /// <summary>进行中的教学关卡 id（null=未开始/已完成）。</summary>
        public string CurrentTutorial => _data.currentTutorial;

        /// <summary>新手课程序列（CourseOrder）是否全部完成（三课全胜=教学整体完成）。</summary>
        public bool IsCourseCompleted
            => CourseOrder.All(id => _data.completedTutorials.Contains(id));

        /// <summary>教学整体完成（2026-10-19 三课制：三课全胜即完成——结业镜像局环节退役）——
        /// 主菜单解除门禁（教学按钮隐藏、联机入口外露）的判据。</summary>
        public bool IsTutorialDone => IsCourseCompleted;

        /// <summary>主菜单是否门禁（true=只显示教学按钮；教学未完成期间）。</summary>
        public bool ShouldGateMainMenu => !IsTutorialDone;

        /// <summary>第三课创作管线走查状态（null=未开课；差集判定「新建」的开课基线+三步进度）。</summary>
        public CreationLessonData Creation => _data.creation;

        /// <summary>下一步要开的教学关卡 id（进行中优先 → 序列中第一个未完成 → 全完成=null）。
        /// null=新手课程已全部走完（三课全胜=IsTutorialDone，主菜单教学按钮已隐藏）。
        /// 2026-10-17 教学一改版兼容：tianji 攻防段并入 basics 第二段移出 CourseOrder——旧档残留的
        /// currentTutorial=tianji（序列外）作废，按序列首门未完成课取。</summary>
        public string NextTutorialId
        {
            get
            {
                if (!string.IsNullOrEmpty(_data.currentTutorial)
                    && System.Array.IndexOf(CourseOrder, _data.currentTutorial) >= 0)
                    return _data.currentTutorial;
                return CourseOrder.FirstOrDefault(id => !_data.completedTutorials.Contains(id));
            }
        }

        // ======================================== 写入 ========================================

        /// <summary>教学关卡完成落档（幂等；清进行中标记——含序列外残留作废，2026-10-17）。</summary>
        public void MarkTutorialCompleted(string tutorialId)
        {
            var id = string.IsNullOrEmpty(tutorialId) ? BasicsId : tutorialId;
            if (!_data.completedTutorials.Contains(id))
                _data.completedTutorials.Add(id);
            if (_data.currentTutorial == id)
                _data.currentTutorial = null;
            else if (!string.IsNullOrEmpty(_data.currentTutorial)
                     && System.Array.IndexOf(CourseOrder, _data.currentTutorial) < 0)
                _data.currentTutorial = null; // tianji 等序列外残留：并入教学一后作废
            SaveAndNotify();
        }

        /// <summary>第三课走查状态写入（TutorialCreationFlow 专用：检测到步骤推进时整组替换落档）。</summary>
        public void WriteCreationState(CreationLessonData creation)
        {
            _data.creation = creation;
            SaveAndNotify();
        }

        /// <summary>进行中关卡占位（仅当前为空时写入——不抢占其他课的进行中标记）。</summary>
        public void SetCurrentTutorial(string tutorialId)
        {
            if (string.IsNullOrEmpty(tutorialId)
                || !string.IsNullOrEmpty(_data.currentTutorial))
                return;
            _data.currentTutorial = tutorialId;
            SaveAndNotify();
        }

        private void SaveAndNotify()
        {
            SaveState();
            TideLog.Info("[TutorialProgress] 进度更新："
                         + $"current={_data.currentTutorial ?? "—"} done={IsTutorialDone}");
            OnProgressChanged?.Invoke();
        }

        // ======================================== 存取 ========================================

        private void LoadOrCreate()
        {
            try
            {
                if (File.Exists(_savePath))
                {
                    var loaded = TideJson.FromJson<TutorialProgressData>(File.ReadAllText(_savePath));
                    if (loaded != null)
                    {
                        _data = loaded;
                        _data.completedTutorials ??= new List<string>();
                        return;
                    }
                    TideLog.Warn("[TutorialProgress] 存档不可解析，按缺档重建");
                }
            }
            catch (Exception ex)
            {
                TideLog.Error($"[TutorialProgress] 读档失败 - {ex.Message}（按缺档重建）");
            }

            // 首次登录建档（2026-10-18 定案：纯课程模型——无跳过询问，建档即零进度，
            // 首点教学=直进教学一；旧档 skipAskPending/skipChosen/skipped 多余键反序列化自动忽略）
            _data = new TutorialProgressData();
            SaveState();
            TideLog.Info($"[TutorialProgress] 首次登录建档：{_savePath}");
        }

        private void SaveState()
        {
            try
            {
                var directory = Path.GetDirectoryName(_savePath);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllText(_savePath, TideJson.ToJson(_data, true));
            }
            catch (Exception ex)
            {
                TideLog.Error($"[TutorialProgress] 存档失败 - {ex.Message}");
            }
        }

        [Serializable]
        private class TutorialProgressData
        {
            public List<string> completedTutorials = new List<string>();
            public string currentTutorial;
            public CreationLessonData creation; // 第三课走查状态（null=未开课；旧档缺列兼容）
        }
    }

    /// <summary>创作管线走查状态（已取消课程的休眠机制，2026-10-17）：差集判定「新建」的开课基线 +
    /// 三步进度（单调推进）。属主=TutorialProgressManager（单存档单源）；读写逻辑在 TutorialCreationFlow。</summary>
    [Serializable]
    public class CreationLessonData
    {
        /// <summary>走查进度：0=未开始 1=效果已新建 2=含新效果的卡已新建 3=含新卡的卡组已保存（本课完成）。</summary>
        public int step;
        /// <summary>步①记档：新建效果 id（内容哈希）。</summary>
        public string effectId;
        /// <summary>步②记档：含新建效果的卡 id。</summary>
        public string cardId;
        /// <summary>步③记档：含新建卡的卡组名（最后一课镜像局加载用）。</summary>
        public string deckName;
        /// <summary>开课时效果库 id 快照（新建=当前 − 基线）。</summary>
        public List<string> baselineEffects = new List<string>();
        /// <summary>开课时卡表 id 快照。</summary>
        public List<string> baselineCards = new List<string>();
    }
}
