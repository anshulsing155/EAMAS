using EAMAS.Core.Enums;
using EAMAS.Core.Models;
using EAMAS.Core.Services;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace EAMAS.Desktop.Services
{
    public class MonitoringBackgroundService : IDisposable
    {
        private readonly ActivityMonitorService _activityService;
        private readonly ScreenshotService _screenshotService;
        private readonly AlertService _alertService;
        private readonly SettingsService _settingsService;
        private readonly ScreenshotPrivacyService _privacyService;
        private readonly TimeIntegrityService _timeIntegrity;

        private CancellationTokenSource? _cts;
        private Task? _activityTask;
        private Task? _screenshotTask;
        private Task? _purgeTask;

        private string _currentOrgId  = string.Empty;
        private string _currentUserId = string.Empty;
        private bool _isRunning;

        // Current window session tracking
        private IntPtr _lastWindow  = IntPtr.Zero;
        private string _lastProcess = string.Empty;
        private string _lastTitle   = string.Empty;
        private DateTime _sessionStart = DateTime.MinValue;
        private TimeSpan _sessionMonotonicStart;  // Stopwatch snapshot at session start
        private bool _wasIdle = false;
        private volatile bool _isScreenLocked = false;

        // Clock-jump detection state
        private int _clockJumpCount;
        private DateTime _lastClockJumpAlert = DateTime.MinValue;

        // Cached today's productivity score (refreshed every 5 minutes)
        private int _cachedProductivityScore;
        private DateTime _productivityScoreCachedAt = DateTime.MinValue;

        /// <summary>Fired when the foreground application changes.</summary>
        public event Action<string, string>? ActivityChanged;

        public bool IsRunning => _isRunning;

        public MonitoringBackgroundService(
            ActivityMonitorService activityService,
            ScreenshotService screenshotService,
            AlertService alertService,
            SettingsService settingsService,
            ScreenshotPrivacyService privacyService,
            TimeIntegrityService timeIntegrity)
        {
            _activityService = activityService;
            _screenshotService = screenshotService;
            _alertService = alertService;
            _settingsService = settingsService;
            _privacyService = privacyService;
            _timeIntegrity = timeIntegrity;

            Microsoft.Win32.SystemEvents.SessionSwitch += OnSessionSwitch;
        }

        /// <summary>Start monitoring for the given user. Not called for SuperAdmin.</summary>
        public void Start(string orgId, string userId)
        {
            if (_isRunning) return;
            _currentOrgId  = orgId;
            _currentUserId = userId;
            _isRunning = true;
            _cts = new CancellationTokenSource();

            _activityTask   = Task.Run(() => ActivityLoop(_cts.Token),   _cts.Token);
            _screenshotTask = Task.Run(() => ScreenshotLoop(_cts.Token), _cts.Token);
            _purgeTask      = Task.Run(() => PurgeLoop(_cts.Token),      _cts.Token);
        }

        public void Stop()
        {
            if (!_isRunning) return;
            _isRunning = false;
            _cts?.Cancel();
            var now = _timeIntegrity.GetTrustedUtcNow();
            if (_isScreenLocked && _sessionStart != DateTime.MinValue)
                FlushSession(_lastProcess, _lastTitle, _sessionStart, now, isIdle: false, isScreenLocked: true);
            else
                FlushCurrentSession(now);
        }

        private void OnSessionSwitch(object sender, Microsoft.Win32.SessionSwitchEventArgs e)
        {
            if (!_isRunning) return;
            var now = _timeIntegrity.GetTrustedUtcNow();

            if (e.Reason == Microsoft.Win32.SessionSwitchReason.SessionLock && !_isScreenLocked)
            {
                FlushCurrentSession(now);
                _isScreenLocked = true;
                _sessionStart   = now;
                _sessionMonotonicStart = _timeIntegrity.GetMonotonicSnapshot();
                _lastProcess    = "ScreenLock";
                _lastTitle      = "Screen Locked";
                _wasIdle        = false;
            }
            else if (e.Reason == Microsoft.Win32.SessionSwitchReason.SessionUnlock && _isScreenLocked)
            {
                FlushSession(_lastProcess, _lastTitle, _sessionStart, now, isIdle: false, isScreenLocked: true);
                _isScreenLocked = false;
                _sessionStart   = DateTime.MinValue;
                _lastProcess    = string.Empty;
                _lastTitle      = string.Empty;
                _lastWindow     = IntPtr.Zero;
            }
        }

        /// <summary>Trigger an immediate manual screenshot capture.</summary>
        public void TriggerScreenshot()
        {
            var settings = _settingsService.GetSettings(_currentOrgId);
            if (!settings.MonitoringEnabled || !settings.ScreenshotsEnabled) return;
            Task.Run(() => CaptureAndSaveAsync(settings, isManual: true));
        }

        // ── Activity Loop ────────────────────────────────────────────────────────

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
                catch (TaskCanceledException) { return; }
                catch (Exception ex)
                {
                    // Log the exception so real bugs are visible; then back off briefly.
                    System.Diagnostics.Debug.WriteLine(
                        $"[MonitoringBackgroundService] ActivityLoop error: {ex.GetType().Name}: {ex.Message}");
                    await Task.Delay(2000, ct).ConfigureAwait(false);
                }
            }
        }

        private void PollActivity(SystemSettings settings)
        {
            if (_isScreenLocked) return;

            var idleTime = WindowsApiService.GetIdleTime();
            var isIdle   = idleTime.TotalSeconds >= settings.IdleThresholdSeconds;
            var now      = _timeIntegrity.GetTrustedUtcNow();

            // ── Clock-manipulation detection ─────────────────────────────────────
            var clockJump = _timeIntegrity.DetectClockJump();
            if (clockJump.HasValue)
            {
                _clockJumpCount++;
                Debug.WriteLine($"[TimeIntegrity] Clock jump detected: {clockJump.Value.TotalSeconds:F1}s (count: {_clockJumpCount})");

                // Fire a ClockManipulation alert (at most once per hour)
                if ((now - _lastClockJumpAlert).TotalHours >= 1)
                {
                    var direction = clockJump.Value > TimeSpan.Zero ? "forward" : "backward";
                    _alertService.CreateAlert(_currentOrgId, _currentUserId,
                        AlertType.ClockManipulation,
                        $"System clock jumped {direction} by {clockJump.Value.Duration().TotalMinutes:F1} minutes. " +
                        $"Activity session durations have been automatically corrected.");
                    _lastClockJumpAlert = now;
                }

                // Discard the current session — its timestamps are now unreliable.
                // Reset session start to the trusted "now" so the next session is clean.
                _sessionStart = now;
                _sessionMonotonicStart = _timeIntegrity.GetMonotonicSnapshot();
            }

            if (isIdle)
            {
                if (!_wasIdle)
                {
                    FlushCurrentSession(now);
                    _wasIdle      = true;
                    _sessionStart = now - idleTime;
                    _sessionMonotonicStart = _timeIntegrity.GetMonotonicSnapshot() - idleTime;
                    _lastProcess  = "Idle";
                    _lastTitle    = "System Idle";
                }

                var distracting = GetTodayDistractingTime();
                var activeTime  = GetTodayActiveTime();
                var score       = GetTodayProductivityScore();

                _alertService.CheckAndGenerateAlerts(
                    _currentOrgId, _currentUserId, settings,
                    idleTime, distracting, score, activeTime, _lastProcess);
                return;
            }

            var hWnd    = WindowsApiService.GetForegroundWindow();
            if (hWnd == IntPtr.Zero) return;

            var pid     = WindowsApiService.GetProcessId(hWnd);
            var process = WindowsApiService.GetProcessName(pid);
            var title   = WindowsApiService.GetWindowTitle(hWnd);

            if (_wasIdle)
            {
                FlushSession(_lastProcess, _lastTitle, _sessionStart, now, isIdle: true, isScreenLocked: false);
                _wasIdle      = false;
                _sessionStart = now;
                _sessionMonotonicStart = _timeIntegrity.GetMonotonicSnapshot();
                _lastProcess  = process;
                _lastTitle    = title;
                _lastWindow   = hWnd;

                ActivityChanged?.Invoke(FormatAppName(process), title);
                return;
            }

            if (hWnd == _lastWindow && process == _lastProcess) return;

            if (_sessionStart != DateTime.MinValue)
                FlushCurrentSession(now);

            _lastWindow  = hWnd;
            _lastProcess = process;
            _lastTitle   = title;
            _sessionStart = now;
            _sessionMonotonicStart = _timeIntegrity.GetMonotonicSnapshot();

            ActivityChanged?.Invoke(FormatAppName(process), title);

            // Check unauthorized-app alert on every process switch
            if (settings.AlertOnUnauthorizedApp)
            {
                var distracting = GetTodayDistractingTime();
                var activeTime  = GetTodayActiveTime();
                var score       = GetTodayProductivityScore();

                _alertService.CheckAndGenerateAlerts(
                    _currentOrgId, _currentUserId, settings,
                    currentIdleTime: TimeSpan.Zero, distractingTimeToday: distracting,
                    productivityScore: score, activeTimeToday: activeTime,
                    currentProcessName: process);
            }
        }

        private void FlushCurrentSession(DateTime end)
        {
            if (_sessionStart == DateTime.MinValue || string.IsNullOrEmpty(_lastProcess)) return;
            FlushSession(_lastProcess, _lastTitle, _sessionStart, end, _wasIdle, isScreenLocked: false);
            _sessionStart = DateTime.MinValue;
        }

        private void FlushSession(string process, string title, DateTime start, DateTime end, bool isIdle, bool isScreenLocked)
        {
            if (end <= start || (end - start).TotalSeconds < 2) return;

            // ── Validate duration against monotonic clock ────────────────────────
            var (validStart, validEnd, wasAdjusted) = _timeIntegrity.ValidateSessionDuration(
                start, end, _sessionMonotonicStart);

            if (validEnd <= validStart || (validEnd - validStart).TotalSeconds < 2) return;

            var log = new ActivityLog
            {
                OrganizationId   = _currentOrgId,
                UserId           = _currentUserId,
                StartTime        = validStart,
                EndTime          = validEnd,
                ProcessName      = process,
                ApplicationName  = isScreenLocked ? "Screen Lock" : FormatAppName(process),
                WindowTitle      = title,
                IsIdle           = isIdle,
                IsScreenLocked   = isScreenLocked,
                WasClockAdjusted = wasAdjusted,
                OriginalEndTime  = wasAdjusted ? end : null
            };

            _activityService.RecordActivity(log);
        }

        // ── Screenshot Loop ──────────────────────────────────────────────────────

        private async Task ScreenshotLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                var settings        = _settingsService.GetSettings(_currentOrgId);
                var baseMinutes     = Math.Max(1, settings.ScreenshotIntervalMinutes);

                // ±40 % random jitter so capture times are unpredictable.
                // e.g. 5 min base → actual delay is anywhere from 3 to 7 minutes.
                var jitter       = baseMinutes * 0.4;
                var actualMinutes= baseMinutes + (Random.Shared.NextDouble() * 2 - 1) * jitter;
                actualMinutes    = Math.Max(0.5, actualMinutes);  // never shorter than 30 s

                await Task.Delay(TimeSpan.FromMinutes(actualMinutes), ct).ConfigureAwait(false);
                if (ct.IsCancellationRequested) break;

                try
                {
                    settings = _settingsService.GetSettings(_currentOrgId);
                    if (settings.MonitoringEnabled && settings.ScreenshotsEnabled)
                        await CaptureAndSaveAsync(settings, isManual: false).ConfigureAwait(false);
                }
                catch (TaskCanceledException) { return; }
                catch { /* swallow individual screenshot failures */ }
            }
        }

        // ── Purge Loop (runs once at startup + every 24 h) ───────────────────────

        private async Task PurgeLoop(CancellationToken ct)
        {
            // Small initial delay so startup work finishes first
            try { await Task.Delay(TimeSpan.FromSeconds(30), ct).ConfigureAwait(false); }
            catch (TaskCanceledException) { return; }

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    _screenshotService.PurgeOldScreenshots(_currentOrgId);
                }
                catch { /* never crash the background service on a purge failure */ }

                try { await Task.Delay(TimeSpan.FromHours(24), ct).ConfigureAwait(false); }
                catch (TaskCanceledException) { return; }
            }
        }

        /// <summary>
        /// Captures the primary screen, applies privacy blur if sensitive content is
        /// detected, uploads to MongoDB GridFS, and saves the record.
        /// No files are written to local disk.
        /// </summary>
        private async Task CaptureAndSaveAsync(SystemSettings settings, bool isManual)
        {
            // Snapshot the active window *before* the capture so metadata matches the image.
            var hWnd        = WindowsApiService.GetForegroundWindow();
            var pid         = WindowsApiService.GetProcessId(hWnd);
            var processName = WindowsApiService.GetProcessName(pid);
            var windowTitle = WindowsApiService.GetWindowTitle(hWnd);
            var currentApp  = FormatAppName(processName);

            var bounds = System.Windows.Forms.Screen.PrimaryScreen!.Bounds;

            using var bmp = new Bitmap(bounds.Width, bounds.Height);
            using (var g  = Graphics.FromImage(bmp))
                g.CopyFromScreen(System.Drawing.Point.Empty, System.Drawing.Point.Empty, bounds.Size);

            var jpegEncoder = GetJpegEncoder();
            var quality     = (long)Math.Clamp(settings.JpegQuality, 1, 100);
            var encParams   = new EncoderParameters(1);
            encParams.Param[0] = new EncoderParameter(Encoder.Quality, quality);

            // ── Full screenshot → bytes ───────────────────────────────────────────
            byte[] fullBytes;
            using (var fullMs = new MemoryStream())
            {
                bmp.Save(fullMs, jpegEncoder, encParams);
                fullBytes = fullMs.ToArray();
            }

            // ── Privacy blur ──────────────────────────────────────────────────────
            bool isPrivacyBlurred    = false;
            string privacyBlurLevel  = "None";
            string? privacyBlurReason = null;

            if (settings.PrivacyBlurEnabled)
            {
                var (level, reason) = _privacyService.Detect(processName, windowTitle);
                if (level != PrivacyBlurLevel.None)
                {
                    fullBytes        = _privacyService.ApplyBlur(fullBytes, level, settings.JpegQuality);
                    isPrivacyBlurred = true;
                    privacyBlurLevel = level.ToString();
                    privacyBlurReason = reason;
                }
            }

            // ── Thumbnail 240×135 ─────────────────────────────────────────────────
            byte[] thumbBytes;
            using (var thumbBmp = new Bitmap(240, 135))
            {
                using var tg = Graphics.FromImage(thumbBmp);
                tg.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                tg.DrawImage(bmp, 0, 0, 240, 135);

                using var thumbMs = new MemoryStream();
                thumbBmp.Save(thumbMs, jpegEncoder, encParams);
                thumbBytes = thumbMs.ToArray();
            }

            // If the full image was blurred, regenerate the thumbnail from the blurred bytes
            // so the thumbnail shown in the gallery also reflects the blur.
            if (isPrivacyBlurred)
            {
                using var blurredMs  = new MemoryStream(fullBytes);
                using var blurredBmp = new Bitmap(blurredMs);
                using var tBmp       = new Bitmap(240, 135);
                using var tg         = Graphics.FromImage(tBmp);
                tg.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                tg.DrawImage(blurredBmp, 0, 0, 240, 135);

                using var thumbMs2 = new MemoryStream();
                tBmp.Save(thumbMs2, jpegEncoder, encParams);
                thumbBytes = thumbMs2.ToArray();
            }

            var record = new Core.Models.ScreenshotRecord
            {
                OrganizationId   = _currentOrgId,
                UserId           = _currentUserId,
                TakenAt          = _timeIntegrity.GetTrustedUtcNow(),   // tamper-resistant timestamp
                ApplicationName  = currentApp,
                IsManual         = isManual,
                IsPrivacyBlurred = isPrivacyBlurred,
                PrivacyBlurLevel = privacyBlurLevel,
                PrivacyBlurReason = privacyBlurReason
            };

            await _screenshotService.SaveScreenshotAsync(fullBytes, thumbBytes, record)
                .ConfigureAwait(false);
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private TimeSpan GetTodayDistractingTime()
        {
            try
            {
                var today = DateTime.Today;
                var usage = _activityService.GetAppUsage(
                    _currentOrgId, _currentUserId, today, today.AddDays(1));
                var ticks = usage
                    .Where(u => u.Category == ActivityCategory.Distracting)
                    .Sum(u => u.Duration.Ticks);
                return TimeSpan.FromTicks(ticks);
            }
            catch { return TimeSpan.Zero; }
        }

        private TimeSpan GetTodayActiveTime()
        {
            try
            {
                var today = DateTime.Today;
                var usage = _activityService.GetAppUsage(
                    _currentOrgId, _currentUserId, today, today.AddDays(1));
                var ticks = usage
                    .Where(u => u.Category != ActivityCategory.Unknown)
                    .Sum(u => u.Duration.Ticks);
                return TimeSpan.FromTicks(ticks);
            }
            catch { return TimeSpan.Zero; }
        }

        /// <summary>
        /// Returns today's productivity score (0–100). The result is cached for 5 minutes
        /// to avoid hammering the database on every poll cycle.
        /// </summary>
        private int GetTodayProductivityScore()
        {
            if ((DateTime.UtcNow - _productivityScoreCachedAt).TotalMinutes < 5)
                return _cachedProductivityScore;

            try
            {
                var today  = DateTime.Today;
                var usage  = _activityService.GetAppUsage(
                    _currentOrgId, _currentUserId, today, today.AddDays(1));

                var active      = TimeSpan.FromTicks(usage.Where(u => u.Category != ActivityCategory.Unknown).Sum(u => u.Duration.Ticks));
                var productive  = TimeSpan.FromTicks(usage.Where(u => u.Category == ActivityCategory.Productive).Sum(u => u.Duration.Ticks));
                var distracting = TimeSpan.FromTicks(usage.Where(u => u.Category == ActivityCategory.Distracting).Sum(u => u.Duration.Ticks));

                int score = active.TotalMinutes > 0
                    ? (int)Math.Clamp(
                        (productive.TotalMinutes - distracting.TotalMinutes * 0.5) /
                        active.TotalMinutes * 100, 0, 100)
                    : 0;

                _cachedProductivityScore  = score;
                _productivityScoreCachedAt = DateTime.UtcNow;
                return score;
            }
            catch { return _cachedProductivityScore; }
        }

        private static string FormatAppName(string processName)
        {
            if (string.IsNullOrEmpty(processName)) return "Unknown";
            return processName switch
            {
                "devenv"              => "Visual Studio",
                "code"                => "VS Code",
                "chrome"              => "Google Chrome",
                "firefox"             => "Mozilla Firefox",
                "msedge"              => "Microsoft Edge",
                "winword"             => "Microsoft Word",
                "excel"               => "Microsoft Excel",
                "powerpnt"            => "PowerPoint",
                "OUTLOOK"             => "Outlook",
                "TEAMS"               => "Microsoft Teams",
                "slack"               => "Slack",
                "explorer"            => "File Explorer",
                "notepad"             => "Notepad",
                "cmd"                 => "Command Prompt",
                "powershell" or "pwsh" => "PowerShell",
                _ => System.Globalization.CultureInfo.CurrentCulture.TextInfo
                         .ToTitleCase(processName.ToLower())
            };
        }

        private static ImageCodecInfo GetJpegEncoder()
            => ImageCodecInfo.GetImageEncoders().First(e => e.MimeType == "image/jpeg");

        public void Dispose()
        {
            Microsoft.Win32.SystemEvents.SessionSwitch -= OnSessionSwitch;
            Stop();
            _cts?.Dispose();
        }
    }
}
