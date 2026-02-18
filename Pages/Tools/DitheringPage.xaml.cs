using Voxelize.Models;
using Voxelize.PageModels.Tools;

namespace Voxelize.Pages.Tools;

public partial class DitheringPage : ContentPage
{
	public DitheringPage(MainPageModel pageModel)
	{
		InitializeComponent();
        BindingContext = pageModel;
    }
}