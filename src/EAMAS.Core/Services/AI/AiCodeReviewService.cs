using EAMAS.Core.Data;
using EAMAS.Core.Enums;
using EAMAS.Core.Models;
using MongoDB.Driver;
using System.Text.Json;

namespace EAMAS.Core.Services.AI
{
    public class AiCodeReviewService
    {
        private readonly MongoDbContext _db;
        private readonly RagService _rag;
        private const int MaxDiffChars = 8000;

        public AiCodeReviewService(MongoDbContext db, RagService rag)
        {
            _db = db;
            _rag = rag;
        }

        public CodeReview? GetById(string id)
            => _db.CodeReviews.Find(r => r.Id == id).FirstOrDefault();

        public List<CodeReview> GetByProject(string projectId, int limit = 50)
            => _db.CodeReviews.Find(r => r.ProjectId == projectId)
                              .SortByDescending(r => r.CreatedAt)
                              .Limit(limit).ToList();

        public List<CodeReview> GetByTask(string taskId)
            => _db.CodeReviews.Find(r => r.TaskId == taskId)
                              .SortByDescending(r => r.CreatedAt).ToList();

        public async Task<CodeReview> ReviewCommitAsync(
            IAiProvider provider, Project project,
            string commitSha, string commitMessage, string commitAuthor, string branch,
            List<ChangedFile> changedFiles, string? relatedTaskTitle = null)
        {
            var review = new CodeReview
            {
                OrganizationId = project.OrganizationId,
                ProjectId = project.Id,
                CommitSha = commitSha,
                CommitMessage = commitMessage,
                CommitAuthor = commitAuthor,
                Branch = branch,
                ChangedFiles = changedFiles,
                Status = CodeReviewStatus.InProgress,
                AiProviderUsed = provider.ProviderType.ToString(),
                RequiresHumanApproval = true
            };

            _db.CodeReviews.InsertOne(review);

            try
            {
                // Retrieve relevant project context
                var query = $"code review {relatedTaskTitle ?? commitMessage}";
                var contextChunks = await _rag.SearchAsync(project.Id, provider, query, topK: 3).ConfigureAwait(false);
                var context = contextChunks.Any() ? _rag.BuildContext(contextChunks) : string.Empty;

                var systemPrompt = $$"""
                    You are a senior code reviewer. Respond in JSON only.
                    Project: {{project.Name}}
                    Tech Stack: {{project.TechStack}}
                    {{(context.Length > 0 ? $"Project context:\n{context}\n" : string.Empty)}}
                    {{(relatedTaskTitle != null ? $"Related task: {relatedTaskTitle}" : string.Empty)}}

                    Review the code changes and output JSON with this exact structure:
                    {
                      "overallScore": <0-100>,
                      "summary": "<1-2 sentences>",
                      "issues": [
                        {
                          "severity": "error|warning|info",
                          "filePath": "<path>",
                          "lineNumber": <int or 0>,
                          "category": "security|performance|naming|logic|architecture|style",
                          "description": "<what is wrong>",
                          "suggestedFix": "<how to fix>"
                        }
                      ],
                      "suggestions": ["<improvement 1>", "<improvement 2>"]
                    }

                    Check for: SQL/NoSQL injection, XSS, hardcoded secrets, missing validation,
                    naming violations, performance issues, logic errors, code duplication.
                    """;

                // Review each file (batch if multiple)
                var allIssues = new List<CodeIssue>();
                var allSuggestions = new List<string>();
                int totalScore = 0;
                int reviewedFiles = 0;
                string overallSummary = string.Empty;

                var textFiles = changedFiles
                    .Where(f => f.Status != "deleted" && !IsBinaryFile(f.FilePath) && !string.IsNullOrEmpty(f.Diff))
                    .Take(10) // max 10 files per review
                    .ToList();

                foreach (var file in textFiles)
                {
                    var truncatedDiff = TruncateDiff(file.Diff, MaxDiffChars);
                    var userPrompt = $"File: {file.FilePath} ({file.Additions}+ {file.Deletions}-)\n\nDiff:\n{truncatedDiff}";

                    try
                    {
                        var raw = await provider.CompleteAsync(systemPrompt, userPrompt, maxTokens: 2000).ConfigureAwait(false);
                        var result = ParseReviewResult(raw, file.FilePath);
                        totalScore += result.score;
                        allIssues.AddRange(result.issues);
                        allSuggestions.AddRange(result.suggestions);
                        if (string.IsNullOrEmpty(overallSummary)) overallSummary = result.summary;
                        reviewedFiles++;
                    }
                    catch { /* skip file on error */ }
                }

                review.OverallScore = reviewedFiles > 0 ? totalScore / reviewedFiles : 75;
                review.Issues = allIssues.Take(20).ToList();
                review.Suggestions = allSuggestions.Distinct().Take(10).ToList();
                review.AiSummary = overallSummary.Length > 0 ? overallSummary
                    : $"Reviewed {reviewedFiles} file(s). Score: {review.OverallScore}/100.";
                review.Status = allIssues.Any(i => i.Severity == "error")
                    ? CodeReviewStatus.Failed : CodeReviewStatus.Passed;

                _db.CodeReviews.ReplaceOne(r => r.Id == review.Id, review);
            }
            catch (Exception ex)
            {
                review.Status = CodeReviewStatus.Failed;
                review.ErrorMessage = ex.Message;
                review.AiSummary = "Review failed due to an error.";
                _db.CodeReviews.ReplaceOne(r => r.Id == review.Id, review);
            }

            return review;
        }

