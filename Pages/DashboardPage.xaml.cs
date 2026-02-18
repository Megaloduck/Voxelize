using Voxelize.Models;
using Voxelize.PageModels;

namespace Voxelize.Pages;

public partial class DashboardPage : ContentPage
{
	public DashboardPage(DashboardPageModel pageModel)
	{
		InitializeComponent();
        BindingContext = pageModel;
    }
}