using System;
using System.Security.Authentication;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using EAMAS.Core.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace EAMAS.Core.Data
{
    /// <summary>
    /// Central MongoDB context. Registered as a singleton in DI.
    /// All collections are org-scoped via OrganizationId field.
    /// </summary>
    public class MongoDbContext
    {
        private readonly IMongoDatabase _db;

        public MongoDbContext(string connectionString, string databaseName)
        {
            // If connectionString not provided, try environment variable to avoid hard-coding credentials.
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                connectionString = Environment.GetEnvironmentVariable("DATABASE_URL") ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException("MongoDB connection string must be supplied either via constructor or the DATABASE_URL environment variable.");
            }

            // Build settings from the connection string so we can tune timeouts and TLS for remote clusters (Atlas)
            var settings = MongoClientSettings.FromConnectionString(connectionString);
            settings.ServerSelectionTimeout = TimeSpan.FromSeconds(60);
            settings.ConnectTimeout = TimeSpan.FromSeconds(10);

            // Ensure modern TLS protocols for Atlas / cloud providers
            settings.SslSettings ??= new SslSettings();
            settings.SslSettings.EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;

            var client = new MongoClient(settings);
            _db = client.GetDatabase(databaseName);

            // Do not block application startup with a synchronous ping or index creation. Start index creation in the background
            // and retry on failure so the app can start even if the database is temporarily unreachable.
            _ = Task.Run(() => EnsureIndexesWithRetryAsync(TimeSpan.FromSeconds(5), maxAttempts: 6));
        }

        // ── Collections ──────────────────────────────────────────────────────────
        public IMongoCollection<Organization> Organizations =>
            _db.GetCollection<Organization>("organizations");

        public IMongoCollection<User> Users =>
            _db.GetCollection<User>("users");

        public IMongoCollection<ActivityLog> ActivityLogs =>
            _db.GetCollection<ActivityLog>("activity_logs");

        public IMongoCollection<AppUsage> AppUsages =>
            _db.GetCollection<AppUsage>("app_usages");

        public IMongoCollection<ScreenshotRecord> ScreenshotRecords =>
            _db.GetCollection<ScreenshotRecord>("screenshot_records");

        public IMongoCollection<Alert> Alerts =>
            _db.GetCollection<Alert>("alerts");

        public IMongoCollection<AppCategoryRule> AppCategoryRules =>
            _db.GetCollection<AppCategoryRule>("app_category_rules");

        public IMongoCollection<SystemSettings> SystemSettings =>
            _db.GetCollection<SystemSettings>("system_settings");

        // ── Index creation ───────────────────────────────────────────────────────
        private void EnsureIndexes()
        {
            // Organizations: unique code (case-insensitive handled at app layer)
            Organizations.Indexes.CreateOne(new CreateIndexModel<Organization>(
                Builders<Organization>.IndexKeys.Ascending(o => o.Code),
                new CreateIndexOptions { Unique = true, Background = true }));

            // Users: unique (OrganizationId + Username)
            Users.Indexes.CreateOne(new CreateIndexModel<User>(
                Builders<User>.IndexKeys
                    .Ascending(u => u.OrganizationId)
                    .Ascending(u => u.Username),
                new CreateIndexOptions { Unique = true, Background = true }));

            // ActivityLogs: (OrganizationId, UserId, StartTime)
            ActivityLogs.Indexes.CreateOne(new CreateIndexModel<ActivityLog>(
                Builders<ActivityLog>.IndexKeys
                    .Ascending(x => x.OrganizationId)
                    .Ascending(x => x.UserId)
                    .Ascending(x => x.StartTime),
                new CreateIndexOptions { Background = true }));

            // AppUsages: (OrganizationId, UserId, RecordedAt)
            AppUsages.Indexes.CreateOne(new CreateIndexModel<AppUsage>(
                Builders<AppUsage>.IndexKeys
                    .Ascending(x => x.OrganizationId)
                    .Ascending(x => x.UserId)
                    .Ascending(x => x.RecordedAt),
                new CreateIndexOptions { Background = true }));

            // ScreenshotRecords: (OrganizationId, UserId, TakenAt)
            ScreenshotRecords.Indexes.CreateOne(new CreateIndexModel<ScreenshotRecord>(
                Builders<ScreenshotRecord>.IndexKeys
                    .Ascending(x => x.OrganizationId)
                    .Ascending(x => x.UserId)
                    .Ascending(x => x.TakenAt),
                new CreateIndexOptions { Background = true }));

            // Alerts: (OrganizationId, UserId, CreatedAt)
            Alerts.Indexes.CreateOne(new CreateIndexModel<Alert>(
                Builders<Alert>.IndexKeys
                    .Ascending(x => x.OrganizationId)
                    .Ascending(x => x.UserId)
                    .Ascending(x => x.CreatedAt),
                new CreateIndexOptions { Background = true }));

            // AppCategoryRules: (OrganizationId, Priority)
            AppCategoryRules.Indexes.CreateOne(new CreateIndexModel<AppCategoryRule>(
                Builders<AppCategoryRule>.IndexKeys
                    .Ascending(x => x.OrganizationId)
                    .Descending(x => x.Priority),
                new CreateIndexOptions { Background = true }));

            // SystemSettings: unique OrganizationId (one settings doc per org)
            SystemSettings.Indexes.CreateOne(new CreateIndexModel<SystemSettings>(
                Builders<SystemSettings>.IndexKeys.Ascending(x => x.OrganizationId),
                new CreateIndexOptions { Unique = true, Background = true }));
        }

        // Async version of EnsureIndexes used by the background retry loop.
        private async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
        {
            // Organizations: unique code (case-insensitive handled at app layer)
            await Organizations.Indexes.CreateOneAsync(new CreateIndexModel<Organization>(
                Builders<Organization>.IndexKeys.Ascending(o => o.Code),
                new CreateIndexOptions { Unique = true, Background = true })).ConfigureAwait(false);

            // Users: unique (OrganizationId + Username)
            await Users.Indexes.CreateOneAsync(new CreateIndexModel<User>(
                Builders<User>.IndexKeys
                    .Ascending(u => u.OrganizationId)
                    .Ascending(u => u.Username),
                new CreateIndexOptions { Unique = true, Background = true })).ConfigureAwait(false);

            // ActivityLogs: (OrganizationId, UserId, StartTime)
            await ActivityLogs.Indexes.CreateOneAsync(new CreateIndexModel<ActivityLog>(
                Builders<ActivityLog>.IndexKeys
                    .Ascending(x => x.OrganizationId)
                    .Ascending(x => x.UserId)
                    .Ascending(x => x.StartTime),
                new CreateIndexOptions { Background = true })).ConfigureAwait(false);

            // AppUsages: (OrganizationId, UserId, RecordedAt)
            await AppUsages.Indexes.CreateOneAsync(new CreateIndexModel<AppUsage>(
                Builders<AppUsage>.IndexKeys
                    .Ascending(x => x.OrganizationId)
                    .Ascending(x => x.UserId)
                    .Ascending(x => x.RecordedAt),
                new CreateIndexOptions { Background = true })).ConfigureAwait(false);

            // ScreenshotRecords: (OrganizationId, UserId, TakenAt)
            await ScreenshotRecords.Indexes.CreateOneAsync(new CreateIndexModel<ScreenshotRecord>(
                Builders<ScreenshotRecord>.IndexKeys
                    .Ascending(x => x.OrganizationId)
                    .Ascending(x => x.UserId)
                    .Ascending(x => x.TakenAt),
                new CreateIndexOptions { Background = true })).ConfigureAwait(false);

            // Alerts: (OrganizationId, UserId, CreatedAt)
            await Alerts.Indexes.CreateOneAsync(new CreateIndexModel<Alert>(
                Builders<Alert>.IndexKeys
                    .Ascending(x => x.OrganizationId)
                    .Ascending(x => x.UserId)
                    .Ascending(x => x.CreatedAt),
                new CreateIndexOptions { Background = true })).ConfigureAwait(false);

            // AppCategoryRules: (OrganizationId, Priority)
            await AppCategoryRules.Indexes.CreateOneAsync(new CreateIndexModel<AppCategoryRule>(
                Builders<AppCategoryRule>.IndexKeys
                    .Ascending(x => x.OrganizationId)
                    .Descending(x => x.Priority),
                new CreateIndexOptions { Background = true })).ConfigureAwait(false);

            // SystemSettings: unique OrganizationId (one settings doc per org)
            await SystemSettings.Indexes.CreateOneAsync(new CreateIndexModel<SystemSettings>(
                Builders<SystemSettings>.IndexKeys.Ascending(x => x.OrganizationId),
                new CreateIndexOptions { Unique = true, Background = true })).ConfigureAwait(false);
        }

        private async Task EnsureIndexesWithRetryAsync(TimeSpan initialDelay, int maxAttempts = 5)
        {
            var attempt = 0;
            var delay = initialDelay;
            while (attempt < maxAttempts)
            {
                attempt++;
                try
                {
                    await EnsureIndexesAsync().ConfigureAwait(false);
                    Debug.WriteLine($"MongoDbContext: EnsureIndexes succeeded on attempt {attempt}.");
                    return;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"MongoDbContext: EnsureIndexes attempt {attempt} failed: {ex.Message}");
                    if (attempt >= maxAttempts)
                    {
                        Debug.WriteLine("MongoDbContext: Max attempts reached; giving up on index creation for now.");
                        return;
                    }

                    try
                    {
                        await Task.Delay(delay).ConfigureAwait(false);
                    }
                    catch (TaskCanceledException) { return; }

                    // Exponential backoff
                    delay = TimeSpan.FromTicks(Math.Min(delay.Ticks * 2, TimeSpan.FromMinutes(5).Ticks));
                }
            }
        }
    }
}
