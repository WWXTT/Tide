using FIMSpace.FTex;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;
using UnityEngine.Rendering;

namespace FIMSpace.FTextureTools
{
    public class FMeshPaintWindow : EditorWindow
    {
        public static FMeshPaintWindow Instance { get; private set; } = null;
        private void OnEnable()
        {
            Instance = this;
            wantsMouseMove = true;
        }
        private void OnDisable() => Instance = null;

        protected static bool called = false;

        protected virtual string Title => titleContent.text;
        protected virtual Texture Logo => titleContent.image;
        protected virtual string SubTitle => titleContent.tooltip;

        private void Update()
        {
            if( isTexturePreviewHovered ) Repaint();
        }

        Vector2 scrollPos = Vector2.zero;
        int maxPreviewSize = 256;

        [MenuItem( "Window/F Mesh Painting Window", false, 222 )]
        public static void Init()
        {
            FMeshPaintWindow window = (FMeshPaintWindow)GetWindow( typeof( FMeshPaintWindow ) );
            window.titleContent = new GUIContent( "Mesh Paint Tool", FTextureToolsGUIUtilities.FindIcon( "SPR_MeshPaint" ), "Paint texture/channel over mesh model" );

            window.position = new Rect( 340, 50, 550, 580 );
            window.Show();
            called = true;

            window.lastPaintOn = Selection.activeGameObject;
        }

        bool saveRemind = false;

