using System.Collections.Generic;
using UnityEngine;
// Unity 6000.5：AudioMixer 族迁入 UnityEngine.Audio 子命名空间（仍在 AudioModule.dll）——
// 别名引用，本文件内维持 Unity 传统短名写法。
using AudioMixer = UnityEngine.Audio.AudioMixer;
using AudioMixerGroup = UnityEngine.Audio.AudioMixerGroup;

namespace Tide.HotUpdate
{
    /// <summary>
    /// 音频播放服务（2026-11-22 音效与 BGM 系统计划 B1/B3）：SFX 池化 + BGM 双源 crossfade +
    /// 音乐/音效分轨音量 + PlayerPrefs 持久化。
    ///
    /// 路由（§2.3）：TideAudioMixer（Master→Music/SFX/Voice，exposed：MasterVol/MusicVol/SfxVol/VoiceVol）
    /// ——音量走 exposed 参数，线性 0..1 → dB（20*log10(max(v,1e-4))）。混音器资产缺失（Audio 组未装）
    /// 时回落纯 C# 音量乘算（source.volume），两条路径行为等价，加载失败一律 Debug.Log 静默不阻塞（§4.2）。
    /// 暂停（§2.6）：沿用 BattlePauseBus 的 AudioListener.pause——BGM/SFX 随暂停停、恢复继续（不从头）；
    /// 教学配音 ignoreListenerPause 语义不动。crossfade 用 unscaled 时间（timeScale=0 也能收尾）。
    /// 素材：YooAsset Audio 组（Assets/Audio，地址=文件名，§2.7）；BGM 懒加载、SFX 按表预载。
    /// 生命周期：UIBootstrap.OnEnable 经 <see cref="Boot"/> 建常驻 GameObject（DontDestroyOnLoad）。
    /// </summary>
    public sealed class AudioService : MonoBehaviour
    {
        // ================= 资产与持久化常量（§4.1/§4.2） =================

        /// <summary>混音器资产名（Assets/Audio/ 下不含扩展名；YooAsset 地址同名）。</summary>
        public const string MixerAssetName = "TideAudioMixer";
        private const string MixerAssetPath = "Assets/Audio/TideAudioMixer.mixer";
        private const string MusicFolder = "Assets/Audio/Music/"; // ogg（streaming 导入，§2.2）
        private const string SfxFolder = "Assets/Audio/SFX/";     // wav 16bit（§2.2）

        /// <summary>exposed 参数名（与 TideAudioMixer.mixer 内一致，改一处须同步另一处）。</summary>
        private const string ParamMaster = "MasterVol";
        private const string ParamMusic = "MusicVol";
        private const string ParamSfx = "SfxVol";
        private const string ParamVoice = "VoiceVol";

        private const string PrefKeyMusic = "audio.music";
        private const string PrefKeySfx = "audio.sfx";
        private const string PrefKeyMute = "audio.mute";
        private const float DefaultMusicVol = 0.6f; // §4.1 默认音量
        private const float DefaultSfxVol = 0.8f;
        private const float MuteDb = -80f;
        /// <summary>写入防抖（§4.1：0.2s + Save）。</summary>
        private const float PrefDebounceSec = 0.2f;
        /// <summary>同类音效最小重播间隔（unscaled 秒，§4.3）：同帧叠爆与回放快进机关枪同一闸。</summary>
        private const float SfxMinInterval = 0.1f;

        // ================= 单例 =================

        public static AudioService Instance { get; private set; }

        /// <summary>启动初始化（UIBootstrap.OnEnable 调）：常驻 GameObject + 服务组件；幂等。
        /// 域重载防护见 <see cref="ResetPlayStatics"/>（BattlePauseBus 同口径）。</summary>
        public static void Boot()
        {
            if (Instance != null) return; // Unity 假 null：上轮 Play 残留实例判死重建
            var go = new GameObject("AudioService");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<AudioService>();
        }

        /// <summary>进 Play 清静态（工程关域重载防护；BattleTileFx/BattlePauseBus 同口径）。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetPlayStatics()
        {
            Instance = null;
        }

