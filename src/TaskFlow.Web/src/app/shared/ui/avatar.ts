import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { TranslationService } from '../../core/i18n/translation.service';

export type AvatarSize = 'sm' | 'md' | 'lg';

const SIZE_MAP: Record<AvatarSize, string> = {
  sm: 'size-6 text-[11px]',
  md: 'size-8 text-[12px]',
  lg: 'size-9 text-sm',
};

@Component({
  selector: 'app-avatar',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span [class]="classes()" [title]="tooltip()" aria-hidden="true">
      {{ initials() }}
    </span>
  `,
})
export class AppAvatar {
  private readonly i18n = inject(TranslationService);

  readonly name = input<string | null | undefined>(null);
  readonly size = input<AvatarSize>('sm');
  readonly solid = input(false);

  protected readonly initials = computed(() => {
    const raw = this.name()?.trim();
    if (!raw) return '';
    const parts = raw.split(/\s+/).filter(Boolean);
    if (/[\u0600-\u06FF]/.test(raw)) {
      return parts[0]?.[0] ?? '';
    }
    if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
    return `${parts[0][0]}${parts[parts.length - 1][0]}`.toUpperCase();
  });

  protected readonly tooltip = computed(() => {
    const raw = this.name()?.trim();
    return raw || this.i18n.t('unassigned');
  });

  protected readonly classes = computed(() => {
    const size = SIZE_MAP[this.size()];
    const hasName = Boolean(this.name()?.trim());
    const tone = !hasName
      ? 'border border-dashed border-slate text-slate'
      : this.solid()
        ? 'bg-ink text-paper'
        : 'bg-ink/10 text-ink';
    return `${size} ${tone} shrink-0 rounded-full grid place-items-center font-bold select-none`;
  });
}
