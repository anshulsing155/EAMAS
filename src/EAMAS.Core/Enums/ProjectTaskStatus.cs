namespace EAMAS.Core.Enums
{
    public enum ProjectTaskStatus
    {
        Backlog = 0,
        Todo = 1,
        InProgress = 2,
        CodeReview = 3,
        QATesting = 4,
        NeedsFix = 5,
        Done = 6
    }

    public enum TaskPriority { Low = 0, Medium = 1, High = 2, Critical = 3 }

    public enum SprintStatus { Planning, Active, Completed }

    public enum CodeReviewStatus { Pending, InProgress, Passed, Failed, NeedsHumanReview }

    public enum QaRunStatus { Queued, Running, Passed, Failed }
}
