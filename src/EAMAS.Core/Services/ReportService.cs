using EAMAS.Core.Data;
using EAMAS.Core.Enums;
using EAMAS.Core.Models;
using MongoDB.Driver;

namespace EAMAS.Core.Services
{
    public record DailySummary(
        DateTime Date,
        TimeSpan ActiveTime,
        TimeSpan IdleTime,
        TimeSpan ProductiveTime,
        TimeSpan DistractingTime,
        int ProductivityScore,
        int ScreenshotCount,
        List<(string App, TimeSpan Duration)> TopApps);

    public record WeeklyTrend(
        DateTime WeekStart,
        List<(DateTime Date, TimeSpan ActiveTime, int Score)> DailyPoints);

    public record AppUsageDetail(
        string ApplicationName,
        string ProcessName,
        ActivityCategory Category,
        TimeSpan TotalDuration,
        int ProductivityRating);

    public record HourlyActivity(
        int Hour,
        TimeSpan ActiveTime,
        TimeSpan ProductiveTime,
        TimeSpan DistractingTime);

    public class ReportService
    {
        private readonly MongoDbContext _db;

        public ReportService(MongoDbContext db)
        {
            _db = db;
        }

        public DailySummary GetDailySummary(string orgId, string userId, DateTime date)
        {
            var start = date.Date;
            var end = start.AddDays(1);

            var logs = _db.ActivityLogs
                .Find(x => x.OrganizationId == orgId &&
                           x.UserId == userId &&
                           x.StartTime >= start &&
                           x.StartTime < end)
                .ToList();

            var activeTime = TimeSpan.FromTicks(logs.Where(x => !x.IsIdle).Sum(x => x.Duration.Ticks));
            var idleTime = TimeSpan.FromTicks(logs.Where(x => x.IsIdle).Sum(x => x.Duration.Ticks));
            var productiveTime = TimeSpan.FromTicks(logs
                .Where(x => !x.IsIdle && x.Category == ActivityCategory.Productive)
                .Sum(x => x.Duration.Ticks));
            var distractingTime = TimeSpan.FromTicks(logs
                .Where(x => !x.IsIdle && x.Category == ActivityCategory.Distracting)
                .Sum(x => x.Duration.Ticks));

            var screenshotCount = (int)_db.ScreenshotRecords.CountDocuments(x =>
                x.OrganizationId == orgId &&
                x.UserId == userId &&
                x.TakenAt >= start &&
                x.TakenAt < end);

            var topApps = _db.AppUsages
                .Find(x => x.OrganizationId == orgId &&
                           x.UserId == userId &&
                           x.RecordedAt == start)
                .SortByDescending(x => x.DurationTicks)
                .Limit(5)
                .ToList()
                .Select(x => (x.ApplicationName, x.Duration))
                .ToList();

            var score = CalculateProductivityScore(activeTime, productiveTime, distractingTime);
            return new DailySummary(date, activeTime, idleTime, productiveTime,
                distractingTime, score, screenshotCount, topApps);
        }

        public List<DailySummary> GetRangeSummaries(string orgId, string userId,
            DateTime from, DateTime to)
        {
            var result = new List<DailySummary>();
            for (var d = from.Date; d < to.Date; d = d.AddDays(1))
                result.Add(GetDailySummary(orgId, userId, d));
            return result;
        }

        public WeeklyTrend GetWeeklyTrend(string orgId, string userId, DateTime weekStart)
        {
            var points = new List<(DateTime, TimeSpan, int)>();
            for (int i = 0; i < 7; i++)
            {
                var d = weekStart.AddDays(i);
                var summary = GetDailySummary(orgId, userId, d);
                points.Add((d, summary.ActiveTime, summary.ProductivityScore));
            }
            return new WeeklyTrend(weekStart, points);
        }

        public Dictionary<string, TimeSpan> GetTopApplications(string orgId, string userId,
            DateTime from, DateTime to, int take = 10)
        {
            var usages = _db.AppUsages
                .Find(x => x.OrganizationId == orgId &&
                           x.UserId == userId &&
                           x.RecordedAt >= from.Date &&
                           x.RecordedAt < to.Date)
                .ToList();

            return usages
                .GroupBy(x => x.ApplicationName)
                .Select(g => new { App = g.Key, Ticks = g.Sum(x => x.DurationTicks) })
                .OrderByDescending(x => x.Ticks)
                .Take(take)
                .ToDictionary(x => x.App, x => TimeSpan.FromTicks(x.Ticks));
        }

