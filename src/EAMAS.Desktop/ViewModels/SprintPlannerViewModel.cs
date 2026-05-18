using EAMAS.Core.Enums;
using EAMAS.Core.Models;
using EAMAS.Core.Services;
using EAMAS.Core.Services.AI;
using System.Collections.ObjectModel;
using System.Windows;

namespace EAMAS.Desktop.ViewModels
{
    public class SprintPlannerViewModel : BaseViewModel
    {
        private readonly SprintService _sprintService;
        private readonly TaskService _taskService;
        private readonly ProjectService _projectService;
        private readonly UserService _userService;
        private readonly AiSprintPlannerService _planner;
        private readonly AiProviderFactory _aiFactory;

        private bool _isBusy;
        private string _statusMessage = string.Empty;
        private Sprint? _proposedSprint;
        private string _sprintGoal = string.Empty;
        private DateTime _startDate = DateTime.Today;
        private DateTime _endDate = DateTime.Today.AddDays(14);

        public bool IsBusy { get => _isBusy; set => Set(ref _isBusy, value); }
        public string StatusMessage { get => _statusMessage; set => Set(ref _statusMessage, value); }
        public Sprint? ProposedSprint { get => _proposedSprint; set => Set(ref _proposedSprint, value); }
        public string SprintGoal { get => _sprintGoal; set => Set(ref _sprintGoal, value); }
        public DateTime StartDate { get => _startDate; set { if (Set(ref _startDate, value)) OnPropertyChanged(nameof(IsDateInvalid)); } }
        public DateTime EndDate { get => _endDate; set { if (Set(ref _endDate, value)) OnPropertyChanged(nameof(IsDateInvalid)); } }
        public bool IsDateInvalid => EndDate <= StartDate;

        public ObservableCollection<Project> Projects { get; } = new();
        public ObservableCollection<Sprint> Sprints { get; } = new();
        public ObservableCollection<ProjectTask> ProposedTasks { get; } = new();
        public ObservableCollection<ProjectTask> BacklogTasks { get; } = new();

        private Project? _selectedProject;
        public Project? SelectedProject { get => _selectedProject; set { if (Set(ref _selectedProject, value)) LoadProjectData(); } }

        public AsyncRelayCommand PlanWithAiCommand { get; }
        public AsyncRelayCommand CreateSprintCommand { get; }
        public AsyncRelayCommand ActivateSprintCommand { get; }
        public AsyncRelayCommand CompleteSprintCommand { get; }
        public RelayCommand<ProjectTask> AddToSprintCommand { get; }
        public RelayCommand<ProjectTask> RemoveFromSprintCommand { get; }

        public SprintPlannerViewModel(
            SprintService sprintService, TaskService taskService,
            ProjectService projectService, UserService userService,
            AiSprintPlannerService planner, AiProviderFactory aiFactory)
        {
            _sprintService = sprintService;
            _taskService = taskService;
            _projectService = projectService;
            _userService = userService;
            _planner = planner;
            _aiFactory = aiFactory;

            PlanWithAiCommand = new AsyncRelayCommand(PlanWithAiAsync);
            CreateSprintCommand = new AsyncRelayCommand(CreateSprintAsync);
            ActivateSprintCommand = new AsyncRelayCommand(ActivateSprintAsync);
            CompleteSprintCommand = new AsyncRelayCommand(CompleteSprintAsync);
            AddToSprintCommand = new RelayCommand<ProjectTask>(AddToSprint);
            RemoveFromSprintCommand = new RelayCommand<ProjectTask>(RemoveFromSprint);

            Load();
        }

        public void Load()
        {
            var projects = _projectService.GetAll(App.CurrentOrgId);
            Projects.Clear();
            foreach (var p in projects) Projects.Add(p);
            SelectedProject = projects.FirstOrDefault();
        }

        private void LoadProjectData()
        {
            if (SelectedProject == null) return;
            var sprints = _sprintService.GetByProject(SelectedProject.Id);
            Sprints.Clear();
            foreach (var s in sprints) Sprints.Add(s);

            var backlog = _taskService.GetBacklog(SelectedProject.Id);
            BacklogTasks.Clear();
            foreach (var t in backlog) BacklogTasks.Add(t);

            EndDate = StartDate.AddDays(SelectedProject.SprintDurationDays);
        }

