import { DOCUMENT } from '@angular/common';
import { Injectable, effect, inject, signal } from '@angular/core';
import { AppStorage } from '../storage/storage';

export type ThemePreference = 'system' | 'light' | 'dark';
const THEME_STORAGE_KEY = 'tf-theme';

@Injectable({ providedIn: 'root' })
export class ThemeStore {
  private readonly doc = inject(DOCUMENT);
  private readonly storage = inject(AppStorage);

  private readonly _theme = signal<ThemePreference>(this.readInitial());
  readonly theme = this._theme.asReadonly();

  constructor() {
    effect(() => {
      const value = this._theme();
      const root = this.doc.documentElement;
      if (value === 'system') {
        delete root.dataset['theme'];
      } else {
        root.dataset['theme'] = value;
      }
      this.storage.set(THEME_STORAGE_KEY, value);
    });
  }

  setTheme(next: ThemePreference, originElement?: HTMLElement | null): void {
    if (next === this._theme()) return;

    const docWithVt = this.doc as Document & {
      startViewTransition?: (cb: () => void) => { finished: Promise<void> };
    };
    const reduced =
      typeof window !== 'undefined' &&
      window.matchMedia?.('(prefers-reduced-motion: reduce)').matches;

    if (!originElement || !docWithVt.startViewTransition || reduced) {
      this._theme.set(next);
      return;
    }

    const rect = originElement.getBoundingClientRect();
    const x = rect.left + rect.width / 2;
    const y = rect.top + rect.height / 2;
    const radius = Math.hypot(
      Math.max(x, window.innerWidth - x),
      Math.max(y, window.innerHeight - y),
    );
    const root = this.doc.documentElement;
    root.style.setProperty('--rx', `${x}px`);
    root.style.setProperty('--ry', `${y}px`);
    root.style.setProperty('--rr', `${radius}px`);
    root.classList.add('theme-vt');

    const vt = docWithVt.startViewTransition(() => {
      this._theme.set(next);
    });
    vt.finished.finally(() => root.classList.remove('theme-vt'));
  }

  private readInitial(): ThemePreference {
    const saved = this.storage.get(THEME_STORAGE_KEY);
    if (saved === 'system' || saved === 'light' || saved === 'dark') {
      return saved;
    }
    return 'system';
  }
}
