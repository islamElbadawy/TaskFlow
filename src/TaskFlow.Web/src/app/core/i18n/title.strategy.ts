import { Injectable, effect, inject } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { RouterStateSnapshot, TitleStrategy } from '@angular/router';
import { Dictionary, TranslationKey } from '../../../i18n/en';
import { TranslationService } from './translation.service';

type StringKey = {
  [K in TranslationKey]: Dictionary[K] extends string ? K : never;
}[TranslationKey];

@Injectable({ providedIn: 'root' })
export class TaskFlowTitleStrategy extends TitleStrategy {
  private readonly title = inject(Title);
  private readonly i18n = inject(TranslationService);
  private lastKey: StringKey | null = null;
  private lastRaw: string | null = null;

  constructor() {
    super();
    effect(() => {
      const dict = this.i18n.dict();
      if (this.lastKey) {
        this.title.setTitle(`${dict[this.lastKey]} · Taskaty`);
      } else if (this.lastRaw) {
        this.title.setTitle(`${this.lastRaw} · Taskaty`);
      }
    });
  }

  override updateTitle(snapshot: RouterStateSnapshot): void {
    const routeTitle = this.buildTitle(snapshot);
    if (!routeTitle) {
      this.lastKey = null;
      this.lastRaw = null;
      this.title.setTitle('Taskaty');
      return;
    }

    const dict = this.i18n.dict();
    if (routeTitle in dict && typeof dict[routeTitle as TranslationKey] === 'string') {
      this.lastKey = routeTitle as StringKey;
      this.lastRaw = null;
      this.title.setTitle(`${dict[this.lastKey]} · Taskaty`);
    } else {
      this.lastKey = null;
      this.lastRaw = routeTitle;
      this.title.setTitle(`${routeTitle} · Taskaty`);
    }
  }

  setDynamicTitle(raw: string): void {
    this.lastKey = null;
    this.lastRaw = raw;
    this.title.setTitle(`${raw} · Taskaty`);
  }
}
