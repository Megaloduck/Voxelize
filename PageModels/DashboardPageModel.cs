using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Input;
using Voxelize.PageModels.Tools;
using Voxelize.Pages.System;
using Voxelize.Pages.Tools;
using Voxelize.Services;

namespace Voxelize.PageModels
{
    public class DashboardPageModel : BasePageModel
    {
        private readonly ThemeService _themeService;

        // Used by SidebarPage.xaml.cs to navigate content
        public Action<Page> NavigateAction { get; set; }

        public DashboardPageModel()
        {
            _themeService = ThemeService.Instance;

            BuildNavigationCommands();
            HookThemeUpdates();
        }

        // -------------------------------
        // THEME BINDING
        // -------------------------------
        public bool IsDarkMode
        {
            get => _themeService.IsDarkMode;
            set
            {
                if (_themeService.IsDarkMode != value)
                {
                    _themeService.IsDarkMode = value;   // ThemeService applies the actual theme
                    OnPropertyChanged();                // Update UI binding
                }
            }
        }

        private void HookThemeUpdates()
        {
            _themeService.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ThemeService.IsDarkMode))
                    OnPropertyChanged(nameof(IsDarkMode));
            };
        }

        // -------------------------------
        // COMMANDS
        // -------------------------------
        public ICommand NavigateToDashboardCommand { get; private set; }

        public ICommand NavigateToSamplingCommand { get; private set; }
        public ICommand NavigateToPosterizationCommand { get; private set; }
        public ICommand NavigateToDitheringCommand { get; private set; } 

        public ICommand NavigateToSettingsCommand { get; private set; }

        // -------------------------------
        // BUILD COMMANDS
        // -------------------------------
        private void BuildNavigationCommands()
        {
            // Tools
            NavigateToSamplingCommand = new Command(() => NavigateAction?.Invoke(new SamplingPage()));
            NavigateToPosterizationCommand = new Command(() => NavigateAction?.Invoke(new PosterizationPage()));
            NavigateToDitheringCommand = new Command(() => NavigateAction?.Invoke(new DitheringPage()));
           
            // System
            NavigateToSettingsCommand = new Command(() => NavigateAction?.Invoke(new SettingsPage()));
        }
    }
}
