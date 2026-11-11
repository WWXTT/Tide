using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.AI;
using CardCore.Attribute;
using CardCore.Network;
using CardCore.Serialization;
using GameBoard;
using Tide.HotUpdate;
    using Cysharp.Threading.Tasks;
    using UnityEngine;
    using UnityEngine.EventSystems;
    using UnityEngine.UI;
    using TMPro;

namespace SynergyUI
{
    public sealed partial class BattleScreen : UIScreen
    {
        // ======================================== 英雄区（2026-10-13 区域变化：技能与角色同区） ========================================

        /// <summary>是否有进行中的目标选择/弹窗：本屏列表模态在身，或引擎结算期反问在途。
        /// <b>两语义裁定（2026-10-13 用户定案）：目标选择期点击角色=「把角色选为目标」；
        /// 主动阶段（我方主阶段·无待选）点击角色=「发动技能」。二者不得混流——选择期一切
        /// 角色点击先走目标语义，实体点选接线属 3D 层目标选择重做（当前列表模态自带遮罩挡点击）。</summary>
        private bool IsTargetSelecting()
        {
            if (_regionSelector != null && _regionSelector.IsOpen) return true;  // Region 选择面在身（反问/响应窗/选项抉择——2026-10-16 弹窗退役）
            if (TargetSelectionService.PendingRequests > 0) return true; // 引擎结算期反问在途
            if (_choreo != null && _choreo.IsTargeting) return true;     // 3D 寻路箭头选目标在途（2026-10-13）
            return false;
        }

        /// <summary>点英雄区（左键）：发动英雄技能（2026-10-13 区域变化定案——英雄技能与角色共用本区，
        /// 主动阶段选中英雄即发动技能）；未携带技能的局回落角色攻击。
        /// 目标选择期点击不走技能（见 IsTargetSelecting 两语义裁定）。</summary>
        private void OnClickHeroZone()
        {
            if (IsTargetSelecting()) return; // 选择期点击=目标语义，由选择器接管（模态遮罩本就挡住；防御性双保险）
            if (!GateFanMyMain()) return;
            var skill = HeroSkillSystem.ResolveSkillCard(Core, P1);
            if (skill == null) { _toast.Show("本局无技能卡"); return; } // 无技能局：发动键本就不亮（防直点兜底）
            ActivateSkillAsync().Forget();
        }

        /// <summary>过渡面主阶段闸（活数据口径）：GateMyMain 依赖 _view（旧渲染链刷新产物，扇形过渡面
        /// 恒 null——纯扇形流程无人调 RefreshLocal，首点必然静默失败）。本地局直接读 TurnEngine 现值。</summary>
        private bool GateFanMyMain()
        {
            if (_gameEnded) return false;
            if (IsTargetSelecting()) return false; // Region 抉择/反问在身不开新交互（同 GateMyMain，静默）
            if (_ctrl == null || Core == null || P1 == null) return false;
            var phase = Core.TurnEngine?.CurrentPhase;
            if (_ctrl.TurnPlayer != P1 || phase == null || phase.Phase != PhaseType.Main)
            {
                _toast.Show(_ctrl.TurnPlayer != P1 ? "对手回合，暂不可操作" : "当前不是主要阶段");
                return false;
            }
            return true;
        }

        // RefreshHeroZone/TempTokenColor（HUD 英雄区按钮+临时元素指示条刷新）：2026-10-14 定案随
        // btn-self-hero/temp-elements 一并退役——角色生命/弹药=角色格悬停、技能=角色格悬停+三键
        //（2026-10-16 技能格撤销后口径，黑白钱包=地牌行黑白元素格）。

        // ---- 中栏按钮 ----
        // （跳过准备阶段按钮已删：准备阶段纯自动推进——TurnEngine.StartNewTurn 内
        //   AdvanceFromStandby 直调，2026-09-24 定案；UI 不再有停等入口。）

        // 墓地使用入口（OnGraveyardPlay）已随 2026-10-30 归土改版整删：墓地视手牌使用退役，
        // 归土=回合末自动复活（引擎 RuleAuraSystem.ReviveAtTurnEnd，无 UI 交互）。

