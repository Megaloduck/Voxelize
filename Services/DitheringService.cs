using SkiaSharp;
using System.Runtime.InteropServices;

namespace Voxelize.Services
{
    public class DitheringService
    {
        // ─── Main Pipeline ────────────────────────────────────────────
        // Pixelate → (optional) Posterize → Dither
        public SKBitmap Process(
            SKBitmap source,
            int gridW, int gridH,
            bool reducePalette, int levels,
            string algorithm,
            float strength)
        {
            // Step 1: Pixelate
            var pixelated = Pixelate(source, gridW, gridH);

            // Step 2: Optional posterization
            var prepared = reducePalette
                ? Posterize(pixelated, levels)
                : pixelated;

            // Step 3: Dither
            return algorithm switch
            {
                "Floyd-Steinberg" => FloydSteinberg(prepared, strength),
                "Ordered (Bayer 4×4)" => Ordered(prepared, strength, 4),
                "Ordered (Bayer 8×8)" => Ordered(prepared, strength, 8),
                "Atkinson" => Atkinson(prepared, strength),
                "Jarvis-Judice-Ninke" => JarvisJudiceNinke(prepared, strength),
                _ => prepared
            };
        }

        // ─── Step 1: Pixelate ─────────────────────────────────────────
        private static SKBitmap Pixelate(SKBitmap source, int gridW, int gridH)
        {
            var small = source.Resize(new SKImageInfo(gridW, gridH), SKFilterQuality.Medium);
            return small.Resize(new SKImageInfo(source.Width, source.Height), SKFilterQuality.None);
        }

        // ─── Step 2: Posterize (LUT-based) ────────────────────────────
        private static SKBitmap Posterize(SKBitmap source, int levels)
        {
            levels = Math.Clamp(levels, 2, 256);
            var result = source.Copy();

            float step = 256f / levels;
            float scale = 255f / (levels - 1);

            byte[] lut = new byte[256];
            for (int i = 0; i < 256; i++)
                lut[i] = (byte)Math.Clamp((int)(MathF.Floor(i / step) * scale), 0, 255);

            int byteCount = result.RowBytes * result.Height;
            int bytesPerPixel = result.BytesPerPixel;
            byte[] pixels = new byte[byteCount];
            Marshal.Copy(result.GetPixels(), pixels, 0, byteCount);

            for (int i = 0; i < result.Width * result.Height; i++)
            {
                int o = i * bytesPerPixel;
                pixels[o + 0] = lut[pixels[o + 0]];
                pixels[o + 1] = lut[pixels[o + 1]];
                pixels[o + 2] = lut[pixels[o + 2]];
            }

            Marshal.Copy(pixels, 0, result.GetPixels(), byteCount);
            return result;
        }

        // ─── Helpers ──────────────────────────────────────────────────

        // Reads pixel buffer into a float[,3] array for error diffusion
        private static float[,] ToFloatBuffer(SKBitmap bmp)
        {
            int count = bmp.Width * bmp.Height;
            int bpp = bmp.BytesPerPixel;
            byte[] raw = new byte[bmp.RowBytes * bmp.Height];
            Marshal.Copy(bmp.GetPixels(), raw, 0, raw.Length);

            // [pixelIndex, channel]  0=B 1=G 2=R
            var buf = new float[count, 3];
            for (int i = 0; i < count; i++)
            {
                int o = i * bpp;
                buf[i, 0] = raw[o + 0];
                buf[i, 1] = raw[o + 1];
                buf[i, 2] = raw[o + 2];
            }
            return buf;
        }

        // Writes float buffer back to a new SKBitmap
        private static SKBitmap FromFloatBuffer(float[,] buf, int width, int height, int bpp)
        {
            var result = new SKBitmap(width, height);
            byte[] raw = new byte[result.RowBytes * result.Height];
            Marshal.Copy(result.GetPixels(), raw, 0, raw.Length);

            for (int i = 0; i < width * height; i++)
            {
                int o = i * bpp;
                raw[o + 0] = Clamp(buf[i, 0]);
                raw[o + 1] = Clamp(buf[i, 1]);
                raw[o + 2] = Clamp(buf[i, 2]);
                raw[o + 3] = 255; // alpha
            }

            Marshal.Copy(raw, 0, result.GetPixels(), raw.Length);
            return result;
        }

        // Snaps a float channel to nearest 0 or 255 (1-bit dither target)
        // For full color, snap to nearest multiple of quantStep
        private static float Quantize(float value, int quantStep)
        {
            return MathF.Round(value / quantStep) * quantStep;
        }

        private static byte Clamp(float v) =>
            (byte)Math.Clamp((int)v, 0, 255);

        // Blends original and dithered result by strength (0=original, 1=full dither)
        private static SKBitmap BlendStrength(SKBitmap original, SKBitmap dithered, float strength)
        {
            if (strength >= 1f) return dithered;
            if (strength <= 0f) return original;

            int count = original.Width * original.Height;
            int bpp = original.BytesPerPixel;
            int byteCount = original.RowBytes * original.Height;

            byte[] src = new byte[byteCount];
            byte[] dst = new byte[byteCount];
            Marshal.Copy(original.GetPixels(), src, 0, byteCount);
            Marshal.Copy(dithered.GetPixels(), dst, 0, byteCount);

            var result = new SKBitmap(original.Width, original.Height);
            byte[] blended = new byte[byteCount];

            for (int i = 0; i < count; i++)
            {
                int o = i * bpp;
                for (int c = 0; c < 3; c++)
                    blended[o + c] = (byte)(src[o + c] + strength * (dst[o + c] - src[o + c]));
                blended[o + 3] = 255;
            }

            Marshal.Copy(blended, 0, result.GetPixels(), byteCount);
            return result;
        }

