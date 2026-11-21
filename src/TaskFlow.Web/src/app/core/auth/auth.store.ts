import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, catchError, finalize, map, of, switchMap, tap } from 'rxjs';
import { AuthApi } from '../api/auth.api';
import { LoginRequest, LoginResponse, RegisterRequest, User } from '../api/models';
import { UsersApi } from '../api/users.api';
import { parseUtcDate } from '../domain/due-date';
import { NotificationsStore } from '../realtime/notifications.store';
import { AppStorage } from '../storage/storage';
import { AuthSessionBridge } from './auth-session';

const STORAGE_KEY = 'tf-session';

interface StoredSession {
  token: string;
  user: User;
  expiresAt: string;
}

export type SignOutReason = 'manual' | 'session';

@Injectable({ providedIn: 'root' })
export class AuthStore {
  private readonly authApi = inject(AuthApi);
  private readonly usersApi = inject(UsersApi);
  private readonly storage = inject(AppStorage);
  private readonly bridge = inject(AuthSessionBridge);
  private readonly notifications = inject(NotificationsStore);
  private readonly router = inject(Router);

  private readonly _user = signal<User | null>(null);
  private readonly _token = signal<string | null>(null);
  private readonly _expiresAt = signal<string | null>(null);
  private readonly _ready = signal(false);

  private expiryTimer: ReturnType<typeof setTimeout> | null = null;

  readonly user = this._user.asReadonly();
  readonly token = this._token.asReadonly();
  readonly ready = this._ready.asReadonly();
  readonly isAuthenticated = computed(() => this._token() !== null && this._user() !== null);

  constructor() {
    this.bridge.registerUnauthorizedHandler(() => {
      void this.signOut('session');
    });
  }

  restoreSession(): Observable<void> {
    const saved = this.readStored();
    if (!saved) {
      this._ready.set(true);
      return of(undefined);
    }

    const expiresMs = parseUtcDate(saved.expiresAt).getTime();
    if (Number.isNaN(expiresMs) || expiresMs <= Date.now()) {
      this.clearState();
      this._ready.set(true);
      return of(undefined);
    }

    this._token.set(saved.token);
    this._user.set(saved.user);
    this._expiresAt.set(saved.expiresAt);
    this.bridge.setToken(saved.token);

    return this.usersApi.me().pipe(
      tap((freshUser) => {
        this._user.set(freshUser);
        this.persist(saved.token, freshUser, saved.expiresAt);
        this.scheduleExpiry(saved.expiresAt);
        void this.notifications.connect(() => this._token() ?? '');
      }),
      map(() => undefined),
      catchError(() => {
        this.clearState();
        return of(undefined);
      }),
      finalize(() => this._ready.set(true)),
    );
  }

  login(credentials: LoginRequest): Observable<User> {
    return this.authApi.login(credentials).pipe(
      tap((response) => this.applyLoginResponse(response)),
      map((response) => response.user),
    );
  }

  registerAndLogin(request: RegisterRequest): Observable<User> {
    return this.authApi
      .register(request)
      .pipe(switchMap(() => this.login({ email: request.email, password: request.password })));
  }

  updateCurrentUser(user: User): void {
    this._user.set(user);
    const token = this._token();
    const expiresAt = this._expiresAt();
    if (token && expiresAt) {
      this.persist(token, user, expiresAt);
    }
  }

  async signOut(reason: SignOutReason = 'manual'): Promise<void> {
    const wasAuthenticated = this.isAuthenticated();
    this.clearState();
    await this.notifications.disconnect();

    if (reason === 'session' && wasAuthenticated) {
      const currentUrl = this.router.url;
      const queryParams: Record<string, string> = { reason: 'session' };
      if (currentUrl && !currentUrl.startsWith('/login') && !currentUrl.startsWith('/register')) {
        queryParams['returnUrl'] = currentUrl;
      }
      await this.router.navigate(['/login'], { queryParams });
    } else if (reason === 'manual') {
      await this.router.navigate(['/login']);
    }
  }

  private applyLoginResponse(response: LoginResponse): void {
    this._token.set(response.token);
    this._user.set(response.user);
    this._expiresAt.set(response.expiresAt);
    this.bridge.setToken(response.token);
    this.persist(response.token, response.user, response.expiresAt);
    this.scheduleExpiry(response.expiresAt);
    void this.notifications.connect(() => this._token() ?? '');
  }

  private scheduleExpiry(expiresAt: string): void {
    if (this.expiryTimer) {
      clearTimeout(this.expiryTimer);
      this.expiryTimer = null;
    }
    const ms = parseUtcDate(expiresAt).getTime() - Date.now();
    if (ms <= 0) {
      void this.signOut('session');
      return;
    }
    const delay = Math.min(ms, 2_147_483_647);
    this.expiryTimer = setTimeout(() => {
      void this.signOut('session');
    }, delay);
  }

  private clearState(): void {
    if (this.expiryTimer) {
      clearTimeout(this.expiryTimer);
      this.expiryTimer = null;
    }
    this._token.set(null);
    this._user.set(null);
    this._expiresAt.set(null);
    this.bridge.setToken(null);
    this.storage.remove(STORAGE_KEY);
  }

  private persist(token: string, user: User, expiresAt: string): void {
    this.storage.set(STORAGE_KEY, JSON.stringify({ token, user, expiresAt }));
  }

  private readStored(): StoredSession | null {
    const raw = this.storage.get(STORAGE_KEY);
    if (!raw) return null;
    try {
      const parsed = JSON.parse(raw) as Partial<StoredSession>;
      if (parsed.token && parsed.user && parsed.expiresAt) {
        return parsed as StoredSession;
      }
      return null;
    } catch {
      return null;
    }
  }
}
