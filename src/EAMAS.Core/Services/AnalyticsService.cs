using EAMAS.Core.Data;
using EAMAS.Core.Enums;
using EAMAS.Core.Models;
using MongoDB.Driver;

namespace EAMAS.Core.Services
{
    public record PeakHourInfo(int Hour, TimeSpan ActiveTime, string Label);

    public record ProductivityTrend(
        string Label,
        int Score,
        TimeSpan ActiveTime,
        TimeSpan ProductiveTime);

    public class AnalyticsService
    {
        private readonly MongoDbContext _db;
        private readonly ReportService _reportService;

        public AnalyticsService(MongoDbContext db, ReportService reportService)
        {
            _db = db;
            _reportService = reportService;
        }

        public List<PeakHourInfo> GetPeakHours(string orgId, string userId,
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
                .GroupBy(x => x.StartTime.Hour)
                .Select(g => new { Hour = g.Key, Ticks = g.Sum(x => x.Duration.Ticks) })
                .OrderByDescending(x => x.Ticks)
                .Take(5)
                .Select(h => new PeakHourInfo(
                    h.Hour,
                    TimeSpan.FromTicks(h.Ticks),
                    $"{h.Hour:D2}:00 - {(h.Hour + 1) % 24:D2}:00"))
                .ToList();
        }

        public List<ProductivityTrend> GetLast30DaysTrend(string orgId, string userId)
        {
            var result = new List<ProductivityTrend>();
            var today = DateTime.Today;
            for (int i = 29; i >= 0; i--)
            {
                var date = today.AddDays(-i);
                var summary = _reportService.GetDailySummary(orgId, userId, date);
                result.Add(new ProductivityTrend(
                    date.ToString("MMM dd"),
                    summary.ProductivityScore,
                    summary.ActiveTime,
                    summary.ProductiveTime));
            }
            return result;
        }

        public Dictionary<string, int> GetAppProductivityRatings(string orgId, string userId,
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
                .GroupBy(x => x.ApplicationName)
                .Select(g => new
                {
                    App = g.Key,
                    Total = g.Sum(x => x.Duration.Ticks),
                    Productive = g.Where(x => x.Category == ActivityCategory.Productive)
                                  .Sum(x => x.Duration.Ticks),
                    Distracting = g.Where(x => x.Category == ActivityCategory.Distracting)
                                   .Sum(x => x.Duration.Ticks)
                })
                .OrderByDescending(x => x.Total)
                .Take(20)
                .ToDictionary(
                    x => x.App,
                    x => x.Total == 0 ? 50
                        : (int)Math.Clamp(
                            ((double)x.Productive / x.Total * 100) -
                            ((double)x.Distracting / x.Total * 50), 0, 100));
        }

        public (TimeSpan avgActive, int avgScore, int streak) GetUserStats(
            string orgId, string userId)
        {
            var today = DateTime.Today;
            var last30 = _reportService.GetRangeSummaries(
                orgId, userId, today.AddDays(-30), today.AddDays(1));
            var activeDays = last30.Where(s => s.ActiveTime.TotalMinutes >= 30).ToList();

            var avgActive = activeDays.Any()
                ? TimeSpan.FromTicks((long)activeDays.Average(s => s.ActiveTime.Ticks))
                : TimeSpan.Zero;
            var avgScore = activeDays.Any() ? (int)activeDays.Average(s => s.ProductivityScore) : 0;

            var streak = 0;
            for (int i = 1; i <= 30; i++)
            {
                var s = last30.FirstOrDefault(x => x.Date == today.AddDays(-i));
                if (s == null || s.ProductivityScore < 40) break;
                streak++;
            }
            return (avgActive, avgScore, streak);
        }

        public Dictionary<ActivityCategory, double> GetCategoryPercentages(
            string orgId, string userId, DateTime from, DateTime to)
        {
            var totals = _reportService.GetCategoryTotals(orgId, userId, from, to);
            var total = totals.Values.Sum(t => t.TotalSeconds);
            if (total == 0) return new Dictionary<ActivityCategory, double>();

            return totals.ToDictionary(
                kvp => kvp.Key,
                kvp => Math.Round(kvp.Value.TotalSeconds / total * 100, 1));
        }
    }
}
