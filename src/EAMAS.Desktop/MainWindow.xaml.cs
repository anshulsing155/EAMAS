using EAMAS.Desktop.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media.Animation;

namespace EAMAS.Desktop
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm;
        private bool _sidebarCollapsed;

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

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!App.IsExiting)
            {
                e.Cancel = true;
                Hide();
            }
            else
            {
                base.OnClosing(e);
            }
        }

        // ── Sidebar collapse / expand ────────────────────────────────

        private void BtnToggleSidebar_Click(object sender, RoutedEventArgs e)
        {
            _sidebarCollapsed = !_sidebarCollapsed;
            double target = _sidebarCollapsed ? 54 : 220;

            var anim = new DoubleAnimation(target,
                new Duration(TimeSpan.FromMilliseconds(220)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };

            SidebarBorder.BeginAnimation(FrameworkElement.WidthProperty, anim);
            _vm.SidebarExpanded = !_sidebarCollapsed;
        }

        // ── Nav highlight ────────────────────────────────────────────

        private void UpdateNavHighlight(Services.AppPage page)
        {
            var nav    = (Style)FindResource("NavButton");
            var active = (Style)FindResource("NavButtonActive");

            BtnDashboard.Style    = page == Services.AppPage.Dashboard     ? active : nav;
            BtnActivity.Style     = page == Services.AppPage.ActivityLogs  ? active : nav;
            BtnScreenshots.Style  = page == Services.AppPage.Screenshots   ? active : nav;
            BtnReports.Style      = page == Services.AppPage.Reports       ? active : nav;
            BtnEmployees.Style    = page == Services.AppPage.Employees     ? active : nav;
            BtnAlerts.Style       = page == Services.AppPage.Alerts        ? active : nav;
            BtnOrganizations.Style = page == Services.AppPage.Organizations ? active : nav;
            BtnSettings.Style     = page == Services.AppPage.Settings      ? active : nav;
        }
    }
}
