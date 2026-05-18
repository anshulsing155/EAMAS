using EAMAS.Core.Enums;
using EAMAS.Core.Models;
using EAMAS.Core.Services;
using EAMAS.Core.Services.AI;
using System.Collections.ObjectModel;
using System.Windows;

namespace EAMAS.Desktop.ViewModels
{
    public class TasksViewModel : BaseViewModel
    {
        private readonly TaskService _taskService;
        private readonly ProjectService _projectService;
        private readonly SprintService _sprintService;
        private readonly UserService _userService;
        private readonly AiCodeReviewService _reviewService;
        private readonly AiStandupService _standupService;
        private readonly AiProviderFactory _aiFactory;

        private ObservableCollection<Project> _projects = new();
        private Project? _selectedProject;
        private Sprint? _activeSprint;
        private bool _isBusy;
        private string _statusMessage = string.Empty;
        private ProjectTask? _selectedTask;
        private User? _selectedTaskAssignee;
        private string _filterUserId = string.Empty;
        private string _filterLabel = string.Empty;
        private string _searchText = string.Empty;
        private bool _showStandups;

        // Kanban columns
        public ObservableCollection<ProjectTask> Backlog { get; } = new();
        public ObservableCollection<ProjectTask> Todo { get; } = new();
        public ObservableCollection<ProjectTask> InProgress { get; } = new();
        public ObservableCollection<ProjectTask> CodeReviewCol { get; } = new();
        public ObservableCollection<ProjectTask> QATesting { get; } = new();
        public ObservableCollection<ProjectTask> NeedsFix { get; } = new();
        public ObservableCollection<ProjectTask> Done { get; } = new();

        public ObservableCollection<Project> Projects { get => _projects; set => Set(ref _projects, value); }
        public Project? SelectedProject { get => _selectedProject; set { if (Set(ref _selectedProject, value)) LoadBoard(); } }
        public Sprint? ActiveSprint { get => _activeSprint; set => Set(ref _activeSprint, value); }
        public bool IsBusy { get => _isBusy; set => Set(ref _isBusy, value); }
        public string StatusMessage { get => _statusMessage; set => Set(ref _statusMessage, value); }
        public ProjectTask? SelectedTask 
        { 
            get => _selectedTask; 
            set 
            { 
                if (Set(ref _selectedTask, value))
                {
                    _selectedTaskAssignee = value != null ? TeamMembers.FirstOrDefault(u => u.Id == value.AssignedToUserId) : null;
                    OnPropertyChanged(nameof(SelectedTaskAssignee));
                }
            } 
        }

        public User? SelectedTaskAssignee
        {
            get => _selectedTaskAssignee;
            set
            {
                if (Set(ref _selectedTaskAssignee, value) && value != null && SelectedTask != null)
                {
                    if (SelectedTask.AssignedToUserId != value.Id)
                    {
                        _taskService.Assign(SelectedTask.Id, value.Id, value.FullName);
                        SelectedTask.AssignedToUserId = value.Id;
                        SelectedTask.AssignedToUserName = value.FullName;
                        
                        var id = SelectedTask.Id;
                        LoadBoard();
                        SelectedTask = AllBoardTasks().FirstOrDefault(t => t.Id == id);
                    }
                }
            }
        }

        private IEnumerable<ProjectTask> AllBoardTasks() => 
            Backlog.Concat(Todo).Concat(InProgress).Concat(CodeReviewCol).Concat(QATesting).Concat(NeedsFix).Concat(Done);
        public string FilterUserId { get => _filterUserId; set { Set(ref _filterUserId, value); LoadBoard(); } }
        public string FilterLabel { get => _filterLabel; set { Set(ref _filterLabel, value); LoadBoard(); } }
        public string SearchText { get => _searchText; set { Set(ref _searchText, value); LoadBoard(); } }
        public bool ShowStandups { get => _showStandups; set => Set(ref _showStandups, value); }

        public bool IsManager => App.CurrentUser?.Role is UserRole.Manager or UserRole.Admin or UserRole.SuperAdmin;
        public bool IsEmployee => App.CurrentUser?.Role == UserRole.Employee;

