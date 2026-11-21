import { A11yModule } from '@angular/cdk/a11y';
import { DIALOG_DATA, Dialog, DialogRef } from '@angular/cdk/dialog';
import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormField, email, form, minLength, required, submit } from '@angular/forms/signals';
import {
  LucideClock,
  LucideEllipsis,
  LucideEye,
  LucideEyeOff,
  LucidePencil,
  LucidePlus,
  LucideSearch,
} from '@lucide/angular';
import { finalize, firstValueFrom } from 'rxjs';
import { DashboardApi } from '../../core/api/dashboard.api';
import { LIMITS } from '../../core/api/limits';
import { USER_ROLES, UpdateUserRequest, User, UserRole } from '../../core/api/models';
import { CreateUserRequest, UsersApi } from '../../core/api/users.api';
import { AuthStore } from '../../core/auth/auth.store';
import { canDeactivateUser, isLastActiveAdmin } from '../../core/domain/permissions';
import { ApiError } from '../../core/http/api-error';
import { DateFmtPipe } from '../../core/i18n/date-fmt.pipe';
import { TranslationService } from '../../core/i18n/translation.service';
import { ToastService } from '../../core/toast/toast.service';
import { StaggerDirective } from '../../shared/motion/stagger.directive';
import { AppAvatar } from '../../shared/ui/avatar';
import { AppButton } from '../../shared/ui/button';
import { ConfirmDialogService } from '../../shared/ui/confirm-dialog';
import { AppEmptyState } from '../../shared/ui/empty-state';
import { AppErrorState } from '../../shared/ui/error-state';
import { AppSkeleton } from '../../shared/ui/skeleton';

type UserActiveFilter = 'all' | 'active' | 'deactivated';

