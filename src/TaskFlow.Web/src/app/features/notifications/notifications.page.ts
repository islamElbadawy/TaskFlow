import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { finalize } from 'rxjs';
import {
  LucideCheck,
  LucideClock,
  LucideMessageSquare,
  LucidePencil,
  LucideUserPlus,
} from '@lucide/angular';
import { LIMITS } from '../../core/api/limits';
import { Notification, Paginated } from '../../core/api/models';
import { DateFmtPipe } from '../../core/i18n/date-fmt.pipe';
import { formatNotificationMessage } from '../../core/i18n/notification-text';
import { RelTimePipe } from '../../core/i18n/rel-time.pipe';
import { TranslationService } from '../../core/i18n/translation.service';
import { NotificationsStore } from '../../core/realtime/notifications.store';
import { ToastService } from '../../core/toast/toast.service';
import { StaggerDirective } from '../../shared/motion/stagger.directive';
import { AppButton } from '../../shared/ui/button';
import { AppEmptyState } from '../../shared/ui/empty-state';
import { AppErrorState } from '../../shared/ui/error-state';
import { AppPaginator } from '../../shared/ui/paginator';
import { AppSkeleton } from '../../shared/ui/skeleton';

type NoteFilter = 'all' | 'unread' | 'read';

@Component({
  selector: 'app-notifications-page',
  imports: [
    StaggerDirective,
    AppButton,
    AppSkeleton,
    AppErrorState,
    AppEmptyState,
    AppPaginator,
    RelTimePipe,
    DateFmtPipe,
    LucideUserPlus,
    LucidePencil,
    LucideCheck,
    LucideMessageSquare,
    LucideClock,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section appStagger>
      <div class="flex items-center justify-between gap-4 flex-wrap">
        <h1 class="display text-3xl font-extrabold" tabindex="-1">
          {{ i18n.t('notifications') }}
        </h1>
        <app-button
          variant="quiet"
          [busy]="markingAll()"
          [disabled]="notificationsStore.unreadCount() === 0"
          (click)="onMarkAllRead()"
        >
          {{ i18n.t('markAllRead') }}
        </app-button>
      </div>

      <div
        class="flex p-0.5 rounded-[var(--radius-control)] bg-sunk border border-line w-fit mt-6"
        role="group"
      >
        @for (opt of filterOptions; track opt.value) {
          <button
            type="button"
            class="tf-transition h-8 px-3 rounded-lg text-[13px] cursor-pointer"
            [class.bg-panel]="filter() === opt.value"
            [class.text-ink]="filter() === opt.value"
            [class.font-bold]="filter() === opt.value"
            [class.shadow-xs]="filter() === opt.value"
            [class.text-slate]="filter() !== opt.value"
            [attr.aria-pressed]="filter() === opt.value"
            (click)="setFilter(opt.value)"
          >
            {{ i18n.t(opt.labelKey) }}
          </button>
        }
      </div>

      <div class="mt-5">
        @if (notifications.isLoading()) {
          <app-skeleton kind="list" />
        } @else if (notifications.error()) {
          <app-error-state (retry)="notifications.reload()" />
        } @else if (notifications.value(); as page) {
          @if (page.items.length === 0) {
            <app-empty-state [message]="i18n.t('caughtUp')" />
          } @else {
            <ul class="bg-panel border border-line rounded-2xl divide-y divide-line">
              @for (item of page.items; track item.id) {
                <li>
                  <button
                    type="button"
                    class="tf-transition w-full text-start flex items-start gap-3 px-5 py-4 hover:bg-ink/[.03] cursor-pointer"
                    (click)="onOpen(item)"
                  >
                    <span
                      class="mt-3 size-2 rounded-full shrink-0"
                      [class.bg-flow]="!item.isRead"
                      [class.bg-transparent]="item.isRead"
                      aria-hidden="true"
                    ></span>
                    <span
                      class="size-9 rounded-full bg-sunk grid place-items-center shrink-0 text-slate"
                      aria-hidden="true"
                    >
                      @switch (item.type) {
                        @case ('TaskAssigned') {
                          <svg lucideUserPlus class="size-[18px]" [strokeWidth]="1.8"></svg>
                        }
                        @case ('TaskUpdated') {
                          <svg lucidePencil class="size-[18px]" [strokeWidth]="1.8"></svg>
                        }
                        @case ('TaskCompleted') {
                          <svg lucideCheck class="size-[18px]" [strokeWidth]="2"></svg>
                        }
                        @case ('CommentAdded') {
                          <svg
                            lucideMessageSquare
                            class="size-[18px] rtl:-scale-x-100"
                            [strokeWidth]="1.8"
                          ></svg>
                        }
                        @case ('DeadlineApproaching') {
                          <svg lucideClock class="size-[18px]" [strokeWidth]="1.8"></svg>
                        }
                      }
                    </span>
                    <span class="flex-1 min-w-0">
                      <span class="block" [class.font-bold]="!item.isRead">
                        {{ messageText(item) }}
                      </span>
                      <span
                        class="block text-xs text-slate mt-0.5"
                        [title]="item.createdAt | dateFmt: { dateStyle: 'full', timeStyle: 'short' }"
                      >
                        {{ item.createdAt | relTime }}
                      </span>
                    </span>
                  </button>
                </li>
              }
            </ul>

            <app-paginator
              [pageNumber]="page.pageNumber"
              [pageSize]="page.pageSize"
              [totalCount]="page.totalCount"
              [hasPreviousPage]="page.hasPreviousPage"
              [hasNextPage]="page.hasNextPage"
              [showPageSize]="false"
              (pageChange)="setPage($event)"
            />
          }
        }
      </div>
    </section>
  `,
})
export class NotificationsPage {
  protected readonly i18n = inject(TranslationService);
  protected readonly notificationsStore = inject(NotificationsStore);
  private readonly toast = inject(ToastService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly filterOptions: readonly {
    value: NoteFilter;
    labelKey: 'filterAll' | 'filterUnread' | 'filterRead';
  }[] = [
    { value: 'all', labelKey: 'filterAll' },
    { value: 'unread', labelKey: 'filterUnread' },
    { value: 'read', labelKey: 'filterRead' },
  ];

  protected readonly markingAll = signal(false);

  private readonly queryParams = toSignal(this.route.queryParamMap, {
    initialValue: this.route.snapshot.queryParamMap,
  });

  protected readonly filter = computed<NoteFilter>(() => {
    const f = this.queryParams().get('filter');
    return f === 'unread' || f === 'read' ? f : 'all';
  });

  protected readonly pageNumber = computed(() =>
    Math.max(1, Number(this.queryParams().get('page')) || 1),
  );

  protected readonly notifications = httpResource<Paginated<Notification>>(() => {
    const params: Record<string, string> = {
      page: String(this.pageNumber()),
      pageSize: String(LIMITS.DEFAULT_PAGE_SIZE),
    };
    const f = this.filter();
    if (f === 'unread') params['unread'] = 'true';
    if (f === 'read') params['unread'] = 'false';
    return { url: '/api/notifications', params };
  });

  protected messageText(item: Notification): string {
    return formatNotificationMessage(item, this.i18n.lang(), this.i18n.dict());
  }

  protected setFilter(filter: NoteFilter): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { filter: filter === 'all' ? null : filter, page: 1 },
      queryParamsHandling: 'merge',
    });
  }

  protected setPage(page: number): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { page },
      queryParamsHandling: 'merge',
    });
  }

  protected onOpen(item: Notification): void {
    this.notificationsStore.markRead(item).subscribe({
      complete: () => {
        if (!item.relatedTaskId) {
          this.notifications.reload();
        }
      },
    });
    if (item.relatedTaskId) {
      void this.router.navigate(['/tasks', item.relatedTaskId]);
    }
  }

  protected onMarkAllRead(): void {
    if (this.markingAll()) return;
    this.markingAll.set(true);
    this.notificationsStore
      .markAllRead()
      .pipe(finalize(() => this.markingAll.set(false)))
      .subscribe((count) => {
        this.toast.info(this.i18n.dict().markedReadCount(count));
        this.notifications.reload();
      });
  }
}
