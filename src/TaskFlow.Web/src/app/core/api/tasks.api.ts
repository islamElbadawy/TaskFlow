import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  CreateTaskRequest,
  MoveTaskRequest,
  Paginated,
  Task,
  TaskListItem,
  TaskPriority,
  TaskStatus,
  UpdateTaskRequest,
} from './models';

export interface TaskQueryParams {
  status?: TaskStatus;
  priority?: TaskPriority;
  assignedToId?: string;
  unassigned?: boolean;
  createdById?: string;
  overdue?: boolean;
  dueBefore?: string;
  search?: string;
  page?: number;
  pageSize?: number;
  sort?: 'createdAt' | 'position';
}

export function buildTaskHttpParams(query: TaskQueryParams): Record<string, string> {
  const params: Record<string, string> = {};
  if (query.status) params['status'] = query.status;
  if (query.priority) params['priority'] = query.priority;
  if (query.unassigned) {
    params['unassigned'] = 'true';
  } else if (query.assignedToId) {
    params['assignedToId'] = query.assignedToId;
  }
  if (query.createdById) params['createdById'] = query.createdById;
  if (query.overdue) params['overdue'] = 'true';
  if (query.dueBefore) params['dueBefore'] = query.dueBefore;
  if (query.search?.trim()) params['search'] = query.search.trim();
  if (query.page) params['page'] = String(query.page);
  if (query.pageSize) params['pageSize'] = String(query.pageSize);
  if (query.sort) params['sort'] = query.sort;
  return params;
}

@Injectable({ providedIn: 'root' })
export class TasksApi {
  private readonly http = inject(HttpClient);

  list(query: TaskQueryParams = {}): Observable<Paginated<TaskListItem>> {
    const params = new HttpParams({ fromObject: buildTaskHttpParams(query) });
    return this.http.get<Paginated<TaskListItem>>('/api/tasks', { params });
  }

  getById(id: string): Observable<Task> {
    return this.http.get<Task>(`/api/tasks/${id}`);
  }

  create(body: CreateTaskRequest): Observable<Task> {
    return this.http.post<Task>('/api/tasks', body);
  }

  update(id: string, body: UpdateTaskRequest): Observable<Task> {
    return this.http.put<Task>(`/api/tasks/${id}`, body);
  }

  changeStatus(id: string, status: TaskStatus): Observable<Task> {
    return this.http.patch<Task>(`/api/tasks/${id}/status`, { status });
  }

  assign(id: string, assignedToId: string | null): Observable<Task> {
    return this.http.patch<Task>(`/api/tasks/${id}/assignee`, { assignedToId });
  }

  move(id: string, body: MoveTaskRequest): Observable<Task> {
    return this.http.patch<Task>(`/api/tasks/${id}/move`, body);
  }

  delete(id: string): Observable<boolean> {
    return this.http.delete<boolean>(`/api/tasks/${id}`);
  }
}