        void OnGUI()
        {
            HandlePaintHistoryKeyboard( Event.current );

            #region Header GUI

            GUIContent title = new GUIContent( Title );
            GUI.Label( new Rect( 0, 2, position.width, 35 ), title, FTextureToolsGUIUtilities.HeaderStyleBig );

            float wdth = FTextureToolsGUIUtilities.HeaderStyleBig.CalcSize( title ).x;
            GUI.DrawTexture( new Rect( position.width / 2f + wdth / 2f + 11, 9, 22, 22 ), Logo );

            GUI.Label( new Rect( 0, 14, position.width, 50 ), SubTitle, FTextureToolsGUIUtilities.HeaderStyle );
            int currY = 54;

            GUILayout.Space( currY + 1 );

            #endregion

            FTextureToolsGUIUtilities.DrawUILineCommon( 6 );

            scrollPos = EditorGUILayout.BeginScrollView( scrollPos, false, false );
            EditorGUILayout.BeginVertical( FTextureToolsGUIUtilities.BGInBoxBlankStyle );


            EditorGUILayout.BeginHorizontal();
            lastPaintOn = EditorGUILayout.ObjectField( "Paint On:", lastPaintOn, typeof( GameObject ), true ) as GameObject;

            GameObject selectionObj = Selection.activeGameObject;

            if( selectionObj && selectionObj != lastPaintOn )
            {
                GUI.backgroundColor = new Color( 0.5f, 1f, 0.5f, 1f );
                if( GUILayout.Button( "Current Selected" ) ) lastPaintOn = selectionObj;
                GUI.backgroundColor = Color.white;
            }

            targetMaterial = EditorGUILayout.ObjectField( targetMaterial, typeof( Material ), true, GUILayout.MaxWidth( 100 ) ) as Material;

            if( paintOnSkinned )
            {
                if( GUILayout.Button( new GUIContent( FTextureToolsGUIUtilities.FindIcon( "FDefault" ), "Reset skinned mesh bones pose to bind pose to avoid paint collider - visible pose mismatch" ), EditorStyles.label, GUILayout.Width( 20 ), GUILayout.Height( 18 ) ) )
                {
                    FTextureToolsGUIUtilities.RestoreBindPose( paintOnSkinned );
                }
            }

            EditorGUILayout.EndHorizontal();

            if( lastPaintOn != null && lastPaintOn.scene.IsValid() == false )
            {
                UnityEngine.Debug.Log( "[F Mesh Paint] Can't mesh-paint project assets, do it on the scene object instead" );
                lastPaintOn = null;
            }

            if( paintOn != lastPaintOn )
            {
                OnSwitchObjectToPaintOn();
            }

            if( paintOn != null && paintOnRenderer )
            {
                if( paintOnRenderer.sharedMaterial == null )
                {
                    EditorGUILayout.HelpBox( "No material on target mesh renderer!", MessageType.Warning );
                }

                if( paintHelperObject == null ) EnsureHelperObjectsExistance();
            }

            ColorWriteMask previewCol = ColorWriteMask.All;
            if( paintChannel == EPaintChannel.R ) previewCol = ColorWriteMask.Red;
            else if( paintChannel == EPaintChannel.G ) previewCol = ColorWriteMask.Green;
            else if( paintChannel == EPaintChannel.B ) previewCol = ColorWriteMask.Blue;
            else if( paintChannel == EPaintChannel.A ) previewCol = ColorWriteMask.Alpha;
            else if( paintChannel == EPaintChannel.RGB ) previewCol = ColorWriteMask.All;

            if( paintOnRenderer && GetTargetedMaterial )
            {
                if( lastReadProperyTextureName != texturePropertyName )
                {
                    ClearLastPropertyBlock();
                    ApplyRendererPreviewTexture( lastReadProperyTextureName, lastReadPaintTexture );
                    lastReadProperyTextureName = texturePropertyName;
                    if( texturePropertyName == "" ) lastReadPaintTexture = null; else lastReadPaintTexture = GetTargetedMaterial.GetTexture( texturePropertyName );
                    OnChangedBaseTextureToPaint();
                }

                EditorGUIUtility.labelWidth = 60;
                GUILayout.Space( 4 );

                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.BeginVertical();

                EditorGUILayout.BeginHorizontal();

                if( texturePropertyName == "" ) GUI.color = Color.green;
                EditorGUILayout.LabelField( "To Paint:", EditorStyles.boldLabel, GUILayout.Width( 60 ) );
                if( GUILayout.Button( texturePropertyName == "" ? "Select" : texturePropertyName, EditorStyles.popup ) ) Menu_TexturePropertySelector();
                GUI.color = Color.white;

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                paintChannel = (EPaintChannel)EditorGUILayout.EnumPopup( "Channel:", paintChannel );
                if( GUILayout.Button( new GUIContent( FTextureToolsGUIUtilities.FindIcon( "FInfo" ), "Click to display info" ), EditorStyles.label, GUILayout.Width( 20 ), GUILayout.Height( 16 ) ) )
                    EditorUtility.DisplayDialog( "Unity Mask Map Channels", "In unity engine the mask map channels are usually:\n\nRed: Metallic\nGreen: Ambient Occlusion\nBlue: Details\nAlpha: Smoothness", "Ok" );
                EditorGUILayout.EndHorizontal();

                GUILayout.Space( 12 );

                EditorGUILayout.BeginHorizontal();

                GUILayout.Space( 12 );

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space( 8 );

                if( saveRemind ) GUI.backgroundColor = new Color( 0.4f, 1f, 0.4f, 1f );

                if( GUILayout.Button( new GUIContent( " Save (no undo)", FTextureToolsGUIUtilities.FindIcon( "FSaveIcon" ), "Saving painted changes to the source texture file - image file will be overwritten - no undo possible!" ), GUILayout.Height( 32 ) ) )
                {
                    SavePaintedTexture();
                }

                GUI.backgroundColor = Color.white;

                if( lastReadPaintTexture )
                {
                    GUILayout.Space( 8 );
                    EditorGUILayout.ObjectField( lastReadPaintTexture, typeof( Texture2D ), true, GUILayout.Width( 32 ), GUILayout.Height( 32 ) );
                }

                GUILayout.Space( 8 );
                EditorGUILayout.EndHorizontal();


                EditorGUILayout.EndVertical();

                var prevTexBox = EditorGUILayout.GetControlRect( false, 92, GUILayout.Width( 92 ) );

                if( lastReadPaintTexture )
                {
                    FTextureToolsGUIUtilities.DrawPreviewTexture( prevTexBox, lastReadPaintTexture, ScaleMode.StretchToFill, previewCol );
                }

                EditorGUILayout.EndHorizontal();

                EditorGUIUtility.labelWidth = 0;
            }
            else
            {
                EnsureHelperObjectsExistance();
            }

            FTextureToolsGUIUtilities.DrawUILineCommon( 16 );


            EditorGUILayout.BeginHorizontal();

            paintValue = EditorGUILayout.Slider( "Paint Value:", paintValue, 0f, 1f );

            if( GUILayout.Button( new GUIContent( FTextureToolsGUIUtilities.FindIcon( "FInfo" ), "Click to display info" ), EditorStyles.label, GUILayout.Width( 20 ), GUILayout.Height( 16 ), GUILayout.Width( 20 ) ) )
                EditorUtility.DisplayDialog( "Shortcuts", "Hold control when painting to paint with zero value.\nHolt alt when painting to restore source texture pixels on paint area.\nShortcuts with focused scene view:\nUse '[' and ']' keys to increase radius\nUse - + keys to quick change target paint value", "Ok" );

            EditorGUILayout.EndHorizontal();


            GUILayout.Space( 8 );

            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.BeginVertical();

            paintRadius = EditorGUILayout.Slider( "Paint Radius:", paintRadius, 0f, 1f );
            brushPower = EditorGUILayout.Slider( "Brush Power:", brushPower, 0f, 1f );
            brushFalloff = EditorGUILayout.Slider( "Brush Falloff:", brushFalloff, 0f, 1f );
            EditorGUILayout.EndVertical();

            var brushPreviewRect = EditorGUILayout.GetControlRect( false, 42, GUILayout.Width( 42 ) );
            brushPreviewRect.size -= new Vector2( 2, 2 );
            brushPreviewRect.position += new Vector2( 3, -5 );
            GUI.DrawTexture( brushPreviewRect, GetBrush().GetBrushPreview(this), ScaleMode.StretchToFill, true );

            if( GUI.Button( brushPreviewRect, new GUIContent( "", "Click Select paint brush type" ), EditorStyles.label ) )
            {
                GenericMenu gMenu = new GenericMenu();

                gMenu.AddItem( new GUIContent( "Default" ), targetBrush == null, () => { targetBrush = null; } );

                var brushes = Resources.LoadAll<FMeshPaintBrush>( "" );

                for( int i = 0; i < brushes.Length; i++ )
                {
                    var newBrush = brushes[i];
                    gMenu.AddItem( new GUIContent( newBrush.name), targetBrush == newBrush, () => { targetBrush = newBrush; } );
                }

                gMenu.ShowAsContext();
            }

            brushPreviewRect.y += brushPreviewRect.height + 4;
            brushPreviewRect.height = 20;
            targetBrush = EditorGUI.ObjectField( brushPreviewRect, targetBrush, typeof( FMeshPaintBrush ), true ) as FMeshPaintBrush;

            EditorGUILayout.EndHorizontal();

            if ( targetBrush != null)
            {
                if ( targetBrush.GetReferenceBrushTexture != null)
                    if ( targetBrush.GetReferenceBrushTexture.isReadable == false)
                    {
                        EditorGUILayout.HelpBox("Error! Brush reference texture is not readable!", MessageType.Warning );
                        if (GUILayout.Button("Switch isReadable for '" + targetBrush.GetReferenceBrushTexture.name+"'") )
                        {
                            string path = AssetDatabase.GetAssetPath( targetBrush.GetReferenceBrushTexture );
                            TextureImporter tImp = (TextureImporter)AssetImporter.GetAtPath( path );
                            tImp.isReadable = !tImp.isReadable;
                            AssetDatabase.ImportAsset( path, ImportAssetOptions.ForceUpdate );
                        }
                    }
            }

            GetBrush().EditorGUIBrushMenu( this );

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();

            GUILayout.Space( 8 );
            EditorGUILayout.BeginHorizontal();

            GUILayout.Space( 32 );
            if( GUILayout.Button( new GUIContent( " Reset Canvas", FTextureToolsGUIUtilities.FindIcon( "FRemove" ), "Reset painted texture to initial state" ), GUILayout.Height( 24 ) ) )
            {
                lastReadPaintTexture = GetTargetedMaterial.GetTexture( texturePropertyName );
                OnChangedBaseTextureToPaint();
            }
            GUILayout.Space( 32 );

            EditorGUILayout.EndHorizontal();
            GUILayout.Space( 8 );

            int previewSize = (int)Mathf.Min( position.width - 32, maxPreviewSize );
            Rect paintPreviewRect = EditorGUILayout.GetControlRect( false, previewSize );
            paintPreviewRect.x += ( paintPreviewRect.width - previewSize ) / 2f;
            paintPreviewRect.width = previewSize;
            GUI.Box( paintPreviewRect, new GUIContent( paintRT ? "" : "No Preview" ), FTextureToolsGUIUtilities.FrameBoxStyle );

            GUILayout.Space( 8 );

            if( paintRT )
            {
                FTextureToolsGUIUtilities.DrawPreviewTexture( paintPreviewRect, paintRT, ScaleMode.StretchToFill, previewCol );

                HandleTexturePreviewInput( paintPreviewRect, Event.current );
                if( isTexturePreviewHovered ) DrawUVBrushPreview( paintPreviewRect, texturePreviewUV );
                else if( IsPainting && hasSurfaceHit ) DrawUVBrushPreview( paintPreviewRect, lastSurfaceHit.textureCoord );

            }

            EditorGUILayout.BeginVertical( FTextureToolsGUIUtilities.BGInBoxBlankStyle );

            maxPreviewSize = EditorGUILayout.IntSlider( "Max Preview Size:", maxPreviewSize, 64, 1024 );
            GUILayout.Space( 6 );

            bool isPainting = IsPainting;
            if( isPainting ) GUI.backgroundColor = Color.green;

            if( GUILayout.Button( new GUIContent( isPainting ? " Painting" : " Switch Painting" ), GUILayout.Height( 28 ) ) )
            {
                if( isPainting ) Tools.current = Tool.Move;
                else ToolManager.SetActiveTool( typeof( FMeshPaintTool ) );
            }

            var lastRect = GUILayoutUtility.GetLastRect();

            if( SceneView.lastActiveSceneView )
            {
                if( SceneView.lastActiveSceneView.drawGizmos == false )
                {
                    EditorGUILayout.HelpBox( "The gizmos on scene view are disabled! Turn it on to see painting gizmos!", MessageType.Warning );
                    if( GUILayout.Button( "Enable scene gizmos" ) ) SceneView.lastActiveSceneView.drawGizmos = true;
                }
            }

            GUI.backgroundColor = Color.white;

            #region Subtitle

            //lastRect.x += lastRect.width / 2f;
            //lastRect.width /= 2f;
            //lastRect.y += lastRect.height - 18;
            //lastRect.height = 18;
            //GUI.Label(lastRect, "FImpossible Creations " + System.DateTime.Now.Year, FTextureProcessWindow.GetRightTipStyle);

            #endregion


            EditorGUILayout.EndVertical();
        }

