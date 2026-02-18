using Voxelize.Models;
using Voxelize.PageModels;

namespace Voxelize.Pages;

public partial class DashboardPage : ContentPage
{
    private readonly DashboardPageModel _pageModel;
    public DashboardPage()
	{
		InitializeComponent();

        _pageModel = new DashboardPageModel();
        _pageModel.NavigateAction = NavigateToPage;
        BindingContext = _pageModel;

        // Load initial page
        _pageModel.NavigateToSamplingCommand.Execute(null);

    }

    private void NavigateToPage(Page page)
    {
        // Cast to ContentPage and extract the Content
        if (page is ContentPage contentPage)
        {
            ContentArea.Content = contentPage.Content;
        }
    }
}