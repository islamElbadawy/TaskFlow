import { ChangeDetectionStrategy, Component, effect, inject, signal } from '@angular/core';
import { FormField, email, form, minLength, required, submit, validate } from '@angular/forms/signals';
import { finalize } from 'rxjs';
import { AuthApi } from '../../core/api/auth.api';
import { LIMITS } from '../../core/api/limits';
import { UsersApi } from '../../core/api/users.api';
import { AuthStore } from '../../core/auth/auth.store';
import { ApiError } from '../../core/http/api-error';
import { TranslationService } from '../../core/i18n/translation.service';
import { ToastService } from '../../core/toast/toast.service';
import { StaggerDirective } from '../../shared/motion/stagger.directive';
import { AppButton } from '../../shared/ui/button';
import { ConfirmDialogService } from '../../shared/ui/confirm-dialog';
import { AppPrefs } from '../../shared/ui/prefs';

interface DetailsModel {
  firstName: string;
  lastName: string;
  email: string;
}

interface PasswordModel {
  currentPassword: string;
  newPassword: string;
  confirmPassword: string;
}

@Component({
  selector: 'app-profile-page',
  imports: [FormField, StaggerDirective, AppButton, AppPrefs],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section appStagger>
      <h1 class="display text-3xl font-extrabold" tabindex="-1">{{ i18n.t('profile') }}</h1>

      <div class="grid gap-6 mt-8 max-w-[760px]">
        <!-- 1. Your details -->
        <form
          class="bg-panel border border-line rounded-2xl p-6"
          novalidate
          (submit)="onSaveDetails($event)"
        >
          <h2 class="text-lg font-bold">{{ i18n.t('yourDetails') }}</h2>

          <div class="grid sm:grid-cols-2 gap-4 mt-5">
            <div>
              <label class="block font-bold text-[14px] mb-1.5" for="prof-first">
                {{ i18n.t('firstName') }}
              </label>
              <input
                id="prof-first"
                class="tf-transition h-10 px-3 rounded-[var(--radius-control)] bg-panel border border-line text-[14px] w-full hover:border-slate/50"
                [class.border-heat]="detailsError('firstName') !== null"
                [formField]="detailsForm.firstName"
              />
              @if (detailsError('firstName'); as err) {
                <p class="text-heat text-[13px] mt-1.5">{{ err }}</p>
              }
            </div>

            <div>
              <label class="block font-bold text-[14px] mb-1.5" for="prof-last">
                {{ i18n.t('lastName') }}
              </label>
              <input
                id="prof-last"
                class="tf-transition h-10 px-3 rounded-[var(--radius-control)] bg-panel border border-line text-[14px] w-full hover:border-slate/50"
                [class.border-heat]="detailsError('lastName') !== null"
                [formField]="detailsForm.lastName"
              />
              @if (detailsError('lastName'); as err) {
                <p class="text-heat text-[13px] mt-1.5">{{ err }}</p>
              }
            </div>
          </div>

          <div class="mt-4">
            <label class="block font-bold text-[14px] mb-1.5" for="prof-email">
              {{ i18n.t('email') }}
            </label>
            <input
              id="prof-email"
              type="email"
              dir="ltr"
              class="tf-transition h-10 px-3 rounded-[var(--radius-control)] bg-panel border border-line text-[14px] w-full sm:w-1/2 text-start hover:border-slate/50"
              [class.border-heat]="detailsError('email') !== null"
              [formField]="detailsForm.email"
            />
            @if (detailsError('email'); as err) {
              <p class="text-heat text-[13px] mt-1.5">{{ err }}</p>
            }
          </div>

          <div class="mt-6">
            <app-button type="submit" [busy]="savingDetails()">{{ i18n.t('save') }}</app-button>
          </div>
        </form>

        <!-- 2. Password -->
        <form
          class="bg-panel border border-line rounded-2xl p-6"
          novalidate
          (submit)="onChangePassword($event)"
        >
          <h2 class="text-lg font-bold">{{ i18n.t('passwordSection') }}</h2>

          <div class="grid sm:grid-cols-3 gap-4 mt-5">
            <div>
              <label class="block font-bold text-[14px] mb-1.5" for="prof-cur-pw">
                {{ i18n.t('currentPassword') }}
              </label>
              <input
                id="prof-cur-pw"
                type="password"
                autocomplete="current-password"
                class="tf-transition h-10 px-3 rounded-[var(--radius-control)] bg-panel border border-line text-[14px] w-full hover:border-slate/50"
                [class.border-heat]="pwError('currentPassword') !== null"
                [formField]="pwForm.currentPassword"
              />
              @if (pwError('currentPassword'); as err) {
                <p class="text-heat text-[13px] mt-1.5">{{ err }}</p>
              }
            </div>

            <div>
              <label class="block font-bold text-[14px] mb-1.5" for="prof-new-pw">
                {{ i18n.t('newPassword') }}
              </label>
              <input
                id="prof-new-pw"
                type="password"
                autocomplete="new-password"
                class="tf-transition h-10 px-3 rounded-[var(--radius-control)] bg-panel border border-line text-[14px] w-full hover:border-slate/50"
                [class.border-heat]="pwError('newPassword') !== null"
                [formField]="pwForm.newPassword"
              />
              <p class="text-slate text-[13px] mt-1.5">{{ i18n.t('passwordHint') }}</p>
              @if (pwError('newPassword'); as err) {
                <p class="text-heat text-[13px] mt-1">{{ err }}</p>
              }
            </div>

            <div>
              <label class="block font-bold text-[14px] mb-1.5" for="prof-cfm-pw">
                {{ i18n.t('confirmPassword') }}
              </label>
              <input
                id="prof-cfm-pw"
                type="password"
                autocomplete="new-password"
                class="tf-transition h-10 px-3 rounded-[var(--radius-control)] bg-panel border border-line text-[14px] w-full hover:border-slate/50"
                [class.border-heat]="pwError('confirmPassword') !== null"
                [formField]="pwForm.confirmPassword"
              />
              @if (pwError('confirmPassword'); as err) {
                <p class="text-heat text-[13px] mt-1.5">{{ err }}</p>
              }
            </div>
          </div>

          <div class="mt-6">
            <app-button type="submit" [busy]="changingPw()">
              {{ i18n.t('changePasswordBtn') }}
            </app-button>
          </div>
        </form>

        <!-- 3. Preferences -->
        <div class="bg-panel border border-line rounded-2xl p-6">
          <h2 class="text-lg font-bold">{{ i18n.t('preferences') }}</h2>
          <div class="flex flex-wrap items-center gap-x-8 gap-y-4 mt-5 text-[14px]">
            <div class="flex items-center gap-3 flex-wrap">
              <span class="text-slate">{{ i18n.t('themeAndLang') }}</span>
              <app-prefs />
            </div>
          </div>
        </div>

        <!-- 4. Danger zone -->
        <div class="bg-panel border border-heat/30 rounded-2xl p-6">
          <h2 class="text-lg font-bold text-heat">{{ i18n.t('dangerZone') }}</h2>
          <p class="text-slate mt-2 text-[14px]">{{ i18n.t('deleteAccountBody') }}</p>
          <div class="mt-5">
            <app-button
              variant="danger"
              [busy]="deletingAccount()"
              (click)="onDeleteAccount()"
            >
              {{ i18n.t('deleteAccount') }}
            </app-button>
          </div>
        </div>
      </div>
    </section>
  `,
})
export class ProfilePage {
  protected readonly i18n = inject(TranslationService);
  private readonly auth = inject(AuthStore);
  private readonly usersApi = inject(UsersApi);
  private readonly authApi = inject(AuthApi);
  private readonly toast = inject(ToastService);
  private readonly confirmDialog = inject(ConfirmDialogService);

  protected readonly detailsModel = signal<DetailsModel>({
    firstName: '',
    lastName: '',
    email: '',
  });
  protected readonly detailsForm = form(this.detailsModel, (p) => {
    required(p.firstName, { message: () => this.i18n.t('fieldRequired') });
    required(p.lastName, { message: () => this.i18n.t('fieldRequired') });
    required(p.email, { message: () => this.i18n.t('fieldRequired') });
    email(p.email, { message: () => this.i18n.t('emailInvalid') });
  });

  protected readonly pwModel = signal<PasswordModel>({
    currentPassword: '',
    newPassword: '',
    confirmPassword: '',
  });
  protected readonly pwForm = form(this.pwModel, (p) => {
    required(p.currentPassword, { message: () => this.i18n.t('fieldRequired') });
    required(p.newPassword, { message: () => this.i18n.t('fieldRequired') });
    minLength(p.newPassword, LIMITS.PASSWORD_MIN, {
      message: () => this.i18n.t('passwordShort'),
    });
    required(p.confirmPassword, { message: () => this.i18n.t('fieldRequired') });
    validate(p.confirmPassword, ({ value }) => {
      if (value() !== this.pwModel().newPassword) {
        return { kind: 'mismatch', message: this.i18n.t('passwordMismatch') };
      }
      return null;
    });
  });

  protected readonly savingDetails = signal(false);
  protected readonly changingPw = signal(false);
  protected readonly deletingAccount = signal(false);
  private readonly detailsServerErrors = signal<Record<string, string>>({});
  private readonly pwServerErrors = signal<Record<string, string>>({});
  private initialized = false;

  constructor() {
    effect(() => {
      const user = this.auth.user();
      if (user && !this.initialized) {
        this.initialized = true;
        this.detailsModel.set({
          firstName: user.firstName,
          lastName: user.lastName,
          email: user.email,
        });
      }
    });
  }

  protected detailsError(field: keyof DetailsModel): string | null {
    const server = this.detailsServerErrors()[field];
    if (server) return server;
    const state = this.detailsForm[field]();
    if (state.touched() && state.invalid()) {
      return state.errors()[0]?.message ?? this.i18n.t('fieldRequired');
    }
    return null;
  }

  protected pwError(field: keyof PasswordModel): string | null {
    const server = this.pwServerErrors()[field];
    if (server) return server;
    const state = this.pwForm[field]();
    if (state.touched() && state.invalid()) {
      return state.errors()[0]?.message ?? this.i18n.t('fieldRequired');
    }
    return null;
  }

  protected async onSaveDetails(event: Event): Promise<void> {
    event.preventDefault();
    const user = this.auth.user();
    if (!user || this.savingDetails()) return;
    this.detailsServerErrors.set({});

    await submit(this.detailsForm, async () => {
      this.savingDetails.set(true);
      const m = this.detailsModel();
      this.usersApi
        .update(user.id, {
          firstName: m.firstName.trim(),
          lastName: m.lastName.trim(),
          email: m.email.trim(),
        })
        .pipe(finalize(() => this.savingDetails.set(false)))
        .subscribe({
          next: (updated) => {
            this.auth.updateCurrentUser(updated);
            this.toast.info(this.i18n.t('profileSaved'));
          },
          error: (err: unknown) => {
            if (err instanceof ApiError && err.status === 409) {
              this.detailsServerErrors.set({ email: this.i18n.t('emailTaken') });
            } else {
              this.toast.error(err instanceof ApiError ? err.detail : this.i18n.t('genericError'));
            }
          },
        });
    });
  }

  protected async onChangePassword(event: Event): Promise<void> {
    event.preventDefault();
    if (this.changingPw()) return;
    this.pwServerErrors.set({});

    await submit(this.pwForm, async () => {
      this.changingPw.set(true);
      const m = this.pwModel();
      this.authApi
        .changePassword({
          currentPassword: m.currentPassword,
          newPassword: m.newPassword,
        })
        .pipe(finalize(() => this.changingPw.set(false)))
        .subscribe({
          next: () => {
            this.toast.info(this.i18n.t('passwordChanged'));
            void this.auth.signOut('manual');
          },
          error: (err: unknown) => {
            if (err instanceof ApiError && err.status === 400) {
              this.pwServerErrors.set({
                currentPassword:
                  err.fieldError('currentPassword') || this.i18n.t('wrongCurrentPassword'),
              });
            } else {
              this.toast.error(err instanceof ApiError ? err.detail : this.i18n.t('genericError'));
            }
          },
        });
    });
  }

  protected async onDeleteAccount(): Promise<void> {
    const user = this.auth.user();
    if (!user || this.deletingAccount()) return;

    const confirmed = await this.confirmDialog.confirm({
      title: this.i18n.t('deleteAccountTitle'),
      body: this.i18n.t('deleteAccountBody'),
      confirmLabel: this.i18n.t('deleteForever'),
      danger: true,
      typedConfirmation: user.email,
    });
    if (!confirmed) return;

    this.deletingAccount.set(true);
    this.usersApi
      .delete(user.id)
      .pipe(finalize(() => this.deletingAccount.set(false)))
      .subscribe({
        next: () => {
          void this.auth.signOut('manual');
        },
        error: (err: unknown) => {
          if (err instanceof ApiError && err.status === 409) {
            this.toast.error(err.detail || this.i18n.t('cantDeleteOwnAccount'));
          } else {
            this.toast.error(err instanceof ApiError ? err.detail : this.i18n.t('genericError'));
          }
        },
      });
  }
}