        public ObservableCollection<User> TeamMembers { get; } = new();
        public ObservableCollection<StandupLog> TodayStandups { get; } = new();
        public List<CodeReview> LatestReviews { get; private set; } = new();

        public AsyncRelayCommand RefreshCommand { get; }
        public AsyncRelayCommand GenerateStandupsCommand { get; }
        public RelayCommand<ProjectTask> MoveForwardCommand { get; }
        public RelayCommand<ProjectTask> MoveBackCommand { get; }
        public RelayCommand<ProjectTask> OpenTaskDetailCommand { get; }
        public RelayCommand<ProjectTask> AssignToMeCommand { get; }
        public RelayCommand<ProjectTask> DeleteTaskCommand { get; }
        public RelayCommand ToggleStandupsCommand { get; }
        public RelayCommand ClearSearchCommand { get; }

        public TasksViewModel(
            TaskService taskService,
            ProjectService projectService,
            SprintService sprintService,
            UserService userService,
            AiCodeReviewService reviewService,
            AiStandupService standupService,
            AiProviderFactory aiFactory)
        {
            _taskService = taskService;
            _projectService = projectService;
            _sprintService = sprintService;
            _userService = userService;
            _reviewService = reviewService;
            _standupService = standupService;
            _aiFactory = aiFactory;

            RefreshCommand = new AsyncRelayCommand(async () => { LoadBoard(); await Task.CompletedTask; });
            GenerateStandupsCommand = new AsyncRelayCommand(GenerateStandupsAsync);
            MoveForwardCommand = new RelayCommand<ProjectTask>(t => MoveTask(t, forward: true));
            MoveBackCommand = new RelayCommand<ProjectTask>(t => MoveTask(t, forward: false));
            OpenTaskDetailCommand = new RelayCommand<ProjectTask>(t => { SelectedTask = t; });
            AssignToMeCommand = new RelayCommand<ProjectTask>(AssignToMe);
            DeleteTaskCommand = new RelayCommand<ProjectTask>(DeleteTask, t => IsManager);
            ToggleStandupsCommand = new RelayCommand(() => ShowStandups = !ShowStandups);
            ClearSearchCommand = new RelayCommand(() => SearchText = string.Empty);

            Load();
        }

        public void Load()
        {
            var orgId = App.CurrentOrgId;
            var projects = _projectService.GetAll(orgId);
            Projects = new ObservableCollection<Project>(projects);
            SelectedProject = projects.FirstOrDefault();

            // Load team members for assignment
            var members = _userService.GetAll(orgId);
            TeamMembers.Clear();
            foreach (var m in members) TeamMembers.Add(m);
        }