        // ================= 运行态 =================

        private AudioMixer _mixer;
        private AudioMixerGroup _groupMusic;
        private AudioMixerGroup _groupSfx;
        private AudioMixerGroup _groupVoice;

        private readonly AudioSource[] _sfx = new AudioSource[8]; // §4.2 八路池
        private int _sfxCursor;
        private readonly Dictionary<SfxId, float> _sfxLastAt = new Dictionary<SfxId, float>();
        private readonly HashSet<string> _missLogged = new HashSet<string>(); // 素材缺失每名只记一次（A 线未跑属常态）

        private AudioSource _musicA;
        private AudioSource _musicB;
        private AudioSource _active;       // 当前主音乐源（另一路候场）
        private BgmId _current = BgmId.None;
        private string _pendingLoopClip;   // intro 播完要接的 loop 文件名（null=无 intro 或非战斗链）

        // crossfade 状态（unscaled 时间驱动）
        private AudioSource _fadeFrom;
        private AudioSource _fadeTo;
        private float _fadeT;
        private float _fadeDur;
        private float _fadeFromGain;      // 起始增益快照（逐帧对当前值 Lerp 会帧率依赖且到不了 0）
        private float _fadeToGain;

        // 音量态（0..1 线性；分轨见 §2.3）
        private float _musicVol = DefaultMusicVol;
        private float _sfxVol = DefaultSfxVol;
        private bool _mute;
        private float _musicDuckDb;        // 场景压音量（教学 -6dB，§4.4）
        private float _prefDirtyAt = -1f;  // -1=无待写

        public bool Mute => _mute;

        /// <summary>静音读口（设置面板文案用；服务未起=false）。</summary>
        public static bool MuteOn => Instance != null && Instance._mute;

        /// <summary>Voice 总线组（教学配音路由用）；混音器缺失=null（等同直连，不阻塞配音）。</summary>
        public static AudioMixerGroup VoiceGroup => Instance != null ? Instance._groupVoice : null;

        private void Awake()
        {
            // 混音器装载（失败静默回落纯 C# 音量乘算）
            _mixer = HotUpdateAssets.Load<AudioMixer>(MixerAssetName, MixerAssetPath);
            if (_mixer != null)
            {
                BindGroup("Music", ref _groupMusic);
                BindGroup("SFX", ref _groupSfx);
                BindGroup("Voice", ref _groupVoice);
            }
            else
            {
                Debug.Log("[Audio] TideAudioMixer 未装载——音量回落 source.volume 乘算（Audio 组未打包或资产缺失）");
            }

            // 音乐双源（A/B 交叉淡入淡出，§4.2）
            _musicA = MakeSource("music-a", _groupMusic);
            _musicB = MakeSource("music-b", _groupMusic);

            // SFX 八路池
            for (int i = 0; i < _sfx.Length; i++)
                _sfx[i] = MakeSource("sfx-" + i, _groupSfx);

            // 音量持久化读取（§4.1：启动读取并 SetFloat 到 mixer）
            _musicVol = PlayerPrefs.GetFloat(PrefKeyMusic, DefaultMusicVol);
            _sfxVol = PlayerPrefs.GetFloat(PrefKeySfx, DefaultSfxVol);
            _mute = PlayerPrefs.GetInt(PrefKeyMute, 0) != 0;
            ApplyVolumes();

            // SFX 预载（§4.2：启动异步预载——让出首帧再同步 Load，不占启动/战斗主链）
            PreloadSfxAsync().Forget();
        }

        private AudioSource MakeSource(string childName, AudioMixerGroup group)
        {
            var child = transform.Find(childName);
            if (child == null)
            {
                var go = new GameObject(childName);
                go.transform.SetParent(transform, false);
                child = go.transform;
            }
            var src = child.GetComponent<AudioSource>();
            if (src == null) src = child.gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            src.clip = null;
            src.outputAudioMixerGroup = group; // null=直连（混音器缺失路径）
            return src;
        }

