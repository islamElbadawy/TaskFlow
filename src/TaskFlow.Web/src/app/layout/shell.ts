import { A11yModule } from '@angular/cdk/a11y';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  afterRenderEffect,
  computed,
  effect,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import {
  LucideActivity,
  LucideBell,
  LucideKanban,
  LucideList,
  LucideLogOut,
  LucideUser,
  LucideUsers,
} from '@lucide/angular';
import { filter } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Notification, UserRole } from '../core/api/models';
import { AuthStore } from '../core/auth/auth.store';
import { formatNotificationMessage } from '../core/i18n/notification-text';
import { RelTimePipe } from '../core/i18n/rel-time.pipe';
import { TranslationService } from '../core/i18n/translation.service';
import { NotificationsStore } from '../core/realtime/notifications.store';
import { ToastService } from '../core/toast/toast.service';
import { AppAvatar } from '../shared/ui/avatar';
import { BrandMark } from '../shared/ui/brand-mark';
import { AppPrefs } from '../shared/ui/prefs';

@Component({
  selector: 'app-shell',
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    A11yModule,
    BrandMark,
    AppPrefs,
    AppAvatar,
    RelTimePipe,
    LucideActivity,
    LucideList,
    LucideKanban,
    LucideUsers,
    LucideBell,
    LucideUser,
    LucideLogOut,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    '(document:click)': 'onDocumentClick($event)',
    '(document:keydown.escape)': 'closePopovers()',
    '(window:resize)': 'positionIndicator()',
    '(window:offline)': 'onOffline()',
  },
  template: `
    <a
      href="#main-content"
      class="sr-only focus:not-sr-only focus:fixed focus:top-2 focus:start-2 z-50 inline-flex items-center h-10 px-4 rounded-[var(--radius-control)] bg-flow text-on-flow font-bold text-[14px]"
    >
      {{ i18n.t('skipToContent') }}
    </a>

    <div class="min-h-screen">
      <header class="tf-topbar sticky top-0 z-30 bg-paper/85 backdrop-blur-md border-b border-line">
        <div class="max-w-6xl mx-auto px-4 md:px-8 h-16 flex items-center gap-6">
          <a routerLink="/dashboard" class="flex items-center gap-2.5 shrink-0">
            <app-brand-mark sizeClass="size-8" />
            <span class="display text-xl font-extrabold">Taskaty</span>
          </a>

          <nav
            #navRef
            class="hidden md:flex relative items-center gap-1 h-full"
            [attr.aria-label]="i18n.t('mainNav')"
          >
            <span
              #indicatorRef
              class="absolute bottom-0 start-0 h-[3px] w-[1px] rounded-t-[3px] bg-flow pointer-events-none"
              style="transform-origin: 0 0; opacity: 0; transition: transform 420ms var(--ease-spring), opacity 200ms"
              aria-hidden="true"
            ></span>

            <a
              routerLink="/dashboard"
              routerLinkActive="text-ink font-bold"
              #dashActive="routerLinkActive"
              [attr.aria-current]="dashActive.isActive ? 'page' : null"
              class="tf-transition relative z-[1] flex items-center gap-2 h-10 px-3 rounded-[var(--radius-control)] text-slate hover:text-ink hover:bg-ink/5 whitespace-nowrap"
            >
              <svg lucideActivity class="size-[18px]" [strokeWidth]="1.8" aria-hidden="true"></svg>
              <span>{{ i18n.t('today') }}</span>
            </a>

            <a
              routerLink="/tasks"
              [routerLinkActiveOptions]="{ exact: true }"
              routerLinkActive="text-ink font-bold"
              #tasksActive="routerLinkActive"
              [attr.aria-current]="tasksActive.isActive ? 'page' : null"
              class="tf-transition relative z-[1] flex items-center gap-2 h-10 px-3 rounded-[var(--radius-control)] text-slate hover:text-ink hover:bg-ink/5 whitespace-nowrap"
            >
              <svg lucideList class="size-[18px]" [strokeWidth]="1.8" aria-hidden="true"></svg>
              <span>{{ i18n.t('tasks') }}</span>
            </a>

            <a
              routerLink="/tasks/board"
              routerLinkActive="text-ink font-bold"
              #boardActive="routerLinkActive"
              [attr.aria-current]="boardActive.isActive ? 'page' : null"
              class="tf-transition relative z-[1] flex items-center gap-2 h-10 px-3 rounded-[var(--radius-control)] text-slate hover:text-ink hover:bg-ink/5 whitespace-nowrap"
            >
              <svg lucideKanban class="size-[18px]" [strokeWidth]="1.8" aria-hidden="true"></svg>
              <span>{{ i18n.t('board') }}</span>
            </a>

            @if (isAdmin()) {
              <a
                routerLink="/admin/users"
                routerLinkActive="text-ink font-bold"
                #usersActive="routerLinkActive"
                [attr.aria-current]="usersActive.isActive ? 'page' : null"
                class="tf-transition relative z-[1] flex items-center gap-2 h-10 px-3 rounded-[var(--radius-control)] text-slate hover:text-ink hover:bg-ink/5 whitespace-nowrap"
              >
                <svg lucideUsers class="size-[18px]" [strokeWidth]="1.8" aria-hidden="true"></svg>
                <span>{{ i18n.t('users') }}</span>
              </a>
            }
          </nav>

          <div class="ms-auto flex items-center gap-2">
            <div class="hidden lg:block">
              <app-prefs />
            </div>

            <!-- Notifications bell -->
            <div class="relative" #bellWrap>
              <button
                type="button"
                class="tf-transition tf-press relative size-10 grid place-items-center rounded-[var(--radius-control)] text-slate hover:text-ink hover:bg-ink/5 cursor-pointer"
                [attr.aria-expanded]="bellOpen()"
                aria-haspopup="true"
                [attr.aria-label]="i18n.dict().unreadBadge(notifications.unreadCount())"
                (click)="toggleBell($event)"
              >
                <svg
                  lucideBell
                  class="size-5"
                  [class.tf-ring]="ringing()"
                  [strokeWidth]="1.8"
                  aria-hidden="true"
                ></svg>
                @if (notifications.unreadCount() > 0) {
                  <span
                    class="num absolute -top-0.5 -end-0.5 min-w-5 h-5 px-1 rounded-full bg-heat text-on-heat text-[11px] leading-5 text-center ring-2 ring-paper"
                    [class.tf-bump]="bumping()"
                  >
                    {{ i18n.formatNumber(notifications.unreadCount()) }}
                  </span>
                }
              </button>

              @if (bellOpen()) {
                <div
                  cdkTrapFocus
                  class="tf-menu-in absolute top-12 -end-12 sm:end-0 w-[min(22rem,calc(100vw-2rem))] bg-panel border border-line rounded-2xl p-2 shadow-xl z-40"
                >
                  <div class="flex justify-between items-center px-3 py-2">
                    <span class="display font-bold">{{ i18n.t('notifications') }}</span>
                    @if (notifications.unreadCount() > 0) {
                      <button
                        type="button"
                        class="text-flow text-sm font-bold hover:underline cursor-pointer"
                        (click)="onMarkAllRead()"
                      >
                        {{ i18n.t('markAllRead') }}
                      </button>
                    }
                  </div>

                  @if (notifications.recent().length === 0) {
                    <p class="px-3 py-6 text-center text-slate text-[14px]">
                      {{ i18n.t('caughtUp') }}
                    </p>
                  } @else {
                    <ul class="text-[14px] max-h-80 overflow-y-auto">
                      @for (item of notifications.recent(); track item.id) {
                        <li>
                          <button
                            type="button"
                            class="tf-transition w-full text-start flex gap-3 px-3 py-2.5 rounded-lg hover:bg-ink/5 cursor-pointer"
                            (click)="onOpenNotification(item)"
                          >
                            <span
                              class="mt-2 size-2 rounded-full shrink-0"
                              [class.bg-flow]="!item.isRead"
                              [class.bg-transparent]="item.isRead"
                              aria-hidden="true"
                            ></span>
                            <span class="flex-1 min-w-0">
                              <span class="block" [class.font-bold]="!item.isRead">
                                {{ notificationText(item) }}
                              </span>
                              <span class="block text-xs text-slate mt-0.5">
                                {{ item.createdAt | relTime }}
                              </span>
                            </span>
                          </button>
                        </li>
                      }
                    </ul>
                  }

                  <div class="border-t border-line mt-1 pt-1">
                    <a
                      routerLink="/notifications"
                      class="tf-transition w-full flex items-center justify-center h-9 px-3 rounded-lg hover:bg-ink/5 text-[14px] text-flow font-bold"
                      (click)="closePopovers()"
                    >
                      {{ i18n.t('seeAllNotifications') }}
                    </a>
                  </div>
                </div>
              }
            </div>

            <!-- Account menu -->
            <div class="relative sm:border-s border-line sm:ps-2 ms-1" #acctWrap>
              <button
                type="button"
                class="tf-transition flex items-center gap-2.5 px-2 rounded-[var(--radius-control)] h-10 hover:bg-ink/5 cursor-pointer"
                aria-haspopup="menu"
                [attr.aria-expanded]="accountOpen()"
                [attr.aria-label]="userName() + ', ' + roleLabel()"
                (click)="toggleAccount($event)"
              >
                <app-avatar [name]="userName()" size="lg" [solid]="true" />
                <span class="leading-tight hidden xl:block text-start">
                  <span class="block font-bold text-sm"><bdi>{{ userName() }}</bdi></span>
                  <span class="block text-xs text-slate">{{ roleLabel() }}</span>
                </span>
              </button>

              @if (accountOpen()) {
                <div
                  cdkTrapFocus
                  role="menu"
                  class="tf-menu-in absolute top-12 end-0 w-72 bg-panel border border-line rounded-2xl p-2 shadow-xl z-40"
                >
                  <div class="px-3 py-2">
                    <p class="font-bold"><bdi>{{ userName() }}</bdi></p>
                    <p class="text-[13px] text-slate truncate text-start" dir="ltr">
                      {{ auth.user()?.email }}
                    </p>
                    <p class="text-[13px] text-slate">{{ roleLabel() }}</p>
                  </div>

                  <div class="lg:hidden px-3 py-2 border-t border-line">
                    <app-prefs />
                  </div>

                  <div class="border-t border-line mt-1 pt-1">
                    <a
                      routerLink="/profile"
                      role="menuitem"
                      class="tf-transition w-full text-start flex items-center gap-2.5 h-9 px-3 rounded-lg hover:bg-ink/5 text-[14px]"
                      (click)="closePopovers()"
                    >
                      <svg
                        lucideUser
                        class="size-[18px] text-slate"
                        [strokeWidth]="1.8"
                        aria-hidden="true"
                      ></svg>
                      <span>{{ i18n.t('profile') }}</span>
                    </a>
                    <button
                      type="button"
                      role="menuitem"
                      class="tf-transition w-full text-start flex items-center gap-2.5 h-9 px-3 rounded-lg hover:bg-ink/5 text-[14px] cursor-pointer"
                      (click)="onLogOut()"
                    >
                      <svg
                        lucideLogOut
                        class="size-[18px] text-slate rtl:-scale-x-100"
                        [strokeWidth]="1.8"
                        aria-hidden="true"
                      ></svg>
                      <span>{{ i18n.t('logOut') }}</span>
                    </button>
                  </div>
                </div>
              }
            </div>
          </div>
        </div>

        <!-- Phone scrollable tab pills -->
        <nav
          class="md:hidden flex items-center gap-2 px-4 pb-2 overflow-x-auto"
          [attr.aria-label]="i18n.t('mainNav')"
        >
          <a
            routerLink="/dashboard"
            routerLinkActive="bg-panel border-flow/40 text-ink font-bold"
            class="tf-transition h-9 px-3 rounded-[var(--radius-control)] text-[14px] border border-line text-slate inline-flex items-center whitespace-nowrap"
          >
            {{ i18n.t('today') }}
          </a>
          <a
            routerLink="/tasks"
            [routerLinkActiveOptions]="{ exact: true }"
            routerLinkActive="bg-panel border-flow/40 text-ink font-bold"
            class="tf-transition h-9 px-3 rounded-[var(--radius-control)] text-[14px] border border-line text-slate inline-flex items-center whitespace-nowrap"
          >
            {{ i18n.t('tasks') }}
          </a>
          <a
            routerLink="/tasks/board"
            routerLinkActive="bg-panel border-flow/40 text-ink font-bold"
            class="tf-transition h-9 px-3 rounded-[var(--radius-control)] text-[14px] border border-line text-slate inline-flex items-center whitespace-nowrap"
          >
            {{ i18n.t('board') }}
          </a>
          @if (isAdmin()) {
            <a
              routerLink="/admin/users"
              routerLinkActive="bg-panel border-flow/40 text-ink font-bold"
              class="tf-transition h-9 px-3 rounded-[var(--radius-control)] text-[14px] border border-line text-slate inline-flex items-center whitespace-nowrap"
            >
              {{ i18n.t('users') }}
            </a>
          }
        </nav>
      </header>

      <main id="main-content" class="max-w-6xl mx-auto px-4 md:px-8 py-8">
        <router-outlet />
      </main>
    </div>
  `,
})
export class AppShell {
  protected readonly auth = inject(AuthStore);
  protected readonly i18n = inject(TranslationService);
  protected readonly notifications = inject(NotificationsStore);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);

  private readonly navRef = viewChild<ElementRef<HTMLElement>>('navRef');
  private readonly indicatorRef = viewChild<ElementRef<HTMLElement>>('indicatorRef');
  private readonly bellWrap = viewChild<ElementRef<HTMLElement>>('bellWrap');
  private readonly acctWrap = viewChild<ElementRef<HTMLElement>>('acctWrap');

  protected readonly bellOpen = signal(false);
  protected readonly accountOpen = signal(false);
  protected readonly ringing = signal(false);
  protected readonly bumping = signal(false);
  private readonly currentUrl = signal(this.router.url);

  protected readonly userName = computed(() => this.auth.user()?.fullName ?? '');
  protected readonly isAdmin = computed(() => this.auth.user()?.role === 'Admin');
  protected readonly roleLabel = computed(() => {
    const role: UserRole = this.auth.user()?.role ?? 'User';
    const d = this.i18n.dict();
    return role === 'Admin' ? d.roleAdmin : role === 'Manager' ? d.roleManager : d.roleUser;
  });

  constructor() {
    this.router.events
      .pipe(
        filter((e): e is NavigationEnd => e instanceof NavigationEnd),
        takeUntilDestroyed(),
      )
      .subscribe((e) => {
        this.closePopovers();
        this.currentUrl.set(e.urlAfterRedirects);
      });

    afterRenderEffect(() => {
      this.currentUrl();
      this.i18n.lang();
      this.isAdmin();
      this.positionIndicator();
    });

    effect(() => {
      const tick = this.notifications.ringTick();
      if (tick > 0) {
        this.ringing.set(false);
        requestAnimationFrame(() => this.ringing.set(true));
        setTimeout(() => this.ringing.set(false), 750);
      }
    });

    effect(() => {
      const tick = this.notifications.badgeTick();
      if (tick > 0) {
        this.bumping.set(false);
        requestAnimationFrame(() => this.bumping.set(true));
        setTimeout(() => this.bumping.set(false), 400);
      }
    });
  }

  protected toggleBell(event: Event): void {
    event.stopPropagation();
    const next = !this.bellOpen();
    this.accountOpen.set(false);
    this.bellOpen.set(next);
  }

  protected toggleAccount(event: Event): void {
    event.stopPropagation();
    const next = !this.accountOpen();
    this.bellOpen.set(false);
    this.accountOpen.set(next);
  }

  protected closePopovers(): void {
    this.bellOpen.set(false);
    this.accountOpen.set(false);
  }

  protected onDocumentClick(event: Event): void {
    const target = event.target as Node | null;
    if (!target) return;
    if (this.bellOpen() && !this.bellWrap()?.nativeElement.contains(target)) {
      this.bellOpen.set(false);
    }
    if (this.accountOpen() && !this.acctWrap()?.nativeElement.contains(target)) {
      this.accountOpen.set(false);
    }
  }

  protected onOffline(): void {
    this.toast.error(this.i18n.t('offlineToast'));
  }

  protected notificationText(item: Notification): string {
    return formatNotificationMessage(item, this.i18n.lang(), this.i18n.dict());
  }

  protected onOpenNotification(item: Notification): void {
    this.closePopovers();
    this.notifications.markRead(item).subscribe();
    if (item.relatedTaskId) {
      void this.router.navigate(['/tasks', item.relatedTaskId]);
    }
  }

  protected onMarkAllRead(): void {
    this.notifications.markAllRead().subscribe((count) => {
      this.toast.info(this.i18n.dict().markedReadCount(count));
    });
  }

  protected async onLogOut(): Promise<void> {
    this.closePopovers();
    await this.auth.signOut('manual');
  }

  protected positionIndicator(): void {
    const nav = this.navRef()?.nativeElement;
    const ind = this.indicatorRef()?.nativeElement;
    if (!nav || !ind) return;

    const active = nav.querySelector<HTMLElement>('[aria-current="page"]');
    if (!active) {
      ind.style.opacity = '0';
      ind.style.transform = 'translateX(0px) scaleX(0)';
      return;
    }

    const navRect = nav.getBoundingClientRect();
    const activeRect = active.getBoundingClientRect();
    const offsetX = activeRect.left - navRect.left + 10;
    const width = Math.max(0, activeRect.width - 20);
    ind.style.opacity = '1';
    ind.style.insetInlineStart = 'auto';
    ind.style.left = '0px';
    ind.style.right = 'auto';
    ind.style.transform = `translateX(${offsetX}px) scaleX(${width})`;
  }
}
