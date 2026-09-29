using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;


namespace CardCore
{
    /// <summary>
    /// 元素解锁管理器
    /// 管理元素类型的解锁状态（白/黑元素需要解锁）
    /// </summary>
    public class ElementUnlockManager
    {
        #region 单例

        private static ElementUnlockManager _instance;
        public static ElementUnlockManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new ElementUnlockManager();
                }
                return _instance;
            }
        }

        #endregion

        #region 常量

        /// <summary>解锁状态保存文件名</summary>
        private const string SAVE_FILE = "ElementUnlock.json";

        /// <summary>默认解锁的元素类型</summary>
        private static readonly ManaType[] DefaultUnlocked = new ManaType[]
        {
            ManaType.Gray, ManaType.Red, ManaType.Blue, ManaType.Green
        };

        #endregion

        #region 字段

        /// <summary>已解锁的元素类型集合</summary>
        private HashSet<ManaType> _unlockedManaTypes = new HashSet<ManaType>();

        /// <summary>保存路径</summary>
        private string _savePath;

        /// <summary>是否已加载</summary>
        public bool IsLoaded { get; private set; }

        /// <summary>解锁状态变更事件</summary>
        public event Action<ManaType> OnElementUnlocked;

        #endregion

        #region 属性

        /// <summary>
        /// 获取已解锁的元素类型列表
        /// </summary>
        public IReadOnlyCollection<ManaType> UnlockedManaTypes => _unlockedManaTypes;

        #endregion

        #region 构造函数

        private ElementUnlockManager()
        {
            _savePath = Path.Combine(Application.persistentDataPath, SAVE_FILE);
            LoadState();
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 检查元素是否已解锁
        /// </summary>
        /// <param name="manaType">元素类型</param>
        /// <returns>是否已解锁</returns>
        public bool IsUnlocked(ManaType manaType)
        {
            return _unlockedManaTypes.Contains(manaType);
        }

        /// <summary>
        /// 解锁元素
        /// </summary>
        /// <param name="manaType">要解锁的元素类型</param>
        /// <returns>是否解锁成功（如果已解锁则返回false）</returns>
        public bool Unlock(ManaType manaType)
        {
            if (_unlockedManaTypes.Contains(manaType))
            {
                return false;
            }

            _unlockedManaTypes.Add(manaType);
            SaveState();

            OnElementUnlocked?.Invoke(manaType);
            Debug.Log($"ElementUnlockManager: 解锁元素 {manaType}");
            return true;
        }

        /// <summary>
        /// 锁定元素（用于测试或重置）
        /// </summary>
        /// <param name="manaType">要锁定的元素类型</param>
        public void Lock(ManaType manaType)
        {
            _unlockedManaTypes.Remove(manaType);
            SaveState();
        }

        /// <summary>
        /// 获取所有可用的元素类型（已解锁的）
        /// </summary>
        /// <returns>可用的元素类型列表</returns>
        public List<ManaType> GetAvailableManaTypes()
        {
            return new List<ManaType>(_unlockedManaTypes);
        }

        /// <summary>
        /// 获取所有需要解锁的元素类型
        /// </summary>
        /// <returns>需要解锁的元素类型列表</returns>
        public List<ManaType> GetLockedManaTypes()
        {
            var result = new List<ManaType>();
            foreach (ManaType manaType in Enum.GetValues(typeof(ManaType)))
            {
                if (!_unlockedManaTypes.Contains(manaType))
                {
                    result.Add(manaType);
                }
            }
            return result;
        }

        /// <summary>
        /// 重置为默认状态（灰红蓝绿解锁，白黑锁定）
        /// </summary>
        public void ResetToDefault()
        {
            _unlockedManaTypes.Clear();
            foreach (var manaType in DefaultUnlocked)
            {
                _unlockedManaTypes.Add(manaType);
            }
            SaveState();
            Debug.Log("ElementUnlockManager: 重置为默认解锁状态");
        }

        /// <summary>
        /// 解锁所有元素
        /// </summary>
        public void UnlockAll()
        {
            foreach (ManaType manaType in Enum.GetValues(typeof(ManaType)))
            {
                _unlockedManaTypes.Add(manaType);
            }
            SaveState();
            Debug.Log("ElementUnlockManager: 解锁所有元素");
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 加载解锁状态
        /// </summary>
        private void LoadState()
        {
            try
            {
                // 初始化默认解锁
                foreach (var manaType in DefaultUnlocked)
                {
                    _unlockedManaTypes.Add(manaType);
                }

                // 尝试从文件加载
                if (File.Exists(_savePath))
                {
                    string json = File.ReadAllText(_savePath);
                    var data = JsonUtility.FromJson<ElementUnlockData>(json);

                    if (data != null && data.UnlockedTypes != null)
                    {
                        // 添加文件中保存的解锁类型
                        foreach (var typeValue in data.UnlockedTypes)
                        {
                            if (Enum.IsDefined(typeof(ManaType), typeValue))
                            {
                                _unlockedManaTypes.Add((ManaType)typeValue);
                            }
                        }
                    }
                }

                IsLoaded = true;
                Debug.Log($"ElementUnlockManager: 加载完成，已解锁 {_unlockedManaTypes.Count} 种元素");
            }
            catch (Exception ex)
            {
                Debug.LogError($"ElementUnlockManager: 加载失败 - {ex.Message}");
                // 确保默认解锁
                ResetToDefault();
                IsLoaded = true;
            }
        }

        /// <summary>
        /// 保存解锁状态
        /// </summary>
        private void SaveState()
        {
            try
            {
                var data = new ElementUnlockData
                {
                    UnlockedTypes = new List<int>()
                };

                foreach (var manaType in _unlockedManaTypes)
                {
                    data.UnlockedTypes.Add((int)manaType);
                }

                string json = JsonUtility.ToJson(data, true);

                // 确保目录存在
                string directory = Path.GetDirectoryName(_savePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(_savePath, json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"ElementUnlockManager: 保存失败 - {ex.Message}");
            }
        }

        #endregion

        #region 数据类

        /// <summary>
        /// 元素解锁数据（可序列化）
        /// </summary>
        [Serializable]
        private class ElementUnlockData
        {
            public List<int> UnlockedTypes;
        }

        #endregion
    }
}
