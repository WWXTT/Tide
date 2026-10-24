using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SynergyUI
{
    /// <summary>
    /// 首次启动隐私同意弹窗（2026-10-24 TapTap 上架合规）：进任何功能界面之前强制展示，
    /// 同意→Grant+补发 TapTap 模块初始化+进主菜单；不同意→退出应用。文本源=
    /// Assets/Resources/PrivacyPolicy.txt（与线上 https://chaoxiysxy.oss-cn-chengdu.aliyuncs.com/index.html
    /// 逐字一致——TapTap 要求后台链接与 APP 内展示内容一致）。
    /// 正文用旧版 uGUI Text（FreeType 按需光栅，无 TMP 图集覆盖问题）——项目 TMP 约定在此
    /// 长文场景豁免；字体=项目 TTF（UiKit.TmpFont.sourceFontFile，庞门正道标题体，全文 640 唯一字符已验覆盖）。
    /// </summary>
    public static class PrivacyConsentDialog
    {
        private static GameObject _open;

        /// <summary>弹窗是否在途（UIBootstrap 再使能守卫用——防域重载路径重建主菜单旁路同意闸）。</summary>
        public static bool IsOpen => _open != null;

        /// <summary>在 Canvas 根上开弹窗。onAgreed=点击同意后的续启回调（Grant+补发 SDK 初始化+进主菜单）。</summary>
        public static void Open(RectTransform canvasRoot, Action onAgreed)
        {
            if (IsOpen) return;

            var dim = UiKit.Overlay("privacy-consent", canvasRoot);
            var dimImg = dim.gameObject.AddComponent<Image>();
            dimImg.color = UiStyle.OverlayDim;
            dimImg.raycastTarget = true; // 模态：挡住下层一切点击

            var panel = UiKit.Node("panel", dim);
            var pimg = panel.gameObject.AddComponent<Image>();
            pimg.sprite = UiKit.RoundedSprite;
            pimg.type = Image.Type.Sliced;
            pimg.color = UiStyle.PanelBg;
            var ol = panel.gameObject.AddComponent<Outline>();
            ol.effectColor = UiStyle.Border;
            ol.effectDistance = Vector2.one;
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(1060f, 860f);

            var g = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            g.spacing = 12f;
            g.padding = new RectOffset(20, 20, 18, 18);
            g.childAlignment = TextAnchor.UpperLeft;
            g.childControlWidth = true;
            g.childControlHeight = true;
            g.childForceExpandWidth = false;
            g.childForceExpandHeight = false;

            UiKit.Size(UiKit.Label("title", panel, "潮汐隐私政策", 22, UiStyle.TextPrimary,
                TextAnchor.MiddleLeft, FontStyle.Bold), fw: 1f);

            var scroll = UiKit.ScrollColumn("policy-scroll", panel, spacing: 8f, pad: 10f);
            UiKit.Size(scroll.Rect, fw: 1f, fh: 1f);

            var body = UiKit.Node("body", scroll.Content);
            var text = body.gameObject.AddComponent<Text>();
            var tmpFont = UiKit.TmpFont;
            text.font = tmpFont != null ? tmpFont.sourceFontFile : null;
            if (text.font == null)
            {
                Debug.LogError("[PrivacyConsent] 项目字体缺失——政策正文回退引擎内置字体，中文可能显示异常");
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            text.text = LoadPolicyText();
            text.fontSize = 22;
            text.lineSpacing = 1.15f;
            text.color = UiStyle.TextBody;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow; // 高度交给 content 的 CSF 量 preferred
            text.raycastTarget = true; // 文字区可拖拽滚动
            text.supportRichText = false;
            // 变高子项标准接法（ScrollColumn 的 VLG childControlHeight=false 只认子项当前 rect 高）：
            // CSF 读旧版 Text 自带的 ILayoutElement.preferredHeight 驱动 body 自身高度，
            // 再经 VLG 汇总喂给 content 的 CSF——正文多高、滚动区就多高。
            body.gameObject.AddComponent<ContentSizeFitter>()
                .verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var bar = UiKit.Row("bar", panel, spacing: 16f);
            bar.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            UiKit.Button("btn-agree", bar, "同意并进入游戏",
                () => { Close(); PrivacyConsent.Grant(); onAgreed?.Invoke(); },
                bg: UiStyle.BtnPrimary, fg: UiStyle.White, width: 300f, height: 44f, fontSize: 16);
            UiKit.Button("btn-refuse", bar, "不同意并退出",
                RefuseAndQuit, width: 300f, height: 44f, fontSize: 16);

            _open = dim.gameObject;
            Debug.Log("[PrivacyConsent] 首启隐私同意弹窗已展示（同意前不初始化任何第三方 SDK）");
        }

        /// <summary>政策全文（与线上同源）。资产缺失不阻断弹窗结构，但落 Error 暴露打包遗漏。</summary>
        private static string LoadPolicyText()
        {
            var asset = Resources.Load<TextAsset>("PrivacyPolicy");
            if (asset != null && !string.IsNullOrEmpty(asset.text)) return asset.text;
            Debug.LogError("[PrivacyConsent] Assets/Resources/PrivacyPolicy.txt 缺失或为空——政策正文不可用（检查打包收录）");
            return "（隐私政策文本缺失：Assets/Resources/PrivacyPolicy.txt 未随包收录。完整版见线上隐私政策页面。）";
        }

        private static void RefuseAndQuit()
        {
            Debug.Log("[PrivacyConsent] 用户不同意隐私政策——退出应用");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private static void Close()
        {
            if (_open == null) return;
            UnityEngine.Object.Destroy(_open);
            _open = null;
        }
    }
}
