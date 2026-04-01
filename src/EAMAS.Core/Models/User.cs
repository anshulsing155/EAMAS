using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EAMAS.Core.Models
{
    /// <summary>
    /// SuperAdmin — system-level, OrganizationId = "SYSTEM", login with org code "SYSTEM".
    /// Admin      — organisation admin, manages users within their org.
    /// Manager    — views employee reports, manages team.
    /// Employee   — own activity monitoring only.
    /// </summary>
    public enum UserRole { SuperAdmin, Admin, Manager, Employee }

    public class User
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        /// <summary>"SYSTEM" for SuperAdmin, otherwise the owning org's ObjectId.</summary>
        public string OrganizationId { get; set; } = string.Empty;

        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public UserRole Role { get; set; }
        public bool IsActive { get; set; } = true;
        public bool ConsentGiven { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLogin { get; set; }
    }
}
