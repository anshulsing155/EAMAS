using EAMAS.Core.Models;
using EAMAS.Core.Services;
using EAMAS.Desktop.Services;
using Microsoft.Win32;

namespace EAMAS.Desktop.ViewModels
{
    public class SettingsViewModel : BaseViewModel
    {
        private readonly SettingsService _settingsService;
        private readonly UserService _userService;
        private readonly AuditLogService _auditLogService;

        // ── Monitoring ────────────────────────────────────────────────────────────
        private bool _monitoringEnabled;
        private bool _screenshotsEnabled;
        private int _screenshotInterval;
        private int _idleThreshold;
        private int _maxScreenshotAge;
        private int _jpegQuality;
        private bool _privacyBlurEnabled;

        // ── Alerts ────────────────────────────────────────────────────────────────
        private bool _alertOnLongIdle;
        private int _longIdleThreshold;
        private bool _alertOnDistracting;
        private int _distractingThreshold;
        private bool _alertOnLowProductivity;
        private int _lowProductivityThreshold;
        private int _lowProductivityMinActive;
        private bool _alertOnUnauthorizedApp;
        private string _blockedApplications = string.Empty;
        private bool _alertOnNoActivity;
        private int _noActivityThreshold;

        // ── Data retention ────────────────────────────────────────────────────────
        private int _activityLogRetentionDays;
        private int _alertRetentionDays;
        private int _auditLogRetentionDays;

        // ── System / account ──────────────────────────────────────────────────────
        private bool _runOnStartup;
        private string _currentPassword = string.Empty;
        private string _newPassword = string.Empty;
        private string _confirmPassword = string.Empty;
        private string _statusMessage = string.Empty;
        private bool _isSaving;

        // ── Software update ───────────────────────────────────────────────────────
        private string _updateStatusMessage = string.Empty;
        private bool _updateAvailable;
        private bool _isCheckingUpdate;

        // ── Monitoring properties ─────────────────────────────────────────────────
        public bool MonitoringEnabled    { get => _monitoringEnabled; set => Set(ref _monitoringEnabled, value); }
        public bool ScreenshotsEnabled   { get => _screenshotsEnabled; set => Set(ref _screenshotsEnabled, value); }
        public int  ScreenshotInterval   { get => _screenshotInterval; set => Set(ref _screenshotInterval, value); }
        public int  IdleThreshold        { get => _idleThreshold; set => Set(ref _idleThreshold, value); }
        public int  MaxScreenshotAge     { get => _maxScreenshotAge; set => Set(ref _maxScreenshotAge, value); }
        public int  JpegQuality          { get => _jpegQuality; set => Set(ref _jpegQuality, value); }
        public bool PrivacyBlurEnabled   { get => _privacyBlurEnabled; set => Set(ref _privacyBlurEnabled, value); }

        // ── Alert properties ──────────────────────────────────────────────────────
        public bool AlertOnLongIdle          { get => _alertOnLongIdle; set => Set(ref _alertOnLongIdle, value); }
        public int  LongIdleThreshold        { get => _longIdleThreshold; set => Set(ref _longIdleThreshold, value); }
        public bool AlertOnDistracting       { get => _alertOnDistracting; set => Set(ref _alertOnDistracting, value); }
        public int  DistractingThreshold     { get => _distractingThreshold; set => Set(ref _distractingThreshold, value); }
        public bool AlertOnLowProductivity   { get => _alertOnLowProductivity; set => Set(ref _alertOnLowProductivity, value); }
        public int  LowProductivityThreshold { get => _lowProductivityThreshold; set => Set(ref _lowProductivityThreshold, value); }
        public int  LowProductivityMinActive { get => _lowProductivityMinActive; set => Set(ref _lowProductivityMinActive, value); }
        public bool AlertOnUnauthorizedApp   { get => _alertOnUnauthorizedApp; set => Set(ref _alertOnUnauthorizedApp, value); }
        public string BlockedApplications   { get => _blockedApplications; set => Set(ref _blockedApplications, value); }
        public bool AlertOnNoActivity        { get => _alertOnNoActivity; set => Set(ref _alertOnNoActivity, value); }
        public int  NoActivityThreshold      { get => _noActivityThreshold; set => Set(ref _noActivityThreshold, value); }

        // ── Retention properties ──────────────────────────────────────────────────
        public int ActivityLogRetentionDays { get => _activityLogRetentionDays; set => Set(ref _activityLogRetentionDays, value); }
        public int AlertRetentionDays       { get => _alertRetentionDays; set => Set(ref _alertRetentionDays, value); }
        public int AuditLogRetentionDays    { get => _auditLogRetentionDays; set => Set(ref _auditLogRetentionDays, value); }

        // ── Status / account ──────────────────────────────────────────────────────
        public string StatusMessage { get => _statusMessage; set => Set(ref _statusMessage, value); }
        public bool   IsSaving      { get => _isSaving; set => Set(ref _isSaving, value); }

