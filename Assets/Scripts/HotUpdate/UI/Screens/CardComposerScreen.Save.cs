using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.Attribute;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UInputField = TMPro.TMP_InputField;

namespace SynergyUI
{
    public sealed partial class CardComposerScreen : UIScreen
    {
        // ======================================== 右栏视图切换 ========================================

        private void ShowRightView(bool cards)
        {
            _rightShowCards = cards;
            if (_libraryList?.Rect != null) _libraryList.Rect.gameObject.SetActive(!cards);
            if (_poolList?.Rect != null) _poolList.Rect.gameObject.SetActive(cards);
            if (_filterRow != null) _filterRow.gameObject.SetActive(true); // 颜色+搜索两列表通用（2026-10-16 类别 chips 退役）
            if (_btnSwitchTable != null)
            {
                var t = _btnSwitchTable.GetComponentInChildren<TMP_Text>(true);
                if (t != null) t.text = cards ? "切换效果库" : "切换卡池"; // 按钮文案=去向
            }
        }

        // ======================================== 左栏：立绘画廊（回收式） ========================================

        // 2026-10-13 立绘随卡类型定案：生物=YGOMonstersRig 模型名（卡面立绘+战场 3D 同名双源）；
        // 2026-10-15 法术/结界=无合适立绘（缺省空白不放图）——画廊无选项。
        // 2026-10-21 虚拟化：贴片结构建一次（MakeArtTile 工厂），数据/高亮反复绑（BindArtTile）——
        // 换类型才重铺，载入/新建卡只重绑可见格（滚动位置不再被重置）。
        private CardKind? _galleryForKind;

        private void BuildArtGallery(bool force = false)
        {
            if (_galleryList == null) return;
            if (!force && _galleryForKind == _kind) { _galleryList.RebindVisible(); return; } // 同类型：仅对账高亮
            _galleryForKind = _kind;
            _artNames = new List<string>();
            if (_kind == CardKind.Creature)
            {
                foreach (var n in CreatureArtNames())
                    if (string.IsNullOrEmpty(_galleryQuery) || n.IndexOf(_galleryQuery, StringComparison.OrdinalIgnoreCase) >= 0)
                        _artNames.Add(n);
            }
            else
            {
                // 2026-11-06 默认图定案：法术/结界各一张默认图 Title（Assets/Art/Icons/Title.png——
                // ACard.prefab 同图已烘为两类型块的立绘占位，未选时卡面回落同一张图）
                if (string.IsNullOrEmpty(_galleryQuery) || "Title".IndexOf(_galleryQuery, StringComparison.OrdinalIgnoreCase) >= 0)
                    _artNames.Add("Title");
            }
            // 2026-11-06 去占位定案：占位项（清空立绘）退役——画廊只列真实可选图；
            // 未选立绘（null）=无高亮格，卡面回落烘焙占位图
            if (_galleryCount != null)
                _galleryCount.text = _artNames.Count + " 张";
            _galleryList.SetGrid(_artNames.Count); // 格几何（184×184·间8·2 列）由 Attach 从 GLG 吸收（2026-10-27 左栏 400 两列化）
        }

        /// <summary>生物立绘可选名（=模型名，与战场 MonsterModelResolver 同源：StreamingAssets bundle
        /// 内嵌 #names 名单，全平台同一条路径）。打包侧只收「prefab+预览图齐全」的目录——名单即全部可选。
        /// 旧编辑器直扫 YGOMonstersRig 目录版已随 2026-10-17 StreamingAssets 化退役（#if 空体正是
        /// CS0161 根因：非编辑器编译下方法体无 yield 无 return）。</summary>
        private static IEnumerable<string> CreatureArtNames()
        {
            return Tide.HotUpdate.MonsterRigAssets.Names();
        }

