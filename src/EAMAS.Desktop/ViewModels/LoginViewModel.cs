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

                var user = _userService.Authenticate(organizationId, Username.Trim(), password);
                if (user == null)
                {
                    ErrorMessage = "Invalid username or password.";
                    return;
                }

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
                        ErrorMessage = "Monitoring consent is required to proceed.";
                        App.CurrentUser = null;
                        App.CurrentOrganization = null;
                        return;
                    }

                    _userService.SetConsent(user.Id, true);
                }

                var mainWindow = App.Services.GetService(typeof(MainWindow)) as MainWindow;
                mainWindow?.Show();

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
