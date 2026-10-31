using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;

namespace SynergyUI
{
    /// <summary>
    /// 教学固定局配置（2026-10-02 教学模式地基）：一局教学的完整剧本——
    /// 双方卡组的**顺序即摸牌序列**（InitGame lockDeckOrder 锁牌库序：前 OpeningHandSize 张为起手、
    /// 之后逐回合摸牌恒取牌库顶）；机器人逐回合动作脚本由 ScriptedAi 按条执行；rngSeed=复现钉子
    /// （2026-10-20 定案：非 0=钉同局重放；0/缺省=每局新抽——效果随机换局不同，卡序/剧本仍确定）。
    /// 数据：Assets/Configs/ 按课拆档（2026-10-19 三课制）：TutorialConfig.json=第一课（basics 攻防）、
    /// TutorialConfig2.json=第二课（advanced 进阶机制）、TutorialConfig3.json=第三课（lunzhuan 末日降临
    /// Boss），TutorialLibrary 三档合并装载（TidePaths 寻址：编辑器直读、直改 JSON 后 Reload 免重启，
    /// 真机由宿主装配进热更包——同 AtomicEffectTable 惯例）。
    /// </summary>
    [Serializable]
    public class TutorialConfig
    {
        public string id;
        public string name;
        /// <summary>效果随机种子（一局一枚、双方共用、换局重抽）：非 0=复现钉子重放同局；
        /// 0/缺省=开局新抽（导演器记日志留种子号，回填即可复现）。</summary>
        public int rngSeed;
        /// <summary>玩家卡组引用文件名（2026-10-17 卡组文件化：读 StreamingAssets/Tutorial/{x}.json
        /// ——{name, cardIds} 红·快攻同款格式；id 引用即是卡组，同 id 出现多次即多张，可重复入组）。</summary>
        public string playerDeckFile;
        /// <summary>机器人卡组引用文件名（同上）。</summary>
        public string aiDeckFile;
        /// <summary>机器人脚本：turn=机器人的第几个回合（从 1 计，与引擎全局回合号无关）。</summary>
        public List<TutorialTurnScript> aiScript;

        /// <summary>预设场面（2026-10-06 教学直入）：非 null=不从第 1 回合空场开局，按场面直接站在
        /// startTurn 回合的操作环节（TutorialScenarioSeeder 静默注入；deck 字段此时只是备用——
        /// 组卡取 scenario 双方并集）。null=旧口径（deck 顺序=摸牌序列，从第 1 回合正常开局）。</summary>
        public TutorialScenario scenario;

        /// <summary>暂停引导步骤（2026-10-06 第一课改版）：非空=TutorialGuide 装闸——步骤未完成时
        /// 攻击/结束回合被钉住（错误目标与锁定生物拒绝、守卫候选只留引导守卫者），完成即推进，
        /// 全部完成引导退场。UI 高亮/提示后续接 OnStepAdvanced；本字段纯逻辑生效。</summary>
        public List<TutorialGuideStepSpec> guide;

        /// <summary>课规（2026-10-06 第二课）：该卡（对手场上卡 id）存活期间，玩家以对方角色为目标的
        /// 攻击宣言被教学闸拒绝（「先解决拦路巨兽」）——卡亡即解锁；法术直伤不在此列。
        /// 独立于引导步骤存活（步骤完成后仍生效，直到 TutorialGuide.End）。</summary>
        public string heroLockCard;

        /// <summary>跳过介绍版引导（2026-10-16 教学一两段式）：非空且玩家选择跳过第一大段时
        /// 用本链替代 guide（直入第二段攻防教学；配合 skipScenario 预设场面直入）。</summary>
        public List<TutorialGuideStepSpec> guideSkip;

        /// <summary>跳过介绍版预设场面（2026-10-16 教学一两段式）：非空且玩家选择跳过第一大段时
        /// 按本场面直入（StartTutorialGame 分流走 scenario 口径）；null=不支持跳过。</summary>
        public TutorialScenario skipScenario;

        /// <summary>后续段场面列表（2026-10-19 教学二多段化）：guide 里的 reveal=midTransition 步
        /// 依次换场——第 1 个换 skipScenario（段二），第 2 个起依次取本列表（段三a/段三b/…）。
        /// 空/缺=课程两段以内，多出的换场步会被跳过（场面空守卫）。</summary>
        public List<TutorialScenario> phaseScenarios;

        /// <summary>取机器人第 aiTurn 回合的剧本（无则 null——ScriptedAi 直接结束回合）。</summary>
        public TutorialTurnScript GetAiTurn(int aiTurn)
            => aiScript?.FirstOrDefault(t => t != null && t.turn == aiTurn);