        /// <summary>贴片结构（v2 工厂，只建一次）：184×184 满格=Rounded6 底板+统一方形缩略（preserveAspect）
        /// +底部半透明名称条（f12 覆盖在缩略下部）+按钮+金框选中描边。内部无布局组（GLG 控格）。</summary>
        private RectTransform MakeArtTile()
        {
            var tile = UiKit.Node("tile", _galleryContent);
            var bg = tile.gameObject.AddComponent<Image>();
            bg.sprite = UiKit.RoundedSprite; // 程序生成圆角（同 UiKit.Button 口径，无 bundle 依赖）
            bg.color = new Color(0.16f, 0.18f, 0.22f, 1f);
            bg.raycastTarget = true;

            var thumb = UiKit.Node("thumb", tile);
            var img = thumb.gameObject.AddComponent<Image>();
            img.raycastTarget = false;
            UiKit.Stretch(thumb); // 184×184 满格（GLG 定格）

            var labelBg = UiKit.Node("label-bg", thumb).gameObject.AddComponent<Image>();
            labelBg.color = new Color(0f, 0f, 0f, 0.55f);
            labelBg.raycastTarget = false;
            var lbRt = labelBg.rectTransform;
            lbRt.anchorMin = new Vector2(0f, 0f);
            lbRt.anchorMax = new Vector2(1f, 0f);
            lbRt.pivot = new Vector2(0.5f, 0f);
            lbRt.sizeDelta = new Vector2(0f, 20f);

            var label = UiKit.Label("label", thumb, "", UiStyle.SmallSize, UiStyle.TextBody, TextAnchor.MiddleLeft);
            var lRt = label.rectTransform;
            lRt.anchorMin = new Vector2(0f, 0f);
            lRt.anchorMax = new Vector2(1f, 0f);
            lRt.pivot = new Vector2(0.5f, 0f);
            lRt.sizeDelta = new Vector2(-8f, 20f);
            lRt.anchoredPosition = new Vector2(0f, 1f);
            label.overflowMode = TextOverflowModes.Ellipsis;

            var btn = tile.gameObject.AddComponent<UnityEngine.UI.Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = bg;
            var ol = tile.gameObject.AddComponent<Outline>(); // 选中金框（bind 对账开关）
            ol.effectColor = Gold;
            ol.effectDistance = new Vector2(2f, -2f);
            ol.enabled = false;
            return tile;
        }

        /// <summary>图库选中金框（v2）。</summary>
        private static readonly Color Gold = new Color(0.91f, 0.75f, 0.35f);

