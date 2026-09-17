using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace FIMSpace.FTextureTools
{
    public class FMeshPaintTool : EditorTool
    {
        public override bool IsAvailable()
        {
            if( FMeshPaintWindow.Instance == null ) return false;
            return true;
        }

        public override void OnActivated()
        {
            EditorApplication.update += UpdateTool;
            EditorApplication.QueuePlayerLoopUpdate();
        }

        public override void OnWillBeDeactivated()
        {
            EditorApplication.update -= UpdateTool;
            EditorApplication.QueuePlayerLoopUpdate();
        }

        void UpdateTool()
        {
            if( FMeshPaintWindow.Instance == null ) return;
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }

        public override GUIContent toolbarIcon =>
            new GUIContent()
            {
                text = "F Mesh Painter",
                image = EditorGUIUtility.IconContent( "Mesh Icon" ).image,
                tooltip = "F Mesh Texture Painter Tool"
            };

        public override void OnToolGUI( EditorWindow window )
        {
            if( FMeshPaintWindow.Instance == null ) return;

            HandleUtility.AddDefaultControl( GUIUtility.GetControlID( FocusType.Passive ) );
            HandleMouse();

            FMeshPaintWindow.Instance.OnToolGUI();
        }

        void HandleMouse()
        {
            Event e = Event.current;

            switch( e.type )
            {
                case EventType.MouseDown:
                    if( e.button == 0 )
                    {
                        ApplyLeftClickTool( e );
                    }
                    break;

                case EventType.MouseDrag:
                    if( e.button == 0 ) ApplyLeftClickTool( e );
                    break;

                case EventType.KeyDown:

                    if( ( e.control || e.command ) && e.keyCode == KeyCode.Z ) { bool handled = e.shift ? FMeshPaintWindow.Instance.RedoPaint() : FMeshPaintWindow.Instance.UndoPaint(); if( handled ) e.Use(); }
                    else if( ( e.control || e.command ) && e.keyCode == KeyCode.Y ) { if( FMeshPaintWindow.Instance.RedoPaint() ) e.Use(); }
                    else if( e.keyCode == KeyCode.LeftBracket ) { FMeshPaintWindow.Instance.IncreasePaintRadius( -0.05f ); e.Use(); }
                    else if( e.keyCode == KeyCode.RightBracket ) { FMeshPaintWindow.Instance.IncreasePaintRadius( 0.05f ); e.Use(); }
                    else if( e.keyCode == KeyCode.Equals ) { FMeshPaintWindow.Instance.IncreasePaintValue( 0.1f ); e.Use(); }
                    else if( e.keyCode == KeyCode.KeypadPlus ) { FMeshPaintWindow.Instance.IncreasePaintValue( 0.1f ); e.Use(); }
                    else if( e.keyCode == KeyCode.Minus ) { FMeshPaintWindow.Instance.IncreasePaintValue( -0.1f ); e.Use(); }
                    else if( e.keyCode == KeyCode.KeypadMinus ) { FMeshPaintWindow.Instance.IncreasePaintValue( -0.1f ); e.Use(); }
                    break;

                case EventType.MouseUp:
                    FMeshPaintWindow.Instance.OnPaintingMouseUp( e );
                    break;
            }
        }

        void ApplyLeftClickTool( Event evt )
        {
            FMeshPaintWindow.Instance.OnPaintingClick( evt );
            evt.Use();
        }

    }
}