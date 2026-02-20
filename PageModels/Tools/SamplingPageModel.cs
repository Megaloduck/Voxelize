using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkiaSharp;
using System.Collections.ObjectModel;
using Voxelize.Services;

namespace Voxelize.PageModels.Tools
{
    public partial class SamplingPageModel : ObservableObject
    {
        private readonly PixelArtService _service = new();
        private SKBitmap _originalBitmap;
        private SKBitmap _resultBitmap;

        // ─── Previews ─────────────────────────────────────────────────
        [ObservableProperty] ImageSource previewImage;
        [ObservableProperty] bool hasImage = false;
        [ObservableProperty] bool isBusy = false;
        [ObservableProperty] string statusText = "Load an image to begin.";

        // ─── Grid Size ────────────────────────────────────────────────
        [ObservableProperty] string customWidth = "32";
        [ObservableProperty] string customHeight = "32";
        [ObservableProperty] bool isLocked = true;

        // ─── Grid Overlay ─────────────────────────────────────────────
        [ObservableProperty] bool showGridOverlay = false;

        // ─── LED Output ───────────────────────────────────────────────
        [ObservableProperty] string selectedFormat = "Arduino / FastLED";
        [ObservableProperty] string formattedOutput = string.Empty;
        [ObservableProperty] bool hasHexCodes = false;

        public List<string> FormatOptions { get; } = new()
        {
            "Arduino / FastLED",
            "Arduino / NeoPixel",
            "Raw Hex Array",
            "Python / MicroPython",
            "Python / Pillow RGB",
            "CSS Hex",
            "JSON",
            "C File (.c)",
            "WLED JSON",
            "cURL",
            "Home Assistant YAML"
        };

        private List<SKColor> _allPixelColors = new();
        private int _gridW, _gridH;

        // ─── Lock Sync ────────────────────────────────────────────────
        partial void OnCustomWidthChanged(string value)
        {
            if (IsLocked) CustomHeight = value;
        }

        partial void OnCustomHeightChanged(string value)
        {
            if (IsLocked) CustomWidth = value;
        }

        partial void OnSelectedFormatChanged(string value) => RebuildOutput();
        partial void OnShowGridOverlayChanged(bool value) => Convert();

        // ─── Commands ─────────────────────────────────────────────────

        [RelayCommand]
        void SetPreset(string size)
        {
            CustomWidth = size;
            CustomHeight = size;
        }

        [RelayCommand]
        void ToggleLock()
        {
            IsLocked = !IsLocked;
            if (IsLocked) CustomHeight = CustomWidth;
        }

        [RelayCommand]
        async Task LoadImage()
        {
            var file = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Select an image",
                FileTypes = FilePickerFileType.Images
            });

            if (file == null) return;

            using var stream = await file.OpenReadAsync();
            _originalBitmap = SKBitmap.Decode(stream);

            if (_originalBitmap == null)
            {
                StatusText = "Failed to decode image.";
                return;
            }

            HasImage = true;
            StatusText = $"Loaded: {_originalBitmap.Width} × {_originalBitmap.Height}px";
            Convert();
        }

        [RelayCommand]
        void Convert()
        {
            if (_originalBitmap == null) return;

            if (!int.TryParse(CustomWidth, out int w) || !int.TryParse(CustomHeight, out int h))
            {
                StatusText = "Invalid resolution input.";
                return;
            }

            w = Math.Clamp(w, 2, 1024);
            h = Math.Clamp(h, 2, 1024);
            CustomWidth = w.ToString();
            CustomHeight = h.ToString();

            IsBusy = true;
            StatusText = "Processing...";

            _resultBitmap = _service.Downsample(_originalBitmap, w, h);

            var displayBitmap = ShowGridOverlay
                ? DrawGridOverlay(_resultBitmap, w, h)
                : _resultBitmap;

            PreviewImage = BitmapToImageSource(displayBitmap);

            // Extract colors and build LED output
            _allPixelColors = ExtractPixelColors(_resultBitmap, w, h);
            _gridW = w;
            _gridH = h;
            RebuildOutput();

            HasHexCodes = _allPixelColors.Count > 0;
            StatusText = $"Output: {w}×{h} grid → {_resultBitmap.Width}×{_resultBitmap.Height}px | {_allPixelColors.Count} pixels";
            IsBusy = false;
        }

        [RelayCommand]
        async Task CopyOutput()
        {
            if (string.IsNullOrEmpty(FormattedOutput)) return;
            await Clipboard.Default.SetTextAsync(FormattedOutput);
            StatusText = "Copied to clipboard!";
        }

        [RelayCommand]
        async Task Export()
        {
            if (_resultBitmap == null) return;

            try
            {
                using var image = SKImage.FromBitmap(_resultBitmap);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                var bytes = data.ToArray();

                var fileName = $"voxelize_{CustomWidth}x{CustomHeight}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                var path = Path.Combine(FileSystem.CacheDirectory, fileName);
                await File.WriteAllBytesAsync(path, bytes);

                await Share.Default.RequestAsync(new ShareFileRequest
                {
                    Title = "Export Pixel Art",
                    File = new ShareFile(path)
                });
            }
            catch (Exception ex)
            {
                StatusText = $"Export failed: {ex.Message}";
            }
        }

        // ─── Private Helpers ──────────────────────────────────────────

        private void RebuildOutput()
        {
            if (_allPixelColors.Count == 0) return;
            FormattedOutput = LedFormatter.Format(_allPixelColors, _gridW, _gridH, SelectedFormat);
        }

        private static List<SKColor> ExtractPixelColors(SKBitmap result, int gridW, int gridH)
        {
            int cellW = result.Width / gridW;
            int cellH = result.Height / gridH;
            var colors = new List<SKColor>(gridW * gridH);

            for (int row = 0; row < gridH; row++)
                for (int col = 0; col < gridW; col++)
                {
                    int px = Math.Clamp(col * cellW + cellW / 2, 0, result.Width - 1);
                    int py = Math.Clamp(row * cellH + cellH / 2, 0, result.Height - 1);
                    colors.Add(result.GetPixel(px, py));
                }

            return colors;
        }

        private static ImageSource BitmapToImageSource(SKBitmap bitmap)
        {
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode();
            var bytes = data.ToArray();
            return ImageSource.FromStream(() => new MemoryStream(bytes));
        }

        private static SKBitmap DrawGridOverlay(SKBitmap source, int gridW, int gridH)
        {
            var copy = source.Copy();
            using var canvas = new SKCanvas(copy);

            var paint = new SKPaint
            {
                Color = new SKColor(0, 0, 0, 80),
                StrokeWidth = 1,
                IsAntialias = false
            };

            int cellW = source.Width / gridW;
            int cellH = source.Height / gridH;

            if (cellW < 2 || cellH < 2) return copy;

            for (int x = 0; x <= source.Width; x += cellW)
                canvas.DrawLine(x, 0, x, source.Height, paint);

            for (int y = 0; y <= source.Height; y += cellH)
                canvas.DrawLine(0, y, source.Width, y, paint);

            return copy;
        }
    }
}