        private async Task PlanWithAiAsync()
        {
            if (SelectedProject == null) { StatusMessage = "Select a project first."; return; }
            IsBusy = true; StatusMessage = "AI is planning the sprint...";
            try
            {
                var dec = _projectService.DecryptSecrets(SelectedProject);
                var provider = _aiFactory.Create(dec.AiProvider, dec.AiApiKey, dec.AiModel, dec.AiTemperature);
                var team = _userService.GetAll(App.CurrentOrgId)
                    .Where(u => u.Role is UserRole.Employee or UserRole.Manager).ToList();

                var (proposed, tasks) = await _planner.PlanSprintAsync(provider, dec, team).ConfigureAwait(false);
                ProposedSprint = proposed;
                SprintGoal = proposed.Goal;
                StartDate = proposed.StartDate;
                EndDate = proposed.EndDate;

                ProposedTasks.Clear();
                foreach (var t in tasks) ProposedTasks.Add(t);

                // Remove proposed tasks from backlog display
                var proposedIds = tasks.Select(t => t.Id).ToHashSet();
                var remaining = BacklogTasks.Where(t => !proposedIds.Contains(t.Id)).ToList();
                BacklogTasks.Clear();
                foreach (var t in remaining) BacklogTasks.Add(t);

                StatusMessage = $"AI proposed {tasks.Count} tasks, {proposed.PlannedVelocity:F0}h planned. Review and create.";
            }
            catch (Exception ex) { StatusMessage = $"Planning failed: {ex.Message}"; }
            IsBusy = false;
        }

        private async Task CreateSprintAsync()
        {
            if (SelectedProject == null || !ProposedTasks.Any()) { StatusMessage = "Plan a sprint first."; return; }
            IsBusy = true; StatusMessage = "Creating sprint...";
            try
            {
                int num = Sprints.Count + 1;
                var sprint = new Sprint
                {
                    OrganizationId = SelectedProject.OrganizationId,
                    ProjectId = SelectedProject.Id,
                    Name = $"Sprint {num}",
                    Goal = SprintGoal,
                    StartDate = StartDate,
                    EndDate = EndDate,
                    Status = SprintStatus.Planning,
                    TaskIds = ProposedTasks.Select(t => t.Id).ToList(),
                    PlannedVelocity = ProposedTasks.Sum(t => t.EstimatedHours)
                };
                _sprintService.Create(sprint);
                Sprints.Insert(0, sprint);
                StatusMessage = $"Sprint '{sprint.Name}' created. Activate it to start.";
            }
            catch (Exception ex) { StatusMessage = $"Failed: {ex.Message}"; }
            IsBusy = false;
            await Task.CompletedTask;
        }

        private async Task ActivateSprintAsync()
        {
            var planning = Sprints.FirstOrDefault(s => s.Status == SprintStatus.Planning);
            if (planning == null) { StatusMessage = "No sprint in Planning status."; return; }
            if (System.Windows.MessageBox.Show($"Activate '{planning.Name}'? This will move tasks to Todo and cannot be undone.",
                "Confirm", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question) != System.Windows.MessageBoxResult.Yes) return;
            _sprintService.Activate(planning.Id);
            planning.Status = SprintStatus.Active;
            StatusMessage = $"'{planning.Name}' is now active!";
            LoadProjectData();
            await Task.CompletedTask;
        }

        private async Task CompleteSprintAsync()
        {
            var active = Sprints.FirstOrDefault(s => s.Status == SprintStatus.Active);
            if (active == null) { StatusMessage = "No active sprint."; return; }
            if (System.Windows.MessageBox.Show($"Complete '{active.Name}'? Unfinished tasks return to Backlog.",
                "Confirm", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question) != System.Windows.MessageBoxResult.Yes) return;

            IsBusy = true; StatusMessage = "Completing sprint...";
            try
            {
                if (SelectedProject != null)
                {
                    var dec = _projectService.DecryptSecrets(SelectedProject);
                    var provider = _aiFactory.Create(dec.AiProvider, dec.AiApiKey, dec.AiModel, dec.AiTemperature);
                    var sprintTasks = _taskService.GetBySprint(active.Id);
                    var retro = await _planner.GenerateRetrospectiveAsync(provider, active, sprintTasks).ConfigureAwait(false);
                    _sprintService.Complete(active.Id, retro);
                }
                else _sprintService.Complete(active.Id);

                active.Status = SprintStatus.Completed;
                StatusMessage = "Sprint completed. Check the retrospective in sprint details.";
                LoadProjectData();
            }
            catch (Exception ex) { StatusMessage = $"Failed: {ex.Message}"; }
            IsBusy = false;
        }

        private void AddToSprint(ProjectTask? task)
        {
            if (task == null) return;
            ProposedTasks.Add(task);
            BacklogTasks.Remove(task);
        }

        private void RemoveFromSprint(ProjectTask? task)
        {
            if (task == null) return;
            BacklogTasks.Add(task);
            ProposedTasks.Remove(task);
        }
    }
}
