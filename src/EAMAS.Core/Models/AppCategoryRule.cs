using EAMAS.Core.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EAMAS.Core.Models
{
    public class AppCategoryRule
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        /// <summary>Org-scoped rule. Empty/null means built-in (not used for custom rules).</summary>
        public string OrganizationId { get; set; } = string.Empty;

        public string Keyword { get; set; } = string.Empty;
        public ActivityCategory Category { get; set; }
        public bool IsActive { get; set; } = true;
        public bool MatchProcessName { get; set; } = true;
        public bool MatchWindowTitle { get; set; } = true;
        public int Priority { get; set; } = 0;
    }
}
