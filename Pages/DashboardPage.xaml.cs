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
        if (page is ContentPage contentPage)
        {
            contentPage.Parent = null; // detach from any previous parent
            ContentArea.Content = new ContentView
            {
                Content = contentPage.Content,
                BindingContext = contentPage.BindingContext  // ✅ carry the correct model
            };
        }
    }
}