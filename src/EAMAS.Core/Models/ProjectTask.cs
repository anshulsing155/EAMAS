using EAMAS.Core.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EAMAS.Core.Models
{
    [BsonIgnoreExtraElements]
    public class ProjectTask
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        public string OrganizationId { get; set; } = string.Empty;
        public string ProjectId { get; set; } = string.Empty;
        public string SprintId { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string AcceptanceCriteria { get; set; } = string.Empty;

        // Assignment
        public string AssignedToUserId { get; set; } = string.Empty;
        public string AssignedToUserName { get; set; } = string.Empty;
        public string CreatedByUserId { get; set; } = string.Empty;

        // Kanban
        public ProjectTaskStatus Status { get; set; } = ProjectTaskStatus.Backlog;
        public int BoardPosition { get; set; }
        public TaskPriority Priority { get; set; } = TaskPriority.Medium;
        public List<string> Labels { get; set; } = new();

        // Estimation
        public double EstimatedHours { get; set; }
        public double ActualHours { get; set; }
        public DateTime? DueDate { get; set; }

        // Code Linkage
        public string RelatedCommitSha { get; set; } = string.Empty;
        public string GitHubPrUrl { get; set; } = string.Empty;

        // AI Fields
        public string AiGeneratedSummary { get; set; } = string.Empty;
        public List<string> SubTasks { get; set; } = new();
        public bool IsAiGenerated { get; set; }

        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
}
