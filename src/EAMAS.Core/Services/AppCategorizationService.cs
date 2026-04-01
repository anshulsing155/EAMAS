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

        public ActivityCategory Categorize(string orgId, string processName, string windowTitle)
        {
            // Org-specific custom rules first (higher priority)
            var rules = _db.AppCategoryRules
                .Find(r => r.OrganizationId == orgId && r.IsActive)
                .SortByDescending(r => r.Priority)
                .ToList();

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
