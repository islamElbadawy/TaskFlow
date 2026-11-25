import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { LIMITS } from './limits';
import { Paginated, Task, TaskListItem, TaskStatus } from './models';
import { TaskQueryParams, TasksApi } from './tasks.api';

/**
 * Board-specific API facade using the position contract (`sort=position` and `PATCH /api/tasks/{id}/move`).
 */
@Injectable({ providedIn: 'root' })
export class BoardApi {
  private readonly tasksApi = inject(TasksApi);

  loadColumn(
    status: TaskStatus,
    filters: Omit<TaskQueryParams, 'status' | 'sort'>,
  ): Observable<Paginated<TaskListItem>> {
    return this.tasksApi.list({
      ...filters,
      status,
      sort: 'position',
      pageSize: filters.pageSize ?? LIMITS.BOARD_PAGE_SIZE,
    });
  }

  moveTask(
    taskId: string,
    status: TaskStatus,
    beforeTaskId: string | null,
  ): Observable<Task> {
    return this.tasksApi.move(taskId, { status, beforeTaskId });
  }
}
