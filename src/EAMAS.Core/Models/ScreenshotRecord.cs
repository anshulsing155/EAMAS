using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EAMAS.Core.Models
{
    public class ScreenshotRecord
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        public string OrganizationId { get; set; } = string.Empty;

        [BsonRepresentation(BsonType.ObjectId)]
        public string UserId { get; set; } = string.Empty;

        public DateTime TakenAt { get; set; }

        /// <summary>GridFS ObjectId of the full-resolution JPEG. Null for legacy file-based records.</summary>
        public string? ImageGridFsId { get; set; }

        /// <summary>Thumbnail JPEG bytes stored inline (240x135). Null for legacy records.</summary>
        public byte[]? ThumbnailData { get; set; }

        /// <summary>Legacy file path – kept for backward-compatibility, not used in new records.</summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>Legacy thumbnail path – kept for backward-compatibility, not used in new records.</summary>
        public string ThumbnailPath { get; set; } = string.Empty;

        public string ApplicationName { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public bool IsSensitive { get; set; }
        public bool IsManual { get; set; }
    }
}
