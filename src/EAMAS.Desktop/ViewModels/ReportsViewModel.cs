using EAMAS.Core.Enums;
using EAMAS.Core.Models;
using EAMAS.Core.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;

namespace EAMAS.Desktop.ViewModels
{
    public class DailySummaryRow
    {
        public string Date { get; set; } = string.Empty;
        public string ActiveTime { get; set; } = string.Empty;
        public string IdleTime { get; set; } = string.Empty;
        public string ProductiveTime { get; set; } = string.Empty;
        public string DistractingTime { get; set; } = string.Empty;
        public int Score { get; set; }
        public int Screenshots { get; set; }
    }

    public class ReportsViewModel : BaseViewModel
    {
        private readonly ReportService _reportService;
        private readonly AnalyticsService _analyticsService;
        private readonly UserService _userService;

        private int _reportTypeIndex;
        private ObservableCollection<DailySummaryRow> _rows = new();
        private List<BarChartItem> _trendChart = new();
        private List<BarChartItem> _topAppsChart = new();
        private bool _isLoading;
        private string _summaryLabel = string.Empty;
        private int _avgScore;
        private string _totalActiveTime = "—";
        private List<User> _users = new();
        private User? _selectedUser;
        private DateTime _customFrom = DateTime.Today.AddDays(-7);
        private DateTime _customTo = DateTime.Today;

        public int ReportTypeIndex { get => _reportTypeIndex; set { Set(ref _reportTypeIndex, value); Generate(); } }
        public ObservableCollection<DailySummaryRow> Rows { get => _rows; set => Set(ref _rows, value); }
        public List<BarChartItem> TrendChart { get => _trendChart; set => Set(ref _trendChart, value); }
        public List<BarChartItem> TopAppsChart { get => _topAppsChart; set => Set(ref _topAppsChart, value); }
        public bool IsLoading { get => _isLoading; set => Set(ref _isLoading, value); }
        public string SummaryLabel { get => _summaryLabel; set => Set(ref _summaryLabel, value); }
        public int AvgScore { get => _avgScore; set => Set(ref _avgScore, value); }
        public string TotalActiveTime { get => _totalActiveTime; set => Set(ref _totalActiveTime, value); }
        public List<User> Users { get => _users; set => Set(ref _users, value); }
        public User? SelectedUser { get => _selectedUser; set { Set(ref _selectedUser, value); Generate(); } }
        public DateTime CustomFrom { get => _customFrom; set => Set(ref _customFrom, value); }
        public DateTime CustomTo { get => _customTo; set => Set(ref _customTo, value); }

        public bool IsAdmin => App.CurrentUser?.Role is UserRole.Admin or UserRole.SuperAdmin;
        public bool IsManager => App.CurrentUser?.Role is UserRole.Manager or UserRole.Admin or UserRole.SuperAdmin;

        public RelayCommand GenerateCommand { get; }
        public RelayCommand ExportCsvCommand { get; }

        public ReportsViewModel(ReportService reportService, AnalyticsService analyticsService,
            UserService userService)
        {
            _reportService = reportService;
            _analyticsService = analyticsService;
            _userService = userService;
            GenerateCommand = new RelayCommand(Generate);
            ExportCsvCommand = new RelayCommand(ExportCsv);
        }

        public void Initialize()
        {
            if (IsManager)
            {
                Users = _userService.GetAll(App.CurrentOrgId)
                    .Where(u => u.IsActive).ToList();
                SelectedUser = Users.FirstOrDefault(u => u.Id == App.CurrentUser!.Id);
            }
            Generate();
        }

