using SynergyUI; // UIScreen / BattleEntry / BattleMode

namespace Tide.HotUpdate
{
    /// <summary>
    /// 屏级音乐钩（2026-11-22 音效与 BGM 系统计划 §4.4/B3）：UIManager.Activate 在每屏装配后
    /// 调 <see cref="OnScreenActivated"/>，按 Audio.json 场景表切曲——菜单族同曲延续不重启
    /// （PlayMusic 同曲 no-op）、战斗进 intro→loop、教学局压 duck、未配置的屏维持现状不切曲。
    /// 终局/退场收尾不走本钩：胜负音与 outro 由 BattleAudioBridge 的 GameOver 接线负责。
    /// </summary>
    public static class ScreenMusic
    {
        /// <summary>UIManager.Activate 唯一调用点（BattleStageDirector.OnScreenActivated 同款口径）。</summary>
        public static void OnScreenActivated(UIScreen screen)
        {
            if (screen == null) return;
            var table = AudioTable.Load();
            var bgm = table.MusicForScreen(screen.GetType().Name);
            if (bgm == BgmId.None) return; // 未配置屏=维持现状（不杀曲不重启）

            // 教学局压音量（§4.4：教学沿用战斗曲，Music -6dB；回菜单清零）
            float duck = bgm == BgmId.Battle && BattleEntry.Mode == BattleMode.Tutorial
                ? table.tutorialDuckDb
                : 0f;
            AudioService.SetMusicDuck(duck);
            AudioService.PlayMusic(bgm); // 同曲在播=no-op（§4.4 同曲延续）
        }
    }
}
