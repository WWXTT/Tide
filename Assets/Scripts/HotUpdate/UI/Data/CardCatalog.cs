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

        // 教学独立卡表（2026-10-16 定案：教学卡不再与正式卡混用；2026-10-17 分家：文件挪至
        // StreamingAssets/Tutorial/，不与正式卡 Card/ 混放——防正常选卡组界面误入测试卡组）：
        // 同格式副表，LoadAll 时合并入索引（教学卡带 tags=教学 → LoadPlayPool 既有隔离自动生效）；
        // id 冲突保正式表（教学表写错撞正式 id 时可查）。缓存新鲜度与主表同盯。
        private const string TutorialCardsFile = "TutorialCards.json";

        // 训练独立卡表（2026-11-03 训练池分家定案）：RL 训练桥/验证器的专用卡池，与玩家主表、
        // 教学副表三足分立——文件在 Assets/TrainingData，随开发仓库走（不进热更包、不下发玩家
        // 设备）。同格式副表，LoadAll 时合并入索引（训练卡带 tags=训练池 → LoadPlayPool/各玩家面
        // 过滤自动隔离）；id 冲突保先装表（主表>教学>训练）。缓存新鲜度独立记账。
        private const string TrainingCardsFile = "TrainingCards.json";

        private static List<CardData> _cache;
        private static Dictionary<string, CardData> _byId;
        private static bool _loading;
        // 缓存新鲜度记账（2026-10-13）：卡表文件会被并行会话/外部工具直写（卡池填充脚本、手工编辑），
        // 旧口径只靠 Invalidate() 手动失效——文件缺失期或外部清空期建的空缓存会一直陈旧，
        // 表现为卡组合界面卡池恒 0 张。缓存前按 mtime 复核，变了就弃缓存重读。
        private static string _cachePath;
        private static DateTime _cacheStampUtc = DateTime.MinValue;
        // 教学/主表 mtime 独立记账（2026-10-22 NRE 修复，与 EffectsLibrary 2026-10-21 同案）：
        // 旧口径拿主表戳比教学表 mtime → 两文件 mtime 永不相等 → 恒过期。装载教学表途中
        // CardLoader 费用巡检重入 GetById→LoadAll 时恒过期触发 Invalidate 把在用 _byId 置空
        // → 外层 MergeTutorialCards 的 _byId.ContainsKey NRE（界面装载异常/卡池空）。
        private static DateTime _tutorialStampUtc = DateTime.MinValue;
        // 训练副表 mtime 独立记账（2026-11-03，与教学副表同案）：缺文件=玩家构建常态（训练表
        // 不随包下发），用 MinValue 占位——文件从不出现则永不判过期。
        private static DateTime _trainingStampUtc = DateTime.MinValue;
        private static DateTime _lastStaleCheckUtc = DateTime.MinValue; // GetById 高频入口的盯盘节流（2026-10-17）

        /// <summary>加载（并缓存）全部卡牌。文件缺失返回空列表。
        /// 重入防护（2026-09-13 崩溃修复）：装载过程中 CardLoader 的费用巡检会经
        /// SummonToken 模板解析回调 GetById——此时 _byId 仍空，旧逻辑再触发 LoadAll
        /// 成无限递归（StackOverflow 硬崩编辑器）。装载中重入返回空表：模板查询得 null，
        /// 计价按既定 fallback 回落数量口径，装载完成后恢复正解。</summary>
        public static List<CardData> LoadAll()
        {
            // 装载中禁 Invalidate（2026-10-26 首切效果表 NRE 修复，10-22 分账修复漏堵的口）：
            // _tutorialStampUtc 在教学表解析成功后才记账（坏 JSON 重试语义），解析途中回调
            // GetById 的盯盘必见「tutorial mtime ≠ MinValue」判过期 → 重入 LoadAll 走本行
            // Invalidate 把在用 _byId 置空 → 外层 MergeTutorialCards 的 _byId.ContainsKey NRE。
            // 重入路径 _cache 非空直接返回旧值（比旧口径"返回空表"信息更多），外层装载继续，
            // 完成后下次调用自然换新。
            if (_cache != null && CacheIsStale() && !_loading)
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
                MergeTutorialCards(); // 教学独立卡表（2026-10-16）：副表合并入索引，教学局按 id 组卡可取
                MergeTrainingCards(); // 训练独立卡表（2026-11-03）：训练桥/验证器按 id 可取，玩家面经 LoadPlayPool 隔离
                MergePackCards(); // 内容包卡合并（G2 2026-11-08）：装包卡并入索引（卸包=断链由此自然成立）
                MergeNetCards(); // 瞬态网络内容（2026-11-09 协议 v11）：对局期对手自建卡并入（默认不进玩家池）
                return _cache;
            }
            finally
            {
                _loading = false;
            }
        }

        /// <summary>缓存是否过期：文件消失 / 路径变 / mtime 变（主表与教学副表任一变化即过期）。
        /// 读取失败按未过期处理（读失败≠数据变）。</summary>
        private static bool CacheIsStale()
        {
            try
            {
                string path = CardDataPaths.FileIn("Cards.json");
                if (path != _cachePath || !File.Exists(path)) return true;
                if (File.GetLastWriteTimeUtc(path) != _cacheStampUtc) return true;
                string tpath = CardDataPaths.TutorialFileIn(TutorialCardsFile);
                if (File.Exists(tpath) && File.GetLastWriteTimeUtc(tpath) != _tutorialStampUtc) return true;
                string rpath = CardDataPaths.TrainingFileIn(TrainingCardsFile);
                if (File.Exists(rpath) && File.GetLastWriteTimeUtc(rpath) != _trainingStampUtc) return true;
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>教学副表合并：教学卡同时进 _cache 与 _byId（与"教学卡在主表"完全同口径——
        /// LoadPlayPool 按 tags=教学过滤照旧生效）；id 冲突保正式表（教学表写错可查）。
        /// 文件缺失=无教学卡，静默（教学表随仓库走，缺失属配置错误由消费面 Warn 兜底）。</summary>
        private static void MergeTutorialCards()
        {
            string tpath = CardDataPaths.TutorialFileIn(TutorialCardsFile);
            if (!File.Exists(tpath))
            {
                TideLog.Warn($"[CardCatalog] 教学卡表未找到: {tpath}（教学局组卡将取不到教学卡）");
                return;
            }
            var cards = CardLoader.LoadCardsFromText(File.ReadAllText(tpath));
            if (cards == null) return;
            _tutorialStampUtc = File.GetLastWriteTimeUtc(tpath); // 解析成功才记账：坏 JSON 不落缓存态，下次调用重试（与主表同口径）
            foreach (var card in cards)
            {
                if (card == null || string.IsNullOrEmpty(card.ID)) continue;
                if (_byId.ContainsKey(card.ID))
                {
                    TideLog.Warn($"[CardCatalog] 教学卡表 id 与正式卡冲突（保正式表）: {card.ID}");
                    continue;
                }
                _byId[card.ID] = card;
                _cache.Add(card);
            }
        }

        /// <summary>训练副表合并（2026-11-03 训练池分家）：训练卡进 _cache 与 _byId 供训练桥/
        /// 验证器按 id 取卡（BattleDeckSources/TideHeadlessServer 随机 30 口径不变）；
        /// id 冲突保先装表（主表&gt;教学&gt;训练）。文件缺失=玩家构建常态（训练表不随包下发），
        /// 静默返回——与教学表「缺失属配置错误」不同口径，勿加 Warn。</summary>
        private static void MergeTrainingCards()
        {
            string rpath = CardDataPaths.TrainingFileIn(TrainingCardsFile);
            if (!File.Exists(rpath)) return;
            var cards = CardLoader.LoadCardsFromText(File.ReadAllText(rpath));
            if (cards == null) return;
            _trainingStampUtc = File.GetLastWriteTimeUtc(rpath); // 解析成功才记账（与教学副表同口径）
            foreach (var card in cards)
            {
                if (card == null || string.IsNullOrEmpty(card.ID)) continue;
                if (_byId.ContainsKey(card.ID))
                {
                    TideLog.Warn($"[CardCatalog] 训练卡表 id 与先装表冲突（保先装表）: {card.ID}");
                    continue;
                }
                _byId[card.ID] = card;
                _cache.Add(card);
            }
        }

        /// <summary>内容包卡合并（G2 2026-11-08）：id 冲突保先装表（装包校验已拒撞号，此处兜底）。
        /// 卡物化在合并位发生（CardLoader 全链装载——含效果解析与装载校验）；卸包后目录消失，
        /// 卡随之出索引 → 卡组校验（DeckCompositionValidator）按 MissingIds **整卡断链**。
        /// 包目录变更不经 mtime 盯盘——装卸走 ContentPackRegistry（内部即调 Invalidate）。</summary>
        private static void MergePackCards()
        {
            foreach (var card in CardCore.ContentPackRegistry.AllCards())
            {
                if (card == null || string.IsNullOrEmpty(card.ID)) continue;
                if (_byId.ContainsKey(card.ID))
                {
                    TideLog.Warn($"[CardCatalog] 内容包卡 id 与先装表冲突（保先装表）: {card.ID}");
                    continue;
                }
                _byId[card.ID] = card;
                _cache.Add(card);
            }
        }

        /// <summary>瞬态网络内容合并（2026-11-09 协议 v11 自建内容跨端）：联机对局期对端随
        /// DeckSubmit/Manifest 携带的卡并入索引——校验/渲染/引擎装载按 ID 可取；本地同 ID 行
        /// 优先（add-if-missing，内容分裂由握手摘要拒绝）。默认不进玩家池（LoadPlayPool 过滤），
        /// 对局结束 TransientNetContent.Clear 即出索引。覆盖层无盘面痕迹——装/清经其显式 Invalidate
        /// 驱动本 LoadAll 重合并（mtime 盯盘看不见它）。</summary>
        private static void MergeNetCards()
        {
            foreach (var card in CardCore.Network.TransientNetContent.Cards)
            {
                if (card == null || string.IsNullOrEmpty(card.ID)) continue;
                if (_byId.ContainsKey(card.ID)) continue;
                _byId[card.ID] = card;
                _cache.Add(card);
            }
        }

        /// <summary>按运行时实例 ID 查表（2026-10-17 修）：Seeder/SummonToken 同 id 多张唯一化为
        /// 衍生物式 `模板#N` 实例 ID——带后缀直查必 miss（印刷基准悬空=受伤生命不变红、
        /// 卡面名字退化为实例串）。本口剥 `#N` 后缀按基础 id 查；无后缀原样。</summary>
        public static CardData GetByInstanceId(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId)) return null;
            var i = instanceId.IndexOf('#');
            return GetById(i > 0 ? instanceId.Substring(0, i) : instanceId);
        }

        /// <summary>按卡牌 ID 取 CardData，找不到返回 null。
        /// 陈旧复核（2026-10-17 缓存旧卡面修复）：高频入口也盯盘——原口径只在 _byId 空时 LoadAll，
        /// 缓存非空时永不检查 CacheIsStale，禁域重载跨 Play 残留的旧缓存会一直沿用旧卡面
        /// （教学演示数值错乱的通道）。mtime 查盘按 2 秒节流防高频 IO。</summary>
        public static CardData GetById(string id)
        {
            if (_byId == null)
                LoadAll();
            else if ((DateTime.UtcNow - _lastStaleCheckUtc).TotalSeconds >= 2)
            {
                _lastStaleCheckUtc = DateTime.UtcNow;
                if (CacheIsStale())
                    LoadAll(); // LoadAll 自带 stale→Invalidate→重读
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

        /// <summary>训练专用卡判定（2026-11-03 训练池分家，tags 含「训练池」）：不进玩家侧任何
        /// 卡池面——随机组卡/卡组构筑/卡牌编辑器预览/联机 AI 填位/衍生物模板候选/CardRef 选卡器
        /// 一律排除（各消费面统一走 LoadPlayPool 或补 IsTrainingCard 判断，勿各自手写 tag 逻辑）；
        /// 训练桥/验证器按 id 组卡（CardCatalog.GetById / BattleDeckSources.AllCards）照常可取。</summary>
        public static bool IsTrainingCard(CardData card)
            => card?.Tags != null && card.Tags.Contains("训练池");

        /// <summary>玩家可用卡池：LoadAll 去除教学专用卡、衍生物模板卡、训练专用卡与瞬态网络内容卡
        ///（各消费面统一走此口，勿各自手写 tag 判断）。</summary>
        public static List<CardData> LoadPlayPool()
            => LoadAll().Where(c => !IsTeachingCard(c) && !IsTokenCard(c) && !IsTrainingCard(c)
                                    && !CardCore.Network.TransientNetContent.IsTransientCard(c.ID)).ToList();
    }
}
