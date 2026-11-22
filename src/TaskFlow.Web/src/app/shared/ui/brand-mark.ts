import { ChangeDetectionStrategy, Component, input } from '@angular/core';

@Component({
  selector: 'app-brand-mark',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <svg
      [class]="sizeClass()"
      viewBox="0 0 32 32"
      aria-hidden="true"
      [class.brand-mark-draw]="draw() && !loading()"
      [class.brand-mark-loading]="loading()"
    >
      <rect
        width="32"
        height="32"
        rx="9"
        [attr.fill]="inverse() ? 'var(--color-on-flow)' : 'var(--color-flow)'"
      />
      <line
        class="tf-bar tf-bar-1"
        x1="9"
        y1="22.5"
        x2="9"
        y2="9.5"
        [attr.stroke]="inverse() ? 'var(--color-brand-panel)' : 'var(--color-on-flow)'"
        stroke-width="3.6"
        stroke-linecap="round"
      />
      <line
        class="tf-bar tf-bar-2"
        x1="16"
        y1="22.5"
        x2="16"
        y2="15.5"
        [attr.stroke]="inverse() ? 'var(--color-brand-panel)' : 'var(--color-on-flow)'"
        stroke-width="3.6"
        stroke-linecap="round"
      />
      <line
        class="tf-bar tf-bar-3"
        x1="23"
        y1="22.5"
        x2="23"
        y2="19.5"
        [attr.stroke]="inverse() ? 'var(--color-brand-panel)' : 'var(--color-on-flow)'"
        stroke-width="3.6"
        stroke-linecap="round"
      />
    </svg>
  `,
})
export class BrandMark {
  readonly sizeClass = input('size-8');
  readonly inverse = input(false);
  readonly draw = input(false);
  readonly loading = input(false);
}
