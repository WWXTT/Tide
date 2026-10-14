using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CardCore;

namespace SynergyUI
{
    /// <summary>
    /// 卡牌总表 —— 从 Assets/Configs 下的卡牌 JSON 读取全部 CardData。
    /// 复用现有 CardLoader.LoadCardsFromText 解析器（无需改 CardLoader），
    /// 用 System.IO 直读文件，与 DeckSerializer 落盘位置一致。
    /// </summary>
    public static class CardCatalog
    {
        // StreamingAssets/Card/Cards.json（2026-09-14 统一定案+更名；2026-09-21 定案：卡属用户数据。
        // 与卡牌合成保存同一文件——合成卡即入池；效果经 EffectsLibrary 引用解析；
        // 原 TestDecks 留作训练桥/验证夹具）。
        private const string CardsConfigRelative = "Card/Cards.json";

        private static List<CardData> _cache;
        private static Dictionary<string, CardData> _byId;
        private static bool _loading;
        // 缓存新鲜度记账（2026-10-13）：卡表文件会被并行会话/外部工具直写（卡池填充脚本、手工编辑），
        // 旧口径只靠 Invalidate() 手动失效——文件缺失期或外部清空期建的空缓存会一直陈旧，
        // 表现为卡组合界面卡池恒 0 张。缓存前按 mtime 复核，变了就弃缓存重读。
        private static string _cachePath;
        private static DateTime _cacheStampUtc = DateTime.MinValue;

        /// <summary>加载（并缓存）全部卡牌。文件缺失返回空列表。
        /// 重入防护（2026-09-13 崩溃修复）：装载过程中 CardLoader 的费用巡检会经
        /// SummonToken 模板解析回调 GetById——此时 _byId 仍空，旧逻辑再触发 LoadAll
        /// 成无限递归（StackOverflow 硬崩编辑器）。装载中重入返回空表：模板查询得 null，
        /// 计价按既定 fallback 回落数量口径，装载完成后恢复正解。</summary>
        public static List<CardData> LoadAll()
        {
            if (_cache != null && CacheIsStale())
            {
                Invalidate();
            }
            if (_cache != null)
            {
                return _cache;
            }
            if (_loading)
            {
                return new List<CardData>();
            }

            _loading = true;
            try
            {
                CardDataPaths.EnsureBootstrap(); // 玩家端首启引导（编辑器空操作）
                string path = CardDataPaths.FileIn("Cards.json");
                _cachePath = path;
                _cacheStampUtc = DateTime.MinValue; // 先记「未读到文件」态——文件出现后 mtime 必 ≠ MinValue，自动恢复
                if (!File.Exists(path))
                {
                    TideLog.Warn($"[CardCatalog] 卡表未找到: {path}");
                    _cache = new List<CardData>();
                    _byId = new Dictionary<string, CardData>();
                    return _cache;
                }

                _cache = CardLoader.LoadCardsFromText(File.ReadAllText(path));
                _cacheStampUtc = File.GetLastWriteTimeUtc(path); // 解析成功才记账：坏 JSON 抛异常不落缓存态，下次调用重试
                _byId = new Dictionary<string, CardData>();
                foreach (var card in _cache)
                {
                    if (!string.IsNullOrEmpty(card.ID))
                    {
                        _byId[card.ID] = card;
                    }
                }
                return _cache;
            }
            finally
            {
                _loading = false;
            }
        }

        /// <summary>缓存是否过期：文件消失 / 路径变 / mtime 变。读取失败按未过期处理（读失败≠数据变）。</summary>
        private static bool CacheIsStale()
        {
            try
            {
                string path = CardDataPaths.FileIn("Cards.json");
                if (path != _cachePath || !File.Exists(path)) return true;
                return File.GetLastWriteTimeUtc(path) != _cacheStampUtc;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>按卡牌 ID 取 CardData，找不到返回 null。</summary>
        public static CardData GetById(string id)
        {
            if (_byId == null)
            {
                LoadAll();
            }
            if (_byId == null || string.IsNullOrEmpty(id))
            {
                return null; // 重入装载未建索引（冷启动费用巡检回调）——查无此卡
            }
            return _byId.TryGetValue(id, out var card) ? card : null;
        }

        /// <summary>清空缓存，下次 LoadAll 重新读盘（编辑卡表后调用）。</summary>
        public static void Invalidate()
        {
            _cache = null;
            _byId = null;
            _cachePath = null;
            _cacheStampUtc = DateTime.MinValue;
        }

        // ======================================== 教学卡隔离（2026-10-06 第一课起） ========================================

        /// <summary>教学专用卡判定（tags 含「教学」）：不进玩家侧卡池面——随机组卡/卡组构筑/
        /// 卡牌编辑器预览/联机 AI 填位一律排除；教学局按 id 组卡（TutorialLibrary.BuildDeck→GetById）
        /// 不经此判定，教学卡照常可取。卡名约定：教学卡不带语义名（教学生物N 编号），名字留给玩家。</summary>
        public static bool IsTeachingCard(CardData card)
            => card?.Tags != null && card.Tags.Contains("教学");

        /// <summary>衍生物模板卡判定（2026-10-09 tags 含「衍生物」）：不进玩家侧卡池面（同教学卡口径）
        /// ——随机组卡/卡组构筑/卡牌编辑器预览/联机 AI 填位排除；仅作 SummonToken 召唤模板存在，
        /// 按 ID 解析照常（GetById/装载校验不受影响）。</summary>
        public static bool IsTokenCard(CardData card)
            => card?.Tags != null && card.Tags.Contains("衍生物");

        /// <summary>玩家可用卡池：LoadAll 去除教学专用卡与衍生物模板卡（各消费面统一走此口，勿各自手写 tag 判断）。</summary>
        public static List<CardData> LoadPlayPool()
            => LoadAll().Where(c => !IsTeachingCard(c) && !IsTokenCard(c)).ToList();
    }
}
