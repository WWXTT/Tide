using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;

namespace SynergyUI
{
    /// <summary>
    /// 教学固定局配置（2026-10-02 教学模式地基）：一局教学的完整剧本——
    /// 双方卡组的**顺序即摸牌序列**（InitGame lockDeckOrder 锁牌库序：前 OpeningHandSize 张为起手、
    /// 之后逐回合摸牌恒取牌库顶）；机器人逐回合动作脚本由 ScriptedAi 按条执行；rngSeed 钉效果随机。
    /// 三者合一台教学局完全确定，可反复重演同一节奏。
    /// 数据：Assets/Configs/TutorialConfig.json（TidePaths 寻址：编辑器直读、直改 JSON 后 Reload 免重启，
    /// 真机由宿主装配进热更包——同 AtomicEffectTable 惯例）。
    /// </summary>
    [Serializable]
    public class TutorialConfig
    {
        public string id;
        public string name;
        public int rngSeed;
        /// <summary>玩家卡组（30 个卡 id，按摸牌顺序；每卡 1 张不重复——卡组定案口径）。</summary>
        public List<string> playerDeck;
        /// <summary>机器人卡组（同上）。</summary>
        public List<string> aiDeck;
        /// <summary>机器人脚本：turn=机器人的第几个回合（从 1 计，与引擎全局回合号无关）。</summary>
        public List<TutorialTurnScript> aiScript;

        /// <summary>取机器人第 aiTurn 回合的剧本（无则 null——ScriptedAi 直接结束回合）。</summary>
        public TutorialTurnScript GetAiTurn(int aiTurn)
            => aiScript?.FirstOrDefault(t => t != null && t.turn == aiTurn);
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
    /// element——card 入元素池（横置放置，当回合不产元素，次回合起横置恢复后可产）；
    /// tap——横置全部元素（LandTapPolicy 产色自动匹配手牌需求）；
    /// play——出 card（mode=抉择模式序号；targets=逗号分隔目标串，"hero"=对方英雄/卡 id=场上单位，
    /// 缺省或 "auto"=引擎自动解析）；
    /// attack——attacker（场上卡 id）宣言攻击 target（"hero"=对方英雄/卡 id=场上单位）；
    /// skill——英雄技能；end——提前结束本回合（跳过剩余动作）。
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
    }

    /// <summary>
    /// 教学配置库（仿 AtomicEffectTable 惯例：静态表 + TidePaths.ReadConfigText + Reload 免重启）。
    /// Get 按 id 取条目；BuildDeck 把卡 id 序列解析为 CardData 卡组（缺失 id 记警告跳过，不炸局）。
    /// </summary>
    public static class TutorialLibrary
    {
        private const string ConfigRelativePath = "Configs/TutorialConfig.json";

        private static Dictionary<string, TutorialConfig> _byId;

        static TutorialLibrary() => Initialize();

        /// <summary>强制重读配置（外部直改 JSON 后免重启刷新）。</summary>
        public static void Reload() => Initialize();

        private static void Initialize()
        {
            _byId = new Dictionary<string, TutorialConfig>();
            try
            {
                string raw = TidePaths.ReadConfigText(ConfigRelativePath);
                if (string.IsNullOrEmpty(raw))
                {
                    TideLog.Warn($"[TutorialLibrary] 配置不可读：{ConfigRelativePath}");
                    return;
                }
                var file = TideJson.FromJson<TutorialConfigFile>(raw);
                if (file?.tutorials == null || file.tutorials.Count == 0)
                {
                    TideLog.Warn($"[TutorialLibrary] 未加载到任何教学条目：{ConfigRelativePath}");
                    return;
                }
                foreach (var t in file.tutorials)
                {
                    if (t == null || string.IsNullOrEmpty(t.id)) continue;
                    _byId[t.id] = t;
                }
            }
            catch (Exception e)
            {
                TideLog.Warn($"[TutorialLibrary] 加载 {ConfigRelativePath} 失败: {e.Message}");
            }
        }

        /// <summary>按 id 取教学条目（找不到返回 null，调用方自行回落）。</summary>
        public static TutorialConfig Get(string id)
            => string.IsNullOrEmpty(id) ? null : _byId.TryGetValue(id, out var t) ? t : null;

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
