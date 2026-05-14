using EAMAS.Core.Models;
using EAMAS.Core.Services;
using System.Drawing;
using System.Drawing.Imaging;

namespace EAMAS.Desktop.Services
{
    public class MonitoringBackgroundService : IDisposable
    {
        private readonly ActivityMonitorService _activityService;
        private readonly ScreenshotService _screenshotService;
        private readonly AlertService _alertService;
        private readonly SettingsService _settingsService;

        private CancellationTokenSource? _cts;
        private Task? _activityTask;
        private Task? _screenshotTask;

        private string _currentOrgId = string.Empty;
        private string _currentUserId = string.Empty;
        private bool _isRunning;

        // Current window session tracking
        private IntPtr _lastWindow = IntPtr.Zero;
        private string _lastProcess = string.Empty;
        private string _lastTitle = string.Empty;
        private DateTime _sessionStart = DateTime.MinValue;
        private bool _wasIdle = false;

        public event Action<string, string>? ActivityChanged;

        public MonitoringBackgroundService(
            ActivityMonitorService activityService,
            ScreenshotService screenshotService,
            AlertService alertService,
            SettingsService settingsService)
        {
            _activityService = activityService;
            _screenshotService = screenshotService;
            _alertService = alertService;
            _settingsService = settingsService;
        }

        public void Start(string orgId, string userId)
        {
            if (_isRunning) return;
            _currentOrgId = orgId;
            _currentUserId = userId;
            _isRunning = true;
            _cts = new CancellationTokenSource();

            _activityTask = Task.Run(() => ActivityLoop(_cts.Token), _cts.Token);
            _screenshotTask = Task.Run(() => ScreenshotLoop(_cts.Token), _cts.Token);
        }

        public void Stop()
        {
            if (!_isRunning) return;
            _isRunning = false;
            _cts?.Cancel();
            FlushCurrentSession(DateTime.UtcNow);
        }

        public void TriggerScreenshot()
        {
            var settings = _settingsService.GetSettings(_currentOrgId);
            if (!settings.MonitoringEnabled || !settings.ScreenshotsEnabled)
                return;

            CaptureAndSave(settings, isManual: true);
        }

        private async Task ActivityLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var settings = _settingsService.GetSettings(_currentOrgId);
                    if (settings.MonitoringEnabled)
                        PollActivity(settings);

