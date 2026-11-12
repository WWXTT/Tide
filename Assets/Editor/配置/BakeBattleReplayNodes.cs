#if UNITY_EDITOR
using System;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 战斗回放控件烘焙（2026-11-12 阶段2·docs/战斗回放·阶段2派工单.md 任务 D）：往 BattleUI.prefab
/// 烘焙「btn-replay-pause（回放暂停/播放/重播钮，克隆 btn-abstract 同款：Image+Button+TMP 子节点）
/// 挂 log-list 直属」与「tpl-turn-jump（回合跳转钮隐藏模板，同克隆）挂 log-list/viewport/content」。
/// 幂等（已烘焙即跳过）——版式归预制体：摆位为可跑默认值，用户随后可在编辑器里自由手调，
/// 重跑本菜单不覆盖已有节点。运行时消费见 BattleScreen（FindOptional 绑定）与
/// BattleLogController.LayTurnJump（UiKit.CloneTemplateFrom 只认隐藏模板）。
/// 菜单：Tools/烘焙/战斗回放控件。
/// </summary>
public static class BakeBattleReplayNodes
{
    private const string PrefabPath = "Assets/Art/Prefab/BattleUI.prefab";

    [MenuItem("Tools/烘焙/战斗回放控件")]
    public static void Bake()
    {
        // LoadPrefabContents 隔离实例正道（Instantiate+SavePrefabAsset 会漏克隆进场景——假成功）
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (root == null)
        {
            Debug.LogError($"[烘焙] 找不到 {PrefabPath}");
            return;
        }
        try
        {
            BakeIntoContents(root);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void BakeIntoContents(GameObject root)
    {
        bool dirty = false;

        var btnAbstract = FindDeep(root.transform, "btn-abstract");
        if (btnAbstract == null)
        {
            Debug.LogError("[烘焙] 缺模板 btn-abstract——先在 BattleUI.prefab 里补齐再跑");
            return;
        }
        var logList = FindDeep(root.transform, "log-list");
        var content = logList != null ? logList.Find("viewport/content") : null; // Transform.Find 吃路径
        if (logList == null || content == null)
        {
            Debug.LogError("[烘焙] 缺 log-list/viewport/content 层级——先在 BattleUI.prefab 里补齐再跑");
            return;
        }

        // ① btn-replay-pause：log-list 直属（与 self/opp-deck-row 同级——Mask 外不随滚动）
        if (FindDeep(root.transform, "btn-replay-pause") == null)
        {
            var btn = UnityEngine.Object.Instantiate(btnAbstract.gameObject, logList);
            btn.name = "btn-replay-pause";
            Place(btn.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(140f, 48f), new Vector2(10f, -10f));
            var lbl = btn.GetComponentInChildren<TMP_Text>(true);
            if (lbl != null) lbl.text = "暂停";
            btn.SetActive(false); // 非回放局隐藏（BattleScreen.Build 兜底再关一次；StartReplayFan 打开）
            dirty = true;
            Debug.Log("[烘焙] 已添加 btn-replay-pause（克隆 btn-abstract → log-list 直属）");
        }
        else Debug.Log("[烘焙] btn-replay-pause 已存在，跳过");

        // ② tpl-turn-jump：content 直属隐藏模板（CloneTemplateFrom 只认 inactive——必须 SetActive(false)）
        if (FindDeep(root.transform, "tpl-turn-jump") == null)
        {
            var tpl = UnityEngine.Object.Instantiate(btnAbstract.gameObject, content);
            tpl.name = "tpl-turn-jump";
            Place(tpl.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(320f, 36f), Vector2.zero);
            var lbl = tpl.GetComponentInChildren<TMP_Text>(true);
            if (lbl != null) lbl.text = "第1回合";
            tpl.SetActive(false); // 硬要求：隐藏模板（克隆体由 BattleLogController.LayTurnJump 实例化）
            dirty = true;
            Debug.Log("[烘焙] 已添加 tpl-turn-jump（克隆 btn-abstract → content 直属隐藏模板）");
        }
        else Debug.Log("[烘焙] tpl-turn-jump 已存在，跳过");

        if (dirty)
        {
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("[烘焙] BattleUI.prefab 已保存——摆位是可跑默认值，请在编辑器里手调至满意");
        }
        else Debug.Log("[烘焙] 无变更（全部节点已在）");
    }

    /// <summary>可跑默认摆位（左上锚点）：锚点 (0,1)、pivot (0,1)、定尺寸、偏移——版式归用户手调。</summary>
    private static void Place(RectTransform rt, Vector2 anchor, Vector2 size, Vector2 pos)
    {
        if (rt == null) return;
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
    }

    private static Transform FindDeep(Transform t, string name)
    {
        if (t == null) return null;
        if (t.name == name) return t;
        for (int i = 0; i < t.childCount; i++)
        {
            var hit = FindDeep(t.GetChild(i), name);
            if (hit != null) return hit;
        }
        return null;
    }
}
#endif
