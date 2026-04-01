using EAMAS.Core.Models;
using EAMAS.Core.Services;
using EAMAS.Desktop.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace EAMAS.Desktop.ViewModels
{
    public class ScreenshotItem
    {
        public string Id { get; set; } = string.Empty;
        public string ThumbnailPath { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string TimeLabel { get; set; } = string.Empty;
        public string AppName { get; set; } = string.Empty;
        public string SizeLabel { get; set; } = string.Empty;
        public bool IsManual { get; set; }
    }

    public class ScreenshotsViewModel : BaseViewModel
    {
        private readonly ScreenshotService _screenshotService;
        private readonly MonitoringBackgroundService _monitoring;
        private readonly UserService _userService;

        private ObservableCollection<ScreenshotItem> _screenshots = new();
        private ScreenshotItem? _selectedScreenshot;
        private DateTime _selectedDate = DateTime.Today;
        private bool _isLoading;
        private List<User> _users = new();
        private User? _selectedUser;
        private int _totalCount;
        private string _storageLabel = "—";

        public ObservableCollection<ScreenshotItem> Screenshots { get => _screenshots; set => Set(ref _screenshots, value); }
        public ScreenshotItem? SelectedScreenshot { get => _selectedScreenshot; set => Set(ref _selectedScreenshot, value); }
        public DateTime SelectedDate { get => _selectedDate; set { Set(ref _selectedDate, value); Load(); } }
        public bool IsLoading { get => _isLoading; set => Set(ref _isLoading, value); }
        public List<User> Users { get => _users; set => Set(ref _users, value); }
        public User? SelectedUser { get => _selectedUser; set { Set(ref _selectedUser, value); Load(); } }
        public int TotalCount { get => _totalCount; set => Set(ref _totalCount, value); }
        public string StorageLabel { get => _storageLabel; set => Set(ref _storageLabel, value); }

        public bool IsAdmin => App.CurrentUser?.Role is UserRole.Admin or UserRole.SuperAdmin;
        public bool IsManager => App.CurrentUser?.Role is UserRole.Manager or UserRole.Admin or UserRole.SuperAdmin;

        public RelayCommand LoadCommand { get; }
        public RelayCommand TakeScreenshotCommand { get; }
        public RelayCommand OpenScreenshotCommand { get; }
        public RelayCommand DeleteScreenshotCommand { get; }
        public RelayCommand PreviousDayCommand { get; }
        public RelayCommand NextDayCommand { get; }
        public RelayCommand SelectCommand { get; }

        public ScreenshotsViewModel(
            ScreenshotService screenshotService,
            MonitoringBackgroundService monitoring,
            UserService userService)
        {
            _screenshotService = screenshotService;
            _monitoring = monitoring;
            _userService = userService;

            LoadCommand = new RelayCommand(Load);
            TakeScreenshotCommand = new RelayCommand(TakeScreenshot);
            OpenScreenshotCommand = new RelayCommand(OpenScreenshot);
            DeleteScreenshotCommand = new RelayCommand(DeleteScreenshot);
            PreviousDayCommand = new RelayCommand(() => SelectedDate = SelectedDate.AddDays(-1));
            NextDayCommand = new RelayCommand(() => SelectedDate = SelectedDate.AddDays(1),
                () => SelectedDate.Date < DateTime.Today);
            SelectCommand = new RelayCommand(obj => SelectedScreenshot = obj as ScreenshotItem);
        }

        public void Initialize()
        {
            if (IsManager)
            {
                Users = _userService.GetAll(App.CurrentOrgId)
                    .Where(u => u.IsActive).ToList();
                SelectedUser = Users.FirstOrDefault(u => u.Id == App.CurrentUser!.Id);
            }
            Load();
        }

        public void Load()
        {
            var orgId = App.CurrentOrgId;
            var userId = SelectedUser?.Id ?? App.CurrentUser!.Id;
            IsLoading = true;

            Task.Run(() =>
            {
                var from = SelectedDate.Date;
                var to = from.AddDays(1);
                var records = _screenshotService.GetScreenshots(orgId, userId, from, to);
                var storage = _screenshotService.GetTotalStorageBytes(orgId, userId);

                var items = records.Select(r => new ScreenshotItem
                {
                    Id = r.Id,
                    ThumbnailPath = System.IO.File.Exists(r.ThumbnailPath)
                        ? r.ThumbnailPath : r.FilePath,
                    FilePath = r.FilePath,
                    TimeLabel = r.TakenAt.ToLocalTime().ToString("HH:mm:ss"),
                    AppName = r.ApplicationName,
                    SizeLabel = FormatSize(r.FileSizeBytes),
                    IsManual = r.IsManual
                }).ToList();

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    Screenshots = new ObservableCollection<ScreenshotItem>(items);
                    TotalCount = items.Count;
                    StorageLabel = FormatSize(storage);
                    IsLoading = false;
                });
            });
        }

        private void TakeScreenshot()
        {
            _monitoring.TriggerScreenshot();
            Task.Delay(1500).ContinueWith(_ =>
                System.Windows.Application.Current.Dispatcher.Invoke(Load));
        }

        private void OpenScreenshot()
        {
            if (SelectedScreenshot == null || !System.IO.File.Exists(SelectedScreenshot.FilePath)) return;
            Process.Start(new ProcessStartInfo(SelectedScreenshot.FilePath) { UseShellExecute = true });
        }

        private void DeleteScreenshot()
        {
            if (SelectedScreenshot == null) return;
            var result = System.Windows.MessageBox.Show("Delete this screenshot?", "Confirm Delete",
                System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);
            if (result != System.Windows.MessageBoxResult.Yes) return;
            _screenshotService.Delete(SelectedScreenshot.Id);
            Load();
        }

        private static string FormatSize(long bytes)
        {
            if (bytes >= 1_073_741_824) return $"{bytes / 1_073_741_824.0:F1} GB";
            if (bytes >= 1_048_576) return $"{bytes / 1_048_576.0:F1} MB";
            if (bytes >= 1_024) return $"{bytes / 1024.0:F1} KB";
            return $"{bytes} B";
        }
    }
}
