using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EAMAS.Core.Models
{
    public class AuditLog
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        public string OrganizationId { get; set; } = string.Empty;

        [BsonRepresentation(BsonType.ObjectId)]
        public string ActorUserId { get; set; } = string.Empty;

        public string ActorName { get; set; } = string.Empty;

        /// <summary>Short verb describing the action, e.g. "UserCreated", "UserDeactivated", "SettingsChanged".</summary>
        public string Action { get; set; } = string.Empty;

        /// <summary>Human-readable summary, e.g. "Created employee John Doe (john.doe)".</summary>
        public string Details { get; set; } = string.Empty;

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