        public void Generate()
        {
            var orgId = App.CurrentOrgId;
            var userId = SelectedUser?.Id ?? App.CurrentUser!.Id;
            var (from, to) = GetDateRange();
            IsLoading = true;

            Task.Run(() =>
            {
                var summaries = _reportService.GetRangeSummaries(orgId, userId, from, to);
                var topApps = _reportService.GetTopApplications(orgId, userId, from, to, 8);
                var total = summaries.Aggregate(TimeSpan.Zero, (a, s) => a + s.ActiveTime);
                var avg = summaries.Where(s => s.ActiveTime.TotalMinutes >= 10).ToList();
                var avgScore = avg.Any() ? (int)avg.Average(s => s.ProductivityScore) : 0;

                var rows = summaries.Select(s => new DailySummaryRow
                {
                    Date = s.Date.ToString("ddd, MMM d"),
                    ActiveTime = FormatTime(s.ActiveTime),
                    IdleTime = FormatTime(s.IdleTime),
                    ProductiveTime = FormatTime(s.ProductiveTime),
                    DistractingTime = FormatTime(s.DistractingTime),
                    Score = s.ProductivityScore,
                    Screenshots = s.ScreenshotCount
                }).ToList();

                var maxActive = summaries.Any() ? summaries.Max(s => s.ActiveTime.TotalHours) : 1;
                if (maxActive < 0.1) maxActive = 1;
                var trendItems = summaries.Select(s => new BarChartItem
                {
                    Label = s.Date.ToString("M/d"),
                    Value = s.ProductivityScore,
                    Color = s.ProductivityScore >= 70 ? "#16A34A"
                          : s.ProductivityScore >= 40 ? "#D97706" : "#EF4444",
                    ValueLabel = $"{s.ProductivityScore}%",
                    BarHeightPercent = s.ProductivityScore
                }).ToList();

                var maxApp = topApps.Any() ? topApps.Max(a => a.Value.TotalMinutes) : 1;
                if (maxApp < 1) maxApp = 1;
                var appItems = topApps.Select(a => new BarChartItem
                {
                    Label = TruncateApp(a.Key),
                    Value = a.Value.TotalMinutes,
                    Color = "#2563EB",
                    ValueLabel = FormatTime(a.Value),
                    BarHeightPercent = a.Value.TotalMinutes / maxApp * 100
                }).ToList();

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    Rows = new ObservableCollection<DailySummaryRow>(rows);
                    TrendChart = trendItems;
                    TopAppsChart = appItems;
                    TotalActiveTime = FormatTime(total);
                    AvgScore = avgScore;
                    SummaryLabel = $"{from:MMM d} – {to.AddDays(-1):MMM d, yyyy}";
                    IsLoading = false;
                });
            });
        }

        private void ExportCsv()
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                FileName = $"EAMAS_Report_{DateTime.Today:yyyyMMdd}",
                DefaultExt = ".csv",
                Filter = "CSV files|*.csv"
            };

            if (dlg.ShowDialog() != true) return;

            var sb = new StringBuilder();
            sb.AppendLine("Date,Active Time,Idle Time,Productive Time,Distracting Time,Score,Screenshots");
            foreach (var row in Rows)
                sb.AppendLine($"{row.Date},{row.ActiveTime},{row.IdleTime}," +
                              $"{row.ProductiveTime},{row.DistractingTime},{row.Score},{row.Screenshots}");

            File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
            System.Windows.MessageBox.Show($"Report exported to {dlg.FileName}", "Export Complete",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private (DateTime from, DateTime to) GetDateRange() => ReportTypeIndex switch
        {
            0 => (DateTime.Today, DateTime.Today.AddDays(1)),
            1 => (DateTime.Today.AddDays(-6), DateTime.Today.AddDays(1)),
            2 => (new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1),
                  new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(1)),
            3 => (CustomFrom.Date, CustomTo.Date.AddDays(1)),
            _ => (DateTime.Today.AddDays(-6), DateTime.Today.AddDays(1))
        };

        private static string FormatTime(TimeSpan ts)
        {
            if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours}h {ts.Minutes:D2}m";
            if (ts.TotalMinutes >= 1) return $"{ts.Minutes}m";
            return ts.TotalSeconds < 1 ? "—" : $"{ts.Seconds}s";
        }

        private static string TruncateApp(string name, int max = 14)
            => name.Length > max ? name[..max] + "…" : name;
    }
}
