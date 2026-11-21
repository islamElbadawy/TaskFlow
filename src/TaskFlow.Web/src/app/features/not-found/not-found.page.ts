import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthStore } from '../../core/auth/auth.store';
import { TranslationService } from '../../core/i18n/translation.service';
import { StaggerDirective } from '../../shared/motion/stagger.directive';
import { BrandMark } from '../../shared/ui/brand-mark';
import { AppButtonLink } from '../../shared/ui/button';

@Component({
  selector: 'app-not-found-page',
  imports: [BrandMark, AppButtonLink, RouterLink, StaggerDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="min-h-[70vh] grid place-items-center text-center px-4">
      <div appStagger>
        <div class="flex justify-center">
          <app-brand-mark sizeClass="size-14" />
        </div>
        <h1 class="display text-3xl font-extrabold mt-6" tabindex="-1">
          {{ i18n.t('notFoundTitle') }}
        </h1>
        <p class="text-slate mt-2">{{ i18n.t('notFoundSub') }}</p>
        <div class="mt-8">
          <a [routerLink]="auth.isAuthenticated() ? '/dashboard' : '/login'" appButton>
            {{ i18n.t('goToday') }}
          </a>
        </div>
      </div>
    </div>
  `,
})
export class NotFoundPage {
  protected readonly i18n = inject(TranslationService);
  protected readonly auth = inject(AuthStore);
}
