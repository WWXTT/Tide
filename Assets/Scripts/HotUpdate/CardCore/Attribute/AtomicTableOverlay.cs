using System;
using System.Collections.Generic;
using System.IO;

namespace CardCore.Attribute
{
    /// <summary>
    /// 原子表工坊·玩家改价覆盖层（2026-10-06）：持久化玩家对原子表总价锚价的修改，落
    /// persistentDataPath/AtomicTableOverlay.json（仿 TutorialProgressManager 范式：惰性单例 +
    /// TideJson + 每变更即写）。只存「hashId → 总价」的偏离行，基线永远是包内
    /// Configs/AttributeValueConfig.json（仓库真相源绝不被运行时改写）。
    ///
    /// 生效模型：表状态 = 基线 JSON + overlay 行（经 AtomicEffectTable.TrySetRowTotal 等比分摊落表）。
    /// EnsureApplied 按 AtomicEffectTable.Version 幂等重放——Reload 重建后版本跳变，
    /// 下次 EnsureApplied 自动重放（合成器「原子表已重读」不冲掉玩家改价）；
    /// 教学局例外：进入前显式只 Reload 不重放（教学钉死数学不受改价影响），退出后由
    /// UI 侧协调器（AtomicTableWorkshop）恢复重放 + 重推卡费。
    /// 改价后的卡费刷新走 CardCostService.ReforceSuggestedCosts（UI 侧协调，CardCore 不依赖卡池）。
    /// </summary>
    public sealed class AtomicTableOverlay
    {
        private const string SAVE_FILE = "AtomicTableOverlay.json";

        private static AtomicTableOverlay _instance;

        /// <summary>运行时单例（真实存档路径；首启缺档=空覆盖层）。</summary>
        public static AtomicTableOverlay Instance
            => _instance ??= new AtomicTableOverlay(Path.Combine(TidePaths.PersistentDataPath, SAVE_FILE));

        private readonly string _savePath;
        private bool _enabled;
        private readonly Dictionary<string, float> _rows = new Dictionary<string, float>();
        private bool _loaded;
        private int _appliedVersion = -1; // 已重放到哪个表代际（-1=从未应用）

        private AtomicTableOverlay(string savePath)
        {
            _savePath = savePath;
        }

        /// <summary>验证口：按指定存档路径构造独立实例（不碰真实存档）。</summary>
        public static AtomicTableOverlay CreateForVerification(string savePath)
            => new AtomicTableOverlay(savePath);

        // ======================================== 查询 ========================================

        /// <summary>覆盖层总开关（关闭=纯基线，行保留在档里不丢）。</summary>
        public bool Enabled => LoadOnce() && _enabled;

        /// <summary>偏离行（hashId → 总价）。键序不保证，UI 展示按表书写序。</summary>
        public IReadOnlyDictionary<string, float> Rows
        {
            get { LoadOnce(); return _rows; }
        }

        // ======================================== 写入（每变更即写） ========================================

        /// <summary>总开关落档（表侧生效由协调器负责：开=EnsureApplied，关=Reload 回基线）。</summary>
        public void SetEnabled(bool enabled)
        {
            LoadOnce();
            if (_enabled == enabled) return;
            _enabled = enabled;
            Save();
        }

        /// <summary>记一行改价（合法性由 AtomicEffectTable.TrySetRowTotal 写入时权威校验，
        /// 此处只存档；同 hashId 重复写=覆盖）。</summary>
        public void SetRow(string hashId, float total)
        {
            LoadOnce();
            _rows[hashId] = total;
            Save();
        }

        /// <summary>删一行偏离（恢复该行默认=回到基线值；表侧当前值由协调器重放刷新）。</summary>
        public void RemoveRow(string hashId)
        {
            LoadOnce();
            if (_rows.Remove(hashId)) Save();
        }

        /// <summary>清空全部偏离行（「恢复默认」；enabled 保留原值——无行时开关无效果）。</summary>
        public void ClearRows()
        {
            LoadOnce();
            if (_rows.Count == 0) return;
            _rows.Clear();
            Save();
        }

        // ======================================== 表侧重放 ========================================

        /// <summary>幂等重放：enabled 且表代际已前进（首次应用 / Reload 重建）时把偏离行落表。
        /// 幂等口径：逐行 TrySetRowTotal（等比分摊，同值重放不改变表内容），失败行只告警不中断。</summary>
        public void EnsureApplied()
        {
            LoadOnce();
            if (!_enabled || _rows.Count == 0) return;
            if (_appliedVersion == AtomicEffectTable.Version) return;
            ApplyToTable();
        }

        /// <summary>无条件把全部偏离行落表（教学局退出恢复用；验证器直调）。</summary>
        public void ApplyToTable()
        {
            LoadOnce();
            foreach (var kv in _rows)
            {
                if (!AtomicEffectTable.TrySetRowTotal(kv.Key, kv.Value))
                    TideLog.Warn($"[AtomicTableOverlay] 行 {kv.Key} 无法改价为 {kv.Value}（不存在/不计价/越界），跳过");
            }
            _appliedVersion = AtomicEffectTable.Version;
        }

        // ======================================== 存取 ========================================

        private bool LoadOnce()
        {
            if (_loaded) return true;
            _loaded = true;
            try
            {
                if (File.Exists(_savePath))
                {
                    var data = TideJson.FromJson<OverlayData>(File.ReadAllText(_savePath));
                    if (data != null)
                    {
                        _enabled = data.enabled;
                        if (data.rows != null)
                            foreach (var r in data.rows)
                                if (r != null && !string.IsNullOrEmpty(r.hashId))
                                    _rows[r.hashId] = r.total;
                        return true;
                    }
                    TideLog.Warn("[AtomicTableOverlay] 存档不可解析，按空覆盖层处理");
                }
            }
            catch (Exception ex)
            {
                TideLog.Error($"[AtomicTableOverlay] 读档失败 - {ex.Message}（按空覆盖层处理）");
            }
            return true;
        }

        private void Save()
        {
            try
            {
                var data = new OverlayData { enabled = _enabled };
                foreach (var kv in _rows)
                    data.rows.Add(new OverlayRow { hashId = kv.Key, total = kv.Value });
                var directory = Path.GetDirectoryName(_savePath);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllText(_savePath, TideJson.ToJson(data, true));
            }
            catch (Exception ex)
            {
                TideLog.Error($"[AtomicTableOverlay] 存档失败 - {ex.Message}");
            }
        }

        [Serializable]
        private class OverlayData
        {
            public bool enabled;
            public List<OverlayRow> rows = new List<OverlayRow>();
        }

        [Serializable]
        private class OverlayRow
        {
            public string hashId;
            public float total;
        }
    }
}
