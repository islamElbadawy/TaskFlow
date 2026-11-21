import { httpResource } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  effect,
  inject,
  input,
  linkedSignal,
  signal,
  viewChild,
} from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { LucideChevronLeft, LucidePencil } from '@lucide/angular';
import { finalize } from 'rxjs';
import { CommentsApi } from '../../core/api/comments.api';
import { LIMITS } from '../../core/api/limits';
import {
  Comment,
  Paginated,
  TASK_STATUSES,
  Task,
  TaskPriority,
  TaskStatus,
  User,
} from '../../core/api/models';
import { TasksApi } from '../../core/api/tasks.api';
import { AuthStore } from '../../core/auth/auth.store';
import {
  canDeleteComment,
  canDeleteTask,
  canEditComment,
  canEditTask,
  canMoveTask,
} from '../../core/domain/permissions';
import { ApiError } from '../../core/http/api-error';
import { DateFmtPipe } from '../../core/i18n/date-fmt.pipe';
import { RelTimePipe } from '../../core/i18n/rel-time.pipe';
import { TaskFlowTitleStrategy } from '../../core/i18n/title.strategy';
import { TranslationService } from '../../core/i18n/translation.service';
import { ToastService } from '../../core/toast/toast.service';
import { TaskHeroService } from '../../shared/motion/task-hero.service';
import { AppButton, AppButtonLink } from '../../shared/ui/button';
import { ConfirmDialogService } from '../../shared/ui/confirm-dialog';
import { AppDueTick } from '../../shared/ui/due-tick';
import { AppEmptyState } from '../../shared/ui/empty-state';
import { AppErrorState } from '../../shared/ui/error-state';
import { AppSkeleton } from '../../shared/ui/skeleton';

const PRIORITY_BAR: Record<TaskPriority, string> = {
  Low: 'bg-todo/50',
  Medium: 'bg-amber',
  High: 'bg-heat',
};

