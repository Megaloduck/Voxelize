using System;
using System.Collections.Generic;
using System.Text;
using SkiaSharp;
using System.Runtime.InteropServices;

namespace Voxelize.Services
{
    public class PosterizationService
    {
        // ─── Main Pipeline ────────────────────────────────────────────
        // Applies pixelation first, then posterization on top
        public SKBitmap Process(SKBitmap source, int gridW, int gridH, int levels)
        {
            var pixelated = Pixelate(source, gridW, gridH);
            return Posterize(pixelated, levels);
        }

        // ─── Step 1: Pixelate (Downsample) ────────────────────────────
        public SKBitmap Pixelate(SKBitmap source, int gridW, int gridH)
        {
            var small = source.Resize(
                new SKImageInfo(gridW, gridH),
                SKFilterQuality.Medium);

            return small.Resize(
                new SKImageInfo(source.Width, source.Height),
                SKFilterQuality.None);
        }

        // ─── Step 2: Posterize (High-Performance via raw pixels) ──────
        // Uses GetPixels() to operate directly on the pixel buffer
        // avoiding per-pixel GetPixel/SetPixel overhead
        public SKBitmap Posterize(SKBitmap source, int levels)
        {
            levels = Math.Clamp(levels, 2, 256);

            var result = source.Copy();

            int pixelCount = result.Width * result.Height;
            int bytesPerPixel = result.BytesPerPixel;

            float step = 256f / levels;
            float scale = 255f / (levels - 1);

            // Build a lookup table (0–255) so we only compute Snap() once per value
            // instead of once per pixel — much faster on large bitmaps
            byte[] lut = new byte[256];
            for (int i = 0; i < 256; i++)
                lut[i] = (byte)Math.Clamp((int)(MathF.Floor(i / step) * scale), 0, 255);

            // Copy pixel buffer into a managed byte array
            int byteCount = result.RowBytes * result.Height;
            byte[] pixels = new byte[byteCount];
            Marshal.Copy(result.GetPixels(), pixels, 0, byteCount);

            // Apply LUT to R, G, B — skip Alpha (offset + 3)
            for (int i = 0; i < pixelCount; i++)
            {
                int offset = i * bytesPerPixel;
                pixels[offset + 0] = lut[pixels[offset + 0]]; // B
                pixels[offset + 1] = lut[pixels[offset + 1]]; // G
                pixels[offset + 2] = lut[pixels[offset + 2]]; // R
            }

            // Write back
            Marshal.Copy(pixels, 0, result.GetPixels(), byteCount);

            return result;
        }

        // Formula: NewColor = Floor(Old / (256/Levels)) * (256 / (Levels-1))
        private static byte Snap(byte value, float step, float scale)
        {
            float snapped = MathF.Floor(value / step) * scale;
            return (byte)Math.Clamp((int)snapped, 0, 255);
        }

        // ─── Bit Depth Label Helper ───────────────────────────────────
        // Maps levels (2–256) to a meaningful bit-depth string
        public static string LevelsTobitDepth(int levels) => levels switch
        {
            2 => "1-bit (2 levels)",
            4 => "2-bit (4 levels)",
            8 => "3-bit (8 levels)",
            16 => "4-bit (16 levels)",
            32 => "5-bit (32 levels)",
            64 => "6-bit (64 levels)",
            128 => "7-bit (128 levels)",
            256 => "8-bit (256 levels — original)",
            _ => $"Custom ({levels} levels)"
        };
    }
}