        private void ClearLastPropertyBlock()
        {
            if( paintOnRenderer == null ) return;

            MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
            paintOnRenderer.GetPropertyBlock( propertyBlock );
            propertyBlock.Clear();
            paintOnRenderer.SetPropertyBlock( propertyBlock );
        }

        private void OnChangedBaseTextureToPaint()
        {
            DisposeRenderTexture();
            DisposePaintPixels();
            ClearPaintHistory();
            sourcePaintPixels = null;
            saveRemind = false;

            if( lastReadPaintTexture == null ) return;

            var refT = lastReadPaintTexture;

            paintRT = new RenderTexture( refT.width, refT.height, 0, RenderTextureFormat.ARGB32 )
            {
                name = "F Mesh Paint Render Texture",
                filterMode = refT.filterMode,
                wrapMode = refT.wrapMode,
                hideFlags = HideFlags.HideAndDontSave
            };

            paintRT.Create();

            CopySourceTextureToPaintRT( refT );
            CreatePaintPixels();
            CacheSourcePaintPixels();
            ApplyRendererPreviewTexture( texturePropertyName, paintRT );
        }

        void CopySourceTextureToPaintRT( Texture sourceTexture )
        {
            RenderTexture previousActive = RenderTexture.active;

            try
            {
                RenderTexture.active = paintRT;
                GL.Clear( true, true, Color.clear );
                Graphics.Blit( sourceTexture, paintRT );
            }
            finally
            {
                RenderTexture.active = previousActive;
            }
        }

        void DisposeRenderTexture()
        {
            if( paintRT )
            {
                if( RenderTexture.active == paintRT ) RenderTexture.active = null;
                paintRT.Release();
                FTextureToolsGUIUtilities.DestroyObject( paintRT );
                paintRT = null;
            }
        }

        void CacheSourcePaintPixels()
        {
            sourcePaintPixels = paintPixels == null ? null : paintPixels.GetPixels32();
        }