        private void BindGroup(string groupName, ref AudioMixerGroup slot)
        {
            var groups = _mixer.FindMatchingGroups(groupName);
            if (groups != null && groups.Length > 0) slot = groups[0];
        }

        private async Cysharp.Threading.Tasks.UniTaskVoid PreloadSfxAsync()
        {
            await Cysharp.Threading.Tasks.UniTask.Yield();
            var table = AudioTable.Load();
            if (table.preload == null) return;
            foreach (var id in table.preload)
            {
                if (!System.Enum.TryParse(id, out SfxId sid)) continue;
                LoadSfxClip(sid); // 缓存填充；缺失记一次日志后不再尝试
            }
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            // crossfade 推进（unscaled：timeScale=0 / 暂停也能收尾，§4.2）
            if (_fadeDur > 0f && (_fadeTo != null || _fadeFrom != null))
            {
                _fadeT += dt;
                float k = Mathf.Clamp01(_fadeT / _fadeDur);
                if (_fadeFrom != null) _fadeFrom.volume = Mathf.Lerp(_fadeFromGain, 0f, k);
                if (_fadeTo != null) _fadeTo.volume = Mathf.Lerp(0f, _fadeToGain, k);
                if (k >= 1f)
                {
                    if (_fadeFrom != null) { _fadeFrom.Stop(); _fadeFrom = null; }
                    _fadeTo = null;
                    _fadeDur = 0f;
                }
            }

            // 战斗曲 intro → loop 链（§4.4：intro 播完接 loop；AudioListener.pause 期 isPlaying
            // 冻结为暂停态，不视作自然播完，防暂停误触发换段）
            if (_pendingLoopClip != null && _active != null && !_active.isPlaying
                && !AudioListener.pause && _active.clip != null)
            {
                var loopClip = LoadClip(MusicFolder, _pendingLoopClip);
                if (loopClip != null)
                {
                    _active.clip = loopClip;
                    _active.loop = true;
                    _active.volume = MusicSourceVolume;
                    _active.Play();
                }
                _pendingLoopClip = null; // loop 缺失也不再重试（A 线未跑属常态）
            }

            // 音量写入防抖落盘（§4.1）
            if (_prefDirtyAt >= 0f && Time.unscaledTime - _prefDirtyAt >= PrefDebounceSec)
            {
                PlayerPrefs.Save();
                _prefDirtyAt = -1f;
            }
        }

        // ================= SFX（§4.2 API + §4.3 接线口径） =================

        /// <summary>播关键音效（池化八路轮转）。同类 0.1s（unscaled）内只播一次——同帧多抽/
        /// 回放快进同一闸（§4.3）。素材未入库/未配置=静默。</summary>
        public static void PlaySfx(SfxId id, float volumeScale = 1f)
        {
            var self = Instance;
            if (self == null || id == SfxId.None) return;

            float now = Time.unscaledTime;
            if (self._sfxLastAt.TryGetValue(id, out float last) && now - last < SfxMinInterval) return;
            self._sfxLastAt[id] = now;

            var clip = self.LoadSfxClip(id);
            if (clip == null) return;

            var src = self._sfx[self._sfxCursor];
            self._sfxCursor = (self._sfxCursor + 1) % self._sfx.Length;
            src.PlayOneShot(clip, volumeScale * self.SfxGain01);
        }

        /// <summary>直接播一个 clip（调试/探针口；不走表与节流闸）。</summary>
        public static void PlayClip(AudioClip clip, float volumeScale = 1f)
        {
            var self = Instance;
            if (self == null || clip == null) return;
            var src = self._sfx[self._sfxCursor];
            self._sfxCursor = (self._sfxCursor + 1) % self._sfx.Length;
            src.PlayOneShot(clip, volumeScale * self.SfxGain01);
        }

        private readonly Dictionary<SfxId, AudioClip> _sfxCache = new Dictionary<SfxId, AudioClip>();

        private AudioClip LoadSfxClip(SfxId id)
        {
            if (_sfxCache.TryGetValue(id, out var cached)) return cached;
            var name = AudioTable.Load().ClipFor(id);
            var clip = name != null ? LoadClip(SfxFolder, name) : null;
            _sfxCache[id] = clip; // null 也缓存：缺失不反复加载
            return clip;
        }

