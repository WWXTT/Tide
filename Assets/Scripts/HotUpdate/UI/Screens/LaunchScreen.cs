using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace SynergyUI
{
    /// <summary>
    /// 启动页（2026-11-22 登录迁设置面板定案）：不再承载 TapTap 登录（登录/云存档/退出登录全部在
    /// 主菜单设置面板 panel-settings 的账号区）——本页退化为过场页：隐私同意（UIBootstrap 装）后
    /// 首屏停留一拍即自动进主菜单；<see cref="ForceShowForDebug"/> 可钉住调布局。
    /// 历史口径：2026-11-09 登录迁入本页 → 2026-11-22 用户定案再迁设置面板，LoginSlot/
    /// AttachLaunchLogin/NotifyLoginStateChanged/PendingNotice（云恢复重进提示，随「云恢复原地刷新」
    /// 定案一并退役）全链剃除。
    /// </summary>
    public sealed class LaunchScreen : UIScreen
    {
        protected override string RootName => "launch-screen";

        /// <summary>调试开关：钉住启动页不自动放行（编辑器观察布局用；运行期置位即可）。</summary>
        public static bool ForceShowForDebug;

        private UnityEngine.UI.Button _enterButton;
        private TMP_Text _enterLabel;
        private TMP_Text _hintLabel;
        private bool _advanced;          // 本轮进屏已放行进主菜单（防重复 Replace）

        protected override void Build()
        {
            Root = UiKit.Screen(RootName, Parent);

            UiKit.Size(UiKit.Label("lbl-title", Root, "潮  汐", 44, UiStyle.TextPrimary,
                TextAnchor.MiddleCenter, FontStyle.Bold), fw: 1f);
            UiKit.Size(UiKit.Label("lbl-sub", Root, "TIDE", 14, UiStyle.TextFaint,
                TextAnchor.MiddleCenter), fw: 1f);

            UiKit.Size(UiKit.Node("spacer-top", Root), fh: 1f);

            var enterRow = UiKit.Row("row-enter", Root);
            enterRow.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            UiKit.Size(enterRow, fw: 1f);
            _enterButton = UiKit.Button("btn-enter", enterRow, "进入", EnterMainMenu);
            _enterLabel = _enterButton != null ? _enterButton.GetComponentInChildren<TMP_Text>(true) : null;

            _hintLabel = UiKit.Label("lbl-hint", Root, "", 13, UiStyle.TextHint,
                TextAnchor.MiddleCenter, wrap: true);
            UiKit.Size(_hintLabel, fw: 1f);

            UiKit.Size(UiKit.Node("spacer-bottom", Root), fh: 1f);
        }

        public override void OnEnter()
        {
            _advanced = false;
            if (_enterLabel != null) _enterLabel.text = "进入";
            // 登录不可用口径已退役（登录在设置面板）——恒自动放行，仅调试钉住时停留
            if (!ForceShowForDebug) AutoAdvance().Forget();
        }

        /// <summary>放行进主菜单（Replace——本页不入导航历史，主菜单不可 Back 回本页）。</summary>
        private void EnterMainMenu()
        {
            if (_advanced || Manager == null) return;
            if (Manager.Current != this) return; // 已被切走（如自动放行竞态）
            _advanced = true;
            Manager.Replace<MainMenuScreen>();
        }

        /// <summary>自动放行：延一帧出 OnEnter 装配链再 Replace，免在 Mount 中途操纵栈；
        /// 期间玩家先点了按钮或已切屏则放弃。</summary>
        private async UniTaskVoid AutoAdvance()
        {
            await UniTask.DelayFrame(1);
            if (_advanced) return;
            if (ForceShowForDebug) return; // 调试钉住：等玩家手动进入
            EnterMainMenu();
        }
    }
}
