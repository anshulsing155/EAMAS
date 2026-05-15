using EAMAS.Core.Models;
using EAMAS.Core.Services;
using EAMAS.Desktop.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;
using System.Windows.Threading;

namespace EAMAS.Desktop.ViewModels
{
    public class MainViewModel : BaseViewModel
    {
        private readonly NavigationService _navigationService;
        private readonly MonitoringBackgroundService _monitoring;
        private readonly AlertService _alertService;
        private readonly DispatcherTimer _alertTimer;

        private AppPage _currentPage;
        private int _unreadAlerts;
        private string _currentActivity = "Monitoring...";
        private bool _sidebarExpanded = true;

        public AppPage CurrentPage
        {
            get => _currentPage;
            set => Set(ref _currentPage, value);
        }

        public int UnreadAlerts
        {
            get => _unreadAlerts;
            set => Set(ref _unreadAlerts, value);
        }

        public string CurrentActivity
        {
            get => _currentActivity;
            set => Set(ref _currentActivity, value);
        }

        public bool SidebarExpanded
        {
            get => _sidebarExpanded;
            set => Set(ref _sidebarExpanded, value);
        }

        public User? CurrentUser => App.CurrentUser;

        public bool IsSuperAdmin => CurrentUser?.Role == UserRole.SuperAdmin;
        public bool IsAdmin    => CurrentUser?.Role is UserRole.Admin or UserRole.SuperAdmin;
        public bool IsManager  => CurrentUser?.Role is UserRole.Manager or UserRole.Admin or UserRole.SuperAdmin;
        public bool IsEmployee => CurrentUser?.Role == UserRole.Employee;

        // Employees nav shown only for org-level Admins.
        // SuperAdmin manages users through the Organisations page instead.
        public bool ShowEmployeesNav => CurrentUser?.Role == UserRole.Admin;

        /// <summary>True when monitoring is active (i.e., the user is not SuperAdmin).</summary>
        public bool IsMonitoring => !IsSuperAdmin;

        public string OrgDisplayName =>
            App.CurrentOrganization?.Name ?? "System Administration";

        public event Action<AppPage>? PageChanged;

        public RelayCommand NavigateDashboardCommand { get; }
        public RelayCommand NavigateActivityCommand { get; }
        public RelayCommand NavigateScreenshotsCommand { get; }
        public RelayCommand NavigateReportsCommand { get; }
        public RelayCommand NavigateEmployeesCommand { get; }
        public RelayCommand NavigateAlertsCommand { get; }
        public RelayCommand NavigateOrganizationsCommand { get; }
        public RelayCommand NavigateSettingsCommand { get; }
        public RelayCommand LogoutCommand { get; }

        public MainViewModel(
            NavigationService navigationService,
            MonitoringBackgroundService monitoring,
            AlertService alertService)
        {
            _navigationService = navigationService;
            _monitoring = monitoring;
            _alertService = alertService;

            NavigateDashboardCommand = new RelayCommand(() => Navigate(AppPage.Dashboard));
            NavigateActivityCommand = new RelayCommand(() => Navigate(AppPage.ActivityLogs));
            NavigateScreenshotsCommand = new RelayCommand(() => Navigate(AppPage.Screenshots));
            NavigateReportsCommand = new RelayCommand(() => Navigate(AppPage.Reports));
            NavigateEmployeesCommand = new RelayCommand(() => Navigate(AppPage.Employees));
            NavigateAlertsCommand = new RelayCommand(() => Navigate(AppPage.Alerts));
            NavigateOrganizationsCommand = new RelayCommand(() => Navigate(AppPage.Organizations));
            NavigateSettingsCommand = new RelayCommand(() => Navigate(AppPage.Settings));
            LogoutCommand = new RelayCommand(Logout);

            _navigationService.PageChanged += p =>
            {
                CurrentPage = p;
                PageChanged?.Invoke(p);
            };

            _monitoring.ActivityChanged += (proc, title) =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    CurrentActivity = $"{proc} — {TruncateTitle(title)}");
            };

            _alertTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            _alertTimer.Tick += (_, _) => RefreshAlerts();
        }

        public void Initialize(ContentControl frame)
        {
            _navigationService.Attach(frame);

            // SuperAdmin is NOT monitored — they are the viewer, not the subject.
            if (CurrentUser != null && !IsSuperAdmin)
                _monitoring.Start(App.CurrentOrgId, CurrentUser.Id);

            Navigate(AppPage.Dashboard);
            RefreshAlerts();
            _alertTimer.Start();
        }

        public void RefreshAlerts()
        {
            if (CurrentUser == null) return;
            try
            {
                if (IsSuperAdmin)
                {
                    UnreadAlerts = _alertService.GetUnreadCountAllOrgs();
                }
                else
                {
                    string? userId = IsAdmin ? null : CurrentUser.Id;
                    UnreadAlerts = _alertService.GetUnreadCount(App.CurrentOrgId, userId);
                }
            }
            catch { /* don't crash on background polling failure */ }
        }

        private void Navigate(AppPage page)
        {
            _navigationService.Navigate(page);
            CurrentPage = page;
        }

        private void Logout()
        {
            _alertTimer.Stop();
            _monitoring.Stop();

            App.TryCloseCurrentSession();

            App.CurrentUser = null;
            App.CurrentOrganization = null;

            // Hide every MainWindow instance before the login window appears
            foreach (var w in System.Windows.Application.Current.Windows
                                   .OfType<MainWindow>().ToList())
                w.Hide();

            var login = App.Services.GetRequiredService<Views.LoginWindow>();
            login.Show();
            login.Activate();
        }

        private static string TruncateTitle(string title, int max = 55)
            => title.Length > max ? title[..max] + "…" : title;
    }
}