        // 技能=一回合一次重复用的法术（2026-11-01 锚价现付定案）：与同名法术**不同速**——
        // 发动速度恒 0（不吃法术槽位盈余）；费用=所选效果锚价现付（引擎 DeriveElementCosts 同源，
        // 技能栏费用显示同为锚价口径——两端口径合流，推翻 2026-10-18「免锚价+抉择另收1灰」）。
        // 多效果技能与法术同款施放时抉择（Region 面板 PickRegionOptionAsync——2026-10-16 弹窗
        // 退役后的唯一抉择 UI，AI 侧 TideActionType.HeroSkill 的 ModeIndex 同为效果序号）。

        /// <summary>技能发动费文案（与引擎锚价现付同源：逐色 ceil——支付终点 NormalizeBill 同口径）。</summary>
        private static string SkillFeeZh(EffectDefinition fx)
        {
            var c = CostDerivationService.DeriveElementCosts(fx);
            if (c == null || c.IsZero) return "免费";
            var parts = new List<string>();
            foreach (var k in c.NonzeroColors())
                parts.Add($"{Math.Max(1, (int)Math.Ceiling(c[k]))}{ManaTypeNames.ZhNameOf(k)}");
            return $"付{string.Join("", parts)}";
        }

        private async UniTaskVoid ActivateSkillAsync(int effectIndex = 0)
        {
            if (NetActive) { NetActivateSkillAsync().Forget(); return; } // 网络局：intent 上行（服务器横置闸）
            var skill = HeroSkillSystem.ResolveSkillCard(Core, P1);
            var effects = skill != null ? HeroSkillSystem.SkillEffectsOf(skill) : null;
            if (effects != null && effects.Count > 1)
            {
                var options = new List<string>();
                for (int i = 0; i < effects.Count; i++)
                {
                    string name = string.IsNullOrEmpty(effects[i]?.DisplayName) ? $"效果 {i + 1}" : effects[i].DisplayName;
                    // 抉择技能定价=所选效果锚价（引擎 DeriveElementCosts 同源）；发动速度恒 0
                    options.Add($"{name}（{SkillFeeZh(effects[i])}·速度0）");
                }
                int? picked = await PickRegionOptionAsync("选择要发动的效果", options);
                if (picked == null) return; // 缺 Region=无交互（本已点「发动」，静默取消）
                effectIndex = picked.Value;
            }
            bool ok = GameActions.ActivateHeroSkill(Core, P1, effectIndex, out var skillReason);
            if (!ok) _toast.Show(skillReason ?? "无法发动技能（本回合已用/阶段不对/费用付不出）"); // 引擎真因（2026-11-03 失败提示细化）
            else _ctrl.SettleResponseWindow().Forget(); // 无候选自动双 Pass，无害
            RefreshField(); // 地牌横置/角色弹药随对账刷新（旧渲染链字段已不绑）
        }

        /// <summary>投降确认（2026-10-17 提示弹窗改 TipPanel：确定、第二键=取消——无超时
        /// 不会误投降；Region 只留给对局中目标选择）。终局后本键=返回主菜单（MarkGameOverUi 改文案）。
        /// 复盘局（2026-11-11 S2）：本键=「退出复盘」直回（无对局可投降）。</summary>
        private async UniTaskVoid OnConcede()
        {
            if (BattleEntry.Mode == BattleMode.Replay) { Manager.Back(); return; } // 复盘退出（键文案已改）
            if (_gameEnded) { Manager.Back(); return; } // 终局：退出通道（旧弹窗取消键职责迁移）
            if (await ShowTipPromptAsync("投降确认", "确认", "取消") != true) return; // 取消/拆屏=不动
            if (NetActive)
            {
                // 网络局：认输 intent——终局经事件流回传（MarkGameOverUi），再走返回通道
                _netClient.Send<object>(NetworkMessageType.IntentConcede, null);
                return;
            }
            Core.EndGame(P2, GameOverReason.Concede);
            RefreshLocal();
        }

        // ======================================== 本地响应窗口（发动弹窗·沿用旧语义） ========================================

        // ======================================== 选项抉择（Region 卡牌列表） ========================================

        // 旧现搭弹窗层（ShowPickOverlay/ShowChoiceOverlay/ShowOverlay/AddCancelButton/
        // SetCancelText/AddOverlayRow/CloseOverlay/OnOverlayCancel——UiKit.Overlay 代码兜底 +
        // ModalBox 全屏遮罩）已随 2026-10-16 定案整删：遮罩拦棋盘点选（守卫窗口卡死）屡次坑人，
        ///BattleUI 内一切临时弹窗 UI 禁止回潮——纯选项抉择统一走下方 PickRegionOptionAsync。

