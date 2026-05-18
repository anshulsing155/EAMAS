using EAMAS.Core.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EAMAS.Core.Models
{
    [BsonIgnoreExtraElements]
    public class QaResult
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        public string OrganizationId { get; set; } = string.Empty;
        public string ProjectId { get; set; } = string.Empty;
        public string TaskId { get; set; } = string.Empty;
        public string CommitSha { get; set; } = string.Empty;

        public QaRunStatus Status { get; set; } = QaRunStatus.Queued;
        public List<QaCheck> Checks { get; set; } = new();
        public string AiQaSummary { get; set; } = string.Empty;
        public bool FeatureMatchesTask { get; set; }
        public string FeatureMatchReason { get; set; } = string.Empty;

        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
    }

    public class QaCheck
    {
        public string Name { get; set; } = string.Empty;
        public bool Passed { get; set; }
        public string Output { get; set; } = string.Empty;
        public int ExitCode { get; set; }
        public long DurationMs { get; set; }
    }
}
