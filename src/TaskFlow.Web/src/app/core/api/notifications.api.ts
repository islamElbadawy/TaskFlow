import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { Notification, Paginated } from './models';

export interface NotificationQueryParams {
  unread?: boolean;
  page?: number;
  pageSize?: number;
}

@Injectable({ providedIn: 'root' })
export class NotificationsApi {
  private readonly http = inject(HttpClient);

  list(query: NotificationQueryParams = {}): Observable<Paginated<Notification>> {
    let params = new HttpParams();
    if (query.unread !== undefined) params = params.set('unread', String(query.unread));
    if (query.page) params = params.set('page', String(query.page));
    if (query.pageSize) params = params.set('pageSize', String(query.pageSize));
    return this.http.get<Paginated<Notification>>('/api/notifications', { params });
  }

  unreadCount(): Observable<number> {
    return this.http.get<number>('/api/notifications/unread-count');
  }

  markRead(id: string): Observable<Notification> {
    return this.http.patch<Notification>(`/api/notifications/${id}/read`, {});
  }

  markAllRead(): Observable<number> {
    return this.http.post<number>('/api/notifications/read-all', {});
  }
}