        /// <summary>Region 卡牌列表选一项（2026-10-16 弹窗退役）：选项=最小卡面（卡名=选项文本），
        /// min=1 强制选（无取消口——已进入的流程选一项完成；超时代选首项）。null=缺 Region
        /// 或无选项（调用方按取消处理）。危险确认（如投降）把「取消」放首位——超时代选只会落取消。</summary>
        private async UniTask<int?> PickRegionOptionAsync(string title, IReadOnlyList<string> options)
        {
            if (_regionSelector == null || options == null || options.Count == 0) return null;
            var picked = await _regionSelector.SelectIndicesAsync(options, 1, 1, title, "", false, 60f);
            return picked != null && picked.Count > 0 ? (int?)picked[0] : null;
        }

        private static string CardDisplayName(Card c)
        {
            if (c is CardWrapper w)
            {
                var d = w.GetData();
                return string.IsNullOrEmpty(d?.CardName) ? d?.ID ?? "?" : d.CardName;
            }
            return c != null ? c.ToString() : "?";
        }

        // ======================================== 选择/响应提示（2026-10-17 label-num 并入定案） ========================================
        // lbl-toast 面板退役（用户挪入 Region 更名 label-num·与 btn-notchain 成对）：提示只有两类
        // ①效果发动的目标选择——介绍完整效果（说明选目标来干嘛）；②响应时——说明响应的是对方的
        // 什么发动。两类全文由 RegionTargetSelector 直写 label-num（+（x/y）计数），不再走顶层 toast。
        // 取消口成对不变：主动效果（生物/法术）可「取消不发动」（AllowCancel→空目标落空）；
        // 可连锁响应可「不连锁」；自动/强制发动不可拒——不选=超时默认代选（Service AutoPick 既有口径）。
        // 场面信息（地牌/单位/格子介绍）不走提示——hover-info 单源（LandCellInfo/CardHoverInfo）。

        /// <summary>选择窗 label-num 提示文案：PromptSource（宿主卡）+PromptEffectId 查卡表
        /// AtomText.RenderEffectSummary 渲染完整效果文（卡面/悬停同源）；查不到效果只报卡名+标题；
        /// 无宿主回落 Title（弃牌「回合结束，手牌超出X张」等自带说明的标题即正文）。</summary>
        private string BuildSelectPrompt(TargetSelectionRequest req)
        {
            string title = string.IsNullOrWhiteSpace(req.Title) ? "请选择目标" : req.Title;
            if (req.PromptSource is Card host)
            {
                var data = host is CardWrapper w ? w.GetData() : null;
                var fx = string.IsNullOrEmpty(req.PromptEffectId)
                    ? null
                    : data?.Effects?.FirstOrDefault(e => e != null && e.Id == req.PromptEffectId);
                if (fx != null)
                {
                    string body = AtomText.RenderEffectSummary(
                        new EffectGraphData(fx.DisplayName) { header = fx, steps = fx.Steps }, data);
                    string fxName = string.IsNullOrEmpty(fx.DisplayName) ? title : fx.DisplayName;
                    return $"【{CardDisplayName(host)}·{fxName}】{body}\n——为上述效果选择目标";
                }
                return $"【{CardDisplayName(host)}】{title}\n——选择目标";
            }
            return title;
        }




        // 声明期目标原子查找（FindTargetingAtomic/FirstTargetingAtomicOf）已随 2026-10-16
        // 法术声明期预选退役删除——目标选择统一走结算期反问（TargetSelectionService）。

        /// <summary>抉择模式显示名（第一个 Choice 步骤的 label 列表；无则空表）。</summary>
        private static List<string> FindChoiceLabels(CardData data)
        {
            var labels = new List<string>();
            if (data?.Effects == null) return labels;
            foreach (var eff in data.Effects)
            {
                if (eff?.Steps == null) continue;
                foreach (var step in eff.Steps)
                {
                    if (step?.choices == null || step.choices.Count < 2) continue;
                    foreach (var c in step.choices)
                        labels.Add(c?.label);
                    return labels;
                }
            }
            return labels;
        }

    }
}
