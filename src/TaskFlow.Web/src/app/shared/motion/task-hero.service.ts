import { Injectable } from '@angular/core';

/**
 * Coordinates the `view-transition-name: task-hero` shared element morph
 * between a clicked task title (in Dashboard/List/Board) and the Task page `<h1>`.
 */
@Injectable({ providedIn: 'root' })
export class TaskHeroService {
  private sourceEl: HTMLElement | null = null;
  private targetEl: HTMLElement | null = null;

  markSource(el: HTMLElement | null): void {
    this.clear();
    if (!el) return;
    this.sourceEl = el;
    el.style.viewTransitionName = 'task-hero';
  }

  markTarget(el: HTMLElement | null): void {
    if (this.sourceEl) {
      this.sourceEl.style.viewTransitionName = '';
      this.sourceEl = null;
    }
    if (!el) return;
    this.targetEl = el;
    el.style.viewTransitionName = 'task-hero';
  }

  clear(): void {
    if (this.sourceEl) {
      this.sourceEl.style.viewTransitionName = '';
      this.sourceEl = null;
    }
    if (this.targetEl) {
      this.targetEl.style.viewTransitionName = '';
      this.targetEl = null;
    }
  }
}
