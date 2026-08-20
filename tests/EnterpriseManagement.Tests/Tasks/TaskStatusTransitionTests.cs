using EnterpriseManagement.Domain.Common;
using EnterpriseManagement.Domain.Enums;

namespace EnterpriseManagement.Tests.Tasks;

/// <summary>
/// Exhaustive coverage of the task workflow.
/// </summary>
/// <remarks>
/// With five statuses there are only 25 ordered pairs, so every one is asserted
/// rather than a sample. A state machine tested by example tends to grow
/// accidental transitions that nobody notices until a task takes an impossible
/// route through the workflow.
/// </remarks>
public class TaskStatusTransitionTests
{
    private static readonly TaskItemStatus[] All = Enum.GetValues<TaskItemStatus>();

    /// <summary>The complete expected transition table, written out independently of the implementation.</summary>
    private static readonly Dictionary<TaskItemStatus, TaskItemStatus[]> Expected = new()
    {
        [TaskItemStatus.Todo] = [TaskItemStatus.InProgress, TaskItemStatus.Cancelled],
        [TaskItemStatus.InProgress] = [TaskItemStatus.Todo, TaskItemStatus.InReview, TaskItemStatus.Done, TaskItemStatus.Cancelled],
        [TaskItemStatus.InReview] = [TaskItemStatus.InProgress, TaskItemStatus.Done, TaskItemStatus.Cancelled],
        [TaskItemStatus.Done] = [TaskItemStatus.InProgress],
        [TaskItemStatus.Cancelled] = []
    };

    [Fact]
    public void Every_status_pair_matches_the_expected_table()
    {
        var mismatches = new List<string>();

        foreach (var from in All)
        {
            foreach (var to in All)
            {
                // Same-status moves are always allowed, so saving a task without
                // changing its status is never an error.
                var expected = from == to || Expected[from].Contains(to);
                var actual = TaskStatusTransitions.IsAllowed(from, to);

                if (expected != actual)
                {
                    mismatches.Add($"{from} -> {to}: expected {expected}, got {actual}");
                }
            }
        }

        Assert.True(mismatches.Count == 0, string.Join(Environment.NewLine, mismatches));
    }

    [Theory]
    [InlineData(TaskItemStatus.InProgress)]
    [InlineData(TaskItemStatus.InReview)]
    [InlineData(TaskItemStatus.Done)]
    [InlineData(TaskItemStatus.Todo)]
    public void Cancelled_is_terminal(TaskItemStatus target)
    {
        // A cancelled task quietly resuming is how work gets done that somebody
        // explicitly decided to stop. Reinstating it means creating a new task,
        // which leaves a record of the decision.
        Assert.False(TaskStatusTransitions.IsAllowed(TaskItemStatus.Cancelled, target));
    }

    [Fact]
    public void Todo_cannot_jump_straight_to_done()
    {
        // Catches the common slip of completing a task when the intent was to
        // assign it, and keeps "who worked on this" answerable.
        Assert.False(TaskStatusTransitions.IsAllowed(TaskItemStatus.Todo, TaskItemStatus.Done));
    }

    [Fact]
    public void Done_can_be_reopened_to_in_progress()
    {
        // Reopening is normal and must stay possible.
        Assert.True(TaskStatusTransitions.IsAllowed(TaskItemStatus.Done, TaskItemStatus.InProgress));
    }

    [Fact]
    public void Done_cannot_be_reopened_straight_into_the_backlog()
    {
        // Reopened work should be visibly active, not silently back in Todo.
        Assert.False(TaskStatusTransitions.IsAllowed(TaskItemStatus.Done, TaskItemStatus.Todo));
    }

    [Fact]
    public void In_progress_may_reach_done_without_review()
    {
        // Not every task warrants review, and forcing a pointless hop teaches
        // people to game the workflow.
        Assert.True(TaskStatusTransitions.IsAllowed(TaskItemStatus.InProgress, TaskItemStatus.Done));
    }

    [Fact]
    public void Review_can_send_work_back()
    {
        Assert.True(TaskStatusTransitions.IsAllowed(TaskItemStatus.InReview, TaskItemStatus.InProgress));
    }

    [Theory]
    [InlineData(TaskItemStatus.Todo)]
    [InlineData(TaskItemStatus.InProgress)]
    [InlineData(TaskItemStatus.InReview)]
    [InlineData(TaskItemStatus.Done)]
    [InlineData(TaskItemStatus.Cancelled)]
    public void A_status_can_always_be_set_to_itself(TaskItemStatus status)
    {
        Assert.True(TaskStatusTransitions.IsAllowed(status, status));
    }

    [Theory]
    [InlineData(TaskItemStatus.Todo)]
    [InlineData(TaskItemStatus.InProgress)]
    [InlineData(TaskItemStatus.InReview)]
    public void Unfinished_work_can_always_be_cancelled(TaskItemStatus from)
    {
        Assert.True(TaskStatusTransitions.IsAllowed(from, TaskItemStatus.Cancelled));
    }

    [Fact]
    public void Finished_work_cannot_be_cancelled()
    {
        // Deliberate, and worth stating: cancelling something already completed
        // is meaningless. Undoing it means reopening first (Done -> InProgress)
        // and then cancelling, which records both decisions rather than
        // collapsing them into one ambiguous jump.
        Assert.False(TaskStatusTransitions.IsAllowed(TaskItemStatus.Done, TaskItemStatus.Cancelled));
    }

    [Fact]
    public void Every_status_is_covered_by_the_table()
    {
        // Guards against adding an enum member and forgetting to define its
        // transitions, which would silently make it terminal.
        foreach (var status in All)
        {
            Assert.True(
                Expected.ContainsKey(status),
                $"{status} has no defined transitions. Add it to TaskStatusTransitions.");
        }
    }

    [Fact]
    public void AllowedFrom_reports_the_reachable_statuses_for_error_messages()
    {
        var fromTodo = TaskStatusTransitions.AllowedFrom(TaskItemStatus.Todo);

        Assert.Equal(2, fromTodo.Count);
        Assert.Contains(TaskItemStatus.InProgress, fromTodo);
        Assert.Contains(TaskItemStatus.Cancelled, fromTodo);
        Assert.Empty(TaskStatusTransitions.AllowedFrom(TaskItemStatus.Cancelled));
    }
}
