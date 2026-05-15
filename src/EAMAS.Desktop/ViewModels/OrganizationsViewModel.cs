using EAMAS.Core.Models;
using EAMAS.Core.Services;
using System.Collections.ObjectModel;

namespace EAMAS.Desktop.ViewModels
{
    public class OrgItem
    {
        public string Id { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public string StatusLabel => IsActive ? "Active" : "Inactive";
        public string StatusColor => IsActive ? "#16A34A" : "#EF4444";
        public DateTime CreatedAt { get; set; }
        public string CreatedLabel => CreatedAt.ToLocalTime().ToString("MMM dd, yyyy");
        /// <summary>Display string shown in org dropdowns.</summary>
        public string DisplayName => $"{Code} — {Name}";
    }

    public class OrganizationsViewModel : BaseViewModel
    {
        private readonly OrganizationService _orgService;
        private readonly UserService _userService;

        // ── Org list ────────────────────────────────────────────────────────
        private ObservableCollection<OrgItem> _organizations = new();
        private OrgItem? _selectedOrg;
        private bool _isEditPanelOpen;
        private bool _isNewOrg;
        private bool _isLoading;
        private string _editName = string.Empty;
        private string _editCode = string.Empty;
        private string _editDescription = string.Empty;
        private string _statusMessage = string.Empty;

        public ObservableCollection<OrgItem> Organizations { get => _organizations; set => Set(ref _organizations, value); }

        public OrgItem? SelectedOrg
        {
            get => _selectedOrg;
            set { Set(ref _selectedOrg, value); if (value != null) OpenEdit(value); }
        }

        public bool IsEditPanelOpen { get => _isEditPanelOpen; set => Set(ref _isEditPanelOpen, value); }

        public bool IsNewOrg
        {
            get => _isNewOrg;
            set { Set(ref _isNewOrg, value); OnPropertyChanged(nameof(EditPanelTitle)); OnPropertyChanged(nameof(ShowUsersPanel)); }
        }

        public bool IsLoading { get => _isLoading; set => Set(ref _isLoading, value); }
        public string EditName { get => _editName; set => Set(ref _editName, value); }
        public string EditCode { get => _editCode; set => Set(ref _editCode, value); }
        public string EditDescription { get => _editDescription; set => Set(ref _editDescription, value); }
        public string StatusMessage { get => _statusMessage; set => Set(ref _statusMessage, value); }
        public string EditPanelTitle => IsNewOrg ? "New Organisation" : "Edit Organisation";

        /// <summary>True when an existing org is selected — shows the Users section.</summary>
        public bool ShowUsersPanel => IsEditPanelOpen && !IsNewOrg;

        public string UsersHeaderTitle => SelectedOrg != null
            ? $"Users — {SelectedOrg.Name}"
            : "Users";

        // ── User management ─────────────────────────────────────────────────
        private ObservableCollection<User> _orgUsers = new();
        private User? _selectedUser;
        private bool _isUserEditOpen;
        private bool _isNewUser;
        private string _userEditFullName = string.Empty;
        private string _userEditEmail = string.Empty;
        private string _userEditDepartment = string.Empty;
        private string _userEditUsername = string.Empty;
        private string _userEditRoleString = "Employee";
        private UserRole _userEditRole = UserRole.Employee;
        private OrgItem? _userEditOrg;
        private string _userEditId = string.Empty;
        private string _userStatusMessage = string.Empty;

        /// <summary>All orgs — used to populate the organisation dropdown in the user edit form.</summary>
        public ObservableCollection<OrgItem> AllOrganizations { get; set; } = new();

        /// <summary>Set by the code-behind before SaveUser executes (PasswordBox workaround).</summary>
        public string UserEditPassword { get; set; } = string.Empty;

        public ObservableCollection<User> OrgUsers { get => _orgUsers; set => Set(ref _orgUsers, value); }

        public User? SelectedUser
        {
            get => _selectedUser;
            set { Set(ref _selectedUser, value); if (value != null) OpenUserEdit(value); }
        }

        public bool IsUserEditOpen { get => _isUserEditOpen; set => Set(ref _isUserEditOpen, value); }

        public bool IsNewUser
        {
            get => _isNewUser;
            set { Set(ref _isNewUser, value); OnPropertyChanged(nameof(UserEditTitle)); }
        }

        public string UserEditFullName { get => _userEditFullName; set => Set(ref _userEditFullName, value); }
        public string UserEditEmail { get => _userEditEmail; set => Set(ref _userEditEmail, value); }
        public string UserEditDepartment { get => _userEditDepartment; set => Set(ref _userEditDepartment, value); }
        public string UserEditUsername { get => _userEditUsername; set => Set(ref _userEditUsername, value); }
        public string UserStatusMessage { get => _userStatusMessage; set => Set(ref _userStatusMessage, value); }
        public string UserEditTitle => IsNewUser ? "Add User" : "Edit User";

