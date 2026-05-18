using EAMAS.Core.Data;
using EAMAS.Core.Enums;
using EAMAS.Core.Models;
using MongoDB.Driver;

namespace EAMAS.Core.Services
{
    /// <summary>
    /// Seeds a complete demo organisation "TechNova Solutions" with realistic data
    /// across all collections. Safe to call multiple times — skips if org already exists.
    /// Login: org code = TECHNOVA, admin = admin@technova / Admin@123
    /// </summary>
    public class DemoDataSeeder
    {
        private readonly MongoDbContext _db;
        private static readonly Random Rng = new(42);

        public DemoDataSeeder(MongoDbContext db) => _db = db;

        // ── Entry point ───────────────────────────────────────────────────────────
        public void Seed()
        {
            // Idempotency guard
            if (_db.Organizations.CountDocuments(o => o.Code == "TECHNOVA") > 0)
                return;

            var org = CreateOrganization();
            var users = CreateUsers(org);
            CreateSystemSettings(org);
            CreateAppCategoryRules(org);

            var (projAlpha, projMobile) = CreateProjects(org, users);
            var (sprint1, sprint2) = CreateSprints(org, projAlpha);
            var (mSprint1, mSprint2) = CreateSprints(org, projMobile);

            var alphaTasks = CreateTasks(org, projAlpha, sprint2, users);
            var mobileTasks = CreateTasks(org, projMobile, mSprint2, users, offset: 20);

            CreateCodeReviews(org, projAlpha, alphaTasks, users);
            CreateQaResults(org, projAlpha, alphaTasks);
            CreateStandupLogs(org, projAlpha, users);
            CreateActivityLogs(org, users);
            CreateAppUsages(org, users);
            CreateAlerts(org, users);
            CreateScreenshotRecords(org, users);
            CreateAuditLogs(org, users);
            CreateProjectEmbeddings(projAlpha, projMobile);

            // Update org.AdminUserId now that we have the admin user id
            var adminUser = users.First(u => u.Role == UserRole.Admin);
            _db.Organizations.UpdateOne(
                o => o.Id == org.Id,
                Builders<Organization>.Update.Set(o => o.AdminUserId, adminUser.Id));
        }

        // ── Organization ──────────────────────────────────────────────────────────
        private Organization CreateOrganization()
        {
            var org = new Organization
            {
                Code = "TECHNOVA",
                Name = "TechNova Solutions",
                Description = "A cutting-edge software company building next-gen SaaS products.",
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddMonths(-6)
            };
            _db.Organizations.InsertOne(org);
            return org;
        }

        // ── Users ─────────────────────────────────────────────────────────────────
        private List<User> CreateUsers(Organization org)
        {
            var pw = UserService.HashPassword("Admin@123");
            var users = new List<User>
            {
                // Admin
                new() { OrganizationId = org.Id, Username = "admin",
                    PasswordHash = pw, FullName = "Alice Johnson",
                    Email = "alice@technova.io", Department = "Engineering",
                    Role = UserRole.Admin, IsActive = true, ConsentGiven = true,
                    CreatedAt = DateTime.UtcNow.AddMonths(-6),
                    LastLogin = DateTime.UtcNow.AddHours(-2) },

                // Manager
                new() { OrganizationId = org.Id, Username = "manager",
                    PasswordHash = pw, FullName = "Bob Smith",
                    Email = "bob@technova.io", Department = "Engineering",
                    Role = UserRole.Manager, IsActive = true, ConsentGiven = true,
                    CreatedAt = DateTime.UtcNow.AddMonths(-5),
                    LastLogin = DateTime.UtcNow.AddHours(-1) },

                // Manager 2
                new() { OrganizationId = org.Id, Username = "manager2",
                    PasswordHash = pw, FullName = "Sara Lee",
                    Email = "sara@technova.io", Department = "QA",
                    Role = UserRole.Manager, IsActive = true, ConsentGiven = true,
                    CreatedAt = DateTime.UtcNow.AddMonths(-4),
                    LastLogin = DateTime.UtcNow.AddHours(-3) },

                // Employees
                new() { OrganizationId = org.Id, Username = "charlie",
                    PasswordHash = pw, FullName = "Charlie Brown",
                    Email = "charlie@technova.io", Department = "Engineering",
                    Role = UserRole.Employee, IsActive = true, ConsentGiven = true,
                    CreatedAt = DateTime.UtcNow.AddMonths(-4),
                    LastLogin = DateTime.UtcNow.AddMinutes(-30) },

                new() { OrganizationId = org.Id, Username = "diana",
                    PasswordHash = pw, FullName = "Diana Prince",
                    Email = "diana@technova.io", Department = "Engineering",
                    Role = UserRole.Employee, IsActive = true, ConsentGiven = true,
                    CreatedAt = DateTime.UtcNow.AddMonths(-3),
                    LastLogin = DateTime.UtcNow.AddMinutes(-45) },

                new() { OrganizationId = org.Id, Username = "eve",
                    PasswordHash = pw, FullName = "Eve Carter",
                    Email = "eve@technova.io", Department = "QA",
                    Role = UserRole.Employee, IsActive = true, ConsentGiven = true,
                    CreatedAt = DateTime.UtcNow.AddMonths(-3),
                    LastLogin = DateTime.UtcNow.AddHours(-1) },

                new() { OrganizationId = org.Id, Username = "frank",
                    PasswordHash = pw, FullName = "Frank Miller",
                    Email = "frank@technova.io", Department = "Engineering",
                    Role = UserRole.Employee, IsActive = true, ConsentGiven = true,
                    CreatedAt = DateTime.UtcNow.AddMonths(-2),
                    LastLogin = DateTime.UtcNow.AddHours(-4) },
            };

            _db.Users.InsertMany(users);
            return users;
        }

        // ── System Settings ───────────────────────────────────────────────────────
        private void CreateSystemSettings(Organization org)
        {
            _db.SystemSettings.InsertOne(new SystemSettings
            {
                OrganizationId = org.Id,
                MonitoringEnabled = true,
                ScreenshotsEnabled = true,
                ScreenshotIntervalMinutes = 10,
                IdleThresholdSeconds = 300,
                ActivityPollIntervalSeconds = 5,
                MaxScreenshotAgeDays = 30,
                JpegQuality = 75,
                AlertOnLongIdle = true,
                LongIdleThresholdMinutes = 20,
                AlertOnDistractingUsage = true,
                DistractingUsageThresholdMinutes = 45,
                AlertOnLowProductivity = true,
                LowProductivityThresholdPercent = 30,
                LowProductivityMinActiveMinutes = 120,
                AlertOnUnauthorizedApp = true,
                BlockedApplications = "steam,epicgames,origin",
                AlertOnNoActivity = true,
                NoActivityThresholdMinutes = 90,
                PrivacyBlurEnabled = true,
                ActivityLogRetentionDays = 90,
                AlertRetentionDays = 60,
                AuditLogRetentionDays = 365,
                UpdatedAt = DateTime.UtcNow
            });
        }

