using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SynergyUI.Online;

namespace SynergyUI
{
    /// <summary>
    /// 主菜单 TapTap 登录徽章（2026-10-23 登录接入）：运行时克隆 MainUI 现有按钮换皮——
    /// 底色/字体/点击渐变全部继承预制体主题，零新增美术资产、零 prefab 手术。
    /// 三态：IsSupported=false（编辑器/未配置）=整体不建；未登录=「TapTap 登录」钮；
    /// 已登录=昵称条+「退出」钮。状态源=TapAccountService.OnChanged（主线程）。
    /// TODO(合规)：上架前按 TapTap《登录按钮设计规范》换官方按钮素材（开发者中心下载，
    /// 届时只动 Build 的克隆来源/贴图，状态机不动）。
    /// </summary>
    public sealed class TapLoginBadge : MonoBehaviour
    {
        private const float Gap = 20f;   // 昵称条与退出钮间距
        private const float Width = 320f; // 昵称/登录钮宽（与主菜单按钮同族，高 84 略低于 100）

        private RectTransform _btnLogin;
        private RectTransform _btnLogout;
        private RectTransform _nameNode;
        private TMP_Text _nameLabel;
        private Button _loginButton;
        private TMP_Text _loginLabel;
        private bool _loggingIn;
        private TipPromptHost _tips;

        /// <summary>挂到主菜单根（幂等——同名节点已在不重建；未支持平台直接不建）。</summary>
        public static void Attach(RectTransform cloneSource, RectTransform root)
        {
            if (root == null || cloneSource == null) return;
            if (root.Find("taptap-badge") != null) return;
            if (!TapAccountService.IsSupported) return; // 编辑器/未配置——徽章缺席即正确态

            var go = new GameObject("taptap-badge", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(root, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1, 1);
            rt.anchoredPosition = new Vector2(-40, -40);
            rt.sizeDelta = new Vector2(Width * 2 + Gap, 84);
            go.AddComponent<TapLoginBadge>().Build(cloneSource);
        }

        private void Build(RectTransform cloneSource)
        {
            _tips = new TipPromptHost(() => (RectTransform)transform.parent);

            _btnLogin = CloneButton(cloneSource, "btn-taptap", Vector2.zero, Width, "TapTap 登录");
            _loginButton = _btnLogin.GetComponent<Button>();
            _loginLabel = _btnLogin.GetComponentInChildren<TMP_Text>(true);
            _loginButton.onClick.AddListener(OnLoginClicked);

            _btnLogout = CloneButton(cloneSource, "btn-taptap-logout",
                new Vector2(-(Width + Gap), 0), 140, "退出");
            _btnLogout.GetComponent<Button>().onClick.AddListener(OnLogoutClicked);

            // 昵称条：克隆整只按钮后剥掉 Button/Image，只留换过文案的 label（字体/颜色继承）
            var nameGo = Instantiate(_btnLogin.gameObject, transform, false);
            nameGo.name = "lbl-taptap-name";
            Destroy(nameGo.GetComponent<Button>());
            Destroy(nameGo.GetComponent<Image>());
            _nameNode = (RectTransform)nameGo.transform;
            _nameNode.anchorMin = _nameNode.anchorMax = _nameNode.pivot = new Vector2(1, 1);
            _nameNode.anchoredPosition = Vector2.zero;
            _nameNode.sizeDelta = new Vector2(Width, 84);
            _nameLabel = nameGo.GetComponentInChildren<TMP_Text>(true);
            if (_nameLabel != null) _nameLabel.overflowMode = TextOverflowModes.Ellipsis; // 长昵称截断不溢出

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

        /// <summary>克隆主菜单按钮：换名/换文案/去源监听/重锚右上角。源按钮可处于隐藏态（克隆后强制激活）。</summary>
        private RectTransform CloneButton(RectTransform source, string name, Vector2 pos, float width, string text)
        {
            var go = Instantiate(source.gameObject, transform, false);
            go.name = name;
            go.SetActive(true);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1, 1);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(width, 84);
            var button = go.GetComponent<Button>();
            if (button != null) button.onClick.RemoveAllListeners(); // 运行时加的监听不随克隆走，保险再清
            var label = go.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = text;
            return rt;
        }
    }
}
