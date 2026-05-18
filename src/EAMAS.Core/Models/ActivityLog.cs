using EAMAS.Core.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EAMAS.Core.Models
{
    [BsonIgnoreExtraElements]
    public class ActivityLog
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        public string OrganizationId { get; set; } = string.Empty;

        [BsonRepresentation(BsonType.ObjectId)]
        public string UserId { get; set; } = string.Empty;

        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string ApplicationName { get; set; } = string.Empty;
        public string ProcessName { get; set; } = string.Empty;
        public string WindowTitle { get; set; } = string.Empty;
        public ActivityCategory Category { get; set; }
        public bool IsIdle { get; set; }
        public bool IsScreenLocked { get; set; }

        /// <summary>True when the session duration was clamped by the time-integrity check.</summary>
        public bool WasClockAdjusted { get; set; }

        /// <summary>The original EndTime before clock-integrity clamping. Null if no adjustment was made.</summary>
        public DateTime? OriginalEndTime { get; set; }

        [BsonIgnore]
        public TimeSpan Duration => EndTime > StartTime ? EndTime - StartTime : TimeSpan.Zero;
    }
}
