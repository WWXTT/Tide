#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
效果 ID 生成器 —— 为 Attribute.xlsm 的原子效果表（AttributeValueConfig sheet）
生成/刷新 8 位十六进制内容哈希 ID 列。

约定：
    ID = sha256(效果描述文本).hexdigest()[:8]
    “效果描述”取 DisplayName 列（展示模板，运行时即 AtomicEffectConfig.Description），
    该 ID 是代码与多语言本地化的统一映射键——引用一律走 ID，不再自造英文代称。
    文本即身份：描述改动 → ID 随之改变（脚本会标出 <-旧值），引用方需同步。

列操作：ID 插在第 1 列（A），第 1 行列名 "ID"、第 2 行类型 "string"。幂等可重跑；
描述为空或哈希冲突时报错退出、不落盘。
注意：插列不会移动既有数据验证（openpyxl 行为），跑完本脚本须再执行
refresh_dropdowns.py 重套下拉，然后 export_to_json.py 导出。

用法（工作簿须处于关闭状态）:
    python gen_effect_ids.py
"""
import hashlib
import sys
from pathlib import Path

import openpyxl

SCRIPT_DIR = Path(__file__).parent.resolve()
WORKBOOK = SCRIPT_DIR / "Attribute.xlsm"
SHEET_NAME = "AttributeValueConfig"
DESC_COLUMN = "DisplayName"   # hash 源：效果描述（展示模板）
ID_COLUMN = "ID"              # 目标列（插入第 1 列）
ROW_VAR, ROW_TYPE, ROW_DATA_START = 1, 2, 3


def effect_id(desc: str) -> str:
    return hashlib.sha256(desc.strip().encode("utf-8")).hexdigest()[:8]


def main() -> None:
    if not WORKBOOK.exists():
        sys.exit(f"工作簿不存在: {WORKBOOK}")
    if (SCRIPT_DIR / ".~lock.Attribute.xlsm#").exists():
        sys.exit("工作簿被 LibreOffice 锁定，请先关闭再重跑")

    wb = openpyxl.load_workbook(WORKBOOK)
    ws = wb[SHEET_NAME]
    headers = {str(c.value).strip(): c.column for c in ws[ROW_VAR] if c.value}

    # 幂等：已有 ID 列则复用，否则插入第 1 列
    inserted = False
    if ID_COLUMN in headers:
        id_col = headers[ID_COLUMN]
    else:
        ws.insert_cols(1)
        ws.cell(row=ROW_VAR, column=1, value=ID_COLUMN)
        ws.cell(row=ROW_TYPE, column=1, value="string")
        id_col = 1
        inserted = True
        headers = {str(c.value).strip(): c.column for c in ws[ROW_VAR] if c.value}

    desc_col = headers[DESC_COLUMN]

    seen = {}  # id -> desc，冲突检测
    new = changed = kept = 0
    for row in range(ROW_DATA_START, ws.max_row + 1):
        desc = ws.cell(row=row, column=desc_col).value
        if desc is None or not str(desc).strip():
            continue
        desc = str(desc).strip()
        eid = effect_id(desc)

        if eid in seen:
            sys.exit(f"哈希冲突：第{row}行「{desc}」与「{seen[eid]}」同 ID {eid}，未保存")
        seen[eid] = desc

        old = ws.cell(row=row, column=id_col).value
        old = str(old).strip() if old is not None else None
        if old == eid:
            kept += 1
        elif old is None:
            new += 1
        else:
            changed += 1
        ws.cell(row=row, column=id_col, value=eid)
        suffix = "" if old == eid else f"  <- {old or '新建'}"
        print(f"  第{row}行 {eid}  {desc}{suffix}")

    wb.save(WORKBOOK)
    wb.close()
    action = "已插入 ID 列（第 1 列），" if inserted else ""
    print(f"\n完成：{action}共 {len(seen)} 行（新建 {new} / 变更 {changed} / 不变 {kept}）")
    if inserted:
        print("后续步骤：python refresh_dropdowns.py && python export_to_json.py")


if __name__ == "__main__":
    main()
