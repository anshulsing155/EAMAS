using EAMAS.Core.Data;
using EAMAS.Core.Enums;
using EAMAS.Core.Models;
using MongoDB.Driver;

namespace EAMAS.Core.Services
{
    public class AlertService
    {
        private readonly MongoDbContext _db;

        public AlertService(MongoDbContext db)
        {
            _db = db;
        }

        public void CreateAlert(string orgId, string userId, AlertType type, string message)
        {
            // Prevent duplicate alerts of the same type within the last hour
            var cutoff = DateTime.UtcNow.AddHours(-1);
            var recent = _db.Alerts.CountDocuments(a =>
                a.OrganizationId == orgId &&
                a.UserId == userId &&
                a.Type == type &&
                a.CreatedAt >= cutoff) > 0;

            if (recent) return;

            _db.Alerts.InsertOne(new Alert
            {
                OrganizationId = orgId,
                UserId = userId,
                Type = type,
                Message = message,
                CreatedAt = DateTime.UtcNow
            });
        }

        public List<Alert> GetUnread(string orgId, string? userId = null)
        {
            var fb = Builders<Alert>.Filter;
            var filter = fb.And(
                fb.Eq(a => a.OrganizationId, orgId),
                fb.Eq(a => a.IsRead, false));

            if (userId != null)
                filter &= fb.Eq(a => a.UserId, userId);

            return _db.Alerts.Find(filter)
                .SortByDescending(a => a.CreatedAt)
                .ToList();
        }

        public List<Alert> GetAll(string orgId, string? userId = null, int limit = 100)
        {
            var filter = Builders<Alert>.Filter.Eq(a => a.OrganizationId, orgId);
            if (userId != null)
                filter &= Builders<Alert>.Filter.Eq(a => a.UserId, userId);

            return _db.Alerts.Find(filter)
                .SortByDescending(a => a.CreatedAt)
                .Limit(limit)
                .ToList();
        }

        /// <summary>SuperAdmin: get alerts across all organisations.</summary>
        public List<Alert> GetAllOrgs(int limit = 200)
        {
            return _db.Alerts.Find(Builders<Alert>.Filter.Empty)
                .SortByDescending(a => a.CreatedAt)
                .Limit(limit)
                .ToList();
        }

        public void MarkRead(string alertId)
        {
            _db.Alerts.UpdateOne(a => a.Id == alertId,
                Builders<Alert>.Update.Set(a => a.IsRead, true));
        }

        /// <summary>Mark all alerts as read for a given org (userId=null means all users in org).</summary>
        public void MarkAllRead(string orgId, string? userId = null)
        {
            var fb = Builders<Alert>.Filter;
            var filter = fb.And(
                fb.Eq(a => a.OrganizationId, orgId),
                fb.Eq(a => a.IsRead, false));

            if (userId != null)
                filter &= fb.Eq(a => a.UserId, userId);

            _db.Alerts.UpdateMany(filter, Builders<Alert>.Update.Set(a => a.IsRead, true));
        }

        /// <summary>SuperAdmin: mark all alerts across all orgs as read.</summary>
        public void MarkAllReadAllOrgs()
        {
            _db.Alerts.UpdateMany(
                Builders<Alert>.Filter.Eq(a => a.IsRead, false),
                Builders<Alert>.Update.Set(a => a.IsRead, true));
        }

        /// <summary>
        /// Evaluates all alert conditions and creates alerts where thresholds are exceeded.
        /// Called from the monitoring background service on every activity poll cycle.
        /// </summary>
        public void CheckAndGenerateAlerts(
            string orgId, string userId, SystemSettings settings,
            TimeSpan currentIdleTime, TimeSpan distractingTimeToday,
            int productivityScore, TimeSpan activeTimeToday, string currentProcessName)
        {
            // ── Long idle ─────────────────────────────────────────────────────────
            if (settings.AlertOnLongIdle &&
                currentIdleTime.TotalMinutes >= settings.LongIdleThresholdMinutes)
            {
                CreateAlert(orgId, userId, AlertType.LongIdle,
                    $"No activity for {(int)currentIdleTime.TotalMinutes} minutes.");
            }

            // ── Distracting usage ─────────────────────────────────────────────────
            if (settings.AlertOnDistractingUsage &&
                distractingTimeToday.TotalMinutes >= settings.DistractingUsageThresholdMinutes)
            {
                CreateAlert(orgId, userId, AlertType.DistractingUsage,
                    $"Distracting usage today: {(int)distractingTimeToday.TotalMinutes} min " +
                    $"(threshold: {settings.DistractingUsageThresholdMinutes} min).");
            }

            // ── Low productivity ──────────────────────────────────────────────────
            // Only fires after the employee has logged enough active time (avoids false
            // positives early in the day).
            if (settings.AlertOnLowProductivity &&
                activeTimeToday.TotalMinutes >= settings.LowProductivityMinActiveMinutes &&
                productivityScore < settings.LowProductivityThresholdPercent)
            {
                CreateAlert(orgId, userId, AlertType.LowProductivity,
                    $"Productivity score is {productivityScore}% " +
                    $"(below threshold of {settings.LowProductivityThresholdPercent}%).");
            }

            // ── Unauthorized app ──────────────────────────────────────────────────
            if (settings.AlertOnUnauthorizedApp &&
                !string.IsNullOrWhiteSpace(settings.BlockedApplications) &&
                !string.IsNullOrWhiteSpace(currentProcessName))
            {
                var blocked = settings.BlockedApplications
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                foreach (var app in blocked)
                {
                    if (currentProcessName.Contains(app, StringComparison.OrdinalIgnoreCase))
                    {
                        CreateAlert(orgId, userId, AlertType.UnauthorizedApp,
                            $"Unauthorized application in use: {currentProcessName}.");
                        break;
                    }
                }
            }

            // ── No activity ───────────────────────────────────────────────────────
            // Fires when idle time exceeds the no-activity threshold (separate from the
            // long-idle alert, which uses a shorter, configurable threshold).
            if (settings.AlertOnNoActivity &&
                currentIdleTime.TotalMinutes >= settings.NoActivityThresholdMinutes)
            {
                CreateAlert(orgId, userId, AlertType.NoActivity,
                    $"No activity recorded for {(int)currentIdleTime.TotalMinutes} minutes.");
            }
        }

        /// <summary>Delete alerts older than <paramref name="days"/> days for an org.</summary>
        public void PurgeOldAlerts(string orgId, int days)
        {
            if (days <= 0) return;
            var cutoff = DateTime.UtcNow.AddDays(-days);
            _db.Alerts.DeleteMany(a => a.OrganizationId == orgId && a.CreatedAt < cutoff);
        }

        public int GetUnreadCount(string orgId, string? userId = null)
        {
            var fb = Builders<Alert>.Filter;
            var filter = fb.And(
                fb.Eq(a => a.OrganizationId, orgId),
                fb.Eq(a => a.IsRead, false));

            if (userId != null)
                filter &= fb.Eq(a => a.UserId, userId);

            return (int)_db.Alerts.CountDocuments(filter);
        }

        /// <summary>SuperAdmin: count unread alerts across all orgs.</summary>
        public int GetUnreadCountAllOrgs()
        {
            return (int)_db.Alerts.CountDocuments(
                Builders<Alert>.Filter.Eq(a => a.IsRead, false));
        }
    }
}
