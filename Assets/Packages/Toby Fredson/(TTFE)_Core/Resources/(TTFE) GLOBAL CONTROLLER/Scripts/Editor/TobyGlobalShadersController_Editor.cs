using UnityEngine;
using UnityEditor;

namespace TobyFredson
{
	[CustomEditor(typeof(TobyGlobalShadersController))]
	public class TobyGlobalShadersController_Editor : Editor
	{
		private Texture2D HFGUI_GrassifyHeader;
		private SerializedProperty windType;
		private SerializedProperty windStrength;
		private SerializedProperty windSpeed;
		private SerializedProperty windMotion;
		private SerializedProperty season;
		private SerializedProperty snow;
		private SerializedProperty wetness; // Added wetness property
		private SerializedProperty _DEBUGVisualizeWindMask;
		private SerializedProperty _showWindDirectionGizmo;
		private SerializedProperty _savePerformanceDeactivateWind;

		void OnEnable()
		{
			HFGUI_GrassifyHeader = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Toby Fredson/The Toby Foliage Engine/(TTFE)_Core/Resources/(TTFE) GLOBAL CONTROLLER/Scripts/Editor/TTFE_Graphic Editor.png");

			windType = serializedObject.FindProperty("windType");
			windStrength = serializedObject.FindProperty("windStrength");
			windSpeed = serializedObject.FindProperty("windSpeed");
			windMotion = serializedObject.FindProperty("windMotion");
			season = serializedObject.FindProperty("season");
			snow = serializedObject.FindProperty("snow");
			wetness = serializedObject.FindProperty("wetness"); // Initialize wetness property
			_DEBUGVisualizeWindMask = serializedObject.FindProperty("_DEBUGVisualizeWindMask");
			_showWindDirectionGizmo = serializedObject.FindProperty("_showWindDirectionGizmo");
			_savePerformanceDeactivateWind = serializedObject.FindProperty("_savePerformanceDeactivateWind");
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			GUILayout.Label(HFGUI_GrassifyHeader, GUILayout.Width(192), GUILayout.Height(96));
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();

			// Container with reduced side padding (50% of 15 = 7.5, rounded to 7)
			EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandWidth(true));
			GUILayout.Space(10); // Top padding
			EditorGUILayout.LabelField("The Toby Foliage Engine version 2.0.0", EditorStyles.centeredGreyMiniLabel);

