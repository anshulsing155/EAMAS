using EAMAS.Core.Data;
using EAMAS.Core.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;

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

        /// <summary>
        /// Saves a screenshot to MongoDB GridFS (full image) and the ScreenshotRecords collection
        /// (metadata + inline thumbnail). No files are written to local disk.
        /// </summary>
        public async Task SaveScreenshotAsync(byte[] fullImageData, byte[] thumbData,
            ScreenshotRecord record)
        {
            var filename =
                $"{record.OrganizationId}_{record.UserId}_{record.TakenAt:yyyyMMdd_HHmmss}.jpg";

            using var ms = new MemoryStream(fullImageData);
            var gridFsId = await _db.ScreenshotBucket
                .UploadFromStreamAsync(filename, ms)
                .ConfigureAwait(false);

            record.ImageGridFsId = gridFsId.ToString();
            record.ThumbnailData = thumbData;
            record.FileSizeBytes = fullImageData.Length;

            await _db.ScreenshotRecords.InsertOneAsync(record).ConfigureAwait(false);
        }

        /// <summary>Download the full-resolution screenshot bytes from GridFS.</summary>
        public async Task<byte[]?> DownloadImageAsync(string gridFsId)
        {
            try
            {
                var id = ObjectId.Parse(gridFsId);
                return await _db.ScreenshotBucket.DownloadAsBytesAsync(id).ConfigureAwait(false);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Legacy synchronous insert for records that already have a FilePath set.</summary>
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

        /// <summary>Get screenshots for all users in the org (Admin/Manager view).</summary>
        public List<ScreenshotRecord> GetScreenshots(string orgId, DateTime from, DateTime to)
        {
            return _db.ScreenshotRecords
                .Find(x => x.OrganizationId == orgId &&
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

            if (!string.IsNullOrEmpty(record.ImageGridFsId))
            {
                try
                {
                    var id = ObjectId.Parse(record.ImageGridFsId);
                    _db.ScreenshotBucket.Delete(id);
                }
                catch { /* GridFS file already gone */ }
            }

            // Clean up legacy file-based records if they exist on this machine
            if (!string.IsNullOrEmpty(record.FilePath) && File.Exists(record.FilePath))
                File.Delete(record.FilePath);
            if (!string.IsNullOrEmpty(record.ThumbnailPath) && File.Exists(record.ThumbnailPath))
                File.Delete(record.ThumbnailPath);

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
                if (!string.IsNullOrEmpty(r.ImageGridFsId))
                {
                    try
                    {
                        var id = ObjectId.Parse(r.ImageGridFsId);
                        _db.ScreenshotBucket.Delete(id);
                    }
                    catch { }
                }

                if (!string.IsNullOrEmpty(r.FilePath) && File.Exists(r.FilePath))
                    File.Delete(r.FilePath);
                if (!string.IsNullOrEmpty(r.ThumbnailPath) && File.Exists(r.ThumbnailPath))
                    File.Delete(r.ThumbnailPath);
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

        public int GetTodayCount(string orgId, string? userId = null)
        {
            var start = DateTime.UtcNow.Date;
            var end = start.AddDays(1);
            var fb = Builders<ScreenshotRecord>.Filter;
            var filter = fb.And(
                fb.Eq(x => x.OrganizationId, orgId),
                fb.Gte(x => x.TakenAt, start),
                fb.Lt(x => x.TakenAt, end));
            if (userId != null)
                filter &= fb.Eq(x => x.UserId, userId);
            return (int)_db.ScreenshotRecords.CountDocuments(filter);
        }
    }
}
