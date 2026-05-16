using EAMAS.Core.Enums;
using EAMAS.Core.Models;
using EAMAS.Core.Services;
using System.Windows.Threading;

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
        private readonly OrganizationService _orgService;
        private readonly UserService _userService;
        private readonly ScreenshotService _screenshotService;

        private readonly DispatcherTimer _refreshTimer;
        private bool _timerStarted;

        private string _activeTimeLabel = "—";
        private string _idleTimeLabel = "—";
        private string _breakTimeLabel = "—";
        private int _productivityScore;
        private int _screenshotCount;
        private int _unreadAlerts;
        private string _peakHour = "—";
        private string _headerSubtitle = string.Empty;
        private List<BarChartItem> _hourlyChart = new();
        private List<AppUsageItem> _topApps = new();
        private List<BarChartItem> _categoryChart = new();
        private bool _isLoading;

        public string ActiveTimeLabel { get => _activeTimeLabel; set => Set(ref _activeTimeLabel, value); }
        public string IdleTimeLabel { get => _idleTimeLabel; set => Set(ref _idleTimeLabel, value); }
        public string BreakTimeLabel { get => _breakTimeLabel; set => Set(ref _breakTimeLabel, value); }
        public int ProductivityScore { get => _productivityScore; set => Set(ref _productivityScore, value); }
        public int ScreenshotCount { get => _screenshotCount; set => Set(ref _screenshotCount, value); }
        public int UnreadAlerts { get => _unreadAlerts; set => Set(ref _unreadAlerts, value); }
        public string PeakHour { get => _peakHour; set => Set(ref _peakHour, value); }
        public string HeaderSubtitle { get => _headerSubtitle; set => Set(ref _headerSubtitle, value); }
        public List<BarChartItem> HourlyChart { get => _hourlyChart; set => Set(ref _hourlyChart, value); }
        public List<AppUsageItem> TopApps { get => _topApps; set => Set(ref _topApps, value); }
        public List<BarChartItem> CategoryChart { get => _categoryChart; set => Set(ref _categoryChart, value); }
        public bool IsLoading { get => _isLoading; set => Set(ref _isLoading, value); }
        public string TodayDate => DateTime.Today.ToString("dddd, MMMM d, yyyy");

        public bool IsSuperAdmin => App.CurrentUser?.Role == UserRole.SuperAdmin;

        public RelayCommand RefreshCommand { get; }

        public DashboardViewModel(
            ActivityMonitorService activityService,
            ReportService reportService,
            AnalyticsService analyticsService,
            AlertService alertService,
            OrganizationService orgService,
            UserService userService,
            ScreenshotService screenshotService)
        {
            _activityService = activityService;
            _reportService = reportService;
            _analyticsService = analyticsService;
            _alertService = alertService;
            _orgService = orgService;
            _userService = userService;
            _screenshotService = screenshotService;

            RefreshCommand = new RelayCommand(Load);

            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
            _refreshTimer.Tick += (_, _) => Load();
        }

        public void StartAutoRefresh()
        {
            if (_timerStarted) return;
            _timerStarted = true;
            _refreshTimer.Start();
        }

        public void StopAutoRefresh() => _refreshTimer.Stop();

        public void Load()
        {
            if (App.CurrentUser == null) return;
            IsLoading = true;

            if (IsSuperAdmin)
                Task.Run(LoadSuperAdminDashboard);
            else
                Task.Run(LoadUserDashboard);
        }

        // ── Per-user dashboard (Employee / Admin / Manager) ──────────────────────

        private void LoadUserDashboard()
        {
            var orgId = App.CurrentOrgId;
            var userId = App.CurrentUser!.Id;
            var today = DateTime.Today;

            var summary = _reportService.GetDailySummary(orgId, userId, today);
            var hourly = _activityService.GetHourlyActivity(orgId, userId, today);
            var appUsage = _activityService.GetAppUsage(orgId, userId, today, today.AddDays(1));
            var peaks = _analyticsService.GetPeakHours(orgId, userId, today, today.AddDays(1));
            var alerts = _alertService.GetUnreadCount(orgId, userId);

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                HeaderSubtitle = App.CurrentOrganization?.Name ?? string.Empty;
                ActiveTimeLabel = FormatTime(summary.ActiveTime);
                IdleTimeLabel = FormatTime(summary.IdleTime);
                BreakTimeLabel = FormatTime(summary.BreakTime);
                ProductivityScore = summary.ProductivityScore;
                ScreenshotCount = summary.ScreenshotCount;
                UnreadAlerts = alerts;
                PeakHour = peaks.FirstOrDefault()?.Label ?? "No activity yet";

                BuildHourlyChart(hourly);
                BuildTopApps(appUsage);
                BuildCategoryChart(summary);
                IsLoading = false;
            });
        }

        // ── SuperAdmin dashboard (cross-org aggregate) ────────────────────────────

        private void LoadSuperAdminDashboard()
        {
            var today = DateTime.Today;
            var orgs = _orgService.GetAll().Where(o => o.IsActive).ToList();

            // Aggregate active time across all orgs / all users today
            var allUsage = _activityService.GetAppUsage(null, null, today, today.AddDays(1));
            var activeTime = TimeSpan.FromTicks(
                allUsage.Where(u => u.Category != ActivityCategory.Unknown).Sum(u => u.Duration.Ticks));

            var productiveTime = TimeSpan.FromTicks(
                allUsage.Where(u => u.Category == ActivityCategory.Productive).Sum(u => u.Duration.Ticks));
            var distractingTime = TimeSpan.FromTicks(
                allUsage.Where(u => u.Category == ActivityCategory.Distracting).Sum(u => u.Duration.Ticks));

            var score = activeTime.TotalMinutes > 0
                ? (int)Math.Max(0, Math.Min(100,
                    (productiveTime.TotalMinutes - distractingTime.TotalMinutes * 0.5)
                    / activeTime.TotalMinutes * 100))
                : 0;

            var hourly = _activityService.GetHourlyActivityAllUsers(null, today);
            var totalScreenshots = orgs.Sum(o => _screenshotService.GetTodayCount(o.Id));
            var alerts = _alertService.GetUnreadCountAllOrgs();
            var activeUsers = _activityService.GetActiveUserCount(null, today);

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                HeaderSubtitle = $"{orgs.Count} organisations · {activeUsers} active users today";
                ActiveTimeLabel = FormatTime(activeTime);
                IdleTimeLabel = $"{orgs.Count} orgs";
                ProductivityScore = score;
                ScreenshotCount = totalScreenshots;
                UnreadAlerts = alerts;
                PeakHour = hourly.OrderByDescending(h => h.Value).FirstOrDefault().Key is int h2
                    ? $"{h2:D2}:00 – {h2 + 1:D2}:00" : "No activity";

                BuildHourlyChart(hourly);
                BuildTopAppsAllOrgs(allUsage);
                BuildCategoryChartFromUsage(allUsage, activeTime);
                IsLoading = false;
            });
        }

        // ── Chart builders ────────────────────────────────────────────────────────

        private void BuildHourlyChart(Dictionary<int, TimeSpan> hourly)
        {
            var maxMinutes = hourly.Values.Max(t => t.TotalMinutes);
            if (maxMinutes < 1) maxMinutes = 1;

            var workHours = Enumerable.Range(8, 12);
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

        private void BuildTopApps(List<AppUsage> appUsage)
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
                    CategoryColor = CategoryColor(a.Category)
                }).ToList();
        }

        private void BuildTopAppsAllOrgs(List<AppUsage> allUsage)
        {
            var grouped = allUsage
                .GroupBy(u => u.ApplicationName)
                .Select(g => new
                {
                    App = g.Key,
                    Duration = TimeSpan.FromTicks(g.Sum(u => u.Duration.Ticks)),
                    Category = g.First().Category
                })
                .OrderByDescending(x => x.Duration)
                .Take(6)
                .ToList();

            var total = grouped.Sum(x => x.Duration.TotalMinutes);
            if (total < 1) total = 1;

            TopApps = grouped.Select(x => new AppUsageItem
            {
                AppName = x.App,
                TimeLabel = FormatTime(x.Duration),
                Percent = x.Duration.TotalMinutes / total * 100,
                CategoryColor = CategoryColor(x.Category)
            }).ToList();
        }

        private void BuildCategoryChart(DailySummary summary)
        {
            var totalMins = (summary.ActiveTime + summary.BreakTime).TotalMinutes;
            if (totalMins < 1) totalMins = 1;
            var neutral = summary.ActiveTime - summary.ProductiveTime - summary.DistractingTime;
            if (neutral < TimeSpan.Zero) neutral = TimeSpan.Zero;

            CategoryChart = new List<BarChartItem>
            {
                new() { Label = "Productive", Value = summary.ProductiveTime.TotalMinutes,
                    Color = "#16A34A", ValueLabel = FormatTime(summary.ProductiveTime),
                    BarHeightPercent = summary.ProductiveTime.TotalMinutes / totalMins * 100 },
                new() { Label = "Neutral", Value = neutral.TotalMinutes,
                    Color = "#3B82F6", ValueLabel = FormatTime(neutral),
                    BarHeightPercent = neutral.TotalMinutes / totalMins * 100 },
                new() { Label = "Distracting", Value = summary.DistractingTime.TotalMinutes,
                    Color = "#EF4444", ValueLabel = FormatTime(summary.DistractingTime),
                    BarHeightPercent = summary.DistractingTime.TotalMinutes / totalMins * 100 },
                new() { Label = "Break", Value = summary.BreakTime.TotalMinutes,
                    Color = "#8B5CF6", ValueLabel = FormatTime(summary.BreakTime),
                    BarHeightPercent = summary.BreakTime.TotalMinutes / totalMins * 100 },
            };
        }

        private void BuildCategoryChartFromUsage(List<AppUsage> allUsage, TimeSpan activeTime)
        {
            var productive = TimeSpan.FromTicks(
                allUsage.Where(u => u.Category == ActivityCategory.Productive).Sum(u => u.Duration.Ticks));
            var distracting = TimeSpan.FromTicks(
                allUsage.Where(u => u.Category == ActivityCategory.Distracting).Sum(u => u.Duration.Ticks));
            var neutral = activeTime - productive - distracting;
            if (neutral < TimeSpan.Zero) neutral = TimeSpan.Zero;
            var total = activeTime.TotalMinutes;
            if (total < 1) total = 1;

            CategoryChart = new List<BarChartItem>
            {
                new() { Label = "Productive", Value = productive.TotalMinutes, Color = "#16A34A",
                    ValueLabel = FormatTime(productive),
                    BarHeightPercent = productive.TotalMinutes / total * 100 },
                new() { Label = "Neutral", Value = neutral.TotalMinutes, Color = "#3B82F6",
                    ValueLabel = FormatTime(neutral),
                    BarHeightPercent = neutral.TotalMinutes / total * 100 },
                new() { Label = "Distracting", Value = distracting.TotalMinutes, Color = "#EF4444",
                    ValueLabel = FormatTime(distracting),
                    BarHeightPercent = distracting.TotalMinutes / total * 100 },
            };
        }

        private static string CategoryColor(ActivityCategory cat) => cat switch
        {
            ActivityCategory.Productive => "#16A34A",
            ActivityCategory.Distracting => "#EF4444",
            _ => "#3B82F6"
        };

        private static string FormatTime(TimeSpan ts)
        {
            if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours}h {ts.Minutes:D2}m";
            if (ts.TotalMinutes >= 1) return $"{ts.Minutes}m";
            return $"{ts.Seconds}s";
        }
    }
}
