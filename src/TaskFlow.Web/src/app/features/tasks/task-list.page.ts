import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { LucideMessageSquare, LucidePlus, LucideSearch } from '@lucide/angular';
import { LIMITS } from '../../core/api/limits';
import {
  Paginated,
  TASK_PRIORITIES,
  TASK_STATUSES,
  TaskListItem,
  TaskPriority,
  TaskStatus,
  User,
} from '../../core/api/models';
import { buildTaskHttpParams } from '../../core/api/tasks.api';
import { AuthStore } from '../../core/auth/auth.store';
import { isAdmin } from '../../core/domain/permissions';
import { TranslationService } from '../../core/i18n/translation.service';
import { StaggerDirective } from '../../shared/motion/stagger.directive';
import { TaskHeroService } from '../../shared/motion/task-hero.service';
import { AppAvatar } from '../../shared/ui/avatar';
import { AppButton, AppButtonLink } from '../../shared/ui/button';
import { AppDueTick } from '../../shared/ui/due-tick';
import { AppEmptyState } from '../../shared/ui/empty-state';
import { AppErrorState } from '../../shared/ui/error-state';
import { AppPaginator } from '../../shared/ui/paginator';
import { AppSkeleton } from '../../shared/ui/skeleton';
import { AppStatusDot } from '../../shared/ui/status-dot';

const PRIORITY_BAR: Record<TaskPriority, string> = {
  Low: 'bg-todo/50',
  Medium: 'bg-amber',
  High: 'bg-heat',
};

