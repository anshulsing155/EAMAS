using EAMAS.Core.Data;
using EAMAS.Core.Enums;
using EAMAS.Core.Models;
using MongoDB.Driver;

namespace EAMAS.Core.Services
{
    public class SprintService
    {
        private readonly MongoDbContext _db;
        private readonly TaskService _tasks;

        public SprintService(MongoDbContext db, TaskService tasks)
        {
            _db = db;
            _tasks = tasks;
        }

        public List<Sprint> GetByProject(string projectId)
            => _db.Sprints.Find(s => s.ProjectId == projectId)
                          .SortByDescending(s => s.StartDate).ToList();

        public Sprint? GetActive(string projectId)
            => _db.Sprints.Find(s => s.ProjectId == projectId && s.Status == SprintStatus.Active)
                          .FirstOrDefault();

        public Sprint? GetById(string id)
            => _db.Sprints.Find(s => s.Id == id).FirstOrDefault();

        public Sprint Create(Sprint sprint)
        {
            sprint.CreatedAt = DateTime.UtcNow;
            _db.Sprints.InsertOne(sprint);
            return sprint;
        }

        public void Update(Sprint sprint)
            => _db.Sprints.ReplaceOne(s => s.Id == sprint.Id, sprint);

        public void Activate(string sprintId)
        {
            var sprint = GetById(sprintId);
            if (sprint == null) return;

            // Only one active sprint per project at a time
            _db.Sprints.UpdateMany(
                s => s.ProjectId == sprint.ProjectId && s.Status == SprintStatus.Active,
                Builders<Sprint>.Update.Set(s => s.Status, SprintStatus.Completed));

            _db.Sprints.UpdateOne(s => s.Id == sprintId,
                Builders<Sprint>.Update.Set(s => s.Status, SprintStatus.Active));

            // Move sprint tasks to Todo
            _tasks.AssignToSprint(sprint.TaskIds, sprintId);
        }

        public void Complete(string sprintId, string aiSummary = "")
        {
            var sprint = GetById(sprintId);
            if (sprint == null) return;

            // Calculate actual velocity
            var doneTasks = sprint.TaskIds
                .Select(id => _tasks.GetById(id))
                .Where(t => t?.Status == ProjectTaskStatus.Done)
                .ToList();

            double actualVelocity = doneTasks.Sum(t => t?.ActualHours ?? t?.EstimatedHours ?? 0);

            _db.Sprints.UpdateOne(s => s.Id == sprintId,
                Builders<Sprint>.Update
                    .Set(s => s.Status, SprintStatus.Completed)
                    .Set(s => s.ActualVelocity, actualVelocity)
                    .Set(s => s.AiSprintSummary, aiSummary));

            // Move unfinished tasks back to Backlog
            var unfinished = sprint.TaskIds
                .Select(id => _tasks.GetById(id))
                .Where(t => t != null && t.Status != ProjectTaskStatus.Done)
                .Select(t => t!.Id)
                .ToList();

            if (unfinished.Any())
            {
                _db.Tasks.UpdateMany(
                    Builders<ProjectTask>.Filter.In(t => t.Id, unfinished),
                    Builders<ProjectTask>.Update
                        .Set(t => t.SprintId, string.Empty)
                        .Set(t => t.Status, ProjectTaskStatus.Backlog)
                        .Set(t => t.UpdatedAt, DateTime.UtcNow));
            }
        }

        public double GetAverageVelocity(string projectId, int lastN = 3)
        {
            var completed = _db.Sprints
                .Find(s => s.ProjectId == projectId && s.Status == SprintStatus.Completed)
                .SortByDescending(s => s.EndDate)
                .Limit(lastN)
                .ToList();

            if (!completed.Any()) return 0;
            return completed.Average(s => s.ActualVelocity > 0 ? s.ActualVelocity : s.PlannedVelocity);
        }
    }
}
