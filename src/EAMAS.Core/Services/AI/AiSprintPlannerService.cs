using EAMAS.Core.Enums;
using EAMAS.Core.Models;
using System.Text.Json;

namespace EAMAS.Core.Services.AI
{
    public class AiSprintPlannerService
    {
        private readonly TaskService _tasks;
        private readonly SprintService _sprints;

        public AiSprintPlannerService(TaskService tasks, SprintService sprints)
        {
            _tasks = tasks;
            _sprints = sprints;
        }

        /// <summary>
        /// AI selects tasks from backlog that fit sprint capacity and suggests a sprint goal.
        /// Returns a proposed Sprint + selected task IDs for Manager review.
        /// </summary>
        public async Task<(Sprint proposed, List<ProjectTask> selectedTasks)> PlanSprintAsync(
            IAiProvider provider, Project project, List<User> teamMembers)
        {
            var backlog = _tasks.GetBacklog(project.Id);
            if (!backlog.Any()) return (BuildEmptySprint(project), new List<ProjectTask>());

            double avgVelocity = _sprints.GetAverageVelocity(project.Id);
            double capacity = avgVelocity > 0
                ? avgVelocity
                : teamMembers.Count * project.WorkHoursPerDay * project.SprintDurationDays * 0.6; // 60% utilization default

            var backlogSummary = string.Join("\n", backlog.Take(30).Select(t =>
                $"- [{t.Id}] [{t.Priority}] {t.Title} (~{t.EstimatedHours}h) Labels: {string.Join(",", t.Labels)}"));

            var systemPrompt = $$"""
                You are an Agile Scrum Master planning a {{project.SprintDurationDays}}-day sprint.
                Team size: {{teamMembers.Count}} developers.
                Sprint capacity: ~{{capacity:F0}} hours total.
                Previous velocity: {{avgVelocity:F0}} hours/sprint.

                Select tasks from the backlog that fit the sprint capacity (total estimated hours ≤ capacity).
                Prioritize Critical > High > Medium > Low.
                Group related tasks for a coherent sprint goal.

                Output JSON:
                {
                  "sprintGoal": "<one clear sentence describing what will be achieved>",
                  "selectedTaskIds": ["<id1>", "<id2>", ...]
                }

                Output ONLY valid JSON, no explanation.
                """;

            var userPrompt = $"Backlog tasks:\n{backlogSummary}\n\nSelect tasks for the sprint.";

            var selectedTaskIds = new List<string>();
            var goal = $"Sprint {DateTime.UtcNow:yyyy-MM-dd}";

            try
            {
                var raw = await provider.CompleteAsync(systemPrompt, userPrompt, maxTokens: 1000).ConfigureAwait(false);
                var json = raw.Trim();
                if (json.StartsWith("```")) json = json[(json.IndexOf('\n') + 1)..];
                if (json.EndsWith("```")) json = json[..json.LastIndexOf("```")];

                var el = JsonSerializer.Deserialize<JsonElement>(json.Trim());
                if (el.TryGetProperty("sprintGoal", out var g)) goal = g.GetString() ?? goal;
                if (el.TryGetProperty("selectedTaskIds", out var ids))
                    selectedTaskIds = ids.EnumerateArray().Select(x => x.GetString() ?? "").Where(s => s.Length > 0).ToList();
            }
            catch { /* fallback: select top tasks by priority up to capacity */ }

            // Fallback: if AI failed or returned no IDs, select greedily by priority
            if (!selectedTaskIds.Any())
            {
                double remaining = capacity;
                foreach (var t in backlog.OrderByDescending(t => (int)t.Priority))
                {
                    if (remaining <= 0) break;
                    selectedTaskIds.Add(t.Id);
                    remaining -= t.EstimatedHours;
                }
            }

            var selected = backlog.Where(t => selectedTaskIds.Contains(t.Id)).ToList();
            int sprintNumber = _sprints.GetByProject(project.Id).Count + 1;

            var sprint = new Sprint
            {
                OrganizationId = project.OrganizationId,
                ProjectId = project.Id,
                Name = $"Sprint {sprintNumber}",
                Goal = goal,
                StartDate = DateTime.UtcNow.Date,
                EndDate = DateTime.UtcNow.Date.AddDays(project.SprintDurationDays),
                Status = SprintStatus.Planning,
                TaskIds = selected.Select(t => t.Id).ToList(),
                PlannedVelocity = selected.Sum(t => t.EstimatedHours)
            };

            return (sprint, selected);
        }

        public async Task<string> GenerateRetrospectiveAsync(IAiProvider provider, Sprint sprint, List<ProjectTask> sprintTasks)
        {
            var done = sprintTasks.Where(t => t.Status == ProjectTaskStatus.Done).ToList();
            var incomplete = sprintTasks.Where(t => t.Status != ProjectTaskStatus.Done).ToList();

            var systemPrompt = "You are an Agile coach writing a sprint retrospective. Be constructive, specific, and brief (3-4 paragraphs).";
            var userPrompt = $"""
                Sprint: {sprint.Name}
                Goal: {sprint.Goal}
                Duration: {sprint.StartDate:yyyy-MM-dd} to {sprint.EndDate:yyyy-MM-dd}
                Planned: {sprint.PlannedVelocity:F0}h | Completed: {sprint.ActualVelocity:F0}h

                Completed ({done.Count}):
                {string.Join("\n", done.Take(10).Select(t => $"- {t.Title}"))}

                Not completed ({incomplete.Count}):
                {string.Join("\n", incomplete.Take(5).Select(t => $"- {t.Title}"))}

                Write a retrospective covering: what went well, what to improve, action items.
                """;

            try { return await provider.CompleteAsync(systemPrompt, userPrompt, maxTokens: 800).ConfigureAwait(false); }
            catch { return "Sprint retrospective could not be generated."; }
        }

        private static Sprint BuildEmptySprint(Project project) => new()
        {
            OrganizationId = project.OrganizationId,
            ProjectId = project.Id,
            Name = "Sprint 1",
            Goal = "No backlog tasks available.",
            StartDate = DateTime.UtcNow.Date,
            EndDate = DateTime.UtcNow.Date.AddDays(project.SprintDurationDays),
            Status = SprintStatus.Planning
        };
    }
}
