using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EAMAS.Core.Models
{
    [BsonIgnoreExtraElements]
    public class ProjectEmbedding
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        public string ProjectId { get; set; } = string.Empty;
        public string ChunkType { get; set; } = string.Empty;   // prd | architecture | code | task
        public string SourcePath { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public float[] Embedding { get; set; } = Array.Empty<float>();
        public DateTime IndexedAt { get; set; } = DateTime.UtcNow;
        public string CommitSha { get; set; } = string.Empty;
    }
}
