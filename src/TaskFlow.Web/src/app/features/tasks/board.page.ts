import { httpResource } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  OnDestroy,
  OnInit,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import {
  LucideEllipsis,
  LucideLock,
  LucideMessageSquare,
  LucidePlus,
  LucideSearch,
} from '@lucide/angular';
import {
  TASK_PRIORITIES,
  TASK_STATUSES,
  TaskListItem,
  TaskPriority,
  TaskStatus,
  User,
} from '../../core/api/models';
import { AuthStore } from '../../core/auth/auth.store';
import { canMoveTask } from '../../core/domain/permissions';
import { TranslationService } from '../../core/i18n/translation.service';
import { RealtimeClient } from '../../core/realtime/realtime-client';
import { StaggerDirective } from '../../shared/motion/stagger.directive';
import { TaskHeroService } from '../../shared/motion/task-hero.service';
import { AppAvatar } from '../../shared/ui/avatar';
import { AppButton, AppButtonLink } from '../../shared/ui/button';
import { AppDueTick } from '../../shared/ui/due-tick';
import { AppEmptyState } from '../../shared/ui/empty-state';
import { AppErrorState } from '../../shared/ui/error-state';
import { AppSkeleton } from '../../shared/ui/skeleton';
import { BoardDragService } from './board-drag.service';
import { BoardFilters, BoardStore } from './board.store';

const STATUS_BAND: Record<TaskStatus, string> = {
  Todo: 'bg-todo',
  InProgress: 'bg-flow',
  Done: 'bg-ink',
};

const PRIORITY_BAR: Record<TaskPriority, string> = {
  Low: 'bg-todo/50',
  Medium: 'bg-amber',
  High: 'bg-heat',
};

