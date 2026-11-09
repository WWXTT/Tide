using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SynergyUI.Online;
using Tide.HotUpdate;

namespace SynergyUI
{
    /// <summary>
    /// 启动页 TapTap 登录区（2026-11-09 登录迁移：主菜单右上徽章退役，登录 UI 迁入 LaunchScreen
    /// 的 LoginSlot——本类实例化进槽位居中摆放，视觉烘焙在 TapLoginBadge.prefab，锚点按槽位口径）。
    /// 两态：未登录=「TapTap 登录」按钮（登录成功→云侧补登录+启动同步→启动页自动进主菜单）；
    /// 已登录=昵称行「已登录 {昵称}，正在进入…」（静默续登/换页闪现）。退出登录按钮退役——
    /// 退出归主菜单云存档面板（用户定案）。状态源=两侧账号服务事件（任一登录即算已登录：
    /// 云侧 basic_info 静默或联机侧 PUBLIC_PROFILE 手动，先到先放行）。
    /// TODO(合规)：上架前按 TapTap《登录按钮设计规范》换官方按钮素材（开发者中心下载，
    /// 届时只动 prefab 换贴图，状态机不动）。
    /// </summary>
    public sealed class TapLoginBadge : MonoBehaviour
    {
        private RectTransform _btnLogin;
        private RectTransform _nameNode;
        private TMP_Text _nameLabel;
        private Button _loginButton;
        private TMP_Text _loginLabel;
        private bool _loggingIn;
        private TipPromptHost _tips;
        private LaunchScreen _screen;

        private static GameObject _prefab; // 进程级预制体句柄保活（同 TipPromptHost 口径）

        /// <summary>往启动页登录槽位实例化登录区并接线（每次进屏槽位全新——实例随屏销毁重建）。
        /// 返回登录可用性（false=编辑器/未配置/平台不支持——不实例化，启动页走游客自动放行）。</summary>
        public static bool Attach(LaunchScreen screen)
        {
            var slot = screen?.LoginSlot;
            if (slot == null) return false;
            if (!TapAccountService.IsSupported) return false; // 编辑器/未配置——登录区缺席即正确态

            if (_prefab == null)
                _prefab = HotUpdateAssets.Load<GameObject>("TapLoginBadge", UiKit.PrefabPath("TapLoginBadge"));
            if (_prefab == null)
            {
                Debug.LogError("[TapLoginBadge] TapLoginBadge.prefab 加载失败——登录区不显示"
                               + "（检查 Assets/Art/Prefab/TapLoginBadge.prefab 与 Yoo UI 组收集）");
                return false;
            }
            var go = Instantiate(_prefab, slot, false);
            go.name = "taptap-login"; // 锚点/尺寸烘焙在预制体根（居中摆放，随槽位）
            go.AddComponent<TapLoginBadge>().Build(screen);
            return true;
        }

        private void Build(LaunchScreen screen)
        {
            _screen = screen;
            var rt = (RectTransform)transform;
            _tips = new TipPromptHost(() => (RectTransform)rt.parent);

            _btnLogin = UiKit.FindDeep(rt, "btn-login");
            _loginButton = _btnLogin != null ? _btnLogin.GetComponentInChildren<Button>(true) : null;
            _loginLabel = _btnLogin != null ? _btnLogin.GetComponentInChildren<TMP_Text>(true) : null;
            if (_loginButton != null) _loginButton.onClick.AddListener(OnLoginClicked);

            _nameNode = UiKit.FindDeep(rt, "lbl-name"); // 预制体默认 inactive
            _nameLabel = _nameNode != null ? _nameNode.GetComponentInChildren<TMP_Text>(true) : null;

            // btn-logout 退役（2026-11-09 用户定案：退出登录归云存档面板）——节点若残留强制隐藏
            var logoutNode = UiKit.FindDeep(rt, "btn-logout");
            if (logoutNode != null) logoutNode.gameObject.SetActive(false);

            TapAccountService.OnChanged += OnAccountChanged;           // 联机侧（PUBLIC_PROFILE 手动登录/登出）
            CloudSave.TapAccountService.OnAccountChanged += OnAccountChanged; // 云侧（basic_info 静默登录/登出）
            Refresh();
        }

        private void OnDestroy()
        {
            TapAccountService.OnChanged -= OnAccountChanged;
            CloudSave.TapAccountService.OnAccountChanged -= OnAccountChanged;
        }

        /// <summary>任一侧登录态变化：已登录（任一侧）→通知启动页放行；否则回登录按钮态。</summary>
        private void OnAccountChanged()
        {
            Refresh();
            _screen?.NotifyLoginStateChanged(IsLoggedIn);
        }

        private static bool IsLoggedIn =>
            CloudSave.TapAccountService.LoggedIn || TapAccountService.IsLoggedIn;

        private void Refresh()
        {
            bool logged = IsLoggedIn;
            if (_btnLogin != null) _btnLogin.gameObject.SetActive(!logged);
            if (_nameNode != null)
            {
                _nameNode.gameObject.SetActive(logged);
                if (logged && _nameLabel != null)
                {
                    var profile = TapAccountService.Current;
                    _nameLabel.text = profile != null ? $"已登录 {profile.Name}，正在进入…" : "已登录，正在进入…";
                }
            }
        }

        private void OnLoginClicked() => LoginFlow().Forget();

        private async UniTaskVoid LoginFlow()
        {
            if (_loggingIn) return;
            _loggingIn = true;
            if (_loginButton != null) _loginButton.interactable = false;
            if (_loginLabel != null) _loginLabel.text = "登录中…";
            try
            {
                // 联机侧拉起授权（PUBLIC_PROFILE——拿昵称头像）；云侧（basic_info 静默）随后补登录
                // 并启动云同步（LoginFromUiAsync=UniTaskVoid 火Forget——登录态经事件回报，
                // OnChanged→放行进主菜单，无需在此 await）。
                if (TapAccountService.IsLoggedIn)
                {
                    CloudSave.TapAccountService.LoginFromUiAsync(); // 罕见态兜底：联机侧已登录、云侧掉队
                }
                else
                {
                    var error = await TapAccountService.LoginAsync();
                    if (error != null)
                    {
                        if (_tips != null) await _tips.ShowAsync(error, "知道了"); // 成功态经 OnChanged→放行
                        return;
                    }
                    CloudSave.TapAccountService.LoginFromUiAsync();
                }
            }
            finally
            {
                _loggingIn = false;
                if (_loginButton != null) _loginButton.interactable = true;
                if (_loginLabel != null) _loginLabel.text = "TapTap 登录";
                Refresh(); // 授权未完成/失败时恢复按钮态（成功则本对象已随屏销毁，本行不达）
            }
        }
    }
}
