namespace EnterpriseManagement.Domain.Enums;

/// <summary>
/// Workflow state of a task.
/// </summary>
/// <remarks>
/// Named <c>TaskItemStatus</c>, not <c>TaskStatus</c>: implicit usings import
/// <c>System.Threading.Tasks</c>, which already defines <c>TaskStatus</c>.
/// </remarks>
public enum TaskItemStatus
{
    Todo = 0,
    InProgress = 1,
    InReview = 2,
    Done = 3,
    Cancelled = 4
}
