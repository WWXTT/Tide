using UnityEditor;
using UnityEngine;

namespace FIMSpace.FTextureTools
{
    public class FHueSaturationWindow : FTextureProcessWindow
    {
        private float EffectBlend = 1f;

        private float Hue = 0f;
        private float Saturation = 0.0f;
        private float Value = 0.0f;

        public AnimationCurve HueBrCurve = AnimationCurve.Linear( 0f, 1f, 1f, 1f );
        public AnimationCurve SaturationBrCurve = AnimationCurve.Linear( 0f, 1f, 1f, 1f );
        public AnimationCurve ValueBrCurve = AnimationCurve.Linear( 0f, 1f, 1f, 1f );

        public AnimationCurve RGBCurve = AnimationCurve.Linear( 0f, 0f, 1f, 1f );

        public static void Init()
        {
            FHueSaturationWindow window = (FHueSaturationWindow)GetWindow( typeof( FHueSaturationWindow ) );
            window.titleContent = new GUIContent( "Hue-Saturation", FTextureToolsGUIUtilities.FindIcon( "SPR_ColorReplace" ), "Tweak colors of target texture" );
            window.previewScale = FEPreview.m_1x1;
            window.drawPreviewScale = true;

            window.previewSize = 128;
            window.position = new Rect( 140, 50, 454, 662 );
            window.Show();

            called = true;
        }

        protected override void OnGUICustom()
        {
            GUILayout.Space( 4 );
            GUI.backgroundColor = new Color( 0.6f, 1f, 0.7f );
            EffectBlend = EditorGUILayout.Slider( new GUIContent( "Effect Blend" ), EffectBlend, 0.0f, 1f );
            GUI.backgroundColor = Color.white;
            GUILayout.Space( 8 );

            int wdth = 54;

            EditorGUILayout.BeginHorizontal();
            Hue = EditorGUILayout.Slider( new GUIContent( "Hue" ), Hue, -1f, 1f );
            HueBrCurve = EditorGUILayout.CurveField( HueBrCurve, Color.white, new Rect( 0, 0, 1, 1 ), GUILayout.MaxWidth( wdth ) );
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            Saturation = EditorGUILayout.Slider( new GUIContent( "Saturation" ), Saturation, -1f, 1f );
            SaturationBrCurve = EditorGUILayout.CurveField( SaturationBrCurve, Color.white, new Rect( 0, 0, 1, 1 ), GUILayout.MaxWidth( wdth ) );
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            Value = EditorGUILayout.Slider( new GUIContent( "Value" ), Value, -1f, 1f );
            ValueBrCurve = EditorGUILayout.CurveField( ValueBrCurve, Color.white, new Rect( 0, 0, 1, 1 ), GUILayout.MaxWidth( wdth ) );
            EditorGUILayout.EndHorizontal();

            GUILayout.Space( 8 );
            RGBCurve = EditorGUILayout.CurveField( "RGB:", RGBCurve, Color.white, new Rect( 0, 0, 1, 1 ) );

            GUILayout.Space( 8 );
        }


        protected override void ProcessTexture( Texture2D source, Texture2D target, bool preview = true )
        {
            if( !preview ) EditorUtility.DisplayProgressBar( "Tweaking Texture Color...", "Preparing... ", 2f / 5f );


            #region Preparing variables to use down below

            Color32[] sourcePixels = source.GetPixels32();
            Color32[] newPixels = source.GetPixels32();

            if( source.width != target.width || source.height != target.height )
            {
                Debug.LogError( "[SEAMLESS GENERATOR] Source texture is different scale or target texture! Can't create seamless texture!" );
                return;
            }

            #endregion


            #region Replacing Texture Color

            if( !preview )
                EditorUtility.DisplayProgressBar( "Replacing...", "Replacing... ", 3f / 5f );

            Vector2 dim = new Vector2( source.width, source.height );

            for( int x = 0; x < source.width; x++ )
            {
                for( int y = 0; y < source.height; y++ )
                {
                    int px = GetPX( x, y, dim );
                    Color sourcePx = sourcePixels[px];

                    // Apply RGB curve using luminance as input
                    float luminance = sourcePx.r * 0.299f + sourcePx.g * 0.587f + sourcePx.b * 0.114f;
                    float rgbCurveValue = RGBCurve.Evaluate( luminance );

                    Color tgtColor = sourcePx;

                    tgtColor.r = Mathf.Clamp01( sourcePx.r + ( rgbCurveValue - luminance ) );
                    tgtColor.g = Mathf.Clamp01( sourcePx.g + ( rgbCurveValue - luminance ) );
                    tgtColor.b = Mathf.Clamp01( sourcePx.b + ( rgbCurveValue - luminance ) );

                    float h, s, v;
                    Color.RGBToHSV( tgtColor, out h, out s, out v );

                    h += Hue * HueBrCurve.Evaluate( luminance );
                    if( h < 0f ) h += 1f;
                    if( h > 1f ) h -= 1f;

                    tgtColor = Color.HSVToRGB( h, Mathf.Clamp01( s + Saturation * EffectBlend * SaturationBrCurve.Evaluate( luminance ) ), Mathf.Clamp01( v + Value * EffectBlend * ValueBrCurve.Evaluate( luminance ) ) );

                    tgtColor.a = sourcePx.a;
                    tgtColor = Color.Lerp( sourcePx, tgtColor, EffectBlend );
                    newPixels[px] = Color32.LerpUnclamped( sourcePixels[px], tgtColor, EffectBlend );
                }
            }

            #endregion


            // Finalizing changes
            if( !preview ) EditorUtility.DisplayProgressBar( "Tweaking Texture Color...", "Applying Color to Texture... ", 4f / 5f );

            target.SetPixels32( newPixels );
            target.Apply( false, false );

            if( !preview )
                EditorUtility.ClearProgressBar();
        }

    }
}