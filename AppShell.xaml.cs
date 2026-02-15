using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using Font = Microsoft.Maui.Font;
using CommunityToolkit.Mvvm.Input;
using Voxelize.Models;
using Voxelize.Pages;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Voxelize
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();


            // Start the app at DashboardPage
            GoToAsync("//DashboardPage");

        }
    }
}
