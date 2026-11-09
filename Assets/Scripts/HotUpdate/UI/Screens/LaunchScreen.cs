using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using Tide.HotUpdate;

namespace SynergyUI
{
    /// <summary>
    /// 启动页（2026-11-09 TapTap 登录迁移定案）：隐私同意后的首屏——TapTap 登录迁入本页
    ///（Tide.TapTap 经 <see cref="TapBridge"/>.AttachLaunchLogin 往 <see cref="LoginSlot"/> 注入
    /// 登录按钮/账号态，登录态变化经 <see cref="NotifyLoginStateChanged"/> 回调本页），
    /// 游客可进（不登录可玩单机内容，云存档/联机需要登录时再回本页引导）。
    /// 自动放行规则：登录不可用（本地单机风味/编辑器/未配置）直接进主菜单——本页不驻留；
    /// 已登录（含启动静默续登）也直接进主菜单。<see cref="ForceShowForDebug"/> 可钉住本页调布局。
    /// 登出/云恢复统一经 <see cref="UIBootstrap.ReturnToLaunch"/> 清栈回本页（Replace 进主菜单，
    /// 本页不入导航历史）。
    /// </summary>
    public sealed class LaunchScreen : UIScreen
    {
        protected override string RootName => "launch-screen";

        /// <summary>调试开关：钉住启动页不自动放行（编辑器观察布局/登录区接线用；运行期置位即可）。</summary>
        public static bool ForceShowForDebug;

        /// <summary>一次性停留提示（2026-11-09 云恢复反馈）：置位后下一次进本页不自动放行，
        /// 把文案显示在提示行，由玩家手动进入（云恢复完成=数据已重载，给一个明确的回场节拍）。</summary>
        public static string PendingNotice;

        /// <summary>登录区槽位：Tide.TapTap 的登录 UI 实例化挂点（居中定宽；单机风味空置）。
        /// public=跨程序集（Tide.TapTap 经 TapBridge 注入，internal 不可见）。</summary>
        public RectTransform LoginSlot { get; private set; }

        private UnityEngine.UI.Button _guestButton;
        private TMP_Text _guestLabel;
        private TMP_Text _hintLabel;
        private bool _loggedIn;
        private bool _advanced;          // 本轮进屏已放行进主菜单（防重复 Replace）
        private bool _holdForNotice;     // 本轮进屏因 PendingNotice 停留（不自动放行）

        protected override void Build()
        {
            Root = UiKit.Screen(RootName, Parent);

            UiKit.Size(UiKit.Label("lbl-title", Root, "潮  汐", 44, UiStyle.TextPrimary,
                TextAnchor.MiddleCenter, FontStyle.Bold), fw: 1f);
            UiKit.Size(UiKit.Label("lbl-sub", Root, "TIDE", 14, UiStyle.TextFaint,
                TextAnchor.MiddleCenter), fw: 1f);

            UiKit.Size(UiKit.Node("spacer-top", Root), fh: 1f);

            LoginSlot = UiKit.Node("login-slot", Root);
            UiKit.Size(LoginSlot, w: 460f, h: 110f);

            var guestRow = UiKit.Row("row-guest", Root);
            guestRow.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            UiKit.Size(guestRow, fw: 1f);
            _guestButton = UiKit.Button("btn-guest", guestRow, "游客进入", EnterMainMenu);
            _guestLabel = _guestButton != null ? _guestButton.GetComponentInChildren<TMP_Text>(true) : null;

            _hintLabel = UiKit.Label("lbl-hint", Root, "", 13, UiStyle.TextHint,
                TextAnchor.MiddleCenter, wrap: true);
            UiKit.Size(_hintLabel, fw: 1f);

            UiKit.Size(UiKit.Node("spacer-bottom", Root), fh: 1f);
        }

        public override void OnEnter()
        {
            _advanced = false;
            _loggedIn = false;
            _holdForNotice = PendingNotice != null;
            if (_holdForNotice)
            {
                if (_hintLabel != null) _hintLabel.text = PendingNotice;
                PendingNotice = null;
            }

            // 登录区装配（单机风味 Module=null→登录不可用→游客自动放行；联机风味返回 true，
            // 装配内部若已登录会立即回调 NotifyLoginStateChanged(true) 触发放行）
            bool loginAvailable = TapBridge.Module?.AttachLaunchLogin(this) ?? false;
            if (!loginAvailable)
            {
                if (_hintLabel != null && !_holdForNotice)
                    _hintLabel.text = "TapTap 登录在真机联机包可用（当前环境不支持，直接进入）";
                RefreshGuestLabel();
                if (!ForceShowForDebug && !_holdForNotice) AutoAdvance().Forget();
            }
            else
            {
                if (_hintLabel != null && !_holdForNotice)
                    _hintLabel.text = "登录 TapTap 可同步云存档并联机对战；游客进入仅本机游玩";
                RefreshGuestLabel();
            }
        }

        /// <summary>登录态回调（Tide.TapTap 注入方接线：云侧/联机侧任一登录成功→true，登出→false）。
        /// true=自动进主菜单；false=刷新按钮文案等待玩家选择。public=跨程序集可见。</summary>
        public void NotifyLoginStateChanged(bool loggedIn)
        {
            _loggedIn = loggedIn;
            RefreshGuestLabel();
            if (loggedIn) AutoAdvance().Forget();
        }

        /// <summary>放行进主菜单（Replace——本页不入导航历史，主菜单不可 Back 回本页）。</summary>
        private void EnterMainMenu()
        {
            if (_advanced || Manager == null) return;
            if (Manager.Current != this) return; // 已被切走（如自动放行竞态）
            _advanced = true;
            Manager.Replace<MainMenuScreen>();
        }

        /// <summary>自动放行（已登录/登录不可用路径）：延一帧出 OnEnter 装配链再 Replace，
        /// 免在 Mount 中途操纵栈；期间玩家先点了按钮或已切屏则放弃。</summary>
        private async UniTaskVoid AutoAdvance()
        {
            await UniTask.DelayFrame(1);
            if (_advanced) return;
            if (_holdForNotice || ForceShowForDebug) return; // 停留节拍/调试钉住：等玩家手动进入
            EnterMainMenu();
        }

        private void RefreshGuestLabel()
        {
            if (_guestLabel == null) return;
            // 已登录=正常进入；未登录=游客语义（提示其云存档/联机不可用）
            _guestLabel.text = _loggedIn ? "进入" : "游客进入";
        }
    }
}