                    var pollMs = Math.Max(1, settings.ActivityPollIntervalSeconds) * 1000;
                    await Task.Delay(pollMs, ct).ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    return;
                }
                catch
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), ct).ConfigureAwait(false);
                }
            }
        }

        private void PollActivity(SystemSettings settings)
        {
            var idleTime = WindowsApiService.GetIdleTime();
            var isIdle = idleTime.TotalSeconds >= settings.IdleThresholdSeconds;
            var now = DateTime.UtcNow;

            if (isIdle)
            {
                if (!_wasIdle)
                {
                    FlushCurrentSession(now);
                    _wasIdle = true;
                    _sessionStart = now - idleTime;
                    _lastProcess = "Idle";
                    _lastTitle = "System Idle";
                }

                _alertService.CheckAndGenerateAlerts(
                    _currentOrgId, _currentUserId, settings, idleTime,
                    GetTodayDistractingTime());
                return;
            }

            var hWnd = WindowsApiService.GetForegroundWindow();
            if (hWnd == IntPtr.Zero) return;

            var pid = WindowsApiService.GetProcessId(hWnd);
            var process = WindowsApiService.GetProcessName(pid);
            var title = WindowsApiService.GetWindowTitle(hWnd);

            if (_wasIdle)
            {
                FlushSession(_lastProcess, _lastTitle, _sessionStart, now, isIdle: true);
                _wasIdle = false;
                _sessionStart = now;
                _lastProcess = process;
                _lastTitle = title;
                _lastWindow = hWnd;
                return;
            }

            if (hWnd == _lastWindow && process == _lastProcess)
                return;

            if (_sessionStart != DateTime.MinValue)
                FlushCurrentSession(now);

            _lastWindow = hWnd;
            _lastProcess = process;
            _lastTitle = title;
            _sessionStart = now;

            ActivityChanged?.Invoke(process, title);
        }

        private void FlushCurrentSession(DateTime end)
        {
            if (_sessionStart == DateTime.MinValue || string.IsNullOrEmpty(_lastProcess)) return;
            FlushSession(_lastProcess, _lastTitle, _sessionStart, end, _wasIdle);
            _sessionStart = DateTime.MinValue;
        }

        private void FlushSession(string process, string title, DateTime start, DateTime end, bool isIdle)
        {
            if (end <= start || (end - start).TotalSeconds < 2) return;

            var log = new ActivityLog
            {
                OrganizationId = _currentOrgId,
                UserId = _currentUserId,
                StartTime = start,
                EndTime = end,
                ProcessName = process,
                ApplicationName = FormatAppName(process),
                WindowTitle = title,
                IsIdle = isIdle
            };

            _activityService.RecordActivity(log);
        }

        private async Task ScreenshotLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                var settings = _settingsService.GetSettings(_currentOrgId);
                var intervalMinutes = Math.Max(1, settings.ScreenshotIntervalMinutes);

                await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), ct).ConfigureAwait(false);

                if (ct.IsCancellationRequested) break;

                try
                {
                    settings = _settingsService.GetSettings(_currentOrgId);
                    if (settings.MonitoringEnabled && settings.ScreenshotsEnabled)
                        CaptureAndSave(settings, isManual: false);
                }
                catch (TaskCanceledException)
                {
                    return;
                }
                catch { /* swallow */ }
            }
        }

        private void CaptureAndSave(SystemSettings settings, bool isManual)
        {
            var directory = string.IsNullOrEmpty(settings.ScreenshotsDirectory)
                ? SettingsService.GetDefaultScreenshotsDirectory(_currentOrgId)
                : settings.ScreenshotsDirectory;
            System.IO.Directory.CreateDirectory(directory);

            var hWnd = WindowsApiService.GetForegroundWindow();
            var pid = WindowsApiService.GetProcessId(hWnd);
            var currentApp = WindowsApiService.GetProcessName(pid);

            var fileName = $"{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
            var filePath = System.IO.Path.Combine(directory, fileName);
            var thumbPath = System.IO.Path.Combine(directory, $"thumb_{fileName}");

            var bounds = System.Windows.Forms.Screen.PrimaryScreen!.Bounds;
            using var bmp = new Bitmap(bounds.Width, bounds.Height);
            using var g = Graphics.FromImage(bmp);
            g.CopyFromScreen(System.Drawing.Point.Empty, System.Drawing.Point.Empty, bounds.Size);

            var jpegEncoder = GetJpegEncoder();
            var encoderParams = new EncoderParameters(1);
            encoderParams.Param[0] = new EncoderParameter(
                Encoder.Quality, (long)settings.JpegQuality);

            bmp.Save(filePath, jpegEncoder, encoderParams);
            var fileInfo = new System.IO.FileInfo(filePath);

            using var thumb = new Bitmap(240, 135);
            using var tg = Graphics.FromImage(thumb);
            tg.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            tg.DrawImage(bmp, 0, 0, 240, 135);
            thumb.Save(thumbPath, jpegEncoder, encoderParams);

            _screenshotService.SaveRecord(new ScreenshotRecord
            {
                OrganizationId = _currentOrgId,
                UserId = _currentUserId,
                TakenAt = DateTime.UtcNow,
                FilePath = filePath,
                ThumbnailPath = thumbPath,
                ApplicationName = FormatAppName(currentApp),
                FileSizeBytes = fileInfo.Length,
                IsManual = isManual
            });
        }

        private TimeSpan GetTodayDistractingTime()
        {
            try
            {
                var today = DateTime.Today;
                var usage = _activityService.GetAppUsage(
                    _currentOrgId, _currentUserId, today, today.AddDays(1));
                var ticks = usage
                    .Where(u => u.Category == Core.Enums.ActivityCategory.Distracting)
                    .Sum(u => u.Duration.Ticks);
                return TimeSpan.FromTicks(ticks);
            }
            catch { return TimeSpan.Zero; }
        }

        private static string FormatAppName(string processName)
        {
            if (string.IsNullOrEmpty(processName)) return "Unknown";
            return processName switch
            {
                "devenv" => "Visual Studio",
                "code" => "VS Code",
                "chrome" => "Google Chrome",
                "firefox" => "Mozilla Firefox",
                "msedge" => "Microsoft Edge",
                "winword" => "Microsoft Word",
                "excel" => "Microsoft Excel",
                "powerpnt" => "PowerPoint",
                "OUTLOOK" => "Outlook",
                "TEAMS" => "Microsoft Teams",
                "slack" => "Slack",
                "explorer" => "File Explorer",
                "notepad" => "Notepad",
                "cmd" => "Command Prompt",
                "powershell" or "pwsh" => "PowerShell",
                _ => System.Globalization.CultureInfo.CurrentCulture.TextInfo
                         .ToTitleCase(processName.ToLower())
            };
        }

        private static ImageCodecInfo GetJpegEncoder()
            => ImageCodecInfo.GetImageEncoders().First(e => e.MimeType == "image/jpeg");

        public void Dispose()
        {
            Stop();
            _cts?.Dispose();
        }
    }
}
