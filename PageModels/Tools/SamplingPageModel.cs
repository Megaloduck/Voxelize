using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkiaSharp;
using Voxelize.Services;

namespace Voxelize.PageModels.Tools
{
    public partial class SamplingPageModel : ObservableObject
    {
        private readonly PixelArtService _service = new();
        private SKBitmap _originalBitmap;
        private SKBitmap _resultBitmap;

        // ─── Image Previews ───────────────────────────────────────────
        [ObservableProperty] ImageSource originalImage;
        [ObservableProperty] ImageSource previewImage;

        // ─── Grid Size ────────────────────────────────────────────────
        [ObservableProperty] int gridSize = 16;
        [ObservableProperty] string customGridSize = "16";
        [ObservableProperty] bool showGridOverlay = false;

        // ─── State ────────────────────────────────────────────────────
        [ObservableProperty] bool hasImage = false;
        [ObservableProperty] bool isBusy = false;
        [ObservableProperty] string statusText = "Load an image to begin.";

        // ─── Preset Buttons (just sets GridSize + syncs text field) ───
        [RelayCommand]
        void SetPreset(string size)
        {
            if (int.TryParse(size, out int val))
            {
                GridSize = val;
                CustomGridSize = size;
            }
        }

        // Called when user finishes typing in the Entry
        [RelayCommand]
        void ApplyCustomGridSize()
        {
            if (int.TryParse(CustomGridSize, out int val))
            {
                val = Math.Clamp(val, 2, 256);
                GridSize = val;
                CustomGridSize = val.ToString();
            }
            else
            {
                // Revert to last valid
                CustomGridSize = GridSize.ToString();
            }
        }

        // ─── Load ─────────────────────────────────────────────────────
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

            OriginalImage = BitmapToImageSource(_originalBitmap);
            HasImage = true;
            StatusText = $"Loaded: {_originalBitmap.Width} × {_originalBitmap.Height}px";

            Convert();
        }

        // ─── Convert ──────────────────────────────────────────────────
        [RelayCommand]
        void Convert()
        {
            if (_originalBitmap == null) return;

            IsBusy = true;
            StatusText = "Processing...";

            _resultBitmap = _service.Downsample(_originalBitmap, GridSize);

            var displayBitmap = ShowGridOverlay
                ? DrawGridOverlay(_resultBitmap, GridSize)
                : _resultBitmap;

            PreviewImage = BitmapToImageSource(displayBitmap);

            StatusText = $"Output: {GridSize}×{GridSize} grid  →  {_resultBitmap.Width}×{_resultBitmap.Height}px";
            IsBusy = false;
        }

        // ─── Toggle Grid Overlay ──────────────────────────────────────
        partial void OnShowGridOverlayChanged(bool value) => Convert();

        // ─── Export ───────────────────────────────────────────────────
        [RelayCommand]
        async Task Export()
        {
            if (_resultBitmap == null) return;

            try
            {
                using var image = SKImage.FromBitmap(_resultBitmap);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                var bytes = data.ToArray();

                var fileName = $"voxelize_{GridSize}x{GridSize}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
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

        // ─── Helpers ──────────────────────────────────────────────────
        private static ImageSource BitmapToImageSource(SKBitmap bitmap)
        {
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode();
            var bytes = data.ToArray();
            return ImageSource.FromStream(() => new MemoryStream(bytes));
        }

        private static SKBitmap DrawGridOverlay(SKBitmap source, int gridSize)
        {
            // Each "pixel block" in source maps back to gridSize×gridSize — 
            // we draw lines every (source.Width / gridSize) pixels
            var copy = source.Copy();
            using var canvas = new SKCanvas(copy);

            var paint = new SKPaint
            {
                Color = new SKColor(0, 0, 0, 80),
                StrokeWidth = 1,
                IsAntialias = false
            };

            int cellW = source.Width / gridSize;
            int cellH = source.Height / gridSize;

            if (cellW < 2 || cellH < 2) return copy; // grid too dense to draw

            for (int x = 0; x <= source.Width; x += cellW)
                canvas.DrawLine(x, 0, x, source.Height, paint);

            for (int y = 0; y <= source.Height; y += cellH)
                canvas.DrawLine(0, y, source.Width, y, paint);

            return copy;
        }
    }
}