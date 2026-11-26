import { LiveAnnouncer } from '@angular/cdk/a11y';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Subscription, finalize, forkJoin } from 'rxjs';
import { BoardApi } from '../../core/api/board.api';
import { LIMITS } from '../../core/api/limits';
import { TASK_STATUSES, TaskListItem, TaskPriority, TaskStatus } from '../../core/api/models';
import { TaskQueryParams } from '../../core/api/tasks.api';
import {
  BoardColumns,
  emptyBoardColumns,
  findUndoBeforeTaskId,
  moveTaskOptimistic,
} from '../../core/domain/board-order';
import { ApiError } from '../../core/http/api-error';
import { TranslationService } from '../../core/i18n/translation.service';
import { ToastService } from '../../core/toast/toast.service';

export interface BoardFilters {
  priority?: TaskPriority;
  assignedToId?: string;
  unassigned?: boolean;
  createdById?: string;
  overdue?: boolean;
  search?: string;
}

interface ColumnMeta {
  page: number;
  totalCount: number;
  hasNextPage: boolean;
  loadingMore: boolean;
}

const initialMeta = (): Record<TaskStatus, ColumnMeta> => ({
  Todo: { page: 1, totalCount: 0, hasNextPage: false, loadingMore: false },
  InProgress: { page: 1, totalCount: 0, hasNextPage: false, loadingMore: false },
  Done: { page: 1, totalCount: 0, hasNextPage: false, loadingMore: false },
});

@Injectable()
export class BoardStore {
  private readonly boardApi = inject(BoardApi);
  private readonly toast = inject(ToastService);
  private readonly i18n = inject(TranslationService);
  private readonly announcer = inject(LiveAnnouncer);

  private readonly _columns = signal<BoardColumns>(emptyBoardColumns());
  private readonly _meta = signal<Record<TaskStatus, ColumnMeta>>(initialMeta());
  private readonly _filters = signal<BoardFilters>({});
  private readonly _loading = signal(false);
  private readonly _error = signal<string | null>(null);
  private readonly _landedTaskId = signal<string | null>(null);

  private loadSub: Subscription | null = null;

  readonly columns = this._columns.asReadonly();
  readonly meta = this._meta.asReadonly();
  readonly filters = this._filters.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();
  readonly landedTaskId = this._landedTaskId.asReadonly();

  readonly isEmpty = computed(() => {
    const c = this._columns();
    return c.Todo.length === 0 && c.InProgress.length === 0 && c.Done.length === 0;
  });

  readonly hasActiveFilters = computed(() => {
    const f = this._filters();
    return Boolean(
      f.priority || f.assignedToId || f.unassigned || f.createdById || f.overdue || f.search,
    );
  });

  load(filters: BoardFilters = this._filters(), silent = false): void {
    this.loadSub?.unsubscribe();
    this._filters.set(filters);
    if (!silent) {
      this._loading.set(true);
    }
    this._error.set(null);

    const query: Omit<TaskQueryParams, 'status' | 'sort'> = {
      ...filters,
      page: 1,
      pageSize: LIMITS.BOARD_PAGE_SIZE,
    };

    this.loadSub = forkJoin({
      todo: this.boardApi.loadColumn('Todo', query),
      inProgress: this.boardApi.loadColumn('InProgress', query),
      done: this.boardApi.loadColumn('Done', query),
    })
      .pipe(finalize(() => this._loading.set(false)))
      .subscribe({
        next: ({ todo, inProgress, done }) => {
          this._columns.set({
            Todo: todo.items,
            InProgress: inProgress.items,
            Done: done.items,
          });
          this._meta.set({
            Todo: {
              page: todo.pageNumber,
              totalCount: todo.totalCount,
              hasNextPage: todo.hasNextPage,
              loadingMore: false,
            },
            InProgress: {
              page: inProgress.pageNumber,
              totalCount: inProgress.totalCount,
              hasNextPage: inProgress.hasNextPage,
              loadingMore: false,
            },
            Done: {
              page: done.pageNumber,
              totalCount: done.totalCount,
              hasNextPage: done.hasNextPage,
              loadingMore: false,
            },
          });
        },
        error: (err: unknown) => {
          this._error.set(err instanceof ApiError ? err.detail : this.i18n.t('errorBody'));
        },
      });
  }

