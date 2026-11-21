import { Injectable, inject } from '@angular/core';
import { TaskStatus } from '../../core/api/models';
import { ReducedMotionService } from '../../shared/motion/reduced-motion';
import { MOTION } from '../../shared/motion/tokens';

export interface BoardDropResult {
  readonly taskId: string;
  readonly targetStatus: TaskStatus;
  readonly beforeTaskId: string | null;
}

interface ActiveDrag {
  taskId: string;
  sourceStatus: TaskStatus;
  el: HTMLElement;
  scrollContainer: HTMLElement;
  startX: number;
  startY: number;
  pointerX: number;
  pointerY: number;
  started: boolean;
  rect: DOMRect;
  offsetX: number;
  offsetY: number;
  ghost: HTMLElement | null;
  placeholder: HTMLElement | null;
  currentCol: HTMLElement | null;
  tilt: number;
  lift: number;
  lastX: number;
  origIndex: number;
  rafId: number;
  settleUntil: number;
  retryTimer: ReturnType<typeof setTimeout> | null;
  allowReorder: boolean;
  onDrop: (result: BoardDropResult) => void;
}

@Injectable()
export class BoardDragService {
  private readonly reduced = inject(ReducedMotionService);
  private drag: ActiveDrag | null = null;
  private lastDragEndAt = 0;

  private readonly boundMove = (e: PointerEvent) => this.onPointerMove(e);
  private readonly boundUp = () => this.onPointerUp();
  private readonly boundCancel = () => this.endDrag(true);
  private readonly boundKeydown = (e: KeyboardEvent) => {
    if (e.key === 'Escape' && this.drag?.started) {
      this.endDrag(true);
    }
  };

  wasJustDragging(): boolean {
    return performance.now() - this.lastDragEndAt < 300;
  }

  beginPointerDown(params: {
    event: PointerEvent;
    cardEl: HTMLElement;
    scrollContainer: HTMLElement;
    taskId: string;
    sourceStatus: TaskStatus;
    allowReorder: boolean;
    onDrop: (result: BoardDropResult) => void;
  }): void {
    const { event, cardEl, scrollContainer, taskId, sourceStatus, allowReorder, onDrop } = params;
    if (event.button !== 0) return;

    this.cleanupListeners();
    this.drag = {
      taskId,
      sourceStatus,
      el: cardEl,
      scrollContainer,
      startX: event.clientX,
      startY: event.clientY,
      pointerX: event.clientX,
      pointerY: event.clientY,
      started: false,
      rect: cardEl.getBoundingClientRect(),
      offsetX: 0,
      offsetY: 0,
      ghost: null,
      placeholder: null,
      currentCol: null,
      tilt: 0,
      lift: 0,
      lastX: event.clientX,
      origIndex: 0,
      rafId: 0,
      settleUntil: 0,
      retryTimer: null,
      allowReorder,
      onDrop,
    };

    window.addEventListener('pointermove', this.boundMove, { passive: false });
    window.addEventListener('pointerup', this.boundUp);
    window.addEventListener('pointercancel', this.boundCancel);
    window.addEventListener('keydown', this.boundKeydown);
  }

  destroy(): void {
    if (this.drag?.started) {
      this.endDrag(true);
    } else {
      this.drag = null;
      this.cleanupListeners();
    }
  }

  private onPointerMove(e: PointerEvent): void {
    const d = this.drag;
    if (!d) return;
    d.pointerX = e.clientX;
    d.pointerY = e.clientY;

    if (!d.started) {
      if (Math.hypot(e.clientX - d.startX, e.clientY - d.startY) < 5) return;
      this.startDrag(d);
    }

    e.preventDefault();
    this.updateTarget(d);
  }

  private onPointerUp(): void {
    if (this.drag?.started) {
      this.endDrag(false);
    } else {
      this.drag = null;
      this.cleanupListeners();
    }
  }

