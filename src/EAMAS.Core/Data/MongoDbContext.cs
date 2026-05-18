using System;
using System.Security.Authentication;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using EAMAS.Core.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;

namespace EAMAS.Core.Data
{
    /// <summary>
    /// Central MongoDB context. Registered as a singleton in DI.
    /// All collections are org-scoped via OrganizationId field.
    /// </summary>
    public class MongoDbContext
    {
        private readonly IMongoDatabase _db;
        private readonly IGridFSBucket _screenshotBucket;

        public MongoDbContext(string connectionString, string databaseName)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                connectionString = Environment.GetEnvironmentVariable("DATABASE_URL") ?? string.Empty;

            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("MongoDB connection string must be supplied either via constructor or the DATABASE_URL environment variable.");

            var settings = MongoClientSettings.FromConnectionString(connectionString);
            // 10 s is enough to detect an unreachable host without blocking the UI thread for a minute.
            settings.ServerSelectionTimeout = TimeSpan.FromSeconds(10);
            settings.ConnectTimeout = TimeSpan.FromSeconds(10);
            settings.SslSettings ??= new SslSettings();
            settings.SslSettings.EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;

            var client = new MongoClient(settings);
            _db = client.GetDatabase(databaseName);

            _screenshotBucket = new GridFSBucket(_db, new GridFSBucketOptions
            {
                BucketName = "eamas_screenshots",
                ChunkSizeBytes = 1024 * 1024 // 1 MB chunks
            });

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

        public IMongoCollection<AuditLog> AuditLogs =>
            _db.GetCollection<AuditLog>("audit_logs");

        /// <summary>GridFS bucket for full-resolution screenshot images.</summary>
        public IGridFSBucket ScreenshotBucket => _screenshotBucket;

        // ── AI Engineering Manager Collections ───────────────────────────────────
        public IMongoCollection<Project> Projects =>
            _db.GetCollection<Project>("projects");

        public IMongoCollection<ProjectTask> Tasks =>
            _db.GetCollection<ProjectTask>("tasks");

        public IMongoCollection<Sprint> Sprints =>
            _db.GetCollection<Sprint>("sprints");

        public IMongoCollection<CodeReview> CodeReviews =>
            _db.GetCollection<CodeReview>("code_reviews");

        public IMongoCollection<QaResult> QaResults =>
            _db.GetCollection<QaResult>("qa_results");

        public IMongoCollection<StandupLog> StandupLogs =>
            _db.GetCollection<StandupLog>("standup_logs");

        public IMongoCollection<ProjectEmbedding> ProjectEmbeddings =>
            _db.GetCollection<ProjectEmbedding>("project_embeddings");