        // ── App Category Rules ────────────────────────────────────────────────────
        private void CreateAppCategoryRules(Organization org)
        {
            var rules = new List<AppCategoryRule>
            {
                // Productive
                Rule(org.Id, "visual studio", ActivityCategory.Productive, 100),
                Rule(org.Id, "vscode", ActivityCategory.Productive, 100),
                Rule(org.Id, "rider", ActivityCategory.Productive, 100),
                Rule(org.Id, "intellij", ActivityCategory.Productive, 100),
                Rule(org.Id, "github", ActivityCategory.Productive, 90),
                Rule(org.Id, "postman", ActivityCategory.Productive, 90),
                Rule(org.Id, "figma", ActivityCategory.Productive, 90),
                Rule(org.Id, "notion", ActivityCategory.Productive, 80),
                Rule(org.Id, "jira", ActivityCategory.Productive, 80),
                Rule(org.Id, "confluence", ActivityCategory.Productive, 80),
                Rule(org.Id, "zoom", ActivityCategory.Productive, 70),
                Rule(org.Id, "teams", ActivityCategory.Productive, 70),
                Rule(org.Id, "slack", ActivityCategory.Productive, 70),
                Rule(org.Id, "terminal", ActivityCategory.Productive, 90),
                Rule(org.Id, "cmd", ActivityCategory.Productive, 90),
                Rule(org.Id, "powershell", ActivityCategory.Productive, 90),
                // Neutral
                Rule(org.Id, "chrome", ActivityCategory.Neutral, 50),
                Rule(org.Id, "firefox", ActivityCategory.Neutral, 50),
                Rule(org.Id, "edge", ActivityCategory.Neutral, 50),
                Rule(org.Id, "outlook", ActivityCategory.Neutral, 60),
                Rule(org.Id, "word", ActivityCategory.Neutral, 60),
                Rule(org.Id, "excel", ActivityCategory.Neutral, 60),
                // Distracting
                Rule(org.Id, "youtube", ActivityCategory.Distracting, 20),
                Rule(org.Id, "netflix", ActivityCategory.Distracting, 10),
                Rule(org.Id, "twitter", ActivityCategory.Distracting, 20),
                Rule(org.Id, "facebook", ActivityCategory.Distracting, 10),
                Rule(org.Id, "reddit", ActivityCategory.Distracting, 20),
                Rule(org.Id, "steam", ActivityCategory.Distracting, 5),
                Rule(org.Id, "spotify", ActivityCategory.Distracting, 30),
            };
            _db.AppCategoryRules.InsertMany(rules);
        }

        private static AppCategoryRule Rule(string orgId, string keyword, ActivityCategory cat, int priority) =>
            new() { OrganizationId = orgId, Keyword = keyword, Category = cat, Priority = priority, IsActive = true };

        // ── Projects ──────────────────────────────────────────────────────────────
        private (Project alpha, Project mobile) CreateProjects(Organization org, List<User> users)
        {
            var admin = users.First(u => u.Role == UserRole.Admin);

            var alpha = new Project
            {
                OrganizationId = org.Id,
                Name = "EAMAS Platform",
                Description = "Core employee activity monitoring and analytics platform. Multi-tenant SaaS with WPF desktop client.",
                GitHubRepoOwner = "technova-io",
                GitHubRepoName = "eamas-platform",
                GitHubAccessToken = string.Empty,
                DefaultBranch = "main",
                AiProvider = AiProviderType.OpenAI,
                AiApiKey = string.Empty,
                AiModel = "gpt-4o",
                AiTemperature = 0.3,
                PrdContent = "Build a comprehensive employee monitoring system with real-time activity tracking, AI-powered analytics, sprint planning, and code review automation. Must support multi-tenancy, role-based access control, and GDPR compliance.",
                ArchitectureNotes = "WPF .NET 8 desktop client with MongoDB backend. Services layer follows repository pattern. MVVM for UI. MongoDB GridFS for screenshots. AI providers: OpenAI / Claude / Gemini.",
                TechStack = "C#, .NET 8, WPF, MongoDB, GridFS, OpenAI API, Claude API",
                SprintDurationDays = 14,
                WorkHoursPerDay = 8,
                QaCommands = "dotnet build;dotnet test --logger console",
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddMonths(-5),
                CreatedByUserId = admin.Id,
                LastSyncedAt = DateTime.UtcNow.AddMinutes(-15),
                LastKnownCommitSha = "a3f9d12"
            };

            var mobile = new Project
            {
                OrganizationId = org.Id,
                Name = "TechNova Mobile",
                Description = "Companion mobile app for managers to view team analytics on the go.",
                GitHubRepoOwner = "technova-io",
                GitHubRepoName = "technova-mobile",
                GitHubAccessToken = string.Empty,
                DefaultBranch = "develop",
                AiProvider = AiProviderType.Claude,
                AiApiKey = string.Empty,
                AiModel = "claude-sonnet-4-6",
                AiTemperature = 0.4,
                PrdContent = "React Native mobile app that surfaces EAMAS analytics for managers. Features: dashboard, team productivity charts, alert notifications, and sprint overview.",
                ArchitectureNotes = "React Native with TypeScript. REST API backend (ASP.NET Core). Push notifications via Firebase. Charts with Victory Native.",
                TechStack = "React Native, TypeScript, ASP.NET Core, Firebase, MongoDB Atlas",
                SprintDurationDays = 14,
                WorkHoursPerDay = 8,
                QaCommands = "npm test;npx jest --coverage",
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddMonths(-3),
                CreatedByUserId = admin.Id,
                LastSyncedAt = DateTime.UtcNow.AddMinutes(-30),
                LastKnownCommitSha = "b7e4c89"
            };

            _db.Projects.InsertMany(new[] { alpha, mobile });
            return (alpha, mobile);
        }

        // ── Sprints ───────────────────────────────────────────────────────────────
        private (Sprint completed, Sprint active) CreateSprints(Organization org, Project proj)
        {
            var sprint1 = new Sprint
            {
                OrganizationId = org.Id,
                ProjectId = proj.Id,
                Name = "Sprint 1",
                Goal = "Set up project foundation, authentication, and core data models",
                StartDate = DateTime.UtcNow.AddDays(-42),
                EndDate = DateTime.UtcNow.AddDays(-28),
                Status = SprintStatus.Completed,
                PlannedVelocity = 64,
                ActualVelocity = 58,
                AiSprintSummary = "Sprint completed with 90% velocity. Team delivered core auth, MongoDB setup, and initial UI shell. 2 tasks moved to backlog due to scope changes. No major blockers encountered. Recommended focus for next sprint: activity tracking and dashboard.",
                CreatedAt = DateTime.UtcNow.AddDays(-44)
            };

            var sprint2 = new Sprint
            {
                OrganizationId = org.Id,
                ProjectId = proj.Id,
                Name = "Sprint 2",
                Goal = "Implement AI Engineering Manager features and Kanban board",
                StartDate = DateTime.UtcNow.AddDays(-14),
                EndDate = DateTime.UtcNow.AddDays(0),
                Status = SprintStatus.Active,
                PlannedVelocity = 72,
                ActualVelocity = 0,
                CreatedAt = DateTime.UtcNow.AddDays(-16)
            };

            _db.Sprints.InsertMany(new[] { sprint1, sprint2 });
            sprint1.TaskIds = new List<string>();
            sprint2.TaskIds = new List<string>();
            return (sprint1, sprint2);
        }