  private startDrag(d: ActiveDrag): void {
    const r = d.el.getBoundingClientRect();
    d.started = true;
    d.rect = r;
    d.offsetX = d.startX - r.left;
    d.offsetY = d.startY - r.top;

    const ghost = d.el.cloneNode(true) as HTMLElement;
    ghost.style.position = 'fixed';
    ghost.style.zIndex = '60';
    ghost.style.pointerEvents = 'none';
    ghost.style.margin = '0';
    ghost.style.willChange = 'transform';
    ghost.style.left = `${r.left}px`;
    ghost.style.top = `${r.top}px`;
    ghost.style.width = `${r.width}px`;
    ghost.style.height = `${r.height}px`;
    ghost.style.boxShadow = '0 22px 44px -14px rgb(0 0 0 / 0.38), 0 2px 6px rgb(0 0 0 / 0.08)';
    document.body.append(ghost);
    d.ghost = ghost;

    const ph = document.createElement('li');
    ph.className = 'rounded-xl border-2 border-dashed border-flow/50 bg-flow-soft/60 list-none';
    ph.dataset['ph'] = '1';
    ph.style.height = `${r.height}px`;
    d.el.before(ph);
    d.el.hidden = true;
    d.placeholder = ph;

    const col = d.el.closest<HTMLElement>('[data-col]');
    if (col) {
      this.highlightCol(col, true);
      d.currentCol = col;
      d.origIndex = Array.from(col.querySelectorAll<HTMLElement>(':scope > li[data-task-id]')).indexOf(
        d.el,
      );
    }

    document.body.style.cursor = 'grabbing';
    d.rafId = requestAnimationFrame(() => this.tick());
  }

  private tick(): void {
    const d = this.drag;
    if (!d?.ghost) return;

    const vx = d.pointerX - d.lastX;
    d.lastX = d.pointerX;

    const still = this.reduced.reduced();
    const targetTilt = still ? 0 : Math.max(-9, Math.min(9, vx * 0.9));
    d.tilt += (targetTilt - d.tilt) * 0.18;
    d.lift += (1 - d.lift) * 0.22;

    const dx = d.pointerX - d.offsetX - d.rect.left;
    const dy = d.pointerY - d.offsetY - d.rect.top;
    d.ghost.style.transform = `translate(${dx}px, ${dy}px) rotate(${d.tilt.toFixed(2)}deg) scale(${(1 + 0.045 * d.lift).toFixed(3)})`;

    // Horizontal auto-scroll near board container edges
    const sc = d.scrollContainer;
    const scRect = sc.getBoundingClientRect();
    const edge = 48;
    if (d.pointerX < scRect.left + edge) {
      sc.scrollLeft -= 8;
    } else if (d.pointerX > scRect.right - edge) {
      sc.scrollLeft += 8;
    }

    d.rafId = requestAnimationFrame(() => this.tick());
  }

  private updateTarget(d: ActiveDrag): void {
    const ph = d.placeholder;
    if (!ph) return;

    const under = document
      .elementFromPoint(d.pointerX, d.pointerY)
      ?.closest<HTMLElement>('[data-col]');
    if (!under) return;

    if (under !== d.currentCol) {
      if (d.currentCol) this.highlightCol(d.currentCol, false);
      this.highlightCol(under, true);
      d.currentCol = under;
    }

    const others = Array.from(
      under.querySelectorAll<HTMLElement>(':scope > li[data-task-id]:not([hidden])'),
    );
    const ref = d.allowReorder
      ? (others.find((li) => {
          const r = li.getBoundingClientRect();
          return d.pointerY < r.top + r.height / 2;
        }) ?? null)
      : null;

    let next = ph.nextElementSibling;
    if (next === d.el) next = next.nextElementSibling;
    if (ph.parentElement === under && next === ref) return;

    const wait = d.settleUntil - performance.now();
    if (wait > 0) {
      if (d.retryTimer) clearTimeout(d.retryTimer);
      d.retryTimer = setTimeout(() => {
        if (this.drag?.started) this.updateTarget(this.drag);
      }, wait + 10);
      return;
    }

    d.settleUntil = performance.now() + 140;
    const oldCol = ph.parentElement;
    if (oldCol && oldCol !== under) {
      this.slideSiblings(oldCol, () => ph.remove());
    }
    this.slideSiblings(under, () => under.insertBefore(ph, ref));
  }

