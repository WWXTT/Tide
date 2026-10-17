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
    /// 状态机（2026-10-17 清理后）：建档 → 按 CourseOrder 逐课推进（basics 两段式 → lunzhuan →
    /// chuangzuo 创作走查）→ 三课全完成 → 主菜单「最后一课」镜像局 → 胜=finalChallengeDone
    /// （IsTutorialDone，全部新手考验完成）。
    /// （旧「询问跳过/考核局免教学」路径已删——AskSkip 弹窗从未实装、考核分支不可达；
    ///   旧档多余 JSON 字段反序列化自动忽略，天然兼容。）
    /// </summary>
    public sealed class TutorialProgressManager
    {
        /// <summary>基础教学关卡 id（与 TutorialConfig.json 条目、BattleEntry.TutorialId 缺省一致）。
        /// 2026-10-16 教学一改版：两段式基础教学回炉重造（介绍段+攻防段），重新进序列第一位；
        /// 旧长教学的「不进序列」口径随之退役。</summary>
        public const string BasicsId = "basics";

        /// <summary>第三课·创作管线关卡 id（无战斗剧本——TutorialCreationFlow 走查制，
        /// 不是 TutorialConfig 条目；StartTutorialBattle 对本 id 分流到 BeginLesson）。</summary>
        public const string CreationLessonId = "chuangzuo";

        /// <summary>新手课程序列（2026-10-17 创作课取消定案）：basics → lunzhuan 两课，
        /// 全部完成 + 最后一课镜像局获胜 = 教学整体完成（IsTutorialDone）。
        /// chuangzuo（创作管线走查）已取消——走查机制休眠保留，旧档 completed 残留惰性、
        /// currentTutorial=chuangzuo 序列外自动作废（NextTutorialId 现行口径）。</summary>
        public static readonly string[] CourseOrder = { BasicsId, "lunzhuan" };

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

        // ======================================== 查询 ========================================

        /// <summary>待询问「是否跳过基础教学，进入考核？」（建档时置位，ChooseSkip 消费；
        /// 2026-10-17 恢复建档跳过链——首次点教学按钮时弹）。</summary>
        public bool SkipAskPending => _data.skipAskPending;

        /// <summary>跳过询问中选了「跳过」（考核路径进行中）：!Skipped 期间教学按钮=重开考核局，
        /// 不再提供回课程入口（2026-10-17 用户定案：负=原地重试）。</summary>
        public bool SkipChosen => _data.skipChosen;

        /// <summary>考核局已获胜=免教学（教学整体完成——主菜单解除门禁、教学按钮换匹配按钮）。</summary>
        public bool Skipped => _data.skipped;

        /// <summary>已完成的教学关卡 id 集。</summary>
        public IReadOnlyList<string> CompletedTutorials => _data.completedTutorials;

        /// <summary>进行中的教学关卡 id（null=未开始/已完成）。</summary>
        public string CurrentTutorial => _data.currentTutorial;

        /// <summary>新手课程序列（CourseOrder）是否全部完成（不含最后一课镜像局）。</summary>
        public bool IsCourseCompleted
            => CourseOrder.All(id => _data.completedTutorials.Contains(id));

        /// <summary>最后一课镜像局是否已获胜（教学整体完成的最后一环）。</summary>
        public bool FinalChallengeDone => _data.finalChallengeDone;

        /// <summary>教学整体完成（考核局获胜免教学 或 新手课程全完成+最后一课镜像局获胜）——
        /// 主菜单解除门禁、教学按钮换匹配按钮的判据。</summary>
        public bool IsTutorialDone => _data.skipped || (IsCourseCompleted && _data.finalChallengeDone);

        /// <summary>主菜单是否门禁（true=只显示教学按钮；教学未完成期间）。</summary>
        public bool ShouldGateMainMenu => !IsTutorialDone;

        /// <summary>主菜单「最后一课」临时按钮显隐判据：新手课程走完、镜像局未胜、未走考核免教学。</summary>
        public bool ShouldShowFinalChallenge
            => !IsTutorialDone && !_data.skipped && IsCourseCompleted && !_data.finalChallengeDone;

        /// <summary>第三课创作管线走查状态（null=未开课；差集判定「新建」的开课基线+三步进度）。</summary>
        public CreationLessonData Creation => _data.creation;

        /// <summary>下一步要开的教学关卡 id（进行中优先 → 序列中第一个未完成 → 全完成=null）。
        /// null=新手课程已走完（此时入口应查 ShouldShowFinalChallenge 走最后一课，而非再开课）。
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

        /// <summary>消耗「是否跳过基础教学，进入考核？」询问（2026-10-17 恢复）：skip=true=进考核
        /// 路径（skipChosen 落档）；false=正常课程路径。幂等消费 skipAskPending。</summary>
        public void ChooseSkip(bool skip)
        {
            _data.skipAskPending = false;
            if (skip) _data.skipChosen = true;
            SaveAndNotify();
        }

        /// <summary>考核局获胜落档=免教学（TutorialFlow.ReportBattleResult 路由）。负=无落档：
        /// 留在考核路径，下次点教学按钮重开考核局（2026-10-17 定案：不提供回课程）。</summary>
        public void MarkAssessmentWon()
        {
            if (_data.skipped) return;
            _data.skipped = true;
            SaveAndNotify();
        }

        /// <summary>最后一课镜像局获胜落档（教学整体完成）。</summary>
        public void MarkFinalChallengeDone()
        {
            if (_data.finalChallengeDone) return;
            _data.finalChallengeDone = true;
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

            // 首次登录建档（2026-10-17 恢复建档跳过链：skipAskPending 置位——首点教学按钮时弹询问）
            _data = new TutorialProgressData { skipAskPending = true };
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
            public bool skipAskPending;    // 建档置位：首点教学弹「是否跳过基础教学，进入考核？」（ChooseSkip 消费）
            public bool skipChosen;        // 选过「跳过」：考核路径进行中（!skipped 时教学按钮=重开考核局）
            public bool skipped;           // 考核局获胜=免教学（教学整体完成）
            public bool finalChallengeDone; // 最后一课镜像局已获胜（教学整体完成的最后一环）
            public List<string> completedTutorials = new List<string>();
            public string currentTutorial;
            public CreationLessonData creation; // 第三课走查状态（null=未开课；旧档缺列兼容）
        }
    }

    /// <summary>第三课「创作管线」走查状态：差集判定「新建」的开课基线 + 三步进度（单调推进）。
    /// 属主=TutorialProgressManager（单存档单源）；读写逻辑在 TutorialCreationFlow。</summary>
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
