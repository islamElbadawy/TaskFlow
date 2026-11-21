import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { RegisterRequest, UpdateUserRequest, User, UserRole } from './models';

export interface CreateUserRequest extends RegisterRequest {
  role: UserRole;
}

@Injectable({ providedIn: 'root' })
export class UsersApi {
  private readonly http = inject(HttpClient);

  list(isActive?: boolean): Observable<User[]> {
    let params = new HttpParams();
    if (isActive !== undefined) {
      params = params.set('isActive', String(isActive));
    }
    return this.http.get<User[]>('/api/users', { params });
  }

  me(): Observable<User> {
    return this.http.get<User>('/api/users/me');
  }

  getById(id: string): Observable<User> {
    return this.http.get<User>(`/api/users/${id}`);
  }

  create(body: CreateUserRequest): Observable<User> {
    return this.http.post<User>('/api/users', body);
  }

  update(id: string, body: UpdateUserRequest): Observable<User> {
    return this.http.put<User>(`/api/users/${id}`, body);
  }

  changeRole(id: string, newRole: UserRole): Observable<boolean> {
    return this.http.put<boolean>(`/api/users/${id}/role`, { newRole });
  }

  setActive(id: string, isActive: boolean): Observable<User> {
    return this.http.patch<User>(`/api/users/${id}/active`, { isActive });
  }

  delete(id: string): Observable<boolean> {
    return this.http.delete<boolean>(`/api/users/${id}`);
  }
}
