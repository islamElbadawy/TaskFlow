import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { Comment, Paginated } from './models';

@Injectable({ providedIn: 'root' })
export class CommentsApi {
  private readonly http = inject(HttpClient);

  list(taskId: string, page = 1, pageSize = 20): Observable<Paginated<Comment>> {
    const params = new HttpParams()
      .set('page', String(page))
      .set('pageSize', String(pageSize));
    return this.http.get<Paginated<Comment>>(`/api/tasks/${taskId}/comments`, { params });
  }

  create(taskId: string, content: string): Observable<Comment> {
    return this.http.post<Comment>(`/api/tasks/${taskId}/comments`, { content });
  }

  update(id: string, content: string): Observable<Comment> {
    return this.http.put<Comment>(`/api/comments/${id}`, { content });
  }

  delete(id: string): Observable<boolean> {
    return this.http.delete<boolean>(`/api/comments/${id}`);
  }
}