        // ── Tasks ─────────────────────────────────────────────────────────────────
        private List<ProjectTask> CreateTasks(Organization org, Project proj, Sprint activeSprint,
            List<User> users, int offset = 0)
        {
            var employees = users.Where(u => u.Role == UserRole.Employee).ToList();
            var manager = users.First(u => u.Role == UserRole.Manager);
            var now = DateTime.UtcNow;

            var taskDefs = new[]
            {
                // Backlog
                (Title: "Implement sprint burndown chart", Desc: "Create a real-time burndown chart showing remaining work vs ideal line for the active sprint.",
                 Criteria: "Chart updates daily. Shows ideal vs actual velocity. Exports as PNG.",
                 Status: ProjectTaskStatus.Backlog, Priority: TaskPriority.Medium, Hours: 8.0,
                 Labels: new[]{"feature","charts"}, Subs: new[]{"Design chart layout","Fetch sprint tasks daily","Render SVG burndown","Add export button"}),

                (Title: "GitHub webhook real-time support", Desc: "Replace 5-minute polling with GitHub webhooks for instant code review triggers.",
                 Criteria: "Webhooks verified with HMAC-SHA256. Fallback to polling if webhook fails.",
                 Status: ProjectTaskStatus.Backlog, Priority: TaskPriority.High, Hours: 6.0,
                 Labels: new[]{"integration","github"}, Subs: new[]{"Register webhook endpoint","Verify HMAC signature","Parse push events","Trigger review pipeline"}),

                (Title: "Mobile companion app dashboard", Desc: "Build the main dashboard screen for the mobile app showing team productivity overview.",
                 Criteria: "Shows today's team productivity score, active alerts count, top apps used.",
                 Status: ProjectTaskStatus.Backlog, Priority: TaskPriority.Low, Hours: 10.0,
                 Labels: new[]{"mobile","feature"}, Subs: new[]{"Design dashboard UI","Connect to REST API","Render productivity chart","Handle offline mode"}),

                // Todo
                (Title: "Add team productivity leaderboard", Desc: "Gamified leaderboard showing relative productivity scores across team members.",
                 Criteria: "Updates daily. Anonymized view for employees. Full view for managers.",
                 Status: ProjectTaskStatus.Todo, Priority: TaskPriority.Medium, Hours: 5.0,
                 Labels: new[]{"feature","analytics"}, Subs: new[]{"Design score algorithm","Create leaderboard UI","Add anonymization toggle","Cache daily scores"}),

                (Title: "Export analytics report to PDF", Desc: "Allow managers to export the analytics dashboard as a formatted PDF report.",
                 Criteria: "Includes charts, summary stats, and per-user breakdown. Branded header.",
                 Status: ProjectTaskStatus.Todo, Priority: TaskPriority.Medium, Hours: 6.0,
                 Labels: new[]{"feature","reporting"}, Subs: new[]{"Integrate PDF library","Capture chart images","Format multi-page layout","Add date range filter"}),

                // In Progress
                (Title: "Implement AI standup generation", Desc: "Use AI to generate daily standup summaries for each team member based on their activity and commits.",
                 Criteria: "Generates for all active employees. Respects project context. Tone: professional.",
                 Status: ProjectTaskStatus.InProgress, Priority: TaskPriority.High, Hours: 8.0,
                 Labels: new[]{"ai","feature"}, Subs: new[]{"Design standup prompt","Integrate with activity logs","Integrate with GitHub commits","Store standup logs","Display in TasksView"}),

                (Title: "Sprint velocity tracking", Desc: "Track and display planned vs actual sprint velocity with trend analysis.",
                 Criteria: "Velocity calculated per sprint. Historical chart shows 5 sprints. Tooltip on hover.",
                 Status: ProjectTaskStatus.InProgress, Priority: TaskPriority.Medium, Hours: 4.0,
                 Labels: new[]{"analytics","sprint"}, Subs: new[]{"Calculate actual velocity on completion","Store velocity history","Render trend chart","Add sprint comparison view"}),

                // Code Review
                (Title: "Fix idle detection race condition", Desc: "Idle timer occasionally fires twice due to a race condition in the dispatcher timer callback.",
                 Criteria: "Idle detection fires exactly once per threshold. No duplicate alerts.",
                 Status: ProjectTaskStatus.CodeReview, Priority: TaskPriority.Critical, Hours: 3.0,
                 Labels: new[]{"bug","monitoring"}, Subs: new[]{"Reproduce race condition","Add mutex around idle state","Write unit test","Verify in integration"}),

                (Title: "Add GDPR data export endpoint", Desc: "Allow employees to export all their personal data as a ZIP archive complying with GDPR Article 20.",
                 Criteria: "Exports activity logs, screenshots, alerts. ZIP encrypted with user password.",
                 Status: ProjectTaskStatus.CodeReview, Priority: TaskPriority.High, Hours: 7.0,
                 Labels: new[]{"compliance","gdpr"}, Subs: new[]{"Design export format","Collect all user data","Package as ZIP","Add progress indicator"}),

                // QA Testing
                (Title: "Screenshot privacy blur implementation", Desc: "Automatically blur sensitive content (passwords, credit cards) in screenshots before storage.",
                 Criteria: "Detects sensitive fields with >90% accuracy. Processing under 500ms per screenshot.",
                 Status: ProjectTaskStatus.QATesting, Priority: TaskPriority.High, Hours: 8.0,
                 Labels: new[]{"privacy","screenshots"}, Subs: new[]{"Implement blur algorithm","Add sensitivity detection","Write blur tests","Benchmark performance"}),

                // Needs Fix
                (Title: "MongoDB connection timeout on cold start", Desc: "App occasionally hangs for 30+ seconds on first launch due to MongoDB connection timeout.",
                 Criteria: "Connection established within 10 seconds on cold start. User sees progress indicator.",
                 Status: ProjectTaskStatus.NeedsFix, Priority: TaskPriority.Critical, Hours: 4.0,
                 Labels: new[]{"bug","performance"}, Subs: new[]{"Profile connection timing","Optimize connection pool","Add startup timeout UI","Add retry logic"}),

                // Done
                (Title: "Implement multi-tenant organization management", Desc: "SuperAdmin can create, edit, and deactivate organizations. Each org has isolated data.",
                 Criteria: "Org data fully isolated. SuperAdmin can switch orgs. Deletion is soft-delete.",
                 Status: ProjectTaskStatus.Done, Priority: TaskPriority.Critical, Hours: 12.0,
                 Labels: new[]{"architecture","feature"}, Subs: new[]{"Design org schema","Implement org CRUD","Add data isolation","Create SuperAdmin UI"}),

                (Title: "Role-based access control (RBAC)", Desc: "Implement SuperAdmin, Admin, Manager, Employee roles with fine-grained permissions.",
                 Criteria: "Each role can only access permitted views. Unauthorized access shows 403 screen.",
                 Status: ProjectTaskStatus.Done, Priority: TaskPriority.Critical, Hours: 8.0,
                 Labels: new[]{"security","feature"}, Subs: new[]{"Define permission matrix","Add role guards","Test each role scenario","Document permissions"}),

                (Title: "Activity log collection service", Desc: "Background Windows service that tracks foreground application usage and window titles.",
                 Criteria: "Captures app switches within 5 seconds. Handles locked screen gracefully.",
                 Status: ProjectTaskStatus.Done, Priority: TaskPriority.Critical, Hours: 10.0,
                 Labels: new[]{"monitoring","feature"}, Subs: new[]{"Implement WinAPI hooks","Track foreground window","Categorize by rules","Write to MongoDB"}),

                (Title: "Login window with brute-force protection", Desc: "Secure login with org code + username + password. Locks account after 5 failed attempts.",
                 Criteria: "Account locked for 15 minutes after 5 failures. Lockout stored in DB.",
                 Status: ProjectTaskStatus.Done, Priority: TaskPriority.High, Hours: 6.0,
                 Labels: new[]{"security","auth"}, Subs: new[]{"Design login UI","Implement lockout logic","Add session tokens","Test brute-force scenarios"}),
            };

            var tasks = new List<ProjectTask>();
            int pos = 0;

            foreach (var (t, i) in taskDefs.Select((t, i) => (t, i)))
            {
                var emp = employees[i % employees.Count];
                var isAssigned = t.Status != ProjectTaskStatus.Backlog;
                var task = new ProjectTask
                {
                    OrganizationId = org.Id,
                    ProjectId = proj.Id,
                    SprintId = t.Status != ProjectTaskStatus.Backlog ? activeSprint.Id : string.Empty,
                    Title = t.Title,
                    Description = t.Desc,
                    AcceptanceCriteria = t.Criteria,
                    AssignedToUserId = isAssigned ? emp.Id : string.Empty,
                    AssignedToUserName = isAssigned ? emp.FullName : string.Empty,
                    CreatedByUserId = manager.Id,
                    Status = t.Status,
                    BoardPosition = pos++,
                    Priority = t.Priority,
                    Labels = t.Labels.ToList(),
                    EstimatedHours = t.Hours,
                    ActualHours = t.Status == ProjectTaskStatus.Done ? t.Hours * (0.8 + Rng.NextDouble() * 0.4) : 0,
                    DueDate = DateTime.UtcNow.AddDays(7 - i),
                    IsAiGenerated = i % 3 == 0,
                    AiGeneratedSummary = i % 3 == 0 ? $"AI-generated task: {t.Title}. Estimated complexity: Medium." : string.Empty,
                    SubTasks = t.Subs.ToList(),
                    RelatedCommitSha = t.Status >= ProjectTaskStatus.CodeReview
                        ? $"{GenerateSha(6)}" : string.Empty,
                    GitHubPrUrl = t.Status >= ProjectTaskStatus.CodeReview
                        ? $"https://github.com/technova-io/{proj.GitHubRepoName}/pull/{10 + i}" : string.Empty,
                    CreatedAt = now.AddDays(-30 + i),
                    UpdatedAt = now.AddDays(-2),
                    StartedAt = t.Status >= ProjectTaskStatus.InProgress ? now.AddDays(-10 + i) : null,
                    CompletedAt = t.Status == ProjectTaskStatus.Done ? now.AddDays(-5 + i % 3) : null,
                };
                tasks.Add(task);
            }

            _db.Tasks.InsertMany(tasks);

            // Update sprint task IDs
            var sprintTaskIds = tasks.Where(t => t.SprintId == activeSprint.Id).Select(t => t.Id).ToList();
            _db.Sprints.UpdateOne(s => s.Id == activeSprint.Id,
                Builders<Sprint>.Update.Set(s => s.TaskIds, sprintTaskIds));

            return tasks;
        }

