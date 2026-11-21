import { InjectionToken } from '@angular/core';

export interface AppConfig {
  /** Base URL for API and SignalR hub. Empty string uses the dev proxy / same origin. */
  readonly apiUrl: string;
}

export const APP_CONFIG = new InjectionToken<AppConfig>('APP_CONFIG', {
  providedIn: 'root',
  factory: () => ({ apiUrl: '' }),
});
