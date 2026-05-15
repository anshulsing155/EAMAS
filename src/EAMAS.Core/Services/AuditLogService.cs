using EAMAS.Core.Data;
using EAMAS.Core.Models;
using MongoDB.Driver;

namespace EAMAS.Core.Services
{
    public class AuditLogService
    {
        private readonly MongoDbContext _db;

        public AuditLogService(MongoDbContext db)
        {
            _db = db;
        }

        public void Log(string orgId, string actorUserId, string actorName,
            string action, string details)
        {
            _db.AuditLogs.InsertOne(new AuditLog
            {
                OrganizationId = orgId,
                ActorUserId    = actorUserId,
                ActorName      = actorName,
                Action         = action,
                Details        = details,
                Timestamp      = DateTime.UtcNow
            });
        }

        /// <summary>Returns the most recent audit entries for an organisation.</summary>
        public List<AuditLog> GetRecent(string orgId, int limit = 200)
        {
            return _db.AuditLogs
                .Find(a => a.OrganizationId == orgId)
                .SortByDescending(a => a.Timestamp)
                .Limit(limit)
                .ToList();
        }

        /// <summary>SuperAdmin: all orgs.</summary>
        public List<AuditLog> GetRecentAll(int limit = 500)
        {
            return _db.AuditLogs
                .Find(Builders<AuditLog>.Filter.Empty)
                .SortByDescending(a => a.Timestamp)
                .Limit(limit)
                .ToList();
        }

        /// <summary>Delete audit entries older than <paramref name="days"/> days for an org.</summary>
        public void PurgeOld(string orgId, int days)
        {
            if (days <= 0) return;
            var cutoff = DateTime.UtcNow.AddDays(-days);
            _db.AuditLogs.DeleteMany(a =>
                a.OrganizationId == orgId && a.Timestamp < cutoff);
        }
    }
}