  private slideSiblings(colEl: HTMLElement, mutate: () => void): void {
    const items = Array.from(
      colEl.querySelectorAll<HTMLElement>(':scope > li[data-task-id]:not([hidden])'),
    );
    const before = new Map<HTMLElement, DOMRect>();
    for (const el of items) {
      before.set(el, el.getBoundingClientRect());
    }

    mutate();
    if (this.reduced.reduced()) return;

    for (const [el, prev] of before) {
      if (!el.isConnected) continue;
      const dy = prev.top - el.getBoundingClientRect().top;
      if (Math.abs(dy) > 1) {
        el.animate([{ transform: `translateY(${dy}px)` }, { transform: 'none' }], {
          duration: MOTION.SLIDE_SIBLINGS_MS,
          easing: MOTION.EASE_FLOW,
        });
      }
    }
  }

  private endDrag(cancel: boolean): void {
    const d = this.drag;
    this.drag = null;
    this.cleanupListeners();
    if (!d) return;

    this.lastDragEndAt = performance.now();
    cancelAnimationFrame(d.rafId);
    if (d.retryTimer) clearTimeout(d.retryTimer);
    document.body.style.cursor = '';

    if (d.currentCol) this.highlightCol(d.currentCol, false);

    const ph = d.placeholder;
    const col = ph?.parentElement;
    if (!ph || !col) {
      d.ghost?.remove();
      d.el.hidden = false;
      return;
    }

    const targetStatus = col.dataset['col'] as TaskStatus;
    const visibleItems = Array.from(col.children).filter(
      (child) =>
        child === ph ||
        ((child as HTMLElement).dataset['taskId'] && !(child as HTMLElement).hidden),
    );
    const slotIndex = visibleItems.indexOf(ph);
    const nextEl = visibleItems[slotIndex + 1] as HTMLElement | undefined;
    const beforeTaskId = nextEl?.dataset['taskId'] ?? null;

    const noChange =
      cancel ||
      (targetStatus === d.sourceStatus && (!d.allowReorder || slotIndex === d.origIndex));

    if (noChange && (col !== d.el.parentElement || ph.nextElementSibling !== d.el)) {
      ph.remove();
      d.el.before(ph);
    }

    const landRect = ph.getBoundingClientRect();
    const finish = () => {
      d.ghost?.remove();
      ph.remove();
      d.el.hidden = false;
      if (!noChange) {
        d.onDrop({
          taskId: d.taskId,
          targetStatus,
          beforeTaskId: d.allowReorder ? beforeTaskId : null,
        });
      }
    };

    if (this.reduced.reduced() || !d.ghost) {
      finish();
      return;
    }

    const curTransform = d.ghost.style.transform;
    const anim = d.ghost.animate(
      [
        { transform: curTransform },
        {
          transform: `translate(${landRect.left - d.rect.left}px, ${landRect.top - d.rect.top}px) rotate(0deg) scale(1)`,
        },
      ],
      { duration: MOTION.GLIDE_IN_MS, easing: MOTION.EASE_FLOW, fill: 'forwards' },
    );
    anim.onfinish = finish;
  }

  private highlightCol(col: HTMLElement, active: boolean): void {
    col.classList.toggle('bg-flow-soft', active);
    col.style.boxShadow = active ? 'inset 0 0 0 2px var(--color-flow)' : '';
  }

  private cleanupListeners(): void {
    window.removeEventListener('pointermove', this.boundMove);
    window.removeEventListener('pointerup', this.boundUp);
    window.removeEventListener('pointercancel', this.boundCancel);
    window.removeEventListener('keydown', this.boundKeydown);
  }
}
