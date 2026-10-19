using System.Collections.Generic;
using CardCore;
using Tide.HotUpdate;
using UnityEngine;

namespace SynergyUI
{
    /// <summary>
    /// 连锁链舞台（2026-10-19 用户定案：卡牌预览进链、逆结算完成才消散）。
    /// BattleUI.prefab 根级 "chain" 节点=链容器（VerticalLayoutGroup 负间距堆叠——400 高槽位
    /// 露出约 100px 成链状），其下首个 ACard 实例=克隆模板（运行时隐藏，prefab 侧零改动）。
    /// 生命周期：卡牌施放入栈即克隆上链（响应窗期链持续堆积）；逆结算期克隆恒保持——快照
    /// ChainCards 随出栈变短，故结算期对账口径=只增不删（2026-10-19「保持到逆结算完成」定案），
    /// 当前步高亮、已结算变暗；整轮完成由回放导演调 Dissolve 消散。
    /// 仅卡牌施放占链位（攻击/守卫/触发效果/SBA 不进链——2026-10-19 用户定案）。
    /// </summary>
    internal sealed class BattleChainStage
    {
        private RectTransform _chainRect;   // chain 容器（Bind 定位；缺节点=整链静默不显）
        private GameObject _template;       // 克隆模板（chain 下首个 ACard 实例，Bind 即隐藏）
        private readonly List<ChainSlot> _slots = new List<ChainSlot>(); // 克隆池（跨刷新复用）

        private const float PendingAlpha = 0.85f;  // 待结算
        private const float ResolvingAlpha = 1f;   // 当前步
        private const float ResolvedAlpha = 0.35f; // 已结算

        private sealed class ChainSlot
        {
            public RectTransform Rect;
            public ACardView View;
            public CanvasGroup Group;
            public uint RuntimeId;
            public bool Resolved;
        }

        /// <summary>绑定 chain 节点（Build 期一次）：定位容器、隐藏模板。缺节点/缺模板=静默禁用。</summary>
        public void Bind(RectTransform root)
        {
            if (root == null) return;
            var t = root.Find("chain");
            if (t == null)
            {
                Debug.Log("[ChainStage] BattleUI 缺 chain 节点——连锁链不显示");
                return;
            }
            _chainRect = (RectTransform)t;
            if (_chainRect.childCount > 0)
            {
                _template = _chainRect.GetChild(0).gameObject;
                _template.SetActive(false); // 模板只作克隆源，不参与布局/渲染
            }
            _chainRect.gameObject.SetActive(false);
        }

        /// <summary>对账（ApplyProjection 同频）：非结算期=全量对账（增删匹配 ChainCards——施放
        /// 失败/离栈的克隆即收）；结算期=只增不删（已结算卡保持到整轮完成由 Dissolve 统一收）。</summary>
        public void Refresh(BattleViewData d)
        {
            if (_chainRect == null || _template == null) return;
            bool resolving = d != null && d.ChainResolving;
            var cards = d?.ChainCards;
            if (!resolving && (cards == null || cards.Count == 0))
            {
                Dissolve();
                return;
            }

            // 增/改：按 ChainCards 底→顶序排布（sibling 序=链视觉序，模板恒占 0 位）
            int alive = 0;
            if (cards != null)
            {
                foreach (var v in cards)
                {
                    if (v == null || v.RuntimeId == 0) continue;
                    var slot = SlotOf(v.RuntimeId);
                    if (slot == null)
                    {
                        slot = CreateSlot();
                        slot.RuntimeId = v.RuntimeId;
                    }
                    slot.Rect.SetSiblingIndex(alive + 1);
                    slot.View.Apply(v.CoreCard != null
                        ? CardOverlayItem.FromLiveCard(v.CoreCard)
                        : CardOverlayItem.FromHand(v), CardOverlayLayout.Full);
                    if (!resolving) ApplyLook(slot); // 结算期外观归 Mark* 口管理，勿覆写
                    alive++;
                }
            }

            // 删：仅非结算期回收不在链上的克隆
            for (int i = _slots.Count - 1; i >= 0; i--)
            {
                if (resolving || ContainsRid(cards, _slots[i].RuntimeId)) continue;
                Object.Destroy(_slots[i].Rect.gameObject);
                _slots.RemoveAt(i);
            }
            _chainRect.gameObject.SetActive(_slots.Count > 0);
        }

        /// <summary>当前步高亮（回放导演逐步调）：目标卡满亮，其余未结算压暗、已结算维持灰。</summary>
        public void MarkResolving(uint rid)
        {
            foreach (var s in _slots)
            {
                if (rid != 0 && s.RuntimeId == rid) { s.Resolved = false; s.Group.alpha = ResolvingAlpha; }
                else if (!s.Resolved) s.Group.alpha = PendingAlpha;
            }
        }

        /// <summary>步结算完成变暗（回放导演逐步调）。</summary>
        public void MarkResolved(uint rid)
        {
            if (rid == 0) return; // 非卡步（攻击/效果/SBA）无链位
            var s = SlotOf(rid);
            if (s == null) return;
            s.Resolved = true;
            s.Group.alpha = ResolvedAlpha;
        }

        /// <summary>整轮结算完成：链消散（回放导演轮末调；看门狗/退屏亦调）。</summary>
        public void Dissolve()
        {
            foreach (var s in _slots) Object.Destroy(s.Rect.gameObject);
            _slots.Clear();
            if (_chainRect != null) _chainRect.gameObject.SetActive(false);
        }

        private ChainSlot SlotOf(uint rid)
        {
            foreach (var s in _slots) if (s.RuntimeId == rid) return s;
            return null;
        }

        private ChainSlot CreateSlot()
        {
            var go = Object.Instantiate(_template, _chainRect, false);
            go.name = "chain-card";
            go.SetActive(true);
            var slot = new ChainSlot
            {
                Rect = (RectTransform)go.transform,
                View = go.AddComponent<ACardView>(),
                Group = go.AddComponent<CanvasGroup>(),
                RuntimeId = 0,
            };
            slot.View.Bind();
            _slots.Add(slot);
            return slot;
        }

        private static bool ContainsRid(List<BattleCardView> cards, uint rid)
        {
            if (cards == null) return false;
            foreach (var v in cards) if (v != null && v.RuntimeId == rid) return true;
            return false;
        }

        private static void ApplyLook(ChainSlot s)
        {
            if (s.Group != null) s.Group.alpha = s.Resolved ? ResolvedAlpha : PendingAlpha;
        }
    }
}
