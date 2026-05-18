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

        /// <summary>Maximum plausible single-session duration (safety cap).</summary>
        private static readonly TimeSpan MaxSessionDuration = TimeSpan.FromMinutes(15);

        public void RecordActivity(ActivityLog log)
        {
            if (log.EndTime <= log.StartTime) return;

            // ── Clock-manipulation guard ─────────────────────────────────────────
            // 1. Reject sessions that end in the future (clock moved forward)
            var now = DateTime.UtcNow;
            if (log.EndTime > now.AddSeconds(10))  // 10s tolerance for processing delay
            {
                log.OriginalEndTime = log.EndTime;
                log.EndTime = now;
                log.WasClockAdjusted = true;
            }

            // 2. Cap maximum session duration — no single window session should
            //    exceed the safety limit. This catches clock jumps that the client
            //    didn't detect (e.g. if TimeIntegrityService was bypassed).
            var duration = log.EndTime - log.StartTime;
            if (duration > MaxSessionDuration)
            {
                if (!log.WasClockAdjusted)
                    log.OriginalEndTime = log.EndTime;
                log.EndTime = log.StartTime + MaxSessionDuration;
                log.WasClockAdjusted = true;
            }

            // 3. After clamping, re-check validity
            if (log.EndTime <= log.StartTime || (log.EndTime - log.StartTime).TotalSeconds < 2) return;

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
                _db.AppUsages.UpdateOne(filter,
                    Builders<AppUsage>.Update.Inc(a => a.DurationTicks, log.Duration.Ticks));
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

        /// <summary>Get activity logs for a specific user. Pass orgId=null for all orgs (SuperAdmin).</summary>
        public List<ActivityLog> GetActivity(string? orgId, string? userId,
            DateTime from, DateTime to, int limit = 500)
        {
            var fb = Builders<ActivityLog>.Filter;
            var filter = fb.And(
                fb.Gte(x => x.StartTime, from),
                fb.Lt(x => x.StartTime, to));

            if (!string.IsNullOrEmpty(orgId))
                filter &= fb.Eq(x => x.OrganizationId, orgId);
            if (!string.IsNullOrEmpty(userId))
                filter &= fb.Eq(x => x.UserId, userId);

            return _db.ActivityLogs
                .Find(filter)
                .SortByDescending(x => x.StartTime)
                .Limit(limit)
                .ToList();
        }

        public List<ActivityLog> GetTodayActivity(string orgId, string userId)
        {
            var today = DateTime.Today;
            return GetActivity(orgId, userId, today, today.AddDays(1));
        }

        /// <summary>Get app usage aggregates. Pass orgId=null for all orgs (SuperAdmin).</summary>
        public List<AppUsage> GetAppUsage(string? orgId, string? userId, DateTime from, DateTime to)
        {
            var fb = Builders<AppUsage>.Filter;
            var filter = fb.And(
                fb.Gte(x => x.RecordedAt, from),
                fb.Lt(x => x.RecordedAt, to));

            if (!string.IsNullOrEmpty(orgId))
                filter &= fb.Eq(x => x.OrganizationId, orgId);
            if (!string.IsNullOrEmpty(userId))
                filter &= fb.Eq(x => x.UserId, userId);

            return _db.AppUsages.Find(filter).ToList();
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

        /// <summary>Hourly activity across all users in an org. Pass orgId=null for all orgs (SuperAdmin).</summary>
        public Dictionary<int, TimeSpan> GetHourlyActivityAllUsers(string? orgId, DateTime date)
        {
            var start = date.Date;
            var end = start.AddDays(1);

            var fb = Builders<ActivityLog>.Filter;
            var filter = fb.And(
                fb.Gte(x => x.StartTime, start),
                fb.Lt(x => x.StartTime, end),
                fb.Eq(x => x.IsIdle, false));

            if (!string.IsNullOrEmpty(orgId))
                filter &= fb.Eq(x => x.OrganizationId, orgId);

            var logs = _db.ActivityLogs.Find(filter).ToList();
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

        // ── Data retention ────────────────────────────────────────────────────────

        /// <summary>
        /// Finds and caps any historically exploited activity logs where users
        /// manipulated their system clocks to inflate their productive time.
        /// </summary>
        public int CleanUpTimeManipulationExploits()
        {
            var now = DateTime.UtcNow;

            // Pull only obviously suspicious records: EndTime in the future, or duration > 15 min.
            var filter = Builders<ActivityLog>.Filter.Or(
                Builders<ActivityLog>.Filter.Gt(x => x.EndTime, now.AddMinutes(5)),
                Builders<ActivityLog>.Filter.Where(
                    x => (x.EndTime > x.StartTime) && (x.EndTime > x.StartTime.AddMinutes(15)))
            );

            var suspiciousLogs = _db.ActivityLogs.Find(filter).ToList();
            int fixedCount = 0;

            foreach (var log in suspiciousLogs)
            {
                var originalEnd = log.EndTime;
                var newEnd = log.EndTime;
                bool needsUpdate = false;

                // Rule 1: EndTime in the future
                if (newEnd > now)
                {
                    newEnd = now;
                    needsUpdate = true;
                }

                // Rule 2: Duration > 15 minutes
                if ((newEnd - log.StartTime).TotalMinutes > 15)
                {
                    newEnd = log.StartTime.AddMinutes(15);
                    needsUpdate = true;
                }

                if (!needsUpdate) continue;

                // Use field-level update to avoid overwriting concurrent writes (race-condition fix).
                _db.ActivityLogs.UpdateOne(
                    Builders<ActivityLog>.Filter.Eq(x => x.Id, log.Id),
                    Builders<ActivityLog>.Update
                        .Set(x => x.EndTime, newEnd)
                        .Set(x => x.WasClockAdjusted, true)
                        .Set(x => x.OriginalEndTime, originalEnd));

                // Correct the AppUsage aggregate — guard against going negative.
                var diffTicks = (originalEnd - log.StartTime).Ticks - (newEnd - log.StartTime).Ticks;
                if (diffTicks > 0)
                {
                    var usageFilter = Builders<AppUsage>.Filter.And(
                        Builders<AppUsage>.Filter.Eq(a => a.OrganizationId, log.OrganizationId),
                        Builders<AppUsage>.Filter.Eq(a => a.UserId, log.UserId),
                        Builders<AppUsage>.Filter.Eq(a => a.ApplicationName, log.ApplicationName),
                        Builders<AppUsage>.Filter.Eq(a => a.RecordedAt, log.StartTime.Date),
                        // Guard: only decrement if current DurationTicks >= diffTicks
                        Builders<AppUsage>.Filter.Gte(a => a.DurationTicks, diffTicks));

                    _db.AppUsages.UpdateOne(usageFilter,
                        Builders<AppUsage>.Update.Inc(a => a.DurationTicks, -diffTicks));
                }

                fixedCount++;
            }

            return fixedCount;
        }

        /// <summary>
        /// Deletes ActivityLog and AppUsage documents older than <paramref name="days"/> days
        /// for the given organisation. Pass orgId=null to purge across all orgs (SuperAdmin).
        /// </summary>
        public void PurgeOldData(string? orgId, int days)
        {
            if (days <= 0) return;
            var cutoff = DateTime.UtcNow.AddDays(-days);

            var logFb = Builders<ActivityLog>.Filter.Lt(x => x.StartTime, cutoff);
            var usageFb = Builders<AppUsage>.Filter.Lt(x => x.RecordedAt, cutoff);

            if (!string.IsNullOrEmpty(orgId))
            {
                logFb   &= Builders<ActivityLog>.Filter.Eq(x => x.OrganizationId, orgId);
                usageFb &= Builders<AppUsage>.Filter.Eq(x => x.OrganizationId, orgId);
            }

            _db.ActivityLogs.DeleteMany(logFb);
            _db.AppUsages.DeleteMany(usageFb);
        }

        /// <summary>Count distinct users who had at least one activity log on the given date.</summary>
        public int GetActiveUserCount(string? orgId, DateTime date)
        {
            var start = date.Date;
            var end = start.AddDays(1);

            var fb = Builders<ActivityLog>.Filter;
            var filter = fb.And(
                fb.Gte(x => x.StartTime, start),
                fb.Lt(x => x.StartTime, end),
                fb.Eq(x => x.IsIdle, false));

            if (!string.IsNullOrEmpty(orgId))
                filter &= fb.Eq(x => x.OrganizationId, orgId);

            return _db.ActivityLogs
                .Distinct(x => x.UserId, filter)
                .ToList()
                .Count;
        }
    }
}
