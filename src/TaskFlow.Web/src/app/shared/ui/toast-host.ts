import { ChangeDetectionStrategy, Component, OnDestroy, inject, input } from '@angular/core';
import { ToastItem, ToastService } from '../../core/toast/toast.service';
import { ReducedMotionService } from '../motion/reduced-motion';
import { MOTION } from '../motion/tokens';

@Component({
  selector: 'app-toast-item',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="relative overflow-hidden bg-panel border border-line rounded-2xl shadow-lg flex items-center gap-4 ps-4 pe-2 py-2 min-h-12 text-[14px] max-w-sm w-full sm:w-auto"
      [class.tf-toast-in]="!toast().leaving"
      [class.tf-toast-out]="toast().leaving"
      [attr.role]="toast().variant === 'error' ? 'alert' : 'status'"
      (mouseenter)="pause()"
      (mouseleave)="resume()"
    >
      <span
        class="size-2 rounded-full shrink-0"
        [class.bg-heat]="toast().variant === 'error'"
        [class.bg-flow]="toast().variant === 'info'"
        aria-hidden="true"
      ></span>
      <span class="flex-1 py-1 text-ink">{{ toast().message }}</span>

      @if (toast().action; as act) {
        <button
          type="button"
          class="tf-transition tf-press h-8 px-3 rounded-[var(--radius-control)] font-bold text-[13px] text-flow hover:bg-flow-soft cursor-pointer"
          (click)="runAction()"
        >
          {{ act.label }}
        </button>
      }

      <span
        class="tf-toast-bar"
        [class.text-heat]="toast().variant === 'error'"
        [class.text-flow]="toast().variant === 'info'"
        [style.animation-duration.ms]="toast().durationMs"
        [style.animation-play-state]="paused ? 'paused' : 'running'"
        aria-hidden="true"
      ></span>
    </div>
  `,
})
export class AppToastItem implements OnDestroy {
  private readonly toastService = inject(ToastService);
  private readonly reduced = inject(ReducedMotionService);

  readonly toast = input.required<ToastItem>();

  protected paused = false;
  private remainingMs = 4000;
  private startedAt = Date.now();
  private dismissTimer: ReturnType<typeof setTimeout> | null = null;
  private removeTimer: ReturnType<typeof setTimeout> | null = null;

  constructor() {
    queueMicrotask(() => {
      this.remainingMs = this.toast().durationMs;
      this.arm();
    });
  }

  ngOnDestroy(): void {
    this.clearTimers();
  }

  protected pause(): void {
    this.paused = true;
    this.clearTimers();
    this.remainingMs = Math.max(0, this.remainingMs - (Date.now() - this.startedAt));
  }

  protected resume(): void {
    this.paused = false;
    this.arm();
  }

  protected runAction(): void {
    this.toast().action?.callback();
    this.dismiss();
  }

  private arm(): void {
    this.clearTimers();
    this.startedAt = Date.now();
    this.dismissTimer = setTimeout(() => this.dismiss(), this.remainingMs);
  }

  private dismiss(): void {
    this.clearTimers();
    const id = this.toast().id;
    if (this.reduced.reduced()) {
      this.toastService.remove(id);
      return;
    }
    this.toastService.markLeaving(id);
    this.removeTimer = setTimeout(() => this.toastService.remove(id), MOTION.EXIT_MS);
  }

  private clearTimers(): void {
    if (this.dismissTimer) clearTimeout(this.dismissTimer);
    if (this.removeTimer) clearTimeout(this.removeTimer);
    this.dismissTimer = null;
    this.removeTimer = null;
  }
}

@Component({
  selector: 'app-toast-host',
  imports: [AppToastItem],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="fixed bottom-6 end-6 start-6 sm:start-auto flex flex-col items-end gap-2 z-50 pointer-events-none"
      aria-live="polite"
    >
      @for (item of toastService.toasts(); track item.id) {
        <div class="pointer-events-auto w-full sm:w-auto flex justify-end">
          <app-toast-item [toast]="item" />
        </div>
      }
    </div>
  `,
})
export class AppToastHost {
  protected readonly toastService = inject(ToastService);
}
