using Voxelize.Models;
using Voxelize.PageModels;

namespace Voxelize.Pages
{
    public partial class MainPage : ContentPage
    {
        public MainPage(MainPageModel model)
        {
            InitializeComponent();
            BindingContext = model;
        }
    }
}