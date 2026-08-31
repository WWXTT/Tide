#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
枚举下拉刷新脚本 —— 原 VBA EnumHelper 宏的跨平台替代（Excel / LibreOffice 通用）。

从 __enums__.xlsx 读取枚举定义（B 列 full_name 分组，D 列枚举值），
为 Attribute.xlsm 中所有第 2 行类型为 'enum' 的列套数据验证下拉。
匹配规则沿用宏的约定：第 1 行列名 == 枚举组 full_name。

表结构约定（无标记列版式）：第 1 行列名 / 第 2 行类型 / 第 3 行起数据。
非 enum 的类型列会清掉残留验证（与宏行为一致）。

用法（改完 __enums__.xlsx 或表结构后跑一次，工作簿须处于关闭状态）:
    python refresh_dropdowns.py

配合导出：
    python export_to_json.py
"""
import sys
from pathlib import Path

import openpyxl
from openpyxl.utils import get_column_letter
from openpyxl.worksheet.datavalidation import DataValidation

SCRIPT_DIR = Path(__file__).parent.resolve()
WORKBOOK = SCRIPT_DIR / "Attribute.xlsm"
ENUM_FILE = SCRIPT_DIR / "__enums__.xlsx"

ROW_VAR, ROW_TYPE, ROW_DATA_START = 1, 2, 3
KNOWN_TYPES = {"string", "int", "float", "enum"}
EXTRA_ROWS = 20  # 数据区下方多套若干行，给将来加行留余量


def load_enums(path: Path) -> dict:
    """__enums__.xlsx → {枚举组名: [值, ...]}
    列位（无标记列版式）：A=full_name 组名，B=comment 注释，C=enum 枚举值，D=des 描述；
    第 1 行表头、第 2 行中文标签，数据从第 3 行起（组名行自带首个值）。
    """
    wb = openpyxl.load_workbook(path, data_only=True)
    ws = wb[wb.sheetnames[0]]
    enums: dict = {}
    current = None
    for row in range(3, ws.max_row + 1):
        full_name = ws.cell(row=row, column=1).value
        value = ws.cell(row=row, column=3).value
        if full_name:
            current = str(full_name).strip()
            enums.setdefault(current, [])
            if value:
                enums[current].append(str(value).strip())
        elif current and value:
            enums[current].append(str(value).strip())
    wb.close()
    return enums


def column_last_used_row(ws, col: int) -> int:
    last = ROW_DATA_START - 1
    for row in range(ROW_DATA_START, ws.max_row + 1):
        if ws.cell(row=row, column=col).value is not None:
            last = row
    return last


def drop_column_validations(ws, col_letter: str) -> None:
    """幂等：移除该列已有的数据验证"""
    ws.data_validations.dataValidation = [
        dv for dv in ws.data_validations.dataValidation
        if not any(rng.coord.split(":")[0].startswith(col_letter) for rng in dv.sqref.ranges)
    ]


def refresh(workbook_path: Path, enums: dict) -> None:
    # 不带 keep_vba：若工作簿仍残留已失效的 EnumHelper 宏，顺带清除
    wb = openpyxl.load_workbook(workbook_path)
    applied, missed = 0, []

    for ws in wb.worksheets:
        # 第 2 行必须含有已知类型标记，才认定为配置表
        type_tokens = {
            str(ws.cell(row=ROW_TYPE, column=c).value or "").strip().lower()
            for c in range(1, ws.max_column + 1)
        }
        if not (type_tokens & KNOWN_TYPES):
            continue

        last_data_row = max(
            (column_last_used_row(ws, c) for c in range(1, ws.max_column + 1)),
            default=ROW_DATA_START - 1,
        )
        end_row = max(last_data_row + EXTRA_ROWS, ROW_DATA_START + EXTRA_ROWS)

        for col in range(1, ws.max_column + 1):
            type_value = str(ws.cell(row=ROW_TYPE, column=col).value or "").strip().lower()
            if not type_value or type_value not in KNOWN_TYPES:
                continue
            col_letter = get_column_letter(col)

            if type_value != "enum":
                # 非 enum 类型列：清掉残留验证（与原宏行为一致）
                before = len(ws.data_validations.dataValidation)
                drop_column_validations(ws, col_letter)
                if len(ws.data_validations.dataValidation) < before:
                    print(f"  {ws.title}!{col_letter} 列：清除残留验证")
                continue

            enum_name = str(ws.cell(row=ROW_VAR, column=col).value or "").strip()
            if not enum_name or enum_name not in enums:
                missed.append(f"{ws.title}!{col_letter} 列（enum={enum_name or '<空>'}）")
                continue

            values = enums[enum_name]
            drop_column_validations(ws, col_letter)
            dv = DataValidation(type="list", formula1=f'"{",".join(values)}"', allow_blank=True)
            dv.error = f"请从下拉列表选择 {enum_name} 的合法值"
            dv.errorTitle = "非法枚举值"
            ws.add_data_validation(dv)
            dv.add(f"{col_letter}{ROW_DATA_START}:{col_letter}{end_row}")
            applied += 1
            print(f"  {ws.title}!{col_letter} 列 ← {enum_name}({len(values)} 值)")

    wb.save(workbook_path)
    wb.close()
    print(f"\n完成：套用 {applied} 个下拉" + (f"；未匹配 {len(missed)} 列: {'; '.join(missed)}" if missed else ""))


def main():
    if not WORKBOOK.exists():
        sys.exit(f"工作簿不存在: {WORKBOOK}")
    if not ENUM_FILE.exists():
        sys.exit(f"枚举定义不存在: {ENUM_FILE}")

    enums = load_enums(ENUM_FILE)
    print(f"载入 {len(enums)} 个枚举组: {', '.join(f'{k}({len(v)})' for k, v in enums.items())}\n")
    refresh(WORKBOOK, enums)


if __name__ == "__main__":
    main()
