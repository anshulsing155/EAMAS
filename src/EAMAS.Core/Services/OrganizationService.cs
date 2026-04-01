using EAMAS.Core.Data;
using EAMAS.Core.Models;
using MongoDB.Driver;

namespace EAMAS.Core.Services
{
    /// <summary>
    /// Manages organisations (tenants). Only SuperAdmin can create/delete organisations.
    /// Each organisation has its own Admin, Managers, and Employees.
    /// </summary>
    public class OrganizationService
    {
        private readonly MongoDbContext _db;

        public OrganizationService(MongoDbContext db)
        {
            _db = db;
        }

        public Organization? GetById(string id)
        {
            return _db.Organizations.Find(o => o.Id == id).FirstOrDefault();
        }

        public Organization? GetByCode(string code)
        {
            var upper = code.Trim().ToUpperInvariant();
            return _db.Organizations
                .Find(o => o.Code == upper && o.IsActive)
                .FirstOrDefault();
        }

        public List<Organization> GetAll()
        {
            return _db.Organizations.Find(_ => true)
                .SortBy(o => o.Name)
                .ToList();
        }

        public Organization Create(string name, string code, string description = "")
        {
            var org = new Organization
            {
                Name = name.Trim(),
                Code = code.Trim().ToUpperInvariant(),
                Description = description,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            _db.Organizations.InsertOne(org);
            return org;
        }

        public void Update(Organization org)
        {
            org.Code = org.Code.Trim().ToUpperInvariant();
            _db.Organizations.ReplaceOne(o => o.Id == org.Id, org);
        }

        public void Deactivate(string orgId)
        {
            var update = Builders<Organization>.Update.Set(o => o.IsActive, false);
            _db.Organizations.UpdateOne(o => o.Id == orgId, update);
        }

        public bool CodeExists(string code, string? excludeId = null)
        {
            var upper = code.Trim().ToUpperInvariant();
            var filter = Builders<Organization>.Filter.Eq(o => o.Code, upper);
            if (excludeId != null)
                filter &= Builders<Organization>.Filter.Ne(o => o.Id, excludeId);
            return _db.Organizations.CountDocuments(filter) > 0;
        }

        public void SetAdmin(string orgId, string adminUserId)
        {
            var update = Builders<Organization>.Update.Set(o => o.AdminUserId, adminUserId);
            _db.Organizations.UpdateOne(o => o.Id == orgId, update);
        }
    }
}
