import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LIMITS } from '../../core/api/limits';
import { Dashboard, Paginated, TaskListItem, TaskPriority } from '../../core/api/models';
import { isDueWithinDays, parseUtcDate } from '../../core/domain/due-date';
import { TranslationService } from '../../core/i18n/translation.service';
import { StaggerDirective } from '../../shared/motion/stagger.directive';
import { TaskHeroService } from '../../shared/motion/task-hero.service';
import { AppAvatar } from '../../shared/ui/avatar';
import { AppDueTick } from '../../shared/ui/due-tick';
import { AppErrorState } from '../../shared/ui/error-state';
import { AppSkeleton } from '../../shared/ui/skeleton';
import { AppStatusDot } from '../../shared/ui/status-dot';

const PRIORITY_BAR: Record<TaskPriority, string> = {
  Low: 'bg-todo/50',
  Medium: 'bg-amber',
  High: 'bg-heat',
};

@Component({
  selector: 'app-dashboard-page',
  imports: [
    RouterLink,
    StaggerDirective,
    AppSkeleton,
    AppErrorState,
    AppDueTick,
    AppStatusDot,
    AppAvatar,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section appStagger>
      <h1 class="display text-[28px] sm:text-[34px] leading-tight font-extrabold" tabindex="-1">
        {{ headline() }}
      </h1>
      <p class="text-slate mt-2">{{ todayLabel() }}</p>

      @if (dashboard.isLoading()) {
        <app-skeleton kind="dashboard" />
      } @else if (dashboard.error()) {
        <div class="mt-8">
          <app-error-state (retry)="reload()" />
        </div>
      } @else if (dashboard.value(); as data) {
        <!-- Status strip -->
        <div class="mt-10">
          <div class="flex items-baseline justify-between mb-3">
            <h2 class="text-lg font-bold">{{ i18n.dict().teamTasks(data.tasks.total) }}</h2>
            <a routerLink="/tasks/board" class="text-sm text-flow font-bold hover:underline">
              {{ i18n.t('openBoard') }}
            </a>
          </div>

          <div
            class="flex w-full h-24 rounded-2xl overflow-hidden tf-grow gap-[3px]"
            role="img"
            [attr.aria-label]="
              i18n.dict().stripLabel(data.tasks.todo, data.tasks.inProgress, data.tasks.done)
            "
          >
            <a
              routerLink="/tasks"
              [queryParams]="{ status: 'Todo' }"
              class="tf-transition bg-todo/30 hover:bg-todo/45 text-ink flex flex-col justify-end p-3 min-w-28 basis-0"
              [style.flex-grow]="Math.max(data.tasks.todo, 1)"
            >
              <span class="display text-3xl font-extrabold num">
                {{ i18n.formatNumber(data.tasks.todo) }}
              </span>
              <span class="text-sm">{{ i18n.t('statusTodo') }}</span>
            </a>

            <a
              routerLink="/tasks"
              [queryParams]="{ status: 'InProgress' }"
              class="tf-transition bg-flow hover:bg-flow-deep text-on-flow flex flex-col justify-end p-3 min-w-28 basis-0"
              [style.flex-grow]="Math.max(data.tasks.inProgress, 1)"
            >
              <span class="display text-3xl font-extrabold num">
                {{ i18n.formatNumber(data.tasks.inProgress) }}
              </span>
              <span class="text-sm">{{ i18n.t('statusInProgress') }}</span>
            </a>

            <a
              routerLink="/tasks"
              [queryParams]="{ status: 'Done' }"
              class="tf-transition bg-ink text-paper hover:opacity-90 flex flex-col justify-end p-3 min-w-28 basis-0"
              [style.flex-grow]="Math.max(data.tasks.done, 1)"
            >
              <span class="display text-3xl font-extrabold num">
                {{ i18n.formatNumber(data.tasks.done) }}
              </span>
              <span class="text-sm">{{ i18n.t('statusDone') }}</span>
            </a>
          </div>
        </div>

        <!-- Due this week + Your work -->
        <div class="grid lg:grid-cols-[1fr_300px] gap-6 mt-10">
          <div class="bg-panel border border-line rounded-2xl p-6">
            <div class="flex items-baseline justify-between gap-2">
              <h2 class="text-lg font-bold">{{ i18n.t('dueThisWeek') }}</h2>
              @if (data.overdueTasks > 0) {
                <a
                  routerLink="/tasks"
                  [queryParams]="{ overdue: 'true' }"
                  class="text-heat font-bold text-sm hover:underline"
                >
                  {{ i18n.dict().overdueCount(data.overdueTasks) }}
                </a>
              }
            </div>

            @if (dueThisWeek().length === 0) {
              <p class="text-slate mt-5 text-[14px]">{{ i18n.t('nothingDue') }}</p>
            } @else {
              <ol class="mt-5 space-y-1.5">
                @for (item of dueThisWeek(); track item.id) {
                  <li>
                    <a
                      [routerLink]="['/tasks', item.id]"
                      [state]="{ from: 'dashboard' }"
                      class="tf-transition flex items-center gap-4 px-3 py-2.5 rounded-xl hover:bg-ink/[.04]"
                      (click)="markHero($event)"
                    >
                      <app-due-tick [dueDate]="item.dueDate" [status]="item.status" />
                      <span class="tf-task-title flex-1 min-w-0 font-bold truncate">
                        <bdi>{{ item.title }}</bdi>
                      </span>
                      <span class="text-[13px] text-slate shrink-0 hidden sm:inline">
                        <app-status-dot [status]="item.status" />
                      </span>
                    </a>
                  </li>
                }
              </ol>
            }
          </div>

          <div class="bg-panel border border-line rounded-2xl p-6 h-fit">
            <h2 class="text-lg font-bold">{{ i18n.t('yourWork') }}</h2>
            <dl class="mt-4 divide-y divide-line text-[14px]">
              <div class="flex justify-between py-2.5">
                <dt class="text-slate"><app-status-dot status="Todo" /></dt>
                <dd class="num">{{ i18n.formatNumber(data.myAssignedTasks.todo) }}</dd>
              </div>
              <div class="flex justify-between py-2.5">
                <dt class="text-slate"><app-status-dot status="InProgress" /></dt>
                <dd class="num">{{ i18n.formatNumber(data.myAssignedTasks.inProgress) }}</dd>
              </div>
              <div class="flex justify-between py-2.5">
                <dt class="text-slate"><app-status-dot status="Done" /></dt>
                <dd class="num">{{ i18n.formatNumber(data.myAssignedTasks.done) }}</dd>
              </div>
              <div class="flex justify-between py-2.5">
                <dt class="text-slate">{{ i18n.t('openCreated') }}</dt>
                <dd class="num">{{ i18n.formatNumber(data.myOpenCreatedTasks) }}</dd>
              </div>
              <div class="flex justify-between py-2.5">
                <dt class="text-slate">{{ i18n.t('unreadNotifications') }}</dt>
                <dd class="num" [class.text-heat]="data.unreadNotifications > 0">
                  {{ i18n.formatNumber(data.unreadNotifications) }}
                </dd>
              </div>
            </dl>
          </div>
        </div>

        <!-- Recent tasks -->
        <div class="mt-10">
          <h2 class="text-lg font-bold mb-3">{{ i18n.t('recentTasks') }}</h2>
          @if (data.recentTasks.length === 0) {
            <p class="bg-panel border border-line rounded-2xl px-5 py-6 text-slate text-[14px]">
              {{ i18n.t('noRecentTasks') }}
            </p>
          } @else {
            <ul class="bg-panel border border-line rounded-2xl divide-y divide-line">
              @for (item of data.recentTasks; track item.id) {
                <li>
                  <a
                    [routerLink]="['/tasks', item.id]"
                    [state]="{ from: 'dashboard' }"
                    class="tf-transition flex items-center gap-4 px-5 h-12 hover:bg-ink/[.03]"
                    (click)="markHero($event)"
                  >
                    <span
                      class="w-1 h-5 rounded shrink-0"
                      [class]="priorityBar(item.priority)"
                      aria-hidden="true"
                    ></span>
                    <span class="tf-task-title flex-1 min-w-0 font-bold truncate">
                      <bdi>{{ item.title }}</bdi>
                    </span>
                    <span class="text-[13px] hidden sm:inline">
                      <app-status-dot [status]="item.status" />
                    </span>
                    <span class="text-[13px] hidden md:inline">
                      <app-due-tick [dueDate]="item.dueDate" [status]="item.status" />
                    </span>
                    <app-avatar [name]="item.assignedToName" size="sm" />
                  </a>
                </li>
              }
            </ul>
          }
        </div>
      }
    </section>
  `,
})
export class DashboardPage {
  protected readonly i18n = inject(TranslationService);
  private readonly hero = inject(TaskHeroService);
  protected readonly Math = Math;

  protected readonly dashboard = httpResource<Dashboard>(() => '/api/dashboard');
  private readonly tasksPool = httpResource<Paginated<TaskListItem>>(() => ({
    url: '/api/tasks',
    params: { pageSize: String(LIMITS.MAX_PAGE_SIZE) },
  }));

  protected readonly todayLabel = computed(() =>
    this.i18n.formatDate(new Date(), { weekday: 'long', day: 'numeric', month: 'long' }),
  );

  protected readonly headline = computed(() => {
    const data = this.dashboard.value();
    if (!data) return this.i18n.t('today');
    return this.i18n.dict().dashboardHeadline(data.myAssignedTasks.inProgress, data.overdueTasks);
  });

  protected readonly dueThisWeek = computed(() => {
    const items = this.tasksPool.value()?.items ?? [];
    const now = new Date();
    return items
      .filter((t) => isDueWithinDays(t.dueDate, t.status, LIMITS.DUE_SOON_DAYS, now))
      .sort((a, b) => {
        const ta = a.dueDate ? parseUtcDate(a.dueDate).getTime() : Number.MAX_SAFE_INTEGER;
        const tb = b.dueDate ? parseUtcDate(b.dueDate).getTime() : Number.MAX_SAFE_INTEGER;
        return ta - tb;
      })
      .slice(0, 8);
  });

  protected priorityBar(priority: TaskPriority): string {
    return PRIORITY_BAR[priority];
  }

  protected markHero(event: Event): void {
    const titleEl = (event.currentTarget as HTMLElement).querySelector<HTMLElement>(
      '.tf-task-title',
    );
    this.hero.markSource(titleEl);
  }

  protected reload(): void {
    this.dashboard.reload();
    this.tasksPool.reload();
  }
}
