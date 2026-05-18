using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EAMAS.Core.Models
{
    [BsonIgnoreExtraElements]
    public class StandupLog
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        public string OrganizationId { get; set; } = string.Empty;
        public string ProjectId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public DateTime Date { get; set; }

        public string YesterdayAccomplished { get; set; } = string.Empty;
        public string TodayFocus { get; set; } = string.Empty;
        public string Blockers { get; set; } = string.Empty;
        public string AiGeneratedMessage { get; set; } = string.Empty;

        public List<string> TasksCompletedYesterday { get; set; } = new();
        public List<string> TasksInProgressToday { get; set; } = new();
        public int CommitsYesterday { get; set; }

        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }
}