        // ── Code Reviews ──────────────────────────────────────────────────────────
        private void CreateCodeReviews(Organization org, Project proj, List<ProjectTask> tasks, List<User> users)
        {
            var reviewableTasks = tasks.Where(t =>
                t.Status >= ProjectTaskStatus.CodeReview && !string.IsNullOrEmpty(t.RelatedCommitSha)).ToList();

            var reviews = new List<CodeReview>();
            var reviewStatuses = new[] { CodeReviewStatus.Passed, CodeReviewStatus.Failed, CodeReviewStatus.NeedsHumanReview, CodeReviewStatus.Passed, CodeReviewStatus.InProgress };
            var qaStatuses = new[] { QaRunStatus.Passed, QaRunStatus.Failed, QaRunStatus.Passed, QaRunStatus.Running, QaRunStatus.Queued };
            var branches = new[] { "feat/standup-ai", "fix/idle-race", "feat/gdpr-export", "feat/privacy-blur", "fix/mongo-timeout" };

            for (int i = 0; i < Math.Min(reviewableTasks.Count, 5); i++)
            {
                var task = reviewableTasks[i];
                var emp = users.Where(u => u.Role == UserRole.Employee).ElementAt(i % 4);
                var score = reviewStatuses[i] == CodeReviewStatus.Passed ? Rng.Next(75, 98) :
                            reviewStatuses[i] == CodeReviewStatus.Failed ? Rng.Next(30, 55) : Rng.Next(55, 75);

                reviews.Add(new CodeReview
                {
                    OrganizationId = org.Id,
                    ProjectId = proj.Id,
                    TaskId = task.Id,
                    AssignedUserId = emp.Id,
                    CommitSha = task.RelatedCommitSha,
                    CommitMessage = $"feat: implement {task.Title.ToLower()}",
                    CommitAuthor = emp.FullName,
                    Branch = branches[i % branches.Length],
                    ChangedFiles = new List<ChangedFile>
                    {
                        new() { FilePath = $"src/Services/{task.Title.Replace(" ", "")}.cs",
                                Status = "modified", Additions = Rng.Next(40, 200), Deletions = Rng.Next(5, 40),
                                Diff = $"// Implementation of {task.Title}\n+ public async Task ExecuteAsync() {{ ... }}" },
                        new() { FilePath = $"src/Tests/{task.Title.Replace(" ", "")}Tests.cs",
                                Status = "added", Additions = Rng.Next(20, 80), Deletions = 0,
                                Diff = $"// Tests for {task.Title}\n+ [Fact] public async Task ShouldExecuteSuccessfully() {{ ... }}" }
                    },
                    Status = reviewStatuses[i],
                    OverallScore = score,
                    AiSummary = $"Code quality score: {score}/100. " + (score >= 75
                        ? "Implementation is clean and well-structured. Good use of async patterns and error handling. Unit tests cover main paths."
                        : "Several issues found. Consider adding error handling for edge cases and improving test coverage. Magic numbers should be extracted to constants."),
                    Issues = GenerateCodeIssues(score),
                    Suggestions = new List<string>
                    {
                        "Consider adding cancellation token support for async operations.",
                        "Extract magic numbers to named constants for better readability.",
                        "Add XML documentation to public methods.",
                        "Consider implementing retry logic for external service calls."
                    }.Take(score >= 75 ? 1 : 3).ToList(),
                    RequiresHumanApproval = score < 80,
                    QaStatus = qaStatuses[i],
                    QaLog = qaStatuses[i] == QaRunStatus.Passed
                        ? "Build succeeded.\nAll 47 tests passed.\nCode coverage: 78%.\nNo critical warnings."
                        : qaStatuses[i] == QaRunStatus.Failed
                            ? "Build succeeded.\n3 tests failed:\n  - ShouldHandleEmptyInput: Expected 'empty' but got null\n  - ShouldRetryOnTimeout: Timeout after 5000ms\nCode coverage: 52%."
                            : "QA in progress...",
                    CreatedAt = DateTime.UtcNow.AddDays(-i - 1),
                    AiProviderUsed = "OpenAI/gpt-4o"
                });
            }

            _db.CodeReviews.InsertMany(reviews);
        }