@Component({
  selector: 'app-task-page',
  imports: [
    RouterLink,
    AppButton,
    AppButtonLink,
    AppSkeleton,
    AppErrorState,
    AppEmptyState,
    AppDueTick,
    DateFmtPipe,
    RelTimePipe,
    LucideChevronLeft,
    LucidePencil,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section>
      <a
        [routerLink]="backRoute()"
        class="tf-transition inline-flex items-center gap-1 text-sm text-slate hover:text-ink"
      >
        <svg
          lucideChevronLeft
          class="size-4 rtl:-scale-x-100"
          [strokeWidth]="2"
          aria-hidden="true"
        ></svg>
        <span>{{ backLabel() }}</span>
      </a>

      @if (taskResource.isLoading() && !task()) {
        <app-skeleton kind="task" />
      } @else if (isNotFound()) {
        <div class="mt-6">
          <app-empty-state
            [message]="i18n.t('taskNotFound')"
            [actionLabel]="i18n.t('backToTasks')"
            (action)="goBackToTasks()"
          />
        </div>
      } @else if (taskResource.error() && !task()) {
        <div class="mt-6">
          <app-error-state (retry)="taskResource.reload()" />
        </div>
      } @else if (task(); as t) {
        <div class="grid lg:grid-cols-[1fr_280px] gap-10 mt-3">
          <article>
            <div class="flex items-start justify-between gap-4 flex-wrap">
              <h1 #titleHeading class="display text-3xl font-extrabold" tabindex="-1">
                <bdi>{{ t.title }}</bdi>
              </h1>
              <div class="flex gap-2 shrink-0">
                @if (mayEdit()) {
                  <a [routerLink]="['/tasks', t.id, 'edit']" appButton variant="quiet">
                    <svg lucidePencil class="size-4" [strokeWidth]="1.8" aria-hidden="true"></svg>
                    <span>{{ i18n.t('edit') }}</span>
                  </a>
                }
                @if (mayDelete()) {
                  <app-button variant="danger" [busy]="deletingTask()" (click)="onDeleteTask(t)">
                    {{ i18n.t('delete') }}
                  </app-button>
                }
              </div>
            </div>

            @if (t.description) {
              <p class="mt-4 max-w-[68ch] whitespace-pre-line"><bdi>{{ t.description }}</bdi></p>
            } @else {
              <p class="mt-4 text-slate">{{ i18n.t('noDescription') }}</p>
            }

            <!-- Comments -->
            <h2 class="text-lg font-bold mt-10 mb-4">
              {{ i18n.dict().commentsCount(commentsTotal()) }}
            </h2>

            @if (hasMoreComments()) {
              <div class="mb-4">
                <app-button
                  variant="quiet"
                  size="sm"
                  [busy]="loadingMoreComments()"
                  (click)="loadOlderComments()"
                >
                  {{ i18n.t('loadOlderComments') }}
                </app-button>
              </div>
            }

            <ol class="space-y-6 border-s-2 border-line ps-6">
              @for (comment of comments(); track comment.id) {
                <li class="relative">
                  <span
                    class="absolute -start-[31px] top-1.5 size-3 rounded-full bg-panel border-2 border-flow"
                    aria-hidden="true"
                  ></span>
                  <div class="flex items-baseline gap-2 flex-wrap text-[14px]">
                    <span class="font-bold"><bdi>{{ comment.userName }}</bdi></span>
                    <span class="text-slate">
                      {{ comment.createdAt | relTime }}
                      @if (comment.updatedAt) {
                        {{ i18n.lang() === 'ar' ? '، ' : ', ' }}{{ i18n.t('edited') }}
                      }
                    </span>

                    <span class="ms-auto flex gap-1">
                      @if (mayEditComment(comment) && editingCommentId() !== comment.id) {
                        <app-button variant="ghost" size="xs" (click)="startEditComment(comment)">
                          {{ i18n.t('edit') }}
                        </app-button>
                      }
                      @if (mayDeleteComment(comment)) {
                        <app-button
                          variant="danger"
                          size="xs"
                          (click)="onDeleteComment(comment)"
                        >
                          {{ i18n.t('delete') }}
                        </app-button>
                      }
                    </span>
                  </div>

                  @if (editingCommentId() === comment.id) {
                    <div class="mt-2 max-w-[68ch]">
                      <textarea
                        rows="3"
                        class="tf-transition w-full px-3 py-2.5 rounded-[var(--radius-control)] bg-panel border border-line text-[14px]"
                        [value]="editingCommentText()"
                        (input)="editingCommentText.set($any($event.target).value)"
                      ></textarea>
                      <div class="flex gap-2 mt-2">
                        <app-button
                          size="sm"
                          [busy]="savingComment()"
                          (click)="saveEditComment(comment)"
                        >
                          {{ i18n.t('save') }}
                        </app-button>
                        <app-button variant="quiet" size="sm" (click)="cancelEditComment()">
                          {{ i18n.t('cancel') }}
                        </app-button>
                      </div>
                    </div>
                  } @else {
                    <p class="mt-1 max-w-[68ch] whitespace-pre-line">
                      <bdi>{{ comment.content }}</bdi>
                    </p>
                  }
                </li>
              }
            </ol>

            <!-- New comment form -->
            <form class="mt-8 max-w-[68ch]" novalidate (submit)="onPostComment($event)">
              <label for="new-comment" class="block font-bold text-[14px] mb-1.5">
                {{ i18n.t('addComment') }}
              </label>
              <textarea
                #commentBox
                id="new-comment"
                rows="3"
                class="tf-transition w-full px-3 py-2.5 rounded-[var(--radius-control)] bg-panel border border-line text-[14px] hover:border-slate/50"
                [class.tf-shake]="commentShaking()"
                [class.border-heat]="newComment().length > commentMax || commentEmptyError()"
                [attr.aria-invalid]="newComment().length > commentMax || commentEmptyError()"
                [value]="newComment()"
                (input)="onCommentInput($any($event.target).value)"
              ></textarea>
              @if (commentEmptyError()) {
                <p class="text-heat text-[13px] mt-1.5">{{ i18n.t('fieldRequired') }}</p>
              }
              <div class="flex justify-between items-center mt-2">
                <span
                  class="text-xs num"
                  [class.text-heat]="newComment().length > commentMax"
                  [class.font-bold]="newComment().length > commentMax"
                  [class.text-slate]="newComment().length <= commentMax"
                >
                  {{ i18n.formatNumber(newComment().length) }} /
                  {{ i18n.formatNumber(commentMax) }}
                </span>
                <app-button
                  type="submit"
                  [busy]="postingComment()"
                  [disabled]="newComment().length > commentMax"
                >
                  {{ i18n.t('postComment') }}
                </app-button>
              </div>
            </form>
          </article>

          <!-- Sidebar metadata -->
          <aside class="lg:border-s lg:border-line lg:ps-8 space-y-5 text-[14px] h-fit">
            <div>
              <label class="text-slate block mb-1" for="task-status-select">
                {{ i18n.t('status') }}
              </label>
              <select
                id="task-status-select"
                class="tf-transition h-10 px-3 rounded-[var(--radius-control)] bg-panel border border-line text-[14px] w-full hover:border-slate/50 disabled:opacity-60"
                [disabled]="!mayMove() || updatingStatus()"
                [value]="t.status"
                (change)="onStatusChange($any($event.target).value)"
              >
                @for (s of statuses; track s) {
                  <option [value]="s" [selected]="t.status === s">{{ statusLabel(s) }}</option>
                }
              </select>
            </div>

            <div>
              <label class="text-slate block mb-1" for="task-assignee-select">
                {{ i18n.t('assignedTo') }}
              </label>
              <select
                id="task-assignee-select"
                class="tf-transition h-10 px-3 rounded-[var(--radius-control)] bg-panel border border-line text-[14px] w-full hover:border-slate/50 disabled:opacity-60"
                [disabled]="!mayEdit() || updatingAssignee()"
                [value]="t.assignedToId ?? ''"
                (change)="onAssigneeChange($any($event.target).value)"
              >
                <option value="">{{ i18n.t('unassigned') }}</option>
                @for (u of activeUsers(); track u.id) {
                  <option [value]="u.id" [selected]="t.assignedToId === u.id">
                    {{ u.fullName }}
                  </option>
                }
              </select>
            </div>

            <div>
              <p class="text-slate mb-1">{{ i18n.t('due') }}</p>
              <p><app-due-tick [dueDate]="t.dueDate" [status]="t.status" /></p>
              @if (t.dueDate) {
                <p class="text-slate mt-1">
                  {{
                    t.dueDate
                      | dateFmt: { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' }
                  }}
                </p>
              }
            </div>

            <div>
              <p class="text-slate">{{ i18n.t('priority') }}</p>
              <p class="inline-flex items-center gap-2 mt-1 font-bold">
                <span
                  class="w-1 h-4 rounded"
                  [class]="priorityBar(t.priority)"
                  aria-hidden="true"
                ></span>
                <span>{{ priorityLabel(t.priority) }}</span>
              </p>
            </div>

            <div>
              <p class="text-slate">{{ i18n.t('createdBy') }}</p>
              <p class="mt-0.5">
                <bdi>{{ t.createdByName }}</bdi
                >{{ i18n.lang() === 'ar' ? '، ' : ', ' }}{{ t.createdAt | relTime }}
              </p>
            </div>
          </aside>
        </div>
      }
    </section>
  `,
})
export class TaskPage {
  protected readonly i18n = inject(TranslationService);
  private readonly auth = inject(AuthStore);
  private readonly tasksApi = inject(TasksApi);
  private readonly commentsApi = inject(CommentsApi);
  private readonly toast = inject(ToastService);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly titleStrategy = inject(TaskFlowTitleStrategy);
  private readonly hero = inject(TaskHeroService);
  private readonly router = inject(Router);

  readonly id = input.required<string>();
  protected readonly statuses = TASK_STATUSES;
  protected readonly commentMax = LIMITS.COMMENT_MAX;

  private readonly titleHeading = viewChild<ElementRef<HTMLElement>>('titleHeading');
  private readonly commentBox = viewChild<ElementRef<HTMLTextAreaElement>>('commentBox');

  protected readonly taskResource = httpResource<Task>(() => `/api/tasks/${this.id()}`);
  protected readonly task = linkedSignal<Task | undefined>(() => this.taskResource.value());

  private readonly commentsResource = httpResource<Paginated<Comment>>(() => ({
    url: `/api/tasks/${this.id()}/comments`,
    params: { page: '1', pageSize: String(LIMITS.DEFAULT_PAGE_SIZE) },
  }));

  protected readonly comments = linkedSignal<Comment[]>(
    () => this.commentsResource.value()?.items ?? [],
  );
  protected readonly commentsTotal = linkedSignal<number>(
    () => this.commentsResource.value()?.totalCount ?? this.task()?.commentsCount ?? 0,
  );
  private readonly commentsPage = signal(1);
  protected readonly hasMoreComments = computed(
    () => this.comments().length < this.commentsTotal(),
  );

  private readonly activeUsersResource = httpResource<User[]>(() => ({
    url: '/api/users',
    params: { isActive: 'true' },
  }));
  protected readonly activeUsers = computed(() => this.activeUsersResource.value() ?? []);

  protected readonly isNotFound = computed(() => {
    const err = this.taskResource.error();
    return err instanceof ApiError && err.status === 404;
  });

  protected readonly mayEdit = computed(() => {
    const t = this.task();
    return t ? canEditTask(t, this.auth.user()) : false;
  });
  protected readonly mayMove = computed(() => {
    const t = this.task();
    return t ? canMoveTask(t, this.auth.user()) : false;
  });
  protected readonly mayDelete = computed(() => {
    const t = this.task();
    return t ? canDeleteTask(t, this.auth.user()) : false;
  });

  protected readonly updatingStatus = signal(false);
  protected readonly updatingAssignee = signal(false);
  protected readonly deletingTask = signal(false);
  protected readonly loadingMoreComments = signal(false);

  protected readonly newComment = signal('');
  protected readonly postingComment = signal(false);
  protected readonly commentShaking = signal(false);
  protected readonly commentEmptyError = signal(false);
  protected readonly editingCommentId = signal<string | null>(null);
  protected readonly editingCommentText = signal('');
  protected readonly savingComment = signal(false);

  private readonly fromOrigin = signal<'board' | 'dashboard' | 'tasks'>(this.readFromState());

  protected readonly backRoute = computed(() => {
    const origin = this.fromOrigin();
    return origin === 'board' ? '/tasks/board' : origin === 'dashboard' ? '/dashboard' : '/tasks';
  });

  protected readonly backLabel = computed(() => {
    const origin = this.fromOrigin();
    const d = this.i18n.dict();
    return origin === 'board' ? d.board : origin === 'dashboard' ? d.today : d.allTasks;
  });

  constructor() {
    effect(() => {
      const t = this.task();
      if (t) {
        this.titleStrategy.setDynamicTitle(t.title);
      }
    });

    effect(() => {
      const el = this.titleHeading()?.nativeElement;
      if (el) {
        this.hero.markTarget(el);
      }
    });

    effect(() => {
      this.id();
      this.commentsPage.set(1);
    });
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

  protected mayEditComment(comment: Comment): boolean {
    return canEditComment(comment, this.auth.user());
  }

  protected mayDeleteComment(comment: Comment): boolean {
    return canDeleteComment(comment, this.auth.user());
  }

  protected onStatusChange(nextStatus: TaskStatus): void {
    const current = this.task();
    if (!current || current.status === nextStatus) return;

    this.task.set({ ...current, status: nextStatus });
    this.updatingStatus.set(true);
    this.tasksApi
      .changeStatus(current.id, nextStatus)
      .pipe(finalize(() => this.updatingStatus.set(false)))
      .subscribe({
        next: (updated) => {
          this.task.set(updated);
          this.toast.info(this.i18n.dict().statusChanged(this.statusLabel(nextStatus)));
        },
        error: (err: unknown) => {
          this.task.set(current);
          this.toast.error(err instanceof ApiError ? err.detail : this.i18n.t('genericError'));
        },
      });
  }

  protected onAssigneeChange(nextAssigneeId: string): void {
    const current = this.task();
    if (!current) return;
    const assignedToId = nextAssigneeId || null;
    if (current.assignedToId === assignedToId) return;

    const targetUser = this.activeUsers().find((u) => u.id === assignedToId);
    this.task.set({
      ...current,
      assignedToId,
      assignedToName: targetUser?.fullName ?? null,
    });
    this.updatingAssignee.set(true);
    this.tasksApi
      .assign(current.id, assignedToId)
      .pipe(finalize(() => this.updatingAssignee.set(false)))
      .subscribe({
        next: (updated) => {
          this.task.set(updated);
          this.toast.info(this.i18n.t('assigneeChanged'));
        },
        error: (err: unknown) => {
          this.task.set(current);
          this.toast.error(err instanceof ApiError ? err.detail : this.i18n.t('genericError'));
        },
      });
  }

  protected async onDeleteTask(t: Task): Promise<void> {
    const confirmed = await this.confirmDialog.confirm({
      title: this.i18n.dict().deleteTaskTitle(t.title),
      body: this.i18n.dict().deleteTaskBody(this.commentsTotal()),
      confirmLabel: this.i18n.t('delete'),
      danger: true,
    });
    if (!confirmed) return;

    this.deletingTask.set(true);
    this.tasksApi
      .delete(t.id)
      .pipe(finalize(() => this.deletingTask.set(false)))
      .subscribe({
        next: () => {
          this.toast.info(this.i18n.t('taskDeleted'));
          void this.router.navigateByUrl(this.backRoute());
        },
        error: (err: unknown) => {
          this.toast.error(err instanceof ApiError ? err.detail : this.i18n.t('genericError'));
        },
      });
  }

  protected loadOlderComments(): void {
    const nextPage = this.commentsPage() + 1;
    this.loadingMoreComments.set(true);
    this.commentsApi
      .list(this.id(), nextPage, LIMITS.DEFAULT_PAGE_SIZE)
      .pipe(finalize(() => this.loadingMoreComments.set(false)))
      .subscribe({
        next: (res) => {
          this.commentsPage.set(nextPage);
          this.comments.update((prev) => [...prev, ...res.items]);
          this.commentsTotal.set(res.totalCount);
        },
        error: (err: unknown) => {
          this.toast.error(err instanceof ApiError ? err.detail : this.i18n.t('genericError'));
        },
      });
  }

  protected onCommentInput(value: string): void {
    this.newComment.set(value);
    if (this.commentEmptyError() && value.trim().length > 0) {
      this.commentEmptyError.set(false);
    }
  }

  protected onPostComment(event: Event): void {
    event.preventDefault();
    const content = this.newComment().trim();
    if (!content) {
      this.commentEmptyError.set(true);
      this.commentShaking.set(false);
      requestAnimationFrame(() => this.commentShaking.set(true));
      setTimeout(() => this.commentShaking.set(false), 300);
      this.commentBox()?.nativeElement.focus();
      return;
    }
    this.commentEmptyError.set(false);
    if (content.length > LIMITS.COMMENT_MAX || this.postingComment()) return;

    this.postingComment.set(true);
    this.commentsApi
      .create(this.id(), content)
      .pipe(finalize(() => this.postingComment.set(false)))
      .subscribe({
        next: (created) => {
          this.comments.update((list) => [...list, created]);
          this.commentsTotal.update((n) => n + 1);
          this.newComment.set('');
          this.toast.info(this.i18n.t('commentPosted'));
        },
        error: (err: unknown) => {
          this.toast.error(err instanceof ApiError ? err.detail : this.i18n.t('genericError'));
        },
      });
  }

  protected startEditComment(comment: Comment): void {
    this.editingCommentId.set(comment.id);
    this.editingCommentText.set(comment.content);
  }

  protected cancelEditComment(): void {
    this.editingCommentId.set(null);
    this.editingCommentText.set('');
  }

  protected saveEditComment(comment: Comment): void {
    const content = this.editingCommentText().trim();
    if (!content || content.length > LIMITS.COMMENT_MAX || this.savingComment()) return;

    this.savingComment.set(true);
    this.commentsApi
      .update(comment.id, content)
      .pipe(finalize(() => this.savingComment.set(false)))
      .subscribe({
        next: (updated) => {
          this.comments.update((list) => list.map((c) => (c.id === updated.id ? updated : c)));
          this.cancelEditComment();
          this.toast.info(this.i18n.t('changesSaved'));
        },
        error: (err: unknown) => {
          this.toast.error(err instanceof ApiError ? err.detail : this.i18n.t('genericError'));
        },
      });
  }

  protected async onDeleteComment(comment: Comment): Promise<void> {
    const confirmed = await this.confirmDialog.confirm({
      title: this.i18n.t('deleteCommentTitle'),
      body: this.i18n.t('deleteCommentBody'),
      confirmLabel: this.i18n.t('delete'),
      danger: true,
    });
    if (!confirmed) return;

    this.commentsApi.delete(comment.id).subscribe({
      next: () => {
        this.comments.update((list) => list.filter((c) => c.id !== comment.id));
        this.commentsTotal.update((n) => Math.max(0, n - 1));
        this.toast.info(this.i18n.t('commentDeleted'));
      },
      error: (err: unknown) => {
        this.toast.error(err instanceof ApiError ? err.detail : this.i18n.t('genericError'));
      },
    });
  }

  protected goBackToTasks(): void {
    void this.router.navigate(['/tasks']);
  }

  private readFromState(): 'board' | 'dashboard' | 'tasks' {
    const state = this.router.currentNavigation()?.extras.state ?? history.state;
    const from = (state as { from?: string } | null)?.from;
    if (from === 'board' || from === 'dashboard' || from === 'tasks') {
      return from;
    }
    return 'tasks';
  }
}
