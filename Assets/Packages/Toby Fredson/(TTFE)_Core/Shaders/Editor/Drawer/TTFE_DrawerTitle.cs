using UnityEngine;
using UnityEditor;
using System;

namespace TobyFredson.Drawers
{
	public class TTFE_DrawerTitle : MaterialPropertyDrawer
	{
		public override void OnGUI(Rect position, MaterialProperty prop, string label, MaterialEditor editor)
		{
			// Adjust position for the title
			position.width -= 24; // Match previous drawers' alignment
			position.height = EditorGUIUtility.singleLineHeight + 4; // Slightly taller for big text
			position.x += 12; // Offset to center within inspector width

			// Approximate background luminance based on Editor theme
			bool isDarkTheme = EditorGUIUtility.isProSkin; // True for dark theme, false for light
			float backgroundLuminance = isDarkTheme ? 0.22f : 0.85f;
			// Choose text color based on background luminance (opposite intensity)
			Color textColor = backgroundLuminance < 0.5f ? new Color(0.92f, 0.92f, 0.92f, 1f) : new Color(0.1f, 0.1f, 0.1f, 1f);
			// Choose shadow color (black for dark theme, white for light theme)
			Color shadowColor = isDarkTheme ? new Color(0f, 0f, 0f, 0.5f) : new Color(1f, 1f, 1f, 0.5f);

			// Create style for the title
			GUIStyle titleStyle = new GUIStyle(EditorStyles.label);
			titleStyle.fontStyle = FontStyle.Bold; // Bold text
			titleStyle.fontSize = 14; // Big text (14pt)
			titleStyle.alignment = TextAnchor.MiddleCenter; // Center horizontally and vertically
			titleStyle.margin = new RectOffset(0, 0, 5, 10); // Margin for spacing

			// Draw shadow (offset by 1 pixel down and right)
			Rect shadowRect = new Rect(position.x + 1, position.y + 1, position.width, position.height);
			titleStyle.normal.textColor = shadowColor; // Set shadow color
			EditorGUI.LabelField(shadowRect, label.ToUpper(), titleStyle);

			// Draw main text on top
			titleStyle.normal.textColor = textColor; // Set main text color
			EditorGUI.LabelField(position, label.ToUpper(), titleStyle);
		}

		// Set the height for the drawer
		public override float GetPropertyHeight(MaterialProperty prop, string label, MaterialEditor editor)
		{
			return EditorGUIUtility.singleLineHeight + 4; // Height for big text
		}
	}
}