using System;
using System.Collections.Generic;
using CardCore;
using UnityEngine;

namespace Tide.HotUpdate
{
    /// <summary>音频总线（2026-11-22 音效与 BGM 系统计划 §2.3）：Master → Music/SFX/Voice。
    /// v1 设置面板只给 Music/Sfx 两条滑条；Voice（教学配音）随 Master 不单列（§4.5）。</summary>
    public enum AudioBus
    {
        Master,
        Music,
        Sfx,
        Voice
    }

    /// <summary>BGM 曲目 id（场景表 Audio.json 的 music[].id 同名映射）。</summary>
    public enum BgmId
    {
        None = 0,
        Menu,
        Battle
    }

    /// <summary>关键音效 id（Audio.json 的 sfx[].id 同名映射；计划 §0 四类：抽牌/命中/死亡/胜负）。</summary>
    public enum SfxId
    {
        None = 0,
        Draw,       // 抽牌
        HitLight,   // 命中·轻
        HitHeavy,   // 命中·重（按伤害量分档，阈值在表内）
        DeathUnit,  // 生物/永久物死亡
        DeathHero,  // 角色死亡（更重；同帧压制 DeathUnit）
        Win,        // 胜利
        Lose        // 失败
    }

    /// <summary>
    /// 音频系统配置表（Assets/Configs/Audio.json，计划 §4.4 数据驱动口径）：
    /// 场景→曲目（screens）、曲目→文件（music）、音效→文件（sfx）、命中分档阈值（hitHeavyThreshold）、
    /// 教学压音量（tutorialDuckDb）、启动预载清单（preload）全在表里——**改曲/换音效不改代码**。
    /// 素材未入库（A 线未跑）=文件名落空 → 加载失败静默（§4.2），系统零阻塞。
    /// 序列化走 TideJson（public 字段 + List，JsonUtility 口径——不用 Dictionary）。
    /// 相对路径常量与其它配置表同源（TimingWeights/ValueSystemRuntimeConfig 同款）。
    /// </summary>
    public sealed class AudioTable
    {
        /// <summary>配置文件相对路径（TidePaths.DataPath 相对；YooAsset 地址=文件名 "Audio"）。</summary>
        public const string ConfigRelativePath = "Configs/Audio.json";

        public List<MusicEntry> music = new List<MusicEntry>();
        public List<ScreenEntry> screens = new List<ScreenEntry>();
        public List<SfxEntry> sfx = new List<SfxEntry>();

        /// <summary>启动异步预载的音效 id 清单（§4.2：启动异步预载，战斗主链零同步加载）。</summary>
        public List<string> preload = new List<string>();

        /// <summary>命中分档阈值：AtomicDamage/CombatDamage ≥ 此值播 HitHeavy，否则 HitLight。</summary>
        public float hitHeavyThreshold = 3f;

        /// <summary>教学局音乐压低（dB，负值；§4.4 教学沿用战斗曲并压音量）。</summary>
        public float tutorialDuckDb = -6f;

        public sealed class MusicEntry
        {
            /// <summary>BgmId 枚举名（Menu/Battle…）。</summary>
            public string id;
            /// <summary>主循环段文件名（Assets/Audio/Music/ 下不含扩展名）。</summary>
            public string clip;
            /// <summary>一次性前奏段（战斗曲 intro→loop；null=直接循环 clip）。</summary>
            public string intro;
            /// <summary>终局收尾段（null=终局纯淡出）。</summary>
            public string outro;
            /// <summary>进屏淡入秒数（§4.4 进屏淡入 1s）。</summary>
            public float fadeInSec = 1f;
        }

        public sealed class ScreenEntry
        {
            /// <summary>屏类名（UIScreen.GetType().Name，如 MainMenuScreen/BattleScreen）。</summary>
            public string screen;
            /// <summary>曲目 id（BgmId 名）；未配置的屏=维持当前曲不切（§4.4 同曲延续）。</summary>
            public string music;
        }

        public sealed class SfxEntry
        {
            /// <summary>SfxId 枚举名（Draw/HitLight…）。</summary>
            public string id;
            /// <summary>音频文件名（Assets/Audio/SFX/ 下不含扩展名，如 sfx_draw_01）。</summary>
            public string clip;
        }

        // ================= 查询口（表内容转枚举，未配置一律 None/null 静默） =================

        /// <summary>屏类名 → 曲目；未配置返回 None（调用方维持现状不切曲）。</summary>
        public BgmId MusicForScreen(string screenName)
        {
            var hit = screens?.Find(s => s.screen == screenName);
            return hit != null && Enum.TryParse(hit.music, out BgmId id) ? id : BgmId.None;
        }

        /// <summary>曲目 → 表项；未配置返回 null。</summary>
        public MusicEntry Music(BgmId id) => music?.Find(m => m.id == id.ToString());

        /// <summary>音效 → 文件名；未配置返回 null。</summary>
        public string ClipFor(SfxId id)
        {
            var hit = sfx?.Find(s => s.id == id.ToString());
            return hit?.clip;
        }

        // ================= 加载（缓存 + 容错：表缺失=空表静默运行） =================

        private static AudioTable _cached;

        /// <summary>加载并缓存配置（失败/缺失=空表——空表=不播任何曲/音效，不抛错）。
        /// 与 LoadText 直读语义配套：编辑器直改 json 后调 <see cref="Reload"/> 免重启。</summary>
        public static AudioTable Load()
        {
            if (_cached != null) return _cached;
            try
            {
                var raw = TidePaths.ReadConfigText(ConfigRelativePath);
                _cached = string.IsNullOrEmpty(raw) ? new AudioTable() : TideJson.FromJson<AudioTable>(raw);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Audio] Audio.json 解析失败，音频系统按空表静默运行：{e.Message}");
                _cached = new AudioTable();
            }
            return _cached;
        }

        /// <summary>丢弃缓存（编辑器直改 json 后 Reload 用）。</summary>
        public static void Reload() => _cached = null;
    }
}
