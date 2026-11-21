using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Interfaces;

namespace TaskFlow.Application.Commands.Tasks;

/// <summary>
/// Board order inside a status column. Positions only need to be ordered, not contiguous: a delete leaves a gap and
/// concurrent writes may leave ties, which read as (position, created_at, id). The next move in a column renumbers it.
/// </summary>
internal static class TaskPositions
{
    /// <summary>The position just after the last task in <paramref name="status"/> (0 for an empty column).</summary>
    public static async Task<int> NextPositionAsync(this IRepository<TaskItem> tasks, TaskItemStatus status,
        CancellationToken cancellationToken)
    {
        var last = await tasks.ListAsync(t => t.Status == status, orderBy: t => t.Position, descending: true,
            skip: 0, take: 1, selector: t => (int?)t.Position, cancellationToken);
        return (last.FirstOrDefault() ?? -1) + 1;
    }

    /// <summary>The column's current order, with the same tie-breaks the board reads use.</summary>
    public static List<TaskItem> InBoardOrder(this IEnumerable<TaskItem> column) =>
        column.OrderBy(t => t.Position).ThenBy(t => t.CreatedAt).ThenBy(t => t.Id).ToList();

    /// <summary>Numbers <paramref name="orderedColumn"/> 0..n-1. Unchanged positions aren't written.</summary>
    public static void Renumber(this IReadOnlyList<TaskItem> orderedColumn)
    {
        for (var i = 0; i < orderedColumn.Count; i++)
            orderedColumn[i].Position = i;
    }
}
