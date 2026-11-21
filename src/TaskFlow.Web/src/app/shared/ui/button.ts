import { ChangeDetectionStrategy, Component, Directive, computed, input } from '@angular/core';

export type ButtonVariant = 'primary' | 'quiet' | 'ghost' | 'danger' | 'danger-solid';
export type ButtonSize = 'md' | 'sm' | 'xs';

const VARIANT_CLASSES: Record<ButtonVariant, string> = {
  primary: 'bg-flow text-on-flow hover:bg-flow-deep',
  quiet: 'border border-line text-ink hover:bg-ink/5',
  ghost: 'text-slate hover:text-ink hover:bg-ink/5',
  danger: 'text-heat hover:bg-heat-soft',
  'danger-solid': 'bg-heat text-on-heat hover:opacity-90',
};

const SIZE_CLASSES: Record<ButtonSize, string> = {
  md: 'h-10 px-4 text-[14px]',
  sm: 'h-9 px-3 text-[13px]',
  xs: 'h-8 px-3 text-[13px]',
};

function buildButtonClasses(variant: ButtonVariant, size: ButtonSize, fullWidth: boolean): string {
  const base =
    'tf-transition tf-press inline-flex items-center justify-center gap-2 rounded-[var(--radius-control)] font-bold whitespace-nowrap no-underline cursor-pointer disabled:opacity-50 disabled:pointer-events-none';
  const width = fullWidth ? 'w-full' : '';
  return `${base} ${SIZE_CLASSES[size]} ${VARIANT_CLASSES[variant]} ${width}`;
}

@Component({
  selector: 'app-button',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button
      [type]="type()"
      [disabled]="disabled() || busy()"
      [attr.aria-busy]="busy() ? 'true' : null"
      [attr.aria-label]="ariaLabel()"
      [class]="classes()"
    >
      @if (busy()) {
        <span
          class="size-4 rounded-full border-2 border-current border-e-transparent animate-spin shrink-0"
          aria-hidden="true"
        ></span>
      }
      <ng-content />
    </button>
  `,
})
export class AppButton {
  readonly variant = input<ButtonVariant>('primary');
  readonly size = input<ButtonSize>('md');
  readonly type = input<'button' | 'submit' | 'reset'>('button');
  readonly disabled = input(false);
  readonly busy = input(false);
  readonly fullWidth = input(false);
  readonly ariaLabel = input<string | null>(null);

  protected readonly classes = computed(() =>
    buildButtonClasses(this.variant(), this.size(), this.fullWidth()),
  );
}

/** Button-styled anchor directive — avoids nesting <button> inside <a>. */
@Directive({
  selector: 'a[appButton]',
  host: {
    '[class]': 'classes()',
  },
})
export class AppButtonLink {
  readonly variant = input<ButtonVariant>('primary');
  readonly size = input<ButtonSize>('md');
  readonly fullWidth = input(false);

  protected readonly classes = computed(() =>
    buildButtonClasses(this.variant(), this.size(), this.fullWidth()),
  );
}
