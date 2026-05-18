using EAMAS.Core.Enums;
using EAMAS.Core.Models;
using EAMAS.Core.Services;
using EAMAS.Core.Services.AI;
using System.Collections.ObjectModel;
using System.Windows;

namespace EAMAS.Desktop.ViewModels
{
    public class ProjectsViewModel : BaseViewModel
    {
        private readonly ProjectService _projectService;
        private readonly GitHubPollingService _polling;
        private readonly AiProviderFactory _aiFactory;
        private readonly RagService _ragService;
        private readonly AiTaskGeneratorService _taskGenerator;
        private readonly TaskService _taskService;

        private ObservableCollection<Project> _projects = new();
        private Project? _selected;
        private bool _isBusy;
        private string _statusMessage = string.Empty;

        // Form fields
        private string _name = string.Empty;
        private string _description = string.Empty;
        private string _repoOwner = string.Empty;
        private string _repoName = string.Empty;
        private string _ghToken = string.Empty;
        private string _defaultBranch = "main";
        private AiProviderType _aiProvider = AiProviderType.OpenAI;
        private string _aiApiKey = string.Empty;
        private string _aiModel = "gpt-4o";
        private double _aiTemperature = 0.3;
        private string _prdContent = string.Empty;
        private string _architectureNotes = string.Empty;
        private string _techStack = string.Empty;
        private int _sprintDays = 14;
        private int _workHoursPerDay = 8;
        private string _qaCommands = string.Empty;
        private bool _isNew = true;

        public ObservableCollection<Project> Projects { get => _projects; set => Set(ref _projects, value); }
        public Project? SelectedProject { get => _selected; set { if (Set(ref _selected, value)) LoadForm(value); } }
        public bool IsBusy { get => _isBusy; set => Set(ref _isBusy, value); }
        public string StatusMessage { get => _statusMessage; set => Set(ref _statusMessage, value); }

        public string Name { get => _name; set => Set(ref _name, value); }
        public string Description { get => _description; set => Set(ref _description, value); }
        public string RepoOwner { get => _repoOwner; set => Set(ref _repoOwner, value); }
        public string RepoName { get => _repoName; set => Set(ref _repoName, value); }
        public string GhToken { get => _ghToken; set => Set(ref _ghToken, value); }
        public string DefaultBranch { get => _defaultBranch; set => Set(ref _defaultBranch, value); }
        public AiProviderType AiProvider { get => _aiProvider; set { if (Set(ref _aiProvider, value)) AiModel = AiProviderFactory.GetDefaultModel(value); } }
        public string AiApiKey { get => _aiApiKey; set => Set(ref _aiApiKey, value); }
        public string AiModel { get => _aiModel; set => Set(ref _aiModel, value); }
        public double AiTemperature { get => _aiTemperature; set => Set(ref _aiTemperature, value); }
        public string PrdContent { get => _prdContent; set => Set(ref _prdContent, value); }
        public string ArchitectureNotes { get => _architectureNotes; set => Set(ref _architectureNotes, value); }
        public string TechStack { get => _techStack; set => Set(ref _techStack, value); }
        public int SprintDays { get => _sprintDays; set => Set(ref _sprintDays, value); }
        public int WorkHoursPerDay { get => _workHoursPerDay; set => Set(ref _workHoursPerDay, value); }
        public string QaCommands { get => _qaCommands; set => Set(ref _qaCommands, value); }
        public bool IsNew { get => _isNew; set => Set(ref _isNew, value); }

        public string[] AiModels => AiProviderFactory.GetModels(AiProvider);
        public AiProviderType[] AllProviders => Enum.GetValues<AiProviderType>();

        public AsyncRelayCommand SaveCommand { get; }
        public AsyncRelayCommand TestGitHubCommand { get; }
        public AsyncRelayCommand TestAiKeyCommand { get; }
        public AsyncRelayCommand IndexKnowledgeBaseCommand { get; }
        public AsyncRelayCommand GenerateTasksCommand { get; }
        public AsyncRelayCommand PollNowCommand { get; }
        public RelayCommand NewProjectCommand { get; }
        public RelayCommand DeleteProjectCommand { get; }

