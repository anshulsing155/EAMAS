using EAMAS.Core.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EAMAS.Core.Models
{
    [BsonIgnoreExtraElements]
    public class Project
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        public string OrganizationId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        // GitHub
        public string GitHubRepoOwner { get; set; } = string.Empty;
        public string GitHubRepoName { get; set; } = string.Empty;
        public string GitHubAccessToken { get; set; } = string.Empty;   // Encrypted at rest
        public string DefaultBranch { get; set; } = "main";
        public string WebhookSecret { get; set; } = string.Empty;        // Encrypted

        // AI Configuration
        public AiProviderType AiProvider { get; set; } = AiProviderType.OpenAI;
        public string AiApiKey { get; set; } = string.Empty;             // Encrypted at rest
        public string AiModel { get; set; } = "gpt-4o";
        public double AiTemperature { get; set; } = 0.3;

        // Project Context (fed into RAG + prompts)
        public string PrdContent { get; set; } = string.Empty;
        public string ArchitectureNotes { get; set; } = string.Empty;
        public string TechStack { get; set; } = string.Empty;

        // Sprint Config
        public int SprintDurationDays { get; set; } = 14;
        public int WorkHoursPerDay { get; set; } = 8;

        // QA Commands (semicolon-separated, e.g. "dotnet build;dotnet test")
        public string QaCommands { get; set; } = string.Empty;

        // Status
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string CreatedByUserId { get; set; } = string.Empty;
        public DateTime? LastSyncedAt { get; set; }
        public string LastKnownCommitSha { get; set; } = string.Empty;
    }
}
