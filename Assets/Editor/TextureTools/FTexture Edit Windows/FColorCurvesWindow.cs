using UnityEditor;
using UnityEngine;

namespace FIMSpace.FTextureTools
{
    public class FColorCurvesWindow : FTextureProcessWindow
    {
        private float EffectBlend = 1f;

        public AnimationCurve RGBCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        public AnimationCurve RCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        public AnimationCurve GCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        public AnimationCurve BCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        public static void Init()
        {
            FColorCurvesWindow window = (FColorCurvesWindow)GetWindow(typeof( FColorCurvesWindow ) );
            window.titleContent = new GUIContent("Color Curves", FTextureToolsGUIUtilities.FindIcon("SPR_ColorReplace"), "Tweak colors of target texture using color curves");
            window.previewScale = FEPreview.m_1x1;
            window.drawPreviewScale = true;

            window.previewSize = 128;
            window.position = new Rect(140, 50, 454, 662);
            window.Show();

            called = true;
        }

        protected override void OnGUICustom()
        {
            GUILayout.Space(4);
            GUI.backgroundColor = new Color(0.6f, 1f, 0.7f);
            EffectBlend = EditorGUILayout.Slider(new GUIContent("Effect Blend"), EffectBlend, 0.0f, 1f);
            GUI.backgroundColor = Color.white;
            GUILayout.Space(8);
            RGBCurve = EditorGUILayout.CurveField( "RGB:", RGBCurve, Color.white, new Rect( 0, 0, 1, 1 ) );
            GUILayout.Space(8);
            RCurve = EditorGUILayout.CurveField( "R:", RCurve, Color.red, new Rect( 0, 0, 1, 1 ) );
            GCurve = EditorGUILayout.CurveField( "G:", GCurve, Color.green, new Rect( 0, 0, 1, 1 ) );
            BCurve = EditorGUILayout.CurveField( "B:", BCurve, Color.blue, new Rect( 0, 0, 1, 1 ) );
            GUILayout.Space(8);
        }


        protected override void ProcessTexture(Texture2D source, Texture2D target, bool preview = true)
        {
            if (!preview) EditorUtility.DisplayProgressBar("Tweaking Texture Color...", "Preparing... ", 2f / 5f);


            #region Preparing variables to use down below

            Color32[] sourcePixels = source.GetPixels32();
            Color32[] newPixels = source.GetPixels32();

            if (source.width != target.width || source.height != target.height)
            {
                Debug.LogError("[SEAMLESS GENERATOR] Source texture is different scale or target texture! Can't create seamless texture!");
                return;
            }

            #endregion


            #region Applying Algorithm

            if (!preview)
                EditorUtility.DisplayProgressBar("Replacing...", "Replacing... ", 3f / 5f);

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

                    // Individual channel curves
                    tgtColor.r = Mathf.Clamp01( RCurve.Evaluate( tgtColor.r ) );
                    tgtColor.g = Mathf.Clamp01( GCurve.Evaluate( tgtColor.g ) );
                    tgtColor.b = Mathf.Clamp01( BCurve.Evaluate( tgtColor.b ) );

                    tgtColor.a = sourcePx.a;
                    tgtColor = Color.Lerp( sourcePx, tgtColor, EffectBlend );
                    newPixels[px] = Color32.LerpUnclamped( sourcePixels[px], tgtColor, EffectBlend );
                }
            }

            #endregion


            // Finalizing changes
            if (!preview) EditorUtility.DisplayProgressBar( "Tweaking Texture Color...", "Applying Color to Texture... ", 4f / 5f);

            target.SetPixels32(newPixels);
            target.Apply(false, false);

            if (!preview)
                EditorUtility.ClearProgressBar();
        }

    }
}