        /// <summary>贴片绑定：立绘图/名称/选中描边/点击监听（原 HighlightArtTile 高亮并入此处）。
        /// 2026-11-06 去占位：数据源无 null 项——占位分支退役，全部条目都是真实可选图。</summary>
        private void BindArtTile(RectTransform tile, int index)
        {
            var spriteName = _artNames[index];

            var thumb = tile.Find("thumb") as RectTransform;
            var img = thumb != null ? thumb.GetComponent<Image>() : null;
            if (img != null)
            {
                var sp = CardArtLibrary.Load(spriteName); // 按名称两级回落（内部带缓存）
                img.sprite = sp; // 池行复用须显式清旧图
                img.preserveAspect = true;
                img.color = sp != null ? Color.white : new Color(0f, 0f, 0f, 0.25f);
            }
            var label = thumb != null ? thumb.Find("label")?.GetComponent<TMP_Text>() : null;
            if (label != null) label.text = spriteName;

            var ol = tile.GetComponent<Outline>();
            if (ol != null) ol.enabled = (_card.Illustration ?? "") == spriteName; // 高亮=与当前立绘对账

            var btn = tile.GetComponent<UnityEngine.UI.Button>();
            if (btn == null) return;
            var captured = spriteName;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                Debug.Log("[UI点击] 立绘 " + captured);
                _card.Illustration = captured;
                Recalculate(); // 卡面立绘随 RefreshCardFace 对账
                _galleryList.RebindVisible(); // 画廊高亮同帧对账（原 HighlightArtTile 全量重描）
                ShowToast("立绘已选：" + captured);
            });
            Describe(tile, $"选用立绘 {captured}——卡面立绘与战场 3D 模型同名双源");
        }

        // ======================================== 自动算费（终值直写） ========================================

        /// <summary>卡费文字公式（2026-10-16 二次改版：type 区方格退役，参考效果组合界面费用公式）：
        /// 构成项逐段「S/K/A/E 标签＝值」＋L3 挂载折扣×f ＝ D 合计（建议分布）＋黑白获得。
        /// 完整逐行明细见 type 区悬浮 desc-strip。</summary>
        private static string CardCostFormulaText(CardCostCalculator.Result r)
        {
            if (r == null) return "费用构成";
            var parts = new List<string>();
            float factor = 1f;
            foreach (var l in r.Breakdown)
            {
                if (string.IsNullOrEmpty(l.Label) || l.Label[0] != '[') continue;
                int close = l.Label.IndexOf(']');
                if (close <= 1) continue;
                var stage = l.Label.Substring(1, close - 1);
                if (stage == "f")
                {
                    if (l.Value > 0f) factor = l.Value;
                    continue;
                }
                if (stage != "S" && stage != "K" && stage != "A" && stage != "E") continue;
                if (Mathf.Approximately(l.Value, 0f)) continue; // 主动效果「构筑期不计锚价」等 0 值行不列
                parts.Add(FormulaPartLabel(stage, l.Label) + "＝" + l.Value.ToString("0.##"));
            }
            var text = "费用：" + (parts.Count > 0 ? string.Join("＋", parts) : "0")
                + (factor < 1f ? "×f" + factor.ToString("0.###") : "");
            if (r.PayloadPrice > 0)
            {
                // P4 §1.2 + 2026-10-31 色锁定案：三段式显净费（构成合计=抵扣前推导费；实抵 N=min(全价,可产出色容量)，
                // 只作用于代价原子可产出色——明细行「[P] 代价抵扣 −N（限X·至多清零该色）」在费用区悬浮构成里）。
                text += "＝D" + ((int)r.PrePayloadCost.Total).ToString("0") + "（" + CostZh(r.PrePayloadCost) + "）"
                        + $" − 代价抵扣 {r.PayloadApplied}（限{r.PayloadScopeZh}）"
                        + " ＝ 净费 D" + r.Total.ToString("0") + "（" + CostZh(r.SuggestedCost) + "）";
            }
            else
                text += "＝D" + r.Total.ToString("0") + "（" + CostZh(r.SuggestedCost) + "）";
            var grants = r.Grants.Where(kv => kv.Value != 0)
                .Select(kv => ManaTypeNames.ZhNameOf((ManaType)kv.Key) + Mathf.Abs(kv.Value).ToString("0.#"))
                .ToList();
            if (grants.Count > 0) text += "｜获得 " + string.Join("", grants);
            return text;
        }

        /// <summary>构成项短标签：S 保留身材/耐久变量（「S(攻2/血3)」）；K/A/E 去尾部括注（「K关键词 Guard」）。</summary>
        private static string FormulaPartLabel(string stage, string label)
        {
            var body = label.Substring(label.IndexOf(']') + 1).Trim(); // 去 "[X] " 前缀
            if (stage == "S")
                return "S" + body.Replace("身材费 ", "").Replace("耐久费 ", "");
            int cut = body.IndexOf('(');
            int cut2 = body.IndexOf('（');
            if (cut2 >= 0 && (cut < 0 || cut2 < cut)) cut = cut2;
            if (cut > 0) body = body.Substring(0, cut).Trim();
            return stage + body;
        }

        /// <summary>费用文本化（效果组合界面 CostZh 同口径）：非零色按序拼「N色」；全零=0。</summary>
        private static string CostZh(ElementCost cost)
        {
            if (cost == null || cost.IsZero) return "0";
            var parts = new List<string>();
            foreach (var color in cost.NonzeroColors())
                parts.Add(cost[color].ToString("0.##") + ManaTypeNames.ZhNameOf(color));
            return string.Join("", parts);
        }

        private void Recalculate()
        {
            ApplyStatFloors(); // 2026-11-03 底盘改版：结构变动后免费属性底托底（先托底再算费）
            var result = CardCostCalculator.Calculate(_card);

            // 自动算费直写（2026-10-16 定案：手动声明/采纳/建议标签全退役）——费用恒=内容推导分布。
            //（2026-10-22 恒推导定案后 CostDict=SuggestedCost=DerivedCost，无声明回显分支可绕。）
            var suggested = result.SuggestedCost;
            bool same = _card.Cost != null && suggested != null && _card.Cost.v.SequenceEqual(suggested.v);
            if (!same)
            {
                _card.Cost = suggested != null && !suggested.IsZero ? suggested.Clone() : null;
                _card.ResetCache();
            }

            RefreshCardFace(result);
        }

        // ======================================== 保存（覆盖替换） ========================================

        private void OnSave()
        {
            ApplyTextLists();
            EnsureCardLists();

            // 减费三选一保存闸（2026-11-02 定案·单源=EffectComposerScreen.DiscountConflictOf）：
            // ① 同一效果只能三选一（有限分支/自由分支/代价）；② 并列两效果禁代价；
            // ③ 光环类（结界的场地光环/仪典行）禁代价；④ 代价行全价 ≤3（2026-11-03 收回——当日放宽 4 作废）。
            string discountConflict = EffectComposerScreen.DiscountConflictOf(_card);
            if (discountConflict != null)
            {
                ShowToast(discountConflict);
                return;
            }

            // 形状拦截（2026-10-15 深夜三类卡改版；2026-11-05 充能系统动作化修订）：
            // 法术=主动 1~3（总效果数 ≤4）；结界=主动全拒（充能不再是卡上效果，P12 修订收回）。
            if (_kind == CardKind.Spell)
            {
                int count = _card.Effects?.Count ?? 0;
                int activeCount = 0;
                foreach (var e in _card.Effects ?? new List<CardEffectData>())
                {
                    int t = e?.TriggerTiming ?? -1;
                    if (t != (int)TriggerTiming.Activate_Active && t != (int)TriggerTiming.Activate_Instant)
                    {
                        ShowToast($"法术效果「{e.DisplayName ?? e.Id}」须为主动效果（法术只挂主动，自动/触发/响应式不收）");
                        return;
                    }
                    activeCount++;
                }
                if (count < 1 || count > 4 || activeCount < 1 || activeCount > 3)
                {
                    ShowToast($"法术须挂 1~3 个主动效果（现 {activeCount}——施放时择一发动）");
                    return;
                }
            }
            else if (_kind == CardKind.Enchantment)
            {
                foreach (var e in _card.Effects ?? new List<CardEffectData>())
                {
                    int t = e?.TriggerTiming ?? -1;
                    if (t == (int)TriggerTiming.Activate_Active || t == (int)TriggerTiming.Activate_Instant
                        || t == (int)TriggerTiming.Activate_Response)
                    {
                        ShowToast($"结界效果「{e.DisplayName ?? e.Id}」为主动效果——结界只挂自动/光环（充能已是系统动作）");
                        return;
                    }
                }
            }

            // 连接光环归属闸（2026-10-15 改版）：聚合后卡面光环只准结界携带
            if (_kind != CardKind.Enchantment && _card.LinkAuras != null && _card.LinkAuras.Count > 0)
            {
                ShowToast(_kind == CardKind.Creature
                    ? "生物不可挂连接光环（2026-10-15 改版）——移除光环后保存"
                    : "法术只挂主动效果——移除光环后保存");
                return;
            }

            // 槽位容量闸（2026-10-16 槽位化定案：生物 主动1/自动1/关键词3+攻守开关；结界 光环恰好1/自动2）
            if (_kind == CardKind.Creature)
            {
                if (ActiveEffectCount > 1)
                {
                    ShowToast($"生物主动槽 0~1（现 {ActiveEffectCount} 个）——删除多余主动效果后保存");
                    return;
                }
                if (AutoEffectCount > 1)
                {
                    ShowToast($"生物自动槽 0~1（现 {AutoEffectCount} 个）——删除多余自动效果后保存");
                    return;
                }
                if (string.IsNullOrWhiteSpace(_attackInput?.text) || string.IsNullOrWhiteSpace(_lifeInput?.text))
                {
                    ShowToast("生物必填项：攻击/生命（留空=未填）");
                    return;
                }
                if ((_card.Power ?? 0) > MaxCreatureStat || (_card.Life ?? 0) > MaxCreatureStat)
                {
                    ShowToast($"生物基础属性上限 {MaxCreatureStat}——攻/血至多 {MaxCreatureStat}，调整后保存");
                    return;
                }
                // 免费属性底闸（2026-11-03 底盘分配改版）：兜底拦「载入旧卡低于新底未触碰」的直存
                //（编辑触碰即被输入钳制/托底收口）；底=FreeAttrBaseOf（3−攻守−首槽），n/n 固定不可偏移。
                int statFloor = CardCompositionCost.FreeAttrBaseOf(_card);
                if ((_card.Power ?? 0) < statFloor || (_card.Life ?? 0) < statFloor)
                {
                    ShowToast($"生物属性下限=免费属性底 {statFloor}（攻/血各至少 {statFloor}——免费底 n/n 固定获得），调整后保存");
                    return;
                }
            }
            else if (_kind == CardKind.Enchantment)
            {
                if (AuraEffectCount == 0)
                {
                    ShowToast("结界必须恰好 1 个光环效果（规则或连接）——点被动区选中后从右栏挂载");
                    return;
                }
                if (AuraEffectCount > 1)
                {
                    ShowToast($"结界光环槽恰好 1（现 {AuraEffectCount} 个）——删除多余光环后保存");
                    return;
                }
                if (AutoEffectCount > 2)
                {
                    ShowToast($"结界自动槽 0~2（现 {AutoEffectCount} 个）——删除多余自动效果后保存");
                    return;
                }
            }
            if (_kind != CardKind.Creature && _card.Keywords != null && _card.Keywords.Count > 0)
            {
                ShowToast("关键词为生物专属——清空关键词或切换为生物后保存");
                return;
            }

            // 参数列 CardRef 选择闸（2026-10-31 阶段C）：变形/模拟/召唤衍生物等引用卡原子未选卡
            // → 拦截保存——与装载期 ValidateAtomParams 同口径（引擎主干行带 then 载荷豁免：
            // 附加诅咒/附加祝福的载荷通道=branch.then，不收 str）。
            string missing = CardRefMissingMessage();
            if (missing != null)
            {
                ShowToast(missing);
                return;
            }

            // 名称缺省（2026-10-16 槽位化）：留空=用卡 ID 当卡名。
            // 2026-11-05 ID 冻结：编辑既有卡沿用其 id（名字不再参与身份）；新建/另存才按内容指纹分配。
            bool nameFromId = string.IsNullOrWhiteSpace(_card.CardName);
            string savedId = string.IsNullOrEmpty(_card.ID)
                ? "C_" + ContentHasher.HashCard(_card)
                : _card.ID;
            if (nameFromId) _card.CardName = savedId;
            bool replacing = !_editingPresetSource // 预设卡恒另存（源档=教学数据，覆盖替换概念不适用）
                             && !string.IsNullOrEmpty(_editingOriginalId) && _editingOriginalId != savedId;

            // 功能内容查重闸（2026-11-13 用户定案：牌库不允许保存一模一样的卡）：名称不参与功能身份
            //（ContentHasher 类注释同口径——名字不同但功能相同=重复），SealCard 跳过 NM 段全库比对；
            // 命中异 id 同封印（只有名字不同的另一张卡）→ 拒存。自身（savedId）与被替换旧卡不比对。
            string seal = ContentHasher.SealCard(_card);
            foreach (var other in CardCatalog.LoadAll())
            {
                if (other == null || other.ID == savedId || other.ID == _editingOriginalId) continue;
                if (ContentHasher.SealCard(other) == seal)
                {
                    ShowToast($"已存在功能完全相同的卡「{other.CardName}」（{other.ID}）——"
                              + "只有名称不同不算新卡，修改内容或删除旧卡后再保存");
                    return;
                }
            }

            // 覆盖替换前置检查（2026-09-24 定案）：旧卡被卡组引用则拦截（删除会使卡组引用悬空）
            if (replacing)
            {
                var refs = ReferenceAudit.DecksReferencingCard(_editingOriginalId);
                if (refs.Count > 0)
                {
                    ShowToast($"旧卡仍被卡组引用（{string.Join("、", refs)}）——先到卡组构建中替换该卡再保存");
                    return;
                }
            }

            var path = CardConfigSerializer.Save(_card, null, replacing ? _editingOriginalId : null);
            if (path == null)
            {
                ShowToast("保存失败");
                return;
            }
            // savedId 已在保存前算定（= ToEntry 落盘 id：既有卡沿用原 id，新卡=内容指纹）
            CardCatalog.Invalidate();
            CorpusCache.Clear();
            TutorialCreationFlow.NotifyCardsChanged(); // 第三课走查步检测（未开课零行为）

            ShowToast(replacing
                ? $"已覆盖替换：{_card.CardName}（{_editingOriginalId} → {savedId}）"
                : $"已保存到卡表：{System.IO.Path.GetFileName(path)}（{savedId}）");

            OnNewCard();
            RefreshLibrary();
        }

        private void ApplyTextLists()
        {
            _card.CardName = string.IsNullOrWhiteSpace(_nameInput?.text) ? null : _nameInput.text.Trim(); // null=保存时缺省成 ID
            // 标签输入已退役（2026-10-16 prefab 重做）：_card.Tags 载入/保存透传，不再从输入框覆写
        }

        /// <summary>CardRef 未选检查（2026-10-31 阶段C 保存闸）：枚举全效果原子（与装载期
        /// ValidateAtomParams 同一嵌套枚举），参数列=CardRef 且 str 为空 → 返回拦截文案；
        /// null=全齐可存。引擎主干行带 then 载荷豁免（附加诅咒/附加祝福——载荷通道=branch.then）。</summary>
        private string CardRefMissingMessage()
        {
            foreach (var eff in _card.Effects ?? new List<CardEffectData>())
            {
                if (eff == null) continue;
                foreach (var atom in CardLoader.EnumerateAtomEntries(eff))
                {
                    var row = AtomicEffectTable.GetByHashId(atom?.refId);
                    if (row == null || row.Param != AtomicParamKind.CardRef) continue;
                    if (ComposerCatalog.IsEngineTrunkRow(row)
                        && atom.branch?.then != null && atom.branch.then.Count > 0)
                        continue; // 引擎主干行：载荷走 branch.then（同装载期豁免）
                    if (string.IsNullOrEmpty(atom.str))
                        return $"效果「{eff.DisplayName ?? eff.Id}」的原子「{row.DisplayName}」未选择模板卡"
                               + "——在卡面衍生物槽（或进效果组合点该原子的「选择卡牌」）选卡后再保存";
                }
            }
            return null;
        }

        /// <summary>操作反馈（2026-10-16 toast 退役定案）：Console 必落，desc-strip 尽力显示
        /// （悬浮事件随后自然覆盖——两机制共存）。</summary>
        private void ShowToast(string message)
        {
            Debug.Log("[CardComposer] " + message);
            SetDescStrip(message);
        }

        // ======================================== 列表行模板克隆 ========================================

        /// <summary>克隆列表自带 inactive row 模板（Image+Button+CostSquares+meta）——
        /// tpl-root 体系已随 2026-10-16 prefab 重做消失，行视觉以两列表模板为单一来源。</summary>
        private RectTransform CloneRow(UiKit.Scroll list)
        {
            if (list?.Content == null) return null;
            // 模板已被虚拟化接管搬去 tpl-park（content 只装活行）——从停靠点克隆
            var clone = UiKit.CloneTemplateFrom(UiKit.RecycledList.TemplatePark(list), "row", list.Content);
            if (clone == null)
                Debug.LogError("[CardComposer] 缺行模板 row（tpl-park/content 直属 inactive row）——该行跳过");
            return clone;
        }

        /// <summary>只清本屏运行时行（rt- 前缀）——prefab 静态子（标题/方格/des）不动。</summary>
        private static void ClearRuntimeChildren(RectTransform zone)
        {
            if (zone == null) return;
            for (int i = zone.childCount - 1; i >= 0; i--)
            {
                var child = zone.GetChild(i);
                if (!child.name.StartsWith("rt-", StringComparison.Ordinal)) continue;
                child.SetParent(null);
                UnityEngine.Object.Destroy(child.gameObject);
            }
        }
    }
}
