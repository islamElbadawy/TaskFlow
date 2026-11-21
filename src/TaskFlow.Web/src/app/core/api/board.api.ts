import { Injectable, inject, signal } from '@angular/core';
import { Observable, catchError, tap, throwError } from 'rxjs';
import { ApiError } from '../http/api-error';
import { LIMITS } from './limits';
import { Paginated, Task, TaskListItem, TaskStatus } from './models';
import { TaskQueryParams, TasksApi } from './tasks.api';

/**
 * Board-specific API facade.
 * Tries the Part 1 position contract (`sort=position` and `PATCH /api/tasks/{id}/move`) first,
 * and falls back transparently to status-only (`PATCH /api/tasks/{id}/status`) if the backend
 * does not support position yet.
 */
@Injectable({ providedIn: 'root' })
export class BoardApi {
  private readonly tasksApi = inject(TasksApi);
  private readonly _supportsPosition = signal(true);

  readonly supportsPosition = this._supportsPosition.asReadonly();

  loadColumn(
    status: TaskStatus,
    filters: Omit<TaskQueryParams, 'status' | 'sort'>,
  ): Observable<Paginated<TaskListItem>> {
    const baseQuery: TaskQueryParams = {
      ...filters,
      status,
      pageSize: filters.pageSize ?? LIMITS.BOARD_PAGE_SIZE,
    };

    if (!this._supportsPosition()) {
      return this.tasksApi.list(baseQuery);
    }

    return this.tasksApi.list({ ...baseQuery, sort: 'position' }).pipe(
      tap((result) => {
        if (result.items.length > 0 && result.items[0].position === undefined) {
          this._supportsPosition.set(false);
        }
      }),
      catchError((err: unknown) => {
        if (err instanceof ApiError && err.status === 400) {
          this._supportsPosition.set(false);
          return this.tasksApi.list(baseQuery);
        }
        return throwError(() => err);
      }),
    );
  }

  moveTask(
    taskId: string,
    status: TaskStatus,
    beforeTaskId: string | null,
  ): Observable<Task> {
    if (!this._supportsPosition()) {
      return this.tasksApi.changeStatus(taskId, status);
    }

    return this.tasksApi.move(taskId, { status, beforeTaskId }).pipe(
      catchError((err: unknown) => {
        if (err instanceof ApiError && (err.status === 404 || err.status === 405)) {
          this._supportsPosition.set(false);
          return this.tasksApi.changeStatus(taskId, status);
        }
        return throwError(() => err);
      }),
    );
  }
}
