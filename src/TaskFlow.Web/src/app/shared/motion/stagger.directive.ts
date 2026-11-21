import { Directive, ElementRef, afterNextRender, effect, inject } from '@angular/core';
import { TranslationService } from '../../core/i18n/translation.service';
import { ReducedMotionService } from './reduced-motion';
import { MOTION } from './tokens';

@Directive({
  selector: '[appStagger]',
})
export class StaggerDirective {
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly reduced = inject(ReducedMotionService);
  private readonly i18n = inject(TranslationService);

  constructor() {
    afterNextRender(() => this.replay());

    // Replay entrance stagger when language/direction switches.
    effect(() => {
      this.i18n.lang();
      this.replay();
    });
  }

  replay(): void {
    const el = this.host.nativeElement;
    if (this.reduced.reduced()) {
      el.classList.remove('tf-stagger');
      return;
    }
    const children = Array.from(el.children) as HTMLElement[];
    children.forEach((child, index) => {
      child.style.setProperty('--i', String(Math.min(index, MOTION.STAGGER_CAP)));
    });
    el.classList.remove('tf-stagger');
    void el.offsetWidth;
    el.classList.add('tf-stagger');
  }
}
