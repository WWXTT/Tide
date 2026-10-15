using UnityEditor;

// 临时取证脚本（2026-10-15 弹窗/自动选目标复现用——事后删除）：
// 每次域加载打开引擎面包屑开关——Logs/EngineCrumb.txt 逐条落盘：
// target-select = 目标反问是否发生/ chooser 是谁/ UI 是否接管；atom-targets = 每原子结算期
// 候选数/选择模式/数量档（判「该问没问」走的是哪个分支）。
[InitializeOnLoad]
public static class TempCrumbEnable
{
    static TempCrumbEnable()
    {
        CardCore.GameActions.CrumbEnabled = true;
        UnityEngine.Debug.Log("[TempCrumbEnable] 引擎面包屑已开启（Logs/EngineCrumb.txt）");
    }
}
