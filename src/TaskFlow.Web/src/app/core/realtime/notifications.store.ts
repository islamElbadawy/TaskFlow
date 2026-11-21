import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { EMPTY, Observable, catchError, forkJoin, tap } from 'rxjs';
import { LIMITS } from '../api/limits';
import { Notification } from '../api/models';
import { NotificationsApi } from '../api/notifications.api';
import { formatNotificationMessage } from '../i18n/notification-text';
import { TranslationService } from '../i18n/translation.service';
import { ToastService } from '../toast/toast.service';
import { RealtimeClient } from './realtime-client';

@Injectable({ providedIn: 'root' })
export class NotificationsStore {
  private readonly api = inject(NotificationsApi);
  private readonly realtime = inject(RealtimeClient);
  private readonly toast = inject(ToastService);
  private readonly i18n = inject(TranslationService);
  private readonly router = inject(Router);

  private readonly _unreadCount = signal(0);
  private readonly _recent = signal<readonly Notification[]>([]);
  private readonly _ringTick = signal(0);
  private readonly _badgeTick = signal(0);

  readonly unreadCount = this._unreadCount.asReadonly();
  readonly recent = this._recent.asReadonly();
  readonly ringTick = this._ringTick.asReadonly();
  readonly badgeTick = this._badgeTick.asReadonly();

  private unsubscribeNotification: (() => void) | null = null;
  private unsubscribeReconnect: (() => void) | null = null;

  async connect(tokenFactory: () => string): Promise<void> {
    this.detachListeners();
    this.unsubscribeNotification = this.realtime.onNotification((n) => this.handleIncoming(n));
    this.unsubscribeReconnect = this.realtime.onReconnected(() => this.refresh());
    await this.realtime.start(tokenFactory);
    this.refresh();
  }

  async disconnect(): Promise<void> {
    this.detachListeners();
    await this.realtime.stop();
    this._unreadCount.set(0);
    this._recent.set([]);
  }

  refresh(): void {
    forkJoin({
      count: this.api.unreadCount(),
      page: this.api.list({ page: 1, pageSize: LIMITS.BELL_PREVIEW_COUNT }),
    })
      .pipe(catchError(() => EMPTY))
      .subscribe(({ count, page }) => {
        if (count !== this._unreadCount()) {
          this._badgeTick.update((v) => v + 1);
        }
        this._unreadCount.set(count);
        this._recent.set(page.items);
      });
  }

  markRead(notification: Notification): Observable<Notification> {
    if (notification.isRead) {
      return EMPTY;
    }
    this._recent.update((list) =>
      list.map((n) => (n.id === notification.id ? { ...n, isRead: true } : n)),
    );
    this._unreadCount.update((c) => Math.max(0, c - 1));
    this._badgeTick.update((v) => v + 1);

    return this.api.markRead(notification.id).pipe(
      catchError(() => {
        this.refresh();
        return EMPTY;
      }),
    );
  }

  markAllRead(): Observable<number> {
    return this.api.markAllRead().pipe(
      tap(() => {
        this._recent.update((list) => list.map((n) => ({ ...n, isRead: true })));
        this._unreadCount.set(0);
        this._badgeTick.update((v) => v + 1);
      }),
    );
  }

  private handleIncoming(notification: Notification): void {
    this._recent.update((list) => {
      const deduped = list.filter((n) => n.id !== notification.id);
      return [notification, ...deduped].slice(0, LIMITS.BELL_PREVIEW_COUNT);
    });
    if (!notification.isRead) {
      this._unreadCount.update((c) => c + 1);
      this._badgeTick.update((v) => v + 1);
    }
    this._ringTick.update((v) => v + 1);

    const text = formatNotificationMessage(notification, this.i18n.lang(), this.i18n.dict());
    const taskId = notification.relatedTaskId;
    this.toast.info(
      text,
      taskId
        ? {
            label: this.i18n.t('open'),
            callback: () => {
              void this.router.navigate(['/tasks', taskId]);
            },
          }
        : undefined,
    );
  }

  private detachListeners(): void {
    this.unsubscribeNotification?.();
    this.unsubscribeReconnect?.();
    this.unsubscribeNotification = null;
    this.unsubscribeReconnect = null;
  }
}
