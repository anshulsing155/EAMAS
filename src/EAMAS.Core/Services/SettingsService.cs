using EAMAS.Core.Data;
using EAMAS.Core.Models;
using MongoDB.Driver;

namespace EAMAS.Core.Services
{
    public class SettingsService
    {
        private readonly MongoDbContext _db;

        public SettingsService(MongoDbContext db)
        {
            _db = db;
        }

        public SystemSettings GetSettings(string orgId)
        {
            var settings = _db.SystemSettings
                .Find(s => s.OrganizationId == orgId)
                .FirstOrDefault();

            if (settings == null)
            {
                settings = CreateDefault(orgId);
                _db.SystemSettings.InsertOne(settings);
            }

            if (string.IsNullOrEmpty(settings.ScreenshotsDirectory))
                settings.ScreenshotsDirectory = GetDefaultScreenshotsDirectory(orgId);

            return settings;
        }

        public void SaveSettings(SystemSettings settings)
        {
            settings.UpdatedAt = DateTime.UtcNow;
            if (string.IsNullOrEmpty(settings.ScreenshotsDirectory))
                settings.ScreenshotsDirectory = GetDefaultScreenshotsDirectory(settings.OrganizationId);

            _db.SystemSettings.ReplaceOne(
                s => s.OrganizationId == settings.OrganizationId,
                settings,
                new ReplaceOptions { IsUpsert = true });
        }

        public static string GetDefaultScreenshotsDirectory(string orgId)
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "EAMAS", "Screenshots", orgId);
            Directory.CreateDirectory(path);
            return path;
        }

        private static SystemSettings CreateDefault(string orgId) => new()
        {
            OrganizationId = orgId,
            MonitoringEnabled = true,
            ScreenshotsEnabled = true,
            ScreenshotIntervalMinutes = 5,
            IdleThresholdSeconds = 300,
            ActivityPollIntervalSeconds = 5,
            MaxScreenshotAgeDays = 30,
            JpegQuality = 70,
            AlertOnLongIdle = true,
            LongIdleThresholdMinutes = 30,
            AlertOnDistractingUsage = true,
            DistractingUsageThresholdMinutes = 60,
            ScreenshotsDirectory = GetDefaultScreenshotsDirectory(orgId),
            UpdatedAt = DateTime.UtcNow
        };
    }
}
