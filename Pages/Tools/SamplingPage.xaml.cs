using Voxelize.PageModels.Tools;

namespace Voxelize.Pages.Tools;

public partial class SamplingPage : ContentPage
{
    public SamplingPage ()
    {
        InitializeComponent();
        BindingContext = new SamplingPageModel();
    }
}