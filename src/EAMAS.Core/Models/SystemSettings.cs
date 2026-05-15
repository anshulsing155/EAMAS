using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EAMAS.Core.Models
{
    [BsonIgnoreExtraElements]
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
        public int MaxScreenshotAgeDays { get; set; } = 7;
        public int JpegQuality { get; set; } = 70;
        public bool AlertOnLongIdle { get; set; } = true;
        public int LongIdleThresholdMinutes { get; set; } = 30;
        public bool AlertOnDistractingUsage { get; set; } = true;
        public int DistractingUsageThresholdMinutes { get; set; } = 60;

        // ── Low-productivity alert ────────────────────────────────────────────────
        public bool AlertOnLowProductivity { get; set; } = true;
        /// <summary>Minimum productivity score (0–100) below which an alert fires.</summary>
        public int LowProductivityThresholdPercent { get; set; } = 30;
        /// <summary>Minimum active minutes in the day before the productivity check fires.</summary>
        public int LowProductivityMinActiveMinutes { get; set; } = 120;

        // ── Unauthorized-app alert ────────────────────────────────────────────────
        public bool AlertOnUnauthorizedApp { get; set; } = false;
        /// <summary>Comma-separated process names that are considered unauthorized.</summary>
        public string BlockedApplications { get; set; } = string.Empty;

        // ── No-activity alert ─────────────────────────────────────────────────────
        public bool AlertOnNoActivity { get; set; } = true;
        /// <summary>Minutes of continuous zero-activity during work hours before alert fires.</summary>
        public int NoActivityThresholdMinutes { get; set; } = 120;

        // ── Screenshot privacy blur ───────────────────────────────────────────────
        /// <summary>When true, screenshots with personally-sensitive content are pixelated before storage.</summary>
        public bool PrivacyBlurEnabled { get; set; } = true;

        // ── Data retention ────────────────────────────────────────────────────────
        /// <summary>Activity logs and AppUsage records older than this are purged. 0 = never purge.</summary>
        public int ActivityLogRetentionDays { get; set; } = 90;
        /// <summary>Alert records older than this are purged. 0 = never purge.</summary>
        public int AlertRetentionDays { get; set; } = 90;
        /// <summary>Audit log entries older than this are purged. 0 = never purge.</summary>
        public int AuditLogRetentionDays { get; set; } = 365;

        public string ScreenshotsDirectory { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
