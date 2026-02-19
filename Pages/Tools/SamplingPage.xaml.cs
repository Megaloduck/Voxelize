using System.Globalization;
using Voxelize.PageModels.Tools;

namespace Voxelize.Pages.Tools;

// ?? / ?? icon based on lock state
public class LockIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && b ? "🔗" : "⛓️‍💥";
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

// Disables Height Entry when locked
public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && !b;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && !b;
}

// Disables Prev button on page 1
public class GreaterThanOneConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is int i && i > 1;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public partial class SamplingPage : ContentPage
{
    public SamplingPage()
    {
        InitializeComponent();
        BindingContext = new SamplingPageModel();
    }
}