        void CreatePaintPixels()
        {
            DisposePaintPixels();
            if( paintRT == null ) return;

            RenderTexture previousActive = RenderTexture.active;
            RenderTexture.active = paintRT;

            paintPixels = new Texture2D( paintRT.width, paintRT.height, TextureFormat.RGBA32, false )
            {
                name = "F Mesh Paint Working Pixels",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = paintRT.filterMode,
                wrapMode = paintRT.wrapMode
            };

            paintPixels.ReadPixels( new Rect( 0f, 0f, paintRT.width, paintRT.height ), 0, 0 );
            paintPixels.Apply( false, false );

            RenderTexture.active = previousActive;
        }

        void DisposePaintPixels()
        {
            if( paintPixels ) FTextureToolsGUIUtilities.DestroyObject( paintPixels );
            paintPixels = null;
        }

        void ApplyRendererPreviewTexture( string propertyName, Texture texture )
        {
            if( texture == null ) return;
            if( paintOnRenderer == null || string.IsNullOrEmpty( propertyName ) ) return;

            MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
            paintOnRenderer.GetPropertyBlock( propertyBlock );
            propertyBlock.SetTexture( propertyName, texture );
            paintOnRenderer.SetPropertyBlock( propertyBlock );
        }

        void SavePaintedTexture()
        {
            saveRemind = false;

            Texture2D sourceTexture = lastReadPaintTexture as Texture2D;
            if( sourceTexture == null || paintPixels == null )
            {
                Debug.LogError( "[F Mesh Paint] Select a texture asset and paint on it before saving." );
                return;
            }

            string assetPath = AssetDatabase.GetAssetPath( sourceTexture );
            if( string.IsNullOrEmpty( assetPath ) || AssetImporter.GetAtPath( assetPath ) == null )
            {
                Debug.LogError( "[F Mesh Paint] The selected texture is not a writable Unity texture asset." );
                return;
            }

            byte[] fileBytes = GetEncodedPaintedTexture( assetPath );
            if( fileBytes == null ) return;

            try
            {
                EditorUtility.DisplayProgressBar( "Saving Mesh Paint", "Writing painted texture file...", 0.5f );
                File.WriteAllBytes( assetPath, fileBytes );

                EditorUtility.DisplayProgressBar( "Saving Mesh Paint", "Reimporting texture asset...", 0.9f );
                AssetDatabase.ImportAsset( assetPath, ImportAssetOptions.ForceUpdate );
                AssetDatabase.Refresh();

                lastReadPaintTexture = AssetDatabase.LoadAssetAtPath<Texture2D>( assetPath );
                CacheSourcePaintPixels();

                EditorGUIUtility.PingObject( lastReadPaintTexture );
            }
            catch( System.Exception exception )
            {
                Debug.LogError( "[F Mesh Paint] Failed to save painted texture: " + exception.Message );
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        byte[] GetEncodedPaintedTexture( string assetPath )
        {
            string extension = Path.GetExtension( assetPath ).ToLowerInvariant();
            switch( extension )
            {
                case ".png": return paintPixels.EncodeToPNG();
                case ".jpg":
                case ".jpeg": return paintPixels.EncodeToJPG( 95 );
                case ".tga": return FTex_AdditionalEncoders.EncodeToTGA( paintPixels );
                case ".tif":
                case ".tiff": return FTex_AdditionalEncoders.EncodeToTIFF( paintPixels );
                case ".exr": return paintPixels.EncodeToEXR();
                default:
                    Debug.LogError( "[F Mesh Paint] Unsupported source texture format '" + extension + "'. Supported formats are PNG, JPG, TGA, TIFF and EXR." );
                    return null;
            }
        }

        Material targetMaterial = null;

        Material GetTargetedMaterial
        {
            get
            {
                if( targetMaterial == null )
                {
                    if( paintOnRenderer ) targetMaterial = paintOnRenderer.sharedMaterial;
                }

                return targetMaterial;
            }
        }

        string texturePropertyName = "";
        string lastReadProperyTextureName = "";
        Texture lastReadPaintTexture = null;

        void Menu_TexturePropertySelector()
        {
            if( paintOnRenderer == null ) return;
            if( GetTargetedMaterial == null ) return;

            Material refMaterial = GetTargetedMaterial;
            GenericMenu genericMenu = new GenericMenu();

            genericMenu.AddItem( new GUIContent( "None" ), texturePropertyName == "", () => { if( CheckToChangeTextureProperty() ) texturePropertyName = ""; } );

            var names = refMaterial.GetPropertyNames( MaterialPropertyType.Texture );
            MaterialProperty[] properties = MaterialEditor.GetMaterialProperties( new Material[] { refMaterial } );

            for( int i = 0; i < names.Length; i += 1 )
            {
                string name = names[i];

                string dispName = "";
                for( int p = 0; p < properties.Length; p++ )
                {
                    if( properties[p].name == name ) { dispName = " (" + properties[p].displayName + ")"; break; }
                }

                genericMenu.AddItem( new GUIContent( name + dispName ), texturePropertyName == name, () => { if( CheckToChangeTextureProperty() ) texturePropertyName = name; } );
            }

            genericMenu.ShowAsContext();
        }

        bool CheckToChangeTextureProperty()
        {
            if( saveRemind == false ) return true;

            if( EditorUtility.DisplayDialog( "Unsaved changes", "You have unsaved changes for the current paint texture", "Save and switch", "Dont save and switch" ) )
            {
                SavePaintedTexture();
                return true;
            }

            return true;
        }

        enum EPaintChannel
        {
            R, G, B, A, RGB
        }

        EPaintChannel paintChannel = EPaintChannel.R;

        bool IsPainting => ToolManager.activeToolType == typeof( FMeshPaintTool );

        GameObject paintOn = null;
        GameObject lastPaintOn = null;
        MeshFilter paintOnFilter = null;
        Renderer paintOnRenderer = null;
        SkinnedMeshRenderer paintOnSkinned => paintOnRenderer as SkinnedMeshRenderer;

        Mesh paintOnMesh
        {
            get
            {
                if( paintOnFilter ) return paintOnFilter.sharedMesh;

                var skin = paintOnSkinned;
                if( skin ) return skin.sharedMesh;

                return null;
            }
        }

        FMeshPaintHelper paintHelperComp = null;

        GameObject paintHelperObject = null;
        MeshCollider paintHelperCollider = null;

        RaycastHit lastSurfaceHit = new RaycastHit();
        bool hasSurfaceHit = false;
        bool isTexturePreviewHovered = false;
        Vector2 texturePreviewUV;
        readonly List<UVSurfacePoint> texturePreviewSurfacePoints = new List<UVSurfacePoint>();

        RenderTexture paintRT;
        Texture2D paintPixels;
        Color32[] sourcePaintPixels;

        const int maxPaintHistorySteps = 12;
        readonly List<Color32[]> undoPaintHistory = new List<Color32[]>();
        readonly List<Color32[]> redoPaintHistory = new List<Color32[]>();
        Color32[] pendingStrokeUndoPixels;
        bool pendingStrokeHasChanges;

        public float paintValue = 1f;
        public float brushPower = 0.5f;
        public float paintRadius = 0.3f;
        public float brushFalloff = 0.75f;
        public readonly float radiusScaler = 0.25f;

        struct UVSurfacePoint
        {
            public Vector3 Position;
            public Vector3 Normal;
            public float BrushRadius;

            public UVSurfacePoint( Vector3 position, Vector3 normal, float brushRadius )
            {
                Position = position;
                Normal = normal;
                BrushRadius = brushRadius;
            }
        }

        void OnSwitchObjectToPaintOn()
        {
            targetMaterial = null;
            texturePropertyName = "";
            lastReadProperyTextureName = "";
            ClearLastPropertyBlock();
            paintOn = lastPaintOn;
            lastReadProperyTextureName = null;
            lastReadPaintTexture = null;
            DisposeRenderTexture();
            DisposePaintPixels();
            ClearPaintHistory();
            sourcePaintPixels = null;
            hasSurfaceHit = false;
            isTexturePreviewHovered = false;
            texturePreviewSurfacePoints.Clear();
            saveRemind = false;

            if( paintOn == null )
            {
                paintOnFilter = null;
                paintOnRenderer = null;
                RestoreObjectsAndTools();
                return;
            }

            EnsureHelperObjectsExistance();
        }

        void EnsureHelperObjectsExistance()
        {
            if( paintOn == null ) return;

            paintOnFilter = paintOn.GetComponent<MeshFilter>();
            paintOnRenderer = paintOn.GetComponent<Renderer>();

            Mesh targetMesh = paintOnMesh;
            if( targetMesh == null ) return;

            if( paintHelperObject == null )
            {
                paintHelperObject = new GameObject( "F Mesh Paint Helper" );
                paintHelperObject.hideFlags = HideFlags.DontSave;
                paintHelperCollider = paintHelperObject.AddComponent<MeshCollider>();
            }

            if( paintHelperCollider == null ) paintHelperCollider = paintHelperObject.AddComponent<MeshCollider>();
            if( paintHelperComp == null ) paintHelperComp = paintHelperObject.GetComponent<FMeshPaintHelper>();
            if( paintHelperComp == null ) paintHelperComp = paintHelperObject.AddComponent<FMeshPaintHelper>();
            if( paintHelperComp.Parent == null ) paintHelperComp.Parent = this;
            if( paintHelperCollider.sharedMesh != targetMesh ) paintHelperCollider.sharedMesh = targetMesh;

            RefreshHelperObjectCoords();
        }

        void RefreshHelperObjectCoords()
        {
            if( paintHelperObject == null ) return;
            if( paintOn == null ) return;

            paintHelperObject.transform.position = paintOn.transform.position;
            paintHelperObject.transform.rotation = paintOn.transform.rotation;
            paintHelperObject.transform.localScale = paintOn.transform.lossyScale;
        }


        private void OnDestroy()
        {
            RestoreObjectsAndTools();
        }

        private void OnBecameInvisible()
        {
            RestoreObjectsAndTools();
        }

        void RestoreObjectsAndTools()
        {
            ClearLastPropertyBlock();

            if( paintHelperObject ) FTextureToolsGUIUtilities.DestroyObject( paintHelperObject );

            targetMaterial = null;
            paintHelperObject = null;
            paintHelperCollider = null;
            paintHelperComp = null;
            saveRemind = false;

            if( IsPainting ) Tools.current = Tool.Move;
            DisposeRenderTexture();
            DisposePaintPixels();
            ClearPaintHistory();
            sourcePaintPixels = null;

            isTexturePreviewHovered = false;
            texturePreviewSurfacePoints.Clear();
        }



        private void OnDrawGizmos()
        {
            if( SceneView.lastActiveSceneView == null ) return;
            if( paintHelperCollider == null || paintHelperCollider.sharedMesh == null ) return;
            if( IsPainting == false ) return;

            Color discColor = Color.Lerp( Color.gray, Color.white, paintValue );
            discColor.a = 0.2f;
            Handles.color = discColor;

            if( isTexturePreviewHovered )
            {
                for( int i = 0; i < texturePreviewSurfacePoints.Count; i++ )
                {
                    UVSurfacePoint surfacePoint = texturePreviewSurfacePoints[i];
                    Handles.DrawSolidDisc( surfacePoint.Position, surfacePoint.Normal, surfacePoint.BrushRadius );
                }
            }
            else if( hasSurfaceHit )
            {
                Handles.CircleHandleCap( 0, lastSurfaceHit.point, Quaternion.LookRotation( lastSurfaceHit.normal ), GetWorldBrushRadius( lastSurfaceHit.triangleIndex ), EventType.Repaint );

                discColor.a = 0.002f;
                Handles.color = discColor;
                Handles.DrawSolidDisc( lastSurfaceHit.point, lastSurfaceHit.normal, GetWorldBrushRadius( lastSurfaceHit.triangleIndex ) );
            }
        }

        private void OnSceneUpdate()
        {
            RefreshHelperObjectCoords();
        }


        #region Paint Tool Commands

        public void IncreasePaintRadius( float v )
        {
            paintRadius += v;
            paintRadius = Mathf.Clamp01( paintRadius );
        }

        public void IncreasePaintValue( float v )
        {
            paintValue += v;
            paintValue = Mathf.Clamp01( paintValue );
        }

        public void OnPaintingMouseUp( Event evt )
        {
            EndPaintStroke();
        }

        public void OnPaintingClick( Event evt )
        {
            UpdateSurfaceHit( evt );
            if( hasSurfaceHit == false || paintRT == null ) return;
            if( paintPixels == null ) CreatePaintPixels();
            if( paintPixels == null ) return;

            PaintAtUV( lastSurfaceHit.textureCoord, evt );
        }


        FMeshPaintBrush targetBrush;
        FMeshPaintBrush defaultBrush = null;
        FMeshPaintBrush GetBrush()
        {
            if( targetBrush != null )
            {
                if( targetBrush.GetReferenceBrushTexture == null ) return targetBrush;
                if ( targetBrush.GetReferenceBrushTexture.isReadable ) return targetBrush;
            }

            if( defaultBrush == null ) defaultBrush = FMeshPaintBrush.CreateInstance<FMeshPaintBrush>();
            return defaultBrush;
        }

        void PaintAtUV( Vector2 brushUV, Event evt )
        {
            float brushRadiusUV = paintRadius * radiusScaler;
            if( brushRadiusUV <= 0f ||  brushPower <= 0f ) return;

            BeginPaintStroke();

            int textureWidth = paintPixels.width;
            int textureHeight = paintPixels.height;
            int minX = Mathf.Max( 0, Mathf.FloorToInt( ( brushUV.x - brushRadiusUV ) * textureWidth ) );
            int maxX = Mathf.Min( textureWidth - 1, Mathf.CeilToInt( ( brushUV.x + brushRadiusUV ) * textureWidth ) );
            int minY = Mathf.Max( 0, Mathf.FloorToInt( ( brushUV.y - brushRadiusUV ) * textureHeight ) );
            int maxY = Mathf.Min( textureHeight - 1, Mathf.CeilToInt( ( brushUV.y + brushRadiusUV ) * textureHeight ) );

            float tgtValue = paintValue;
            if( evt.control ) tgtValue = 0f;
            bool restoreSourcePixels = evt.alt && sourcePaintPixels != null && sourcePaintPixels.Length == textureWidth * textureHeight;
            if( restoreSourcePixels == false && evt.control ) tgtValue = 0f;

            var brush = GetBrush();
            bool changed = false;

            if( brush.CustomPixelProcessing)
            {
                brush.CustomPixelProcessApply( this, ref changed, minX, maxX, minY, maxY, paintPixels, brushUV, brushRadiusUV );
            }
            else
            {
                for( int y = minY; y <= maxY; y++ )
                    for( int x = minX; x <= maxX; x++ )
                    {
                        Vector2 pixelUV = new Vector2( ( x + 0.5f ) / textureWidth, ( y + 0.5f ) / textureHeight );

                        float strokeBrushPower = brush.GetBrushPowerFor( this, x, y, minX, maxX, minY, maxY, pixelUV, brushUV, brushRadiusUV );
                        float blend = brushPower * strokeBrushPower;

                        if( blend <= 0f ) continue;

                        Color pixel = paintPixels.GetPixel( x, y );
                        Color sourcePixel = restoreSourcePixels ? sourcePaintPixels[y * textureWidth + x] : Color.black;

                        switch( paintChannel )
                        {
                            case EPaintChannel.R: pixel.r = Mathf.Lerp( pixel.r, restoreSourcePixels ? sourcePixel.r : tgtValue, blend ); break;
                            case EPaintChannel.G: pixel.g = Mathf.Lerp( pixel.g, restoreSourcePixels ? sourcePixel.g : tgtValue, blend ); break;
                            case EPaintChannel.B: pixel.b = Mathf.Lerp( pixel.b, restoreSourcePixels ? sourcePixel.b : tgtValue, blend ); break;
                            case EPaintChannel.A: pixel.a = Mathf.Lerp( pixel.a, restoreSourcePixels ? sourcePixel.a : tgtValue, blend ); break;

                            case EPaintChannel.RGB:
                                pixel.r = Mathf.Lerp( pixel.r, restoreSourcePixels ? sourcePixel.r : tgtValue, blend );
                                pixel.g = Mathf.Lerp( pixel.g, restoreSourcePixels ? sourcePixel.g : tgtValue, blend );
                                pixel.b = Mathf.Lerp( pixel.b, restoreSourcePixels ? sourcePixel.b : tgtValue, blend );
                                break;
                        }

                        paintPixels.SetPixel( x, y, pixel );
                        changed = true;
                    }
            }

            if( changed == false ) return;

            RegisterPaintStrokeChange();
            saveRemind = true;
            paintPixels.Apply( false, false );
            Graphics.Blit( paintPixels, paintRT );
            Repaint();
            SceneView.RepaintAll();
        }


        void BeginPaintStroke()
        {
            if( pendingStrokeUndoPixels != null || paintPixels == null ) return;

            pendingStrokeUndoPixels = paintPixels.GetPixels32();
            pendingStrokeHasChanges = false;
        }

        void RegisterPaintStrokeChange()
        {
            if( pendingStrokeUndoPixels == null ) BeginPaintStroke();
            if( pendingStrokeUndoPixels == null || pendingStrokeHasChanges ) return;

            undoPaintHistory.Add( pendingStrokeUndoPixels );
            if( undoPaintHistory.Count > maxPaintHistorySteps ) undoPaintHistory.RemoveAt( 0 );
            redoPaintHistory.Clear();
            pendingStrokeHasChanges = true;
        }

        void EndPaintStroke()
        {
            pendingStrokeUndoPixels = null;
            pendingStrokeHasChanges = false;
        }

        void ClearPaintHistory()
        {
            undoPaintHistory.Clear();
            redoPaintHistory.Clear();
            EndPaintStroke();
        }

        public bool UndoPaint()
        {
            EndPaintStroke();
            if( paintPixels == null || undoPaintHistory.Count == 0 ) return false;

            redoPaintHistory.Add( paintPixels.GetPixels32() );
            int lastIndex = undoPaintHistory.Count - 1;
            ApplyPaintHistoryPixels( undoPaintHistory[lastIndex] );
            undoPaintHistory.RemoveAt( lastIndex );
            return true;
        }

        public bool RedoPaint()
        {
            EndPaintStroke();
            if( paintPixels == null || redoPaintHistory.Count == 0 ) return false;

            undoPaintHistory.Add( paintPixels.GetPixels32() );
            int lastIndex = redoPaintHistory.Count - 1;
            ApplyPaintHistoryPixels( redoPaintHistory[lastIndex] );
            redoPaintHistory.RemoveAt( lastIndex );
            return true;
        }

        void ApplyPaintHistoryPixels( Color32[] pixels )
        {
            if( pixels == null || paintPixels == null || pixels.Length != paintPixels.width * paintPixels.height ) return;

            paintPixels.SetPixels32( pixels );
            paintPixels.Apply( false, false );
            Graphics.Blit( paintPixels, paintRT );
            saveRemind = true;
            Repaint();
            SceneView.RepaintAll();
        }

        void HandlePaintHistoryKeyboard( Event evt )
        {
            if( evt == null || evt.type != EventType.KeyDown || ( evt.control == false && evt.command == false ) ) return;

            bool handled = false;
            if( evt.keyCode == KeyCode.Z ) handled = evt.shift ? RedoPaint() : UndoPaint();
            else if( evt.keyCode == KeyCode.Y ) handled = RedoPaint();

            if( handled ) evt.Use();
        }

        public void OnToolGUI()
        {
            UpdateSurfaceHit( Event.current );
        }

        void UpdateSurfaceHit( Event evt )
        {
            hasSurfaceHit = false;
            if( evt == null || paintHelperCollider == null || paintHelperCollider.sharedMesh == null ) return;

            Ray ray = HandleUtility.GUIPointToWorldRay( evt.mousePosition );
            RaycastHit surfaceHit;
            if( paintHelperCollider.Raycast( ray, out surfaceHit, Mathf.Infinity ) == false ) return;

            lastSurfaceHit = surfaceHit;
            hasSurfaceHit = true;
            Repaint();
        }

        void HandleTexturePreviewInput( Rect previewRect, Event evt )
        {
            bool isInsidePreview = evt != null && evt.type != EventType.MouseLeaveWindow && previewRect.Contains( evt.mousePosition );
            if( isInsidePreview == false )
            {
                if( isTexturePreviewHovered )
                {
                    isTexturePreviewHovered = false;
                    texturePreviewSurfacePoints.Clear();
                    SceneView.RepaintAll();
                }

                return;
            }

            Vector2 previewUV = new Vector2(
                Mathf.InverseLerp( previewRect.xMin, previewRect.xMax, evt.mousePosition.x ),
                1f - Mathf.InverseLerp( previewRect.yMin, previewRect.yMax, evt.mousePosition.y ) );

            if( isTexturePreviewHovered == false || ( previewUV - texturePreviewUV ).sqrMagnitude > 0.000001f )
            {
                texturePreviewUV = previewUV;
                isTexturePreviewHovered = true;
                UpdateTexturePreviewSurfacePoints();
                SceneView.RepaintAll();
            }

            if( evt.type == EventType.MouseUp && evt.button == 0 )
            {
                EndPaintStroke();
                return;
            }

            if( IsPainting == false || evt.button != 0 ) return;
            if( evt.type != EventType.MouseDown && evt.type != EventType.MouseDrag ) return;

            PaintAtUV( texturePreviewUV, evt );
            evt.Use();
        }

        void UpdateTexturePreviewSurfacePoints()
        {
            texturePreviewSurfacePoints.Clear();

            Mesh mesh = paintOnMesh;
            if( mesh == null || paintOn == null ) return;

            Vector2[] uvs = mesh.uv;
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            int[] triangles = mesh.triangles;
            if( uvs == null || vertices == null || triangles == null || uvs.Length != vertices.Length ) return;

            Transform meshTransform = paintOn.transform;
            for( int i = 0; i < triangles.Length; i += 3 )
            {
                int indexA = triangles[i];
                int indexB = triangles[i + 1];
                int indexC = triangles[i + 2];
                Vector3 barycentric;
                if( TryGetUVBarycentric( texturePreviewUV, uvs[indexA], uvs[indexB], uvs[indexC], out barycentric ) == false ) continue;

                Vector3 localPosition = vertices[indexA] * barycentric.x + vertices[indexB] * barycentric.y + vertices[indexC] * barycentric.z;
                Vector3 localNormal;
                if( normals != null && normals.Length == vertices.Length )
                    localNormal = normals[indexA] * barycentric.x + normals[indexB] * barycentric.y + normals[indexC] * barycentric.z;
                else
                    localNormal = Vector3.Cross( vertices[indexB] - vertices[indexA], vertices[indexC] - vertices[indexA] );

                Vector3 normal = meshTransform.TransformDirection( localNormal ).normalized;
                if( normal == Vector3.zero ) normal = Vector3.up;
                float brushRadius = GetWorldBrushRadius( indexA, indexB, indexC, uvs, vertices, meshTransform );
                texturePreviewSurfacePoints.Add( new UVSurfacePoint( meshTransform.TransformPoint( localPosition ), normal, brushRadius ) );
            }
        }

        float GetWorldBrushRadius( int triangleIndex )
        {
            Mesh mesh = paintOnMesh;
            if( mesh == null || paintOn == null ) return paintRadius * radiusScaler;

            int[] triangles = mesh.triangles;
            int firstTriangleIndex = triangleIndex * 3;
            if( firstTriangleIndex < 0 || firstTriangleIndex + 2 >= triangles.Length ) return paintRadius * radiusScaler;

            return GetWorldBrushRadius( triangles[firstTriangleIndex], triangles[firstTriangleIndex + 1], triangles[firstTriangleIndex + 2], mesh.uv, mesh.vertices, paintOn.transform );
        }

        float GetWorldBrushRadius( int indexA, int indexB, int indexC, Vector2[] uvs, Vector3[] vertices, Transform meshTransform )
        {
            float brushRadiusUV = paintRadius * radiusScaler;
            if( uvs == null || vertices == null || meshTransform == null ) return brushRadiusUV;
            if( indexA < 0 || indexB < 0 || indexC < 0 || indexA >= uvs.Length || indexB >= uvs.Length || indexC >= uvs.Length || indexA >= vertices.Length || indexB >= vertices.Length || indexC >= vertices.Length ) return brushRadiusUV;

            Vector2 uvAB = uvs[indexB] - uvs[indexA];
            Vector2 uvAC = uvs[indexC] - uvs[indexA];
            float determinant = uvAB.x * uvAC.y - uvAB.y * uvAC.x;
            if( Mathf.Abs( determinant ) < 0.000001f ) return brushRadiusUV;

            Vector3 worldAB = meshTransform.TransformPoint( vertices[indexB] ) - meshTransform.TransformPoint( vertices[indexA] );
            Vector3 worldAC = meshTransform.TransformPoint( vertices[indexC] ) - meshTransform.TransformPoint( vertices[indexA] );
            Vector3 worldPerU = ( worldAB * uvAC.y - worldAC * uvAB.y ) / determinant;
            Vector3 worldPerV = ( worldAC * uvAB.x - worldAB * uvAC.x ) / determinant;
            float worldUnitsPerUV = Mathf.Sqrt( Vector3.Cross( worldPerU, worldPerV ).magnitude );
            return worldUnitsPerUV > 0f ? brushRadiusUV * worldUnitsPerUV : brushRadiusUV;
        }

        bool TryGetUVBarycentric( Vector2 point, Vector2 uvA, Vector2 uvB, Vector2 uvC, out Vector3 barycentric )
        {
            float denominator = ( uvB.y - uvC.y ) * ( uvA.x - uvC.x ) + ( uvC.x - uvB.x ) * ( uvA.y - uvC.y );
            if( Mathf.Abs( denominator ) < 0.000001f )
            {
                barycentric = Vector3.zero;
                return false;
            }

            float baryA = ( ( uvB.y - uvC.y ) * ( point.x - uvC.x ) + ( uvC.x - uvB.x ) * ( point.y - uvC.y ) ) / denominator;
            float baryB = ( ( uvC.y - uvA.y ) * ( point.x - uvC.x ) + ( uvA.x - uvC.x ) * ( point.y - uvC.y ) ) / denominator;
            float baryC = 1f - baryA - baryB;
            const float tolerance = 0.0001f;
            if( baryA < -tolerance || baryB < -tolerance || baryC < -tolerance )
            {
                barycentric = Vector3.zero;
                return false;
            }

            barycentric = new Vector3( baryA, baryB, baryC );
            return true;
        }

        void DrawUVBrushPreview( Rect previewRect, Vector2 uv )
        {
            float centerX = uv.x * previewRect.width;
            float centerY = ( 1f - uv.y ) * previewRect.height;
            float radius = Mathf.Max( 6f, paintRadius * radiusScaler * previewRect.width );
            Rect circleRect = new Rect( centerX - radius, centerY - radius, radius * 2f, radius * 2f );

            Color previousColor = GUI.color;
            GUI.color = Color.Lerp( Color.white, Color.yellow, 0.5f + paintValue / 2f );
            GUI.BeginGroup( previewRect );
            GUI.DrawTexture( circleRect, GetBrush().GetBrushPreview(this), ScaleMode.StretchToFill, true );
            GUI.EndGroup();
            GUI.color = previousColor;
        }

        #endregion


        [ExecuteAlways]
        class FMeshPaintHelper : MonoBehaviour
        {
            public FMeshPaintWindow Parent;

            private void Update()
            {
                if( Parent == null ) return;
                Parent.OnSceneUpdate();
            }

            private void OnDrawGizmos()
            {
                if( Parent == null ) return;
                Parent.OnDrawGizmos();
            }
        }

    }

}
