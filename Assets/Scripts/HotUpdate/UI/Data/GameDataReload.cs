using CardCore;

namespace SynergyUI
{
    /// <summary>
    /// 全量数据重载口（2026-11-09 云存档改版定案）：本地存档文件被外部落盘（云恢复/导入对手卡组）
    /// 之后的确定性失效——不再依赖 CardCatalog/EffectsLibrary 的 mtime 盯盘（2 秒节流的间接刷新，
    /// 云恢复后存在读旧数据窗口）。写盘方落盘完成即调本口，下次访问必读新数据。
    /// 覆盖面与 ContentPackRegistry 内容包卸载路径同款（那边 InvalidateCaches 私有，此处同款
    /// 四件套直排）+ 三个进度管理器惰性单例重置（下次访问按新落盘数据重建）。
    /// 文件型序列化器（DeckSerializer/CardConfigSerializer 等）无内存态，无需处理。
    /// </summary>
    public static class GameDataReload
    {
        /// <summary>全量失效：内容包注册表/卡目录/效果库/词条表 + 教学/挑战/元素解锁进度单例。</summary>
        public static void ReloadAllCaches()
        {
            ContentPackRegistry.Invalidate();
            CardCatalog.Invalidate();
            EffectsLibrary.Reload();
            CompositeAtomTable.Invalidate();
            TutorialProgressManager.ResetForCloudRestore();
            ChallengeProgressManager.ResetForCloudRestore();
            ElementUnlockManager.ResetForCloudRestore();
        }
    }
}
