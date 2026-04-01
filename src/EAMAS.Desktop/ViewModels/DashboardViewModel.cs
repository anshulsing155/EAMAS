using EAMAS.Core.Enums;
using EAMAS.Core.Services;

namespace EAMAS.Desktop.ViewModels
{
    public class BarChartItem
    {
        public string Label { get; set; } = string.Empty;
        public double Value { get; set; }
        public string Color { get; set; } = "#2563EB";
        public string ValueLabel { get; set; } = string.Empty;
        public double BarHeightPercent { get; set; }
    }

    public class AppUsageItem
    {
        public string AppName { get; set; } = string.Empty;
        public string TimeLabel { get; set; } = string.Empty;
        public double Percent { get; set; }
        public string CategoryColor { get; set; } = "#3B82F6";
    }

    public class DashboardViewModel : BaseViewModel
    {
        private readonly ActivityMonitorService _activityService;
        private readonly ReportService _reportService;
        private readonly AnalyticsService _analyticsService;
        private readonly AlertService _alertService;

        private string _activeTimeLabel = "—";
        private string _idleTimeLabel = "—";
        private int _productivityScore;
        private int _screenshotCount;
        private int _unreadAlerts;
        private string _peakHour = "—";
        private List<BarChartItem> _hourlyChart = new();
        private List<AppUsageItem> _topApps = new();
        private List<BarChartItem> _categoryChart = new();
        private bool _isLoading;

        public string ActiveTimeLabel { get => _activeTimeLabel; set => Set(ref _activeTimeLabel, value); }
        public string IdleTimeLabel { get => _idleTimeLabel; set => Set(ref _idleTimeLabel, value); }
        public int ProductivityScore { get => _productivityScore; set => Set(ref _productivityScore, value); }
        public int ScreenshotCount { get => _screenshotCount; set => Set(ref _screenshotCount, value); }
        public int UnreadAlerts { get => _unreadAlerts; set => Set(ref _unreadAlerts, value); }
        public string PeakHour { get => _peakHour; set => Set(ref _peakHour, value); }
        public List<BarChartItem> HourlyChart { get => _hourlyChart; set => Set(ref _hourlyChart, value); }
        public List<AppUsageItem> TopApps { get => _topApps; set => Set(ref _topApps, value); }
        public List<BarChartItem> CategoryChart { get => _categoryChart; set => Set(ref _categoryChart, value); }
        public bool IsLoading { get => _isLoading; set => Set(ref _isLoading, value); }
        public string TodayDate => DateTime.Today.ToString("dddd, MMMM d, yyyy");

        public RelayCommand RefreshCommand { get; }

        public DashboardViewModel(
            ActivityMonitorService activityService,
            ReportService reportService,
            AnalyticsService analyticsService,
            AlertService alertService)
        {
            _activityService = activityService;
            _reportService = reportService;
            _analyticsService = analyticsService;
            _alertService = alertService;
            RefreshCommand = new RelayCommand(Load);
        }

        public void Load()
        {
            if (App.CurrentUser == null) return;
            IsLoading = true;

            Task.Run(() =>
            {
                var orgId = App.CurrentOrgId;
                var userId = App.CurrentUser.Id;
                var today = DateTime.Today;

                var summary = _reportService.GetDailySummary(orgId, userId, today);
                var hourly = _activityService.GetHourlyActivity(orgId, userId, today);
                var appUsage = _activityService.GetAppUsage(orgId, userId, today, today.AddDays(1));
                var peaks = _analyticsService.GetPeakHours(orgId, userId, today, today.AddDays(1));
                var alerts = _alertService.GetUnreadCount(orgId, userId);

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    ActiveTimeLabel = FormatTime(summary.ActiveTime);
                    IdleTimeLabel = FormatTime(summary.IdleTime);
                    ProductivityScore = summary.ProductivityScore;
                    ScreenshotCount = summary.ScreenshotCount;
                    UnreadAlerts = alerts;
                    PeakHour = peaks.FirstOrDefault()?.Label ?? "No activity yet";

                    BuildHourlyChart(hourly);
                    BuildTopApps(appUsage);
                    BuildCategoryChart(summary);
                    IsLoading = false;
                });
            });
        }

        private void BuildHourlyChart(Dictionary<int, TimeSpan> hourly)
        {
            var maxMinutes = hourly.Values.Max(t => t.TotalMinutes);
            if (maxMinutes < 1) maxMinutes = 1;

            var workHours = Enumerable.Range(8, 12); // 8 AM to 8 PM
            HourlyChart = workHours.Select(h => new BarChartItem
            {
                Label = $"{h:D2}",
                Value = hourly.TryGetValue(h, out var t) ? t.TotalMinutes : 0,
                Color = "#2563EB",
                ValueLabel = hourly.TryGetValue(h, out var t2) && t2.TotalMinutes > 0
                    ? $"{(int)t2.TotalMinutes}m" : "",
                BarHeightPercent = hourly.TryGetValue(h, out var t3)
                    ? t3.TotalMinutes / maxMinutes * 100 : 0
            }).ToList();
        }

        private void BuildTopApps(List<Core.Models.AppUsage> appUsage)
        {
            var total = appUsage.Sum(a => a.Duration.TotalMinutes);
            if (total < 1) total = 1;

            TopApps = appUsage
                .OrderByDescending(a => a.Duration)
                .Take(6)
                .Select(a => new AppUsageItem
                {
                    AppName = a.ApplicationName,
                    TimeLabel = FormatTime(a.Duration),
                    Percent = a.Duration.TotalMinutes / total * 100,
                    CategoryColor = a.Category switch
                    {
                        ActivityCategory.Productive => "#16A34A",
                        ActivityCategory.Distracting => "#EF4444",
                        _ => "#3B82F6"
                    }
                }).ToList();
        }

        private void BuildCategoryChart(Core.Services.DailySummary summary)
        {
            var total = summary.ActiveTime.TotalMinutes;
            if (total < 1) total = 1;
            var neutral = summary.ActiveTime - summary.ProductiveTime - summary.DistractingTime;
            if (neutral < TimeSpan.Zero) neutral = TimeSpan.Zero;

            CategoryChart = new List<BarChartItem>
            {
                new() { Label = "Productive", Value = summary.ProductiveTime.TotalMinutes,
                    Color = "#16A34A", ValueLabel = FormatTime(summary.ProductiveTime),
                    BarHeightPercent = summary.ProductiveTime.TotalMinutes / total * 100 },
                new() { Label = "Neutral", Value = neutral.TotalMinutes,
                    Color = "#3B82F6", ValueLabel = FormatTime(neutral),
                    BarHeightPercent = neutral.TotalMinutes / total * 100 },
                new() { Label = "Distracting", Value = summary.DistractingTime.TotalMinutes,
                    Color = "#EF4444", ValueLabel = FormatTime(summary.DistractingTime),
                    BarHeightPercent = summary.DistractingTime.TotalMinutes / total * 100 },
            };
        }

        private static string FormatTime(TimeSpan ts)
        {
            if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours}h {ts.Minutes:D2}m";
            if (ts.TotalMinutes >= 1) return $"{ts.Minutes}m";
            return $"{ts.Seconds}s";
        }
    }
}
