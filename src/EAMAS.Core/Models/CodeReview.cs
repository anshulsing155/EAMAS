using EAMAS.Core.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EAMAS.Core.Models
{
    [BsonIgnoreExtraElements]
    public class CodeReview
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        public string OrganizationId { get; set; } = string.Empty;
        public string ProjectId { get; set; } = string.Empty;
        public string TaskId { get; set; } = string.Empty;
        public string AssignedUserId { get; set; } = string.Empty;

        // Commit Info
        public string CommitSha { get; set; } = string.Empty;
        public string CommitMessage { get; set; } = string.Empty;
        public string CommitAuthor { get; set; } = string.Empty;
        public string Branch { get; set; } = string.Empty;
        public List<ChangedFile> ChangedFiles { get; set; } = new();

        // AI Review
        public CodeReviewStatus Status { get; set; } = CodeReviewStatus.Pending;
        public int OverallScore { get; set; }
        public string AiSummary { get; set; } = string.Empty;
        public List<CodeIssue> Issues { get; set; } = new();
        public List<string> Suggestions { get; set; } = new();
        public bool RequiresHumanApproval { get; set; } = true;

        // QA
        public QaRunStatus QaStatus { get; set; } = QaRunStatus.Queued;
        public string QaLog { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string AiProviderUsed { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
    }

    public class ChangedFile
    {
        public string FilePath { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;   // added | modified | deleted
        public int Additions { get; set; }
        public int Deletions { get; set; }
        public string Diff { get; set; } = string.Empty;
    }

    public class CodeIssue
    {
        public string Severity { get; set; } = "info";   // error | warning | info
        public string FilePath { get; set; } = string.Empty;
        public int LineNumber { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string SuggestedFix { get; set; } = string.Empty;
    }
}