        // ── Index creation ───────────────────────────────────────────────────────
        private async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
        {
            await Organizations.Indexes.CreateOneAsync(new CreateIndexModel<Organization>(
                Builders<Organization>.IndexKeys.Ascending(o => o.Code),
                new CreateIndexOptions { Unique = true, Background = true })).ConfigureAwait(false);

            await Users.Indexes.CreateOneAsync(new CreateIndexModel<User>(
                Builders<User>.IndexKeys
                    .Ascending(u => u.OrganizationId)
                    .Ascending(u => u.Username),
                new CreateIndexOptions { Unique = true, Background = true })).ConfigureAwait(false);

            await ActivityLogs.Indexes.CreateOneAsync(new CreateIndexModel<ActivityLog>(
                Builders<ActivityLog>.IndexKeys
                    .Ascending(x => x.OrganizationId)
                    .Ascending(x => x.UserId)
                    .Ascending(x => x.StartTime),
                new CreateIndexOptions { Background = true })).ConfigureAwait(false);

            await AppUsages.Indexes.CreateOneAsync(new CreateIndexModel<AppUsage>(
                Builders<AppUsage>.IndexKeys
                    .Ascending(x => x.OrganizationId)
                    .Ascending(x => x.UserId)
                    .Ascending(x => x.RecordedAt),
                new CreateIndexOptions { Background = true })).ConfigureAwait(false);

            await ScreenshotRecords.Indexes.CreateOneAsync(new CreateIndexModel<ScreenshotRecord>(
                Builders<ScreenshotRecord>.IndexKeys
                    .Ascending(x => x.OrganizationId)
                    .Ascending(x => x.UserId)
                    .Ascending(x => x.TakenAt),
                new CreateIndexOptions { Background = true })).ConfigureAwait(false);

            await Alerts.Indexes.CreateOneAsync(new CreateIndexModel<Alert>(
                Builders<Alert>.IndexKeys
                    .Ascending(x => x.OrganizationId)
                    .Ascending(x => x.UserId)
                    .Ascending(x => x.CreatedAt),
                new CreateIndexOptions { Background = true })).ConfigureAwait(false);

            await AppCategoryRules.Indexes.CreateOneAsync(new CreateIndexModel<AppCategoryRule>(
                Builders<AppCategoryRule>.IndexKeys
                    .Ascending(x => x.OrganizationId)
                    .Descending(x => x.Priority),
                new CreateIndexOptions { Background = true })).ConfigureAwait(false);

            await SystemSettings.Indexes.CreateOneAsync(new CreateIndexModel<SystemSettings>(
                Builders<SystemSettings>.IndexKeys.Ascending(x => x.OrganizationId),
                new CreateIndexOptions { Unique = true, Background = true })).ConfigureAwait(false);

            await AuditLogs.Indexes.CreateOneAsync(new CreateIndexModel<AuditLog>(
                Builders<AuditLog>.IndexKeys
                    .Ascending(x => x.OrganizationId)
                    .Descending(x => x.Timestamp),
                new CreateIndexOptions { Background = true })).ConfigureAwait(false);

            // ── AI Engineering Manager indexes ────────────────────────────────────
            await Projects.Indexes.CreateOneAsync(new CreateIndexModel<Project>(
                Builders<Project>.IndexKeys.Ascending(x => x.OrganizationId),
                new CreateIndexOptions { Background = true })).ConfigureAwait(false);

            await Tasks.Indexes.CreateManyAsync(new[]
            {
                new CreateIndexModel<ProjectTask>(
                    Builders<ProjectTask>.IndexKeys
                        .Ascending(x => x.OrganizationId)
                        .Ascending(x => x.ProjectId)
                        .Ascending(x => x.Status),
                    new CreateIndexOptions { Background = true }),
                new CreateIndexModel<ProjectTask>(
                    Builders<ProjectTask>.IndexKeys
                        .Ascending(x => x.OrganizationId)
                        .Ascending(x => x.AssignedToUserId)
                        .Ascending(x => x.Status),
                    new CreateIndexOptions { Background = true }),
                new CreateIndexModel<ProjectTask>(
                    Builders<ProjectTask>.IndexKeys
                        .Ascending(x => x.ProjectId)
                        .Ascending(x => x.SprintId),
                    new CreateIndexOptions { Background = true })
            }).ConfigureAwait(false);

            await Sprints.Indexes.CreateOneAsync(new CreateIndexModel<Sprint>(
                Builders<Sprint>.IndexKeys
                    .Ascending(x => x.ProjectId)
                    .Descending(x => x.StartDate),
                new CreateIndexOptions { Background = true })).ConfigureAwait(false);

            await CodeReviews.Indexes.CreateManyAsync(new[]
            {
                new CreateIndexModel<CodeReview>(
                    Builders<CodeReview>.IndexKeys
                        .Ascending(x => x.ProjectId)
                        .Ascending(x => x.CommitSha),
                    new CreateIndexOptions { Unique = true, Background = true }),
                new CreateIndexModel<CodeReview>(
                    Builders<CodeReview>.IndexKeys
                        .Ascending(x => x.TaskId),
                    new CreateIndexOptions { Background = true })
            }).ConfigureAwait(false);

            await StandupLogs.Indexes.CreateOneAsync(new CreateIndexModel<StandupLog>(
                Builders<StandupLog>.IndexKeys
                    .Ascending(x => x.OrganizationId)
                    .Ascending(x => x.UserId)
                    .Descending(x => x.Date),
                new CreateIndexOptions { Background = true })).ConfigureAwait(false);

            await ProjectEmbeddings.Indexes.CreateManyAsync(new[]
            {
                new CreateIndexModel<ProjectEmbedding>(
                    Builders<ProjectEmbedding>.IndexKeys
                        .Ascending(x => x.ProjectId)
                        .Ascending(x => x.ChunkType),
                    new CreateIndexOptions { Background = true }),
                new CreateIndexModel<ProjectEmbedding>(
                    Builders<ProjectEmbedding>.IndexKeys
                        .Ascending(x => x.ProjectId)
                        .Ascending(x => x.SourcePath),
                    new CreateIndexOptions { Background = true })
            }).ConfigureAwait(false);
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
                        Debug.WriteLine("MongoDbContext: Max attempts reached; giving up on index creation.");
                        return;
                    }

                    try { await Task.Delay(delay).ConfigureAwait(false); }
                    catch (TaskCanceledException) { return; }

                    delay = TimeSpan.FromTicks(Math.Min(delay.Ticks * 2, TimeSpan.FromMinutes(5).Ticks));
                }
            }
        }
    }
}