@Component({
  selector: 'app-board-page',
  providers: [BoardStore, BoardDragService],
  imports: [
    RouterLink,
    StaggerDirective,
    AppButton,
    AppButtonLink,
    AppSkeleton,
    AppErrorState,
    AppEmptyState,
    AppDueTick,
    AppAvatar,
    LucidePlus,
    LucideLock,
    LucideEllipsis,
    LucideMessageSquare,
    LucideSearch,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    '(document:click)': 'onDocumentClick($event)',
    '(document:keydown.escape)': 'closeCardMenu()',
  },
  template: `
    <section>
      <div class="flex items-center justify-between gap-4 flex-wrap">
        <h1 class="display text-3xl font-extrabold" tabindex="-1">{{ i18n.t('board') }}</h1>
        <a routerLink="/tasks/new" [state]="{ from: 'board' }" appButton>
          <svg lucidePlus class="size-[18px]" [strokeWidth]="2" aria-hidden="true"></svg>
          <span>{{ i18n.t('newTask') }}</span>
        </a>
      </div>

      <p class="text-slate mt-1">
        {{ store.supportsPosition() ? i18n.t('boardHint') : i18n.t('boardHintStatusOnly') }}
      </p>

      <!-- Filters -->
      <div class="mt-4 mb-6 flex flex-wrap items-center gap-2">
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
            [class.border-flow]="!!store.filters().search"
            [class.border-line]="!store.filters().search"
            [placeholder]="i18n.t('searchTasks')"
            [value]="store.filters().search ?? ''"
            (input)="onSearchInput($any($event.target).value)"
          />
        </label>

        <label class="relative">
          <span class="sr-only">{{ i18n.t('priority') }}</span>
          <select
            class="tf-transition h-9 px-3 rounded-[var(--radius-control)] bg-panel border text-[13px] hover:border-slate/50"
            [class.border-flow]="!!store.filters().priority"
            [class.text-flow]="!!store.filters().priority"
            [class.font-bold]="!!store.filters().priority"
            [class.border-line]="!store.filters().priority"
            [value]="store.filters().priority ?? ''"
            (change)="setPriorityFilter(selectVal($event))"
          >
            <option value="">{{ i18n.t('priority') }}: {{ i18n.t('any') }}</option>
            @for (p of priorities; track p) {
              <option [value]="p">{{ priorityLabel(p) }}</option>
            }
          </select>
        </label>

        <label class="relative">
          <span class="sr-only">{{ i18n.t('assignee') }}</span>
          <select
            class="tf-transition h-9 px-3 rounded-[var(--radius-control)] bg-panel border text-[13px] hover:border-slate/50"
            [class.border-flow]="!!assigneeFilterValue()"
            [class.text-flow]="!!assigneeFilterValue()"
            [class.font-bold]="!!assigneeFilterValue()"
            [class.border-line]="!assigneeFilterValue()"
            [value]="assigneeFilterValue()"
            (change)="setAssigneeFilter(selectVal($event))"
          >
            <option value="">{{ i18n.t('assignee') }}: {{ i18n.t('anyone') }}</option>
            <option value="unassigned">{{ i18n.t('unassigned') }}</option>
            @for (u of activeUsers(); track u.id) {
              <option [value]="u.id">{{ u.fullName }}</option>
            }
          </select>
        </label>

        <label class="relative">
          <span class="sr-only">{{ i18n.t('createdBy') }}</span>
          <select
            class="tf-transition h-9 px-3 rounded-[var(--radius-control)] bg-panel border text-[13px] hover:border-slate/50"
            [class.border-flow]="!!store.filters().createdById"
            [class.text-flow]="!!store.filters().createdById"
            [class.font-bold]="!!store.filters().createdById"
            [class.border-line]="!store.filters().createdById"
            [value]="store.filters().createdById ?? ''"
            (change)="setFilter({ createdById: selectVal($event) })"
          >
            <option value="">{{ i18n.t('createdBy') }}: {{ i18n.t('anyone') }}</option>
            @for (u of allUsers(); track u.id) {
              <option [value]="u.id">{{ u.fullName }}</option>
            }
          </select>
        </label>

        <label
          class="tf-transition inline-flex items-center gap-2 h-9 px-3 rounded-[var(--radius-control)] border text-[13px] cursor-pointer"
          [class.border-heat/40]="store.filters().overdue"
          [class.text-heat]="store.filters().overdue"
          [class.font-bold]="store.filters().overdue"
          [class.border-line]="!store.filters().overdue"
        >
          <input
            type="checkbox"
            class="accent-[var(--color-heat)]"
            [checked]="!!store.filters().overdue"
            (change)="setFilter({ overdue: $any($event.target).checked || undefined })"
          />
          <span>{{ i18n.t('overdueOnly') }}</span>
        </label>

        @if (store.hasActiveFilters()) {
          <app-button variant="ghost" size="sm" (click)="clearFilters()">
            {{ i18n.t('clearFilters') }}
          </app-button>
        }
      </div>

      @if (store.loading()) {
        <app-skeleton kind="board" />
      } @else if (store.error()) {
        <app-error-state [detail]="store.error()" (retry)="store.load()" />
      } @else if (store.isEmpty() && !store.hasActiveFilters()) {
        <app-empty-state
          [message]="i18n.t('emptyTasks')"
          [actionLabel]="i18n.t('newTask')"
          (action)="goToNewTask()"
        />
      } @else {
        <div
          #boardScroll
          class="flex gap-5 overflow-x-auto snap-x pb-4"
          appStagger
        >
          @for (status of statuses; track status) {
            <div class="snap-start shrink-0 w-[86vw] sm:w-[310px]">
              <div class="h-1.5 rounded-full mb-3" [class]="statusBand(status)"></div>
              <h2 class="flex items-baseline gap-2 mb-3 text-[17px] font-bold">
                <span>{{ statusLabel(status) }}</span>
                <span class="text-slate font-normal text-sm num inline-block">
                  {{ i18n.formatNumber(store.meta()[status].totalCount) }}
                </span>
              </h2>

              <ul
                [attr.data-col]="status"
                class="tf-transition space-y-2.5 min-h-40 rounded-2xl p-1 -m-1"
              >
                @for (task of store.columns()[status]; track task.id) {
                  @let locked = !mayMove(task);
                  <li
                    [attr.data-task-id]="task.id"
                    class="tf-transition bg-panel border border-line rounded-xl relative ps-4 pe-3 py-3 select-none"
                    [class.opacity-75]="locked"
                    [class.cursor-pointer]="locked"
                    [class.tf-lift]="!locked && openMenuTaskId() !== task.id"
                    [class.cursor-grab]="!locked"
                    [class.touch-none]="!locked"
                    [class.z-30]="openMenuTaskId() === task.id"
                    [class.tf-landed]="store.landedTaskId() === task.id"
                    tabindex="0"
                    (pointerdown)="onCardPointerDown($event, task, locked)"
                    (click)="onCardClick($event, task)"
                    (keydown.enter)="onCardClick($event, task)"
                  >
                    <span
                      class="absolute start-0 top-3 bottom-3 w-1 rounded-e"
                      [class]="priorityBar(task.priority)"
                      aria-hidden="true"
                    ></span>

                    <div class="flex items-start justify-between gap-2">
                      <p class="tf-task-title font-bold leading-snug"><bdi>{{ task.title }}</bdi></p>

                      @if (locked) {
                        <span class="text-slate shrink-0" [title]="i18n.t('lockedTooltip')">
                          <svg
                            lucideLock
                            class="size-4"
                            [strokeWidth]="2"
                            aria-hidden="true"
                          ></svg>
                          <span class="sr-only">{{ i18n.t('lockedShort') }}</span>
                        </span>
                      } @else {
                        <button
                          type="button"
                          data-move-trigger
                          aria-haspopup="menu"
                          [attr.aria-expanded]="openMenuTaskId() === task.id"
                          [attr.aria-label]="i18n.dict().moveTaskAria(task.title)"
                          class="tf-transition shrink-0 -m-1 p-1 rounded-md text-slate hover:text-ink hover:bg-ink/5 cursor-pointer"
                          (click)="toggleCardMenu($event, task.id)"
                        >
                          <svg
                            lucideEllipsis
                            class="size-4"
                            [strokeWidth]="2"
                            aria-hidden="true"
                          ></svg>
                        </button>
                      }
                    </div>

                    <div class="flex items-center gap-3 mt-3 text-[13px]">
                      <app-due-tick [dueDate]="task.dueDate" [status]="task.status" />
                      @if (task.commentsCount > 0) {
                        <span class="text-slate num inline-flex items-center gap-1">
                          <svg
                            lucideMessageSquare
                            class="size-3.5 rtl:-scale-x-100"
                            [strokeWidth]="2"
                            aria-hidden="true"
                          ></svg>
                          <span>{{ i18n.formatNumber(task.commentsCount) }}</span>
                        </span>
                      }
                      <span class="ms-auto">
                        <app-avatar [name]="task.assignedToName" size="sm" />
                      </span>
                    </div>

                    @if (openMenuTaskId() === task.id) {
                      <div
                        role="menu"
                        class="tf-menu-in absolute end-2 top-10 z-20 w-48 bg-panel border border-line rounded-2xl p-1.5 shadow-xl"
                      >
                        @for (dest of otherStatuses(task.status); track dest) {
                          <button
                            type="button"
                            role="menuitem"
                            class="tf-transition w-full text-start flex items-center gap-2.5 h-9 px-3 rounded-lg hover:bg-ink/5 text-[14px] cursor-pointer"
                            (click)="onMoveMenuPick(task, dest)"
                          >
                            <span
                              class="size-2 rounded-full shrink-0"
                              [class]="statusBand(dest)"
                              aria-hidden="true"
                            ></span>
                            <span>{{ i18n.dict().moveToColumn(statusLabel(dest)) }}</span>
                          </button>
                        }
                        <div class="border-t border-line my-1"></div>
                        <button
                          type="button"
                          role="menuitem"
                          class="tf-transition w-full text-start flex items-center gap-2.5 h-9 px-3 rounded-lg hover:bg-ink/5 text-[14px] cursor-pointer"
                          (click)="openTaskFromMenu(task.id)"
                        >
                          {{ i18n.t('open') }}
                        </button>
                      </div>
                    }
                  </li>
                }
              </ul>

              @if (store.meta()[status].hasNextPage) {
                <div class="mt-3">
                  <app-button
                    variant="quiet"
                    size="sm"
                    [fullWidth]="true"
                    [busy]="store.meta()[status].loadingMore"
                    (click)="store.loadMore(status)"
                  >
                    {{ i18n.t('loadMore') }}
                  </app-button>
                </div>
              }
            </div>
          }
        </div>
      }
    </section>
  `,
})
export class BoardPage implements OnInit, OnDestroy {
  protected readonly store = inject(BoardStore);
  protected readonly i18n = inject(TranslationService);
  private readonly auth = inject(AuthStore);
  private readonly drag = inject(BoardDragService);
  private readonly hero = inject(TaskHeroService);
  private readonly realtime = inject(RealtimeClient);
  private readonly router = inject(Router);

