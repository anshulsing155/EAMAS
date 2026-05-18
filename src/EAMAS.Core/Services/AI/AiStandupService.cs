using EAMAS.Core.Data;
using EAMAS.Core.Models;
using MongoDB.Driver;

namespace EAMAS.Core.Services.AI
{
    public class AiStandupService
    {
        private readonly MongoDbContext _db;
        private readonly TaskService _tasks;

        public AiStandupService(MongoDbContext db, TaskService tasks)
        {
            _db = db;
            _tasks = tasks;
        }

        public async Task<StandupLog> GenerateStandupAsync(IAiProvider provider, Project project, User user)
        {
            var today = DateTime.UtcNow.Date;

            // Check if already generated today
            var existing = _db.StandupLogs.Find(s =>
                s.ProjectId == project.Id && s.UserId == user.Id && s.Date == today).FirstOrDefault();
            if (existing != null) return existing;

            var completedYesterday = _tasks.GetCompletedYesterday(project.Id, user.Id);
            var inProgressToday = _tasks.GetInProgress(project.Id, user.Id);
            var reviewsYesterday = _db.CodeReviews.Find(r =>
                r.ProjectId == project.Id && r.AssignedUserId == user.Id
                && r.CreatedAt >= today.AddDays(-1) && r.CreatedAt < today).CountDocuments();

            var needsFix = _tasks.GetByStatus(project.Id, Enums.ProjectTaskStatus.NeedsFix)
                .Where(t => t.AssignedToUserId == user.Id).ToList();

            var systemPrompt = $"""
                You are generating a daily standup update for {user.FullName} ({user.Role}).
                Project: {project.Name}
                Keep it professional, concise, and under 120 words total.
                Format as three sections: Yesterday, Today, Blockers.
                Write in first person from the developer's perspective.
                """;

            var userPrompt = $"""
                Yesterday completed ({completedYesterday.Count} tasks):
                {(completedYesterday.Any() ? string.Join("\n", completedYesterday.Select(t => $"- {t.Title}")) : "- No tasks completed")}
                Commits pushed: {reviewsYesterday}

                Today working on ({inProgressToday.Count} tasks):
                {(inProgressToday.Any() ? string.Join("\n", inProgressToday.Select(t => $"- {t.Title}")) : "- Planning new tasks")}

                Needs fix ({needsFix.Count} items):
                {(needsFix.Any() ? string.Join("\n", needsFix.Take(3).Select(t => $"- {t.Title}")) : "None")}

                Generate the standup message.
                """;

            string aiMessage;
            try { aiMessage = await provider.CompleteAsync(systemPrompt, userPrompt, maxTokens: 300).ConfigureAwait(false); }
            catch { aiMessage = BuildFallbackStandup(completedYesterday, inProgressToday, needsFix, user.FullName); }

            var log = new StandupLog
            {
                OrganizationId = project.OrganizationId,
                ProjectId = project.Id,
                UserId = user.Id,
                UserName = user.FullName,
                Date = today,
                AiGeneratedMessage = aiMessage,
                YesterdayAccomplished = completedYesterday.Any()
                    ? string.Join(", ", completedYesterday.Select(t => t.Title)) : "No tasks completed",
                TodayFocus = inProgressToday.Any()
                    ? string.Join(", ", inProgressToday.Select(t => t.Title)) : "Planning",
                Blockers = needsFix.Any()
                    ? string.Join(", ", needsFix.Take(3).Select(t => t.Title)) : "None",
                TasksCompletedYesterday = completedYesterday.Select(t => t.Id).ToList(),
                TasksInProgressToday = inProgressToday.Select(t => t.Id).ToList(),
                CommitsYesterday = (int)reviewsYesterday,
                GeneratedAt = DateTime.UtcNow
            };

            _db.StandupLogs.InsertOne(log);
            return log;
        }

        public List<StandupLog> GetForProject(string projectId, DateTime date)
            => _db.StandupLogs.Find(s => s.ProjectId == projectId && s.Date == date.Date).ToList();

        public List<StandupLog> GetForUser(string userId, int lastNDays = 7)
        {
            var since = DateTime.UtcNow.Date.AddDays(-lastNDays);
            return _db.StandupLogs.Find(s => s.UserId == userId && s.Date >= since)
                                  .SortByDescending(s => s.Date).ToList();
        }

        private static string BuildFallbackStandup(
            List<ProjectTask> done, List<ProjectTask> todo, List<ProjectTask> fix, string name)
        {
            return $"""
                Yesterday: {(done.Any() ? $"Completed {string.Join(", ", done.Take(3).Select(t => t.Title))}." : "No tasks completed.")}
                Today: {(todo.Any() ? $"Working on {string.Join(", ", todo.Take(3).Select(t => t.Title))}." : "Planning upcoming tasks.")}
                Blockers: {(fix.Any() ? $"Code review issues on {string.Join(", ", fix.Take(2).Select(t => t.Title))}." : "None.")}
                """;
        }
    }
}
