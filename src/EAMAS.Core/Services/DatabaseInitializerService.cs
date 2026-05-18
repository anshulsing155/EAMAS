using EAMAS.Core.Data;
using EAMAS.Core.Models;
using MongoDB.Driver;
using System.Diagnostics;

namespace EAMAS.Core.Services
{
    /// <summary>
    /// Seeds the initial SuperAdmin user on first run, then seeds demo data.
    /// The initial password MUST be set via the EAMAS_SUPERADMIN_PASSWORD environment variable.
    /// If the env-var is absent the account is seeded with a temporary password and
    /// MustChangePassword is set to true so the operator is forced to rotate it on first login.
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
            new DemoDataSeeder(_db).Seed();
        }

        private void SeedSuperAdmin()
        {
            const string systemOrgId = "SYSTEM";

            // Only seed if no SuperAdmin exists yet.
            if (_db.Users.CountDocuments(u =>
                    u.OrganizationId == systemOrgId &&
                    u.Role == UserRole.SuperAdmin) > 0)
                return;

            var envPassword = Environment.GetEnvironmentVariable("EAMAS_SUPERADMIN_PASSWORD");
            bool usingFallback = string.IsNullOrWhiteSpace(envPassword);

            // Fallback: generate a random 16-char temporary password so no known default
            // credential exists in the database.  MustChangePassword = true forces rotation.
            string initialPassword = usingFallback
                ? GenerateTemporaryPassword()
                : envPassword!;

            if (usingFallback)
            {
                // Write the temporary password to the debug output so the installer / operator
                // can read it on first run.  Never log this in production telemetry.
                Debug.WriteLine("[EAMAS] *** EAMAS_SUPERADMIN_PASSWORD is not set. ***");
                Debug.WriteLine($"[EAMAS] Temporary SuperAdmin password: {initialPassword}");
                Debug.WriteLine("[EAMAS] You will be required to change it on first login.");
            }

            _db.Users.InsertOne(new User
            {
                OrganizationId = systemOrgId,
                Username       = "superadmin",
                PasswordHash   = UserService.HashPassword(initialPassword),
                FullName       = "System Administrator",
                Email          = "superadmin@eamas.local",
                Department     = "IT",
                Role           = UserRole.SuperAdmin,
                IsActive       = true,
                ConsentGiven   = true,
                MustChangePassword = usingFallback,  // force rotation when using auto-generated pwd
                CreatedAt      = DateTime.UtcNow
            });
        }

        /// <summary>Generates a cryptographically random 16-character temporary password.</summary>
        private static string GenerateTemporaryPassword()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789!@#$%";
            var bytes  = System.Security.Cryptography.RandomNumberGenerator.GetBytes(16);
            var result = new System.Text.StringBuilder(16);
            foreach (var b in bytes)
                result.Append(chars[b % chars.Length]);
            return result.ToString();
        }
    }
}