        public void LoadBoard()
        {
            if (SelectedProject == null) { ClearAllColumns(); return; }

            ActiveSprint = _sprintService.GetActive(SelectedProject.Id);

            var allTasks = IsEmployee
                ? _taskService.GetByUser(App.CurrentOrgId, App.CurrentUser!.Id)
                : _taskService.GetByProject(SelectedProject.Id);

            // Apply filters
            if (!string.IsNullOrEmpty(FilterUserId))
                allTasks = allTasks.Where(t => t.AssignedToUserId == FilterUserId).ToList();
            if (!string.IsNullOrEmpty(FilterLabel))
                allTasks = allTasks.Where(t => t.Labels.Contains(FilterLabel, StringComparer.OrdinalIgnoreCase)).ToList();
            if (!string.IsNullOrEmpty(SearchText))
            {
                var q = SearchText.Trim().ToLowerInvariant();
                allTasks = allTasks.Where(t =>
                    t.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    (t.Description?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (t.AssignedToUserName?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();
            }

            PopulateColumn(Backlog, allTasks, ProjectTaskStatus.Backlog);
            PopulateColumn(Todo, allTasks, ProjectTaskStatus.Todo);
            PopulateColumn(InProgress, allTasks, ProjectTaskStatus.InProgress);
            PopulateColumn(CodeReviewCol, allTasks, ProjectTaskStatus.CodeReview);
            PopulateColumn(QATesting, allTasks, ProjectTaskStatus.QATesting);
            PopulateColumn(NeedsFix, allTasks, ProjectTaskStatus.NeedsFix);
            PopulateColumn(Done, allTasks, ProjectTaskStatus.Done);

            // Load latest reviews for task detail
            if (IsManager)
                LatestReviews = _reviewService.GetByProject(SelectedProject.Id, limit: 10);

            // Today's standups
            if (IsManager)
            {
                var standups = _standupService.GetForProject(SelectedProject.Id, DateTime.UtcNow.Date);
                TodayStandups.Clear();
                foreach (var s in standups) TodayStandups.Add(s);
            }
        }

        private void MoveTask(ProjectTask? task, bool forward)
        {
            if (task == null) return;
            var newStatus = forward ? Next(task.Status) : Prev(task.Status);
            if (newStatus == task.Status) return;
            _taskService.MoveStatus(task.Id, newStatus);
            task.Status = newStatus;
            LoadBoard();
        }

        private static ProjectTaskStatus Next(ProjectTaskStatus s) => s switch
        {
            ProjectTaskStatus.Backlog => ProjectTaskStatus.Todo,
            ProjectTaskStatus.Todo => ProjectTaskStatus.InProgress,
            ProjectTaskStatus.InProgress => ProjectTaskStatus.CodeReview,
            ProjectTaskStatus.CodeReview => ProjectTaskStatus.QATesting,
            ProjectTaskStatus.QATesting => ProjectTaskStatus.Done,
            ProjectTaskStatus.NeedsFix => ProjectTaskStatus.InProgress,
            _ => s
        };

        private static ProjectTaskStatus Prev(ProjectTaskStatus s) => s switch
        {
            ProjectTaskStatus.Todo => ProjectTaskStatus.Backlog,
            ProjectTaskStatus.InProgress => ProjectTaskStatus.Todo,
            ProjectTaskStatus.CodeReview => ProjectTaskStatus.InProgress,
            ProjectTaskStatus.QATesting => ProjectTaskStatus.CodeReview,
            ProjectTaskStatus.Done => ProjectTaskStatus.QATesting,
            _ => s
        };

        private void AssignToMe(ProjectTask? task)
        {
            if (task == null || App.CurrentUser == null) return;
            _taskService.Assign(task.Id, App.CurrentUser.Id, App.CurrentUser.FullName);
            task.AssignedToUserId = App.CurrentUser.Id;
            task.AssignedToUserName = App.CurrentUser.FullName;
            LoadBoard();
        }

        private void DeleteTask(ProjectTask? task)
        {
            if (task == null) return;
            if (System.Windows.MessageBox.Show($"Delete task '{task.Title}'?", "Confirm",
                System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning) != System.Windows.MessageBoxResult.Yes) return;
            _taskService.Delete(task.Id);
            LoadBoard();
        }

        private async Task GenerateStandupsAsync()
        {
            if (SelectedProject == null) { StatusMessage = "Select a project first."; return; }
            IsBusy = true; StatusMessage = "Generating standups...";
            try
            {
                var dec = _projectService.DecryptSecrets(SelectedProject);
                var provider = _aiFactory.Create(dec.AiProvider, dec.AiApiKey, dec.AiModel, dec.AiTemperature);
                var members = _userService.GetAll(App.CurrentOrgId)
                    .Where(u => u.Role == UserRole.Employee || u.Role == UserRole.Manager).ToList();
                int generated = 0;
                foreach (var member in members)
                {
                    await _standupService.GenerateStandupAsync(provider, dec, member).ConfigureAwait(false);
                    generated++;
                }
                StatusMessage = $"Generated standups for {generated} team member(s).";
                LoadBoard();
            }
            catch (Exception ex) { StatusMessage = $"Failed: {ex.Message}"; }
            IsBusy = false;
        }

        private static void PopulateColumn(ObservableCollection<ProjectTask> col, List<ProjectTask> all, ProjectTaskStatus status)
        {
            col.Clear();
            foreach (var t in all.Where(t => t.Status == status).OrderBy(t => t.BoardPosition))
                col.Add(t);
        }

        private void ClearAllColumns()
        {
            Backlog.Clear(); Todo.Clear(); InProgress.Clear();
            CodeReviewCol.Clear(); QATesting.Clear(); NeedsFix.Clear(); Done.Clear();
        }
    }
}
