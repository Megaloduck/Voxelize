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
        [ObservableProperty] bool isLocked = true; // square lock

        // ─── Grid Overlay ─────────────────────────────────────────────
        [ObservableProperty] bool showGridOverlay = false;

        // ─── Hex Panel ───────────────────────────────────────────────
        [ObservableProperty] ObservableCollection<string> hexCodes = new();
        [ObservableProperty] int currentPage = 1;
        [ObservableProperty] int totalPages = 1;
        [ObservableProperty] bool hasHexCodes = false;

        private List<string> _allHexCodes = new();
        private const int PageSize = 200;

        // ─── Lock sync: when locked, W change mirrors to H ────────────
        partial void OnCustomWidthChanged(string value)
        {
            if (IsLocked) CustomHeight = value;
        }

        partial void OnCustomHeightChanged(string value)
        {
            if (IsLocked) CustomWidth = value;
        }

        // ─── Preset ───────────────────────────────────────────────────
        [RelayCommand]
        void SetPreset(string size)
        {
            CustomWidth = size;
            CustomHeight = size;
        }

        // ─── Toggle Lock ──────────────────────────────────────────────
        [RelayCommand]
        void ToggleLock()
        {
            IsLocked = !IsLocked;
            if (IsLocked) CustomHeight = CustomWidth; // snap to square on lock
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
            Convert();
        }

        // ─── Convert ──────────────────────────────────────────────────
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

            // Build hex list
            _allHexCodes = ExtractHexCodes(_resultBitmap, w, h);
            TotalPages = (int)Math.Ceiling(_allHexCodes.Count / (double)PageSize);
            CurrentPage = 1;
            LoadPage(1);

            HasHexCodes = _allHexCodes.Count > 0;
            StatusText = $"Output: {w}×{h} grid → {_resultBitmap.Width}×{_resultBitmap.Height}px  |  {_allHexCodes.Count} pixels";
            IsBusy = false;
        }

        // ─── Pagination ───────────────────────────────────────────────
        [RelayCommand]
        void NextPage()
        {
            if (CurrentPage < TotalPages)
            {
                CurrentPage++;
                LoadPage(CurrentPage);
            }
        }

        [RelayCommand]
        void PrevPage()
        {
            if (CurrentPage > 1)
            {
                CurrentPage--;
                LoadPage(CurrentPage);
            }
        }

        private void LoadPage(int page)
        {
            int skip = (page - 1) * PageSize;
            var slice = _allHexCodes.Skip(skip).Take(PageSize);
            HexCodes = new ObservableCollection<string>(slice);
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

        // ─── Helpers ─────────────────────────────────────────────────
        private static ImageSource BitmapToImageSource(SKBitmap bitmap)
        {
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode();
            var bytes = data.ToArray();
            return ImageSource.FromStream(() => new MemoryStream(bytes));
        }

        private static List<string> ExtractHexCodes(SKBitmap result, int gridW, int gridH)
        {
            // Sample the centre of each pixel block
            int cellW = result.Width / gridW;
            int cellH = result.Height / gridH;

            var codes = new List<string>(gridW * gridH);

            for (int row = 0; row < gridH; row++)
            {
                for (int col = 0; col < gridW; col++)
                {
                    int px = col * cellW + cellW / 2;
                    int py = row * cellH + cellH / 2;

                    px = Math.Clamp(px, 0, result.Width - 1);
                    py = Math.Clamp(py, 0, result.Height - 1);

                    var c = result.GetPixel(px, py);
                    codes.Add($"#{c.Red:X2}{c.Green:X2}{c.Blue:X2}");
                }
            }

            return codes;
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