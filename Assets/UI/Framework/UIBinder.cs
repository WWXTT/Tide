using System;
using UnityEngine.UIElements;

namespace SynergyUI
{
    /// <summary>
    /// 控件接线助手 —— 沉淀重复的「查控件 + 绑回调」模式，减少各界面样板代码。
    /// 全部为静态方法，按需调用。
    /// </summary>
    public static class UIBinder
    {
        /// <summary>给具名按钮绑定点击回调，返回该按钮。找不到则返回 null。</summary>
        public static Button BindButton(VisualElement root, string name, Action onClick)
        {
            var btn = root.Q<Button>(name);
            if (btn != null && onClick != null)
            {
                btn.clicked += onClick;
            }
            return btn;
        }

        /// <summary>
        /// 双向绑定一个值控件（TextField / Toggle / Slider 等 INotifyValueChanged）：
        /// 写入初始值，并在用户改值时回调。
        /// </summary>
        public static void BindField<TValue>(
            INotifyValueChanged<TValue> field,
            TValue initialValue,
            Action<TValue> onChanged)
        {
            if (field == null)
            {
                return;
            }
            field.SetValueWithoutNotify(initialValue);
            if (onChanged != null)
            {
                field.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
            }
        }

        /// <summary>设置具名 Label 文本（找不到则忽略）。</summary>
        public static void SetText(VisualElement root, string name, string text)
        {
            var label = root.Q<Label>(name);
            if (label != null)
            {
                label.text = text;
            }
        }
    }
}
