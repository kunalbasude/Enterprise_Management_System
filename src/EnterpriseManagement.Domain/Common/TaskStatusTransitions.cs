using EnterpriseManagement.Domain.Enums;

namespace EnterpriseManagement.Domain.Common;

/// <summary>
/// The allowed task status transitions.
/// </summary>
/// <remarks>
/// <para>
/// Lives in Domain because it is a business rule, not a persistence or HTTP
/// concern — the same rule holds whether the change arrives over REST, from a
/// background job, or from a bulk import.
/// </para>
/// <para>
/// A table of allowed moves rather than a chain of if-statements: the rules stay
/// readable as a whole, a new status is one entry rather than a hunt for every
/// branch that needs updating, and the rules can be tested exhaustively by
/// iterating every pair.
/// </para>
/// </remarks>
public static class TaskStatusTransitions
{
    private static readonly IReadOnlyDictionary<TaskItemStatus, TaskItemStatus[]> Allowed =
        new Dictionary<TaskItemStatus, TaskItemStatus[]>
        {
            // Work must be started before it can be finished. This blocks the
            // common slip of marking a task Done when the intent was to assign
            // it, and keeps "who worked on this" answerable.
            [TaskItemStatus.Todo] =
            [
                TaskItemStatus.InProgress,
                TaskItemStatus.Cancelled
            ],

            // Done is reachable directly: not every task warrants review, and
            // forcing a pointless InReview hop teaches people to game the flow.
            [TaskItemStatus.InProgress] =
            [
                TaskItemStatus.Todo,
                TaskItemStatus.InReview,
                TaskItemStatus.Done,
                TaskItemStatus.Cancelled
            ],

            // Review either passes or sends the work back.
            [TaskItemStatus.InReview] =
            [
                TaskItemStatus.InProgress,
                TaskItemStatus.Done,
                TaskItemStatus.Cancelled
            ],

            // Reopening is normal and must stay possible. It is a move back to
            // InProgress specifically, so a reopened task is visibly active
            // rather than silently sitting in the backlog again.
            [TaskItemStatus.Done] =
            [
                TaskItemStatus.InProgress
            ],

            // Terminal. A cancelled task quietly resuming is how work gets done
            // that somebody explicitly decided to stop; reinstating it should
            // mean creating a new task, which leaves a record of the decision.
            [TaskItemStatus.Cancelled] = []
        };

    /// <summary>Whether a task may move directly from one status to another.</summary>
    /// <remarks>
    /// A move to the same status is permitted, so saving a task without changing
    /// its status is not an error.
    /// </remarks>
    public static bool IsAllowed(TaskItemStatus from, TaskItemStatus to) =>
        from == to || (Allowed.TryGetValue(from, out var targets) && targets.Contains(to));

    /// <summary>The statuses reachable from <paramref name="from"/>, for error messages and UI.</summary>
    public static IReadOnlyList<TaskItemStatus> AllowedFrom(TaskItemStatus from) =>
        Allowed.TryGetValue(from, out var targets) ? targets : [];
}
