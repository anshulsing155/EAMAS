using EAMAS.Core.Models;
using EAMAS.Core.Services;
using System.Collections.ObjectModel;
using System.Windows;

namespace EAMAS.Desktop.ViewModels
{
    public class EmployeesViewModel : BaseViewModel
    {
        private readonly UserService _userService;
        private readonly ReportService _reportService;

        private ObservableCollection<User> _employees = new();
        private User? _selectedEmployee;
        private bool _isEditPanelOpen;
        private string _editFullName = string.Empty;
        private string _editEmail = string.Empty;
        private string _editDepartment = string.Empty;
        private string _editUsername = string.Empty;
        private string _editPassword = string.Empty;
        private UserRole _editRole = UserRole.Employee;
        private bool _isNewUser;
        private string _statusMessage = string.Empty;
        private string _editId = string.Empty;
        private string _editRoleString = "Employee";

        public ObservableCollection<User> Employees { get => _employees; set => Set(ref _employees, value); }
        public User? SelectedEmployee { get => _selectedEmployee; set { Set(ref _selectedEmployee, value); OnSelectedChanged(); } }
        public bool IsEditPanelOpen { get => _isEditPanelOpen; set => Set(ref _isEditPanelOpen, value); }
        public string EditFullName { get => _editFullName; set => Set(ref _editFullName, value); }
        public string EditEmail { get => _editEmail; set => Set(ref _editEmail, value); }
        public string EditDepartment { get => _editDepartment; set => Set(ref _editDepartment, value); }
        public string EditUsername { get => _editUsername; set => Set(ref _editUsername, value); }
        public string EditPassword { get => _editPassword; set => Set(ref _editPassword, value); }
        public UserRole EditRole { get => _editRole; set => Set(ref _editRole, value); }
        public string EditRoleString
        {
            get => _editRole.ToString();
            set
            {
                if (Enum.TryParse<UserRole>(value, out var r)) EditRole = r;
                Set(ref _editRoleString, value);
            }
        }
        public bool IsNewUser { get => _isNewUser; set => Set(ref _isNewUser, value); }
        public string StatusMessage { get => _statusMessage; set => Set(ref _statusMessage, value); }

        /// <summary>Roles that an org admin/manager can assign (excludes SuperAdmin).</summary>
        public List<string> RoleOptions { get; } = new() { "Admin", "Manager", "Employee" };

        public RelayCommand AddNewCommand { get; }
        public RelayCommand SaveCommand { get; }
        public RelayCommand CancelEditCommand { get; }
        public RelayCommand DeactivateCommand { get; }
        public RelayCommand LoadCommand { get; }

        public EmployeesViewModel(UserService userService, ReportService reportService)
        {
            _userService = userService;
            _reportService = reportService;
            AddNewCommand = new RelayCommand(AddNew);
            SaveCommand = new RelayCommand(Save);
            CancelEditCommand = new RelayCommand(() => IsEditPanelOpen = false);
            DeactivateCommand = new RelayCommand(Deactivate, () => SelectedEmployee != null);
            LoadCommand = new RelayCommand(Load);
        }

        public void Load()
        {
            // Only load users belonging to the current organisation
            var users = _userService.GetAll(App.CurrentOrgId);
            Employees = new ObservableCollection<User>(users);
        }

        private void AddNew()
        {
            IsNewUser = true;
            _editId = string.Empty;
            EditFullName = string.Empty;
            EditEmail = string.Empty;
            EditDepartment = string.Empty;
            EditUsername = string.Empty;
            EditPassword = string.Empty;
            EditRole = UserRole.Employee;
            IsEditPanelOpen = true;
        }

        private void OnSelectedChanged()
        {
            if (SelectedEmployee == null) return;
            IsNewUser = false;
            _editId = SelectedEmployee.Id;
            EditFullName = SelectedEmployee.FullName;
            EditEmail = SelectedEmployee.Email;
            EditDepartment = SelectedEmployee.Department;
            EditUsername = SelectedEmployee.Username;
            EditPassword = string.Empty;
            EditRole = SelectedEmployee.Role;
            IsEditPanelOpen = true;
        }

        private void Save()
        {
            if (string.IsNullOrWhiteSpace(EditFullName) || string.IsNullOrWhiteSpace(EditUsername))
            {
                StatusMessage = "Full name and username are required.";
                return;
            }

            if (IsNewUser)
            {
                if (string.IsNullOrWhiteSpace(EditPassword) || EditPassword.Length < 6)
                {
                    StatusMessage = "Password must be at least 6 characters.";
                    return;
                }
                if (_userService.UsernameExists(App.CurrentOrgId, EditUsername))
                {
                    StatusMessage = "Username already exists in this organisation.";
                    return;
                }
                _userService.CreateUser(App.CurrentOrgId, EditUsername, EditPassword,
                    EditFullName, EditEmail, EditDepartment, EditRole);
                StatusMessage = $"User '{EditFullName}' created successfully.";
            }
            else
            {
                _userService.UpdateUser(new User
                {
                    Id = _editId,
                    OrganizationId = App.CurrentOrgId,
                    FullName = EditFullName,
                    Email = EditEmail,
                    Department = EditDepartment,
                    Role = EditRole,
                    IsActive = true
                });
                if (!string.IsNullOrWhiteSpace(EditPassword) && EditPassword.Length >= 6)
                    _userService.ChangePassword(_editId, EditPassword);
                StatusMessage = "User updated successfully.";
            }

            Load();
            IsEditPanelOpen = false;
        }

        private void Deactivate()
        {
            if (SelectedEmployee == null) return;
            if (SelectedEmployee.Id == App.CurrentUser?.Id)
            {
                StatusMessage = "Cannot deactivate your own account.";
                return;
            }
            var result = System.Windows.MessageBox.Show(
                $"Deactivate user '{SelectedEmployee.FullName}'?",
                "Confirm Deactivation", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;
            _userService.DeleteUser(SelectedEmployee.Id);
            Load();
            IsEditPanelOpen = false;
        }
    }
}