        // ─── Algorithm 1: Floyd-Steinberg ─────────────────────────────
        //
        //         X   7/16
        //   3/16  5/16  1/16
        //
        private static SKBitmap FloydSteinberg(SKBitmap source, float strength)
        {
            int w = source.Width, h = source.Height;
            var buf = ToFloatBuffer(source);
            int quantStep = 128; // snaps to 0 or 255 per channel

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int idx = y * w + x;
                    for (int c = 0; c < 3; c++)
                    {
                        float old = buf[idx, c];
                        float nw = Quantize(old, quantStep);
                        float err = old - nw;
                        buf[idx, c] = nw;

                        if (x + 1 < w)
                            buf[idx + 1, c] += err * 7f / 16f;
                        if (x - 1 >= 0 && y + 1 < h)
                            buf[idx + w - 1, c] += err * 3f / 16f;
                        if (y + 1 < h)
                            buf[idx + w, c] += err * 5f / 16f;
                        if (x + 1 < w && y + 1 < h)
                            buf[idx + w + 1, c] += err * 1f / 16f;
                    }
                }
            }

            var dithered = FromFloatBuffer(buf, w, h, source.BytesPerPixel);
            return BlendStrength(source, dithered, strength);
        }

        // ─── Algorithm 2: Ordered / Bayer ─────────────────────────────
        private static readonly float[,] Bayer4 = {
            {  0f, 8f, 2f,10f },
            { 12f, 4f,14f, 6f },
            {  3f,11f, 1f, 9f },
            { 15f, 7f,13f, 5f }
        };

        private static readonly float[,] Bayer8 = {
            {  0f,32f, 8f,40f, 2f,34f,10f,42f },
            { 48f,16f,56f,24f,50f,18f,58f,26f },
            { 12f,44f, 4f,36f,14f,46f, 6f,38f },
            { 60f,28f,52f,20f,62f,30f,54f,22f },
            {  3f,35f,11f,43f, 1f,33f, 9f,41f },
            { 51f,19f,59f,27f,49f,17f,57f,25f },
            { 15f,47f, 7f,39f,13f,45f, 5f,37f },
            { 63f,31f,55f,23f,61f,29f,53f,21f }
        };

        private static SKBitmap Ordered(SKBitmap source, float strength, int size)
        {
            int w = source.Width, h = source.Height;
            var buf = ToFloatBuffer(source);
            var matrix = size == 4 ? Bayer4 : Bayer8;
            float matSize = size * size;

            var result = new float[w * h, 3];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int idx = y * w + x;
                    float threshold = (matrix[y % size, x % size] / matSize - 0.5f) * 255f;

                    for (int c = 0; c < 3; c++)
                    {
                        float adjusted = buf[idx, c] + threshold;
                        result[idx, c] = Quantize(Math.Clamp(adjusted, 0f, 255f), 128);
                    }
                }
            }

            var dithered = FromFloatBuffer(result, w, h, source.BytesPerPixel);
            return BlendStrength(source, dithered, strength);
        }

        // ─── Algorithm 3: Atkinson ────────────────────────────────────
        //
        //       X   1/8  1/8
        //  1/8  1/8  1/8
        //       1/8
        //
        private static SKBitmap Atkinson(SKBitmap source, float strength)
        {
            int w = source.Width, h = source.Height;
            var buf = ToFloatBuffer(source);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int idx = y * w + x;
                    for (int c = 0; c < 3; c++)
                    {
                        float old = buf[idx, c];
                        float nw = Quantize(old, 128);
                        // Atkinson only diffuses 6/8 (3/4) of the error
                        float err = (old - nw) / 8f;
                        buf[idx, c] = nw;

                        void Spread(int ti) { if (ti >= 0 && ti < w * h) buf[ti, c] += err; }

                        Spread(idx + 1);
                        Spread(idx + 2);
                        Spread(idx + w - 1);
                        Spread(idx + w);
                        Spread(idx + w + 1);
                        Spread(idx + w * 2);
                    }
                }
            }

            var dithered = FromFloatBuffer(buf, w, h, source.BytesPerPixel);
            return BlendStrength(source, dithered, strength);
        }

        // ─── Algorithm 4: Jarvis-Judice-Ninke ────────────────────────
        //
        //             X  7/48  5/48
        //  3/48  5/48  7/48  5/48  3/48
        //  1/48  3/48  5/48  3/48  1/48
        //
        private static SKBitmap JarvisJudiceNinke(SKBitmap source, float strength)
        {
            int w = source.Width, h = source.Height;
            var buf = ToFloatBuffer(source);

            // [dx, dy, weight numerator] — denominator is 48
            (int dx, int dy, float w48)[] kernel = {
                ( 1, 0, 7f), ( 2, 0, 5f),
                (-2, 1, 3f), (-1, 1, 5f), ( 0, 1, 7f), ( 1, 1, 5f), ( 2, 1, 3f),
                (-2, 2, 1f), (-1, 2, 3f), ( 0, 2, 5f), ( 1, 2, 3f), ( 2, 2, 1f)
            };

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int idx = y * w + x;
                    for (int c = 0; c < 3; c++)
                    {
                        float old = buf[idx, c];
                        float nw = Quantize(old, 128);
                        float err = old - nw;
                        buf[idx, c] = nw;

                        foreach (var (dx, dy, weight) in kernel)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx >= 0 && nx < w && ny >= 0 && ny < h)
                                buf[ny * w + nx, c] += err * weight / 48f;
                        }
                    }
                }
            }

            var dithered = FromFloatBuffer(buf, w, h, source.BytesPerPixel);
            return BlendStrength(source, dithered, strength);
        }
    }
}