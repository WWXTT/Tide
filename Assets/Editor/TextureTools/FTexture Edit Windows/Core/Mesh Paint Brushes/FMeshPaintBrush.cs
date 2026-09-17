using System;
using UnityEngine;

namespace FIMSpace.FTextureTools
{
    public class FMeshPaintBrush : ScriptableObject
    {
        public virtual Texture2D GetReferenceBrushTexture => null;
        public virtual bool CustomPixelProcessing => false;

        public virtual float GetBrushPowerFor( FMeshPaintWindow meshPainter, int x, int y, int minX, int maxX, int minY, int maxY, Vector2 pixelUV, Vector2 brushUV, float brushRadiusUV )
        {
            float normalizedDistance = Vector2.Distance( pixelUV, brushUV ) / brushRadiusUV;
            return GetBrushFalloff( normalizedDistance, meshPainter.brushFalloff );
        }

        protected float GetBrushFalloff(float normalizedDistance, float brushFalloff)
        {
            if( normalizedDistance > 1f ) return 0f; // Spherical distance cut
            if( brushFalloff <= 0.0001f ) return 1f; // No falloff, just spherical distance

            // Default paint algorithm for mathematical falloff
            float falloffStart = 1f - brushFalloff;
            return 1f - Mathf.SmoothStep( 0f, 1f, Mathf.InverseLerp( falloffStart, 1f, normalizedDistance ) );
        }

        protected Texture2D uvPreviewCircleSprite;
        protected float previewedBrushFalloff = -1f;
        protected float previewedBrushPower = -1f;

        public virtual Texture2D GetBrushPreview( FMeshPaintWindow meshPainter )
        {
            const int size = 128;

            if( uvPreviewCircleSprite == null )
            {
                uvPreviewCircleSprite = new Texture2D( size, size, TextureFormat.RGBA32, false )
                {
                    name = "F Mesh Paint UV Brush Preview",
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                previewedBrushFalloff = -1f;
                previewedBrushPower = -1f;
            }

            if( Mathf.Approximately( previewedBrushFalloff, meshPainter.brushFalloff ) && Mathf.Approximately( previewedBrushPower, meshPainter.brushPower ) )
                return uvPreviewCircleSprite;

            previewedBrushFalloff = meshPainter.brushFalloff;
            previewedBrushPower = meshPainter.brushPower;

            ComputePreviewBrushPixels( meshPainter, size, uvPreviewCircleSprite );

            uvPreviewCircleSprite.Apply();
            return uvPreviewCircleSprite;
        }

        protected virtual void ComputePreviewBrushPixels( FMeshPaintWindow meshPainter, int size, Texture2D previewTexture)
        {
            Vector2 center = new Vector2( ( size - 1 ) * 0.5f, ( size - 1 ) * 0.5f );
            float outerRadius = size * 0.5f - 1f;
            for( int y = 0; y < size; y++ )
                for( int x = 0; x < size; x++ )
                {
                    float normalizedDistance = Vector2.Distance( new Vector2( x, y ), center ) / outerRadius;
                    float alpha = GetBrushFalloff( normalizedDistance, meshPainter.brushFalloff ) * previewedBrushPower;
                    previewTexture.SetPixel( x, y, new Color( 1f, 1f, 1f, alpha ) );
                }
        }

        public virtual void EditorGUIBrushMenu( FMeshPaintWindow meshPainter )
        {
            
        }

        public virtual void CustomPixelProcessApply( FMeshPaintWindow fMeshPaintWindow, ref bool changed, int minX, int maxX, int minY, int maxY, Texture2D paintPixels, Vector2 brushUV, float brushRadiusUV )
        {
        }

    }
}