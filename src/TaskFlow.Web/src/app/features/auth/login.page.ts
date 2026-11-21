import { ChangeDetectionStrategy, Component, OnDestroy, computed, inject, signal } from '@angular/core';
import { FormField, email, form, required, submit } from '@angular/forms/signals';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { LucideCircleX, LucideClock, LucideEye, LucideEyeOff } from '@lucide/angular';
import { finalize } from 'rxjs';
import { AuthStore } from '../../core/auth/auth.store';
import { ApiError } from '../../core/http/api-error';
import { TranslationService } from '../../core/i18n/translation.service';
import { StaggerDirective } from '../../shared/motion/stagger.directive';
import { AppButton } from '../../shared/ui/button';
import { AppPrefs } from '../../shared/ui/prefs';
import { AuthBrandPanel } from './components/auth-brand-panel';

interface LoginModel {
  email: string;
  password: string;
}

@Component({
  selector: 'app-login-page',
  imports: [
    AuthBrandPanel,
    AppPrefs,
    AppButton,
    FormField,
    RouterLink,
    StaggerDirective,
    LucideClock,
    LucideCircleX,
    LucideEye,
    LucideEyeOff,
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
          <h1 class="display text-3xl font-extrabold" tabindex="-1">{{ i18n.t('logIn') }}</h1>
          <p class="text-slate mt-1">{{ i18n.t('welcomeSub') }}</p>

          @if (sessionEnded()) {
            <div
              class="mt-6 flex gap-2.5 items-start rounded-xl border border-line bg-sunk px-3.5 py-3 text-[14px]"
              role="status"
            >
              <svg
                lucideClock
                class="size-5 shrink-0 text-slate mt-0.5"
                [strokeWidth]="1.8"
                aria-hidden="true"
              ></svg>
              <span>{{ i18n.t('sessionEnded') }}</span>
            </div>
          }

          @if (formError()) {
            <div
              role="alert"
              class="mt-6 flex gap-2.5 items-start rounded-xl bg-heat-soft text-heat px-3.5 py-3 text-[14px] font-bold"
              [class.tf-shake]="shakeTick() > 0"
            >
              <svg
                lucideCircleX
                class="size-5 shrink-0 mt-0.5"
                [strokeWidth]="1.8"
                aria-hidden="true"
              ></svg>
              <span>{{ formError() }}</span>
            </div>
          }

          <div class="mt-7">
            <label class="block font-bold text-[14px] mb-1.5" for="login-email">
              {{ i18n.t('email') }}
            </label>
            <input
              id="login-email"
              type="email"
              dir="ltr"
              autocomplete="email"
              class="tf-transition h-10 px-3 rounded-[var(--radius-control)] bg-panel border border-line text-[14px] w-full text-start hover:border-slate/50"
              [class.border-heat]="loginForm.email().touched() && loginForm.email().invalid()"
              [attr.aria-invalid]="loginForm.email().touched() && loginForm.email().invalid()"
              [formField]="loginForm.email"
            />
            @if (loginForm.email().touched() && loginForm.email().invalid()) {
              <p class="text-heat text-[13px] mt-1.5">
                {{ loginForm.email().errors()[0]?.message || i18n.t('emailInvalid') }}
              </p>
            }
          </div>

          <div class="mt-5">
            <label class="block font-bold text-[14px] mb-1.5" for="login-password">
              {{ i18n.t('password') }}
            </label>
            <div class="relative">
              <input
                id="login-password"
                [type]="showPassword() ? 'text' : 'password'"
                autocomplete="current-password"
                class="tf-transition h-10 ps-3 pe-11 rounded-[var(--radius-control)] bg-panel border border-line text-[14px] w-full hover:border-slate/50"
                [class.border-heat]="
                  loginForm.password().touched() && loginForm.password().invalid()
                "
                [attr.aria-invalid]="
                  loginForm.password().touched() && loginForm.password().invalid()
                "
                [formField]="loginForm.password"
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
            @if (loginForm.password().touched() && loginForm.password().invalid()) {
              <p class="text-heat text-[13px] mt-1.5">
                {{ loginForm.password().errors()[0]?.message || i18n.t('fieldRequired') }}
              </p>
            }
          </div>

          <div class="mt-8">
            <app-button
              type="submit"
              [fullWidth]="true"
              [busy]="submitting()"
              [disabled]="cooldown() > 0"
            >
              {{ i18n.t('logIn') }}
            </app-button>
          </div>

          <p class="text-center text-slate mt-6 text-[14px]">
            {{ i18n.t('newHere') }}
            <a routerLink="/register" class="text-flow font-bold hover:underline">
              {{ i18n.t('createAccount') }}
            </a>
          </p>
        </form>
      </div>
    </div>
  `,
})
export class LoginPage implements OnDestroy {
  protected readonly i18n = inject(TranslationService);
  private readonly auth = inject(AuthStore);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly model = signal<LoginModel>({ email: '', password: '' });
  protected readonly loginForm = form(this.model, (p) => {
    required(p.email, { message: () => this.i18n.t('fieldRequired') });
    email(p.email, { message: () => this.i18n.t('emailInvalid') });
    required(p.password, { message: () => this.i18n.t('fieldRequired') });
  });

  protected readonly showPassword = signal(false);
  protected readonly submitting = signal(false);
  protected readonly cooldown = signal(0);
  protected readonly rawError = signal<string | null>(null);
  protected readonly shakeTick = signal(0);

  protected readonly sessionEnded = computed(
    () => this.route.snapshot.queryParamMap.get('reason') === 'session',
  );

  protected readonly formError = computed(() => {
    const cd = this.cooldown();
    if (cd > 0) {
      return this.i18n.dict().tooManyAttempts(cd);
    }
    return this.rawError();
  });

  private cooldownTimer: ReturnType<typeof setInterval> | null = null;

  ngOnDestroy(): void {
    this.clearCooldown();
  }

  protected async onSubmit(event: Event): Promise<void> {
    event.preventDefault();
    if (this.submitting() || this.cooldown() > 0) return;

    await submit(this.loginForm, async () => {
      this.submitting.set(true);
      this.rawError.set(null);
      const { email: em, password } = this.model();

      this.auth
        .login({ email: em.trim(), password })
        .pipe(finalize(() => this.submitting.set(false)))
        .subscribe({
          next: () => {
            const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
            const safeTarget =
              returnUrl && returnUrl.startsWith('/') && !returnUrl.startsWith('//')
                ? returnUrl
                : '/dashboard';
            void this.router.navigateByUrl(safeTarget);
          },
          error: (err: unknown) => {
            this.shakeTick.update((v) => v + 1);
            if (err instanceof ApiError) {
              if (err.status === 401) {
                this.rawError.set(this.i18n.t('badCredentials'));
              } else if (err.status === 429) {
                this.startCooldown(err.retryAfterSeconds ?? 60);
              } else {
                this.rawError.set(err.detail || this.i18n.t('genericError'));
              }
            } else {
              this.rawError.set(this.i18n.t('genericError'));
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
