using EAMAS.Core.Data;
using EAMAS.Core.Models;
using MongoDB.Driver;

namespace EAMAS.Core.Services
{
    /// <summary>
    /// Seeds initial data on first run:
    ///  - A "SYSTEM" organisation for the SuperAdmin
    ///  - The default SuperAdmin user (superadmin / Admin@123)
    /// </summary>
    public class DatabaseInitializerService
    {
        private readonly MongoDbContext _db;
        private readonly SettingsService _settingsService;

        public DatabaseInitializerService(MongoDbContext db, SettingsService settingsService)
        {
            _db = db;
            _settingsService = settingsService;
        }

        public void Initialize()
        {
            SeedSuperAdmin();
        }

        private void SeedSuperAdmin()
        {
            const string systemOrgId = "SYSTEM";

            // Only seed if no SuperAdmin exists
            if (_db.Users.CountDocuments(u =>
                    u.OrganizationId == systemOrgId &&
                    u.Role == UserRole.SuperAdmin) > 0)
                return;

            _db.Users.InsertOne(new User
            {
                OrganizationId = systemOrgId,
                Username = "superadmin",
                PasswordHash = UserService.HashPassword("Admin@123"),
                FullName = "System Administrator",
                Email = "superadmin@eamas.local",
                Department = "IT",
                Role = UserRole.SuperAdmin,
                IsActive = true,
                ConsentGiven = true,
                CreatedAt = DateTime.UtcNow
            });
        }
    }
}
