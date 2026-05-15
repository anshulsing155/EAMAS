using EAMAS.Core.Enums;
using EAMAS.Core.Models;
using EAMAS.Core.Services;
using System.Collections.ObjectModel;

namespace EAMAS.Desktop.ViewModels
{
    public class ActivityLogItem
    {
        public string Time { get; set; } = string.Empty;
        public string AppName { get; set; } = string.Empty;
        public string WindowTitle { get; set; } = string.Empty;
        public string Duration { get; set; } = string.Empty;
        public ActivityCategory Category { get; set; }
        public bool IsIdle { get; set; }
        public string UserLabel { get; set; } = string.Empty;
        public string CategoryLabel => IsIdle ? "Idle" : Category.ToString();
    }

    public class ActivityLogsViewModel : BaseViewModel
    {
        private readonly ActivityMonitorService _activityService;
        private readonly UserService _userService;
        private readonly OrganizationService _orgService;

        private ObservableCollection<ActivityLogItem> _logs = new();
        private DateTime _selectedDate = DateTime.Today;
        private List<User> _users = new();
        private User? _selectedUser;
        private List<Organization> _organizations = new();
        private Organization? _selectedOrg;
        private bool _isLoading;
        private string _filterText = string.Empty;
        private int _totalRecords;
        private List<ActivityLogItem> _allLogs = new();

        public ObservableCollection<ActivityLogItem> Logs { get => _logs; set => Set(ref _logs, value); }
        public DateTime SelectedDate { get => _selectedDate; set { Set(ref _selectedDate, value); Load(); } }
        public List<User> Users { get => _users; set => Set(ref _users, value); }
        public User? SelectedUser { get => _selectedUser; set { Set(ref _selectedUser, value); Load(); } }
        public List<Organization> Organizations { get => _organizations; set => Set(ref _organizations, value); }

        public Organization? SelectedOrg
        {
            get => _selectedOrg;
            set
            {
                Set(ref _selectedOrg, value);
                // Reload users for the selected org when SuperAdmin changes org filter
                if (IsSuperAdmin && value != null)
                    LoadUsersForOrg(value.Id);
                Load();
            }
        }

        public bool IsLoading { get => _isLoading; set => Set(ref _isLoading, value); }
        public string FilterText { get => _filterText; set { Set(ref _filterText, value); ApplyFilter(); } }
        public int TotalRecords { get => _totalRecords; set => Set(ref _totalRecords, value); }

        public bool IsSuperAdmin => App.CurrentUser?.Role == UserRole.SuperAdmin;
        public bool IsAdmin => App.CurrentUser?.Role is UserRole.Admin or UserRole.SuperAdmin;
        public bool IsManager => App.CurrentUser?.Role is UserRole.Manager or UserRole.Admin or UserRole.SuperAdmin;
        /// <summary>Employees can only see their own data; SuperAdmin uses the org selector instead.</summary>
        public bool ShowUserSelector => IsManager && !IsSuperAdmin;
        public bool ShowOrgSelector => IsSuperAdmin;

        public RelayCommand LoadCommand { get; }
        public RelayCommand PreviousDayCommand { get; }
        public RelayCommand NextDayCommand { get; }
        public RelayCommand TodayCommand { get; }

        public ActivityLogsViewModel(
            ActivityMonitorService activityService,
            UserService userService,
            OrganizationService orgService)
        {
            _activityService = activityService;
            _userService = userService;
            _orgService = orgService;

            LoadCommand = new RelayCommand(Load);
            PreviousDayCommand = new RelayCommand(() => SelectedDate = SelectedDate.AddDays(-1));
            NextDayCommand = new RelayCommand(() => SelectedDate = SelectedDate.AddDays(1),
                () => SelectedDate.Date < DateTime.Today);
            TodayCommand = new RelayCommand(() => SelectedDate = DateTime.Today);
        }

        public void Initialize()
        {
            if (IsSuperAdmin)
            {
                Organizations = _orgService.GetAll().Where(o => o.IsActive).ToList();
                _selectedOrg = Organizations.FirstOrDefault();
                OnPropertyChanged(nameof(SelectedOrg));
                if (_selectedOrg != null) LoadUsersForOrg(_selectedOrg.Id);
            }
            else if (IsManager)
            {
                Users = _userService.GetAll(App.CurrentOrgId).Where(u => u.IsActive).ToList();
                _selectedUser = Users.FirstOrDefault(u => u.Id == App.CurrentUser!.Id);
                OnPropertyChanged(nameof(SelectedUser));
            }
            Load();
        }

        private void LoadUsersForOrg(string orgId)
        {
            Users = _userService.GetAll(orgId).Where(u => u.IsActive).ToList();
            _selectedUser = null;
            OnPropertyChanged(nameof(SelectedUser));
        }

        public void Load()
        {
            IsLoading = true;

            // Determine effective orgId and userId for the query
            string? orgId;
            string? userId;

            if (IsSuperAdmin)
            {
                orgId = SelectedOrg?.Id; // null = all orgs
                userId = SelectedUser?.Id; // null = all users in that org
            }
            else
            {
                orgId = App.CurrentOrgId;
                userId = SelectedUser?.Id ?? App.CurrentUser!.Id;
            }

            Task.Run(() =>
            {
                var from = SelectedDate.Date;
                var to = from.AddDays(1);
                var raw = _activityService.GetActivity(orgId, userId, from, to);

                var items = raw.Select(l => new ActivityLogItem
                {
                    Time = l.StartTime.ToLocalTime().ToString("HH:mm:ss"),
                    AppName = l.ApplicationName,
                    WindowTitle = l.WindowTitle.Length > 80
                        ? l.WindowTitle[..80] + "…" : l.WindowTitle,
                    Duration = FormatDuration(l.Duration),
                    Category = l.Category,
                    IsIdle = l.IsIdle,
                    UserLabel = l.UserId
                }).ToList();

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    _allLogs = items;
                    TotalRecords = items.Count;
                    ApplyFilter();
                    IsLoading = false;
                });
            });
        }

        private void ApplyFilter()
        {
            var filtered = string.IsNullOrWhiteSpace(FilterText)
                ? _allLogs
                : _allLogs.Where(l =>
                    l.AppName.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ||
                    l.WindowTitle.Contains(FilterText, StringComparison.OrdinalIgnoreCase)).ToList();

            Logs = new ObservableCollection<ActivityLogItem>(filtered);
        }

        private static string FormatDuration(TimeSpan ts)
        {
            if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours}h {ts.Minutes:D2}m";
            if (ts.TotalMinutes >= 1) return $"{ts.Minutes}m {ts.Seconds:D2}s";
            return $"{ts.Seconds}s";
        }
    }
}
