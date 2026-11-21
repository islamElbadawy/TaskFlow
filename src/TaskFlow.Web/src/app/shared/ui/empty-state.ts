import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { BrandMark } from './brand-mark';
import { AppButton } from './button';

@Component({
  selector: 'app-empty-state',
  imports: [BrandMark, AppButton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="bg-panel border border-dashed border-line rounded-2xl text-center py-16 px-6">
      <div class="flex justify-center opacity-60">
        <app-brand-mark sizeClass="size-10" />
      </div>
      <p class="mt-4 text-[17px] text-ink">{{ message() }}</p>
      @if (actionLabel()) {
        <div class="mt-6">
          <app-button (click)="action.emit()">{{ actionLabel() }}</app-button>
        </div>
      }
    </div>
  `,
})
export class AppEmptyState {
  readonly message = input.required<string>();
  readonly actionLabel = input<string | null>(null);
  readonly action = output<void>();
}