        public ProjectsViewModel(
            ProjectService projectService,
            GitHubPollingService polling,
            AiProviderFactory aiFactory,
            RagService ragService,
            AiTaskGeneratorService taskGenerator,
            TaskService taskService)
        {
            _projectService = projectService;
            _polling = polling;
            _aiFactory = aiFactory;
            _ragService = ragService;
            _taskGenerator = taskGenerator;
            _taskService = taskService;

            SaveCommand = new AsyncRelayCommand(SaveAsync);
            TestGitHubCommand = new AsyncRelayCommand(TestGitHubAsync);
            TestAiKeyCommand = new AsyncRelayCommand(TestAiKeyAsync);
            IndexKnowledgeBaseCommand = new AsyncRelayCommand(IndexKnowledgeBaseAsync);
            GenerateTasksCommand = new AsyncRelayCommand(GenerateTasksAsync);
            PollNowCommand = new AsyncRelayCommand(PollNowAsync);
            NewProjectCommand = new RelayCommand(NewProject);
            DeleteProjectCommand = new RelayCommand(DeleteProject);

            Load();
        }

        public void Load()
        {
            var orgId = App.CurrentOrgId;
            var list = _projectService.GetAll(orgId);
            Projects = new ObservableCollection<Project>(list);
            if (Projects.Any()) SelectedProject = Projects[0];
            else NewProject();
        }

        private void NewProject()
        {
            IsNew = true;
            SelectedProject = null;
            Name = Description = RepoOwner = RepoName = GhToken = string.Empty;
            DefaultBranch = "main";
            AiProvider = AiProviderType.OpenAI;
            AiApiKey = AiModel = PrdContent = ArchitectureNotes = TechStack = QaCommands = string.Empty;
            AiModel = "gpt-4o";
            AiTemperature = 0.3;
            SprintDays = 14;
            WorkHoursPerDay = 8;
        }

        private void LoadForm(Project? p)
        {
            if (p == null) return;
            IsNew = false;
            var dec = _projectService.DecryptSecrets(p);
            Name = dec.Name; Description = dec.Description;
            RepoOwner = dec.GitHubRepoOwner; RepoName = dec.GitHubRepoName;
            GhToken = dec.GitHubAccessToken; DefaultBranch = dec.DefaultBranch;
            AiProvider = dec.AiProvider; AiApiKey = dec.AiApiKey; AiModel = dec.AiModel;
            AiTemperature = dec.AiTemperature; PrdContent = dec.PrdContent;
            ArchitectureNotes = dec.ArchitectureNotes; TechStack = dec.TechStack;
            SprintDays = dec.SprintDurationDays; WorkHoursPerDay = dec.WorkHoursPerDay;
            QaCommands = dec.QaCommands;
        }