        [NonSerialized] private List<string> _playerDeckIds;
        [NonSerialized] private List<string> _aiDeckIds;

        /// <summary>玩家卡组 id 序（读 deckFile 展开；仅非空结果缓存——文件缺/读失败的空表不粘死，
        /// 下次访问重读自愈。真机实录 2026-10-18：首启竞速空表被 ??= 缓存后整个会话教学恒回落普通局）。</summary>
        public List<string> PlayerDeckIds
            => DeckIdsCached(ref _playerDeckIds, playerDeckFile);

        /// <summary>机器人卡组 id 序（同上）。</summary>
        public List<string> AiDeckIds
            => DeckIdsCached(ref _aiDeckIds, aiDeckFile);

        /// <summary>卡组 id 缓存读（非空命中即用；空表/未读每次重读——失败态不落缓存）。</summary>
        private List<string> DeckIdsCached(ref List<string> cache, string deckFile)
        {
            if (cache == null || cache.Count == 0)
                cache = TutorialLibrary.LoadDeckIds(deckFile);
            return cache;
        }
    }

    /// <summary>机器人单回合脚本：动作按序执行；type=end 提前截断（脚本末尾隐含结束回合）。</summary>
    [Serializable]
    public class TutorialTurnScript
    {
        public int turn;
        public List<TutorialAction> actions;
    }

    /// <summary>
    /// 脚本动作（type 区分，全部经 GameActions 正规入口、引擎权威校验）：
    /// element——card 入元素池（2026-10-11 锁色：target 可选=锁定产色名，缺省主色；
    ///           地牌不耗竭，付费时系统按账单自动横置）；
    /// tap——已删（2026-10-11 现取现用：无回合初横置动作）；
    /// play——出 card（mode=抉择模式序号；targets=逗号分隔目标串，"hero"=对方英雄/卡 id=场上单位，
    ///           缺省或 "auto"=引擎自动解析）；
    /// attack——attacker（场上卡 id）宣言攻击 target（"hero"=对方英雄/卡 id=场上单位）；
    /// fallback=attack 首选目标不可解析（已死/已离场）时的回落目标（2026-10-17 考验：四小只打
    ///           6/6 主将、主将死后转攻角色）；skill——英雄技能；end——提前结束本回合（跳过剩余动作）；
    /// activate——场上卡 card 宣动主动效果（2026-10-19 第二课连锁教学：mode=第几个主动效果 0 起，
    ///           宣动后开响应窗等人类玩家连锁/让过，不立即排干栈）；
    /// phantom——幻卡清算（2026-10-18 教学二·AI 第一回合签名动作）：烧空己方全部区域（镜像卡组），
    ///           凭空召唤 card（末日降临，mode=紊乱 RushSickness 层数）+ target 仪典载体入场并直挂
    ///           targets 规则光环（如 FatigueImmunity）。实现见 ScriptedAi.ExecutePhantom。
    /// wallet——钱包灌入（2026-10-20 段六充盈钱包制）：给 side（"player"/"ai"，缺省 ai）的临时钱包
    ///           加 mode 个 target 色（颜色名缺省 Gray）元素——走 AddMana 官方口（事件可观测、
    ///           生成钳同口径）。用途：钱包任一回合结束清空，场景播种的 bank 跨不过回合边界——
    ///           须在实际消费的回合内（如 AI 连打前）重灌。
    /// </summary>
    [Serializable]
    public class TutorialAction
    {
        public string type;
        public string card;
        public string attacker;
        public string target;
        public int mode;
        public string targets;
        public string fallback;
        public string side;
    }

    /// <summary>
    /// 教学配置库（仿 AtomicEffectTable 惯例：静态表 + TidePaths.ReadConfigText + Reload 免重启）。
    /// 按课拆档（2026-10-19 三课制）：第一课 TutorialConfig.json + 第二课 TutorialConfig2.json +
    /// 第三课 TutorialConfig3.json，三档合并装载进同一 id 表——消费方（BattleScreen/TutorialFlow
    /// 按 id Get）零改动；跨档重复 id 保先装档并 Warn（与 EffectsLibrary 教学副表合并同口径）。
    /// Get 按 id 取条目；BuildDeck 把卡 id 序列解析为 CardData 卡组（缺失 id 记警告跳过，不炸局）。
    /// </summary>
    public static class TutorialLibrary
    {
        private static readonly string[] ConfigRelativePaths =
        {
            "Configs/TutorialConfig.json",  // 第一课（basics 攻防）
            "Configs/TutorialConfig2.json", // 第二课（advanced 进阶机制）
            "Configs/TutorialConfig3.json", // 第三课（lunzhuan 末日降临 Boss）
        };

