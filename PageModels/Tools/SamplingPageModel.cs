using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Text;
using Voxelize.Services;
using Voxelize.Models;

namespace Voxelize.PageModels.Tools
{
    public partial class SamplingPageModel : ObservableObject
    {
        private readonly PixelArtService _service = new();

        [ObservableProperty]
        ImageSource previewImage;

        private SKBitmap _originalBitmap;

        [ObservableProperty]
        int pixelSize = 8;

        [ObservableProperty]
        int colorDepth = 16;

        [RelayCommand]
        public async Task LoadImage()
        {
            var file = await FilePicker.Default.PickAsync();
            if (file == null) return;

            using var stream = await file.OpenReadAsync();
            _originalBitmap = SKBitmap.Decode(stream);

            ApplyPixelArt();
        }

        [RelayCommand]
        void ApplyPixelArt()
        {
            if (_originalBitmap == null) return;

            var result = _service.ConvertToPixelArt(_originalBitmap, PixelSize, ColorDepth);

            using var image = SKImage.FromBitmap(result);
            using var data = image.Encode();

            var bytes = data.ToArray(); // ✅ Copy bytes before disposal
            PreviewImage = ImageSource.FromStream(() => new MemoryStream(bytes));
        }
    }
 
}
