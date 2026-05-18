using EAMAS.Core.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EAMAS.Core.Models
{
    [BsonIgnoreExtraElements]
    public class Sprint
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        public string OrganizationId { get; set; } = string.Empty;
        public string ProjectId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Goal { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public SprintStatus Status { get; set; } = SprintStatus.Planning;
        public List<string> TaskIds { get; set; } = new();
        public double PlannedVelocity { get; set; }
        public double ActualVelocity { get; set; }
        public string AiSprintSummary { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
