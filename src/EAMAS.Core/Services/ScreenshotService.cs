using EAMAS.Core.Data;
using EAMAS.Core.Models;
using MongoDB.Driver;

namespace EAMAS.Core.Services
{
    public class ScreenshotService
    {
        private readonly MongoDbContext _db;
        private readonly SettingsService _settingsService;

        public ScreenshotService(MongoDbContext db, SettingsService settingsService)
        {
            _db = db;
            _settingsService = settingsService;
        }

        public void SaveRecord(ScreenshotRecord record)
        {
            _db.ScreenshotRecords.InsertOne(record);
        }

        public List<ScreenshotRecord> GetScreenshots(string orgId, string userId,
            DateTime from, DateTime to)
        {
            return _db.ScreenshotRecords
                .Find(x => x.OrganizationId == orgId &&
                           x.UserId == userId &&
                           x.TakenAt >= from &&
                           x.TakenAt < to)
                .SortByDescending(x => x.TakenAt)
                .ToList();
        }

        public List<ScreenshotRecord> GetTodayScreenshots(string orgId, string userId)
        {
            var today = DateTime.Today;
            return GetScreenshots(orgId, userId, today, today.AddDays(1));
        }

        public void Delete(string screenshotId)
        {
            var record = _db.ScreenshotRecords
                .Find(x => x.Id == screenshotId)
                .FirstOrDefault();
            if (record == null) return;

            if (File.Exists(record.FilePath)) File.Delete(record.FilePath);
            if (File.Exists(record.ThumbnailPath)) File.Delete(record.ThumbnailPath);
            _db.ScreenshotRecords.DeleteOne(x => x.Id == screenshotId);
        }

        public void PurgeOldScreenshots(string orgId)
        {
            var settings = _settingsService.GetSettings(orgId);
            var cutoff = DateTime.UtcNow.AddDays(-settings.MaxScreenshotAgeDays);

            var old = _db.ScreenshotRecords
                .Find(x => x.OrganizationId == orgId && x.TakenAt < cutoff)
                .ToList();

            foreach (var r in old)
            {
                if (File.Exists(r.FilePath)) File.Delete(r.FilePath);
                if (File.Exists(r.ThumbnailPath)) File.Delete(r.ThumbnailPath);
            }

            _db.ScreenshotRecords.DeleteMany(
                x => x.OrganizationId == orgId && x.TakenAt < cutoff);
        }

        public long GetTotalStorageBytes(string orgId, string? userId = null)
        {
            var filter = Builders<ScreenshotRecord>.Filter.Eq(x => x.OrganizationId, orgId);
            if (userId != null)
                filter &= Builders<ScreenshotRecord>.Filter.Eq(x => x.UserId, userId);

            var records = _db.ScreenshotRecords.Find(filter).ToList();
            return records.Sum(r => r.FileSizeBytes);
        }
    }
}