        private async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(Name)) { StatusMessage = "Project name is required."; return; }
            IsBusy = true;
            StatusMessage = "Saving...";
            try
            {
                var project = IsNew ? new Project() : (_selected != null ? CloneFromForm(_selected) : new Project());
                ApplyFormToProject(project);

                if (IsNew)
                {
                    project.OrganizationId = App.CurrentOrgId;
                    project.CreatedByUserId = App.CurrentUser?.Id ?? string.Empty;
                    _projectService.Create(project);
                    Projects.Add(project);
                    SelectedProject = project;
                    IsNew = false;
                }
                else
                {
                    _projectService.Update(project);
                    var idx = Projects.IndexOf(_selected!);
                    if (idx >= 0) Projects[idx] = project;
                    SelectedProject = project;
                }
                StatusMessage = "Saved successfully.";
            }
            catch (Exception ex) { StatusMessage = $"Save failed: {ex.Message}"; }
            finally { IsBusy = false; }
            await Task.CompletedTask;
        }

        private async Task TestGitHubAsync()
        {
            if (string.IsNullOrWhiteSpace(RepoOwner) || string.IsNullOrWhiteSpace(RepoName) || string.IsNullOrWhiteSpace(GhToken))
            {
                StatusMessage = "Fill in repo owner, name, and token first.";
                return;
            }
            IsBusy = true; StatusMessage = "Testing GitHub connection...";
            bool ok = await _polling.TestConnectionAsync(RepoOwner, RepoName, GhToken).ConfigureAwait(false);
            StatusMessage = ok ? "GitHub connection successful!" : "GitHub connection failed. Check owner, repo name, and token.";
            IsBusy = false;
        }

        private async Task TestAiKeyAsync()
        {
            if (string.IsNullOrWhiteSpace(AiApiKey)) { StatusMessage = "Enter an API key first."; return; }
            IsBusy = true; StatusMessage = "Testing AI key...";
            try
            {
                var provider = _aiFactory.Create(AiProvider, AiApiKey, AiModel, AiTemperature);
                var result = await provider.CompleteAsync("You are a test assistant.", "Reply with: OK", maxTokens: 10).ConfigureAwait(false);
                StatusMessage = result.Contains("OK", StringComparison.OrdinalIgnoreCase) || result.Length > 0
                    ? $"AI key valid! Response: {result.Trim()[..Math.Min(40, result.Length)]}"
                    : "Key may be invalid — no response.";
            }
            catch (Exception ex) { StatusMessage = $"AI key test failed: {ex.Message}"; }
            IsBusy = false;
        }

        private async Task IndexKnowledgeBaseAsync()
        {
            if (_selected == null) { StatusMessage = "Save the project first."; return; }
            IsBusy = true; StatusMessage = "Indexing knowledge base...";
            try
            {
                var dec = _projectService.DecryptSecrets(_selected);
                var provider = _aiFactory.Create(dec.AiProvider, dec.AiApiKey, dec.AiModel, dec.AiTemperature);
                await _ragService.IndexProjectAsync(_selected.Id, provider, PrdContent, ArchitectureNotes, TechStack).ConfigureAwait(false);
                StatusMessage = "Knowledge base indexed successfully.";
            }
            catch (Exception ex) { StatusMessage = $"Indexing failed: {ex.Message}"; }
            IsBusy = false;
        }

        private async Task GenerateTasksAsync()
        {
            if (_selected == null) { StatusMessage = "Save the project first."; return; }
            if (string.IsNullOrWhiteSpace(PrdContent)) { StatusMessage = "Enter PRD content first."; return; }
            IsBusy = true; StatusMessage = "AI is generating tasks from PRD...";
            try
            {
                var dec = _projectService.DecryptSecrets(_selected);
                var provider = _aiFactory.Create(dec.AiProvider, dec.AiApiKey, dec.AiModel, dec.AiTemperature);
                var existing = _taskService.GetByProject(_selected.Id).Select(t => t.Title).ToList();
                var generated = await _taskGenerator.GenerateTasksAsync(provider, dec, existingTaskTitles: existing).ConfigureAwait(false);

                if (!generated.Any()) { StatusMessage = "No tasks generated. Check PRD content."; return; }

                var result = System.Windows.MessageBox.Show(
                    $"AI generated {generated.Count} tasks. Add them to the backlog?",
                    "Tasks Generated", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);

                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    _taskService.BulkCreate(generated);
                    StatusMessage = $"{generated.Count} tasks added to backlog.";
                }
                else StatusMessage = "Task generation cancelled.";
            }
            catch (Exception ex) { StatusMessage = $"Generation failed: {ex.Message}"; }
            IsBusy = false;
        }

        private async Task PollNowAsync()
        {
            if (_selected == null) { StatusMessage = "Select a project first."; return; }
            IsBusy = true; StatusMessage = "Polling GitHub for new commits...";
            try
            {
                await _polling.PollProjectNowAsync(_selected.Id).ConfigureAwait(false);
                StatusMessage = "Poll complete.";
            }
            catch (Exception ex) { StatusMessage = $"Poll failed: {ex.Message}"; }
            IsBusy = false;
        }

        private void DeleteProject()
        {
            if (_selected == null) return;
            var r = System.Windows.MessageBox.Show($"Deactivate project '{_selected.Name}'?", "Confirm",
                System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
            if (r != System.Windows.MessageBoxResult.Yes) return;
            _projectService.Deactivate(_selected.Id);
            Projects.Remove(_selected);
            SelectedProject = Projects.FirstOrDefault();
            if (SelectedProject == null) NewProject();
        }

        private void ApplyFormToProject(Project p)
        {
            p.Name = Name; p.Description = Description;
            p.GitHubRepoOwner = RepoOwner; p.GitHubRepoName = RepoName;
            p.GitHubAccessToken = GhToken; p.DefaultBranch = DefaultBranch;
            p.AiProvider = AiProvider; p.AiApiKey = AiApiKey; p.AiModel = AiModel;
            p.AiTemperature = AiTemperature; p.PrdContent = PrdContent;
            p.ArchitectureNotes = ArchitectureNotes; p.TechStack = TechStack;
            p.SprintDurationDays = SprintDays; p.WorkHoursPerDay = WorkHoursPerDay;
            p.QaCommands = QaCommands;
        }

        private static Project CloneFromForm(Project src) => new()
        {
            Id = src.Id, OrganizationId = src.OrganizationId,
            CreatedAt = src.CreatedAt, CreatedByUserId = src.CreatedByUserId,
            IsActive = src.IsActive, LastSyncedAt = src.LastSyncedAt,
            LastKnownCommitSha = src.LastKnownCommitSha, WebhookSecret = src.WebhookSecret
        };
    }
}
