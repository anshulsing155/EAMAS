using EAMAS.Core.Enums;
using EAMAS.Core.Models;
using EAMAS.Core.Services;
using System.Collections.ObjectModel;
using System.Windows.Threading;

namespace EAMAS.Desktop.ViewModels
{
    public class AlertItem
    {
        public string Id { get; set; } = string.Empty;
        public AlertType Type { get; set; }
        public string TypeLabel { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string TimeLabel { get; set; } = string.Empty;
        public bool IsRead { get; set; }
        public string TypeColor => Type switch
        {
            AlertType.LongIdle => "#F59E0B",
            AlertType.DistractingUsage => "#EF4444",
            AlertType.LowProductivity => "#F97316",
            AlertType.UnauthorizedApp => "#DC2626",
            AlertType.NoActivity => "#6B7280",
            _ => "#3B82F6"
        };
    }

    public class AlertsViewModel : BaseViewModel
    {
        private readonly AlertService _alertService;
        private readonly DispatcherTimer _refreshTimer;

        private ObservableCollection<AlertItem> _alerts = new();
        private AlertItem? _selectedAlert;
        private bool _isLoading;
        private bool _showUnreadOnly;
        private int _unreadCount;

        public ObservableCollection<AlertItem> Alerts { get => _alerts; set => Set(ref _alerts, value); }
        public AlertItem? SelectedAlert { get => _selectedAlert; set => Set(ref _selectedAlert, value); }
        public bool IsLoading { get => _isLoading; set => Set(ref _isLoading, value); }
        public int UnreadCount { get => _unreadCount; set => Set(ref _unreadCount, value); }

        public bool ShowUnreadOnly
        {
            get => _showUnreadOnly;
            set { Set(ref _showUnreadOnly, value); Load(); }
        }

        public bool IsSuperAdmin => App.CurrentUser?.Role == UserRole.SuperAdmin;
        public bool IsAdmin => App.CurrentUser?.Role is UserRole.Admin or UserRole.SuperAdmin;

        public RelayCommand LoadCommand { get; }
        public RelayCommand MarkReadCommand { get; }
        public RelayCommand MarkAllReadCommand { get; }

        public AlertsViewModel(AlertService alertService)
        {
            _alertService = alertService;

            LoadCommand = new RelayCommand(Load);
            MarkReadCommand = new RelayCommand(MarkSelected, () => SelectedAlert != null && !SelectedAlert.IsRead);
            MarkAllReadCommand = new RelayCommand(MarkAll);

            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            _refreshTimer.Tick += (_, _) => Load();
        }

        public void Initialize()
        {
            Load();
            _refreshTimer.Start();
        }

        public void Cleanup() => _refreshTimer.Stop();

        public void Load()
        {
            if (App.CurrentUser == null) return;
            IsLoading = true;

            Task.Run(() =>
            {
                List<Alert> raw;
                int unread;

                if (IsSuperAdmin)
                {
                    // SuperAdmin sees all alerts across all orgs
                    raw = _alertService.GetAllOrgs(limit: 200);
                    unread = _alertService.GetUnreadCountAllOrgs();
                }
                else
                {
                    var orgId = App.CurrentOrgId;
                    var userId = IsAdmin ? null : App.CurrentUser!.Id;
                    raw = ShowUnreadOnly
                        ? _alertService.GetUnread(orgId, userId)
                        : _alertService.GetAll(orgId, userId, limit: 200);
                    unread = _alertService.GetUnreadCount(orgId, userId);
                }

                var items = raw.Select(a => new AlertItem
                {
                    Id = a.Id,
                    Type = a.Type,
                    TypeLabel = FormatType(a.Type),
                    Message = a.Message,
                    TimeLabel = a.CreatedAt.ToLocalTime().ToString("MMM dd, HH:mm"),
                    IsRead = a.IsRead
                }).ToList();

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    Alerts = new ObservableCollection<AlertItem>(items);
                    UnreadCount = unread;
                    IsLoading = false;
                });
            });
        }

        private void MarkSelected()
        {
            if (SelectedAlert == null || SelectedAlert.IsRead) return;
            _alertService.MarkRead(SelectedAlert.Id);
            Load();
        }

        private void MarkAll()
        {
            if (IsSuperAdmin)
                _alertService.MarkAllReadAllOrgs();
            else
            {
                var userId = IsAdmin ? null : App.CurrentUser!.Id;
                _alertService.MarkAllRead(App.CurrentOrgId, userId);
            }
            Load();
        }

        private static string FormatType(AlertType type) => type switch
        {
            AlertType.LongIdle => "Long Idle",
            AlertType.DistractingUsage => "Distracting Usage",
            AlertType.LowProductivity => "Low Productivity",
            AlertType.UnauthorizedApp => "Unauthorized App",
            AlertType.NoActivity => "No Activity",
            _ => type.ToString()
        };
    }
}
