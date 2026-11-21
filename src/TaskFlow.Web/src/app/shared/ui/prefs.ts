import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { LucideGlobe, LucideMonitor, LucideMoon, LucideSun } from '@lucide/angular';
import { TranslationService } from '../../core/i18n/translation.service';
import { ThemePreference, ThemeStore } from '../../core/theme/theme.store';

@Component({
  selector: 'app-prefs',
  imports: [LucideMonitor, LucideSun, LucideMoon, LucideGlobe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex items-center gap-2">
      <div
        class="flex p-0.5 rounded-[var(--radius-control)] bg-sunk border border-line"
        role="group"
        [attr.aria-label]="i18n.t('theme')"
      >
        @for (opt of themes; track opt.value) {
          <button
            type="button"
            class="tf-transition h-8 w-8 grid place-items-center rounded-lg cursor-pointer"
            [class.bg-panel]="themeStore.theme() === opt.value"
            [class.text-ink]="themeStore.theme() === opt.value"
            [class.shadow-xs]="themeStore.theme() === opt.value"
            [class.text-slate]="themeStore.theme() !== opt.value"
            [class.hover:text-ink]="themeStore.theme() !== opt.value"
            [attr.aria-pressed]="themeStore.theme() === opt.value"
            [title]="i18n.t(opt.labelKey)"
            (click)="selectTheme(opt.value, $event)"
          >
            @switch (opt.value) {
              @case ('system') {
                <svg lucideMonitor class="size-4" [strokeWidth]="1.8" aria-hidden="true"></svg>
              }
              @case ('light') {
                <svg lucideSun class="size-4" [strokeWidth]="1.8" aria-hidden="true"></svg>
              }
              @case ('dark') {
                <svg lucideMoon class="size-4" [strokeWidth]="1.8" aria-hidden="true"></svg>
              }
            }
            <span class="sr-only">{{ i18n.t(opt.labelKey) }}</span>
          </button>
        }
      </div>

      <button
        type="button"
        class="tf-transition tf-press inline-flex items-center gap-2 h-9 px-3 rounded-[var(--radius-control)] border border-line text-[13px] font-bold text-ink hover:bg-ink/5 cursor-pointer"
        [attr.lang]="i18n.lang() === 'en' ? 'ar' : 'en'"
        (click)="i18n.toggle()"
      >
        <svg lucideGlobe class="size-4" [strokeWidth]="1.8" aria-hidden="true"></svg>
        <span>{{ i18n.t('otherLang') }}</span>
      </button>
    </div>
  `,
})
export class AppPrefs {
  protected readonly themeStore = inject(ThemeStore);
  protected readonly i18n = inject(TranslationService);

  protected readonly themes: readonly {
    value: ThemePreference;
    labelKey: 'system' | 'light' | 'dark';
  }[] = [
    { value: 'system', labelKey: 'system' },
    { value: 'light', labelKey: 'light' },
    { value: 'dark', labelKey: 'dark' },
  ];

  protected selectTheme(value: ThemePreference, event: Event): void {
    this.themeStore.setTheme(value, event.currentTarget as HTMLElement);
  }
}
