import { httpResource } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';
import { FormField, form, maxLength, required, submit } from '@angular/forms/signals';
import { Router } from '@angular/router';
import { finalize } from 'rxjs';
import { LIMITS } from '../../core/api/limits';
import {
  TASK_PRIORITIES,
  TASK_STATUSES,
  Task,
  TaskPriority,
  TaskStatus,
  User,
} from '../../core/api/models';
import { TasksApi } from '../../core/api/tasks.api';
import { AuthStore } from '../../core/auth/auth.store';
import { HasUnsavedChanges } from '../../core/auth/unsaved-changes.guard';
import { toDateInputValue, toEndOfDayUtc } from '../../core/domain/due-date';
import { canEditTask } from '../../core/domain/permissions';
import { ApiError } from '../../core/http/api-error';
import { TranslationService } from '../../core/i18n/translation.service';
import { ToastService } from '../../core/toast/toast.service';
import { StaggerDirective } from '../../shared/motion/stagger.directive';
import { AppButton } from '../../shared/ui/button';
import { ConfirmDialogService } from '../../shared/ui/confirm-dialog';
import { AppErrorState } from '../../shared/ui/error-state';
import { AppSkeleton } from '../../shared/ui/skeleton';

interface TaskFormModel {
  title: string;
  description: string;
  priority: TaskPriority;
  dueDateInput: string;
  assignedToId: string;
  status: TaskStatus;
}

const PRIORITY_BAR: Record<TaskPriority, string> = {
  Low: 'bg-todo/50',
  Medium: 'bg-amber',
  High: 'bg-heat',
};

