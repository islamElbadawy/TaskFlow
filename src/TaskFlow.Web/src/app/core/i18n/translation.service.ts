import { DOCUMENT } from '@angular/common';
import { Injectable, computed, effect, inject, signal } from '@angular/core';
import { Dictionary, EN, TranslationKey } from '../../../i18n/en';
import { AppStorage } from '../storage/storage';

export type AppLang = 'en' | 'ar';
const LANG_STORAGE_KEY = 'tf-lang';

type StringKeys = {
  [K in TranslationKey]: Dictionary[K] extends string ? K : never;
}[TranslationKey];

@Injectable({ providedIn: 'root' })
export class TranslationService {
  private readonly doc = inject(DOCUMENT);
  private readonly storage = inject(AppStorage);

  private readonly _lang = signal<AppLang>(this.detectInitialLang());
  private readonly _dict = signal<Dictionary>(EN);

  readonly lang = this._lang.asReadonly();
  readonly dict = this._dict.asReadonly();
  readonly dir = computed<'ltr' | 'rtl'>(() => (this._lang() === 'ar' ? 'rtl' : 'ltr'));
  readonly locale = computed(() => (this._lang() === 'ar' ? 'ar-EG' : 'en-GB'));

  constructor() {
    effect(() => {
      const lang = this._lang();
      const root = this.doc.documentElement;
      root.lang = lang;
      root.dir = lang === 'ar' ? 'rtl' : 'ltr';
      this.storage.set(LANG_STORAGE_KEY, lang);
    });
  }

  private arCache: Dictionary | null = null;

  /** Called in provideAppInitializer so Arabic loads before first render if saved/detected. */
  async init(): Promise<void> {
    if (this._lang() === 'ar') {
      await this.loadArabic();
    } else if (typeof window !== 'undefined') {
      // Preload Arabic dictionary in the background so switching languages is instant.
      setTimeout(() => {
        void this.preloadArabic();
      }, 1000);
    }
  }

  async setLang(lang: AppLang): Promise<void> {
    if (lang === this._lang()) return;

    const apply = async () => {
      if (lang === 'ar') {
        await this.loadArabic();
      } else {
        this._dict.set(EN);
      }
      this._lang.set(lang);
    };

    const docWithVt = this.doc as Document & {
      startViewTransition?: (cb: () => void | Promise<void>) => { finished: Promise<void> };
    };
    const reduced =
      typeof window !== 'undefined' &&
      window.matchMedia?.('(prefers-reduced-motion: reduce)').matches;

    if (docWithVt.startViewTransition && !reduced) {
      if (lang === 'ar') {
        await this.loadArabic();
      }
      docWithVt.startViewTransition(() => {
        if (lang === 'en') {
          this._dict.set(EN);
        }
        this._lang.set(lang);
      });
    } else {
      await apply();
    }
  }

  async toggle(): Promise<void> {
    await this.setLang(this._lang() === 'en' ? 'ar' : 'en');
  }

  t(key: StringKeys): string {
    return this._dict()[key];
  }

  formatNumber(value: number): string {
    return new Intl.NumberFormat(this.locale()).format(value);
  }

  formatDate(date: Date, options?: Intl.DateTimeFormatOptions): string {
    return new Intl.DateTimeFormat(this.locale(), options).format(date);
  }

  formatRelativeTime(date: Date, now: Date = new Date()): string {
    const diffSeconds = Math.round((date.getTime() - now.getTime()) / 1000);
    const abs = Math.abs(diffSeconds);
    const rtf = new Intl.RelativeTimeFormat(this.locale(), { numeric: 'auto' });

    if (abs < 60) return rtf.format(0, 'minute');
    if (abs < 3600) return rtf.format(Math.round(diffSeconds / 60), 'minute');
    if (abs < 86_400) return rtf.format(Math.round(diffSeconds / 3600), 'hour');
    if (abs < 2_592_000) return rtf.format(Math.round(diffSeconds / 86_400), 'day');
    if (abs < 31_536_000) return rtf.format(Math.round(diffSeconds / 2_592_000), 'month');
    return rtf.format(Math.round(diffSeconds / 31_536_000), 'year');
  }

  private async preloadArabic(): Promise<Dictionary> {
    if (!this.arCache) {
      const mod = await import('../../../i18n/ar');
      this.arCache = mod.AR;
    }
    return this.arCache;
  }

  private async loadArabic(): Promise<void> {
    const ar = await this.preloadArabic();
    this._dict.set(ar);
  }

  private detectInitialLang(): AppLang {
    const saved = this.storage.get(LANG_STORAGE_KEY);
    if (saved === 'en' || saved === 'ar') return saved;
    const htmlLang = this.doc.documentElement.lang;
    if (htmlLang === 'ar') return 'ar';
    if (typeof navigator !== 'undefined' && navigator.language?.toLowerCase().startsWith('ar')) {
      return 'ar';
    }
    return 'en';
  }
}
