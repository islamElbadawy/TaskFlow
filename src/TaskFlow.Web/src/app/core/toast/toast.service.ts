import { Injectable, signal } from '@angular/core';

export type ToastVariant = 'info' | 'error';

export interface ToastAction {
  readonly label: string;
  readonly callback: () => void;
}

export interface ToastItem {
  readonly id: number;
  readonly message: string;
  readonly variant: ToastVariant;
  readonly action?: ToastAction;
  readonly durationMs: number;
  readonly leaving: boolean;
}

const DEFAULT_DURATION_MS = 4000;

@Injectable({ providedIn: 'root' })
export class ToastService {
  private nextId = 1;
  private readonly _toasts = signal<readonly ToastItem[]>([]);

  readonly toasts = this._toasts.asReadonly();

  show(
    message: string,
    options?: { variant?: ToastVariant; action?: ToastAction; durationMs?: number },
  ): number {
    const id = this.nextId++;
    const item: ToastItem = {
      id,
      message,
      variant: options?.variant ?? 'info',
      action: options?.action,
      durationMs: options?.durationMs ?? DEFAULT_DURATION_MS,
      leaving: false,
    };
    this._toasts.update((list) => [...list, item]);
    return id;
  }

  info(message: string, action?: ToastAction): number {
    return this.show(message, { variant: 'info', action });
  }

  error(message: string, action?: ToastAction): number {
    return this.show(message, { variant: 'error', action });
  }

  markLeaving(id: number): void {
    this._toasts.update((list) => list.map((t) => (t.id === id ? { ...t, leaving: true } : t)));
  }

  remove(id: number): void {
    this._toasts.update((list) => list.filter((t) => t.id !== id));
  }
}
