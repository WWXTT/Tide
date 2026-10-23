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
    /// 主菜单 TapTap 登录徽章（2026-10-23 预制体化）：视觉全部烘焙在 TapLoginBadge.prefab
    /// （按钮=主菜单按钮同款平色底+ColorTint，字体/对齐烘焙），本类只负责加载实例化与
    /// 三节点绑定（TipPromptHost 同款加载姿势——HotUpdateAssets.Load + Instantiate）。
    /// 三态：IsSupported=false（编辑器/未配置）=不实例化（徽章缺席即正确态）；
    /// 未登录=「TapTap 登录」钮；已登录=昵称条+「退出」钮。状态源=TapAccountService.OnChanged（主线程）。
    /// TODO(合规)：上架前按 TapTap《登录按钮设计规范》换官方按钮素材（开发者中心下载，
    /// 届时只动 prefab 换贴图，状态机不动）。
    /// </summary>
    public sealed class TapLoginBadge : MonoBehaviour
    {
        private RectTransform _btnLogin;
        private RectTransform _btnLogout;
        private RectTransform _nameNode;
        private TMP_Text _nameLabel;
        private Button _loginButton;
        private TMP_Text _loginLabel;
        private bool _loggingIn;
        private TipPromptHost _tips;

        private static GameObject _prefab; // 进程级预制体句柄保活（同 TipPromptHost 口径）

        /// <summary>实例化徽章预制体并接线（幂等——同名节点已在不重建；未支持平台直接不建）。</summary>
        public static void Attach(RectTransform root)
        {
            if (root == null) return;
            if (root.Find("taptap-badge") != null) return;
            if (!TapAccountService.IsSupported) return; // 编辑器/未配置——徽章缺席即正确态

            if (_prefab == null)
                _prefab = HotUpdateAssets.Load<GameObject>("TapLoginBadge", UiKit.PrefabPath("TapLoginBadge"));
            if (_prefab == null)
            {
                Debug.LogError("[TapLoginBadge] TapLoginBadge.prefab 加载失败——徽章不显示"
                               + "（检查 Assets/Art/Prefab/TapLoginBadge.prefab 与 Yoo UI 组收集）");
                return;
            }
            var go = Instantiate(_prefab, root, false);
            go.name = "taptap-badge"; // 锚点/尺寸烘焙在预制体根（右上 -40,-40）
            go.AddComponent<TapLoginBadge>().Build();
        }

        private void Build()
        {
            var rt = (RectTransform)transform;
            _tips = new TipPromptHost(() => (RectTransform)rt.parent);

            _btnLogin = UiKit.FindDeep(rt, "btn-login");
            _loginButton = _btnLogin != null ? _btnLogin.GetComponentInChildren<Button>(true) : null;
            _loginLabel = _btnLogin != null ? _btnLogin.GetComponentInChildren<TMP_Text>(true) : null;
            if (_loginButton != null) _loginButton.onClick.AddListener(OnLoginClicked);

            _btnLogout = UiKit.FindDeep(rt, "btn-logout");
            var logoutButton = _btnLogout != null ? _btnLogout.GetComponentInChildren<Button>(true) : null;
            if (logoutButton != null) logoutButton.onClick.AddListener(OnLogoutClicked);

            _nameNode = UiKit.FindDeep(rt, "lbl-name"); // 预制体默认 inactive
            _nameLabel = _nameNode != null ? _nameNode.GetComponentInChildren<TMP_Text>(true) : null;

            TapAccountService.OnChanged += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            TapAccountService.OnChanged -= Refresh;
        }

        private void Refresh()
        {
            bool logged = TapAccountService.IsLoggedIn;
            if (_btnLogin != null) _btnLogin.gameObject.SetActive(!logged);
            if (_nameNode != null)
            {
                _nameNode.gameObject.SetActive(logged);
                if (logged && _nameLabel != null) _nameLabel.text = TapAccountService.Current.Name;
            }
            if (_btnLogout != null) _btnLogout.gameObject.SetActive(logged);
        }

        private void OnLoginClicked() => LoginFlow().Forget();

        private async UniTaskVoid LoginFlow()
        {
            if (_loggingIn) return;
            _loggingIn = true;
            _loginButton.interactable = false;
            if (_loginLabel != null) _loginLabel.text = "登录中…";
            try
            {
                var error = await TapAccountService.LoginAsync();
                if (error != null) await _tips.ShowAsync(error, "知道了"); // 成功态经 OnChanged→Refresh
            }
            finally
            {
                _loggingIn = false;
                _loginButton.interactable = true;
                if (_loginLabel != null) _loginLabel.text = "TapTap 登录";
            }
        }

        private void OnLogoutClicked() => TapAccountService.Logout(); // 状态回刷经 OnChanged→Refresh
    }
}