  loadMore(status: TaskStatus): void {
    const colMeta = this._meta()[status];
    if (!colMeta.hasNextPage || colMeta.loadingMore) return;

    this._meta.update((m) => ({
      ...m,
      [status]: { ...m[status], loadingMore: true },
    }));

    this.boardApi
      .loadColumn(status, {
        ...this._filters(),
        page: colMeta.page + 1,
        pageSize: LIMITS.BOARD_PAGE_SIZE,
      })
      .subscribe({
        next: (res) => {
          this._columns.update((cols) => ({
            ...cols,
            [status]: [...cols[status], ...res.items],
          }));
          this._meta.update((m) => ({
            ...m,
            [status]: {
              page: res.pageNumber,
              totalCount: res.totalCount,
              hasNextPage: res.hasNextPage,
              loadingMore: false,
            },
          }));
        },
        error: (err: unknown) => {
          this._meta.update((m) => ({
            ...m,
            [status]: { ...m[status], loadingMore: false },
          }));
          this.toast.error(err instanceof ApiError ? err.detail : this.i18n.t('genericError'));
        },
      });
  }

  moveTask(
    task: TaskListItem,
    targetStatus: TaskStatus,
    beforeTaskId: string | null,
    isUndo = false,
  ): void {
    const previousColumns = this._columns();
    const previousMeta = this._meta();
    const fromStatus = task.status;
    const sameColumn = fromStatus === targetStatus;

    const undoBeforeId = findUndoBeforeTaskId(previousColumns[fromStatus], task.id);

    this._columns.set(moveTaskOptimistic(previousColumns, task.id, targetStatus, beforeTaskId));
    if (!sameColumn) {
      this._meta.update((m) => ({
        ...m,
        [fromStatus]: { ...m[fromStatus], totalCount: Math.max(0, m[fromStatus].totalCount - 1) },
        [targetStatus]: { ...m[targetStatus], totalCount: m[targetStatus].totalCount + 1 },
      }));
    }
    this.pulseLanded(task.id);

    this.boardApi.moveTask(task.id, targetStatus, beforeTaskId).subscribe({
      next: () => {
        const statusLabel = this.statusLabel(targetStatus);
        const message = sameColumn
          ? this.i18n.dict().reordered(task.title, statusLabel)
          : this.i18n.dict().moved(task.title, statusLabel);

        void this.announcer.announce(message, 'polite');

        if (!isUndo) {
          const updatedTask: TaskListItem = { ...task, status: targetStatus };
          this.toast.info(message, {
            label: this.i18n.t('undo'),
            callback: () => this.moveTask(updatedTask, fromStatus, undoBeforeId, true),
          });
        }
      },
      error: (err: unknown) => {
        this._columns.set(previousColumns);
        this._meta.set(previousMeta);
        const msg =
          err instanceof ApiError
            ? err.status === 403
              ? this.i18n.t('forbiddenError')
              : err.detail || this.i18n.t('genericError')
            : this.i18n.t('genericError');
        this.toast.error(msg);
        void this.announcer.announce(msg, 'assertive');
      },
    });
  }

  findTask(taskId: string): TaskListItem | null {
    const cols = this._columns();
    for (const status of TASK_STATUSES) {
      const found = cols[status].find((t) => t.id === taskId);
      if (found) return found;
    }
    return null;
  }

  private pulseLanded(taskId: string): void {
    this._landedTaskId.set(taskId);
    setTimeout(() => {
      if (this._landedTaskId() === taskId) {
        this._landedTaskId.set(null);
      }
    }, 550);
  }

  private statusLabel(status: TaskStatus): string {
    const d = this.i18n.dict();
    return status === 'Todo' ? d.statusTodo : status === 'InProgress' ? d.statusInProgress : d.statusDone;
  }
}
