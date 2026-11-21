import { ChangeDetectionStrategy, Component, input } from '@angular/core';

@Component({
  selector: 'app-brand-mark',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <svg
      [class]="sizeClass()"
      viewBox="0 0 32 32"
      aria-hidden="true"
      [class.brand-mark-draw]="draw()"
    >
      <rect
        width="32"
        height="32"
        rx="9"
        [attr.fill]="inverse() ? 'var(--color-on-flow)' : 'var(--color-flow)'"
      />
      <path
        d="M9 22.5V9.5M16 22.5v-7M23 22.5v-3"
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
}
