import { ChangeDetectionStrategy, Component, OnDestroy, computed, inject, signal } from '@angular/core';
import { FormField, email, form, minLength, required, submit, validate } from '@angular/forms/signals';
import { Router, RouterLink } from '@angular/router';
import { LucideCircleX } from '@lucide/angular';
import { finalize } from 'rxjs';
import { LIMITS } from '../../core/api/limits';
import { AuthStore } from '../../core/auth/auth.store';
import { ApiError } from '../../core/http/api-error';
import { TranslationService } from '../../core/i18n/translation.service';
import { ToastService } from '../../core/toast/toast.service';
import { StaggerDirective } from '../../shared/motion/stagger.directive';
import { AppButton } from '../../shared/ui/button';
import { AppPrefs } from '../../shared/ui/prefs';
import { AuthBrandPanel } from './components/auth-brand-panel';

interface RegisterModel {
  firstName: string;
  lastName: string;
  email: string;
  password: string;
  confirmPassword: string;
}

@Component({
  selector: 'app-register-page',
  imports: [
    AuthBrandPanel,
    AppPrefs,
    AppButton,
    FormField,
    RouterLink,
    StaggerDirective,
    LucideCircleX,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="min-h-screen grid lg:grid-cols-2">
      <app-auth-brand-panel />

      <div class="relative grid place-items-center p-8 pt-20">
        <div class="absolute top-4 end-4">
          <app-prefs />
        </div>

        <form class="w-full max-w-sm" appStagger novalidate (submit)="onSubmit($event)">
          <h1 class="display text-3xl font-extrabold" tabindex="-1">
            {{ i18n.t('createAccountTitle') }}
          </h1>
          <p class="text-slate mt-1">{{ i18n.t('registerSub') }}</p>

          @if (bannerError()) {
            <div
              role="alert"
              class="mt-6 flex gap-2.5 items-start rounded-xl bg-heat-soft text-heat px-3.5 py-3 text-[14px] font-bold"
            >
              <svg
                lucideCircleX
                class="size-5 shrink-0 mt-0.5"
                [strokeWidth]="1.8"
                aria-hidden="true"
              ></svg>
              <span>{{ bannerError() }}</span>
            </div>
          }

          <div class="grid grid-cols-2 gap-3 mt-7">
            <div>
              <label class="block font-bold text-[14px] mb-1.5" for="reg-first">
                {{ i18n.t('firstName') }}
              </label>
              <input
                id="reg-first"
                autocomplete="given-name"
                class="tf-transition h-10 px-3 rounded-[var(--radius-control)] bg-panel border border-line text-[14px] w-full hover:border-slate/50"
                [class.border-heat]="fieldError('firstName') !== null"
                [attr.aria-invalid]="fieldError('firstName') !== null"
                [formField]="regForm.firstName"
              />
              @if (fieldError('firstName'); as err) {
                <p class="text-heat text-[13px] mt-1.5">{{ err }}</p>
              }
            </div>

            <div>
              <label class="block font-bold text-[14px] mb-1.5" for="reg-last">
                {{ i18n.t('lastName') }}
              </label>
              <input
                id="reg-last"
                autocomplete="family-name"
                class="tf-transition h-10 px-3 rounded-[var(--radius-control)] bg-panel border border-line text-[14px] w-full hover:border-slate/50"
                [class.border-heat]="fieldError('lastName') !== null"
                [attr.aria-invalid]="fieldError('lastName') !== null"
                [formField]="regForm.lastName"
              />
              @if (fieldError('lastName'); as err) {
                <p class="text-heat text-[13px] mt-1.5">{{ err }}</p>
              }
            </div>
          </div>

          <div class="mt-5">
            <label class="block font-bold text-[14px] mb-1.5" for="reg-email">
              {{ i18n.t('email') }}
            </label>
            <input
              id="reg-email"
              type="email"
              dir="ltr"
              autocomplete="email"
              class="tf-transition h-10 px-3 rounded-[var(--radius-control)] bg-panel border border-line text-[14px] w-full text-start hover:border-slate/50"
              [class.border-heat]="fieldError('email') !== null"
              [attr.aria-invalid]="fieldError('email') !== null"
              [formField]="regForm.email"
            />
            @if (fieldError('email'); as err) {
              <p class="text-heat text-[13px] mt-1.5">{{ err }}</p>
            }
          </div>

          <div class="mt-5">
            <label class="block font-bold text-[14px] mb-1.5" for="reg-password">
              {{ i18n.t('password') }}
            </label>
            <input
              id="reg-password"
              type="password"
              autocomplete="new-password"
              class="tf-transition h-10 px-3 rounded-[var(--radius-control)] bg-panel border border-line text-[14px] w-full hover:border-slate/50"
              [class.border-heat]="fieldError('password') !== null"
              [attr.aria-invalid]="fieldError('password') !== null"
              [formField]="regForm.password"
            />
            <p class="text-slate text-[13px] mt-1.5">{{ i18n.t('passwordHint') }}</p>
            @if (fieldError('password'); as err) {
              <p class="text-heat text-[13px] mt-1">{{ err }}</p>
            }
          </div>

          <div class="mt-5">
            <label class="block font-bold text-[14px] mb-1.5" for="reg-confirm">
              {{ i18n.t('confirmPassword') }}
            </label>
            <input
              id="reg-confirm"
              type="password"
              autocomplete="new-password"
              class="tf-transition h-10 px-3 rounded-[var(--radius-control)] bg-panel border border-line text-[14px] w-full hover:border-slate/50"
              [class.border-heat]="fieldError('confirmPassword') !== null"
              [attr.aria-invalid]="fieldError('confirmPassword') !== null"
              [formField]="regForm.confirmPassword"
            />
            @if (fieldError('confirmPassword'); as err) {
              <p class="text-heat text-[13px] mt-1.5">{{ err }}</p>
            }
          </div>

          <div class="mt-8">
            <app-button
              type="submit"
              [fullWidth]="true"
              [busy]="submitting()"
              [disabled]="cooldown() > 0"
            >
              {{ i18n.t('createAccountBtn') }}
            </app-button>
          </div>

          <p class="text-center text-slate mt-6 text-[14px]">
            {{ i18n.t('haveAccount') }}
            <a routerLink="/login" class="text-flow font-bold hover:underline">
              {{ i18n.t('logIn') }}
            </a>
          </p>
        </form>
      </div>
    </div>
  `,
})
export class RegisterPage implements OnDestroy {
  protected readonly i18n = inject(TranslationService);
  private readonly auth = inject(AuthStore);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);

  protected readonly model = signal<RegisterModel>({
    firstName: '',
    lastName: '',
    email: '',
    password: '',
    confirmPassword: '',
  });

  protected readonly regForm = form(this.model, (p) => {
    required(p.firstName, { message: () => this.i18n.t('fieldRequired') });
    required(p.lastName, { message: () => this.i18n.t('fieldRequired') });
    required(p.email, { message: () => this.i18n.t('fieldRequired') });
    email(p.email, { message: () => this.i18n.t('emailInvalid') });
    required(p.password, { message: () => this.i18n.t('fieldRequired') });
    minLength(p.password, LIMITS.PASSWORD_MIN, { message: () => this.i18n.t('passwordShort') });
    required(p.confirmPassword, { message: () => this.i18n.t('fieldRequired') });
    validate(p.confirmPassword, ({ value }) => {
      if (value() !== this.model().password) {
        return { kind: 'mismatch', message: this.i18n.t('passwordMismatch') };
      }
      return null;
    });
  });

  protected readonly submitting = signal(false);
  protected readonly cooldown = signal(0);
  protected readonly serverFieldErrors = signal<Record<string, string>>({});
  protected readonly rawBannerError = signal<string | null>(null);

  protected readonly bannerError = computed(() => {
    const cd = this.cooldown();
    if (cd > 0) return this.i18n.dict().tooManyAttempts(cd);
    return this.rawBannerError();
  });

  private cooldownTimer: ReturnType<typeof setInterval> | null = null;

  ngOnDestroy(): void {
    this.clearCooldown();
  }

  protected fieldError(field: keyof RegisterModel): string | null {
    const server = this.serverFieldErrors()[field];
    if (server) return server;
    const state = this.regForm[field]();
    if (state.touched() && state.invalid()) {
      return state.errors()[0]?.message ?? this.i18n.t('fieldRequired');
    }
    return null;
  }

  protected async onSubmit(event: Event): Promise<void> {
    event.preventDefault();
    if (this.submitting() || this.cooldown() > 0) return;

    this.serverFieldErrors.set({});
    this.rawBannerError.set(null);

    await submit(this.regForm, async () => {
      this.submitting.set(true);
      const m = this.model();
      this.auth
        .registerAndLogin({
          firstName: m.firstName.trim(),
          lastName: m.lastName.trim(),
          email: m.email.trim(),
          password: m.password,
        })
        .pipe(finalize(() => this.submitting.set(false)))
        .subscribe({
          next: () => {
            this.toast.info(this.i18n.t('welcomeToast'));
            void this.router.navigate(['/dashboard']);
          },
          error: (err: unknown) => {
            if (err instanceof ApiError) {
              if (err.status === 409) {
                this.serverFieldErrors.set({ email: this.i18n.t('emailTaken') });
              } else if (err.status === 429) {
                this.startCooldown(err.retryAfterSeconds ?? 60);
              } else if (err.status === 400) {
                const mapped: Record<string, string> = {};
                for (const key of ['firstName', 'lastName', 'email', 'password'] as const) {
                  const msg = err.fieldError(key);
                  if (msg) mapped[key] = msg;
                }
                this.serverFieldErrors.set(mapped);
                if (Object.keys(mapped).length === 0) {
                  this.rawBannerError.set(err.detail || this.i18n.t('genericError'));
                }
              } else {
                this.rawBannerError.set(err.detail || this.i18n.t('genericError'));
              }
            } else {
              this.rawBannerError.set(this.i18n.t('genericError'));
            }
          },
        });
    });
  }

  private startCooldown(seconds: number): void {
    this.clearCooldown();
    this.cooldown.set(Math.max(1, seconds));
    this.cooldownTimer = setInterval(() => {
      const next = this.cooldown() - 1;
      if (next <= 0) {
        this.clearCooldown();
        this.cooldown.set(0);
      } else {
        this.cooldown.set(next);
      }
    }, 1000);
  }

  private clearCooldown(): void {
    if (this.cooldownTimer) {
      clearInterval(this.cooldownTimer);
      this.cooldownTimer = null;
    }
  }
}
