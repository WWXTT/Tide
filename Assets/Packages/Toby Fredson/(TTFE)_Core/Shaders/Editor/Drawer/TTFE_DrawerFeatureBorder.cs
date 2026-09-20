using UnityEngine;
using UnityEditor;
using System;

namespace TobyFredson.Drawers
{
	public class TTFE_DrawerFeatureBorder : MaterialPropertyDrawer
	{
		public override void OnGUI(Rect position, MaterialProperty prop, string label, MaterialEditor editor)
		{
			// Adjust position for the rectangle
			position.width -= 24;
			position.height = EditorGUIUtility.singleLineHeight + 12; // Height for bigger text

			// Draw a solid desaturated green rectangle
			Color rectColor = new Color(0.303f, 0.364f, 0.303f, 1f); // 4% lighter green
			EditorGUI.DrawRect(position, rectColor); // Fill the entire rectangle

			// Calculate luminance of rectColor
			float luminance = 0.2126f * rectColor.r + 0.7152f * rectColor.g + 0.0722f * rectColor.b;
			// Choose text color based on border luminance (threshold of 0.5 for light/dark)
			Color textColor = luminance < 0.5f ? new Color(0.92f, 0.92f, 0.92f, 1f) : new Color(0.2f, 0.2f, 0.2f, 1f);

			// Draw the text inside, centered both vertically and horizontally
			Rect textRect = new Rect(position.x + 12, position.y + 3, position.width - 24, EditorGUIUtility.singleLineHeight + 6);
			GUIStyle textStyle = new GUIStyle(EditorStyles.label);
			textStyle.alignment = TextAnchor.MiddleCenter; // Center vertically and horizontally
			textStyle.fontStyle = FontStyle.Bold; // Bold text
			textStyle.fontSize = 12; // Bigger text
			textStyle.normal.textColor = textColor; // Dynamic text color
			EditorGUI.LabelField(textRect, label, textStyle);
		}

		// Set the height for the drawer
		public override float GetPropertyHeight(MaterialProperty prop, string label, MaterialEditor editor)
		{
			return EditorGUIUtility.singleLineHeight + 12; // Height for green rectangle and bigger text
		}
	}
}