			// Reduced side margins for content
			GUILayout.BeginHorizontal();
			GUILayout.Space(7); // Left padding
			EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));

			Color rectColor = new Color(0.303f, 0.364f, 0.303f, 1f);
			float luminance = 0.2126f * rectColor.r + 0.7152f * rectColor.g + 0.0722f * rectColor.b;
			Color textColor = luminance < 0.5f ? new Color(0.92f, 0.92f, 0.92f, 1f) : new Color(0.1f, 0.1f, 0.1f, 1f);

			GUIStyle textStyle = new GUIStyle(EditorStyles.label)
			{
				alignment = TextAnchor.MiddleCenter,
				fontStyle = FontStyle.Bold,
				fontSize = 12,
				normal = { textColor = textColor }
			};

			// Global Wind Parameters Section
			Rect windRect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight + 6);
			windRect.width -= 14; // Reduced margins
			windRect.x += 7; // Shift right to align with content padding
			windRect.height = EditorGUIUtility.singleLineHeight + 6;
			EditorGUI.DrawRect(windRect, rectColor);
			Rect windTextRect = new Rect(windRect.x + 12, windRect.y + 1, windRect.width - 24, EditorGUIUtility.singleLineHeight + 2);
			EditorGUI.LabelField(windTextRect, "Global Wind Parameters", textStyle);

			EditorGUILayout.Space();
			EditorGUILayout.PropertyField(windType);
			EditorGUILayout.PropertyField(windStrength);
			EditorGUILayout.PropertyField(windSpeed);
			EditorGUILayout.PropertyField(windMotion);
			EditorGUILayout.Space();
			EditorGUILayout.HelpBox("Choose between wind types: 'Gentle Breeze', 'Strong Wind', or 'Wind Off'. Only one wind type can be active at a time.", MessageType.Info);
			EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
			EditorGUILayout.Space();

			// Global Season Settings Section
			Rect seasonRect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight + 6);
			seasonRect.width -= 14; // Reduced margins
			seasonRect.x += 7; // Shift right to align with content padding
			seasonRect.height = EditorGUIUtility.singleLineHeight + 6;
			EditorGUI.DrawRect(seasonRect, rectColor);
			Rect seasonTextRect = new Rect(seasonRect.x + 12, seasonRect.y + 1, seasonRect.width - 24, EditorGUIUtility.singleLineHeight + 2);
			EditorGUI.LabelField(seasonTextRect, "Global Season Settings", textStyle);

			EditorGUILayout.Space();
			EditorGUILayout.PropertyField(season);
			EditorGUILayout.PropertyField(snow);
			EditorGUILayout.PropertyField(wetness, new GUIContent("Wetness")); // Added wetness slider under snow
			EditorGUILayout.Space();
			EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
			EditorGUILayout.Space();

			// Debug TTFE Section
			Rect debugRect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight + 6);
			debugRect.width -= 14; // Reduced margins
			debugRect.x += 7; // Shift right to align with content padding
			debugRect.height = EditorGUIUtility.singleLineHeight + 6;
			EditorGUI.DrawRect(debugRect, rectColor);
			Rect debugTextRect = new Rect(debugRect.x + 12, debugRect.y + 1, debugRect.width - 24, EditorGUIUtility.singleLineHeight + 2);
			EditorGUI.LabelField(debugTextRect, "Debug TTFE", textStyle);

			EditorGUILayout.Space();
			// Visualize Wind Mask toggle (right-aligned)
			GUILayout.BeginHorizontal();
			GUILayout.Space(5); // Reduced indent for toggle
			EditorGUILayout.LabelField("Visualize Wind Mask", GUILayout.ExpandWidth(true));
			GUILayout.FlexibleSpace();
			EditorGUILayout.PropertyField(_DEBUGVisualizeWindMask, GUIContent.none, GUILayout.Width(20));
			GUILayout.Space(5); // Reduced right padding for toggle
			GUILayout.EndHorizontal();

			// Show Wind Direction Gizmo toggle (right-aligned)
			GUILayout.BeginHorizontal();
			GUILayout.Space(5); // Reduced indent for toggle
			EditorGUILayout.LabelField("Show Wind Direction Gizmo", GUILayout.ExpandWidth(true));
			GUILayout.FlexibleSpace();
			EditorGUILayout.PropertyField(_showWindDirectionGizmo, GUIContent.none, GUILayout.Width(20));
			GUILayout.Space(5); // Reduced right padding for toggle
			GUILayout.EndHorizontal();

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
			EditorGUILayout.Space();

			// Advanced Settings Section
			Rect advancedRect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight + 6);
			advancedRect.width -= 14; // Reduced margins
			advancedRect.x += 7; // Shift right to align with content padding
			advancedRect.height = EditorGUIUtility.singleLineHeight + 6;
			EditorGUI.DrawRect(advancedRect, rectColor);
			Rect advancedTextRect = new Rect(advancedRect.x + 12, advancedRect.y + 1, advancedRect.width - 24, EditorGUIUtility.singleLineHeight + 2);
			EditorGUI.LabelField(advancedTextRect, "Advanced Settings", textStyle);

			EditorGUILayout.Space();
			// Save Performance Deactivate Wind toggle (right-aligned)
			GUILayout.BeginHorizontal();
			GUILayout.Space(5); // Reduced indent for toggle
			EditorGUILayout.LabelField("[SAVE PERFORMANCE] Deactivate Wind", GUILayout.ExpandWidth(true));
			GUILayout.FlexibleSpace();
			EditorGUILayout.PropertyField(_savePerformanceDeactivateWind, GUIContent.none, GUILayout.Width(20));
			GUILayout.Space(5); // Reduced right padding for toggle
			GUILayout.EndHorizontal();

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
			EditorGUILayout.Space();

			EditorGUILayout.EndVertical();
			GUILayout.Space(7); // Right padding
			GUILayout.EndHorizontal();
			GUILayout.Space(10); // Bottom padding
			EditorGUILayout.EndVertical();

			serializedObject.ApplyModifiedProperties();
		}
	}
}