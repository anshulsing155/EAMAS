using EAMAS.Core.Enums;
using EAMAS.Core.Models;
using EAMAS.Core.Services;
using EAMAS.Desktop.Services;
using EAMAS.Desktop.Views;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;

namespace EAMAS.Desktop.ViewModels
{
    public class DailySummaryRow
    {
        public string Date            { get; set; } = string.Empty;
        public string ActiveTime      { get; set; } = string.Empty;
        public string IdleTime        { get; set; } = string.Empty;
        public string BreakTime       { get; set; } = string.Empty;
        public string ProductiveTime  { get; set; } = string.Empty;
        public string DistractingTime { get; set; } = string.Empty;
        public int    Score           { get; set; }
        public int    Screenshots     { get; set; }
        public string TopApp          { get; set; } = string.Empty;
    }

    public class ReportsViewModel : BaseViewModel
    {
        private readonly ReportService    _reportService;
        private readonly AnalyticsService _analyticsService;
        private readonly UserService      _userService;

        private int    _reportTypeIndex;
        private ObservableCollection<DailySummaryRow> _rows = new();
        private List<BarChartItem> _trendChart   = new();
        private List<BarChartItem> _topAppsChart = new();
        private bool   _isLoading;
        private string _summaryLabel    = string.Empty;
        private int    _avgScore;
        private string _totalActiveTime = "—";
        private string _totalProductiveTime = "—";
        private string _totalIdleTime   = "—";
        private string _totalBreakTime  = "—";
        private int    _totalScreenshots;
        private int    _alertCount;
        private List<User>  _users        = new();
        private User?       _selectedUser;
        private DateTime    _customFrom   = DateTime.Today.AddDays(-7);
        private DateTime    _customTo     = DateTime.Today;

        // Category breakdown (for UI display)
        private string _productivePct   = "0%";
        private string _distractingPct  = "0%";
        private string _neutralPct      = "0%";
        private string _idlePct         = "0%";
        private string _breakPct        = "0%";

        // The last-built bundle (used by export commands)
        private ReportBundle? _lastBundle;

        // ── Properties ───────────────────────────────────────────────────────────

        public int    ReportTypeIndex   { get => _reportTypeIndex; set { Set(ref _reportTypeIndex, value); Generate(); } }
        public ObservableCollection<DailySummaryRow> Rows { get => _rows; set => Set(ref _rows, value); }
        public List<BarChartItem> TrendChart   { get => _trendChart;   set => Set(ref _trendChart, value); }
        public List<BarChartItem> TopAppsChart { get => _topAppsChart; set => Set(ref _topAppsChart, value); }
        public bool   IsLoading         { get => _isLoading;        set => Set(ref _isLoading, value); }
        public string SummaryLabel      { get => _summaryLabel;     set => Set(ref _summaryLabel, value); }
        public int    AvgScore          { get => _avgScore;         set => Set(ref _avgScore, value); }
        public string TotalActiveTime   { get => _totalActiveTime;  set => Set(ref _totalActiveTime, value); }
        public string TotalProductiveTime{ get => _totalProductiveTime; set => Set(ref _totalProductiveTime, value); }
        public string TotalIdleTime     { get => _totalIdleTime;    set => Set(ref _totalIdleTime, value); }
        public string TotalBreakTime    { get => _totalBreakTime;   set => Set(ref _totalBreakTime, value); }
        public int    TotalScreenshots  { get => _totalScreenshots; set => Set(ref _totalScreenshots, value); }
        public int    AlertCount        { get => _alertCount;       set => Set(ref _alertCount, value); }
        public List<User> Users         { get => _users;            set => Set(ref _users, value); }
        public User?  SelectedUser      { get => _selectedUser;     set { Set(ref _selectedUser, value); Generate(); } }
        public DateTime CustomFrom      { get => _customFrom;       set => Set(ref _customFrom, value); }
        public DateTime CustomTo        { get => _customTo;         set => Set(ref _customTo, value); }

        public string ProductivePct  { get => _productivePct;  set => Set(ref _productivePct, value); }
        public string DistractingPct { get => _distractingPct; set => Set(ref _distractingPct, value); }
        public string NeutralPct     { get => _neutralPct;     set => Set(ref _neutralPct, value); }
        public string IdlePct        { get => _idlePct;        set => Set(ref _idlePct, value); }
        public string BreakPct       { get => _breakPct;       set => Set(ref _breakPct, value); }