        /// <summary>The organisation the user being edited belongs to (or will be created in).</summary>
        public OrgItem? UserEditOrg { get => _userEditOrg; set => Set(ref _userEditOrg, value); }

        public string UserEditRoleString
        {
            get => _userEditRole.ToString();
            set
            {
                if (Enum.TryParse<UserRole>(value, out var r)) _userEditRole = r;
                Set(ref _userEditRoleString, value);
            }
        }

        /// <summary>SuperAdmin can assign Admin/Manager/Employee within any org.</summary>
        public List<string> UserRoleOptions { get; } = new() { "Admin", "Manager", "Employee" };

        // ── Commands ────────────────────────────────────────────────────────
        public RelayCommand LoadCommand { get; }
        public RelayCommand AddNewCommand { get; }
        public RelayCommand SaveCommand { get; }
        public RelayCommand CancelCommand { get; }
        public RelayCommand DeactivateCommand { get; }
        public RelayCommand AddUserCommand { get; }
        public RelayCommand SaveUserCommand { get; }
        public RelayCommand CancelUserCommand { get; }
        public RelayCommand DeactivateUserCommand { get; }

        public OrganizationsViewModel(OrganizationService orgService, UserService userService)
        {
            _orgService = orgService;
            _userService = userService;

            LoadCommand = new RelayCommand(Load);
            AddNewCommand = new RelayCommand(AddNew);
            SaveCommand = new RelayCommand(Save);
            CancelCommand = new RelayCommand(Cancel);
            DeactivateCommand = new RelayCommand(Deactivate, () => !IsNewOrg && SelectedOrg?.IsActive == true);

            AddUserCommand = new RelayCommand(AddUser);
            SaveUserCommand = new RelayCommand(SaveUser);
            CancelUserCommand = new RelayCommand(() =>
            {
                IsUserEditOpen = false;
                _selectedUser = null;
                OnPropertyChanged(nameof(SelectedUser));
            });
            DeactivateUserCommand = new RelayCommand(DeactivateUser, () => SelectedUser != null && !IsNewUser);
        }

        public void Initialize() => Load();

        public void Load()
        {
            IsLoading = true;
            Task.Run(() =>
            {
                var orgs = _orgService.GetAll();
                var items = orgs.Select(o => new OrgItem
                {
                    Id = o.Id,
                    Code = o.Code,
                    Name = o.Name,
                    Description = o.Description,
                    IsActive = o.IsActive,
                    CreatedAt = o.CreatedAt
                }).ToList();

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    Organizations = new ObservableCollection<OrgItem>(items);
                    AllOrganizations = new ObservableCollection<OrgItem>(items);
                    OnPropertyChanged(nameof(AllOrganizations));
                    IsLoading = false;
                });
            });
        }

        private void LoadOrgUsers(string orgId)
        {
            var users = _userService.GetAll(orgId);
            OrgUsers = new ObservableCollection<User>(users);
        }

        private void AddNew()
        {
            _selectedOrg = null;
            OnPropertyChanged(nameof(SelectedOrg));
            IsNewOrg = true;
            EditName = string.Empty;
            EditCode = string.Empty;
            EditDescription = string.Empty;
            StatusMessage = string.Empty;
            IsEditPanelOpen = true;
            OrgUsers = new ObservableCollection<User>();
            IsUserEditOpen = false;
            OnPropertyChanged(nameof(ShowUsersPanel));
        }

        private void OpenEdit(OrgItem org)
        {
            IsNewOrg = false;
            EditName = org.Name;
            EditCode = org.Code;
            EditDescription = org.Description;
            StatusMessage = string.Empty;
            IsEditPanelOpen = true;
            IsUserEditOpen = false;
            _selectedUser = null;
            OnPropertyChanged(nameof(SelectedUser));
            LoadOrgUsers(org.Id);
            OnPropertyChanged(nameof(ShowUsersPanel));
            OnPropertyChanged(nameof(UsersHeaderTitle));
        }

        private void Save()
        {
            StatusMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(EditName)) { StatusMessage = "Organisation name is required."; return; }
            if (string.IsNullOrWhiteSpace(EditCode)) { StatusMessage = "Organisation code is required."; return; }

