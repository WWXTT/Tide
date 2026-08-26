using UnityEngine;
using UnityEditorInternal;

namespace UnityEditor.DanbaidongGUI
{
    /// <summary>
    /// 渐变斜坡编辑器窗口，用于编辑和保存 GradientsRamp 资源
    /// </summary>
    public class GradientsRampEditorWindow : EditorWindow
    {
        /// <summary> 目标渐变斜坡对象 </summary>
        public GradientsRamp m_GradientRampObject;

        private SerializedObject serializedObject;
        private SerializedProperty gradientRampObectProp;
        private SerializedProperty gradientRampTexProp;
        private SerializedProperty gradientsListProp;
        private ReorderableList gradientsList;
        private Texture2D checkerboardTexture;

        /// <summary> 是否通过纹理创建（当窗口失去焦点时关闭纹理） </summary>
        public bool editWithTex = false;

        /// <summary> 打开渐变斜坡编辑器窗口（无预设纹理） </summary>
        [MenuItem("Tools/创建渐变贴图")]
        public static void ShowWindow()
        {
            var window = EditorWindow.GetWindow(typeof(GradientsRampEditorWindow));
            window.position = new Rect(800, 300, 500, 809);
        }

        /// <summary> 打开渐变斜坡编辑器窗口，并指定初始斜坡纹理 </summary>
        /// <param name="rampTex">初始斜坡纹理</param>
        public static void ShowWindow(Texture2D rampTex)
        {
            GradientsRampEditorWindow window = (GradientsRampEditorWindow)EditorWindow.GetWindow(typeof(GradientsRampEditorWindow));
            window.position = new Rect(800, 300, 500, 809);
            window.m_GradientRampObject = new GradientsRamp(rampTex);
            window.editWithTex = true;
        }

        private void OnEnable()
        {
            // 确保渐变斜坡对象存在
            if (m_GradientRampObject == null)
                m_GradientRampObject = new GradientsRamp();

            // 序列化当前窗口自身
            serializedObject = new SerializedObject(this);

            // 获取属性引用
            gradientRampObectProp = serializedObject.FindProperty("m_GradientRampObject");
            gradientRampTexProp = gradientRampObectProp.FindPropertyRelative("rampTexture");
            gradientsListProp = gradientRampObectProp.FindPropertyRelative("gradients");

            // 初始化可重排列表 UI 和棋盘格纹理
            InitReorderableListGUI();
            InitCheckerboardTexture(4, 4);
        }

        /// <summary> 初始化渐变列表的可重排列表控件 </summary>
        private void InitReorderableListGUI()
        {
            // 创建可重排列表，支持拖动、添加、删除
            gradientsList = new ReorderableList(serializedObject, gradientsListProp, true, true, true, true);

            // 定义列表每个元素的绘制回调
            gradientsList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
            {
                var element = gradientsList.serializedProperty.GetArrayElementAtIndex(index);
                rect.y += 2;
                // 绘制渐变字段
                EditorGUI.PropertyField(new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight), element, GUIContent.none);
            };

            // 定义添加新元素时的回调
            gradientsList.onAddCallback = (ReorderableList list) =>
            {
                int newIndex = list.count;
                gradientsListProp.arraySize = newIndex + 1;
                serializedObject.ApplyModifiedProperties();
                // 添加一个示例渐变
                m_GradientRampObject.gradients[newIndex] = GradientsRamp.CreateSampleGradient();
                serializedObject.Update();
            };

            // 设置列表头标题
            gradientsList.drawHeaderCallback = (Rect rect) =>
            {
                EditorGUI.LabelField(rect, "渐变列表");
            };
        }

