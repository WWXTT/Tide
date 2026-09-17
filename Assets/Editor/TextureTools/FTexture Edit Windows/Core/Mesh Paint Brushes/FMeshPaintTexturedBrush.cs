using UnityEngine;

namespace FIMSpace.FTextureTools
{
    public class FMeshPaintTexturedBrush : FMeshPaintBrush
    {
        public override Texture2D GetReferenceBrushTexture => BrushTexture;

        public Texture2D BrushTexture;
        Texture2D lastPixelsOf = null;
        Color32[] brushPixels = null;

        void EnsureProperBrushReferencePixels()
        {
            if( brushPixels != null && lastPixelsOf != null && lastPixelsOf == BrushTexture ) return;
            lastPixelsOf = BrushTexture;
            brushPixels = lastPixelsOf.GetPixels32();
        }

        protected override void ComputePreviewBrushPixels( FMeshPaintWindow meshPainter, int size, Texture2D previewTexture )
        {
            if( BrushTexture == null )
            {
                base.ComputePreviewBrushPixels( meshPainter, size, previewTexture );
                return;
            }

            EnsureProperBrushReferencePixels();

            for( int y = 0; y < size; y++ )
                for( int x = 0; x < size; x++ )
                {
                    float normalizedDistance = Vector2.Distance( new Vector2( x, y ), new Vector2( ( size - 1 ) * 0.5f, ( size - 1 ) * 0.5f ) ) / ( size * 0.5f - 1f );
                    int textureX = Mathf.Clamp( Mathf.FloorToInt( ( x / (float) size ) * BrushTexture.width ), 0, BrushTexture.width - 1 );
                    int textureY = Mathf.Clamp( Mathf.FloorToInt( ( y / (float) size ) * BrushTexture.height ), 0, BrushTexture.height - 1 );
                    float texturePower = brushPixels[textureY * BrushTexture.width + textureX].r / 255f;
                    float alpha = GetBrushFalloff( normalizedDistance, meshPainter.brushFalloff ) * texturePower * previewedBrushPower;
                    previewTexture.SetPixel( x, y, new Color( 1f, 1f, 1f, alpha ) );
                }
        }

        public override float GetBrushPowerFor( FMeshPaintWindow meshPainter, int x, int y, int minX, int maxX, int minY, int maxY, Vector2 pixelUV, Vector2 brushUV, float brushRadiusUV )
        {
            if( BrushTexture == null ) return base.GetBrushPowerFor( meshPainter, x, y, minX, maxX, minY, maxY, pixelUV, brushUV, brushRadiusUV );

            EnsureProperBrushReferencePixels();

            float u = Mathf.InverseLerp( brushUV.x - brushRadiusUV, brushUV.x + brushRadiusUV, pixelUV.x );
            float v = Mathf.InverseLerp( brushUV.y - brushRadiusUV, brushUV.y + brushRadiusUV, pixelUV.y );
            int textureX = Mathf.Clamp( Mathf.FloorToInt( u * BrushTexture.width ), 0, BrushTexture.width - 1 );
            int textureY = Mathf.Clamp( Mathf.FloorToInt( v * BrushTexture.height ), 0, BrushTexture.height - 1 );
            float texturePower = brushPixels[textureY * BrushTexture.width + textureX].r / 255f;

            return base.GetBrushPowerFor( meshPainter, x, y, minX, maxX, minY, maxY, pixelUV, brushUV, brushRadiusUV ) * texturePower;
        }

    }
}
