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
            // Prevent duplicates within the last hour
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
            var filter = Builders<Alert>.Filter.And(
                Builders<Alert>.Filter.Eq(a => a.OrganizationId, orgId),
                Builders<Alert>.Filter.Eq(a => a.IsRead, false));

            if (userId != null)
                filter &= Builders<Alert>.Filter.Eq(a => a.UserId, userId);

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

        public void MarkRead(string alertId)
        {
            var update = Builders<Alert>.Update.Set(a => a.IsRead, true);
            _db.Alerts.UpdateOne(a => a.Id == alertId, update);
        }

        public void MarkAllRead(string orgId, string userId)
        {
            var filter = Builders<Alert>.Filter.And(
                Builders<Alert>.Filter.Eq(a => a.OrganizationId, orgId),
                Builders<Alert>.Filter.Eq(a => a.UserId, userId),
                Builders<Alert>.Filter.Eq(a => a.IsRead, false));
            var update = Builders<Alert>.Update.Set(a => a.IsRead, true);
            _db.Alerts.UpdateMany(filter, update);
        }

        public void CheckAndGenerateAlerts(string orgId, string userId,
            SystemSettings settings, TimeSpan currentIdleTime, TimeSpan distractingTimeToday)
        {
            if (settings.AlertOnLongIdle &&
                currentIdleTime.TotalMinutes >= settings.LongIdleThresholdMinutes)
            {
                CreateAlert(orgId, userId, AlertType.LongIdle,
                    $"Employee has been idle for {(int)currentIdleTime.TotalMinutes} minutes.");
            }

            if (settings.AlertOnDistractingUsage &&
                distractingTimeToday.TotalMinutes >= settings.DistractingUsageThresholdMinutes)
            {
                CreateAlert(orgId, userId, AlertType.DistractingUsage,
                    $"Distracting usage today: {(int)distractingTimeToday.TotalMinutes} minutes.");
            }
        }

        public int GetUnreadCount(string orgId, string? userId = null)
        {
            var filter = Builders<Alert>.Filter.And(
                Builders<Alert>.Filter.Eq(a => a.OrganizationId, orgId),
                Builders<Alert>.Filter.Eq(a => a.IsRead, false));
            if (userId != null)
                filter &= Builders<Alert>.Filter.Eq(a => a.UserId, userId);
            return (int)_db.Alerts.CountDocuments(filter);
        }
    }
}
