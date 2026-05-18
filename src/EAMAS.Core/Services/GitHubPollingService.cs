using EAMAS.Core.Data;
using EAMAS.Core.Models;
using EAMAS.Core.Services.AI;
using MongoDB.Driver;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace EAMAS.Core.Services
{
    /// <summary>
    /// Polls GitHub for new commits every 5 minutes per project.
    /// On new commit: fetches diff → triggers AI code review → links to task.
    /// </summary>
    public class GitHubPollingService : IDisposable
    {
        private readonly MongoDbContext _db;
        private readonly ProjectService _projectService;
        private readonly TaskService _taskService;
        private readonly AiCodeReviewService _reviewService;
        private readonly AiProviderFactory _aiFactory;
        private readonly EncryptionService _enc;
        private Timer? _timer;
        private bool _running;
        private readonly HttpClient _http;

        public event Action<string>? StatusChanged;

        public GitHubPollingService(
            MongoDbContext db, ProjectService projectService, TaskService taskService,
            AiCodeReviewService reviewService, AiProviderFactory aiFactory, EncryptionService enc)
        {
            _db = db;
            _projectService = projectService;
            _taskService = taskService;
            _reviewService = reviewService;
            _aiFactory = aiFactory;
            _enc = enc;
            _http = new HttpClient();
            _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("EAMAS", "1.0"));
            _http.Timeout = TimeSpan.FromSeconds(30);
        }

        public void Start()
        {
            if (_running) return;
            _running = true;
            _timer = new Timer(async _ =>
            {
                try { await PollAllProjectsAsync().ConfigureAwait(false); }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[GitHubPollingService] Unhandled error: {ex.GetType().Name}: {ex.Message}");
                }
            }, null, TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(5));
        }

        public void Stop()
        {
            _running = false;
            _timer?.Dispose();
            _timer = null;
        }

        public async Task PollProjectNowAsync(string projectId)
        {
            var proj = _projectService.GetById(projectId);
            if (proj == null) return;
            await PollProjectAsync(proj).ConfigureAwait(false);
        }

        private async Task PollAllProjectsAsync()
        {
            if (!_running) return;
            try
            {
                // Get all active projects across all orgs
                var projects = _db.Projects.Find(p => p.IsActive
                    && !string.IsNullOrEmpty(p.GitHubRepoOwner)
                    && !string.IsNullOrEmpty(p.AiApiKey)).ToList();

                foreach (var project in projects)
                    await PollProjectAsync(project).ConfigureAwait(false);
            }
            catch { /* polling must never crash */ }
        }

        private async Task PollProjectAsync(Project encryptedProject)
        {
            try
            {
                var project = _projectService.DecryptSecrets(encryptedProject);
                if (string.IsNullOrEmpty(project.GitHubAccessToken)) return;

                var provider = _aiFactory.Create(project.AiProvider, project.AiApiKey, project.AiModel, project.AiTemperature);
                var commits = await FetchNewCommitsAsync(project).ConfigureAwait(false);

                if (!commits.Any()) return;

                StatusChanged?.Invoke($"Found {commits.Count} new commit(s) in {project.Name}");

                foreach (var commit in commits)
                {
                    await ProcessCommitAsync(provider, project, commit).ConfigureAwait(false);
                }

                _projectService.UpdateLastSync(project.Id, commits.First().sha);
            }
            catch { /* individual project failure should not stop other projects */ }
        }

        private async Task ProcessCommitAsync(AI.IAiProvider provider, Project project, GitCommit commit)
        {
            // Check if already reviewed
            var existing = _db.CodeReviews.Find(r => r.ProjectId == project.Id && r.CommitSha == commit.sha).Any();
            if (existing) return;

            var changedFiles = await FetchCommitDiffAsync(project, commit.sha).ConfigureAwait(false);

            // Try to match commit to a task via branch name or commit message
            var task = TryMatchTask(project.Id, commit.branch, commit.message);

            string? taskTitle = task?.Title;
            string taskId = task?.Id ?? string.Empty;

            var review = await _reviewService.ReviewCommitAsync(
                provider, project,
                commit.sha, commit.message, commit.author, commit.branch,
                changedFiles, taskTitle).ConfigureAwait(false);

            // Update review with task link
            if (!string.IsNullOrEmpty(taskId))
            {
                _db.CodeReviews.UpdateOne(r => r.Id == review.Id,
                    Builders<CodeReview>.Update.Set(r => r.TaskId, taskId));

                // Move task card based on review result
                if (review.Status == Enums.CodeReviewStatus.Passed && task?.Status == Enums.ProjectTaskStatus.InProgress)
                    _taskService.MoveStatus(taskId, Enums.ProjectTaskStatus.QATesting);
                else if (review.Status == Enums.CodeReviewStatus.Failed)
                    _taskService.MoveStatus(taskId, Enums.ProjectTaskStatus.NeedsFix);

                _taskService.LinkCommit(taskId, commit.sha);
            }
        }

        private async Task<List<GitCommit>> FetchNewCommitsAsync(Project project)
        {
            var since = project.LastSyncedAt?.ToString("o") ?? DateTime.UtcNow.AddHours(-24).ToString("o");
            var url = $"https://api.github.com/repos/{project.GitHubRepoOwner}/{project.GitHubRepoName}/commits?sha={project.DefaultBranch}&since={since}&per_page=20";

            using var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, url);
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", project.GitHubAccessToken);

            var response = await _http.SendAsync(request).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return new List<GitCommit>();

            var json = await response.Content.ReadFromJsonAsync<JsonElement>().ConfigureAwait(false);
            var result = new List<GitCommit>();

            foreach (var item in json.EnumerateArray())
            {
                result.Add(new GitCommit
                {
                    sha = item.GetProperty("sha").GetString() ?? "",
                    message = item.GetProperty("commit").GetProperty("message").GetString()?.Split('\n')[0] ?? "",
                    author = item.GetProperty("commit").GetProperty("author").GetProperty("name").GetString() ?? "",
                    branch = project.DefaultBranch
                });
            }

            // Filter out already-known commits
            if (!string.IsNullOrEmpty(project.LastKnownCommitSha))
                result = result.TakeWhile(c => c.sha != project.LastKnownCommitSha).ToList();

            return result;
        }

        private async Task<List<ChangedFile>> FetchCommitDiffAsync(Project project, string sha)
        {
            var url = $"https://api.github.com/repos/{project.GitHubRepoOwner}/{project.GitHubRepoName}/commits/{sha}";

            using var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, url);
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", project.GitHubAccessToken);

            var response = await _http.SendAsync(request).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return new List<ChangedFile>();

            var json = await response.Content.ReadFromJsonAsync<JsonElement>().ConfigureAwait(false);
            var files = new List<ChangedFile>();

            if (!json.TryGetProperty("files", out var filesEl)) return files;

            foreach (var f in filesEl.EnumerateArray().Take(15))
            {
                files.Add(new ChangedFile
                {
                    FilePath = f.TryGetProperty("filename", out var fn) ? fn.GetString() ?? "" : "",
                    Status = f.TryGetProperty("status", out var st) ? st.GetString() ?? "" : "",
                    Additions = f.TryGetProperty("additions", out var a) ? a.GetInt32() : 0,
                    Deletions = f.TryGetProperty("deletions", out var d) ? d.GetInt32() : 0,
                    Diff = f.TryGetProperty("patch", out var p) ? p.GetString() ?? "" : ""
                });
            }
            return files;
        }

        private ProjectTask? TryMatchTask(string projectId, string branch, string message)
        {
            // Branch convention: task-{objectId}-description or feature/task-{objectId}
            var parts = branch.Split('-', '/');
            foreach (var part in parts)
            {
                if (part.Length == 24) // MongoDB ObjectId length
                {
                    var task = _taskService.GetById(part);
                    if (task?.ProjectId == projectId) return task;
                }
            }

            // Keyword match against active tasks
            var activeTasks = _taskService.GetByStatus(projectId, Enums.ProjectTaskStatus.InProgress);
            var msgWords = message.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return activeTasks.OrderByDescending(t =>
                msgWords.Count(w => t.Title.ToLowerInvariant().Contains(w))).FirstOrDefault();
        }

        public async Task<bool> TestConnectionAsync(string owner, string repo, string token)
        {
            try
            {
                var url = $"https://api.github.com/repos/{owner}/{repo}";
                using var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, url);
                request.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                var response = await _http.SendAsync(request).ConfigureAwait(false);
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        private void SetGitHubAuth(string token)
        {
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }

        public void Dispose()
        {
            _timer?.Dispose();
            _http.Dispose();
        }

        private record GitCommit(string sha = "", string message = "", string author = "", string branch = "");
    }
}
