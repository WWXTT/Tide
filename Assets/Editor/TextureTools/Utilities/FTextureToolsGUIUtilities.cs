using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FIMSpace.FTextureTools
{
    public static class FTextureToolsGUIUtilities
    {

        public static void DrawUILineCommon(int padding = 6, int thickness = 1, float width = 0.975f)
        {
            DrawUILine(0.35f, 0.35f, thickness, padding, width);
        }

        public static void DrawUILine(Color color, int thickness = 2, int padding = 10, float width = 1f)
        {
            Rect rect = EditorGUILayout.GetControlRect(GUILayout.Height(padding + thickness));
            float w = rect.width; float off = rect.width - rect.width * width;
            rect.height = thickness; rect.y += padding / 2; rect.x -= 2; rect.x += off / 2f; rect.width += 2; rect.width *= width;
            EditorGUI.DrawRect(rect, color);
        }

        public static void DrawUILine(float alpha, float brightness = 0.25f, int thickness = 2, int padding = 10, float width = 1f)
        {
            Rect rect = EditorGUILayout.GetControlRect(GUILayout.Height(padding + thickness));
            float w = rect.width; float off = rect.width - rect.width * width;
            rect.height = thickness; rect.y += padding / 2; rect.x -= 2; rect.x += off / 2f; rect.width += 2; rect.width *= width;
            EditorGUI.DrawRect(rect, new Color(brightness, brightness, brightness, alpha));
        }

        public static GUIStyle FrameBoxStyle { get { if (__frameBoxStyle != null) return __frameBoxStyle; __frameBoxStyle = new GUIStyle(EditorStyles.helpBox); Texture2D bg = Resources.Load<Texture2D>("FFrameBox"); __frameBoxStyle.normal.background = bg; __frameBoxStyle.border = new RectOffset(6, 6, 6, 6); __frameBoxStyle.padding = new RectOffset(1, 1, 1, 1); return __frameBoxStyle; } }
        private static GUIStyle __frameBoxStyle = null;

        public static GUIStyle HeaderStyle { get { if (__headerStyle != null) return __headerStyle; __headerStyle = new GUIStyle(EditorStyles.boldLabel); __headerStyle.richText = true; __headerStyle.padding = new RectOffset(0, 0, 0, 0); __headerStyle.margin = __headerStyle.padding; __headerStyle.alignment = TextAnchor.MiddleCenter; __headerStyle.active.textColor = Color.white; return __headerStyle; } }
        private static GUIStyle __headerStyle = null;

        public static GUIStyle HeaderStyleBig { get { if (__headerStyleBig != null) return __headerStyleBig; __headerStyleBig = new GUIStyle(HeaderStyle); __headerStyleBig.fontSize = 17; __headerStyleBig.fontStyle = FontStyle.Normal; return __headerStyle; } }
        private static GUIStyle __headerStyleBig = null;

        public static GUIStyle BGInBoxStyle { get { if (__inBoxStyle != null) return __inBoxStyle; __inBoxStyle = new GUIStyle(EditorStyles.helpBox); Texture2D bg = Resources.Load<Texture2D>("FInBoxSprite"); __inBoxStyle.normal.background = bg; __inBoxStyle.border = new RectOffset(4, 4, 4, 4); __inBoxStyle.padding = new RectOffset(8, 6, 5, 5); __inBoxStyle.margin = new RectOffset(0, 0, 0, 0); return __inBoxStyle; } }
        private static GUIStyle __inBoxStyle = null;

        public static GUIStyle BGInBoxBlankStyle { get { if (__inBoxBlankStyle != null) return __inBoxBlankStyle; __inBoxBlankStyle = new GUIStyle(); __inBoxBlankStyle.padding = BGInBoxStyle.padding; __inBoxBlankStyle.margin = new RectOffset(10,10,4,4); return __inBoxBlankStyle; } }
        private static GUIStyle __inBoxBlankStyle = null;

        static Dictionary<string, Texture2D> _Icons = null;

        /// <summary> Loading texture and remembering reference in the dictionary </summary>
        public static Texture FindIcon(string path)
        {
            if (_Icons == null) _Icons = new Dictionary<string, Texture2D>();

            Texture2D iconTex = null;

            if (_Icons.TryGetValue(path, out iconTex))
            {
                if (iconTex == null) _Icons.Remove(path);
                else return iconTex;
            }

            if (iconTex == null)
            {
                iconTex = Resources.Load<Texture2D>(path);
                _Icons.Add(path, iconTex);
            }

            return iconTex;
        }

        public static void DestroyObject( UnityEngine.Object obj, bool allowDestroyAssets = false )
        {
            if( obj == null ) return;

#if UNITY_EDITOR
            if( Application.isPlaying == false )
                GameObject.DestroyImmediate( obj, allowDestroyAssets );
            else
                GameObject.Destroy( obj );
#else
                GameObject.Destroy(obj);
#endif
        }

        public static void DrawPreviewTexture( Rect rect, Texture from, ScaleMode scaleMode, UnityEngine.Rendering.ColorWriteMask channel )
        {
            if( channel == UnityEngine.Rendering.ColorWriteMask.Alpha )
                EditorGUI.DrawTextureAlpha( rect, from, scaleMode, 1f, 0 );
            else
                EditorGUI.DrawPreviewTexture( rect, from, null, scaleMode, 1f, 0, channel );
        }


        public static void RestoreBindPose(SkinnedMeshRenderer skin )
        {
            if( skin == null || skin.sharedMesh == null ) return;

            Transform[] bones = skin.bones;
            Matrix4x4[] bindPoses = skin.sharedMesh.bindposes;

            if( bones == null || bones.Length == 0 ) return;

            if( bindPoses == null || bindPoses.Length != bones.Length )
            {
                Debug.LogError(
                    $"Cannot restore bind pose for '{skin.name}': " +
                    $"bones count ({bones.Length}) differs from bindposes count " +
                    $"({bindPoses?.Length ?? 0}).",
                    skin );

                return;
            }

            var boneEntries = new List<BoneEntry>( bones.Length );
            Matrix4x4 rendererToWorld = skin.transform.localToWorldMatrix;

            for( int i = 0; i < bones.Length; i++ )
            {
                Transform bone = bones[i];
                if( bone == null ) continue;

                Matrix4x4 targetWorldMatrix = rendererToWorld * bindPoses[i].inverse;

                boneEntries.Add( new BoneEntry
                {
                    Bone = bone,
                    TargetWorldMatrix = targetWorldMatrix,
                    HierarchyDepth = GetHierarchyDepth( bone )
                } );
            }

            boneEntries.Sort( ( a, b ) => a.HierarchyDepth.CompareTo( b.HierarchyDepth ) );

            foreach( BoneEntry entry in boneEntries )
            {
                Transform bone = entry.Bone;
                Matrix4x4 targetLocalMatrix = bone.parent != null ? bone.parent.worldToLocalMatrix * entry.TargetWorldMatrix : entry.TargetWorldMatrix;
                DecomposeMatrix( targetLocalMatrix, out Vector3 localPosition, out Quaternion localRotation, out Vector3 localScale );

                bone.localPosition = localPosition;
                bone.localRotation = localRotation;
                bone.localScale = localScale;
            }
        }

        private static int GetHierarchyDepth( Transform transform )
        {
            int depth = 0;

            while( transform.parent != null )
            {
                depth++;
                transform = transform.parent;
            }

            return depth;
        }

        private static void DecomposeMatrix( Matrix4x4 matrix, out Vector3 position, out Quaternion rotation, out Vector3 scale )
        {
            position = matrix.GetColumn( 3 );

            Vector3 right = matrix.GetColumn( 0 );
            Vector3 up = matrix.GetColumn( 1 );
            Vector3 forward = matrix.GetColumn( 2 );

            scale = new Vector3( right.magnitude, up.magnitude, forward.magnitude );

            if( matrix.determinant < 0f )
            {
                scale.x = -scale.x;
                right = -right;
            }

            if( Mathf.Abs( scale.x ) > Mathf.Epsilon ) right /= Mathf.Abs( scale.x );
            if( Mathf.Abs( scale.y ) > Mathf.Epsilon ) up /= Mathf.Abs( scale.y );
            if( Mathf.Abs( scale.z ) > Mathf.Epsilon ) forward /= Mathf.Abs( scale.z );

            rotation = forward.sqrMagnitude > Mathf.Epsilon && up.sqrMagnitude > Mathf.Epsilon ? Quaternion.LookRotation( forward, up ) : Quaternion.identity;
        }

        private struct BoneEntry
        {
            public Transform Bone;
            public Matrix4x4 TargetWorldMatrix;
            public int HierarchyDepth;
        }
    }
}