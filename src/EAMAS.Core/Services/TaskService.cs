using EAMAS.Core.Data;
using EAMAS.Core.Enums;
using EAMAS.Core.Models;
using MongoDB.Driver;

namespace EAMAS.Core.Services
{
    public class TaskService
    {
        private readonly MongoDbContext _db;

        public TaskService(MongoDbContext db) => _db = db;

        public List<ProjectTask> GetByProject(string projectId)
            => _db.Tasks.Find(t => t.ProjectId == projectId)
                        .SortBy(t => t.BoardPosition).ToList();

        public List<ProjectTask> GetBySprint(string sprintId)
            => _db.Tasks.Find(t => t.SprintId == sprintId)
                        .SortBy(t => t.BoardPosition).ToList();

        public List<ProjectTask> GetByUser(string orgId, string userId)
            => _db.Tasks.Find(t => t.OrganizationId == orgId && t.AssignedToUserId == userId)
                        .SortByDescending(t => t.UpdatedAt).ToList();

        public List<ProjectTask> GetByStatus(string projectId, ProjectTaskStatus status)
            => _db.Tasks.Find(t => t.ProjectId == projectId && t.Status == status)
                        .SortBy(t => t.BoardPosition).ToList();

        public List<ProjectTask> GetBacklog(string projectId)
            => _db.Tasks.Find(t => t.ProjectId == projectId
                            && t.Status == ProjectTaskStatus.Backlog
                            && string.IsNullOrEmpty(t.SprintId))
                        .SortBy(t => (int)t.Priority).ToList();

        public ProjectTask? GetById(string id)
            => _db.Tasks.Find(t => t.Id == id).FirstOrDefault();

        public ProjectTask Create(ProjectTask task)
        {
            task.CreatedAt = DateTime.UtcNow;
            task.UpdatedAt = DateTime.UtcNow;
            task.BoardPosition = GetNextPosition(task.ProjectId, task.Status);
            _db.Tasks.InsertOne(task);
            return task;
        }

        public void BulkCreate(List<ProjectTask> tasks)
        {
            foreach (var t in tasks)
            {
                t.CreatedAt = DateTime.UtcNow;
                t.UpdatedAt = DateTime.UtcNow;
            }
            if (tasks.Any()) _db.Tasks.InsertMany(tasks);
        }

        public void Update(ProjectTask task)
        {
            task.UpdatedAt = DateTime.UtcNow;
            _db.Tasks.ReplaceOne(t => t.Id == task.Id, task);
        }

        public void MoveStatus(string taskId, ProjectTaskStatus newStatus)
        {
            var now = DateTime.UtcNow;
            var update = Builders<ProjectTask>.Update
                .Set(t => t.Status, newStatus)
                .Set(t => t.UpdatedAt, now);

            if (newStatus == ProjectTaskStatus.InProgress)
                update = update.Set(t => t.StartedAt, now);
            if (newStatus == ProjectTaskStatus.Done)
                update = update.Set(t => t.CompletedAt, now);

            _db.Tasks.UpdateOne(t => t.Id == taskId, update);
        }

        public void Assign(string taskId, string userId, string userName)
            => _db.Tasks.UpdateOne(t => t.Id == taskId,
                Builders<ProjectTask>.Update
                    .Set(t => t.AssignedToUserId, userId)
                    .Set(t => t.AssignedToUserName, userName)
                    .Set(t => t.UpdatedAt, DateTime.UtcNow));

        public void LinkCommit(string taskId, string commitSha)
            => _db.Tasks.UpdateOne(t => t.Id == taskId,
                Builders<ProjectTask>.Update
                    .Set(t => t.RelatedCommitSha, commitSha)
                    .Set(t => t.UpdatedAt, DateTime.UtcNow));

        public void AssignToSprint(List<string> taskIds, string sprintId)
        {
            if (!taskIds.Any()) return;
            _db.Tasks.UpdateMany(
                Builders<ProjectTask>.Filter.In(t => t.Id, taskIds),
                Builders<ProjectTask>.Update
                    .Set(t => t.SprintId, sprintId)
                    .Set(t => t.Status, ProjectTaskStatus.Todo)
                    .Set(t => t.UpdatedAt, DateTime.UtcNow));
        }

        public void Delete(string id)
            => _db.Tasks.DeleteOne(t => t.Id == id);

        public int CountByStatus(string projectId, ProjectTaskStatus status)
            => (int)_db.Tasks.CountDocuments(t => t.ProjectId == projectId && t.Status == status);

        public List<ProjectTask> GetCompletedYesterday(string projectId, string userId)
        {
            var yesterday = DateTime.UtcNow.Date.AddDays(-1);
            var today = DateTime.UtcNow.Date;
            return _db.Tasks.Find(t => t.ProjectId == projectId
                                    && t.AssignedToUserId == userId
                                    && t.Status == ProjectTaskStatus.Done
                                    && t.CompletedAt >= yesterday
                                    && t.CompletedAt < today).ToList();
        }

        public List<ProjectTask> GetInProgress(string projectId, string userId)
            => _db.Tasks.Find(t => t.ProjectId == projectId
                                && t.AssignedToUserId == userId
                                && t.Status == ProjectTaskStatus.InProgress).ToList();

        private int GetNextPosition(string projectId, ProjectTaskStatus status)
        {
            var last = _db.Tasks
                .Find(t => t.ProjectId == projectId && t.Status == status)
                .SortByDescending(t => t.BoardPosition)
                .Limit(1)
                .FirstOrDefault();
            return (last?.BoardPosition ?? -1) + 1;
        }
    }
}
