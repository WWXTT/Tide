using UnityEngine;

namespace SynergyUI
{
    /// <summary>
    /// 分阶段迁移的临时占位屏（2026-10-01 UITK→UGUI）：Phase 0 全屏占位化保证编译，
    /// Phase 1-5 逐屏替换为真移植，全部完成后本文件删除。
    /// 旧 UITK 实现整份可从 tag uitk-ui-final 取回（git show uitk-ui-final:&lt;path&gt;）。
    /// </summary>
    internal static class ScreenStub
    {
        public static RectTransform Build(RectTransform parent, UIManager manager, string title, string note)
        {
            var root = UiKit.Screen("stub", parent);
            UiKit.Label("title", root, title, UiStyle.TitleSize, UiStyle.TextPrimary,
                TextAnchor.LowerLeft, FontStyle.Bold);
            UiKit.Label("note", root, note + "——正在从 UI Toolkit 迁移到 UGUI（分阶段进行）",
                UiStyle.BodySize, UiStyle.TextFaint);
            var bar = UiKit.Row("bar", root);
            UiKit.Button("back", bar, "← 返回", () => manager.Back());
            return root;
        }
    }
}
