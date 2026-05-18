using EAMAS.Core.Enums;
using EAMAS.Core.Models;
using EAMAS.Core.Services;
using EAMAS.Core.Services.AI;
using System.Collections.ObjectModel;
using System.Windows;

namespace EAMAS.Desktop.ViewModels
{
    public class TaskDetailViewModel : BaseViewModel
    {
        private readonly TaskService _taskService;
        private readonly UserService _userService;
        private readonly AiCodeReviewService _reviewService;
        private readonly ProjectService _projectService;
        private readonly AiProviderFactory _aiFactory;
        private readonly AiTaskGeneratorService _taskGenerator;
        private readonly RagService _ragService;

        private ProjectTask? _task;
        private bool _isBusy;
        private string _statusMessage = string.Empty;

        public ProjectTask? Task { get => _task; set { if (Set(ref _task, value)) OnTaskChanged(); } }
        public bool IsBusy { get => _isBusy; set => Set(ref _isBusy, value); }
        public string StatusMessage { get => _statusMessage; set => Set(ref _statusMessage, value); }

        public ObservableCollection<User> TeamMembers { get; } = new();
        public ObservableCollection<CodeReview> Reviews { get; } = new();
        public ObservableCollection<string> SubTaskChecks { get; } = new();

        // Edit fields
        private string _editTitle = string.Empty;
        private string _editDescription = string.Empty;
        private string _editAcceptanceCriteria = string.Empty;
        private double _editEstimatedHours;
        private TaskPriority _editPriority;
        private string _editLabels = string.Empty;
        private DateTime? _editDueDate;
        private string _assignedUserId = string.Empty;

        public string EditTitle { get => _editTitle; set => Set(ref _editTitle, value); }
        public string EditDescription { get => _editDescription; set => Set(ref _editDescription, value); }
        public string EditAcceptanceCriteria { get => _editAcceptanceCriteria; set => Set(ref _editAcceptanceCriteria, value); }
        public double EditEstimatedHours { get => _editEstimatedHours; set => Set(ref _editEstimatedHours, value); }
        public TaskPriority EditPriority { get => _editPriority; set => Set(ref _editPriority, value); }
        public string EditLabels { get => _editLabels; set => Set(ref _editLabels, value); }
        public DateTime? EditDueDate { get => _editDueDate; set => Set(ref _editDueDate, value); }
        public string AssignedUserId { get => _assignedUserId; set => Set(ref _assignedUserId, value); }

        public TaskPriority[] Priorities => Enum.GetValues<TaskPriority>();
        public bool IsManager => App.CurrentUser?.Role is UserRole.Manager or UserRole.Admin or UserRole.SuperAdmin;

        public AsyncRelayCommand SaveCommand { get; }
        public AsyncRelayCommand RefineWithAiCommand { get; }
        public RelayCommand AssignCommand { get; }

        public TaskDetailViewModel(
            TaskService taskService, UserService userService,
            AiCodeReviewService reviewService, ProjectService projectService,
            AiProviderFactory aiFactory, AiTaskGeneratorService taskGenerator, RagService ragService)
        {
            _taskService = taskService;
            _userService = userService;
            _reviewService = reviewService;
            _projectService = projectService;
            _aiFactory = aiFactory;
            _taskGenerator = taskGenerator;
            _ragService = ragService;

            SaveCommand = new AsyncRelayCommand(SaveAsync);
            RefineWithAiCommand = new AsyncRelayCommand(RefineWithAiAsync);
            AssignCommand = new RelayCommand(AssignUser);
        }

        private void OnTaskChanged()
        {
            if (_task == null) return;
            EditTitle = _task.Title;
            EditDescription = _task.Description;
            EditAcceptanceCriteria = _task.AcceptanceCriteria;
            EditEstimatedHours = _task.EstimatedHours;
            EditPriority = _task.Priority;
            EditLabels = string.Join(", ", _task.Labels);
            EditDueDate = _task.DueDate;
            AssignedUserId = _task.AssignedToUserId;

            // Load reviews
            Reviews.Clear();
            foreach (var r in _reviewService.GetByTask(_task.Id)) Reviews.Add(r);

            // Load team
            TeamMembers.Clear();
            foreach (var u in _userService.GetAll(App.CurrentOrgId)) TeamMembers.Add(u);

            // Load subtasks
            SubTaskChecks.Clear();
            foreach (var st in _task.SubTasks) SubTaskChecks.Add(st);
        }

        private async Task SaveAsync()
        {
            if (_task == null) return;
            _task.Title = EditTitle;
            _task.Description = EditDescription;
            _task.AcceptanceCriteria = EditAcceptanceCriteria;
            _task.EstimatedHours = EditEstimatedHours;
            _task.Priority = EditPriority;
            _task.DueDate = EditDueDate;
            _task.Labels = EditLabels.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
            _taskService.Update(_task);
            StatusMessage = "Task saved.";
            await System.Threading.Tasks.Task.CompletedTask;
        }

        private async Task RefineWithAiAsync()
        {
            if (_task == null) return;
            IsBusy = true; StatusMessage = "AI is refining task...";
            try
            {
                var project = _projectService.GetById(_task.ProjectId);
                if (project == null) { StatusMessage = "Project not found."; return; }
                var dec = _projectService.DecryptSecrets(project);
                var provider = _aiFactory.Create(dec.AiProvider, dec.AiApiKey, dec.AiModel);

                var systemPrompt = $"You are a senior engineering manager. Improve this task for clarity and completeness. Project: {dec.Name}. Tech: {dec.TechStack}. Output JSON: {{title, description, acceptanceCriteria, subTasks:[string]}}";
                var userPrompt = $"Task: {EditTitle}\nDescription: {EditDescription}\nCriteria: {EditAcceptanceCriteria}";

                var raw = await provider.CompleteAsync(systemPrompt, userPrompt, maxTokens: 1000).ConfigureAwait(false);
                var json = raw.Trim();
                if (json.StartsWith("```")) json = json[(json.IndexOf('\n') + 1)..];
                if (json.EndsWith("```")) json = json[..json.LastIndexOf("```")];
                var el = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(json.Trim());
                if (el.TryGetProperty("title", out var t)) EditTitle = t.GetString() ?? EditTitle;
                if (el.TryGetProperty("description", out var d)) EditDescription = d.GetString() ?? EditDescription;
                if (el.TryGetProperty("acceptanceCriteria", out var ac)) EditAcceptanceCriteria = ac.GetString() ?? EditAcceptanceCriteria;
                if (el.TryGetProperty("subTasks", out var st))
                {
                    SubTaskChecks.Clear();
                    foreach (var s in st.EnumerateArray()) SubTaskChecks.Add(s.GetString() ?? "");
                    if (_task != null) _task.SubTasks = SubTaskChecks.ToList();
                }
                StatusMessage = "Task refined by AI.";
            }
            catch (Exception ex) { StatusMessage = $"Failed: {ex.Message}"; }
            IsBusy = false;
        }

        private void AssignUser()
        {
            if (_task == null || string.IsNullOrEmpty(AssignedUserId)) return;
            var user = TeamMembers.FirstOrDefault(u => u.Id == AssignedUserId);
            if (user == null) return;
            _taskService.Assign(_task.Id, user.Id, user.FullName);
            _task.AssignedToUserId = user.Id;
            _task.AssignedToUserName = user.FullName;
            StatusMessage = $"Assigned to {user.FullName}.";
        }
    }
}
