import { DOCUMENT } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { AuthStore } from './core/auth/auth.store';
import { ThemeStore } from './core/theme/theme.store';
import { BrandMark } from './shared/ui/brand-mark';
import { AppToastHost } from './shared/ui/toast-host';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, AppToastHost, BrandMark],
  templateUrl: './app.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class App {
  protected readonly auth = inject(AuthStore);
  private readonly router = inject(Router);
  private readonly doc = inject(DOCUMENT);

  // Ensure ThemeStore effect initializes on boot.
  protected readonly theme = inject(ThemeStore);

  constructor() {
    this.router.events
      .pipe(
        filter((e) => e instanceof NavigationEnd),
        takeUntilDestroyed(),
      )
      .subscribe(() => {
        requestAnimationFrame(() => {
          const h1 = this.doc.querySelector<HTMLElement>('h1');
          h1?.focus({ preventScroll: true });
        });
      });
  }
}
