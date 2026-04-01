using EAMAS.Core.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EAMAS.Core.Models
{
    public class AppUsage
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        public string OrganizationId { get; set; } = string.Empty;

        [BsonRepresentation(BsonType.ObjectId)]
        public string UserId { get; set; } = string.Empty;

        public string ApplicationName { get; set; } = string.Empty;
        public string ProcessName { get; set; } = string.Empty;

        /// <summary>Stored as total ticks (long) in MongoDB since TimeSpan is not natively supported.</summary>
        [BsonIgnore]
        public TimeSpan Duration
        {
            get => TimeSpan.FromTicks(DurationTicks);
            set => DurationTicks = value.Ticks;
        }

        [BsonElement("DurationTicks")]
        public long DurationTicks { get; set; }

        public DateTime RecordedAt { get; set; }
        public ActivityCategory Category { get; set; }
    }
}
