using EAMAS.Core.Models;
using EAMAS.Core.Services;
using Microsoft.Win32;

namespace EAMAS.Desktop.ViewModels
{
    public class SettingsViewModel : BaseViewModel
    {
        private readonly SettingsService _settingsService;
        private readonly UserService _userService;

        private bool _monitoringEnabled;
        private bool _screenshotsEnabled;
        private int _screenshotInterval;
        private int _idleThreshold;
        private int _maxScreenshotAge;
        private int _jpegQuality;
        private bool _alertOnLongIdle;
        private int _longIdleThreshold;
        private bool _alertOnDistracting;
        private int _distractingThreshold;
        private bool _runOnStartup;
        private string _currentPassword = string.Empty;
        private string _newPassword = string.Empty;
        private string _confirmPassword = string.Empty;
        private string _statusMessage = string.Empty;
        private bool _isSaving;

        public bool MonitoringEnabled { get => _monitoringEnabled; set => Set(ref _monitoringEnabled, value); }
        public bool ScreenshotsEnabled { get => _screenshotsEnabled; set => Set(ref _screenshotsEnabled, value); }
        public int ScreenshotInterval { get => _screenshotInterval; set => Set(ref _screenshotInterval, value); }
        public int IdleThreshold { get => _idleThreshold; set => Set(ref _idleThreshold, value); }
        public int MaxScreenshotAge { get => _maxScreenshotAge; set => Set(ref _maxScreenshotAge, value); }
        public int JpegQuality { get => _jpegQuality; set => Set(ref _jpegQuality, value); }
        public bool AlertOnLongIdle { get => _alertOnLongIdle; set => Set(ref _alertOnLongIdle, value); }
        public int LongIdleThreshold { get => _longIdleThreshold; set => Set(ref _longIdleThreshold, value); }
        public bool AlertOnDistracting { get => _alertOnDistracting; set => Set(ref _alertOnDistracting, value); }
        public int DistractingThreshold { get => _distractingThreshold; set => Set(ref _distractingThreshold, value); }
        public string StatusMessage { get => _statusMessage; set => Set(ref _statusMessage, value); }
        public bool IsSaving { get => _isSaving; set => Set(ref _isSaving, value); }

        public bool RunOnStartup
        {
            get => _runOnStartup;
            set
            {
                Set(ref _runOnStartup, value);
                SetStartupRegistry(value);
            }
        }

        public bool IsAdmin => App.CurrentUser?.Role is UserRole.Admin or UserRole.SuperAdmin;
        public bool IsEmployee => App.CurrentUser?.Role == UserRole.Employee;
        public string CurrentUserName => App.CurrentUser?.FullName ?? App.CurrentUser?.Username ?? "—";
        public string CurrentUserRole => App.CurrentUser?.Role.ToString() ?? "—";
        public string CurrentOrgName => App.CurrentOrganization?.Name ?? "System";

        public RelayCommand SaveSettingsCommand { get; }
        public RelayCommand ChangePasswordCommand { get; }

        public SettingsViewModel(SettingsService settingsService, UserService userService)
        {
            _settingsService = settingsService;
            _userService = userService;
            SaveSettingsCommand = new RelayCommand(SaveSettings, () => IsAdmin);
            ChangePasswordCommand = new RelayCommand(
                () => ChangePassword(_currentPassword, _newPassword, _confirmPassword));
        }

        public void Initialize()
        {
            _runOnStartup = IsStartupEnabled();
            OnPropertyChanged(nameof(RunOnStartup));

            if (!IsEmployee)
            {
                var s = _settingsService.GetSettings(App.CurrentOrgId);
                MonitoringEnabled = s.MonitoringEnabled;
                ScreenshotsEnabled = s.ScreenshotsEnabled;
                ScreenshotInterval = s.ScreenshotIntervalMinutes;
                IdleThreshold = s.IdleThresholdSeconds / 60;
                MaxScreenshotAge = s.MaxScreenshotAgeDays;
                JpegQuality = s.JpegQuality;
                AlertOnLongIdle = s.AlertOnLongIdle;
                LongIdleThreshold = s.LongIdleThresholdMinutes;
                AlertOnDistracting = s.AlertOnDistractingUsage;
                DistractingThreshold = s.DistractingUsageThresholdMinutes;
            }
        }

        private void SaveSettings()
        {
            IsSaving = true;
            StatusMessage = string.Empty;
            try
            {
                if (ScreenshotInterval < 1)
                { StatusMessage = "Screenshot interval must be at least 1 minute."; return; }
                if (IdleThreshold < 1)
                { StatusMessage = "Idle threshold must be at least 1 minute."; return; }
                if (MaxScreenshotAge < 1)
                { StatusMessage = "Max screenshot age must be at least 1 day."; return; }
                if (JpegQuality is < 1 or > 100)
                { StatusMessage = "Screenshot quality must be between 1 and 100."; return; }
                if (LongIdleThreshold < 1 || DistractingThreshold < 1)
                { StatusMessage = "Alert thresholds must be at least 1 minute."; return; }

                var existing = _settingsService.GetSettings(App.CurrentOrgId);
                existing.MonitoringEnabled = MonitoringEnabled;
                existing.ScreenshotsEnabled = ScreenshotsEnabled;
                existing.ScreenshotIntervalMinutes = ScreenshotInterval;
                existing.IdleThresholdSeconds = IdleThreshold * 60;
                existing.MaxScreenshotAgeDays = MaxScreenshotAge;
                existing.JpegQuality = JpegQuality;
                existing.AlertOnLongIdle = AlertOnLongIdle;
                existing.LongIdleThresholdMinutes = LongIdleThreshold;
                existing.AlertOnDistractingUsage = AlertOnDistracting;
                existing.DistractingUsageThresholdMinutes = DistractingThreshold;

                _settingsService.SaveSettings(existing);
                StatusMessage = "Settings saved successfully.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error saving settings: {ex.Message}";
            }
            finally
            {
                IsSaving = false;
            }
        }

        public void SetPasswordInputs(string current, string newPwd, string confirm)
        {
            _currentPassword = current;
            _newPassword = newPwd;
            _confirmPassword = confirm;
        }

        private void ChangePassword(string current, string newPwd, string confirm)
        {
            if (string.IsNullOrWhiteSpace(newPwd) || newPwd.Length < 6)
            { StatusMessage = "New password must be at least 6 characters."; return; }
            if (newPwd != confirm)
            { StatusMessage = "Passwords do not match."; return; }

            var currentUser = _userService.GetById(App.CurrentUser!.Id);
            if (currentUser == null || !UserService.VerifyPassword(current, currentUser.PasswordHash))
            { StatusMessage = "Current password is incorrect."; return; }

            _userService.ChangePassword(App.CurrentUser.Id, newPwd);
            StatusMessage = "Password changed successfully.";
        }

        // ── Windows Startup Registry ──────────────────────────────────────────────

        private static readonly string _startupKey =
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

        private static bool IsStartupEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(_startupKey);
                return key?.GetValue("EAMAS") != null;
            }
            catch { return false; }
        }

        private static void SetStartupRegistry(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(_startupKey, writable: true);
                if (key == null) return;

                if (enable)
                {
                    var exePath = System.Diagnostics.Process.GetCurrentProcess()
                                      .MainModule?.FileName ?? string.Empty;
                    key.SetValue("EAMAS", $"\"{exePath}\"");
                }
                else
                {
                    key.DeleteValue("EAMAS", throwOnMissingValue: false);
                }
            }
            catch { /* registry access can fail in restricted environments */ }
        }
    }
}
