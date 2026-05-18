using System.Windows;
using System.Windows.Media.Animation;
using SWC = System.Windows.Controls;

namespace EAMAS.Desktop.Services
{
    public enum AppPage
    {
        Dashboard,
        ActivityLogs,
        Screenshots,
        Reports,
        Tasks,
        Projects,
        SprintPlanner,
        Employees,
        Alerts,
        Organizations,
        Settings
    }

    public class NavigationService
    {
        private SWC.ContentControl? _frame;
        private readonly IServiceProvider _services;

        public event Action<AppPage>? PageChanged;
        public AppPage CurrentPage { get; private set; }

        public NavigationService(IServiceProvider services)
        {
            _services = services;
        }

        public void Attach(SWC.ContentControl frame)
        {
            _frame = frame;
        }

        public void Navigate(AppPage page)
        {
            if (_frame == null) return;

            var view = GetView(page);

            if (_frame.Content == null)
            {
                // First navigation — no old content to fade out
                _frame.Content = view;
                _frame.BeginAnimation(UIElement.OpacityProperty,
                    MakeFadeIn());
            }
            else
            {
                var fadeOut = new DoubleAnimation(1d, 0d,
                    new Duration(TimeSpan.FromMilliseconds(120)))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                };

                fadeOut.Completed += (_, _) =>
                {
                    _frame.Content = view;
                    _frame.BeginAnimation(UIElement.OpacityProperty, MakeFadeIn());
                };

                _frame.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            }

            CurrentPage = page;
            PageChanged?.Invoke(page);
        }

        private static DoubleAnimation MakeFadeIn() =>
            new DoubleAnimation(0d, 1d, new Duration(TimeSpan.FromMilliseconds(220)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

        private SWC.UserControl GetView(AppPage page) => page switch
        {
            AppPage.Dashboard     => (SWC.UserControl)GetService<Views.DashboardView>(),
            AppPage.ActivityLogs  => (SWC.UserControl)GetService<Views.ActivityLogsView>(),
            AppPage.Screenshots   => (SWC.UserControl)GetService<Views.ScreenshotsView>(),
            AppPage.Reports       => (SWC.UserControl)GetService<Views.ReportsView>(),
            AppPage.Tasks         => (SWC.UserControl)GetService<Views.TasksView>(),
            AppPage.Projects      => (SWC.UserControl)GetService<Views.ProjectsView>(),
            AppPage.SprintPlanner => (SWC.UserControl)GetService<Views.SprintPlannerView>(),
            AppPage.Employees     => (SWC.UserControl)GetService<Views.EmployeesView>(),
            AppPage.Alerts        => (SWC.UserControl)GetService<Views.AlertsView>(),
            AppPage.Organizations => (SWC.UserControl)GetService<Views.OrganizationsView>(),
            AppPage.Settings      => (SWC.UserControl)GetService<Views.SettingsView>(),
            _ => throw new ArgumentOutOfRangeException(nameof(page))
        };

        private T GetService<T>() where T : notnull =>
            (T)_services.GetService(typeof(T))!;
    }
}