        public bool IsAdmin   => App.CurrentUser?.Role is UserRole.Admin or UserRole.SuperAdmin;
        public bool IsManager => App.CurrentUser?.Role is UserRole.Manager or UserRole.Admin or UserRole.SuperAdmin;

        public RelayCommand      GenerateCommand       { get; }
        public RelayCommand      ExportCsvCommand      { get; }
        public AsyncRelayCommand ExportExcelCommand    { get; }
        public AsyncRelayCommand ExportPdfCommand      { get; }
        public RelayCommand      OpenMethodologyCommand{ get; }

        public ReportsViewModel(ReportService reportService, AnalyticsService analyticsService,
            UserService userService)
        {
            _reportService    = reportService;
            _analyticsService = analyticsService;
            _userService      = userService;

            GenerateCommand        = new RelayCommand(Generate);
            ExportCsvCommand       = new RelayCommand(ExportCsv,   () => _lastBundle != null && !_isLoading);
            ExportExcelCommand     = new AsyncRelayCommand(ExportExcelAsync, () => _lastBundle != null && !_isLoading);
            ExportPdfCommand       = new AsyncRelayCommand(ExportPdfAsync,   () => _lastBundle != null && !_isLoading);
            OpenMethodologyCommand = new RelayCommand(OpenMethodology);
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
            var orgId  = App.CurrentOrgId;
            var userId = SelectedUser?.Id ?? App.CurrentUser!.Id;
            var (from, to) = GetDateRange();
            IsLoading = true;
            _lastBundle = null;

            Task.Run(() =>
            {
                // ── Fetch all data in parallel ────────────────────────────────
                var summaries   = _reportService.GetRangeSummaries(orgId, userId, from, to);
                var topApps     = _reportService.GetTopApplications(orgId, userId, from, to, 8);
                var appDetails  = _reportService.GetAppUsageDetails(orgId, userId, from, to, 20);
                var catTotals   = _reportService.GetCategoryTotals(orgId, userId, from, to);
                var hourly      = _reportService.GetHourlyBreakdown(orgId, userId, from, to);
                var alerts      = _reportService.GetAlertsForPeriod(orgId, userId, from, to);
                var auditLogs   = IsAdmin
                    ? _reportService.GetAuditLogsForPeriod(orgId, from, to)
                    : new List<AuditLog>();

                // ── Aggregates ────────────────────────────────────────────────
                var totalActive     = TimeSpan.FromTicks(summaries.Sum(s => s.ActiveTime.Ticks));
                var totalIdle       = TimeSpan.FromTicks(summaries.Sum(s => s.IdleTime.Ticks));
                var totalBreak      = TimeSpan.FromTicks(summaries.Sum(s => s.BreakTime.Ticks));
                var totalProductive = TimeSpan.FromTicks(summaries.Sum(s => s.ProductiveTime.Ticks));
                var totalDistracting= TimeSpan.FromTicks(summaries.Sum(s => s.DistractingTime.Ticks));
                var totalShots      = summaries.Sum(s => s.ScreenshotCount);
                var scored          = summaries.Where(s => s.ActiveTime.TotalMinutes >= 10).ToList();
                var avgScore        = scored.Any() ? (int)scored.Average(s => s.ProductivityScore) : 0;

                // ── Category percentages ──────────────────────────────────────
                var totalTimeTicks = totalActive.Ticks + totalIdle.Ticks + totalBreak.Ticks;
                if (totalTimeTicks == 0) totalTimeTicks = 1;
                string CatPct(ActivityCategory cat)
                {
                    catTotals.TryGetValue(cat, out var ts);
                    return $"{(double)ts.Ticks / totalTimeTicks * 100:F0}%";
                }
                var idlePct  = $"{(double)totalIdle.Ticks  / totalTimeTicks * 100:F0}%";
                var breakPct = $"{(double)totalBreak.Ticks / totalTimeTicks * 100:F0}%";

                // ── Grid rows ─────────────────────────────────────────────────
                var rows = summaries.Select(s => new DailySummaryRow
                {
                    Date            = s.Date.ToString("ddd, MMM d"),
                    ActiveTime      = FormatTime(s.ActiveTime),
                    IdleTime        = FormatTime(s.IdleTime),
                    BreakTime       = FormatTime(s.BreakTime),
                    ProductiveTime  = FormatTime(s.ProductiveTime),
                    DistractingTime = FormatTime(s.DistractingTime),
                    Score           = s.ProductivityScore,
                    Screenshots     = s.ScreenshotCount,
                    TopApp          = s.TopApps.FirstOrDefault().App ?? "—"
                }).ToList();

                // ── Trend chart ───────────────────────────────────────────────
                var trendItems = summaries.Select(s => new BarChartItem
                {
                    Label           = s.Date.ToString("M/d"),
                    Value           = s.ProductivityScore,
                    Color           = s.ProductivityScore >= 70 ? "#16A34A"
                                    : s.ProductivityScore >= 40 ? "#D97706" : "#EF4444",
                    ValueLabel      = $"{s.ProductivityScore}%",
                    BarHeightPercent= s.ProductivityScore
                }).ToList();

                // ── Top apps chart ────────────────────────────────────────────
                var maxApp = topApps.Any() ? topApps.Max(a => a.Value.TotalMinutes) : 1;
                if (maxApp < 1) maxApp = 1;
                var appItems = topApps.Select(a => new BarChartItem
                {
                    Label           = TruncateApp(a.Key),
                    Value           = a.Value.TotalMinutes,
                    Color           = "#2563EB",
                    ValueLabel      = FormatTime(a.Value),
                    BarHeightPercent= a.Value.TotalMinutes / maxApp * 100
                }).ToList();

                // ── Build bundle for export ───────────────────────────────────
                var selectedUser = SelectedUser ?? App.CurrentUser!;
                var bundle = new ReportBundle
                {
                    OrganizationName   = App.CurrentOrganization?.Name ?? "—",
                    EmployeeName       = selectedUser.FullName.Length > 0 ? selectedUser.FullName : selectedUser.Username,
                    EmployeeRole       = selectedUser.Role.ToString(),
                    EmployeeDepartment = selectedUser.Department,
                    GeneratedBy        = App.CurrentUser!.FullName.Length > 0 ? App.CurrentUser.FullName : App.CurrentUser.Username,
                    From               = from,
                    To                 = to,
                    DailySummaries     = summaries,
                    AppDetails         = appDetails,
                    CategoryTotals     = catTotals,
                    HourlyBreakdown    = hourly,
                    Alerts             = alerts,
                    AuditLogs          = auditLogs,
                    TotalActive        = totalActive,
                    TotalIdle          = totalIdle,
                    TotalBreakTime     = totalBreak,
                    TotalProductive    = totalProductive,
                    TotalDistracting   = totalDistracting,
                    AvgScore           = avgScore,
                    TotalScreenshots   = totalShots
                };

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    Rows             = new ObservableCollection<DailySummaryRow>(rows);
                    TrendChart       = trendItems;
                    TopAppsChart     = appItems;
                    TotalActiveTime  = FormatTime(totalActive);
                    TotalProductiveTime = FormatTime(totalProductive);
                    TotalIdleTime    = FormatTime(totalIdle);
                    TotalBreakTime   = FormatTime(totalBreak);
                    TotalScreenshots = totalShots;
                    AlertCount       = alerts.Count;
                    AvgScore         = avgScore;
                    SummaryLabel     = $"{from:MMM d} – {to.AddDays(-1):MMM d, yyyy}";
                    ProductivePct    = CatPct(ActivityCategory.Productive);
                    DistractingPct   = CatPct(ActivityCategory.Distracting);
                    NeutralPct       = CatPct(ActivityCategory.Neutral);
                    IdlePct          = idlePct;
                    BreakPct         = breakPct;
                    _lastBundle      = bundle;
                    IsLoading        = false;
                });
            });
        }

        // ── Export ───────────────────────────────────────────────────────────────

        private void ExportCsv()
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                FileName    = $"EAMAS_Report_{DateTime.Today:yyyyMMdd}",
                DefaultExt  = ".csv",
                Filter      = "CSV files|*.csv"
            };
            if (dlg.ShowDialog() != true) return;

            var sb = new StringBuilder();
            sb.AppendLine("Date,Active Time,Idle Time,Break Time (Screen Lock),Productive Time,Distracting Time," +
                          "Score,Screenshots,Top App");
            foreach (var row in Rows)
                sb.AppendLine($"\"{row.Date}\",{row.ActiveTime},{row.IdleTime},{row.BreakTime}," +
                              $"{row.ProductiveTime},{row.DistractingTime}," +
                              $"{row.Score},{row.Screenshots},\"{row.TopApp}\"");

            // Category summary block
            if (_lastBundle != null)
            {
                sb.AppendLine();
                sb.AppendLine("CATEGORY SUMMARY");
                sb.AppendLine("Category,Total Time,Percentage");
                var totalTicks = (_lastBundle.TotalActive + _lastBundle.TotalIdle + _lastBundle.TotalBreakTime).Ticks;
                if (totalTicks == 0) totalTicks = 1;
                foreach (var kv in _lastBundle.CategoryTotals)
                    sb.AppendLine($"{kv.Key},{ExportService.FormatTime(kv.Value)}," +
                                  $"{(double)kv.Value.Ticks / totalTicks * 100:F1}%");
                sb.AppendLine($"Idle,{ExportService.FormatTime(_lastBundle.TotalIdle)}," +
                              $"{(double)_lastBundle.TotalIdle.Ticks / totalTicks * 100:F1}%");
                sb.AppendLine($"Break (Screen Lock),{ExportService.FormatTime(_lastBundle.TotalBreakTime)}," +
                              $"{(double)_lastBundle.TotalBreakTime.Ticks / totalTicks * 100:F1}%");

                // App usage block
                sb.AppendLine();
                sb.AppendLine("APPLICATION USAGE");
                sb.AppendLine("Application,Process,Category,Total Time,Productivity Rating");
                foreach (var a in _lastBundle.AppDetails)
                    sb.AppendLine($"\"{a.ApplicationName}\",\"{a.ProcessName}\"," +
                                  $"{a.Category},{ExportService.FormatTime(a.TotalDuration)},{a.ProductivityRating}%");

                // Alerts block
                if (_lastBundle.Alerts.Any())
                {
                    sb.AppendLine();
                    sb.AppendLine("ALERTS");
                    sb.AppendLine("Date/Time,Type,Message,Resolved");
                    foreach (var al in _lastBundle.Alerts)
                        sb.AppendLine($"{al.CreatedAt.ToLocalTime():yyyy-MM-dd HH:mm}," +
                                      $"{al.Type},\"{al.Message}\",{(al.IsResolved ? "Yes" : "No")}");
                }
            }

            // Methodology block
            sb.AppendLine();
            sb.AppendLine("ACTIVITY MEASUREMENT GUIDE");
            sb.AppendLine();
            sb.AppendLine("CATEGORY,DEFINITION,SCORE IMPACT");
            sb.AppendLine("Productive,\"Active window matches a work-related application (IDE, Office, email, meetings, terminal, design, version control)\",+POSITIVE — raises score");
            sb.AppendLine("Distracting,\"Active window matches a non-work application (social media, streaming, gaming, entertainment)\",−NEGATIVE — lowers score");
            sb.AppendLine("Neutral,\"Active window is neither work-related nor clearly distracting (File Explorer, Calculator, Notepad, Spotify, unrecognised apps)\",NONE — not counted");
            sb.AppendLine("Idle,\"No keyboard or mouse input detected for the configured idle threshold\",EXCLUDED — not part of Active Time");
            sb.AppendLine();
            sb.AppendLine("PRODUCTIVITY SCORE FORMULA");
            sb.AppendLine("Score = ( Productive Time / Active Time ) x 100");
            sb.AppendLine("Active Time = all time with keyboard or mouse input (idle excluded from denominator)");
            sb.AppendLine("Score Bands: >= 70% Excellent | 40-69% Acceptable | < 40% Needs Attention");
            sb.AppendLine();
            sb.AppendLine("BUILT-IN PRODUCTIVE APPS");
            sb.AppendLine("Dev Editors,\"VS Code, Visual Studio, Rider, IntelliJ, PyCharm, WebStorm, CLion, Android Studio, Notepad++, Sublime Text, Atom, Eclipse, NetBeans\"");
            sb.AppendLine("Office Suite,\"Excel, Word, PowerPoint, OneNote, Outlook\"");
            sb.AppendLine("Communication,\"Microsoft Teams, Slack, Zoom\"");
            sb.AppendLine("Dev Tools,\"Postman, Insomnia, DBeaver, SSMS, CMD, PowerShell, Windows Terminal, Git, GitHub Desktop, SourceTree\"");
            sb.AppendLine("Design & Creative,\"Figma, Adobe apps, Blender\"");
            sb.AppendLine();
            sb.AppendLine("BUILT-IN DISTRACTING APPS");
            sb.AppendLine("Video Streaming,\"YouTube, Netflix, Amazon Prime, Disney+, Twitch, VLC\"");
            sb.AppendLine("Social Media,\"TikTok, Instagram, Facebook, Twitter/X, Reddit\"");
            sb.AppendLine("Gaming,\"Steam, Epic Games, League of Legends, Valorant\"");
            sb.AppendLine();
            sb.AppendLine("Custom rules can be added in Settings > App Categories to override or extend these defaults.");

            File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
            System.Windows.MessageBox.Show($"CSV report saved to:\n{dlg.FileName}", "Export Complete",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private Task ExportExcelAsync()
        {
            if (_lastBundle == null) return Task.CompletedTask;
            var bundle = _lastBundle;

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                FileName   = $"EAMAS_Report_{DateTime.Today:yyyyMMdd}",
                DefaultExt = ".xlsx",
                Filter     = "Excel Workbook|*.xlsx"
            };
            if (dlg.ShowDialog() != true) return Task.CompletedTask;

            var path = dlg.FileName;
            return Task.Run(() =>
            {
                try
                {
                    ExportService.ExportToExcel(bundle, path);
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        System.Windows.MessageBox.Show($"Excel report saved to:\n{path}", "Export Complete",
                            MessageBoxButton.OK, MessageBoxImage.Information));
                }
                catch (Exception ex)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        System.Windows.MessageBox.Show($"Excel export failed:\n{ex.Message}", "Export Error",
                            MessageBoxButton.OK, MessageBoxImage.Error));
                }
            });
        }

        private Task ExportPdfAsync()
        {
            if (_lastBundle == null) return Task.CompletedTask;
            var bundle = _lastBundle;

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                FileName   = $"EAMAS_Report_{DateTime.Today:yyyyMMdd}",
                DefaultExt = ".pdf",
                Filter     = "PDF Document|*.pdf"
            };
            if (dlg.ShowDialog() != true) return Task.CompletedTask;

            var path = dlg.FileName;
            return Task.Run(() =>
            {
                try
                {
                    ExportService.ExportToPdf(bundle, path);
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        System.Windows.MessageBox.Show($"PDF report saved to:\n{path}", "Export Complete",
                            MessageBoxButton.OK, MessageBoxImage.Information));
                }
                catch (Exception ex)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        System.Windows.MessageBox.Show($"PDF export failed:\n{ex.Message}", "Export Error",
                            MessageBoxButton.OK, MessageBoxImage.Error));
                }
            });
        }

        private static void OpenMethodology()
        {
            var win = new ActivityMethodologyWindow
            {
                Owner = System.Windows.Application.Current.MainWindow
            };
            win.ShowDialog();
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private (DateTime from, DateTime to) GetDateRange() => ReportTypeIndex switch
        {
            0 => (DateTime.Today, DateTime.Today.AddDays(1)),
            1 => (DateTime.Today.AddDays(-6), DateTime.Today.AddDays(1)),
            2 => (new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1),
                  new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(1)),
            3 => (CustomFrom.Date, CustomTo.Date.AddDays(1)),
            _ => (DateTime.Today.AddDays(-6), DateTime.Today.AddDays(1))
        };

        internal static string FormatTime(TimeSpan ts)
        {
            if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours}h {ts.Minutes:D2}m";
            if (ts.TotalMinutes >= 1) return $"{ts.Minutes}m";
            return ts.TotalSeconds < 1 ? "—" : $"{ts.Seconds}s";
        }

        private static string TruncateApp(string name, int max = 14)
            => name.Length > max ? name[..max] + "…" : name;
    }
}