        public Dictionary<ActivityCategory, TimeSpan> GetCategoryTotals(string orgId, string userId,
            DateTime from, DateTime to)
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

        public static int CalculateProductivityScore(
            TimeSpan activeTime, TimeSpan productiveTime, TimeSpan distractingTime)
        {
            if (activeTime.TotalSeconds < 60) return 0;
            var nonIdle = activeTime.TotalMinutes;
            var productive = productiveTime.TotalMinutes;
            var distracting = distractingTime.TotalMinutes;
            var score = ((productive - distracting * 0.5) / nonIdle) * 100.0;
            return Math.Clamp((int)Math.Round(score), 0, 100);
        }

        public int GetAverageProductivityScore(string orgId, string userId,
            DateTime from, DateTime to)
        {
            var summaries = GetRangeSummaries(orgId, userId, from, to);
            var scored = summaries.Where(s => s.ActiveTime.TotalMinutes >= 10).ToList();
            if (!scored.Any()) return 0;
            return (int)scored.Average(s => s.ProductivityScore);
        }

        /// <summary>Per-app usage with category and productivity rating for the period.</summary>
        public List<AppUsageDetail> GetAppUsageDetails(string orgId, string userId,
            DateTime from, DateTime to, int take = 20)
        {
            var logs = _db.ActivityLogs
                .Find(x => x.OrganizationId == orgId &&
                           x.UserId == userId &&
                           !x.IsIdle &&
                           x.StartTime >= from &&
                           x.StartTime < to)
                .ToList();

            return logs
                .GroupBy(x => x.ApplicationName)
                .Select(g =>
                {
                    var total      = g.Sum(x => x.Duration.Ticks);
                    var productive = g.Where(x => x.Category == ActivityCategory.Productive).Sum(x => x.Duration.Ticks);
                    var distracting= g.Where(x => x.Category == ActivityCategory.Distracting).Sum(x => x.Duration.Ticks);
                    var dominant   = g.GroupBy(x => x.Category)
                                      .OrderByDescending(c => c.Sum(x => x.Duration.Ticks))
                                      .First().Key;
                    var processName= g.First().ProcessName;
                    var rating     = total == 0 ? 50
                        : (int)Math.Clamp(
                            ((double)productive / total * 100) -
                            ((double)distracting / total * 50), 0, 100);
                    return new AppUsageDetail(
                        g.Key, processName, dominant,
                        TimeSpan.FromTicks(total), rating);
                })
                .OrderByDescending(a => a.TotalDuration)
                .Take(take)
                .ToList();
        }

        /// <summary>Activity broken down by hour-of-day for the full period.</summary>
        public List<HourlyActivity> GetHourlyBreakdown(string orgId, string userId,
            DateTime from, DateTime to)
        {
            var logs = _db.ActivityLogs
                .Find(x => x.OrganizationId == orgId &&
                           x.UserId == userId &&
                           !x.IsIdle &&
                           x.StartTime >= from &&
                           x.StartTime < to)
                .ToList();

            return Enumerable.Range(0, 24).Select(h =>
            {
                var hourLogs    = logs.Where(x => x.StartTime.Hour == h).ToList();
                var active      = TimeSpan.FromTicks(hourLogs.Sum(x => x.Duration.Ticks));
                var productive  = TimeSpan.FromTicks(hourLogs
                    .Where(x => x.Category == ActivityCategory.Productive).Sum(x => x.Duration.Ticks));
                var distracting = TimeSpan.FromTicks(hourLogs
                    .Where(x => x.Category == ActivityCategory.Distracting).Sum(x => x.Duration.Ticks));
                return new HourlyActivity(h, active, productive, distracting);
            }).ToList();
        }

        /// <summary>All alerts fired for the user in the given period.</summary>
        public List<Alert> GetAlertsForPeriod(string orgId, string userId,
            DateTime from, DateTime to)
        {
            return _db.Alerts
                .Find(x => x.OrganizationId == orgId &&
                           x.UserId == userId &&
                           x.CreatedAt >= from &&
                           x.CreatedAt < to)
                .SortByDescending(x => x.CreatedAt)
                .ToList();
        }

        /// <summary>Audit log entries for the organisation in the given period (admin only).</summary>
        public List<AuditLog> GetAuditLogsForPeriod(string orgId, DateTime from, DateTime to)
        {
            return _db.AuditLogs
                .Find(x => x.OrganizationId == orgId &&
                           x.Timestamp >= from &&
                           x.Timestamp < to)
                .SortByDescending(x => x.Timestamp)
                .ToList();
        }
    }
}
