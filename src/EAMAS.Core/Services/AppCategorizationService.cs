using EAMAS.Core.Data;
using EAMAS.Core.Enums;
using EAMAS.Core.Models;
using MongoDB.Driver;

namespace EAMAS.Core.Services
{
    public class AppCategorizationService
    {
        private readonly MongoDbContext _db;

        private static readonly Dictionary<string, ActivityCategory> _builtinRules =
            new(StringComparer.OrdinalIgnoreCase)
        {
            // Productive
            ["devenv"] = ActivityCategory.Productive,
            ["code"] = ActivityCategory.Productive,
            ["rider"] = ActivityCategory.Productive,
            ["idea"] = ActivityCategory.Productive,
            ["pycharm"] = ActivityCategory.Productive,
            ["webstorm"] = ActivityCategory.Productive,
            ["clion"] = ActivityCategory.Productive,
            ["android studio"] = ActivityCategory.Productive,
            ["visual studio"] = ActivityCategory.Productive,
            ["notepad++"] = ActivityCategory.Productive,
            ["sublime_text"] = ActivityCategory.Productive,
            ["atom"] = ActivityCategory.Productive,
            ["eclipse"] = ActivityCategory.Productive,
            ["netbeans"] = ActivityCategory.Productive,
            ["excel"] = ActivityCategory.Productive,
            ["winword"] = ActivityCategory.Productive,
            ["powerpnt"] = ActivityCategory.Productive,
            ["onenote"] = ActivityCategory.Productive,
            ["outlook"] = ActivityCategory.Productive,
            ["teams"] = ActivityCategory.Productive,
            ["slack"] = ActivityCategory.Productive,
            ["zoom"] = ActivityCategory.Productive,
            ["postman"] = ActivityCategory.Productive,
            ["insomnia"] = ActivityCategory.Productive,
            ["dbeaver"] = ActivityCategory.Productive,
            ["ssms"] = ActivityCategory.Productive,
            ["cmd"] = ActivityCategory.Productive,
            ["powershell"] = ActivityCategory.Productive,
            ["wt"] = ActivityCategory.Productive,
            ["git"] = ActivityCategory.Productive,
            ["github desktop"] = ActivityCategory.Productive,
            ["sourcetree"] = ActivityCategory.Productive,
            ["figma"] = ActivityCategory.Productive,
            ["adobe"] = ActivityCategory.Productive,
            ["blender"] = ActivityCategory.Productive,
            // Distracting
            ["youtube"] = ActivityCategory.Distracting,
            ["netflix"] = ActivityCategory.Distracting,
            ["amazon prime"] = ActivityCategory.Distracting,
            ["disney+"] = ActivityCategory.Distracting,
            ["twitch"] = ActivityCategory.Distracting,
            ["tiktok"] = ActivityCategory.Distracting,
            ["instagram"] = ActivityCategory.Distracting,
            ["facebook"] = ActivityCategory.Distracting,
            ["twitter"] = ActivityCategory.Distracting,
            ["reddit"] = ActivityCategory.Distracting,
            ["steam"] = ActivityCategory.Distracting,
            ["epicgames"] = ActivityCategory.Distracting,
            ["leagueoflegends"] = ActivityCategory.Distracting,
            ["valorant"] = ActivityCategory.Distracting,
            ["vlc"] = ActivityCategory.Distracting,
            // Neutral
            ["spotify"] = ActivityCategory.Neutral,
        };

        public AppCategorizationService(MongoDbContext db)
        {
            _db = db;
        }

        // ── Per-org custom rule cache (avoids a DB query on every poll cycle) ──────────────────
        private readonly Dictionary<string, (List<AppCategoryRule> Rules, DateTime ExpiresAt)> _ruleCache = new();
        private readonly object _cacheLock = new();
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

        private List<AppCategoryRule> GetCachedRules(string orgId)
        {
            lock (_cacheLock)
            {
                if (_ruleCache.TryGetValue(orgId, out var cached) && cached.ExpiresAt > DateTime.UtcNow)
                    return cached.Rules;

                var rules = _db.AppCategoryRules
                    .Find(r => r.OrganizationId == orgId && r.IsActive)
                    .SortByDescending(r => r.Priority)
                    .ToList();

                _ruleCache[orgId] = (rules, DateTime.UtcNow + CacheTtl);
                return rules;
            }
        }

        public ActivityCategory Categorize(string orgId, string processName, string windowTitle)
        {
            // Org-specific custom rules first (higher priority), loaded from cache
            var rules = GetCachedRules(orgId);

            foreach (var rule in rules)
            {
                var kw = rule.Keyword.ToLowerInvariant();
                if (rule.MatchProcessName && processName.ToLowerInvariant().Contains(kw))
                    return rule.Category;
                if (rule.MatchWindowTitle && windowTitle.ToLowerInvariant().Contains(kw))
                    return rule.Category;
            }

            // Built-in rules
            var pn = processName.ToLowerInvariant();
            var wt = windowTitle.ToLowerInvariant();
            foreach (var (keyword, category) in _builtinRules)
            {
                var kw = keyword.ToLowerInvariant();
                if (pn.Contains(kw) || wt.Contains(kw))
                    return category;
            }

            return ActivityCategory.Neutral;
        }

        /// <summary>Invalidates the cached rules for an org, e.g. after an admin adds/edits a rule.</summary>
        public void InvalidateCache(string orgId)
        {
            lock (_cacheLock)
            {
                _ruleCache.Remove(orgId);
            }
        }

        public List<AppCategoryRule> GetCustomRules(string orgId)
        {
            return _db.AppCategoryRules
                .Find(r => r.OrganizationId == orgId)
                .SortByDescending(r => r.Priority)
                .ToList();
        }

        public void AddRule(AppCategoryRule rule)
        {
            _db.AppCategoryRules.InsertOne(rule);
        }

        public void UpdateRule(AppCategoryRule rule)
        {
            _db.AppCategoryRules.ReplaceOne(r => r.Id == rule.Id, rule);
        }

        public void DeleteRule(string ruleId)
        {
            _db.AppCategoryRules.DeleteOne(r => r.Id == ruleId);
        }
    }
}