        private readonly Dictionary<string, AudioClip> _musicCache = new Dictionary<string, AudioClip>();

        private AudioClip LoadClip(string folder, string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (_musicCache.TryGetValue(folder + name, out var cached)) return cached;
            // 双扩展试载（wav/ogg——切片规格 §2.2：SFX=wav、BGM=ogg；先按各自规格一发，再互换兜底）
            var clip = HotUpdateAssets.Load<AudioClip>(name, folder + name + ".wav");
            if (clip == null) clip = HotUpdateAssets.Load<AudioClip>(name, folder + name + ".ogg");
            if (clip == null && _missLogged.Add(folder + name))
                Debug.Log($"[Audio] 音频缺失（静默，A 线素材未入库属常态）：{folder}{name}");
            _musicCache[folder + name] = clip;
            return clip;
        }

        // ================= BGM（§4.2/§4.4） =================

        /// <summary>切曲（双源 crossfade）。同曲在播=no-op（§4.4 同曲延续不重启）。
        /// fadeSec&lt;0=用表内 fadeInSec。曲目未配置=淡出停曲。</summary>
        public static void PlayMusic(BgmId id, float fadeSec = -1f)
        {
            var self = Instance;
            if (self == null) return;
            if (id == BgmId.None)
            {
                StopMusic(1f);
                return;
            }
            if (id == self._current)
            {
                return; // 同曲延续
            }

            var entry = AudioTable.Load().Music(id);
            if (entry == null)
            {
                Debug.Log($"[Audio] 曲目未配置（Audio.json music 缺 id={id}），维持现状");
                return;
            }

            var introClip = self.LoadClip(MusicFolder, entry.intro);
            var mainClip = self.LoadClip(MusicFolder, entry.clip);
            if (introClip == null && mainClip == null) return; // 素材全缺：不切曲（避免静默杀掉在播曲）

            var src = self._active == self._musicA ? self._musicB : self._musicA;
            src.clip = introClip != null ? introClip : mainClip;
            src.loop = introClip == null; // intro 一次性；播完由 Update 接 loop
            self._pendingLoopClip = introClip != null ? entry.clip : null;
            self._current = id;
            self.StartFade(src, fadeSec >= 0f ? fadeSec : entry.fadeInSec);
        }

        /// <summary>停曲（淡出；unscaled 推进）。</summary>
        public static void StopMusic(float fadeSec = 1f)
        {
            var self = Instance;
            if (self == null) return;
            self._current = BgmId.None;
            self._pendingLoopClip = null;
            if (self._active == null) return;
            self.StartFade(null, fadeSec);
        }

        /// <summary>终局收尾（§4.4）：淡出当前曲；表里配了 outro 段则淡入 outro 一次性播完，
        /// 未配/素材缺=纯淡出。</summary>
        public static void PlayOutro(float fadeSec = 1.5f)
        {
            var self = Instance;
            if (self == null) return;
            var entry = AudioTable.Load().Music(self._current);
            self._current = BgmId.None;
            self._pendingLoopClip = null;
            var outroClip = entry != null ? self.LoadClip(MusicFolder, entry.outro) : null;
            if (outroClip != null)
            {
                var src = self._active == self._musicA ? self._musicB : self._musicA;
                src.clip = outroClip;
                src.loop = false;
                self.StartFade(src, fadeSec);
            }
            else if (self._active != null)
            {
                self.StartFade(null, fadeSec);
            }
        }

        /// <summary>场景压音量（教学局 -6dB，§4.4；回菜单清 0）。dB 口径，0=不压。</summary>
        public static void SetMusicDuck(float duckDb)
        {
            var self = Instance;
            if (self == null || Mathf.Approximately(self._musicDuckDb, duckDb)) return;
            self._musicDuckDb = duckDb;
            self.ApplyVolumes();
        }

