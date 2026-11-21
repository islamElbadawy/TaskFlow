import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map, of, switchMap } from 'rxjs';
import { AuthApi } from './auth.api';
import { RegisterRequest, UpdateUserRequest, User, UserRole } from './models';

export interface CreateUserRequest extends RegisterRequest {
  role: UserRole;
}

@Injectable({ providedIn: 'root' })
export class UsersApi {
  private readonly http = inject(HttpClient);
  private readonly authApi = inject(AuthApi);

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

  /**
   * Registers a new user via /api/auth/register (without affecting the Admin's session),
   * and chains PUT /api/users/{id}/role when a non-default role is chosen.
   */
  create(body: CreateUserRequest): Observable<User> {
    const { role, ...registerPayload } = body;
    return this.authApi.register(registerPayload).pipe(
      switchMap((created) => {
        if (role === 'User') {
          return of(created);
        }
        return this.changeRole(created.id, role).pipe(map(() => ({ ...created, role })));
      }),
    );
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
