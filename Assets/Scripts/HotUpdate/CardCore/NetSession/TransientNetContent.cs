using System;
using System.Collections.Generic;
using CardCore.Serialization;

namespace CardCore.Network
{
    /// <summary>
    /// 瞬态网络内容覆盖层（2026-11-09 协议 v11 自建内容跨端定案）：联机对局期间，对端随
    /// DeckSubmit/MatchManifest 携带的卡/效果/词条内容（完整 JSON）装进本内存层——
    /// CardCatalog.GetById/EffectsLibrary.Get/CompositeAtomTable.Get 查询链并入（add-if-missing，
    /// **本地同 ID 行优先**——ID 冻结后同 ID 可载不同内容，本地版本胜出，内容分裂由握手摘要拒绝）。
    /// 仅存在于对局期：NetRoom.TeardownMatch（房主侧）与 BattleScreen.NetShutdown（客户端侧）Clear；
    /// **不在 GameCore.Reset 清**——Reset 在开局 InitGame 内先于 BuildDeck/引擎解析发生，清了会
    /// 把刚装的提交方内容冲掉。装/清时显式失效三表缓存（覆盖层不落盘，mtime 盯盘看不见它）。
    /// 「默认不显示」：覆盖层卡不进 LoadPlayPool（卡组构筑/随机组卡看不见对手卡）——只按 ID
    /// 解析（校验/渲染/引擎装载）；对局结束落对局记录（EncounterStore），玩家点「导入对手卡组」
    /// 才真正进本地表。
    /// </summary>
    public static class TransientNetContent
    {
        private static readonly List<CardData> _cards = new List<CardData>();
        private static readonly Dictionary<string, CardData> _cardsById = new Dictionary<string, CardData>(StringComparer.Ordinal);
        private static readonly Dictionary<string, EffectSlimDto> _effectsById = new Dictionary<string, EffectSlimDto>(StringComparer.Ordinal);
        private static readonly Dictionary<string, CompositeAtomDef> _atomDefsById = new Dictionary<string, CompositeAtomDef>(StringComparer.Ordinal);

        public static bool HasContent =>
            _cardsById.Count > 0 || _effectsById.Count > 0 || _atomDefsById.Count > 0;

        /// <summary>覆盖层卡（CardCatalog.LoadAll 合并位消费；仅对局期非空）。</summary>
        public static IReadOnlyList<CardData> Cards => _cards;

        /// <summary>覆盖层效果（EffectsLibrary.LoadAll 合并位消费）。</summary>
        public static IEnumerable<EffectSlimDto> Effects => _effectsById.Values;

        /// <summary>按 ID 取覆盖层词条（CompositeAtomTable.Get 兜底位消费；无则 null）。</summary>
        public static CompositeAtomDef GetAtomDef(string id)
            => !string.IsNullOrEmpty(id) && _atomDefsById.TryGetValue(id, out var def) ? def : null;

        /// <summary>卡是否来自覆盖层（LoadPlayPool 过滤用——对手内容默认不显示）。</summary>
        public static bool IsTransientCard(string cardId)
            => !string.IsNullOrEmpty(cardId) && _cardsById.ContainsKey(cardId);

        /// <summary>装载对端内容（DeckSubmit 提交方携带 / MatchManifest 对手侧下发共用）。
        /// null/空数组=无内容（存量客户端行为）。坏载荷跳过并告警（不拒局——缺行由既有
        /// 握手闭环校验显性拒绝）。返回是否实际新增（新增才失效缓存）。</summary>
        public static bool Install(
            MsgCardPayload[] cards, MsgEffectPayload[] effects, MsgAtomRowPayload[] atomRows)
        {
            bool added = false;
            if (cards != null)
            {
                foreach (var p in cards)
                {
                    var card = ParseCard(p);
                    if (card == null || string.IsNullOrEmpty(card.ID)) continue;
                    if (_cardsById.ContainsKey(card.ID)) continue;
                    _cardsById[card.ID] = card;
                    _cards.Add(card);
                    added = true;
                }
            }
            if (effects != null)
            {
                foreach (var p in effects)
                {
                    if (p == null || string.IsNullOrEmpty(p.EffectJson)) continue;
                    EffectSlimDto dto;
                    try { dto = TideJson.FromJson<EffectSlimDto>(p.EffectJson); }
                    catch (Exception ex)
                    {
                        TideLog.Warn($"[TransientNet] 效果载荷损坏跳过（{p.EffectId}）：{ex.Message}");
                        continue;
                    }
                    if (dto == null || string.IsNullOrEmpty(dto.id)) continue;
                    if (_effectsById.ContainsKey(dto.id)) continue;
                    _effectsById[dto.id] = dto;
                    added = true;
                }
            }
            if (atomRows != null)
            {
                foreach (var p in atomRows)
                {
                    if (p == null || string.IsNullOrEmpty(p.RowJson)) continue;
                    CompositeAtomDef def;
                    try { def = TideJson.FromJson<CompositeAtomDef>(p.RowJson); }
                    catch (Exception ex)
                    {
                        TideLog.Warn($"[TransientNet] 词条载荷损坏跳过（{p.RefId}）：{ex.Message}");
                        continue;
                    }
                    if (def == null || string.IsNullOrEmpty(def.id) || def.body == null) continue;
                    if (_atomDefsById.ContainsKey(def.id)) continue;
                    _atomDefsById[def.id] = def;
                    added = true;
                }
            }
            if (added) InvalidateLookups();
            return added;
        }

        /// <summary>清空覆盖层（对局拆除/退屏收口；幂等——空层不动缓存）。</summary>
        public static void Clear()
        {
            if (!HasContent && _cards.Count == 0) return;
            _cards.Clear();
            _cardsById.Clear();
            _effectsById.Clear();
            _atomDefsById.Clear();
            InvalidateLookups();
        }

        private static CardData ParseCard(MsgCardPayload p)
        {
            if (p == null || string.IsNullOrEmpty(p.CardJson)) return null;
            try
            {
                // 行 JSON 与 Cards.json 同构（CardConfigEntry）——包一层 wrapper 走 CardLoader
                // 全链物化（效果解析+装载校验），与内容包卡物化同口径。
                var cards = CardLoader.LoadCardsFromText("{\"cards\":[" + p.CardJson + "]}");
                return cards != null && cards.Count > 0 ? cards[0] : null;
            }
            catch (Exception ex)
            {
                TideLog.Warn($"[TransientNet] 卡载荷损坏跳过（{p.CardId}）：{ex.Message}");
                return null;
            }
        }

        /// <summary>覆盖层内容变化 → 三表缓存确定性失效（覆盖层无盘面痕迹，mtime 盯盘看不见）。</summary>
        private static void InvalidateLookups()
        {
            SynergyUI.CardCatalog.Invalidate();
            EffectsLibrary.Reload();
            CompositeAtomTable.Invalidate();
        }
    }
}