        private static List<CodeIssue> GenerateCodeIssues(int score)
        {
            var issues = new List<CodeIssue>();
            if (score < 80)
            {
                issues.Add(new CodeIssue { Severity = "warning", FilePath = "src/Services/Core.cs", LineNumber = 42,
                    Category = "Maintainability", Description = "Magic number 300 should be extracted to a named constant.",
                    SuggestedFix = "private const int IdleThresholdSeconds = 300;" });
                issues.Add(new CodeIssue { Severity = "info", FilePath = "src/Services/Core.cs", LineNumber = 87,
                    Category = "Performance", Description = "Consider caching this LINQ query result — it runs on every property access.",
                    SuggestedFix = "Cache with Lazy<T> or memoize the result in a backing field." });
            }
            if (score < 60)
            {
                issues.Add(new CodeIssue { Severity = "error", FilePath = "src/Services/Core.cs", LineNumber = 65,
                    Category = "Security", Description = "User input is concatenated into a MongoDB filter string without sanitization.",
                    SuggestedFix = "Use parameterized Builders<T>.Filter.Eq() instead of raw string filters." });
            }
            return issues;
        }

        // ── QA Results ────────────────────────────────────────────────────────────
        private void CreateQaResults(Organization org, Project proj, List<ProjectTask> tasks)
        {
            var done = tasks.Where(t => t.Status == ProjectTaskStatus.Done).ToList();
            var results = new List<QaResult>();

            foreach (var (task, i) in done.Take(3).Select((t, i) => (t, i)))
            {
                var passed = i != 1;
                results.Add(new QaResult
                {
                    OrganizationId = org.Id,
                    ProjectId = proj.Id,
                    TaskId = task.Id,
                    CommitSha = GenerateSha(7),
                    Status = passed ? QaRunStatus.Passed : QaRunStatus.Failed,
                    Checks = new List<QaCheck>
                    {
                        new() { Name = "dotnet build", Passed = true, ExitCode = 0,
                            Output = "Build succeeded. 0 errors, 2 warnings.", DurationMs = 3200 },
                        new() { Name = "dotnet test", Passed = passed, ExitCode = passed ? 0 : 1,
                            Output = passed
                                ? "Passed! All 47 tests succeeded. Coverage: 79%."
                                : "FAILED! 3 of 47 tests failed.\n  × ShouldHandleNullInput\n  × ShouldRetryOnFailure",
                            DurationMs = 8700 },
                        new() { Name = "dotnet test --filter Category=Integration", Passed = passed, ExitCode = passed ? 0 : 1,
                            Output = passed ? "12 integration tests passed." : "2 integration tests failed.",
                            DurationMs = 4100 }
                    },
                    AiQaSummary = passed
                        ? "All checks passed. Code quality is good. Feature implementation matches task acceptance criteria. No regressions detected."
                        : "Build succeeded but 3 unit tests failed. Feature partially implemented. Missing null handling in edge cases. Recommend fixing before merge.",
                    FeatureMatchesTask = passed,
                    FeatureMatchReason = passed
                        ? "Implementation matches all acceptance criteria defined in the task."
                        : "Core feature works but 2 of 5 acceptance criteria not yet met.",
                    StartedAt = DateTime.UtcNow.AddDays(-i - 2),
                    CompletedAt = DateTime.UtcNow.AddDays(-i - 2).AddMinutes(16)
                });
            }

            _db.QaResults.InsertMany(results);
        }

        // ── Standup Logs ──────────────────────────────────────────────────────────
        private void CreateStandupLogs(Organization org, Project proj, List<User> users)
        {
            var employees = users.Where(u => u.Role is UserRole.Employee or UserRole.Manager).ToList();
            var logs = new List<StandupLog>();

            string[] yesterdayDone =
            {
                "Completed authentication flow implementation and fixed JWT expiry edge case.",
                "Reviewed 3 PRs, merged 2. Investigated performance regression in activity monitor.",
                "Implemented privacy blur algorithm for screenshots. Added unit tests.",
                "Fixed MongoDB connection timeout on cold start. Deployed to staging.",
                "Wrote integration tests for sprint velocity tracking feature.",
                "Designed new UI components for the Kanban board. Synced with team on UX.",
                "Refactored AI standup service to use new prompt template. Improved accuracy.",
            };
            string[] todayPlan =
            {
                "Implement GDPR data export endpoint and write integration tests.",
                "Continue AI standup review. Start burndown chart implementation.",
                "Optimize screenshot processing pipeline to reduce latency below 500ms.",
                "Implement GitHub webhook support to replace polling mechanism.",
                "Add team productivity leaderboard and daily score caching.",
                "Review code review feedback, fix issues, re-submit for approval.",
                "Implement PDF export for analytics reports.",
            };
            string[] blockers =
            {
                string.Empty,
                "Waiting on design mockups for the new dashboard layout.",
                string.Empty,
                string.Empty,
                "Need access to production MongoDB to debug index performance issue.",
                string.Empty,
                string.Empty,
            };

            for (int day = 0; day < 3; day++)
            {
                foreach (var (user, i) in employees.Select((u, i) => (u, i)))
                {
                    logs.Add(new StandupLog
                    {
                        OrganizationId = org.Id,
                        ProjectId = proj.Id,
                        UserId = user.Id,
                        UserName = user.FullName,
                        Date = DateTime.UtcNow.AddDays(-day).Date,
                        YesterdayAccomplished = yesterdayDone[(i + day) % yesterdayDone.Length],
                        TodayFocus = todayPlan[(i + day) % todayPlan.Length],
                        Blockers = blockers[(i + day) % blockers.Length],
                        AiGeneratedMessage = $"[AI] {user.FullName} had a productive day. Key deliverable: {yesterdayDone[(i + day) % yesterdayDone.Length]}. Focus today: {todayPlan[(i + day) % todayPlan.Length]}.",
                        TasksCompletedYesterday = new List<string> { $"Task-{10 + i}", $"Task-{20 + i}" },
                        TasksInProgressToday = new List<string> { $"Task-{30 + i}" },
                        CommitsYesterday = Rng.Next(0, 6),
                        GeneratedAt = DateTime.UtcNow.AddDays(-day).Date.AddHours(9)
                    });
                }
            }

            _db.StandupLogs.InsertMany(logs);
        }

