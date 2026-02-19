using System;
using System.Collections.Generic;
using System.Text;
using SkiaSharp;

namespace Voxelize.Services
{
    public class PixelArtService
    {
        public SKBitmap ConvertToPixelArt(SKBitmap source, int pixelSize, int colorDepth)
        {
            int w = source.Width / pixelSize;
            int h = source.Height / pixelSize;

            var resized = source.Resize(
                new SKImageInfo(w, h),
                SKFilterQuality.None);

            var reduced = ReduceColors(resized, colorDepth);

            return reduced.Resize(
                new SKImageInfo(source.Width, source.Height),
                SKFilterQuality.None);
        }

        private SKBitmap ReduceColors(SKBitmap bmp, int depth)
        {
            var result = new SKBitmap(bmp.Width, bmp.Height);

            for (int y = 0; y < bmp.Height; y++)
            {
                for (int x = 0; x < bmp.Width; x++)
                {
                    var c = bmp.GetPixel(x, y);

                    byte r = (byte)(c.Red / depth * depth);
                    byte g = (byte)(c.Green / depth * depth);
                    byte b = (byte)(c.Blue / depth * depth);

                    result.SetPixel(x, y, new SKColor(r, g, b));
                }
            }

            return result;
        }
        public SKBitmap Downsample(SKBitmap source, int gridSize)
        {
            // Step 1: shrink to gridSize × gridSize
            var small = source.Resize(
                new SKImageInfo(gridSize, gridSize),
                SKFilterQuality.Medium);

            // Step 2: scale back up — nearest-neighbour keeps hard pixel edges
            return small.Resize(
                new SKImageInfo(source.Width, source.Height),
                SKFilterQuality.None);
        }
    }
}