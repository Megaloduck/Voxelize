using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkiaSharp;
using Voxelize.Services;

namespace Voxelize.PageModels.Tools
{
    public partial class DitheringPageModel : ObservableObject
    {
        private readonly DitheringService _service = new();

        private SKBitmap _originalBitmap;
        private SKBitmap _resultBitmap;

        // ─── State ────────────────────────────────────────────────────
        [ObservableProperty] bool hasImage = false;
        [ObservableProperty] bool isBusy = false;
        [ObservableProperty] bool isComparing = false;
        [ObservableProperty] string statusText = "Load an image to begin.";

        // ─── Preview ──────────────────────────────────────────────────
        [ObservableProperty] ImageSource previewImage;

        // ─── Grid ─────────────────────────────────────────────────────
        [ObservableProperty] string customWidth = "64";
        [ObservableProperty] string customHeight = "64";
        [ObservableProperty] bool isLocked = true;

        partial void OnCustomWidthChanged(string value) { if (IsLocked) CustomHeight = value; }
        partial void OnCustomHeightChanged(string value) { if (IsLocked) CustomWidth = value; }

        // ─── Palette ──────────────────────────────────────────────────
        [ObservableProperty] bool reducePalette = false;
        [ObservableProperty] double posterizeLevels = 8;
        [ObservableProperty] string bitDepthLabel = "3-bit (8 levels)";

        partial void OnPosterizeLevelsChanged(double value)
        {
            BitDepthLabel = PosterizationService.LevelsTobitDepth((int)value);
            TriggerDebounced();
        }

        partial void OnReducePaletteChanged(bool value) => TriggerDebounced();

        // ─── Dithering ────────────────────────────────────────────────
        [ObservableProperty] string selectedAlgorithm = "Floyd-Steinberg";
        [ObservableProperty] double ditherStrength = 1.0;

        public List<string> AlgorithmOptions { get; } = new()
        {
            "Floyd-Steinberg",
            "Ordered (Bayer 4×4)",
            "Ordered (Bayer 8×8)",
            "Atkinson",
            "Jarvis-Judice-Ninke"
        };

        partial void OnSelectedAlgorithmChanged(string value) => TriggerDebounced();
        partial void OnDitherStrengthChanged(double value) => TriggerDebounced();

        // ─── Debounce ─────────────────────────────────────────────────
        private CancellationTokenSource _debounceCts;

        private void TriggerDebounced()
        {
            if (_originalBitmap == null) return;
            _debounceCts?.Cancel();
            _debounceCts = new CancellationTokenSource();
            var token = _debounceCts.Token;

            Task.Run(async () =>
            {
                await Task.Delay(200, token);
                if (!token.IsCancellationRequested)
                    await MainThread.InvokeOnMainThreadAsync(ProcessImage);
            }, token);
        }

        // ─── Commands ─────────────────────────────────────────────────
        [RelayCommand]
        void SetPreset(string value)
        {
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

            IsBusy = true;
            StatusText = "Processing...";

            var algo = SelectedAlgorithm;
            var strength = (float)DitherStrength;
            var levels = (int)PosterizeLevels;
            var reduce = ReducePalette;

            _resultBitmap = await Task.Run(() =>
                _service.Process(_originalBitmap, w, h, reduce, levels, algo, strength));

            PreviewImage = BitmapToImageSource(_resultBitmap);
            StatusText = $"Grid: {w}×{h}  |  {algo}  |  Strength: {strength:P0}"
                       + (reduce ? $"  |  {BitDepthLabel}" : "");
            IsBusy = false;
        }

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
                PreviewImage = BitmapToImageSource(_resultBitmap);
            StatusText = $"Grid: {CustomWidth}×{CustomHeight}  |  {SelectedAlgorithm}";
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

                var fileName = $"dithered_{SelectedAlgorithm.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                var path = Path.Combine(FileSystem.CacheDirectory, fileName);
                await File.WriteAllBytesAsync(path, bytes);

                await Share.Default.RequestAsync(new ShareFileRequest
                {
                    Title = "Export Dithered Image",
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