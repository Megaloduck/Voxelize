using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkiaSharp;
using Voxelize.Services;

namespace Voxelize.PageModels.Tools
{
    public partial class PosterizationPageModel : ObservableObject
    {
        private readonly PosterizationService _service = new();

        private SKBitmap _originalBitmap;
        private SKBitmap _resultBitmap;

        // ─── State ────────────────────────────────────────────────────
        [ObservableProperty] bool hasImage = false;
        [ObservableProperty] bool isBusy = false;
        [ObservableProperty] bool isComparing = false;  // tap-hold comparison
        [ObservableProperty] string statusText = "Load an image to begin.";

        // ─── Preview ──────────────────────────────────────────────────
        [ObservableProperty] ImageSource previewImage;

        // ─── Pixelation ───────────────────────────────────────────────
        [ObservableProperty] string customWidth = "64";
        [ObservableProperty] string customHeight = "64";
        [ObservableProperty] bool isLocked = true;

        partial void OnCustomWidthChanged(string value)
        {
            if (IsLocked) CustomHeight = value;
        }

        partial void OnCustomHeightChanged(string value)
        {
            if (IsLocked) CustomWidth = value;
        }

        // ─── Posterization ────────────────────────────────────────────
        [ObservableProperty] double posterizeLevels = 8;  // Slider value (double for MAUI Slider)
        [ObservableProperty] string bitDepthLabel = "3-bit (8 levels)";

        // Debounce timer — avoids re-processing on every slider tick
        private CancellationTokenSource _debounceCts;

        partial void OnPosterizeLevelsChanged(double value)
        {
            BitDepthLabel = PosterizationService.LevelsTobitDepth((int)value);
            TriggerDebounced();
        }

        private void TriggerDebounced()
        {
            _debounceCts?.Cancel();
            _debounceCts = new CancellationTokenSource();
            var token = _debounceCts.Token;

            Task.Run(async () =>
            {
                await Task.Delay(200, token);   // 200ms debounce window
                if (!token.IsCancellationRequested)
                    await MainThread.InvokeOnMainThreadAsync(ProcessImage);
            }, token);
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

            HasImage = true;
            StatusText = $"Loaded: {_originalBitmap.Width} × {_originalBitmap.Height}px";
            await ProcessImage();
        }

        // ─── Core Pipeline ────────────────────────────────────────────
        [RelayCommand]
        async Task ProcessImage()
        {
            if (_originalBitmap == null) return;

            if (!int.TryParse(CustomWidth, out int w) || !int.TryParse(CustomHeight, out int h))
            {
                StatusText = "Invalid resolution input.";
                return;
            }

            w = Math.Clamp(w, 2, 1024);
            h = Math.Clamp(h, 2, 1024);

            int levels = (int)PosterizeLevels;

            IsBusy = true;
            StatusText = "Processing...";

            // Run heavy work on background thread
            _resultBitmap = await Task.Run(() => _service.Process(_originalBitmap, w, h, levels));

            PreviewImage = BitmapToImageSource(_resultBitmap);
            StatusText = $"Grid: {w}×{h}  |  {BitDepthLabel}";
            IsBusy = false;
        }

        // ─── Preset ───────────────────────────────────────────────────
        [RelayCommand]
        void SetPreset(string value)
        {
            // "levels:8" → set posterization levels
            if (value.StartsWith("levels:") &&
                int.TryParse(value.Replace("levels:", ""), out int lvl))
            {
                PosterizeLevels = Math.Clamp(lvl, 2, 64);
                return;
            }

            // plain number → set grid size
            if (int.TryParse(value, out int size))
            {
                CustomWidth = size.ToString();
                CustomHeight = size.ToString();
            }
        }

        [RelayCommand]
        void ToggleLock()
        {
            IsLocked = !IsLocked;
            if (IsLocked) CustomHeight = CustomWidth;
        }

        // ─── Comparison (tap and hold) ────────────────────────────────
        [RelayCommand]
        void CompareStart()
        {
            if (_originalBitmap == null) return;
            IsComparing = true;
            PreviewImage = BitmapToImageSource(_originalBitmap);
            StatusText = "Showing original...";
        }

        [RelayCommand]
        void CompareEnd()
        {
            if (!IsComparing) return;
            IsComparing = false;
            if (_resultBitmap != null)
            {
                PreviewImage = BitmapToImageSource(_resultBitmap);
                StatusText = $"Grid: {CustomWidth}×{CustomHeight}  |  {BitDepthLabel}";
            }
        }

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

                var fileName = $"posterized_{CustomWidth}x{CustomHeight}_{(int)PosterizeLevels}lvl_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                var path = Path.Combine(FileSystem.CacheDirectory, fileName);
                await File.WriteAllBytesAsync(path, bytes);

                await Share.Default.RequestAsync(new ShareFileRequest
                {
                    Title = "Export Posterized Image",
                    File = new ShareFile(path)
                });
            }
            catch (Exception ex)
            {
                StatusText = $"Export failed: {ex.Message}";
            }
        }

        // ─── Helper ───────────────────────────────────────────────────
        private static ImageSource BitmapToImageSource(SKBitmap bitmap)
        {
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode();
            var bytes = data.ToArray();
            return ImageSource.FromStream(() => new MemoryStream(bytes));
        }
    }
}