        // ── Activity Logs ─────────────────────────────────────────────────────────
        private void CreateActivityLogs(Organization org, List<User> users)
        {
            var employees = users.Where(u => u.Role == UserRole.Employee).ToList();
            var logs = new List<ActivityLog>();

            var productiveApps = new[]
            {
                ("Visual Studio 2022", "devenv", ActivityCategory.Productive),
                ("VS Code", "code", ActivityCategory.Productive),
                ("GitHub Desktop", "github", ActivityCategory.Productive),
                ("Postman", "postman", ActivityCategory.Productive),
                ("Windows Terminal", "wt", ActivityCategory.Productive),
            };
            var neutralApps = new[]
            {
                ("Google Chrome", "chrome", ActivityCategory.Neutral),
                ("Microsoft Outlook", "outlook", ActivityCategory.Neutral),
                ("Microsoft Teams", "teams", ActivityCategory.Neutral),
                ("Slack", "slack", ActivityCategory.Neutral),
            };
            var distractingApps = new[]
            {
                ("YouTube", "chrome", ActivityCategory.Distracting),
                ("Reddit", "chrome", ActivityCategory.Distracting),
                ("Spotify", "spotify", ActivityCategory.Distracting),
            };

            string[] windowTitles =
            {
                "EAMAS.Desktop — Visual Studio 2022",
                "TasksViewModel.cs - EAMAS.Desktop",
                "GitHub - eamas-platform — Chrome",
                "Sprint Planning — Confluence",
                "Inbox — Outlook",
                "standup — #engineering — Slack",
                "YouTube — Chrome",
                "Postman — API Testing",
                "Windows Terminal",
            };

            foreach (var user in employees)
            {
                for (int day = 0; day < 7; day++)
                {
                    var dayStart = DateTime.UtcNow.AddDays(-day).Date.AddHours(9);
                    var current = dayStart;

                    while (current < dayStart.AddHours(8))
                    {
                        var rand = Rng.NextDouble();
                        (string app, string process, ActivityCategory cat) = rand < 0.60
                            ? productiveApps[Rng.Next(productiveApps.Length)]
                            : rand < 0.85
                                ? neutralApps[Rng.Next(neutralApps.Length)]
                                : distractingApps[Rng.Next(distractingApps.Length)];

                        var durationMins = Rng.Next(5, 45);
                        var end = current.AddMinutes(durationMins);

                        logs.Add(new ActivityLog
                        {
                            OrganizationId = org.Id,
                            UserId = user.Id,
                            StartTime = current,
                            EndTime = end,
                            ApplicationName = app,
                            ProcessName = process,
                            WindowTitle = windowTitles[Rng.Next(windowTitles.Length)],
                            Category = cat,
                            IsIdle = false,
                            IsScreenLocked = false
                        });

                        current = end;

                        // Occasional idle gap
                        if (Rng.NextDouble() < 0.1)
                        {
                            var idleEnd = current.AddMinutes(Rng.Next(5, 20));
                            logs.Add(new ActivityLog
                            {
                                OrganizationId = org.Id, UserId = user.Id,
                                StartTime = current, EndTime = idleEnd,
                                ApplicationName = "Idle", ProcessName = "idle",
                                WindowTitle = string.Empty, Category = ActivityCategory.Neutral,
                                IsIdle = true, IsScreenLocked = false
                            });
                            current = idleEnd;
                        }
                    }
                }
            }

            _db.ActivityLogs.InsertMany(logs);
        }

        // ── App Usages ────────────────────────────────────────────────────────────
        private void CreateAppUsages(Organization org, List<User> users)
        {
            var employees = users.Where(u => u.Role == UserRole.Employee).ToList();
            var usages = new List<AppUsage>();

            var apps = new[]
            {
                ("Visual Studio 2022", "devenv", ActivityCategory.Productive, 180),
                ("VS Code", "code", ActivityCategory.Productive, 120),
                ("GitHub Desktop", "github", ActivityCategory.Productive, 45),
                ("Postman", "postman", ActivityCategory.Productive, 30),
                ("Windows Terminal", "wt", ActivityCategory.Productive, 60),
                ("Google Chrome", "chrome", ActivityCategory.Neutral, 90),
                ("Microsoft Teams", "teams", ActivityCategory.Neutral, 60),
                ("Slack", "slack", ActivityCategory.Neutral, 45),
                ("Microsoft Outlook", "outlook", ActivityCategory.Neutral, 30),
                ("YouTube", "chrome", ActivityCategory.Distracting, 20),
                ("Spotify", "spotify", ActivityCategory.Distracting, 40),
                ("Reddit", "chrome", ActivityCategory.Distracting, 15),
            };

            foreach (var user in employees)
            {
                for (int day = 0; day < 7; day++)
                {
                    foreach (var (app, process, cat, baseMinutes) in apps)
                    {
                        var variance = (int)(baseMinutes * 0.3);
                        var minutes = Math.Max(1, baseMinutes + Rng.Next(-variance, variance));

                        usages.Add(new AppUsage
                        {
                            OrganizationId = org.Id,
                            UserId = user.Id,
                            ApplicationName = app,
                            ProcessName = process,
                            Duration = TimeSpan.FromMinutes(minutes),
                            RecordedAt = DateTime.UtcNow.AddDays(-day).Date,
                            Category = cat
                        });
                    }
                }
            }

            _db.AppUsages.InsertMany(usages);
        }

        // ── Alerts ────────────────────────────────────────────────────────────────
        private void CreateAlerts(Organization org, List<User> users)
        {
            var employees = users.Where(u => u.Role == UserRole.Employee).ToList();
            var alerts = new List<Alert>
            {
                new() { OrganizationId = org.Id, UserId = employees[0].Id,
                    Type = AlertType.LongIdle, IsRead = true, IsResolved = true,
                    Message = $"{employees[0].FullName} was idle for 47 minutes (threshold: 20 min) on {DateTime.UtcNow.AddDays(-1):MMM d}.",
                    CreatedAt = DateTime.UtcNow.AddDays(-1).AddHours(2) },

                new() { OrganizationId = org.Id, UserId = employees[1].Id,
                    Type = AlertType.DistractingUsage, IsRead = true, IsResolved = false,
                    Message = $"{employees[1].FullName} spent 68 minutes on distracting apps today (threshold: 45 min). Top: YouTube (28 min), Reddit (22 min).",
                    CreatedAt = DateTime.UtcNow.AddHours(-3) },

                new() { OrganizationId = org.Id, UserId = employees[2].Id,
                    Type = AlertType.LowProductivity, IsRead = false, IsResolved = false,
                    Message = $"{employees[2].FullName} productivity score is 24% today (threshold: 30%). Active time: 2h 10min.",
                    CreatedAt = DateTime.UtcNow.AddHours(-1) },

                new() { OrganizationId = org.Id, UserId = employees[3].Id,
                    Type = AlertType.NoActivity, IsRead = false, IsResolved = false,
                    Message = $"{employees[3].FullName} had no activity detected for 95 minutes between 10:30 AM – 12:05 PM.",
                    CreatedAt = DateTime.UtcNow.AddHours(-2) },

                new() { OrganizationId = org.Id, UserId = employees[0].Id,
                    Type = AlertType.UnauthorizedApp, IsRead = true, IsResolved = true,
                    Message = $"{employees[0].FullName} launched 'steam.exe' which is on the blocked applications list.",
                    CreatedAt = DateTime.UtcNow.AddDays(-2) },

                new() { OrganizationId = org.Id, UserId = employees[1].Id,
                    Type = AlertType.LongIdle, IsRead = false, IsResolved = false,
                    Message = $"{employees[1].FullName} was idle for 32 minutes (threshold: 20 min).",
                    CreatedAt = DateTime.UtcNow.AddMinutes(-30) },

                new() { OrganizationId = org.Id, UserId = employees[2].Id,
                    Type = AlertType.DistractingUsage, IsRead = false, IsResolved = false,
                    Message = $"{employees[2].FullName} spent 52 minutes on Spotify today.",
                    CreatedAt = DateTime.UtcNow.AddHours(-4) },
            };

            _db.Alerts.InsertMany(alerts);
        }