        private static Dictionary<string, TutorialConfig> _byId;

        static TutorialLibrary() => Initialize();

        /// <summary>强制重读配置（外部直改 JSON 后免重启刷新）。</summary>
        public static void Reload() => Initialize();

        private static void Initialize()
        {
            _byId = new Dictionary<string, TutorialConfig>();
            foreach (var configPath in ConfigRelativePaths)
                LoadFile(configPath);
        }

        private static void LoadFile(string configPath)
        {
            try
            {
                string raw = TidePaths.ReadConfigText(configPath);
                if (string.IsNullOrEmpty(raw))
                {
                    TideLog.Warn($"[TutorialLibrary] 配置不可读：{configPath}");
                    return;
                }
                var file = TideJson.FromJson<TutorialConfigFile>(raw);
                if (file?.tutorials == null || file.tutorials.Count == 0)
                {
                    TideLog.Warn($"[TutorialLibrary] 未加载到任何教学条目：{configPath}");
                    return;
                }
                foreach (var t in file.tutorials)
                {
                    if (t == null || string.IsNullOrEmpty(t.id)) continue;
                    if (_byId.ContainsKey(t.id))
                    {
                        TideLog.Warn($"[TutorialLibrary] 重复教学条目 id（保先装档）：{t.id} @ {configPath}");
                        continue;
                    }
                    _byId[t.id] = t;
                }
            }
            catch (Exception e)
            {
                TideLog.Warn($"[TutorialLibrary] 加载 {configPath} 失败: {e.Message}");
            }
        }

        /// <summary>按 id 取教学条目（找不到返回 null，调用方自行回落）。</summary>
        public static TutorialConfig Get(string id)
            => string.IsNullOrEmpty(id) ? null : _byId.TryGetValue(id, out var t) ? t : null;

        /// <summary>教学/测试卡组引用文件 → 卡 id 序（2026-10-17 卡组文件化：{name, cardIds} 红·快攻
        /// 同款 DeckData 格式——id 引用即是卡组，同 id 多次出现即多张，全面支持重复卡；2026-10-18
        /// 迁 CardDataPaths.TutorialFileIn 寻址）。文件缺/读失败=空表（Warn 兜底，不缓存复用）。</summary>
        public static List<string> LoadDeckIds(string deckFile)
        {
            if (string.IsNullOrEmpty(deckFile)) return new List<string>();
            // 本读是教学开局链首个裸 File 探测点，须先落 persistent 种子（真机首启竞速实录：
            // 2026-10-18 引导只挂在 CardCatalog/EffectsLibrary，此处先行则必判"文件不存在"回落普通局）
            CardDataPaths.EnsureBootstrap();
            try
            {
                string path = CardDataPaths.TutorialFileIn(deckFile + ".json");
                if (!System.IO.File.Exists(path))
                {
                    TideLog.Warn($"[TutorialLibrary] 卡组引用文件不存在：{path}");
                    return new List<string>();
                }
                var deck = TideJson.FromJson<DeckData>(System.IO.File.ReadAllText(path));
                if (deck?.cardIds == null || deck.cardIds.Count == 0)
                {
                    TideLog.Warn($"[TutorialLibrary] 卡组引用文件为空：{path}");
                    return new List<string>();
                }
                return deck.cardIds;
            }
            catch (Exception e)
            {
                TideLog.Warn($"[TutorialLibrary] 卡组引用文件读取失败：{deckFile}：{e.Message}");
                return new List<string>();
            }
        }

        /// <summary>卡 id 序列 → CardData 卡组（保持顺序=摸牌序列；缺失 id 记警告跳过）。</summary>
        public static List<CardData> BuildDeck(List<string> cardIds)
        {
            var deck = new List<CardData>();
            foreach (var id in cardIds ?? new List<string>())
            {
                var data = CardCatalog.GetById(id);
                if (data == null)
                {
                    TideLog.Warn($"[TutorialLibrary] 卡 id 不在卡池：{id}（已跳过——教学卡组配置有误）");
                    continue;
                }
                deck.Add(data);
            }
            return deck;
        }
    }

    /// <summary>配置文件根（顶层 tutorials 数组）。</summary>
    [Serializable]
    public class TutorialConfigFile
    {
        public List<TutorialConfig> tutorials;
    }
}
