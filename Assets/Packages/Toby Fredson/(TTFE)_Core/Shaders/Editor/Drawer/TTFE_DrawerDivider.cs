using UnityEngine;
using UnityEditor;
using System;

namespace TobyFredson.Drawers
{
	public class TTFE_DrawerDivider : MaterialPropertyDrawer
	{
		public override void OnGUI(Rect position, MaterialProperty prop, string label, MaterialEditor editor)
		{
			// Adjust position for label and divider
			position.width -= 24;
		

			// Draw a thicker, lighter gray divider line
			Rect dividerRect = new Rect(position.x, position.y + position.height - 20, position.width, 2); // Thicker (2px)
			EditorGUI.DrawRect(dividerRect, new Color(0.5f, 0.5f, 0.5f, 1f)); // Lighter gray color
		}

		// Ensure the drawer has a fixed height
		public override float GetPropertyHeight(MaterialProperty prop, string label, MaterialEditor editor)
		{
			return EditorGUIUtility.singleLineHeight + 3; // Add space for thicker divider
		}
	}
}