  private readonly boardScroll = viewChild<ElementRef<HTMLElement>>('boardScroll');
  private unsubscribeBoardChanged: (() => void) | null = null;
  private syncTimer: ReturnType<typeof setTimeout> | null = null;
  private searchTimer: ReturnType<typeof setTimeout> | null = null;

  protected readonly statuses = TASK_STATUSES;
  protected readonly priorities = TASK_PRIORITIES;
  protected readonly openMenuTaskId = signal<string | null>(null);

  private readonly usersResource = httpResource<User[]>(() => '/api/users');
  protected readonly allUsers = computed(() => this.usersResource.value() ?? []);
  protected readonly activeUsers = computed(() => this.allUsers().filter((u) => u.isActive));

  protected readonly assigneeFilterValue = computed(() => {
    const f = this.store.filters();
    if (f.unassigned) return 'unassigned';
    return f.assignedToId ?? '';
  });

  ngOnInit(): void {
    this.store.load();
    this.unsubscribeBoardChanged = this.realtime.onBoardChanged(() => {
      if (this.drag.wasJustDragging()) return;
      if (this.syncTimer) clearTimeout(this.syncTimer);
      this.syncTimer = setTimeout(() => this.store.load(this.store.filters(), true), 250);
    });
  }

  ngOnDestroy(): void {
    this.unsubscribeBoardChanged?.();
    if (this.syncTimer) clearTimeout(this.syncTimer);
    if (this.searchTimer) clearTimeout(this.searchTimer);
    this.drag.destroy();
  }

