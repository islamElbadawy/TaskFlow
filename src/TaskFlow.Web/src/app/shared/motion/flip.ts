import { MOTION } from './tokens';

/**
 * FLIP animation helper: captures bounding rects of matching elements before a DOM change,
 * runs `mutate`, then animates each element from its previous position to its new one.
 */
export function flipElements(
  elements: Iterable<HTMLElement>,
  mutate: () => void,
  options?: {
    durationMs?: number;
    reducedMotion?: boolean;
    overrides?: ReadonlyMap<HTMLElement, DOMRect>;
  },
): void {
  const list = Array.from(elements);
  const first = new Map<HTMLElement, DOMRect>();
  for (const el of list) {
    first.set(el, options?.overrides?.get(el) ?? el.getBoundingClientRect());
  }

  mutate();

  if (options?.reducedMotion) return;

  const duration = options?.durationMs ?? MOTION.FLIP_MS;
  for (const el of list) {
    if (!el.isConnected) continue;
    const prev = first.get(el);
    if (!prev) continue;
    const next = el.getBoundingClientRect();
    const dx = prev.left - next.left;
    const dy = prev.top - next.top;
    if (Math.abs(dx) < 1 && Math.abs(dy) < 1) continue;
    el.animate([{ transform: `translate(${dx}px, ${dy}px)` }, { transform: 'none' }], {
      duration,
      easing: MOTION.EASE_FLOW,
    });
  }
}