        private void StartFade(AudioSource to, float dur)
        {
            _fadeFrom = _active;
            _fadeFromGain = _fadeFrom != null ? _fadeFrom.volume : 0f;
            _fadeTo = to;
            _fadeT = 0f;
            _fadeDur = Mathf.Max(0.05f, dur);
            _fadeToGain = MusicSourceVolume;
            _active = to;
            if (to != null)
            {
                to.volume = 0f;
                to.Play();
            }
            // to=null（纯淡出）：_fadeFrom 在 Update 里归零后 Stop
        }

        /// <summary>音乐源应携带的音量：mixer 路径恒 1（分轨音量由 MusicVol 参数管，源上再乘
        /// 会双重衰减）；回落路径=实际增益。</summary>
        private float MusicSourceVolume => _mixer != null ? 1f : MusicGain01;

        // ================= 音量（§4.1/§4.2 API） =================

        /// <summary>设总线音量（线性 0..1）：走 mixer exposed 参数（dB），回落路径改增益乘算。
        /// 写入防抖 0.2s 后 PlayerPrefs.Save（§4.1）。</summary>
        public static void SetBusVolume(AudioBus bus, float v01)
        {
            var self = Instance;
            if (self == null) return;
            v01 = Mathf.Clamp01(v01);
            switch (bus)
            {
                case AudioBus.Music: self._musicVol = v01; break;
                case AudioBus.Sfx: self._sfxVol = v01; break;
                default: return; // Master/Voice v1 不单列（§4.5：主音量滑条不单列、Voice 随 Master）
            }
            self.ApplyVolumes();
            self.MarkPrefDirty();
        }

        public static float GetBusVolume(AudioBus bus)
        {
            var self = Instance;
            if (self == null) return 0f;
            switch (bus)
            {
                case AudioBus.Music: return self._musicVol;
                case AudioBus.Sfx: return self._sfxVol;
                default: return 1f;
            }
        }

        /// <summary>静音开关（Master 闸：mixer MasterVol=-80dB / 回落路径乘 0）。</summary>
        public static void SetMute(bool mute)
        {
            var self = Instance;
            if (self == null || self._mute == mute) return;
            self._mute = mute;
            self.ApplyVolumes();
            self.MarkPrefDirty();
        }

        private void MarkPrefDirty()
        {
            PlayerPrefs.SetFloat(PrefKeyMusic, _musicVol);
            PlayerPrefs.SetFloat(PrefKeySfx, _sfxVol);
            PlayerPrefs.SetInt(PrefKeyMute, _mute ? 1 : 0);
            _prefDirtyAt = Time.unscaledTime; // 防抖：0.2s 无新写才 Save（§4.1）
        }

        /// <summary>线性 0..1 → dB（§2.3 公式；0 下沉 -80dB 免对数负无穷）。</summary>
        private static float LinearToDb(float v) => v <= 0.0001f ? MuteDb : 20f * Mathf.Log10(v);

        /// <summary>回落路径的音乐增益（乘 duck 线性值）。</summary>
        private float MusicGain01 => _mute ? 0f : _musicVol * DbToLinear01(_musicDuckDb);

        private float SfxGain01 => _mute ? 0f : _sfxVol;

        private static float DbToLinear01(float db) => db <= -79f ? 0f : Mathf.Pow(10f, db / 20f);

        private void ApplyVolumes()
        {
            if (_mixer != null)
            {
                _mixer.SetFloat(ParamMaster, _mute ? MuteDb : 0f);
                _mixer.SetFloat(ParamMusic, LinearToDb(_musicVol) + _musicDuckDb);
                _mixer.SetFloat(ParamSfx, LinearToDb(_sfxVol));
                _mixer.SetFloat(ParamVoice, 0f); // Voice 随 Master（§2.3）
                return;
            }
            // 回落路径：直接改在播源的音量（mixer 缺失时等价生效）；crossfade 在途时让位
            // （Update 按快照增益推进，此处覆写会打断淡入淡出斜坡）
            if (_fadeDur > 0f) return;
            if (_musicA != null && _musicA.isPlaying) _musicA.volume = MusicGain01;
            if (_musicB != null && _musicB.isPlaying) _musicB.volume = MusicGain01;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
