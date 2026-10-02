// 热更管线（HybridCLR+YooAsset）解除期：由 TIDE_HYBRID_YOO 宏整体启停，恢复步骤见项目根《热更插件解除与恢复.md》
#if TIDE_HYBRID_YOO
using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Tide.Launch
{
    /// <summary>
    /// 启动阶段极简控制台（OnGUI 绘制，零资产依赖，主包专用）。
    /// 热更链路跑通后如需美化，可替换为 UITK 启动界面——但启动屏资产需放主包，勿入 YooAsset。
    /// </summary>
    public static class PatchConsole
    {
        private static string _stage = "初始化…";
        private static string _detail = "";
        private static string _error = "";
        private static float _progress01 = -1f; // -1 = 不显示进度条
        private static bool _showRetry;
        private static bool _retryClicked;

        public static void Info(string stage)
        {
            _stage = stage;
            _detail = "";
            _error = "";
            _progress01 = -1f;
            Debug.Log($"[Launch] {stage}");
        }

        public static void Progress(int current, int total, long bytesCurrent, long bytesTotal)
        {
            _progress01 = total > 0 ? (float)current / total : 0f;
            _detail = $"{current}/{total}（{bytesCurrent / 1024 / 1024}MB / {bytesTotal / 1024 / 1024}MB）";
        }

        /// <summary>不确定进度的阶段显示（0~1，<0 忽略）</summary>
        public static void Spin(float progress01)
        {
            if (progress01 >= 0f)
                _progress01 = progress01;
        }

        public static void Warn(string message)
        {
            Debug.LogWarning($"[Launch] {message}");
        }

        public static void Error(string message)
        {
            _error = message;
            Debug.LogError($"[Launch] {message}");
        }

        /// <summary>失败后等待玩家点重试，随后重跑整个启动流程</summary>
        public static async UniTask WaitRetryAsync()
        {
            Error(_error.Length > 0 ? _error : "启动失败");
            _retryClicked = false;
            _showRetry = true;
            await UniTask.WaitUntil(() => _retryClicked);
            _showRetry = false;
            _error = "";
        }

        public static void Draw()
        {
            const float width = 560f;
            const float height = 180f;
            var area = new Rect((Screen.width - width) / 2f, Screen.height / 2f - height, width, height);
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label("Tide", new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter });
            GUILayout.Space(10f);

            string message = _error.Length > 0 ? _error : _stage;
            var style = _error.Length > 0 ? new GUIStyle(GUI.skin.label) { normal = { textColor = Color.red } } : GUI.skin.label;
            GUILayout.Label(message, style);

            if (_progress01 >= 0f)
            {
                var bar = GUILayoutUtility.GetRect(0f, 20f, GUILayout.ExpandWidth(true));
                GUI.Box(bar, GUIContent.none);
                var fill = new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(_progress01), bar.height);
                if (fill.width > 1f)
                {
                    var prev = GUI.color;
                    GUI.color = new Color(0.3f, 0.7f, 1f);
                    GUI.DrawTexture(fill, Texture2D.whiteTexture);
                    GUI.color = prev;
                }
                if (_detail.Length > 0)
                    GUILayout.Label(_detail);
            }

            if (_showRetry && GUILayout.Button("重试", GUILayout.Height(32f)))
                _retryClicked = true;

            GUILayout.EndArea();
        }
    }
}
#endif