@Component({
  selector: 'app-task-form-page',
  imports: [FormField, StaggerDirective, AppButton, AppSkeleton, AppErrorState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="max-w-[640px]">
      @if (isEdit() && existingTask.isLoading()) {
        <app-skeleton kind="task" />
      } @else if (isEdit() && existingTask.error()) {
        <app-error-state (retry)="existingTask.reload()" />
      } @else {
        <form appStagger novalidate (submit)="onSubmit($event)">
          <h1 class="display text-3xl font-extrabold" tabindex="-1">
            {{ isEdit() ? i18n.t('editTaskTitle') : i18n.t('newTaskTitle') }}
          </h1>

          <!-- Title -->
          <div class="mt-8">
            <label class="block font-bold text-[14px] mb-1.5" for="tf-title">
              {{ i18n.t('title') }}
            </label>
            <input
              id="tf-title"
              class="tf-transition h-10 px-3 rounded-[var(--radius-control)] bg-panel border border-line text-[14px] w-full hover:border-slate/50"
              [class.border-heat]="fieldError('title') !== null"
              [attr.aria-invalid]="fieldError('title') !== null"
              [formField]="taskForm.title"
              (input)="markDirty()"
            />
            <div class="flex justify-between items-start gap-2 mt-1.5">
              @if (fieldError('title'); as err) {
                <p class="text-heat text-[13px]">{{ err }}</p>
              }
              <span
                class="ms-auto text-xs num"
                [class.text-heat]="model().title.length > titleMax"
                [class.font-bold]="model().title.length > titleMax"
                [class.text-slate]="model().title.length <= titleMax"
              >
                {{ i18n.formatNumber(model().title.length) }} / {{ i18n.formatNumber(titleMax) }}
              </span>
            </div>
          </div>

          <!-- Description -->
          <div class="mt-5">
            <label class="block font-bold text-[14px] mb-1.5" for="tf-desc">
              {{ i18n.t('description') }}
              <span class="font-normal text-slate">{{ i18n.t('optional') }}</span>
            </label>
            <textarea
              id="tf-desc"
              rows="5"
              class="tf-transition px-3 py-2.5 rounded-[var(--radius-control)] bg-panel border border-line text-[14px] w-full resize-y hover:border-slate/50"
              [class.border-heat]="fieldError('description') !== null"
              [attr.aria-invalid]="fieldError('description') !== null"
              [formField]="taskForm.description"
              (input)="markDirty()"
            ></textarea>
            <div class="flex justify-between items-start gap-2 mt-1.5">
              @if (fieldError('description'); as err) {
                <p class="text-heat text-[13px]">{{ err }}</p>
              }
              <span
                class="ms-auto text-xs num"
                [class.text-heat]="model().description.length > descMax"
                [class.font-bold]="model().description.length > descMax"
                [class.text-slate]="model().description.length <= descMax"
              >
                {{ i18n.formatNumber(model().description.length) }} /
                {{ i18n.formatNumber(descMax) }}
              </span>
            </div>
          </div>

          <!-- Priority segmented control -->
          <div class="mt-5">
            <span id="tf-prio-label" class="block font-bold text-[14px] mb-1.5">
              {{ i18n.t('priority') }}
            </span>
            <div
              class="flex p-0.5 rounded-[var(--radius-control)] bg-sunk border border-line w-full sm:w-80"
              role="group"
              aria-labelledby="tf-prio-label"
            >
              @for (p of priorities; track p) {
                <button
                  type="button"
                  class="tf-transition flex-1 h-8 px-2.5 inline-flex items-center justify-center gap-1.5 rounded-lg text-[13px] cursor-pointer"
                  [class.bg-panel]="model().priority === p"
                  [class.text-ink]="model().priority === p"
                  [class.font-bold]="model().priority === p"
                  [class.shadow-xs]="model().priority === p"
                  [class.text-slate]="model().priority !== p"
                  [attr.aria-pressed]="model().priority === p"
                  (click)="setPriority(p)"
                >
                  <span class="w-1 h-3.5 rounded" [class]="priorityBar(p)" aria-hidden="true"></span>
                  <span>{{ priorityLabel(p) }}</span>
                </button>
              }
            </div>
          </div>

          <!-- Due date & Assignee -->
          <div class="grid sm:grid-cols-2 gap-5 mt-5">
            <div>
              <label class="block font-bold text-[14px] mb-1.5" for="tf-due">
                {{ i18n.t('dueDate') }}
              </label>
              <div class="flex gap-2">
                <input
                  id="tf-due"
                  type="date"
                  class="tf-transition h-10 px-3 rounded-[var(--radius-control)] bg-panel border border-line text-[14px] flex-1 num hover:border-slate/50"
                  [value]="model().dueDateInput"
                  (change)="onDueDateChange($any($event.target).value)"
                />
                @if (model().dueDateInput) {
                  <app-button variant="ghost" (click)="onDueDateChange('')">
                    {{ i18n.t('clear') }}
                  </app-button>
                }
              </div>
            </div>

            <div>
              <label class="block font-bold text-[14px] mb-1.5" for="tf-assignee">
                {{ i18n.t('assignee') }}
              </label>
              <select
                id="tf-assignee"
                class="tf-transition h-10 px-3 rounded-[var(--radius-control)] bg-panel border border-line text-[14px] w-full hover:border-slate/50"
                [class.border-heat]="serverFieldErrors()['assignedToId']"
                [value]="model().assignedToId"
                (change)="onAssigneeChange($any($event.target).value)"
              >
                <option value="">{{ i18n.t('unassigned') }}</option>
                @for (u of activeUsers(); track u.id) {
                  <option [value]="u.id" [selected]="model().assignedToId === u.id">
                    {{ u.fullName }}
                  </option>
                }
              </select>
              @if (serverFieldErrors()['assignedToId']; as err) {
                <p class="text-heat text-[13px] mt-1.5">{{ err }}</p>
              }
            </div>
          </div>

          <!-- Status (edit only) -->
          @if (isEdit()) {
            <div class="mt-5">
              <label class="block font-bold text-[14px] mb-1.5" for="tf-status">
                {{ i18n.t('status') }}
              </label>
              <select
                id="tf-status"
                class="tf-transition h-10 px-3 rounded-[var(--radius-control)] bg-panel border border-line text-[14px] w-full sm:w-80 hover:border-slate/50"
                [value]="model().status"
                (change)="onStatusChange($any($event.target).value)"
              >
                @for (s of statuses; track s) {
                  <option [value]="s" [selected]="model().status === s">
                    {{ statusLabel(s) }}
                  </option>
                }
              </select>
            </div>
          } @else {
            <p class="text-slate text-[13px] mt-5">{{ i18n.t('newStartsTodo') }}</p>
          }

          <div class="flex gap-2 mt-10 pt-6 border-t border-line">
            <app-button type="submit" [busy]="saving()">
              {{ isEdit() ? i18n.t('saveChanges') : i18n.t('createTask') }}
            </app-button>
            <app-button variant="quiet" (click)="onCancel()">
              {{ i18n.t('cancel') }}
            </app-button>
          </div>
        </form>
      }
    </section>
  `,
})
export class TaskFormPage implements HasUnsavedChanges {
  protected readonly i18n = inject(TranslationService);
  private readonly auth = inject(AuthStore);
  private readonly tasksApi = inject(TasksApi);
  private readonly toast = inject(ToastService);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly router = inject(Router);

  readonly id = input<string>();
  protected readonly isEdit = computed(() => Boolean(this.id()));
  protected readonly priorities = TASK_PRIORITIES;
  protected readonly statuses = TASK_STATUSES;
  protected readonly titleMax = LIMITS.TITLE_MAX;
  protected readonly descMax = LIMITS.DESCRIPTION_MAX;

  protected readonly existingTask = httpResource<Task>(() => {
    const taskId = this.id();
    return taskId ? `/api/tasks/${taskId}` : undefined;
  });

  private readonly activeUsersResource = httpResource<User[]>(() => ({
    url: '/api/users',
    params: { isActive: 'true' },
  }));
  protected readonly activeUsers = computed(() => this.activeUsersResource.value() ?? []);

  protected readonly model = signal<TaskFormModel>({
    title: '',
    description: '',
    priority: 'Medium',
    dueDateInput: '',
    assignedToId: '',
    status: 'Todo',
  });

  protected readonly taskForm = form(this.model, (p) => {
    required(p.title, { message: () => this.i18n.t('titleRequired') });
    maxLength(p.title, LIMITS.TITLE_MAX, { message: () => this.i18n.t('titleTooLong') });
    maxLength(p.description, LIMITS.DESCRIPTION_MAX, {
      message: () => this.i18n.t('descriptionTooLong'),
    });
  });

  protected readonly saving = signal(false);
  protected readonly serverFieldErrors = signal<Record<string, string>>({});
  private readonly dirty = signal(false);
  private originalDueDateIso: string | null = null;
  private originalDueDateInput = '';
  private hydrated = false;
  private readonly fromBoard = this.readFromBoard();

  constructor() {
    effect(() => {
      const task = this.existingTask.value();
      if (!task || this.hydrated) return;

      if (!canEditTask(task, this.auth.user())) {
        this.toast.error(this.i18n.t('forbiddenError'));
        void this.router.navigate(['/tasks', task.id]);
        return;
      }

      this.hydrated = true;
      this.originalDueDateIso = task.dueDate;
      this.originalDueDateInput = toDateInputValue(task.dueDate);
      this.model.set({
        title: task.title,
        description: task.description ?? '',
        priority: task.priority,
        dueDateInput: this.originalDueDateInput,
        assignedToId: task.assignedToId ?? '',
        status: task.status,
      });
    });
  }

  async canDeactivate(): Promise<boolean> {
    if (!this.dirty()) return true;
    return this.confirmDialog.confirm({
      title: this.i18n.t('discardTitle'),
      body: this.i18n.t('discardBody'),
      confirmLabel: this.i18n.t('discard'),
      cancelLabel: this.i18n.t('keepEditing'),
      danger: true,
    });
  }

  protected markDirty(): void {
    this.dirty.set(true);
  }

  protected setPriority(priority: TaskPriority): void {
    this.model.update((m) => ({ ...m, priority }));
    this.dirty.set(true);
  }

  protected onDueDateChange(dueDateInput: string): void {
    this.model.update((m) => ({ ...m, dueDateInput }));
    this.dirty.set(true);
  }

  protected onAssigneeChange(assignedToId: string): void {
    this.model.update((m) => ({ ...m, assignedToId }));
    this.serverFieldErrors.update((e) => {
      const copy = { ...e };
      delete copy['assignedToId'];
      return copy;
    });
    this.dirty.set(true);
  }

  protected onStatusChange(status: TaskStatus): void {
    this.model.update((m) => ({ ...m, status }));
    this.dirty.set(true);
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

  protected fieldError(field: 'title' | 'description'): string | null {
    const server = this.serverFieldErrors()[field];
    if (server) return server;
    const state = this.taskForm[field]();
    if (state.touched() && state.invalid()) {
      return state.errors()[0]?.message ?? this.i18n.t('fieldRequired');
    }
    return null;
  }

  protected async onSubmit(event: Event): Promise<void> {
    event.preventDefault();
    if (this.saving()) return;
    this.serverFieldErrors.set({});

    await submit(this.taskForm, async () => {
      this.saving.set(true);
      const m = this.model();
      // Due-date rule (03-api): send stored value unchanged on edit unless user modified the date.
      const dueDate =
        this.isEdit() && m.dueDateInput === this.originalDueDateInput
          ? this.originalDueDateIso
          : toEndOfDayUtc(m.dueDateInput);

      const basePayload = {
        title: m.title.trim(),
        description: m.description ?? '',
        priority: m.priority,
        dueDate,
        assignedToId: m.assignedToId || null,
      };

      const editId = this.id();
      const request$ = editId
        ? this.tasksApi.update(editId, { ...basePayload, status: m.status })
        : this.tasksApi.create(basePayload);

      request$.pipe(finalize(() => this.saving.set(false))).subscribe({
        next: (saved) => {
          this.dirty.set(false);
          if (editId) {
            this.toast.info(this.i18n.t('changesSaved'));
            void this.router.navigate(['/tasks', editId]);
          } else {
            this.toast.info(this.i18n.t('taskCreated'));
            if (this.fromBoard) {
              void this.router.navigate(['/tasks/board']);
            } else {
              void this.router.navigate(['/tasks', saved.id]);
            }
          }
        },
        error: (err: unknown) => {
          if (err instanceof ApiError && err.status === 400) {
            const mapped: Record<string, string> = {};
            for (const key of ['title', 'description', 'assignedToId'] as const) {
              const msg = err.fieldError(key);
              if (msg) mapped[key] = msg;
            }
            this.serverFieldErrors.set(mapped);
            if (Object.keys(mapped).length === 0) {
              this.toast.error(err.detail || this.i18n.t('genericError'));
            }
          } else {
            this.toast.error(err instanceof ApiError ? err.detail : this.i18n.t('genericError'));
          }
        },
      });
    });
  }

  protected async onCancel(): Promise<void> {
    const editId = this.id();
    if (editId) {
      await this.router.navigate(['/tasks', editId]);
    } else if (this.fromBoard) {
      await this.router.navigate(['/tasks/board']);
    } else {
      await this.router.navigate(['/tasks']);
    }
  }

  private readFromBoard(): boolean {
    const state = this.router.currentNavigation()?.extras.state ?? history.state;
    return (state as { from?: string } | null)?.from === 'board';
  }
}
