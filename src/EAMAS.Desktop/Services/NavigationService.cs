using System.Windows.Controls;

namespace EAMAS.Desktop.Services
{
    public enum AppPage
    {
        Dashboard,
        ActivityLogs,
        Screenshots,
        Reports,
        Employees,
        Settings
    }

    public class NavigationService
    {
        private ContentControl? _frame;
        private readonly IServiceProvider _services;

        public event Action<AppPage>? PageChanged;
        public AppPage CurrentPage { get; private set; }

        public NavigationService(IServiceProvider services)
        {
            _services = services;
        }

        public void Attach(ContentControl frame)
        {
            _frame = frame;
        }

        public void Navigate(AppPage page)
        {
            if (_frame == null) return;

            var view = page switch
            {
                AppPage.Dashboard => (System.Windows.Controls.UserControl)GetService<Views.DashboardView>(),
                AppPage.ActivityLogs => (System.Windows.Controls.UserControl)GetService<Views.ActivityLogsView>(),
                AppPage.Screenshots => (System.Windows.Controls.UserControl)GetService<Views.ScreenshotsView>(),
                AppPage.Reports => (System.Windows.Controls.UserControl)GetService<Views.ReportsView>(),
                AppPage.Employees => (System.Windows.Controls.UserControl)GetService<Views.EmployeesView>(),
                AppPage.Settings => (System.Windows.Controls.UserControl)GetService<Views.SettingsView>(),
                _ => throw new ArgumentOutOfRangeException()
            };

            _frame.Content = view;
            CurrentPage = page;
            PageChanged?.Invoke(page);
        }

        private T GetService<T>() where T : notnull =>
            (T)_services.GetService(typeof(T))!;
    }
}