@Component({
  selector: 'app-task-list-page',
  imports: [
    RouterLink,
    StaggerDirective,
    AppButton,
    AppButtonLink,
    AppSkeleton,
    AppErrorState,
    AppEmptyState,
    AppDueTick,
    AppStatusDot,
    AppAvatar,
    AppPaginator,
    LucidePlus,
    LucideMessageSquare,
    LucideSearch,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section appStagger>
      <div class="flex items-center justify-between gap-4 flex-wrap">
        <h1 class="display text-3xl font-extrabold" tabindex="-1">{{ i18n.t('allTasks') }}</h1>
        <a routerLink="/tasks/new" appButton>
          <svg lucidePlus class="size-[18px]" [strokeWidth]="2" aria-hidden="true"></svg>
          <span>{{ i18n.t('newTask') }}</span>
        </a>
      </div>

      <!-- Filter bar synced to URL -->
      <div class="mt-6 flex flex-wrap items-center gap-2">
        <label class="relative w-full sm:w-56">
          <span class="sr-only">{{ i18n.t('searchTasks') }}</span>
          <svg
            lucideSearch
            class="size-4 absolute start-3 top-2.5 text-slate pointer-events-none"
            [strokeWidth]="1.8"
            aria-hidden="true"
          ></svg>
          <input
            type="search"
            class="tf-transition h-9 ps-9 pe-3 rounded-[var(--radius-control)] bg-panel border text-[13px] w-full hover:border-slate/50"
            [class.border-flow]="!!filters().search"
            [class.border-line]="!filters().search"
            [placeholder]="i18n.t('searchTasks')"
            [value]="filters().search ?? ''"
            (input)="onSearchInput($any($event.target).value)"
          />
        </label>

        <label class="relative">
          <span class="sr-only">{{ i18n.t('status') }}</span>
          <select
            class="tf-transition h-9 px-3 rounded-[var(--radius-control)] bg-panel border text-[13px] hover:border-slate/50"
            [class.border-flow]="!!filters().status"
            [class.text-flow]="!!filters().status"
            [class.font-bold]="!!filters().status"
            [class.border-line]="!filters().status"
            [value]="filters().status ?? ''"
            (change)="updateFilter('status', selectVal($event))"
          >
            <option value="">{{ i18n.t('status') }}: {{ i18n.t('any') }}</option>
            @for (s of statuses; track s) {
              <option [value]="s" [selected]="filters().status === s">{{ statusLabel(s) }}</option>
            }
          </select>
        </label>

        <label class="relative">
          <span class="sr-only">{{ i18n.t('priority') }}</span>
          <select
            class="tf-transition h-9 px-3 rounded-[var(--radius-control)] bg-panel border text-[13px] hover:border-slate/50"
            [class.border-flow]="!!filters().priority"
            [class.text-flow]="!!filters().priority"
            [class.font-bold]="!!filters().priority"
            [class.border-line]="!filters().priority"
            [value]="filters().priority ?? ''"
            (change)="updateFilter('priority', selectVal($event))"
          >
            <option value="">{{ i18n.t('priority') }}: {{ i18n.t('any') }}</option>
            @for (p of priorities; track p) {
              <option [value]="p" [selected]="filters().priority === p">
                {{ priorityLabel(p) }}
              </option>
            }
          </select>
        </label>

        @if (isAdmin()) {
          <label class="relative">
            <span class="sr-only">{{ i18n.t('assignee') }}</span>
            <select
              class="tf-transition h-9 px-3 rounded-[var(--radius-control)] bg-panel border text-[13px] hover:border-slate/50"
              [class.border-flow]="!!assigneeControlValue()"
              [class.text-flow]="!!assigneeControlValue()"
              [class.font-bold]="!!assigneeControlValue()"
              [class.border-line]="!assigneeControlValue()"
              [value]="assigneeControlValue()"
              (change)="onAssigneeFilterChange(selectVal($event))"
            >
              <option value="">{{ i18n.t('assignee') }}: {{ i18n.t('anyone') }}</option>
              <option value="unassigned" [selected]="filters().unassigned">
                {{ i18n.t('unassigned') }}
              </option>
              @for (u of activeUsers(); track u.id) {
                <option [value]="u.id" [selected]="filters().assignedToId === u.id">
                  {{ u.fullName }}
                </option>
              }
            </select>
          </label>
        }

        <label class="relative">
          <span class="sr-only">{{ i18n.t('createdBy') }}</span>
          <select
            class="tf-transition h-9 px-3 rounded-[var(--radius-control)] bg-panel border text-[13px] hover:border-slate/50"
            [class.border-flow]="!!filters().createdById"
            [class.text-flow]="!!filters().createdById"
            [class.font-bold]="!!filters().createdById"
            [class.border-line]="!filters().createdById"
            [value]="filters().createdById ?? ''"
            (change)="updateFilter('createdById', selectVal($event))"
          >
            <option value="">{{ i18n.t('createdBy') }}: {{ i18n.t('anyone') }}</option>
            @for (u of allUsers(); track u.id) {
              <option [value]="u.id" [selected]="filters().createdById === u.id">
                {{ u.fullName }}
              </option>
            }
          </select>
        </label>

        <label
          class="tf-transition inline-flex items-center gap-2 h-9 px-3 rounded-[var(--radius-control)] border text-[13px] cursor-pointer"
          [class.border-heat/40]="filters().overdue"
          [class.text-heat]="filters().overdue"
          [class.font-bold]="filters().overdue"
          [class.border-line]="!filters().overdue"
        >
          <input
            type="checkbox"
            class="accent-[var(--color-heat)]"
            [checked]="filters().overdue"
            (change)="updateFilter('overdue', $any($event.target).checked ? 'true' : null)"
          />
          <span>{{ i18n.t('overdueOnly') }}</span>
        </label>

        @if (hasActiveFilters()) {
          <app-button variant="ghost" size="sm" (click)="clearFilters()">
            {{ i18n.t('clearFilters') }}
          </app-button>
        }
      </div>

      <div class="mt-5">
        @if (tasks.isLoading()) {
          <app-skeleton kind="list" />
        } @else if (tasks.error()) {
          <app-error-state (retry)="tasks.reload()" />
        } @else if (tasks.value(); as page) {
          @if (page.items.length === 0) {
            @if (hasActiveFilters()) {
              <app-empty-state
                [message]="i18n.t('emptyFiltered')"
                [actionLabel]="i18n.t('clearFilters')"
                (action)="clearFilters()"
              />
            } @else {
              <app-empty-state
                [message]="i18n.t('emptyTasks')"
                [actionLabel]="i18n.t('newTask')"
                (action)="goToNewTask()"
              />
            }
          } @else {
            <!-- Desktop table -->
            <div class="bg-panel border border-line rounded-2xl overflow-hidden hidden md:block">
              <table class="w-full text-[14px]">
                <thead class="text-slate">
                  <tr class="border-b border-line">
                    <th class="text-start font-normal px-5 h-11">{{ i18n.t('colTask') }}</th>
                    <th class="text-start font-normal px-3">{{ i18n.t('status') }}</th>
                    <th class="text-start font-normal px-3">{{ i18n.t('due') }}</th>
                    <th class="text-start font-normal px-3">{{ i18n.t('assignedTo') }}</th>
                    <th class="px-5 text-center">
                      <svg
                        lucideMessageSquare
                        class="size-4 inline rtl:-scale-x-100"
                        [strokeWidth]="1.8"
                        aria-hidden="true"
                      ></svg>
                      <span class="sr-only">{{ i18n.t('commentsCol') }}</span>
                    </th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-line">
                  @for (item of page.items; track item.id) {
                    <tr
                      class="tf-transition hover:bg-ink/[.03] cursor-pointer"
                      (click)="openTask(item.id, $event)"
                    >
                      <td class="px-5 h-12">
                        <span class="flex items-center gap-3">
                          <span
                            class="w-1 h-5 rounded shrink-0"
                            [class]="priorityBar(item.priority)"
                            aria-hidden="true"
                          ></span>
                          <a
                            [routerLink]="['/tasks', item.id]"
                            [state]="{ from: 'tasks' }"
                            class="tf-task-title font-bold hover:underline"
                            (click)="$event.stopPropagation(); markHero($event)"
                          >
                            <bdi>{{ item.title }}</bdi>
                          </a>
                          <span class="text-slate text-[13px]">
                            {{ priorityLabel(item.priority) }}
                          </span>
                        </span>
                      </td>
                      <td class="px-3"><app-status-dot [status]="item.status" /></td>
                      <td class="px-3 text-[13px]">
                        <app-due-tick [dueDate]="item.dueDate" [status]="item.status" />
                      </td>
                      <td class="px-3">
                        <span class="flex items-center gap-2">
                          <app-avatar [name]="item.assignedToName" size="sm" />
                          <span [class.text-slate]="!item.assignedToName">
                            <bdi>{{ item.assignedToName || i18n.t('unassigned') }}</bdi>
                          </span>
                        </span>
                      </td>
                      <td class="px-5 text-slate num text-center">
                        {{ i18n.formatNumber(item.commentsCount) }}
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>

            <!-- Phone stacked cards -->
            <ul class="md:hidden space-y-2.5">
              @for (item of page.items; track item.id) {
                <li>
                  <a
                    [routerLink]="['/tasks', item.id]"
                    [state]="{ from: 'tasks' }"
                    class="tf-transition tf-lift block bg-panel border border-line rounded-xl relative ps-4 pe-3 py-3"
                    (click)="markHero($event)"
                  >
                    <span
                      class="absolute start-0 top-3 bottom-3 w-1 rounded-e"
                      [class]="priorityBar(item.priority)"
                      aria-hidden="true"
                    ></span>
                    <p class="tf-task-title font-bold"><bdi>{{ item.title }}</bdi></p>
                    <div class="flex items-center gap-3 mt-2 text-[13px]">
                      <app-status-dot [status]="item.status" />
                      <app-due-tick [dueDate]="item.dueDate" [status]="item.status" />
                      <span class="ms-auto">
                        <app-avatar [name]="item.assignedToName" size="sm" />
                      </span>
                    </div>
                  </a>
                </li>
              }
            </ul>

            <app-paginator
              [pageNumber]="page.pageNumber"
              [pageSize]="page.pageSize"
              [totalCount]="page.totalCount"
              [hasPreviousPage]="page.hasPreviousPage"
              [hasNextPage]="page.hasNextPage"
              (pageChange)="onPageChange($event)"
              (pageSizeChange)="onPageSizeChange($event)"
            />
          }
        }
      </div>
    </section>
  `,
})
export class TaskListPage {
  protected readonly i18n = inject(TranslationService);
  private readonly auth = inject(AuthStore);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly hero = inject(TaskHeroService);

  protected readonly isAdmin = computed(() => isAdmin(this.auth.user()));
  protected readonly statuses = TASK_STATUSES;
  protected readonly priorities = TASK_PRIORITIES;

  private readonly queryParams = toSignal(this.route.queryParamMap, {
    initialValue: this.route.snapshot.queryParamMap,
  });

  protected readonly filters = computed(() => {
    const q = this.queryParams();
    const status = q.get('status') as TaskStatus | null;
    const priority = q.get('priority') as TaskPriority | null;
    const unassigned = q.get('unassigned') === 'true';
    const assignedToId = unassigned ? undefined : (q.get('assignedToId') ?? undefined);
    const createdById = q.get('createdById') ?? undefined;
    const overdue = q.get('overdue') === 'true';
    const search = q.get('q') ?? undefined;
    const page = Math.max(1, Number(q.get('page')) || 1);
    const pageSize = Number(q.get('pageSize')) || LIMITS.DEFAULT_PAGE_SIZE;
    return {
      status: status && TASK_STATUSES.includes(status) ? status : undefined,
      priority: priority && TASK_PRIORITIES.includes(priority) ? priority : undefined,
      unassigned,
      assignedToId,
      createdById,
      overdue,
      search,
      page,
      pageSize,
    };
  });

  protected readonly assigneeControlValue = computed(() => {
    const f = this.filters();
    if (f.unassigned) return 'unassigned';
    return f.assignedToId ?? '';
  });

  protected readonly hasActiveFilters = computed(() => {
    const f = this.filters();
    return Boolean(
      f.status || f.priority || f.unassigned || f.assignedToId || f.createdById || f.overdue || f.search,
    );
  });

  protected readonly tasks = httpResource<Paginated<TaskListItem>>(() => ({
    url: '/api/tasks',
    params: buildTaskHttpParams(this.filters()),
  }));

  private readonly usersResource = httpResource<User[]>(() => '/api/users');
  protected readonly allUsers = computed(() => this.usersResource.value() ?? []);
  protected readonly activeUsers = computed(() => this.allUsers().filter((u) => u.isActive));

  protected priorityBar(priority: TaskPriority): string {
    return PRIORITY_BAR[priority];
  }

  protected statusLabel(status: TaskStatus): string {
    const d = this.i18n.dict();
    return status === 'Todo' ? d.statusTodo : status === 'InProgress' ? d.statusInProgress : d.statusDone;
  }

  protected priorityLabel(priority: TaskPriority): string {
    const d = this.i18n.dict();
    return priority === 'Low' ? d.priorityLow : priority === 'Medium' ? d.priorityMedium : d.priorityHigh;
  }

  protected selectVal(event: Event): string | null {
    const val = (event.target as HTMLSelectElement).value;
    return val || null;
  }

  protected updateFilter(key: string, value: string | null): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { [key]: value, page: 1 },
      queryParamsHandling: 'merge',
    });
  }

  protected onAssigneeFilterChange(value: string | null): void {
    const patch =
      value === 'unassigned'
        ? { unassigned: 'true', assignedToId: null, page: 1 }
        : { unassigned: null, assignedToId: value, page: 1 };
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: patch,
      queryParamsHandling: 'merge',
    });
  }

  private searchTimer: ReturnType<typeof setTimeout> | null = null;

  protected onSearchInput(value: string): void {
    if (this.searchTimer) clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => {
      this.updateFilter('q', value.trim() || null);
    }, 250);
  }

  protected clearFilters(): void {
    if (this.searchTimer) clearTimeout(this.searchTimer);
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: {
        status: null,
        priority: null,
        unassigned: null,
        assignedToId: null,
        createdById: null,
        overdue: null,
        q: null,
        page: null,
      },
      queryParamsHandling: 'merge',
    });
  }

  protected onPageChange(page: number): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { page },
      queryParamsHandling: 'merge',
    });
  }

  protected onPageSizeChange(pageSize: number): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { pageSize, page: 1 },
      queryParamsHandling: 'merge',
    });
  }

  protected openTask(id: string, event: Event): void {
    const titleEl = (event.currentTarget as HTMLElement).querySelector<HTMLElement>('.tf-task-title');
    this.hero.markSource(titleEl);
    void this.router.navigate(['/tasks', id], { state: { from: 'tasks' } });
  }

  protected markHero(event: Event): void {
    const el = (event.currentTarget as HTMLElement).querySelector<HTMLElement>('.tf-task-title')
      ?? (event.currentTarget as HTMLElement);
    this.hero.markSource(el);
  }

  protected goToNewTask(): void {
    void this.router.navigate(['/tasks/new']);
  }
}
