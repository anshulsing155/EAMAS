using EAMAS.Core.Enums;
using EAMAS.Core.Models;
using EAMAS.Core.Services;
using EAMAS.Desktop.Views;
using System.Windows;

namespace EAMAS.Desktop.ViewModels
{
    public class LoginViewModel : BaseViewModel
    {
        private readonly UserService _userService;
        private readonly OrganizationService _orgService;

        private string _organizationCode = string.Empty;
        private string _username = string.Empty;
        private string _errorMessage = string.Empty;
        private bool _isLoading;

        public string OrganizationCode
        {
            get => _organizationCode;
            set => Set(ref _organizationCode, value);
        }

        public string Username
        {
            get => _username;
            set => Set(ref _username, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => Set(ref _errorMessage, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => Set(ref _isLoading, value);
        }

        public LoginViewModel(UserService userService, OrganizationService orgService)
        {
            _userService = userService;
            _orgService = orgService;
        }

        public void Login(string password)
        {
            if (string.IsNullOrWhiteSpace(OrganizationCode) ||
                string.IsNullOrWhiteSpace(Username) ||
                string.IsNullOrWhiteSpace(password))
            {
                ErrorMessage = "Please enter organisation code, username and password.";
                return;
            }

            ErrorMessage = string.Empty;
            IsLoading = true;

            try
            {
                var orgCode = OrganizationCode.Trim().ToUpperInvariant();

                // Resolve the effective OrganizationId
                string organizationId;
                Organization? org = null;

                if (orgCode == "SYSTEM")
                {
                    // SuperAdmin login
                    organizationId = "SYSTEM";
                }
                else
                {
                    org = _orgService.GetByCode(orgCode);
                    if (org == null)
                    {
                        ErrorMessage = "Organisation not found or inactive.";
                        return;
                    }
                    organizationId = org.Id;
                }

                var user = _userService.Authenticate(
                    organizationId, Username.Trim(), password, out var failReason);

                if (user == null)
                {
                    ErrorMessage = failReason switch
                    {
                        AuthFailReason.AccountLocked =>
                            $"Account is temporarily locked after too many failed attempts. " +
                            $"Try again in {_userService.GetRemainingLockoutMinutes(organizationId, Username.Trim())} minute(s).",
                        _ => "Invalid username or password."
                    };
                    return;
                }

                // ── Auto-expire stale sessions (> 8 h) ──────────────
                if (!string.IsNullOrEmpty(user.ActiveSessionToken) &&
                    user.SessionStartedAt.HasValue &&
                    (DateTime.UtcNow - user.SessionStartedAt.Value).TotalHours > 8)
                {
                    _userService.ForceCloseSession(user.Id);
                    user = _userService.GetById(user.Id) ?? user; // clear token on local object
                }

                // ── Single-session enforcement ───────────────────────
                if (!string.IsNullOrEmpty(user.ActiveSessionToken))
                {
                    var when = user.SessionStartedAt.HasValue
                        ? user.SessionStartedAt.Value.ToLocalTime().ToString("dd MMM yyyy HH:mm")
                        : "unknown time";
                    var machine = user.SessionMachine ?? "another computer";

                    var choice = System.Windows.MessageBox.Show(
                        $"This account is already logged in on \"{machine}\" (since {when}).\n\n" +
                        "Sign out the other session and continue here?",
                        "Account Already Active",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (choice == System.Windows.MessageBoxResult.No)
                    {
                        ErrorMessage = "Login cancelled — another session is active.";
                        return;
                    }

                    // Force-clear the stale/other session
                    _userService.ForceCloseSession(user.Id);
                }

                // Open a new session token in MongoDB
                var sessionToken = _userService.OpenSession(user.Id);
                App.CurrentSessionToken = sessionToken;

                App.CurrentUser = user;
                App.CurrentOrganization = org; // null for SuperAdmin

                // Consent check for employees
                if (!user.ConsentGiven && user.Role == UserRole.Employee)
                {
                    var result = System.Windows.MessageBox.Show(
                        "EAMAS monitors your screen activity, application usage, " +
                        "and takes periodic screenshots for productivity tracking.\n\n" +
                        "Do you consent to this monitoring?",
                        "Monitoring Consent Required",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (result == System.Windows.MessageBoxResult.No)
                    {
                        // Close the session token we just opened so it doesn't stay orphaned
                        _userService.CloseSession(user.Id, sessionToken);
                        App.CurrentSessionToken = null;
                        ErrorMessage = "Monitoring consent is required to proceed.";
                        App.CurrentUser = null;
                        App.CurrentOrganization = null;
                        return;
                    }

                    _userService.SetConsent(user.Id, true);
                }

                // ── Forced password change ───────────────────────────
                if (user.MustChangePassword)
                {
                    var pwdMsg = "Your account was created with a temporary password.\n" +
                                 "You must set a new password before continuing.";
                    System.Windows.MessageBox.Show(pwdMsg, "Password Change Required",
                        MessageBoxButton.OK, MessageBoxImage.Warning);

                    // Navigate to settings for password change instead of the dashboard.
                    // The MainWindow will open but the password-change dialog is shown first.
                    // (SettingsView enforces the change and clears MustChangePassword on success.)
                }

                var mainWindow = App.Services.GetService(typeof(MainWindow)) as MainWindow;
                mainWindow?.Show();

                // Update tray to show logged-in state
                App.SetTrayLoggedIn(user.FullName);

                // Run data-retention purge in background (fire-and-forget)
                App.RunDataRetentionPurge();

                // Check for software updates in background (fire-and-forget)
                App.CheckForUpdatesAsync();

                // Start GitHub polling for AI-driven code review
                if (App.Services.GetService(typeof(EAMAS.Core.Services.GitHubPollingService))
                    is EAMAS.Core.Services.GitHubPollingService poller)
                    poller.Start();

                System.Windows.Application.Current.Windows
                    .OfType<LoginWindow>().FirstOrDefault()?.Close();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Login error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
