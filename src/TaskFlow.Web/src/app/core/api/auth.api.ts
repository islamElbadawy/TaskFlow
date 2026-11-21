import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ChangePasswordRequest, LoginRequest, LoginResponse, RegisterRequest, User } from './models';

@Injectable({ providedIn: 'root' })
export class AuthApi {
  private readonly http = inject(HttpClient);

  register(body: RegisterRequest): Observable<User> {
    return this.http.post<User>('/api/auth/register', body);
  }

  login(body: LoginRequest): Observable<LoginResponse> {
    return this.http.post<LoginResponse>('/api/auth/login', body);
  }

  changePassword(body: ChangePasswordRequest): Observable<boolean> {
    return this.http.post<boolean>('/api/auth/change-password', body);
  }
}
