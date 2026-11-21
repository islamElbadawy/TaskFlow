import { Injectable, signal } from '@angular/core';

/**
 * Lightweight bridge holding the current bearer token and 401 handler so
 * HTTP interceptors never create circular DI with AuthStore.
 */
@Injectable({ providedIn: 'root' })
export class AuthSessionBridge {
  private readonly _token = signal<string | null>(null);
  private unauthorizedHandler: (() => void) | null = null;

  readonly token = this._token.asReadonly();

  setToken(token: string | null): void {
    this._token.set(token);
  }

  registerUnauthorizedHandler(handler: () => void): void {
    this.unauthorizedHandler = handler;
  }

  notifyUnauthorized(): void {
    this.unauthorizedHandler?.();
  }
}
