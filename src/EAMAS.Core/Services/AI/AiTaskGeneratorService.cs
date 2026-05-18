using EAMAS.Core.Enums;
using EAMAS.Core.Models;
using System.Text.Json;

namespace EAMAS.Core.Services.AI
{
    public class AiTaskGeneratorService
    {
        private readonly RagService _rag;

        public AiTaskGeneratorService(RagService rag) => _rag = rag;

        /// <summary>
        /// Generates tasks from the project PRD/requirements.
        /// Returns unsaved tasks — Manager reviews and saves them.
        /// </summary>
        public async Task<List<ProjectTask>> GenerateTasksAsync(
            IAiProvider provider, Project project,
            string? featureText = null, List<string>? existingTaskTitles = null)
        {
            var contextChunks = await _rag.SearchAsync(project.Id, provider,
                featureText ?? "project requirements tasks features", topK: 5).ConfigureAwait(false);

            var context = _rag.BuildContext(contextChunks);
            var existing = existingTaskTitles?.Any() == true
                ? $"\n\nExisting tasks (avoid duplicating):\n- {string.Join("\n- ", existingTaskTitles)}"
                : string.Empty;

            var systemPrompt = $"""
                You are a senior engineering manager breaking down a PRD into developer tasks.
                Project: {project.Name}
                Tech Stack: {project.TechStack}
                {(contextChunks.Any() ? $"Relevant project context:\n{context}" : string.Empty)}
                {existing}

                Output a JSON array of tasks. Each task must have:
                - title: string (concise, action-verb first)
                - description: string (2-4 sentences, what and why)
                - acceptanceCriteria: string (bullet list as plain text)
                - estimatedHours: number (realistic, 1-16)
                - priority: "Low" | "Medium" | "High" | "Critical"
                - labels: string[] (1-3 labels like "backend", "frontend", "auth", "api", "bug", "ui")
                - subTasks: string[] (2-5 implementation steps)

                Output ONLY valid JSON array, no markdown, no explanation.
                """;

            var userPrompt = featureText != null
                ? $"Break down this feature into implementable developer tasks:\n\n{featureText}"
                : $"Break down the entire project requirements into developer tasks:\n\n{project.PrdContent}";

            var raw = await provider.CompleteAsync(systemPrompt, userPrompt, maxTokens: 6000).ConfigureAwait(false);
            return ParseTasks(raw, project);
        }

        private static List<ProjectTask> ParseTasks(string rawJson, Project project)
        {
            try
            {
                // Strip markdown code fences if present
                var json = rawJson.Trim();
                if (json.StartsWith("```")) json = json[(json.IndexOf('\n') + 1)..];
                if (json.EndsWith("```")) json = json[..json.LastIndexOf("```")];
                json = json.Trim();

                var arr = JsonSerializer.Deserialize<JsonElement[]>(json);
                if (arr == null) return new List<ProjectTask>();

                return arr.Select((el, idx) =>
                {
                    var priority = el.TryGetProperty("priority", out var p)
                        ? Enum.TryParse<TaskPriority>(p.GetString(), out var pr) ? pr : TaskPriority.Medium
                        : TaskPriority.Medium;

                    return new ProjectTask
                    {
                        OrganizationId = project.OrganizationId,
                        ProjectId = project.Id,
                        Title = el.TryGetProperty("title", out var t) ? t.GetString() ?? "Task" : "Task",
                        Description = el.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "",
                        AcceptanceCriteria = el.TryGetProperty("acceptanceCriteria", out var ac) ? ac.GetString() ?? "" : "",
                        EstimatedHours = el.TryGetProperty("estimatedHours", out var h) ? h.GetDouble() : 4,
                        Priority = priority,
                        Labels = el.TryGetProperty("labels", out var lb)
                            ? lb.EnumerateArray().Select(x => x.GetString() ?? "").Where(s => s.Length > 0).ToList()
                            : new List<string>(),
                        SubTasks = el.TryGetProperty("subTasks", out var st)
                            ? st.EnumerateArray().Select(x => x.GetString() ?? "").Where(s => s.Length > 0).ToList()
                            : new List<string>(),
                        Status = ProjectTaskStatus.Backlog,
                        BoardPosition = idx,
                        IsAiGenerated = true
                    };
                }).ToList();
            }
            catch
            {
                return new List<ProjectTask>();
            }
        }
    }
}
