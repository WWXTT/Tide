#if UNITY_EDITOR
using System;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 大厅观战节点烘焙（2026-11-11 v16 多成员房间）：往 LobbyUI.prefab 房间页（panel-room）烘焙
/// 「btn-spectate（座位↔观战切换钮，克隆 btn-add-ai 同款）」与「lbl-spectators（观战计数行，
/// 克隆 lbl-room-hint 同款）」。幂等（已烘焙即跳过）——版式归预制体：摆位为可跑默认值，
/// 用户随后可在编辑器里自由手调，重跑本菜单不覆盖已有节点。
/// 菜单：Tools/烘焙/大厅观战节点。
/// </summary>
public static class BakeLobbySpectateNodes
{
    private const string PrefabPath = "Assets/Art/Prefab/LobbyUI.prefab";

    [MenuItem("Tools/烘焙/大厅观战节点")]
    public static void Bake()
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (root == null)
        {
            Debug.LogError($"[烘焙] 找不到 {PrefabPath}");
            return;
        }
        bool dirty = false;

        var btnAddAi = FindDeep(root.transform, "btn-add-ai");
        var hint = FindDeep(root.transform, "lbl-room-hint");

        if (FindDeep(root.transform, "btn-spectate") == null)
        {
            if (btnAddAi == null) { Debug.LogError("[烘焙] 缺模板 btn-add-ai——先在 prefab 里补齐再跑"); return; }
            var btn = UnityEngine.Object.Instantiate(btnAddAi.gameObject, btnAddAi.parent);
            btn.name = "btn-spectate";
            // 摆位默认值：模板右侧一格（等宽 +12 间距）——仅可跑默认，版式由用户在编辑器手调
            var rt = btn.GetComponent<RectTransform>();
            var src = btnAddAi.GetComponent<RectTransform>();
            if (rt != null && src != null)
            {
                rt.anchorMin = src.anchorMin; rt.anchorMax = src.anchorMax;
                rt.pivot = src.pivot; rt.anchoredPosition = src.anchoredPosition;
                rt.sizeDelta = src.sizeDelta;
                rt.anchoredPosition = new Vector2(src.anchoredPosition.x + src.sizeDelta.x + 12f, src.anchoredPosition.y);
            }
            var lbl = btn.GetComponentInChildren<TMP_Text>(true);
            if (lbl != null) lbl.text = "转为观战";
            dirty = true;
            Debug.Log("[烘焙] 已添加 btn-spectate（克隆 btn-add-ai）");
        }
        else Debug.Log("[烘焙] btn-spectate 已存在，跳过");

        if (FindDeep(root.transform, "lbl-spectators") == null)
        {
            if (hint == null) { Debug.LogError("[烘焙] 缺模板 lbl-room-hint——先在 prefab 里补齐再跑"); return; }
            var node = UnityEngine.Object.Instantiate(hint.gameObject, hint.parent);
            node.name = "lbl-spectators";
            var rt = node.GetComponent<RectTransform>();
            var src = hint.GetComponent<RectTransform>();
            if (rt != null && src != null)
            {
                rt.anchorMin = src.anchorMin; rt.anchorMax = src.anchorMax;
                rt.pivot = src.pivot; rt.anchoredPosition = src.anchoredPosition;
                rt.sizeDelta = src.sizeDelta;
                rt.anchoredPosition = new Vector2(src.anchoredPosition.x, src.anchoredPosition.y + 36f);
            }
            var txt = node.GetComponent<TMP_Text>();
            if (txt != null) txt.text = "";
            dirty = true;
            Debug.Log("[烘焙] 已添加 lbl-spectators（克隆 lbl-room-hint）");
        }
        else Debug.Log("[烘焙] lbl-spectators 已存在，跳过");

        if (dirty)
        {
            PrefabUtility.SavePrefabAsset(root);
            Debug.Log("[烘焙] LobbyUI.prefab 已保存——摆位是可跑默认值，请在编辑器里手调至满意");
        }
        else Debug.Log("[烘焙] 无变更（全部节点已在）");
    }

    private static Transform FindDeep(Transform t, string name)
    {
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
