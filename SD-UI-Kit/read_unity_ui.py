# -*- coding: utf-8 -*-
"""从 Unity prefab YAML 提取 UI 组件清单（名称/类型/布局尺寸）。

只读操作：不修改任何 Unity 资产。输出树形文本 + JSON（stdout），
重定向到文件即可保存：python read_unity_ui.py <prefab路径> > manifest_source.json

已知组件 GUID（UnityEngine / TMP 官方固定值）：
  Image          fe87c0e1cc204ed48ad3b3786fc2126
  Button         4e29b1a8efbd4b44bb3f3716e73f07ff
  TextMeshProUGUI f4688fdb7df04437aeb418b961361dc5
  RawImage       1344c3c82d62a2a41a87a7fe33d95382
"""
import io
import json
import re
import sys

GUID_BUTTON = "4e29b1a8efbd4b44bb3f3716e73f07ff"
GUID_TMP = "f4688fdb7df04437aeb418b961361dc5"
GUID_RAW = "1344c3c82d62a2a41a87a7fe33d95382"
# Image 的 guid 存在官方/项目两个尾段变体（...6fc2126 与 ...40f39efc），按前缀识别
GUID_IMAGE_PREFIX = "fe87c0e1cc204ed48ad3b3"

DOC_RE = re.compile(r"^--- !u!(\d+) &(\d+)\s*$")
FLT = r"(-?[\d.eE+-]+)"


def _f(s, key):
    m = re.search(key + r":\s*\{?\s*x:\s*" + FLT + r",\s*y:\s*" + FLT, s)
    return (float(m.group(1)), float(m.group(2))) if m else None


def parse(path):
    docs = {}
    with io.open(path, "r", encoding="utf-8") as fh:
        cur_id, cur_cls, buf = None, None, []
        for line in fh:
            m = DOC_RE.match(line.strip("\r\n"))
            if m:
                if cur_id is not None:
                    docs[cur_id] = (int(cur_cls), "\n".join(buf))
                cur_cls, cur_id, buf = int(m.group(1)), int(m.group(2)), []
            else:
                buf.append(line)
        if cur_id is not None:
            docs[cur_id] = (int(cur_cls), "\n".join(buf))

    gos, rts, monos = {}, {}, {}
    for fid, (cls, body) in docs.items():
        if cls == 1:  # GameObject
            name = re.search(r"m_Name:\s*(.+)", body)
            comps = [int(x) for x in re.findall(r"component:\s*\{fileID:\s*(\d+)\}", body)]
            gos[fid] = {"name": name.group(1).strip() if name else "?", "comps": comps}
        elif cls == 224:  # RectTransform
            go = re.search(r"m_GameObject:\s*\{fileID:\s*(\d+)\}", body)
            father = re.search(r"m_Father:\s*\{fileID:\s*(\d+)\}", body)
            sd = _f(body, "m_SizeDelta") or (0.0, 0.0)
            amin = _f(body, "m_AnchorMin") or (0.0, 0.0)
            amax = _f(body, "m_AnchorMax") or (0.0, 0.0)
            scale = _f(body, "m_LocalScale") or (1.0, 1.0)
            rts[fid] = {
                "go": int(go.group(1)) if go else 0,
                "father": int(father.group(1)) if father else 0,
                "size": sd, "amin": amin, "amax": amax,
                "scale": scale,
            }
        elif cls == 114:  # MonoBehaviour
            go = re.search(r"m_GameObject:\s*\{fileID:\s*(\d+)\}", body)
            g = re.search(r"m_Script:\s*\{fileID:\s*\d+,\s*guid:\s*([0-9a-f]+)", body)
            monos[fid] = {"go": int(go.group(1)) if go else 0, "guid": g.group(1) if g else ""}

    # GameObject -> kinds
    go_kinds = {}
    for fid, mo in monos.items():
        if mo["guid"].startswith(GUID_IMAGE_PREFIX):
            kind = "Image"
        else:
            kind = {GUID_BUTTON: "Button", GUID_TMP: "TMP", GUID_RAW: "Raw"}.get(mo["guid"])
        if kind:
            go_kinds.setdefault(mo["go"], []).append(kind)

    # rt -> go 反查
    go_of_rt, rt_of_go = {}, {}
    for fid, rt in rts.items():
        go_of_rt[fid] = rt["go"]
        rt_of_go[rt["go"]] = fid

    def size_of(fid, parent_size):
        rt = rts[fid]
        w_fix = abs(rt["amin"][0] - rt["amax"][0]) < 1e-4
        h_fix = abs(rt["amin"][1] - rt["amax"][1]) < 1e-4
        sx, sy = abs(rt["scale"][0]) or 1.0, abs(rt["scale"][1]) or 1.0
        w = rt["size"][0] * sx if w_fix else (parent_size[0] + rt["size"][0]) * sx
        h = rt["size"][1] * sy if h_fix else (parent_size[1] + rt["size"][1]) * sy
        return w, h

    # 以 RectTransform 根（father 为 0 或父不在 rts 中）为树根，向下累计尺寸
    roots = [fid for fid, rt in rts.items() if rt["father"] not in rts]
    out_items = []

    def walk(fid, parent_size, prefix, depth):
        if fid not in rts:
            return
        w, h = size_of(fid, parent_size)
        go = rts[fid]["go"]
        name = gos.get(go, {}).get("name", "?")
        kinds = go_kinds.get(go, [])
        kind = "Button" if "Button" in kinds else ("TMP" if "TMP" in kinds else ("Image" if "Image" in kinds else ("Raw" if "Raw" in kinds else "Node")))
        if w >= 16 and h >= 16 and kind in ("Button", "Image", "Raw"):
            out_items.append({
                "path": prefix + name, "kind": kind,
                "w": round(w), "h": round(h), "depth": depth,
            })
        children = [c for c, r in rts.items() if r["father"] == fid]
        for c in children:
            walk(c, (w, h), prefix + name + "/", depth + 1)

    for r in roots:
        walk(r, (1920.0, 1080.0), "", 0)

    return {
        "prefab": path,
        "count": len(out_items),
        "items": out_items,
    }


if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("usage: python read_unity_ui.py <prefab> [> out.json]", file=sys.stderr)
        sys.exit(1)
    result = parse(sys.argv[1])
    print(json.dumps(result, ensure_ascii=False, indent=1))
