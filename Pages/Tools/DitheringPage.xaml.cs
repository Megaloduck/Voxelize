using System.Globalization;
using Voxelize.PageModels.Tools;

namespace Voxelize.Pages.Tools;

public partial class DitheringPage : ContentPage
{
    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b && !b;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b && !b;
    }

    public class LockIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b && b ? "🔒" : "🔓";
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
    public DitheringPage()
    {
        InitializeComponent();
        BindingContext = new DitheringPageModel();
    }
}