  protected statusBand(status: TaskStatus): string {
    return STATUS_BAND[status];
  }

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

  protected otherStatuses(current: TaskStatus): TaskStatus[] {
    return TASK_STATUSES.filter((s) => s !== current);
  }

  protected mayMove(task: TaskListItem): boolean {
    return canMoveTask(task, this.auth.user());
  }

  protected selectVal(event: Event): string | undefined {
    const v = (event.target as HTMLSelectElement).value;
    return v || undefined;
  }

  protected setPriorityFilter(value: string | undefined): void {
    const priority =
      value && TASK_PRIORITIES.includes(value as TaskPriority)
        ? (value as TaskPriority)
        : undefined;
    this.setFilter({ priority });
  }

  protected setFilter(patch: Partial<BoardFilters>): void {
    void this.store.load({ ...this.store.filters(), ...patch });
  }

  protected setAssigneeFilter(value: string | undefined): void {
    const next: BoardFilters = {
      ...this.store.filters(),
      unassigned: value === 'unassigned' ? true : undefined,
      assignedToId: value && value !== 'unassigned' ? value : undefined,
    };
    void this.store.load(next);
  }

  protected onSearchInput(value: string): void {
    if (this.searchTimer) clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => {
      this.setFilter({ search: value.trim() || undefined });
    }, 250);
  }

  protected clearFilters(): void {
    if (this.searchTimer) clearTimeout(this.searchTimer);
    this.store.load({});
  }

  protected onCardPointerDown(event: PointerEvent, task: TaskListItem, locked: boolean): void {
    if (locked) return;
    const target = event.target as HTMLElement | null;
    if (target?.closest('button, [role="menu"]')) return;

    const cardEl = event.currentTarget as HTMLElement;
    const container = this.boardScroll()?.nativeElement;
    if (!container) return;

    this.closeCardMenu();
    this.drag.beginPointerDown({
      event,
      cardEl,
      scrollContainer: container,
      taskId: task.id,
      sourceStatus: task.status,
      allowReorder: this.store.supportsPosition(),
      onDrop: ({ taskId, targetStatus, beforeTaskId }) => {
        const current = this.store.findTask(taskId);
        if (current) {
          void this.store.moveTask(current, targetStatus, beforeTaskId);
        }
      },
    });
  }

  protected onCardClick(event: Event, task: TaskListItem): void {
    if (this.drag.wasJustDragging()) return;
    const target = event.target as HTMLElement | null;
    if (target?.closest('button, [role="menu"]')) return;

    const titleEl = (event.currentTarget as HTMLElement).querySelector<HTMLElement>(
      '.tf-task-title',
    );
    this.hero.markSource(titleEl);
    void this.router.navigate(['/tasks', task.id], { state: { from: 'board' } });
  }

  protected toggleCardMenu(event: Event, taskId: string): void {
    event.stopPropagation();
    this.openMenuTaskId.update((cur) => (cur === taskId ? null : taskId));
  }

  protected onDocumentClick(event: Event): void {
    const target = event.target as HTMLElement | null;
    if (target?.closest('[role="menu"], [data-move-trigger]')) return;
    this.closeCardMenu();
  }

  protected closeCardMenu(): void {
    this.openMenuTaskId.set(null);
  }

  protected onMoveMenuPick(task: TaskListItem, targetStatus: TaskStatus): void {
    this.closeCardMenu();
    void this.store.moveTask(task, targetStatus, null);
  }

  protected openTaskFromMenu(taskId: string): void {
    this.closeCardMenu();
    void this.router.navigate(['/tasks', taskId], { state: { from: 'board' } });
  }

  protected goToNewTask(): void {
    void this.router.navigate(['/tasks/new'], { state: { from: 'board' } });
  }
}