@Component({
  selector: 'app-edit-user-dialog',
  imports: [A11yModule, FormField, AppButton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      cdkTrapFocus
      [cdkTrapFocusAutoCapture]="true"
      class="tf-menu-in bg-panel text-ink border border-line rounded-2xl shadow-2xl p-6 w-[min(440px,calc(100vw-2rem))]"
      role="dialog"
      aria-modal="true"
      aria-labelledby="edit-user-title"
    >
      <h2 id="edit-user-title" class="text-lg font-bold">{{ i18n.t('editUser') }}</h2>

      <form class="mt-5" novalidate (submit)="onSave($event)">
        <div class="grid grid-cols-2 gap-3">
          <div>
            <label class="block font-bold text-[14px] mb-1.5" for="eu-first">
              {{ i18n.t('firstName') }}
            </label>
            <input
              id="eu-first"
              class="tf-transition h-10 px-3 rounded-[var(--radius-control)] bg-panel border border-line text-[14px] w-full"
              [formField]="editForm.firstName"
            />
          </div>
          <div>
            <label class="block font-bold text-[14px] mb-1.5" for="eu-last">
              {{ i18n.t('lastName') }}
            </label>
            <input
              id="eu-last"
              class="tf-transition h-10 px-3 rounded-[var(--radius-control)] bg-panel border border-line text-[14px] w-full"
              [formField]="editForm.lastName"
            />
          </div>
        </div>

        <div class="mt-4">
          <label class="block font-bold text-[14px] mb-1.5" for="eu-email">
            {{ i18n.t('email') }}
          </label>
          <input
            id="eu-email"
            type="email"
            dir="ltr"
            class="tf-transition h-10 px-3 rounded-[var(--radius-control)] bg-panel border border-line text-[14px] w-full text-start"
            [formField]="editForm.email"
          />
          @if (emailError()) {
            <p class="text-heat text-[13px] mt-1.5">{{ emailError() }}</p>
          }
        </div>

        <div class="flex justify-end gap-2 mt-7">
          <app-button variant="quiet" (click)="close(null)">{{ i18n.t('cancel') }}</app-button>
          <app-button type="submit">{{ i18n.t('save') }}</app-button>
        </div>
      </form>
    </div>
  `,
})
export class EditUserDialogComponent {
  protected readonly i18n = inject(TranslationService);
  private readonly user = inject<User>(DIALOG_DATA);
  private readonly dialogRef = inject<DialogRef<UpdateUserRequest | null>>(DialogRef);

  protected readonly model = signal<UpdateUserRequest>({
    firstName: this.user.firstName,
    lastName: this.user.lastName,
    email: this.user.email,
  });

  protected readonly editForm = form(this.model, (p) => {
    required(p.firstName);
    required(p.lastName);
    required(p.email);
    email(p.email);
  });

  protected readonly emailError = computed(() => {
    const state = this.editForm.email();
    if (state.touched() && state.invalid()) {
      return this.i18n.t('emailInvalid');
    }
    return null;
  });

  protected async onSave(event: Event): Promise<void> {
    event.preventDefault();
    await submit(this.editForm, async () => {
      const m = this.model();
      this.dialogRef.close({
        firstName: m.firstName.trim(),
        lastName: m.lastName.trim(),
        email: m.email.trim(),
      });
    });
  }

  protected close(result: UpdateUserRequest | null): void {
    this.dialogRef.close(result);
  }
}

@Component({
  selector: 'app-create-user-dialog',
  imports: [A11yModule, FormField, AppButton, LucideEye, LucideEyeOff],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      cdkTrapFocus
      [cdkTrapFocusAutoCapture]="true"
      class="tf-menu-in bg-panel text-ink border border-line rounded-2xl shadow-2xl p-6 w-[min(460px,calc(100vw-2rem))]"
      role="dialog"
      aria-modal="true"
      aria-labelledby="create-user-title"
    >
      <h2 id="create-user-title" class="text-lg font-bold">{{ i18n.t('addUser') }}</h2>

      @if (bannerError()) {
        <p role="alert" class="mt-4 rounded-xl bg-heat-soft text-heat px-3.5 py-2.5 text-[14px] font-bold">
          {{ bannerError() }}
        </p>
      }

      <form class="mt-5" novalidate (submit)="onCreate($event)">
        <div class="grid grid-cols-2 gap-3">
          <div>
            <label class="block font-bold text-[14px] mb-1.5" for="cu-first">
              {{ i18n.t('firstName') }}
            </label>
            <input
              id="cu-first"
              class="tf-transition h-10 px-3 rounded-[var(--radius-control)] bg-panel border border-line text-[14px] w-full"
              [class.border-heat]="fieldError('firstName') !== null"
              [formField]="createForm.firstName"
            />
            @if (fieldError('firstName'); as err) {
              <p class="text-heat text-[13px] mt-1.5">{{ err }}</p>
            }
          </div>

          <div>
            <label class="block font-bold text-[14px] mb-1.5" for="cu-last">
              {{ i18n.t('lastName') }}
            </label>
            <input
              id="cu-last"
              class="tf-transition h-10 px-3 rounded-[var(--radius-control)] bg-panel border border-line text-[14px] w-full"
              [class.border-heat]="fieldError('lastName') !== null"
              [formField]="createForm.lastName"
            />
            @if (fieldError('lastName'); as err) {
              <p class="text-heat text-[13px] mt-1.5">{{ err }}</p>
            }
          </div>
        </div>

        <div class="mt-4">
          <label class="block font-bold text-[14px] mb-1.5" for="cu-email">
            {{ i18n.t('email') }}
          </label>
          <input
            id="cu-email"
            type="email"
            dir="ltr"
            autocomplete="off"
            class="tf-transition h-10 px-3 rounded-[var(--radius-control)] bg-panel border border-line text-[14px] w-full text-start"
            [class.border-heat]="fieldError('email') !== null"
            [formField]="createForm.email"
          />
          @if (fieldError('email'); as err) {
            <p class="text-heat text-[13px] mt-1.5">{{ err }}</p>
          }
        </div>

        <div class="mt-4">
          <label class="block font-bold text-[14px] mb-1.5" for="cu-pw">
            {{ i18n.t('password') }}
          </label>
          <div class="relative">
            <input
              id="cu-pw"
              [type]="showPassword() ? 'text' : 'password'"
              autocomplete="new-password"
              class="tf-transition h-10 ps-3 pe-11 rounded-[var(--radius-control)] bg-panel border border-line text-[14px] w-full"
              [class.border-heat]="fieldError('password') !== null"
              [formField]="createForm.password"
            />
            <button
              type="button"
              class="tf-transition absolute end-1 top-1 size-8 grid place-items-center rounded-lg text-slate hover:text-ink hover:bg-ink/5 cursor-pointer"
              [attr.aria-label]="showPassword() ? i18n.t('hidePassword') : i18n.t('showPassword')"
              [attr.aria-pressed]="showPassword()"
              (click)="showPassword.set(!showPassword())"
            >
              @if (showPassword()) {
                <svg lucideEyeOff class="size-4.5" [strokeWidth]="1.8" aria-hidden="true"></svg>
              } @else {
                <svg lucideEye class="size-4.5" [strokeWidth]="1.8" aria-hidden="true"></svg>
              }
            </button>
          </div>
          <p class="text-slate text-[13px] mt-1.5">{{ i18n.t('passwordHint') }}</p>
          @if (fieldError('password'); as err) {
            <p class="text-heat text-[13px] mt-1">{{ err }}</p>
          }
        </div>

        <div class="mt-4">
          <label class="block font-bold text-[14px] mb-1.5" for="cu-role">
            {{ i18n.t('colRole') }}
          </label>
          <select
            id="cu-role"
            class="tf-transition h-10 px-3 rounded-[var(--radius-control)] bg-panel border border-line text-[14px] w-full"
            [value]="model().role"
            (change)="setRole($any($event.target).value)"
          >
            @for (r of roles; track r) {
              <option [value]="r" [selected]="model().role === r">{{ roleLabel(r) }}</option>
            }
          </select>
        </div>

        <div class="flex justify-end gap-2 mt-7">
          <app-button variant="quiet" [disabled]="saving()" (click)="close(null)">
            {{ i18n.t('cancel') }}
          </app-button>
          <app-button type="submit" [busy]="saving()">{{ i18n.t('createUser') }}</app-button>
        </div>
      </form>
    </div>
  `,
})
export class CreateUserDialogComponent {
  protected readonly i18n = inject(TranslationService);
  private readonly usersApi = inject(UsersApi);
  private readonly dialogRef = inject<DialogRef<User | null>>(DialogRef);

  protected readonly roles = USER_ROLES;
  protected readonly showPassword = signal(false);
  protected readonly saving = signal(false);
  protected readonly bannerError = signal<string | null>(null);
  private readonly serverFieldErrors = signal<Record<string, string>>({});

  protected readonly model = signal<CreateUserRequest>({
    firstName: '',
    lastName: '',
    email: '',
    password: '',
    role: 'User',
  });

  protected readonly createForm = form(this.model, (p) => {
    required(p.firstName, { message: () => this.i18n.t('fieldRequired') });
    required(p.lastName, { message: () => this.i18n.t('fieldRequired') });
    required(p.email, { message: () => this.i18n.t('fieldRequired') });
    email(p.email, { message: () => this.i18n.t('emailInvalid') });
    required(p.password, { message: () => this.i18n.t('fieldRequired') });
    minLength(p.password, LIMITS.PASSWORD_MIN, { message: () => this.i18n.t('passwordShort') });
  });

  protected setRole(role: UserRole): void {
    this.model.update((m) => ({ ...m, role }));
  }

  protected roleLabel(role: UserRole): string {
    const d = this.i18n.dict();
    return role === 'Admin' ? d.roleAdmin : role === 'Manager' ? d.roleManager : d.roleUser;
  }

  protected fieldError(field: 'firstName' | 'lastName' | 'email' | 'password'): string | null {
    const server = this.serverFieldErrors()[field];
    if (server) return server;
    const state = this.createForm[field]();
    if (state.touched() && state.invalid()) {
      return state.errors()[0]?.message ?? this.i18n.t('fieldRequired');
    }
    return null;
  }

  protected async onCreate(event: Event): Promise<void> {
    event.preventDefault();
    if (this.saving()) return;
    this.serverFieldErrors.set({});
    this.bannerError.set(null);

    await submit(this.createForm, async () => {
      this.saving.set(true);
      const m = this.model();
      this.usersApi
        .create({
          firstName: m.firstName.trim(),
          lastName: m.lastName.trim(),
          email: m.email.trim(),
          password: m.password,
          role: m.role,
        })
        .pipe(finalize(() => this.saving.set(false)))
        .subscribe({
          next: (created) => this.dialogRef.close(created),
          error: (err: unknown) => {
            if (err instanceof ApiError) {
              if (err.status === 409) {
                this.serverFieldErrors.set({ email: this.i18n.t('emailTaken') });
              } else if (err.status === 400) {
                const mapped: Record<string, string> = {};
                for (const key of ['firstName', 'lastName', 'email', 'password'] as const) {
                  const msg = err.fieldError(key);
                  if (msg) mapped[key] = msg;
                }
                this.serverFieldErrors.set(mapped);
                if (Object.keys(mapped).length === 0) {
                  this.bannerError.set(err.detail || this.i18n.t('genericError'));
                }
              } else {
                this.bannerError.set(err.detail || this.i18n.t('genericError'));
              }
            } else {
              this.bannerError.set(this.i18n.t('genericError'));
            }
          },
        });
    });
  }

  protected close(result: User | null): void {
    this.dialogRef.close(result);
  }
}

@Component({
  selector: 'app-users-page',
  imports: [
    StaggerDirective,
    AppButton,
    AppAvatar,
    AppSkeleton,
    AppErrorState,
    AppEmptyState,
    DateFmtPipe,
    LucidePlus,
    LucideClock,
    LucideSearch,
    LucideEllipsis,
    LucidePencil,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    '(document:click)': 'onDocumentClick($event)',
    '(document:keydown.escape)': 'openMenuUserId.set(null)',
  },
  template: `
    <section appStagger>
      <div class="flex items-center justify-between gap-4 flex-wrap">
        <h1 class="display text-3xl font-extrabold" tabindex="-1">{{ i18n.t('users') }}</h1>
        <div class="flex items-center gap-2 flex-wrap">
          <app-button variant="quiet" [busy]="runningReminders()" (click)="onRunReminders()">
            <svg lucideClock class="size-[18px]" [strokeWidth]="1.8" aria-hidden="true"></svg>
            <span>{{ i18n.t('runReminders') }}</span>
          </app-button>
          <app-button (click)="onAddUser()">
            <svg lucidePlus class="size-[18px]" [strokeWidth]="2" aria-hidden="true"></svg>
            <span>{{ i18n.t('addUser') }}</span>
          </app-button>
        </div>
      </div>

      <div class="flex flex-wrap items-center justify-between gap-3 mt-6">
        <div
          class="flex p-0.5 rounded-[var(--radius-control)] bg-sunk border border-line w-fit"
          role="group"
        >
          @for (opt of filterOptions; track opt.value) {
            <button
              type="button"
              class="tf-transition h-8 px-3 rounded-lg text-[13px] cursor-pointer"
              [class.bg-panel]="activeFilter() === opt.value"
              [class.text-ink]="activeFilter() === opt.value"
              [class.font-bold]="activeFilter() === opt.value"
              [class.shadow-xs]="activeFilter() === opt.value"
              [class.text-slate]="activeFilter() !== opt.value"
              [attr.aria-pressed]="activeFilter() === opt.value"
              (click)="activeFilter.set(opt.value)"
            >
              {{ i18n.t(opt.labelKey) }}
            </button>
          }
        </div>

        <label class="relative w-full sm:w-72">
          <span class="sr-only">{{ i18n.t('searchUsers') }}</span>
          <svg
            lucideSearch
            class="size-4 absolute start-3 top-3 text-slate pointer-events-none"
            [strokeWidth]="1.8"
            aria-hidden="true"
          ></svg>
          <input
            type="search"
            class="tf-transition h-10 ps-9 pe-3 rounded-[var(--radius-control)] bg-panel border border-line text-[14px] w-full hover:border-slate/50"
            [placeholder]="i18n.t('searchUsers')"
            [value]="searchQuery()"
            (input)="searchQuery.set($any($event.target).value)"
          />
        </label>
      </div>

      <div class="mt-5">
        @if (usersResource.isLoading()) {
          <app-skeleton kind="list" />
        } @else if (usersResource.error()) {
          <app-error-state (retry)="usersResource.reload()" />
        } @else if (filteredUsers().length === 0) {
          <app-empty-state [message]="i18n.t('emptyUsers')" />
        } @else {
          <!-- Desktop table -->
          <div class="bg-panel border border-line rounded-2xl hidden lg:block">
            <table class="w-full text-[14px]">
              <thead class="text-slate">
                <tr class="border-b border-line">
                  <th class="text-start font-normal px-5 h-11">{{ i18n.t('colName') }}</th>
                  <th class="text-start font-normal px-3">{{ i18n.t('email') }}</th>
                  <th class="text-start font-normal px-3">{{ i18n.t('colRole') }}</th>
                  <th class="text-start font-normal px-3">{{ i18n.t('status') }}</th>
                  <th class="text-start font-normal px-3">{{ i18n.t('colJoined') }}</th>
                  <th class="w-14"></th>
                </tr>
              </thead>
              <tbody class="divide-y divide-line">
                @for (u of filteredUsers(); track u.id) {
                  @let lastAdmin = isLastAdmin(u);
                  @let isSelf = u.id === auth.user()?.id;
                  <tr [class.opacity-70]="!u.isActive">
                    <td class="px-5 h-14">
                      <span class="flex items-center gap-3">
                        <app-avatar [name]="u.fullName" size="md" />
                        <span class="font-bold"><bdi>{{ u.fullName }}</bdi></span>
                        @if (isSelf) {
                          <span class="text-[11px] px-2 rounded-full bg-flow-soft text-flow font-bold">
                            {{ i18n.t('you') }}
                          </span>
                        }
                      </span>
                    </td>
                    <td class="px-3 text-slate text-start" dir="ltr">{{ u.email }}</td>
                    <td class="px-3">
                      <select
                        class="tf-transition h-9 px-2.5 rounded-[var(--radius-control)] bg-panel border border-line text-[13px] disabled:opacity-60"
                        [disabled]="lastAdmin"
                        [title]="lastAdmin ? i18n.t('lastAdminRule') : ''"
                        [value]="u.role"
                        (change)="onRoleChange(u, $event)"
                      >
                        @for (r of roles; track r) {
                          <option [value]="r" [selected]="u.role === r">{{ roleLabel(r) }}</option>
                        }
                      </select>
                    </td>
                    <td class="px-3">
                      <span class="inline-flex items-center gap-2" [class.text-slate]="!u.isActive">
                        <span
                          class="size-2 rounded-full"
                          [class.bg-flow]="u.isActive"
                          [class.border]="!u.isActive"
                          [class.border-slate]="!u.isActive"
                        ></span>
                        <span>{{ u.isActive ? i18n.t('active') : i18n.t('deactivated') }}</span>
                      </span>
                    </td>
                    <td class="px-3 text-slate">{{ u.createdAt | dateFmt }}</td>
                    <td class="px-3 relative">
                      <button
                        type="button"
                        class="tf-transition size-9 grid place-items-center rounded-lg text-slate hover:text-ink hover:bg-ink/5 cursor-pointer"
                        aria-haspopup="menu"
                        [attr.aria-label]="i18n.dict().rowActionsAria(u.fullName)"
                        (click)="toggleMenu($event, u.id)"
                      >
                        <svg lucideEllipsis class="size-4" [strokeWidth]="2" aria-hidden="true"></svg>
                      </button>

                      @if (openMenuUserId() === u.id) {
                        <div
                          role="menu"
                          class="tf-menu-in absolute top-11 end-3 z-20 w-60 bg-panel border border-line rounded-2xl p-2 shadow-xl"
                        >
                          <button
                            type="button"
                            role="menuitem"
                            class="tf-transition w-full text-start flex items-center gap-2.5 h-9 px-3 rounded-lg hover:bg-ink/5 text-[14px] cursor-pointer"
                            (click)="onEditUser(u)"
                          >
                            <svg lucidePencil class="size-4 text-slate" [strokeWidth]="1.8"></svg>
                            <span>{{ i18n.t('editUser') }}</span>
                          </button>

                          <button
                            type="button"
                            role="menuitem"
                            class="tf-transition w-full text-start flex items-center gap-2.5 h-9 px-3 rounded-lg hover:bg-ink/5 text-[14px] disabled:opacity-50 disabled:pointer-events-none cursor-pointer"
                            [disabled]="u.isActive && !mayDeactivate(u)"
                            [title]="deactivateReason(u)"
                            (click)="onToggleActive(u)"
                          >
                            {{ u.isActive ? i18n.t('deactivate') : i18n.t('reactivate') }}
                          </button>

                          <div class="border-t border-line my-1"></div>

                          <button
                            type="button"
                            role="menuitem"
                            class="tf-transition w-full text-start flex items-center gap-2.5 h-9 px-3 rounded-lg hover:bg-heat-soft text-heat text-[14px] disabled:opacity-50 disabled:pointer-events-none cursor-pointer"
                            [disabled]="lastAdmin"
                            [title]="lastAdmin ? i18n.t('lastAdminRule') : ''"
                            (click)="onDeleteUser(u)"
                          >
                            {{ i18n.t('delete') }}
                          </button>

                          @if (deactivateReason(u); as why) {
                            <p class="px-3 pt-2 pb-1 text-[12px] text-slate">{{ why }}</p>
                          }
                        </div>
                      }
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>

          <!-- Phone/tablet cards -->
          <ul class="lg:hidden space-y-2.5">
            @for (u of filteredUsers(); track u.id) {
              @let lastAdmin = isLastAdmin(u);
              <li
                class="bg-panel border border-line rounded-2xl p-4 relative"
                [class.opacity-70]="!u.isActive"
              >
                <div class="flex items-center gap-3">
                  <app-avatar [name]="u.fullName" size="lg" />
                  <div class="flex-1 min-w-0">
                    <p class="font-bold"><bdi>{{ u.fullName }}</bdi></p>
                    <p class="text-[13px] text-slate truncate text-start" dir="ltr">
                      {{ u.email }}
                    </p>
                  </div>
                  <button
                    type="button"
                    class="tf-transition size-9 grid place-items-center rounded-lg text-slate hover:text-ink hover:bg-ink/5 cursor-pointer"
                    aria-haspopup="menu"
                    [attr.aria-label]="i18n.dict().rowActionsAria(u.fullName)"
                    (click)="toggleMenu($event, u.id)"
                  >
                    <svg lucideEllipsis class="size-4" [strokeWidth]="2" aria-hidden="true"></svg>
                  </button>
                </div>

                <div class="flex items-center justify-between gap-3 mt-3 text-[14px]">
                  <select
                    class="tf-transition h-9 px-2.5 rounded-[var(--radius-control)] bg-panel border border-line text-[13px] disabled:opacity-60"
                    [disabled]="lastAdmin"
                    [value]="u.role"
                    (change)="onRoleChange(u, $event)"
                  >
                    @for (r of roles; track r) {
                      <option [value]="r" [selected]="u.role === r">{{ roleLabel(r) }}</option>
                    }
                  </select>
                  <span class="inline-flex items-center gap-2" [class.text-slate]="!u.isActive">
                    <span
                      class="size-2 rounded-full"
                      [class.bg-flow]="u.isActive"
                      [class.border]="!u.isActive"
                      [class.border-slate]="!u.isActive"
                    ></span>
                    <span>{{ u.isActive ? i18n.t('active') : i18n.t('deactivated') }}</span>
                  </span>
                </div>

                @if (openMenuUserId() === u.id) {
                  <div
                    role="menu"
                    class="tf-menu-in absolute top-14 end-4 z-20 w-60 bg-panel border border-line rounded-2xl p-2 shadow-xl"
                  >
                    <button
                      type="button"
                      role="menuitem"
                      class="tf-transition w-full text-start flex items-center gap-2.5 h-9 px-3 rounded-lg hover:bg-ink/5 text-[14px] cursor-pointer"
                      (click)="onEditUser(u)"
                    >
                      {{ i18n.t('editUser') }}
                    </button>
                    <button
                      type="button"
                      role="menuitem"
                      class="tf-transition w-full text-start flex items-center gap-2.5 h-9 px-3 rounded-lg hover:bg-ink/5 text-[14px] disabled:opacity-50 cursor-pointer"
                      [disabled]="u.isActive && !mayDeactivate(u)"
                      (click)="onToggleActive(u)"
                    >
                      {{ u.isActive ? i18n.t('deactivate') : i18n.t('reactivate') }}
                    </button>
                    <div class="border-t border-line my-1"></div>
                    <button
                      type="button"
                      role="menuitem"
                      class="tf-transition w-full text-start flex items-center gap-2.5 h-9 px-3 rounded-lg hover:bg-heat-soft text-heat text-[14px] disabled:opacity-50 cursor-pointer"
                      [disabled]="lastAdmin"
                      (click)="onDeleteUser(u)"
                    >
                      {{ i18n.t('delete') }}
                    </button>
                  </div>
                }
              </li>
            }
          </ul>
        }
      </div>
    </section>
  `,
})
export class UsersPage {
  protected readonly i18n = inject(TranslationService);
  protected readonly auth = inject(AuthStore);
  private readonly usersApi = inject(UsersApi);
  private readonly dashboardApi = inject(DashboardApi);
  private readonly toast = inject(ToastService);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly dialog = inject(Dialog);

  protected readonly roles = USER_ROLES;
  protected readonly filterOptions: readonly {
    value: UserActiveFilter;
    labelKey: 'filterAll' | 'filterActive' | 'filterDeactivated';
  }[] = [
    { value: 'all', labelKey: 'filterAll' },
    { value: 'active', labelKey: 'filterActive' },
    { value: 'deactivated', labelKey: 'filterDeactivated' },
  ];

  protected readonly activeFilter = signal<UserActiveFilter>('all');
  protected readonly searchQuery = signal('');
  protected readonly openMenuUserId = signal<string | null>(null);
  protected readonly runningReminders = signal(false);

  protected readonly usersResource = httpResource<User[]>(() => '/api/users');
  protected readonly allUsers = computed(() => this.usersResource.value() ?? []);

  protected readonly filteredUsers = computed(() => {
    const f = this.activeFilter();
    const q = this.searchQuery().trim().toLowerCase();
    return this.allUsers().filter((u) => {
      if (f === 'active' && !u.isActive) return false;
      if (f === 'deactivated' && u.isActive) return false;
      if (q && !u.fullName.toLowerCase().includes(q) && !u.email.toLowerCase().includes(q)) {
        return false;
      }
      return true;
    });
  });

  protected roleLabel(role: UserRole): string {
    const d = this.i18n.dict();
    return role === 'Admin' ? d.roleAdmin : role === 'Manager' ? d.roleManager : d.roleUser;
  }

  protected isLastAdmin(user: User): boolean {
    return isLastActiveAdmin(user, this.allUsers());
  }

  protected mayDeactivate(user: User): boolean {
    return canDeactivateUser(user, this.auth.user(), this.allUsers());
  }

  protected deactivateReason(user: User): string {
    if (user.id === this.auth.user()?.id) return this.i18n.t('selfDeactivateRule');
    if (this.isLastAdmin(user)) return this.i18n.t('lastAdminRule');
    return '';
  }

  protected onDocumentClick(event: Event): void {
    const target = event.target as HTMLElement | null;
    if (target?.closest('[role="menu"]')) return;
    this.openMenuUserId.set(null);
  }

  protected toggleMenu(event: Event, userId: string): void {
    event.stopPropagation();
    this.openMenuUserId.update((cur) => (cur === userId ? null : userId));
  }

  protected async onRoleChange(user: User, event: Event): Promise<void> {
    const select = event.target as HTMLSelectElement;
    const nextRole = select.value as UserRole;
    if (nextRole === user.role) return;

    const confirmed = await this.confirmDialog.confirm({
      title: this.i18n.dict().roleChangeTitle(user.fullName, this.roleLabel(nextRole)),
      body: this.i18n.t('roleChangeBody'),
      confirmLabel: this.i18n.t('changeRole'),
    });
    if (!confirmed) {
      select.value = user.role;
      return;
    }

    this.usersApi.changeRole(user.id, nextRole).subscribe({
      next: () => {
        this.toast.info(this.i18n.t('roleChanged'));
        this.usersResource.reload();
      },
      error: (err: unknown) => {
        select.value = user.role;
        this.toast.error(err instanceof ApiError ? err.detail : this.i18n.t('genericError'));
      },
    });
  }

  protected async onAddUser(): Promise<void> {
    const ref = this.dialog.open<User | null>(CreateUserDialogComponent, {
      backdropClass: ['bg-black/45', 'backdrop-blur-[2px]'],
      panelClass: ['focus:outline-none'],
      autoFocus: 'first-tabbable',
      restoreFocus: true,
    });
    const created = await firstValueFrom(ref.closed);
    if (!created) return;
    this.toast.info(this.i18n.t('userCreated'));
    this.usersResource.reload();
  }

  protected async onEditUser(user: User): Promise<void> {
    this.openMenuUserId.set(null);
    const ref = this.dialog.open<UpdateUserRequest | null, User>(EditUserDialogComponent, {
      data: user,
      backdropClass: ['bg-black/45', 'backdrop-blur-[2px]'],
      panelClass: ['focus:outline-none'],
      autoFocus: 'first-tabbable',
      restoreFocus: true,
    });
    const result = await firstValueFrom(ref.closed);
    if (!result) return;

    this.usersApi.update(user.id, result).subscribe({
      next: (updated) => {
        if (updated.id === this.auth.user()?.id) {
          this.auth.updateCurrentUser(updated);
        }
        this.toast.info(this.i18n.t('userSaved'));
        this.usersResource.reload();
      },
      error: (err: unknown) => {
        this.toast.error(err instanceof ApiError ? err.detail : this.i18n.t('genericError'));
      },
    });
  }

  protected async onToggleActive(user: User): Promise<void> {
    this.openMenuUserId.set(null);
    const nextActive = !user.isActive;
    const confirmed = await this.confirmDialog.confirm(
      nextActive
        ? {
            title: this.i18n.dict().reactivateTitle(user.fullName),
            body: this.i18n.t('reactivateBody'),
            confirmLabel: this.i18n.t('reactivate'),
          }
        : {
            title: this.i18n.dict().deactivateTitle(user.fullName),
            body: this.i18n.t('deactivateBody'),
            confirmLabel: this.i18n.t('deactivate'),
            danger: true,
          },
    );
    if (!confirmed) return;

    this.usersApi.setActive(user.id, nextActive).subscribe({
      next: () => {
        this.toast.info(
          nextActive ? this.i18n.t('userReactivated') : this.i18n.t('userDeactivated'),
        );
        this.usersResource.reload();
      },
      error: (err: unknown) => {
        this.toast.error(err instanceof ApiError ? err.detail : this.i18n.t('genericError'));
      },
    });
  }

  protected async onDeleteUser(user: User): Promise<void> {
    this.openMenuUserId.set(null);
    const confirmed = await this.confirmDialog.confirm({
      title: this.i18n.dict().deleteUserTitle(user.fullName),
      body: this.i18n.t('deleteUserBody'),
      confirmLabel: this.i18n.t('delete'),
      danger: true,
    });
    if (!confirmed) return;

    this.usersApi.delete(user.id).subscribe({
      next: () => {
        this.toast.info(this.i18n.t('userDeleted'));
        this.usersResource.reload();
      },
      error: (err: unknown) => {
        this.toast.error(err instanceof ApiError ? err.detail : this.i18n.t('genericError'));
      },
    });
  }

  protected onRunReminders(): void {
    if (this.runningReminders()) return;
    this.runningReminders.set(true);
    this.dashboardApi
      .runReminders()
      .pipe(finalize(() => this.runningReminders.set(false)))
      .subscribe({
        next: (result) => {
          if (result.remindersCreated > 0) {
            this.toast.info(this.i18n.dict().remindersSent(result.remindersCreated));
          } else {
            this.toast.info(this.i18n.t('noRemindersDue'));
          }
        },
        error: (err: unknown) => {
          this.toast.error(err instanceof ApiError ? err.detail : this.i18n.t('genericError'));
        },
      });
  }
}
