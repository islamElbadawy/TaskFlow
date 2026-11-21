import { Injectable, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class ReducedMotionService {
  private readonly _reduced = signal(this.queryMatches());
  readonly reduced = this._reduced.asReadonly();

  constructor() {
    if (typeof window !== 'undefined' && typeof window.matchMedia === 'function') {
      const media = window.matchMedia('(prefers-reduced-motion: reduce)');
      const onChange = (event: MediaQueryListEvent) => this._reduced.set(event.matches);
      media.addEventListener?.('change', onChange);
    }
  }

  private queryMatches(): boolean {
    if (typeof window === 'undefined' || typeof window.matchMedia !== 'function') {
      return false;
    }
    return window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  }
}
