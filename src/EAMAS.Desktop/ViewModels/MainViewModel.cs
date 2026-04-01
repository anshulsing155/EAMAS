using EAMAS.Core.Models;
using EAMAS.Core.Services;
using EAMAS.Desktop.Services;
using System.Windows.Controls;

namespace EAMAS.Desktop.ViewModels
{
    public class MainViewModel : BaseViewModel
    {
        private readonly NavigationService _navigationService;
        private readonly MonitoringBackgroundService _monitoring;
        private readonly AlertService _alertService;
        private AppPage _currentPage;
        private int _unreadAlerts;
        private string _currentActivity = "Monitoring...";

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

        public User? CurrentUser => App.CurrentUser;

        public bool IsAdmin => CurrentUser?.Role is UserRole.Admin or UserRole.SuperAdmin;
        public bool IsManager => CurrentUser?.Role is UserRole.Manager or UserRole.Admin or UserRole.SuperAdmin;

        /// <summary>Organisation display name shown in the sidebar header.</summary>
        public string OrgDisplayName =>
            App.CurrentOrganization?.Name ?? "System Administration";

        public event Action<AppPage>? PageChanged;

        public RelayCommand NavigateDashboardCommand { get; }
        public RelayCommand NavigateActivityCommand { get; }
        public RelayCommand NavigateScreenshotsCommand { get; }
        public RelayCommand NavigateReportsCommand { get; }
        public RelayCommand NavigateEmployeesCommand { get; }
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
        }

        public void Initialize(ContentControl frame)
        {
            _navigationService.Attach(frame);

            if (CurrentUser != null)
                _monitoring.Start(App.CurrentOrgId, CurrentUser.Id);

            Navigate(AppPage.Dashboard);
            RefreshAlerts();
        }

        public void RefreshAlerts()
        {
            if (CurrentUser == null) return;
            // Admins/SuperAdmin see all org alerts; others see their own
            var userId = IsAdmin ? null : CurrentUser.Id;
            UnreadAlerts = _alertService.GetUnreadCount(App.CurrentOrgId, userId);
        }

        private void Navigate(AppPage page)
        {
            _navigationService.Navigate(page);
            CurrentPage = page;
        }

        private void Logout()
        {
            _monitoring.Stop();
            App.CurrentUser = null;
            App.CurrentOrganization = null;

            var login = App.Services.GetService(typeof(Views.LoginWindow)) as Views.LoginWindow;
            login?.Show();

            System.Windows.Application.Current.Windows
                .OfType<EAMAS.Desktop.MainWindow>().FirstOrDefault()?.Close();
        }

        private static string TruncateTitle(string title, int max = 60)
            => title.Length > max ? title[..max] + "…" : title;
    }
}