        // ── Software update ───────────────────────────────────────────────────────
        public string CurrentAppVersion
        {
            get
            {
                var v = UpdateService.CurrentVersion;
                return $"v{v.Major}.{v.Minor}.{v.Build}";
            }
        }
        public string UpdateStatusMessage { get => _updateStatusMessage; set => Set(ref _updateStatusMessage, value); }
        public bool   UpdateAvailable     { get => _updateAvailable;     set => Set(ref _updateAvailable, value); }
        public bool   IsCheckingUpdate    { get => _isCheckingUpdate;    set => Set(ref _isCheckingUpdate, value); }

        public bool RunOnStartup
        {
            get => _runOnStartup;
            set { Set(ref _runOnStartup, value); SetStartupRegistry(value); }
        }

        // ── Computed / read-only ──────────────────────────────────────────────────
        public bool   IsAdmin        => App.CurrentUser?.Role is UserRole.Admin or UserRole.SuperAdmin;
        public bool   IsEmployee     => App.CurrentUser?.Role == UserRole.Employee;
        public string CurrentUserName => App.CurrentUser?.FullName ?? App.CurrentUser?.Username ?? "—";
        public string CurrentUserRole => App.CurrentUser?.Role.ToString() ?? "—";
        public string CurrentOrgName  => App.CurrentOrganization?.Name ?? "System";
        public bool   ConsentGiven    => App.CurrentUser?.ConsentGiven ?? false;

        public RelayCommand      SaveSettingsCommand    { get; }
        public RelayCommand      ChangePasswordCommand  { get; }
        public RelayCommand      RevokeConsentCommand   { get; }
        public AsyncRelayCommand CheckForUpdatesCommand { get; }
        public AsyncRelayCommand InstallUpdateCommand   { get; }

        public SettingsViewModel(SettingsService settingsService, UserService userService,
            AuditLogService auditLogService)
        {
            _settingsService = settingsService;
            _userService     = userService;
            _auditLogService = auditLogService;

            SaveSettingsCommand   = new RelayCommand(SaveSettings, () => IsAdmin);
            ChangePasswordCommand = new RelayCommand(
                () => ChangePassword(_currentPassword, _newPassword, _confirmPassword));
            RevokeConsentCommand  = new RelayCommand(RevokeConsent, () => IsEmployee && ConsentGiven);
            CheckForUpdatesCommand = new AsyncRelayCommand(CheckForUpdatesAsync,
                () => !_isCheckingUpdate);
            InstallUpdateCommand  = new AsyncRelayCommand(
                () => App.DownloadUpdate(), () => _updateAvailable);
        }

        public void Initialize()
        {
            _runOnStartup = IsStartupEnabled();
            OnPropertyChanged(nameof(RunOnStartup));

            if (!IsEmployee)
            {
                var s = _settingsService.GetSettings(App.CurrentOrgId);
                MonitoringEnabled    = s.MonitoringEnabled;
                ScreenshotsEnabled   = s.ScreenshotsEnabled;
                ScreenshotInterval   = s.ScreenshotIntervalMinutes;
                IdleThreshold        = s.IdleThresholdSeconds / 60;
                MaxScreenshotAge     = s.MaxScreenshotAgeDays;
                JpegQuality          = s.JpegQuality;
                PrivacyBlurEnabled   = s.PrivacyBlurEnabled;

                AlertOnLongIdle          = s.AlertOnLongIdle;
                LongIdleThreshold        = s.LongIdleThresholdMinutes;
                AlertOnDistracting       = s.AlertOnDistractingUsage;
                DistractingThreshold     = s.DistractingUsageThresholdMinutes;
                AlertOnLowProductivity   = s.AlertOnLowProductivity;
                LowProductivityThreshold = s.LowProductivityThresholdPercent;
                LowProductivityMinActive = s.LowProductivityMinActiveMinutes;
                AlertOnUnauthorizedApp   = s.AlertOnUnauthorizedApp;
                BlockedApplications      = s.BlockedApplications;
                AlertOnNoActivity        = s.AlertOnNoActivity;
                NoActivityThreshold      = s.NoActivityThresholdMinutes;

                ActivityLogRetentionDays = s.ActivityLogRetentionDays;
                AlertRetentionDays       = s.AlertRetentionDays;
                AuditLogRetentionDays    = s.AuditLogRetentionDays;
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
                if (LowProductivityThreshold is < 1 or > 99)
                { StatusMessage = "Low-productivity threshold must be 1–99%."; return; }
                if (NoActivityThreshold < 1)
                { StatusMessage = "No-activity threshold must be at least 1 minute."; return; }

                var existing = _settingsService.GetSettings(App.CurrentOrgId);
                existing.MonitoringEnabled           = MonitoringEnabled;
                existing.ScreenshotsEnabled          = ScreenshotsEnabled;
                existing.ScreenshotIntervalMinutes   = ScreenshotInterval;
                existing.IdleThresholdSeconds        = IdleThreshold * 60;
                existing.MaxScreenshotAgeDays        = MaxScreenshotAge;
                existing.JpegQuality                 = JpegQuality;
                existing.PrivacyBlurEnabled          = PrivacyBlurEnabled;
                existing.AlertOnLongIdle             = AlertOnLongIdle;
                existing.LongIdleThresholdMinutes    = LongIdleThreshold;
                existing.AlertOnDistractingUsage     = AlertOnDistracting;
                existing.DistractingUsageThresholdMinutes = DistractingThreshold;
                existing.AlertOnLowProductivity      = AlertOnLowProductivity;
                existing.LowProductivityThresholdPercent  = LowProductivityThreshold;
                existing.LowProductivityMinActiveMinutes  = LowProductivityMinActive;
                existing.AlertOnUnauthorizedApp      = AlertOnUnauthorizedApp;
                existing.BlockedApplications         = BlockedApplications;
                existing.AlertOnNoActivity           = AlertOnNoActivity;
                existing.NoActivityThresholdMinutes  = NoActivityThreshold;
                existing.ActivityLogRetentionDays    = ActivityLogRetentionDays;
                existing.AlertRetentionDays          = AlertRetentionDays;
                existing.AuditLogRetentionDays       = AuditLogRetentionDays;

                _settingsService.SaveSettings(existing);

                _auditLogService.Log(App.CurrentOrgId, App.CurrentUser!.Id,
                    App.CurrentUser.FullName, "SettingsChanged",
                    "Monitoring / alert settings updated.");

                StatusMessage = "Settings saved successfully.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error saving settings: {ex.Message}";
            }
            finally { IsSaving = false; }
        }