        // ── Screenshot Records ────────────────────────────────────────────────────
        private void CreateScreenshotRecords(Organization org, List<User> users)
        {
            var employees = users.Where(u => u.Role == UserRole.Employee).ToList();
            var records = new List<ScreenshotRecord>();

            var apps = new[]
            {
                "Visual Studio 2022", "VS Code", "Google Chrome", "Microsoft Teams",
                "Slack", "Postman", "Windows Terminal", "Microsoft Outlook",
                "File Explorer", "GitHub Desktop"
            };

            // Create a tiny placeholder thumbnail (real ones are 240×135 JPEG)
            byte[] fakeThumbnail = GenerateFakeThumbnailBytes();

            foreach (var user in employees)
            {
                for (int day = 0; day < 7; day++)
                {
                    var dayBase = DateTime.UtcNow.AddDays(-day).Date.AddHours(9);
                    var shotsPerDay = Rng.Next(6, 14);

                    for (int shot = 0; shot < shotsPerDay; shot++)
                    {
                        var takenAt = dayBase.AddMinutes(shot * Rng.Next(20, 50));
                        if (takenAt > DateTime.UtcNow) break;

                        var isManual = Rng.NextDouble() < 0.1;
                        var isBlurred = Rng.NextDouble() < 0.08;
                        var blurLevel = isBlurred
                            ? (Rng.NextDouble() < 0.5 ? "Full" : "Partial")
                            : "None";
                        var blurReason = isBlurred
                            ? (blurLevel == "Full" ? "Personal app detected: whatsapp" : "Sensitive browser page: banking")
                            : null;

                        records.Add(new ScreenshotRecord
                        {
                            OrganizationId = org.Id,
                            UserId = user.Id,
                            TakenAt = takenAt,
                            ImageGridFsId = null,  // No actual GridFS image in seed data
                            ThumbnailData = fakeThumbnail,
                            FilePath = string.Empty,
                            ThumbnailPath = string.Empty,
                            ApplicationName = apps[Rng.Next(apps.Length)],
                            FileSizeBytes = Rng.Next(80_000, 350_000),
                            IsSensitive = isBlurred,
                            IsManual = isManual,
                            IsPrivacyBlurred = isBlurred,
                            PrivacyBlurLevel = blurLevel,
                            PrivacyBlurReason = blurReason
                        });
                    }
                }
            }

            _db.ScreenshotRecords.InsertMany(records);
        }

        // ── Audit Logs ───────────────────────────────────────────────────────────
        private void CreateAuditLogs(Organization org, List<User> users)
        {
            var admin = users.First(u => u.Role == UserRole.Admin);
            var manager = users.First(u => u.Role == UserRole.Manager);
            var employees = users.Where(u => u.Role == UserRole.Employee).ToList();

            var logs = new List<AuditLog>
            {
                new()
                {
                    OrganizationId = org.Id,
                    ActorUserId = admin.Id,
                    ActorName = admin.FullName,
                    Action = "OrganizationCreated",
                    Details = $"Created organization '{org.Name}' with code {org.Code}.",
                    Timestamp = DateTime.UtcNow.AddMonths(-6)
                },
                new()
                {
                    OrganizationId = org.Id,
                    ActorUserId = admin.Id,
                    ActorName = admin.FullName,
                    Action = "UserCreated",
                    Details = $"Created manager account '{manager.Username}' for {manager.FullName}.",
                    Timestamp = DateTime.UtcNow.AddMonths(-5)
                },
                new()
                {
                    OrganizationId = org.Id,
                    ActorUserId = admin.Id,
                    ActorName = admin.FullName,
                    Action = "UserCreated",
                    Details = $"Created employee account '{employees[0].Username}' for {employees[0].FullName}.",
                    Timestamp = DateTime.UtcNow.AddMonths(-4)
                },
                new()
                {
                    OrganizationId = org.Id,
                    ActorUserId = admin.Id,
                    ActorName = admin.FullName,
                    Action = "UserCreated",
                    Details = $"Created employee account '{employees[1].Username}' for {employees[1].FullName}.",
                    Timestamp = DateTime.UtcNow.AddMonths(-3)
                },
                new()
                {
                    OrganizationId = org.Id,
                    ActorUserId = admin.Id,
                    ActorName = admin.FullName,
                    Action = "SettingsChanged",
                    Details = "Updated monitoring settings: screenshot interval changed from 5 to 10 minutes, privacy blur enabled.",
                    Timestamp = DateTime.UtcNow.AddDays(-60)
                },
                new()
                {
                    OrganizationId = org.Id,
                    ActorUserId = admin.Id,
                    ActorName = admin.FullName,
                    Action = "CategoryRuleAdded",
                    Details = "Added app category rule: 'steam' → Distracting (priority 5).",
                    Timestamp = DateTime.UtcNow.AddDays(-45)
                },
                new()
                {
                    OrganizationId = org.Id,
                    ActorUserId = admin.Id,
                    ActorName = admin.FullName,
                    Action = "UserDeactivated",
                    Details = "Deactivated user 'intern01' — contract ended.",
                    Timestamp = DateTime.UtcNow.AddDays(-30)
                },
                new()
                {
                    OrganizationId = org.Id,
                    ActorUserId = admin.Id,
                    ActorName = admin.FullName,
                    Action = "PasswordReset",
                    Details = $"Reset password for user '{employees[2].Username}'.",
                    Timestamp = DateTime.UtcNow.AddDays(-14)
                },
                new()
                {
                    OrganizationId = org.Id,
                    ActorUserId = manager.Id,
                    ActorName = manager.FullName,
                    Action = "ReportGenerated",
                    Details = $"Generated weekly report for {employees[0].FullName} (period: last 7 days). Exported as PDF.",
                    Timestamp = DateTime.UtcNow.AddDays(-7)
                },
                new()
                {
                    OrganizationId = org.Id,
                    ActorUserId = admin.Id,
                    ActorName = admin.FullName,
                    Action = "SettingsChanged",
                    Details = "Updated alert thresholds: long idle changed from 30 to 20 minutes, blocked apps updated to include 'epicgames,origin'.",
                    Timestamp = DateTime.UtcNow.AddDays(-3)
                },
                new()
                {
                    OrganizationId = org.Id,
                    ActorUserId = admin.Id,
                    ActorName = admin.FullName,
                    Action = "AlertsDismissed",
                    Details = "Marked 12 alerts as read for the Engineering department.",
                    Timestamp = DateTime.UtcNow.AddDays(-1)
                },
                new()
                {
                    OrganizationId = org.Id,
                    ActorUserId = manager.Id,
                    ActorName = manager.FullName,
                    Action = "SessionForceClose",
                    Details = $"Force-closed stale session for user '{employees[3].Username}' on machine 'WORKSTATION-07'.",
                    Timestamp = DateTime.UtcNow.AddHours(-6)
                },
            };

            _db.AuditLogs.InsertMany(logs);
        }

