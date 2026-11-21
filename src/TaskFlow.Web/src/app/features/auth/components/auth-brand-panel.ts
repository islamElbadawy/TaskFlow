import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { TranslationService } from '../../../core/i18n/translation.service';
import { BrandMark } from '../../../shared/ui/brand-mark';

@Component({
  selector: 'app-auth-brand-panel',
  imports: [BrandMark],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="relative overflow-hidden bg-brand-panel text-white p-8 lg:p-10 flex flex-col min-h-72 h-full"
    >
      <div class="flex items-center gap-3">
        <app-brand-mark sizeClass="size-9" [inverse]="true" [draw]="true" />
        <span class="display text-2xl font-extrabold">TaskFlow</span>
      </div>

      <div class="mt-auto max-w-md relative z-10 pt-10">
        <p class="display text-[34px] lg:text-[44px] leading-[1.15] font-extrabold">
          {{ i18n.t('tagline') }}
        </p>
        <p class="mt-4 text-white/80 text-[17px]">
          {{ i18n.t('taglineSub') }}
        </p>
      </div>

      <div
        class="absolute end-10 top-24 hidden sm:flex gap-4 opacity-90 pointer-events-none"
        aria-hidden="true"
      >
        <div class="w-32 space-y-3">
          <div class="tf-walk h-16 rounded-xl bg-white/15" style="--i: 1"></div>
          <div class="tf-walk h-16 rounded-xl bg-white/15" style="--i: 2"></div>
          <div class="tf-walk h-16 rounded-xl bg-white/10" style="--i: 3"></div>
        </div>
        <div class="w-32 space-y-3 mt-10">
          <div class="tf-walk h-16 rounded-xl bg-white/25" style="--i: 4"></div>
          <div class="tf-walk h-16 rounded-xl bg-white/20" style="--i: 5"></div>
        </div>
        <div class="w-32 space-y-3 mt-20">
          <div class="tf-walk h-16 rounded-xl bg-white" style="--i: 6"></div>
        </div>
      </div>
    </div>
  `,
})
export class AuthBrandPanel {
  protected readonly i18n = inject(TranslationService);
}
