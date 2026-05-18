using EAMAS.Core.Data;
using EAMAS.Core.Enums;
using EAMAS.Core.Models;
using MongoDB.Driver;
using System.Runtime.Versioning;

namespace EAMAS.Core.Services
{
    [SupportedOSPlatform("windows")]
    public class ProjectService
    {
        private readonly MongoDbContext _db;
        private readonly EncryptionService _enc;

        public ProjectService(MongoDbContext db, EncryptionService enc)
        {
            _db = db;
            _enc = enc;
        }

        public List<Project> GetAll(string orgId)
            => _db.Projects.Find(p => p.OrganizationId == orgId && p.IsActive)
                           .SortBy(p => p.Name).ToList();

        public Project? GetById(string id)
            => _db.Projects.Find(p => p.Id == id).FirstOrDefault();

        public Project Create(Project project)
        {
            project.CreatedAt = DateTime.UtcNow;
            EncryptSecrets(project);
            _db.Projects.InsertOne(project);
            return project;
        }

        public void Update(Project project)
        {
            EncryptSecrets(project);
            _db.Projects.ReplaceOne(p => p.Id == project.Id, project);
        }

        public void Deactivate(string id)
            => _db.Projects.UpdateOne(p => p.Id == id,
                Builders<Project>.Update.Set(p => p.IsActive, false));

        public void UpdateLastSync(string id, string lastCommitSha)
            => _db.Projects.UpdateOne(p => p.Id == id,
                Builders<Project>.Update
                    .Set(p => p.LastSyncedAt, DateTime.UtcNow)
                    .Set(p => p.LastKnownCommitSha, lastCommitSha));

        /// <summary>Returns a copy with decrypted secrets for runtime use.</summary>
        public Project DecryptSecrets(Project project)
        {
            var copy = Clone(project);
            copy.AiApiKey = _enc.Decrypt(copy.AiApiKey);
            copy.GitHubAccessToken = _enc.Decrypt(copy.GitHubAccessToken);
            copy.WebhookSecret = _enc.Decrypt(copy.WebhookSecret);
            return copy;
        }

        private void EncryptSecrets(Project p)
        {
            if (!string.IsNullOrEmpty(p.AiApiKey) && !IsAlreadyEncrypted(p.AiApiKey))
                p.AiApiKey = _enc.Encrypt(p.AiApiKey);
            if (!string.IsNullOrEmpty(p.GitHubAccessToken) && !IsAlreadyEncrypted(p.GitHubAccessToken))
                p.GitHubAccessToken = _enc.Encrypt(p.GitHubAccessToken);
            if (!string.IsNullOrEmpty(p.WebhookSecret) && !IsAlreadyEncrypted(p.WebhookSecret))
                p.WebhookSecret = _enc.Encrypt(p.WebhookSecret);
        }

        // Base64 strings are much longer than raw keys — heuristic guard against double-encrypting
        private static bool IsAlreadyEncrypted(string val)
        {
            try { var b = Convert.FromBase64String(val); return b.Length > 28; }
            catch { return false; }
        }

        private static Project Clone(Project p) => new()
        {
            Id = p.Id, OrganizationId = p.OrganizationId, Name = p.Name,
            Description = p.Description, GitHubRepoOwner = p.GitHubRepoOwner,
            GitHubRepoName = p.GitHubRepoName, GitHubAccessToken = p.GitHubAccessToken,
            DefaultBranch = p.DefaultBranch, WebhookSecret = p.WebhookSecret,
            AiProvider = p.AiProvider, AiApiKey = p.AiApiKey, AiModel = p.AiModel,
            AiTemperature = p.AiTemperature, PrdContent = p.PrdContent,
            ArchitectureNotes = p.ArchitectureNotes, TechStack = p.TechStack,
            SprintDurationDays = p.SprintDurationDays, WorkHoursPerDay = p.WorkHoursPerDay,
            QaCommands = p.QaCommands, IsActive = p.IsActive, CreatedAt = p.CreatedAt,
            CreatedByUserId = p.CreatedByUserId, LastSyncedAt = p.LastSyncedAt,
            LastKnownCommitSha = p.LastKnownCommitSha
        };
    }
}
