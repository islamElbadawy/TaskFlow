import { TaskListItem } from '../api/models';
import {
  beforeTaskIdFor,
  findUndoBeforeTaskId,
  groupByStatus,
  moveTaskOptimistic,
} from './board-order';

const makeTask = (id: string, status: TaskListItem['status'] = 'Todo'): TaskListItem => ({
  id,
  title: `Task ${id}`,
  status,
  priority: 'Medium',
  dueDate: null,
  isOverdue: false,
  createdById: 'u1',
  createdByName: 'User',
  assignedToId: null,
  assignedToName: null,
  commentsCount: 0,
  createdAt: '2026-11-20T00:00:00Z',
  updatedAt: null,
  position: 0,
});

describe('board-order', () => {
  it('computes beforeTaskId for top, middle, and bottom slots', () => {
    const col = [makeTask('a'), makeTask('b'), makeTask('c')];
    expect(beforeTaskIdFor(col, 0)).toBe('a');
    expect(beforeTaskIdFor(col, 1)).toBe('b');
    expect(beforeTaskIdFor(col, 3)).toBeNull();
    expect(beforeTaskIdFor([], 0)).toBeNull();
  });

  it('finds undo beforeTaskId for original position', () => {
    const col = [makeTask('a'), makeTask('b'), makeTask('c')];
    expect(findUndoBeforeTaskId(col, 'a')).toBe('b');
    expect(findUndoBeforeTaskId(col, 'b')).toBe('c');
    expect(findUndoBeforeTaskId(col, 'c')).toBeNull();
  });

  it('moves tasks immutably across columns and within a column', () => {
    const cols = groupByStatus([
      makeTask('a', 'Todo'),
      makeTask('b', 'Todo'),
      makeTask('c', 'InProgress'),
    ]);

    const movedAcross = moveTaskOptimistic(cols, 'a', 'InProgress', 'c');
    expect(movedAcross.Todo.map((t) => t.id)).toEqual(['b']);
    expect(movedAcross.InProgress.map((t) => t.id)).toEqual(['a', 'c']);
    expect(movedAcross.InProgress[0].status).toBe('InProgress');
    // Original unchanged
    expect(cols.Todo.map((t) => t.id)).toEqual(['a', 'b']);

    const reordered = moveTaskOptimistic(movedAcross, 'c', 'InProgress', 'a');
    expect(reordered.InProgress.map((t) => t.id)).toEqual(['c', 'a']);
  });
});