        // ── Project Embeddings (RAG) ─────────────────────────────────────────────
        private void CreateProjectEmbeddings(Project projAlpha, Project projMobile)
        {
            var embeddings = new List<ProjectEmbedding>();

            // Alpha project embeddings
            var alphaChunks = new[]
            {
                (Type: "prd", Path: "PRD.md",
                 Content: "Build a comprehensive employee monitoring system with real-time activity tracking, AI-powered analytics, sprint planning, and code review automation. Must support multi-tenancy, role-based access control, and GDPR compliance."),
                (Type: "architecture", Path: "ARCHITECTURE.md",
                 Content: "WPF .NET 8 desktop client with MongoDB backend. Services layer follows repository pattern. MVVM for UI. MongoDB GridFS for screenshots. AI providers: OpenAI / Claude / Gemini."),
                (Type: "prd", Path: "PRD.md#features",
                 Content: "Core features: Active vs idle time tracking, foreground application logging, periodic screenshot capture, daily/weekly/monthly reports, productivity categorization, multi-tenant login, role-based access."),
                (Type: "architecture", Path: "ARCHITECTURE.md#data",
                 Content: "MongoDB collections: organizations, users, activity_logs, app_usages, screenshot_records, alerts, app_category_rules, system_settings, audit_logs, projects, tasks, sprints, code_reviews, qa_results, standup_logs."),
                (Type: "prd", Path: "PRD.md#security",
                 Content: "Security requirements: PBKDF2 password hashing with 100K iterations, brute-force lockout after 5 attempts, DPAPI encrypted config, session tokens, privacy blur on sensitive screenshots."),
                (Type: "code", Path: "src/EAMAS.Core/Services/ActivityMonitorService.cs",
                 Content: "Activity monitor records foreground window changes every 5 seconds. Categorizes apps as Productive/Neutral/Distracting. Updates AppUsage aggregates per-day."),
                (Type: "code", Path: "src/EAMAS.Desktop/Services/MonitoringBackgroundService.cs",
                 Content: "Background service runs 3 loops: activity polling, screenshot capture with jitter, and periodic purge. Handles idle detection and screen lock events."),
                (Type: "task", Path: "tasks/sprint-2",
                 Content: "Sprint 2 tasks: AI standup generation, sprint velocity tracking, idle detection race fix, GDPR data export, screenshot privacy blur, MongoDB cold start fix."),
            };

            foreach (var (type, path, content) in alphaChunks)
            {
                embeddings.Add(new ProjectEmbedding
                {
                    ProjectId = projAlpha.Id,
                    ChunkType = type,
                    SourcePath = path,
                    Content = content,
                    Embedding = GenerateFakeEmbedding(256),
                    IndexedAt = DateTime.UtcNow.AddDays(-Rng.Next(1, 30)),
                    CommitSha = GenerateSha(7)
                });
            }

            // Mobile project embeddings
            var mobileChunks = new[]
            {
                (Type: "prd", Path: "PRD.md",
                 Content: "React Native mobile app that surfaces EAMAS analytics for managers. Features: dashboard, team productivity charts, alert notifications, and sprint overview."),
                (Type: "architecture", Path: "ARCHITECTURE.md",
                 Content: "React Native with TypeScript. REST API backend (ASP.NET Core). Push notifications via Firebase. Charts with Victory Native."),
                (Type: "prd", Path: "PRD.md#screens",
                 Content: "Mobile screens: Login, Dashboard (team score, active users, alerts), Employee Detail, Sprint Board, Settings. Pull-to-refresh, offline caching."),
                (Type: "code", Path: "src/screens/DashboardScreen.tsx",
                 Content: "Dashboard renders team productivity score gauge, hourly activity bar chart, top 5 apps list, and unread alert count badge. Data refreshed every 60s."),
            };

            foreach (var (type, path, content) in mobileChunks)
            {
                embeddings.Add(new ProjectEmbedding
                {
                    ProjectId = projMobile.Id,
                    ChunkType = type,
                    SourcePath = path,
                    Content = content,
                    Embedding = GenerateFakeEmbedding(256),
                    IndexedAt = DateTime.UtcNow.AddDays(-Rng.Next(1, 20)),
                    CommitSha = GenerateSha(7)
                });
            }

            _db.ProjectEmbeddings.InsertMany(embeddings);
        }

        // ── Helpers ──────────────────────────────────────────────────────────────
        private static string GenerateSha(int length)
        {
            const string chars = "abcdef0123456789";
            return new string(Enumerable.Range(0, length).Select(_ => chars[Rng.Next(chars.Length)]).ToArray());
        }

        /// <summary>
        /// Generates a minimal valid JPEG byte array as placeholder thumbnail data.
        /// Real thumbnails (240×135) are created by the screenshot capture pipeline.
        /// </summary>
        private static byte[] GenerateFakeThumbnailBytes()
        {
            return new byte[]
            {
                0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01,
                0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0xFF, 0xDB, 0x00, 0x43,
                0x00, 0x08, 0x06, 0x06, 0x07, 0x06, 0x05, 0x08, 0x07, 0x07, 0x07, 0x09,
                0x09, 0x08, 0x0A, 0x0C, 0x14, 0x0D, 0x0C, 0x0B, 0x0B, 0x0C, 0x19, 0x12,
                0x13, 0x0F, 0x14, 0x1D, 0x1A, 0x1F, 0x1E, 0x1D, 0x1A, 0x1C, 0x1C, 0x20,
                0x24, 0x2E, 0x27, 0x20, 0x22, 0x2C, 0x23, 0x1C, 0x1C, 0x28, 0x37, 0x29,
                0x2C, 0x30, 0x31, 0x34, 0x34, 0x34, 0x1F, 0x27, 0x39, 0x3D, 0x38, 0x32,
                0x3C, 0x2E, 0x33, 0x34, 0x32, 0xFF, 0xD9
            };
        }

        /// <summary>Generates a fake unit-vector embedding for RAG seed data.</summary>
        private static float[] GenerateFakeEmbedding(int dimensions)
        {
            var vec = new float[dimensions];
            for (int i = 0; i < dimensions; i++)
                vec[i] = (float)(Rng.NextDouble() * 2 - 1);

            // Normalize to unit vector (cosine similarity expects this)
            var magnitude = (float)Math.Sqrt(vec.Sum(v => v * v));
            if (magnitude > 0)
                for (int i = 0; i < dimensions; i++)
                    vec[i] /= magnitude;

            return vec;
        }
    }
}