        public void SetPasswordInputs(string current, string newPwd, string confirm)
        {
            _currentPassword = current;
            _newPassword     = newPwd;
            _confirmPassword = confirm;
        }

        private void ChangePassword(string current, string newPwd, string confirm)
        {
            // ── Strength requirements ─────────────────────────────────────
            if (string.IsNullOrWhiteSpace(newPwd) || newPwd.Length < 8)
            { StatusMessage = "New password must be at least 8 characters."; return; }
            if (!newPwd.Any(char.IsUpper))
            { StatusMessage = "New password must contain at least one uppercase letter."; return; }
            if (!newPwd.Any(char.IsLower))
            { StatusMessage = "New password must contain at least one lowercase letter."; return; }
            if (!newPwd.Any(char.IsDigit))
            { StatusMessage = "New password must contain at least one digit."; return; }
            if (!newPwd.Any(c => !char.IsLetterOrDigit(c)))
            { StatusMessage = "New password must contain at least one special character."; return; }
            if (newPwd != confirm)
            { StatusMessage = "Passwords do not match."; return; }

            var currentUser = _userService.GetById(App.CurrentUser!.Id);
            if (currentUser == null || !UserService.VerifyPassword(current, currentUser.PasswordHash))
            { StatusMessage = "Current password is incorrect."; return; }

            _userService.ChangePassword(App.CurrentUser.Id, newPwd);

            // Clear forced-change flag if it was set (temporary password flow)
            if (currentUser.MustChangePassword)
                _userService.ClearMustChangePassword(App.CurrentUser.Id);

            _auditLogService.Log(App.CurrentOrgId, App.CurrentUser.Id,
                App.CurrentUser.FullName, "PasswordChanged", "User changed their own password.");

            StatusMessage = "Password changed successfully.";
        }

        private void RevokeConsent()
        {
            var result = System.Windows.MessageBox.Show(
                "Revoking your monitoring consent will log you out immediately " +
                "and monitoring will stop.\n\nProceed?",
                "Revoke Monitoring Consent",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (result != System.Windows.MessageBoxResult.Yes) return;

            _userService.SetConsent(App.CurrentUser!.Id, false);
            App.ExitApp();
        }

        // ── Software update ───────────────────────────────────────────────────────

        private async Task CheckForUpdatesAsync()
        {
            IsCheckingUpdate = true;
            UpdateStatusMessage = "Checking for updates...";
            UpdateAvailable = false;
            try
            {
                var svc = new UpdateService();
                var update = await svc.CheckForUpdateAsync().ConfigureAwait(false);
                if (update != null)
                {
                    App.SetPendingUpdate(update);
                    UpdateAvailable = true;
                    UpdateStatusMessage = $"Version {update.Version} is available.";
                }
                else
                {
                    UpdateStatusMessage = $"You are up to date ({CurrentAppVersion}).";
                }
            }
            catch
            {
                UpdateStatusMessage = "Could not reach the update server. Check your connection.";
            }
            finally
            {
                IsCheckingUpdate = false;
            }
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
                    var exe = System.Diagnostics.Process.GetCurrentProcess()
                                  .MainModule?.FileName ?? string.Empty;
                    key.SetValue("EAMAS", $"\"{exe}\"");
                }
                else
                {
                    key.DeleteValue("EAMAS", throwOnMissingValue: false);
                }
            }
            catch { }
        }
    }
}
