using UnityEngine;

namespace FIMSpace.FTextureTools
{
    public class FMeshPaintBlurBrush : FMeshPaintBrush
    {
        public override bool CustomPixelProcessing => true;

        static readonly int[] gaussianWeights = { 1, 4, 6, 4, 1 };

        public override void CustomPixelProcessApply( FMeshPaintWindow meshPainter, ref bool changed, int minX, int maxX, int minY, int maxY, Texture2D paintPixels, Vector2 brushUV, float brushRadiusUV )
        {
            int textureWidth = paintPixels.width;
            int textureHeight = paintPixels.height;
            Color32[] sourcePixels = paintPixels.GetPixels32();
            Color32[] blurredPixels = (Color32[])sourcePixels.Clone();

            for( int y = minY; y <= maxY; y++ )
                for( int x = minX; x <= maxX; x++ )
                {
                    Vector2 pixelUV = new Vector2( ( x + 0.5f ) / textureWidth, ( y + 0.5f ) / textureHeight );

                    float strokeBrushPower = GetBrushPowerFor( meshPainter, x, y, minX, maxX, minY, maxY, pixelUV, brushUV, brushRadiusUV );
                    float blend = meshPainter.paintValue * meshPainter.brushPower * strokeBrushPower;

                    if( blend <= 0f ) continue;

                    int pixelIndex = y * textureWidth + x;
                    Color pixel = sourcePixels[pixelIndex];
                    Color blurredPixel = GetGaussianBlurredPixel( sourcePixels, textureWidth, textureHeight, x, y );
                    Color32 result = Color.Lerp( pixel, blurredPixel, blend );

                    if( result.Equals( sourcePixels[pixelIndex] ) ) continue;

                    blurredPixels[pixelIndex] = result;
                    changed = true;
                }

            if( changed ) paintPixels.SetPixels32( blurredPixels );
        }

        /// <summary> Returns a single-pass 5x5 Gaussian blur using clamped texture-edge samples </summary>
        Color GetGaussianBlurredPixel( Color32[] pixels, int width, int height, int x, int y )
        {
            Color color = Color.clear;
            float totalWeight = 0f;

            for( int offsetY = -2; offsetY <= 2; offsetY++ )
                for( int offsetX = -2; offsetX <= 2; offsetX++ )
                {
                    int sampleX = Mathf.Clamp( x + offsetX, 0, width - 1 );
                    int sampleY = Mathf.Clamp( y + offsetY, 0, height - 1 );
                    int weight = gaussianWeights[offsetX + 2] * gaussianWeights[offsetY + 2];
                    color += (Color)pixels[sampleY * width + sampleX] * weight;
                    totalWeight += weight;
                }

            return color / totalWeight;
        }
    }
}
