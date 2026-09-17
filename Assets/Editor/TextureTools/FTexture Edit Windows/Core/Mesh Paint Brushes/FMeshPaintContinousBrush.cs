using UnityEditor;
using UnityEngine;

namespace FIMSpace.FTextureTools
{
    public class FMeshPaintContinousBrush : FMeshPaintBrush
    {
        public override Texture2D GetReferenceBrushTexture => SeamlessTexture;

        public Texture2D SeamlessTexture;
        public float TilingScale = 1f;

        Texture2D lastPixelsOf = null;
        Color32[] brushPixels = null;
        Texture2D previewedTexture = null;
        float previewedTilingScale = float.NaN;

        public override Texture2D GetBrushPreview( FMeshPaintWindow meshPainter )
        {
            // The base preview cache only observes falloff and power, so invalidate it
            // when the source texture or its tiling changes too.
            if( previewedTexture != SeamlessTexture || !Mathf.Approximately( previewedTilingScale, TilingScale ) )
            {
                previewedTexture = SeamlessTexture;
                previewedTilingScale = TilingScale;
                previewedBrushFalloff = -1f;
            }

            return base.GetBrushPreview( meshPainter );
        }

        public override void EditorGUIBrushMenu( FMeshPaintWindow meshPainter )
        {
            GUILayout.Space( 5 );
            TilingScale = EditorGUILayout.Slider( "Texture Tiling Scale:", TilingScale, 0.1f, 10f );
            TilingScale = Mathf.Clamp( TilingScale, 0.1f, 10f );
        }

        void EnsureProperBrushReferencePixels()
        {
            if( brushPixels != null && lastPixelsOf != null && lastPixelsOf == SeamlessTexture ) return;
            lastPixelsOf = SeamlessTexture;
            brushPixels = lastPixelsOf.GetPixels32();
        }

        protected override void ComputePreviewBrushPixels( FMeshPaintWindow meshPainter, int size, Texture2D previewTexture )
        {
            if( SeamlessTexture == null )
            {
                base.ComputePreviewBrushPixels( meshPainter, size, previewTexture );
                return;
            }

            EnsureProperBrushReferencePixels();

            Vector2 center = new Vector2( ( size - 1 ) * 0.5f, ( size - 1 ) * 0.5f );
            float outerRadius = size * 0.5f - 1f;

            for( int y = 0; y < size; y++ )
                for( int x = 0; x < size; x++ )
                {
                    Vector2 previewUV = new Vector2( ( x + 0.5f ) / size, ( y + 0.5f ) / size );
                    float texturePower = GetTexturePowerAtUV( previewUV );
                    float normalizedDistance = Vector2.Distance( new Vector2( x, y ), center ) / outerRadius;
                    float alpha = GetBrushFalloff( normalizedDistance, meshPainter.brushFalloff ) * texturePower * previewedBrushPower;
                    previewTexture.SetPixel( x, y, new Color( 1f, 1f, 1f, alpha ) );
                }
        }

        public override float GetBrushPowerFor( FMeshPaintWindow meshPainter, int x, int y, int minX, int maxX, int minY, int maxY, Vector2 pixelUV, Vector2 brushUV, float brushRadiusUV )
        {
            if( SeamlessTexture == null ) return base.GetBrushPowerFor( meshPainter, x, y, minX, maxX, minY, maxY, pixelUV, brushUV, brushRadiusUV );

            EnsureProperBrushReferencePixels();

            // Sampling absolute mesh UVs keeps the pattern fixed while the brush moves,
            // like scrolling over a continuous 2D surface instead of restarting each stamp.
            float texturePower = GetTexturePowerAtUV( pixelUV );

            return base.GetBrushPowerFor( meshPainter, x, y, minX, maxX, minY, maxY, pixelUV, brushUV, brushRadiusUV ) * texturePower;
        }

        float GetTexturePowerAtUV( Vector2 uv )
        {
            float u = Mathf.Repeat( uv.x * TilingScale, 1f );
            float v = Mathf.Repeat( uv.y * TilingScale, 1f );
            int textureX = Mathf.Clamp( Mathf.FloorToInt( u * SeamlessTexture.width ), 0, SeamlessTexture.width - 1 );
            int textureY = Mathf.Clamp( Mathf.FloorToInt( v * SeamlessTexture.height ), 0, SeamlessTexture.height - 1 );
            return brushPixels[textureY * SeamlessTexture.width + textureX].r / 255f;
        }
    }
}
