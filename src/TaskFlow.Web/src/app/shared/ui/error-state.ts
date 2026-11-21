import { ChangeDetectionStrategy, Component, inject, input, output } from '@angular/core';
import { LucideCircleX } from '@lucide/angular';
import { TranslationService } from '../../core/i18n/translation.service';
import { AppButton } from './button';

@Component({
  selector: 'app-error-state',
  imports: [AppButton, LucideCircleX],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="bg-panel border border-line rounded-2xl text-center py-16 px-6" role="alert">
      <svg
        lucideCircleX
        class="size-10 mx-auto text-heat"
        [strokeWidth]="1.8"
        aria-hidden="true"
      ></svg>
      <p class="mt-4 text-[17px] font-bold">{{ title() || i18n.t('errorTitle') }}</p>
      <p class="text-slate mt-1">{{ detail() || i18n.t('errorBody') }}</p>
      <div class="mt-6">
        <app-button variant="quiet" (click)="retry.emit()">{{ i18n.t('retry') }}</app-button>
      </div>
    </div>
  `,
})
export class AppErrorState {
  protected readonly i18n = inject(TranslationService);

  readonly title = input<string | null>(null);
  readonly detail = input<string | null>(null);
  readonly retry = output<void>();
}
