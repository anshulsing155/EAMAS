using EAMAS.Desktop.Converters;
using EAMAS.Desktop.ViewModels;
using System;
using System.Windows;

namespace EAMAS.Desktop
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm;

        public MainWindow(MainViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            DataContext = vm;
            vm.PageChanged += UpdateNavHighlight;
        }

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
            _vm.Initialize(MainContent);
        }

        private void UpdateNavHighlight(Services.AppPage page)
        {
            var navStyle = (Style)FindResource("NavButton");
            var activeStyle = (Style)FindResource("NavButtonActive");

            BtnDashboard.Style = page == Services.AppPage.Dashboard ? activeStyle : navStyle;
            BtnActivity.Style = page == Services.AppPage.ActivityLogs ? activeStyle : navStyle;
            BtnScreenshots.Style = page == Services.AppPage.Screenshots ? activeStyle : navStyle;
            BtnReports.Style = page == Services.AppPage.Reports ? activeStyle : navStyle;
            BtnEmployees.Style = page == Services.AppPage.Employees ? activeStyle : navStyle;
            BtnSettings.Style = page == Services.AppPage.Settings ? activeStyle : navStyle;
        }
    }
}
