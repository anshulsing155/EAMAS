using EAMAS.Core.Models;
using EAMAS.Core.Services;
using System.Windows;

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
        private string _screenshotsDir = string.Empty;
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
        public string ScreenshotsDir { get => _screenshotsDir; set => Set(ref _screenshotsDir, value); }
        public string StatusMessage { get => _statusMessage; set => Set(ref _statusMessage, value); }
        public bool IsSaving { get => _isSaving; set => Set(ref _isSaving, value); }

        public bool IsAdmin => App.CurrentUser?.Role is UserRole.Admin or UserRole.SuperAdmin;
        public string CurrentUserName => App.CurrentUser?.FullName ?? App.CurrentUser?.Username ?? "—";
        public string CurrentUserRole => App.CurrentUser?.Role.ToString() ?? "—";
        public string CurrentOrgName => App.CurrentOrganization?.Name ?? "System";

        public RelayCommand SaveSettingsCommand { get; }
        public RelayCommand BrowseFolderCommand { get; }
        public RelayCommand ChangePasswordCommand { get; }

        public SettingsViewModel(SettingsService settingsService, UserService userService)
        {
            _settingsService = settingsService;
            _userService = userService;
            SaveSettingsCommand = new RelayCommand(SaveSettings, () => IsAdmin);
            BrowseFolderCommand = new RelayCommand(BrowseFolder, () => IsAdmin);
            ChangePasswordCommand = new RelayCommand(
                () => ChangePassword(_currentPassword, _newPassword, _confirmPassword));
        }

        public void Initialize()
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
            ScreenshotsDir = string.IsNullOrEmpty(s.ScreenshotsDirectory)
                ? SettingsService.GetDefaultScreenshotsDirectory(App.CurrentOrgId)
                : s.ScreenshotsDirectory;
        }

        private void SaveSettings()
        {
            IsSaving = true;
            try
            {
                _settingsService.SaveSettings(new SystemSettings
                {
                    OrganizationId = App.CurrentOrgId,
                    MonitoringEnabled = MonitoringEnabled,
                    ScreenshotsEnabled = ScreenshotsEnabled,
                    ScreenshotIntervalMinutes = ScreenshotInterval,
                    IdleThresholdSeconds = IdleThreshold * 60,
                    MaxScreenshotAgeDays = MaxScreenshotAge,
                    JpegQuality = JpegQuality,
                    AlertOnLongIdle = AlertOnLongIdle,
                    LongIdleThresholdMinutes = LongIdleThreshold,
                    AlertOnDistractingUsage = AlertOnDistracting,
                    DistractingUsageThresholdMinutes = DistractingThreshold,
                    ScreenshotsDirectory = ScreenshotsDir
                });
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

        private void BrowseFolder()
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select Screenshots Folder",
                SelectedPath = ScreenshotsDir
            };
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                ScreenshotsDir = dialog.SelectedPath;
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
            {
                StatusMessage = "New password must be at least 6 characters.";
                return;
            }
            if (newPwd != confirm)
            {
                StatusMessage = "Passwords do not match.";
                return;
            }
            // Verify current password by re-authenticating
            var user = _userService.Authenticate(
                App.CurrentUser!.OrganizationId,
                App.CurrentUser.Username,
                current);
            if (user == null)
            {
                StatusMessage = "Current password is incorrect.";
                return;
            }
            _userService.ChangePassword(App.CurrentUser.Id, newPwd);
            StatusMessage = "Password changed successfully.";
        }
    }
}
