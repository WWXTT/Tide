using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CardCore
{
    /// <summary>
    /// 挑战模式进度管理器（2026-10-06）：仿 TutorialProgressManager 范式（惰性单例 + TideJson +
    /// 每变更即写、CreateForVerification 注入临时存档），记录九档难度各自的胜/负与最高通关难度。
    /// 定案：难度全开自由选（不做顺序解锁门禁），战绩只作展示与自我目标。
    /// 难度语义（单值联动）：难度 N（1..9）= 电脑地牌槽上限 9+N、起手 6+N（GameCore.InitGame
    /// 的 p2LandCapBonus/p2ExtraOpeningDraws，曲线=ResourceCurve.ChallengeLandCurve）。
    /// 结果上报入口=TutorialFlow.ReportBattleResult 的 Challenge 分支（UI 层不自持进度状态）。
    /// </summary>
    public sealed class ChallengeProgressManager
    {
        /// <summary>难度档数（1..MaxDifficulty）。</summary>
        public const int MaxDifficulty = 9;

        private const string SAVE_FILE = "ChallengeProgress.json";

        private static ChallengeProgressManager _instance;

        /// <summary>运行时单例（真实存档路径）。</summary>
        public static ChallengeProgressManager Instance
            => _instance ??= new ChallengeProgressManager(Path.Combine(TidePaths.PersistentDataPath, SAVE_FILE));

        private readonly string _savePath;
        private ChallengeProgressData _data;

        /// <summary>进度变更事件（UI 战绩展示的刷新接线点）。</summary>
        public event Action OnProgressChanged;

        private ChallengeProgressManager(string savePath)
        {
            _savePath = savePath;
            LoadOrCreate();
        }

        /// <summary>验证口：按指定存档路径构造独立实例（编辑器验证器/无头门禁用临时文件）。</summary>
        public static ChallengeProgressManager CreateForVerification(string savePath)
            => new ChallengeProgressManager(savePath);

        // ======================================== 查询 ========================================

        /// <summary>最高通关难度（0=尚未通关任何档）。</summary>
        public int BestCleared => _data.bestCleared;

        /// <summary>某难度的胜场数（越界=0）。</summary>
        public int Wins(int difficulty) => GetRecord(difficulty)?.wins ?? 0;

        /// <summary>某难度的负场数（越界=0）。</summary>
        public int Losses(int difficulty) => GetRecord(difficulty)?.losses ?? 0;

        /// <summary>某难度战绩文本（UI 展示口径：「3胜1负」/「未挑战」）。</summary>
        public string RecordText(int difficulty)
        {
            var r = GetRecord(difficulty);
            return r == null ? "未挑战" : $"{r.wins}胜{r.losses}负";
        }

        private DifficultyRecord GetRecord(int difficulty)
            => difficulty >= 1 && difficulty <= MaxDifficulty
                ? _data.records.FirstOrDefault(r => r != null && r.difficulty == difficulty)
                : null;

        // ======================================== 写入 ========================================

        /// <summary>对局结果落档（TutorialFlow.ReportBattleResult 的 Challenge 分支调用；
        /// 难度越界忽略——BattleEntry.ChallengeDifficulty 缺省/残留的防线）。</summary>
        public void RecordResult(int difficulty, bool playerWon)
        {
            var r = GetRecord(difficulty);
            if (r == null) return;
            if (playerWon)
            {
                r.wins++;
                if (difficulty > _data.bestCleared)
                    _data.bestCleared = difficulty;
            }
            else
            {
                r.losses++;
            }
            SaveAndNotify();
        }

        private void SaveAndNotify()
        {
            SaveState();
            TideLog.Info($"[ChallengeProgress] 战绩更新：best={_data.bestCleared}，"
                         + string.Join("，", _data.records.Where(r => r != null && (r.wins > 0 || r.losses > 0))
                             .Select(r => $"D{r.difficulty}={r.wins}胜{r.losses}负")));
            OnProgressChanged?.Invoke();
        }

        // ======================================== 存取 ========================================

        private void LoadOrCreate()
        {
            try
            {
                if (File.Exists(_savePath))
                {
                    var loaded = TideJson.FromJson<ChallengeProgressData>(File.ReadAllText(_savePath));
                    if (loaded != null)
                    {
                        _data = loaded;
                        _data.records ??= new List<DifficultyRecord>();
                        EnsureRecordRows();
                        return;
                    }
                    TideLog.Warn("[ChallengeProgress] 存档不可解析，按缺档重建");
                }
            }
            catch (Exception ex)
            {
                TideLog.Error($"[ChallengeProgress] 读档失败 - {ex.Message}（按缺档重建）");
            }

            _data = new ChallengeProgressData();
            EnsureRecordRows();
            SaveState();
            TideLog.Info($"[ChallengeProgress] 首次建档：{_savePath}");
        }

        /// <summary>补齐 1..9 的行（旧档/手改档缺行兼容；重复行保留首见）。</summary>
        private void EnsureRecordRows()
        {
            for (int d = 1; d <= MaxDifficulty; d++)
            {
                if (GetRecord(d) == null)
                    _data.records.Add(new DifficultyRecord { difficulty = d });
            }
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
                TideLog.Error($"[ChallengeProgress] 存档失败 - {ex.Message}");
            }
        }

        [Serializable]
        private class ChallengeProgressData
        {
            public int bestCleared;
            public List<DifficultyRecord> records = new List<DifficultyRecord>();
        }

        [Serializable]
        private class DifficultyRecord
        {
            public int difficulty;
            public int wins;
            public int losses;
        }
    }
}
