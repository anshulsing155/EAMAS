using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EAMAS.Core.Models
{
    public class SystemSettings
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        /// <summary>One settings document per organisation.</summary>
        public string OrganizationId { get; set; } = string.Empty;

        public bool MonitoringEnabled { get; set; } = true;
        public bool ScreenshotsEnabled { get; set; } = true;
        public int ScreenshotIntervalMinutes { get; set; } = 5;
        public int IdleThresholdSeconds { get; set; } = 300;
        public int ActivityPollIntervalSeconds { get; set; } = 5;
        public int MaxScreenshotAgeDays { get; set; } = 30;
        public int JpegQuality { get; set; } = 70;
        public bool AlertOnLongIdle { get; set; } = true;
        public int LongIdleThresholdMinutes { get; set; } = 30;
        public bool AlertOnDistractingUsage { get; set; } = true;
        public int DistractingUsageThresholdMinutes { get; set; } = 60;
        public string ScreenshotsDirectory { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
