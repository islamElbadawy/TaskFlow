import { TaskListItem, TaskStatus } from '../api/models';

export type BoardColumns = Record<TaskStatus, TaskListItem[]>;

export function emptyBoardColumns(): BoardColumns {
  return { Todo: [], InProgress: [], Done: [] };
}

export function groupByStatus(items: readonly TaskListItem[]): BoardColumns {
  const cols = emptyBoardColumns();
  for (const item of items) {
    cols[item.status].push(item);
  }
  return cols;
}

/**
 * Given the target column's items (excluding the moved task) and the target insertion slot index,
 * returns the ID of the task immediately below the slot, or null if inserting at the end.
 */
export function beforeTaskIdFor(
  targetColumnWithoutMoved: readonly TaskListItem[],
  slotIndex: number,
): string | null {
  if (slotIndex < 0 || slotIndex >= targetColumnWithoutMoved.length) {
    return null;
  }
  return targetColumnWithoutMoved[slotIndex].id;
}

/** Returns the beforeTaskId needed to undo moving a task back to its original column and index. */
export function findUndoBeforeTaskId(
  originalColumn: readonly TaskListItem[],
  taskId: string,
): string | null {
  const index = originalColumn.findIndex((t) => t.id === taskId);
  if (index === -1 || index + 1 >= originalColumn.length) {
    return null;
  }
  return originalColumn[index + 1].id;
}

/** Optimistically moves a task across or within columns without mutating the input record. */
export function moveTaskOptimistic(
  columns: BoardColumns,
  taskId: string,
  targetStatus: TaskStatus,
  beforeTaskId: string | null,
): BoardColumns {
  let moved: TaskListItem | undefined;
  const next: BoardColumns = {
    Todo: [],
    InProgress: [],
    Done: [],
  };

  for (const status of ['Todo', 'InProgress', 'Done'] as const) {
    for (const item of columns[status]) {
      if (item.id === taskId) {
        moved = { ...item, status: targetStatus };
      } else {
        next[status].push(item);
      }
    }
  }

  if (!moved) return columns;

  const dest = next[targetStatus];
  const insertAt = beforeTaskId ? dest.findIndex((t) => t.id === beforeTaskId) : -1;
  if (insertAt === -1) {
    dest.push(moved);
  } else {
    dest.splice(insertAt, 0, moved);
  }

  return next;
}