        public void UpdateQaStatus(string reviewId, QaRunStatus status, string log)
            => _db.CodeReviews.UpdateOne(r => r.Id == reviewId,
                Builders<CodeReview>.Update
                    .Set(r => r.QaStatus, status)
                    .Set(r => r.QaLog, log));

        private static (int score, string summary, List<CodeIssue> issues, List<string> suggestions)
            ParseReviewResult(string raw, string filePath)
        {
            try
            {
                var json = raw.Trim();
                if (json.StartsWith("```")) json = json[(json.IndexOf('\n') + 1)..];
                if (json.EndsWith("```")) json = json[..json.LastIndexOf("```")];
                json = json.Trim();

                var el = JsonSerializer.Deserialize<JsonElement>(json);
                int score = el.TryGetProperty("overallScore", out var s) ? s.GetInt32() : 75;
                string summary = el.TryGetProperty("summary", out var sm) ? sm.GetString() ?? "" : "";
                var suggestions = el.TryGetProperty("suggestions", out var sg)
                    ? sg.EnumerateArray().Select(x => x.GetString() ?? "").Where(s => s.Length > 0).ToList()
                    : new List<string>();
                var issues = new List<CodeIssue>();
                if (el.TryGetProperty("issues", out var iss))
                {
                    foreach (var i in iss.EnumerateArray())
                    {
                        issues.Add(new CodeIssue
                        {
                            Severity = i.TryGetProperty("severity", out var sv) ? sv.GetString() ?? "info" : "info",
                            FilePath = i.TryGetProperty("filePath", out var fp) ? fp.GetString() ?? filePath : filePath,
                            LineNumber = i.TryGetProperty("lineNumber", out var ln) ? ln.GetInt32() : 0,
                            Category = i.TryGetProperty("category", out var cat) ? cat.GetString() ?? "" : "",
                            Description = i.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : "",
                            SuggestedFix = i.TryGetProperty("suggestedFix", out var fix) ? fix.GetString() ?? "" : ""
                        });
                    }
                }
                return (score, summary, issues, suggestions);
            }
            catch
            {
                return (75, "Review completed.", new List<CodeIssue>(), new List<string>());
            }
        }

        private static string TruncateDiff(string diff, int maxChars)
            => diff.Length <= maxChars ? diff : diff[..maxChars] + "\n... [truncated]";

        private static bool IsBinaryFile(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".ico" or ".pdf"
                       or ".exe" or ".dll" or ".zip" or ".bin" or ".db";
        }
    }
}
