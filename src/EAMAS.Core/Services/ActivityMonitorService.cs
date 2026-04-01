using EAMAS.Core.Data;
using EAMAS.Core.Enums;
using EAMAS.Core.Models;
using MongoDB.Driver;

namespace EAMAS.Core.Services
{
    public class ActivityMonitorService
    {
        private readonly MongoDbContext _db;
        private readonly AppCategorizationService _categorizer;

        public ActivityMonitorService(MongoDbContext db, AppCategorizationService categorizer)
        {
            _db = db;
            _categorizer = categorizer;
        }

        public void RecordActivity(ActivityLog log)
        {
            if (log.EndTime <= log.StartTime) return;
            log.Category = _categorizer.Categorize(log.OrganizationId, log.ProcessName, log.WindowTitle);
            _db.ActivityLogs.InsertOne(log);
            UpdateAppUsage(log);
        }

        private void UpdateAppUsage(ActivityLog log)
        {
            var date = log.StartTime.Date;
            var filter = Builders<AppUsage>.Filter.And(
                Builders<AppUsage>.Filter.Eq(a => a.OrganizationId, log.OrganizationId),
                Builders<AppUsage>.Filter.Eq(a => a.UserId, log.UserId),
                Builders<AppUsage>.Filter.Eq(a => a.ApplicationName, log.ApplicationName),
                Builders<AppUsage>.Filter.Eq(a => a.RecordedAt, date));

            var existing = _db.AppUsages.Find(filter).FirstOrDefault();
            if (existing != null)
            {
                var update = Builders<AppUsage>.Update
                    .Inc(a => a.DurationTicks, log.Duration.Ticks);
                _db.AppUsages.UpdateOne(filter, update);
            }
            else
            {
                _db.AppUsages.InsertOne(new AppUsage
                {
                    OrganizationId = log.OrganizationId,
                    UserId = log.UserId,
                    ApplicationName = log.ApplicationName,
                    ProcessName = log.ProcessName,
                    Duration = log.Duration,
                    RecordedAt = date,
                    Category = log.Category
                });
            }
        }

        public List<ActivityLog> GetActivity(string orgId, string userId,
            DateTime from, DateTime to, int limit = 500)
        {
            return _db.ActivityLogs
                .Find(x => x.OrganizationId == orgId &&
                           x.UserId == userId &&
                           x.StartTime >= from &&
                           x.StartTime < to)
                .SortByDescending(x => x.StartTime)
                .Limit(limit)
                .ToList();
        }

        public List<ActivityLog> GetTodayActivity(string orgId, string userId)
        {
            var today = DateTime.Today;
            return GetActivity(orgId, userId, today, today.AddDays(1));
        }

        public List<AppUsage> GetAppUsage(string orgId, string userId, DateTime from, DateTime to)
        {
            return _db.AppUsages
                .Find(x => x.OrganizationId == orgId &&
                           x.UserId == userId &&
                           x.RecordedAt >= from &&
                           x.RecordedAt < to)
                .ToList();
        }

        public Dictionary<int, TimeSpan> GetHourlyActivity(string orgId, string userId, DateTime date)
        {
            var start = date.Date;
            var end = start.AddDays(1);

            var logs = _db.ActivityLogs
                .Find(x => x.OrganizationId == orgId &&
                           x.UserId == userId &&
                           !x.IsIdle &&
                           x.StartTime >= start &&
                           x.StartTime < end)
                .ToList();

            var result = new Dictionary<int, TimeSpan>();
            for (int h = 0; h < 24; h++) result[h] = TimeSpan.Zero;
            foreach (var log in logs)
                result[log.StartTime.Hour] += log.Duration;
            return result;
        }

        public Dictionary<ActivityCategory, TimeSpan> GetCategoryBreakdown(
            string orgId, string userId, DateTime from, DateTime to)
        {
            var logs = _db.ActivityLogs
                .Find(x => x.OrganizationId == orgId &&
                           x.UserId == userId &&
                           !x.IsIdle &&
                           x.StartTime >= from &&
                           x.StartTime < to)
                .ToList();

            return logs
                .GroupBy(x => x.Category)
                .ToDictionary(
                    g => g.Key,
                    g => TimeSpan.FromTicks(g.Sum(x => x.Duration.Ticks)));
        }
    }
}