            var code = EditCode.Trim().ToUpperInvariant();
            try
            {
                if (IsNewOrg)
                {
                    if (_orgService.CodeExists(code)) { StatusMessage = "An organisation with this code already exists."; return; }
                    _orgService.Create(EditName.Trim(), code, EditDescription.Trim());
                    StatusMessage = "Organisation created successfully.";
                }
                else if (SelectedOrg != null)
                {
                    if (_orgService.CodeExists(code, excludeId: SelectedOrg.Id)) { StatusMessage = "An organisation with this code already exists."; return; }
                    var org = _orgService.GetById(SelectedOrg.Id);
                    if (org == null) { StatusMessage = "Organisation not found."; return; }
                    org.Name = EditName.Trim();
                    org.Code = code;
                    org.Description = EditDescription.Trim();
                    _orgService.Update(org);
                    StatusMessage = "Organisation updated successfully.";
                }

                Load();
                IsEditPanelOpen = false;
                OnPropertyChanged(nameof(ShowUsersPanel));
            }
            catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; }
        }

        private void Cancel()
        {
            IsEditPanelOpen = false;
            _selectedOrg = null;
            OnPropertyChanged(nameof(SelectedOrg));
            StatusMessage = string.Empty;
            IsUserEditOpen = false;
            OrgUsers = new ObservableCollection<User>();
            OnPropertyChanged(nameof(ShowUsersPanel));
        }

        private void Deactivate()
        {
            if (SelectedOrg == null) return;
            var result = System.Windows.MessageBox.Show(
                $"Deactivate organisation '{SelectedOrg.Name}'? All users in this org will lose access.",
                "Confirm Deactivation",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);
            if (result != System.Windows.MessageBoxResult.Yes) return;
            _orgService.Deactivate(SelectedOrg.Id);
            Load();
            IsEditPanelOpen = false;
            OnPropertyChanged(nameof(ShowUsersPanel));
        }

        // ── User management ─────────────────────────────────────────────────

        private void AddUser()
        {
            IsNewUser = true;
            _userEditId = string.Empty;
            _selectedUser = null;
            OnPropertyChanged(nameof(SelectedUser));
            UserEditFullName = string.Empty;
            UserEditEmail = string.Empty;
            UserEditDepartment = string.Empty;
            UserEditUsername = string.Empty;
            UserEditPassword = string.Empty;
            _userEditRole = UserRole.Employee;
            OnPropertyChanged(nameof(UserEditRoleString));
            UserEditOrg = SelectedOrg;   // default to the currently viewed org
            UserStatusMessage = string.Empty;
            IsUserEditOpen = true;
        }

        private void OpenUserEdit(User user)
        {
            IsNewUser = false;
            _userEditId = user.Id;
            UserEditFullName = user.FullName;
            UserEditEmail = user.Email;
            UserEditDepartment = user.Department;
            UserEditUsername = user.Username;
            UserEditPassword = string.Empty;
            _userEditRole = user.Role;
            OnPropertyChanged(nameof(UserEditRoleString));
            // Resolve the OrgItem that matches this user's org
            UserEditOrg = AllOrganizations.FirstOrDefault(o => o.Id == user.OrganizationId)
                          ?? SelectedOrg;
            UserStatusMessage = string.Empty;
            IsUserEditOpen = true;
        }

        public void SaveUser()
        {
            if (UserEditOrg == null) { UserStatusMessage = "Please select an organisation."; return; }
            var orgId = UserEditOrg.Id;

            if (string.IsNullOrWhiteSpace(UserEditFullName) || string.IsNullOrWhiteSpace(UserEditUsername))
            { UserStatusMessage = "Full name and username are required."; return; }

            try
            {
                if (IsNewUser)
                {
                    if (string.IsNullOrWhiteSpace(UserEditPassword) || UserEditPassword.Length < 6)
                    { UserStatusMessage = "Password must be at least 6 characters."; return; }
                    if (_userService.UsernameExists(orgId, UserEditUsername))
                    { UserStatusMessage = "Username already exists in this organisation."; return; }
                    _userService.CreateUser(orgId, UserEditUsername, UserEditPassword,
                        UserEditFullName, UserEditEmail, UserEditDepartment, _userEditRole);
                    UserStatusMessage = $"User '{UserEditFullName}' created in {UserEditOrg.Name}.";
                }
                else
                {
                    _userService.UpdateUser(new User
                    {
                        Id = _userEditId,
                        OrganizationId = orgId,
                        FullName = UserEditFullName,
                        Email = UserEditEmail,
                        Department = UserEditDepartment,
                        Role = _userEditRole,
                        IsActive = true
                    });
                    if (!string.IsNullOrWhiteSpace(UserEditPassword) && UserEditPassword.Length >= 6)
                        _userService.ChangePassword(_userEditId, UserEditPassword);
                    UserStatusMessage = "User updated.";
                }

                // Reload the user list for whichever org is currently selected in the left panel
                if (SelectedOrg != null) LoadOrgUsers(SelectedOrg.Id);
                IsUserEditOpen = false;
            }
            catch (Exception ex) { UserStatusMessage = $"Error: {ex.Message}"; }
        }

        private void DeactivateUser()
        {
            if (SelectedUser == null || SelectedOrg == null) return;
            var result = System.Windows.MessageBox.Show(
                $"Deactivate user '{SelectedUser.FullName}'?",
                "Confirm", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
            if (result != System.Windows.MessageBoxResult.Yes) return;
            _userService.DeleteUser(SelectedUser.Id);
            LoadOrgUsers(SelectedOrg.Id);
            IsUserEditOpen = false;
            _selectedUser = null;
            OnPropertyChanged(nameof(SelectedUser));
        }
    }
}