        /// <summary> 初始化棋盘格纹理，用于预览时显示透明背景 </summary>
        /// <param name="width">棋盘格宽度（像素）</param>
        /// <param name="height">棋盘格高度（像素）</param>
        private void InitCheckerboardTexture(int width, int height)
        {
            if (checkerboardTexture == null)
            {
                checkerboardTexture = new Texture2D(width, height);
                checkerboardTexture.filterMode = FilterMode.Point;

                // 生成两种灰色交替的像素
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        Color color = (x + y) % 2 == 0 ? new Color(0.95f, 0.95f, 0.95f, 1f) : new Color(0.75f, 0.75f, 0.75f, 1f);
                        checkerboardTexture.SetPixel(x, y, color);
                    }
                }
                checkerboardTexture.Apply();
            }
        }

        void OnGUI()
        {
            serializedObject.Update();

            // ---------- 纹理输入 ----------
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(gradientRampTexProp);
            if (EditorGUI.EndChangeCheck())
            {
                // 将序列化属性同步到实际对象
                serializedObject.ApplyModifiedProperties();
                // 根据新纹理加载渐变数据
                m_GradientRampObject.LoadGradientRamp(m_GradientRampObject.rampTexture);
                // 重新刷新序列化数据（渐变列表可能已变化）
                serializedObject.Update();
            }
            EditorGUILayout.EndHorizontal();

            // 如果没有纹理，则禁用后续 UI
            if (m_GradientRampObject.rampTexture == null)
                GUI.enabled = false;

            // ---------- 纹理预览 ----------
            GUILayout.Space(10);
            if (m_GradientRampObject.rampTexture != null)
            {
                var rect = EditorGUILayout.GetControlRect(true, 64);
                rect.xMin += 5;
                rect.xMax -= 5;

                // 临时修改纹理过滤模式为点采样，避免模糊预览
                var filterModeOri = m_GradientRampObject.rampTexture.filterMode;
                m_GradientRampObject.rampTexture.filterMode = FilterMode.Point;

                // 绘制棋盘格背景（仅绘制一部分，模拟重复效果）
                Rect texCoords = new Rect(0, 0, 0.5f * rect.width / (float)checkerboardTexture.width, 0.5f * rect.height / (float)checkerboardTexture.height);
                GUI.DrawTextureWithTexCoords(rect, checkerboardTexture, texCoords);

                // 绘制斜坡纹理本身
                GUI.DrawTexture(rect, m_GradientRampObject.rampTexture);

                // 恢复过滤模式
                m_GradientRampObject.rampTexture.filterMode = filterModeOri;
            }
            GUILayout.Space(10);

            // ---------- 渐变列表 ----------
            gradientsList.DoLayoutList();
            GUILayout.Space(10);

            // ---------- 保存与关闭按钮 ----------
            GUILayout.BeginHorizontal();
            Vector2Int singleRampSize = m_GradientRampObject.singleRampSize;
            var rampSizeStyle = new GUIStyle(EditorStyles.boldLabel);
            // 如果高度超过100，文字变红提示可能过大
            rampSizeStyle.normal.textColor = (singleRampSize.y > 100) ? Color.red : rampSizeStyle.normal.textColor;
            GUILayout.Label("单张斜坡尺寸: ", rampSizeStyle, GUILayout.Width(110));
            EditorGUI.BeginChangeCheck();
            singleRampSize = EditorGUILayout.Vector2IntField("", singleRampSize, GUILayout.Width(100));
            if (EditorGUI.EndChangeCheck())
            {
                m_GradientRampObject.singleRampSize = singleRampSize;
            }
            GUILayout.FlexibleSpace();

            // 保存按钮：将当前渐变数据烘焙到纹理并保存
            if (GUILayout.Button("保存", GUILayout.Width(80)))
            {
                m_GradientRampObject.SaveRampData(true);
            }
            // 关闭窗口按钮
            if (GUILayout.Button("关闭", GUILayout.Width(80)))
            {
                Close();
            }
            GUILayout.EndHorizontal();

            GUI.enabled = true;
            serializedObject.ApplyModifiedProperties();
        }
    }
}