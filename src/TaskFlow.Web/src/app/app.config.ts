import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import {
  ApplicationConfig,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import {
  TitleStrategy,
  provideRouter,
  withComponentInputBinding,
  withInMemoryScrolling,
  withViewTransitions,
} from '@angular/router';
import { forkJoin, from } from 'rxjs';
import { routes } from './app.routes';
import { AuthStore } from './core/auth/auth.store';
import { apiBaseInterceptor } from './core/http/api-base.interceptor';
import { authInterceptor } from './core/http/auth.interceptor';
import { envelopeInterceptor } from './core/http/envelope.interceptor';
import { errorInterceptor } from './core/http/error.interceptor';
import { TaskFlowTitleStrategy } from './core/i18n/title.strategy';
import { TranslationService } from './core/i18n/translation.service';
import { RealtimeClient } from './core/realtime/realtime-client';
import { SignalrRealtimeClient } from './core/realtime/signalr-realtime-client';
import { AppStorage, LocalAppStorage } from './core/storage/storage';
import { TaskHeroService } from './shared/motion/task-hero.service';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    { provide: AppStorage, useClass: LocalAppStorage },
    { provide: RealtimeClient, useClass: SignalrRealtimeClient },
    { provide: TitleStrategy, useClass: TaskFlowTitleStrategy },
    provideHttpClient(
      withFetch(),
      withInterceptors([apiBaseInterceptor, authInterceptor, envelopeInterceptor, errorInterceptor]),
    ),
    provideRouter(
      routes,
      withComponentInputBinding(),
      withInMemoryScrolling({ scrollPositionRestoration: 'top' }),
      withViewTransitions({
        skipInitialTransition: true,
        onViewTransitionCreated: ({ transition, from, to }) => {
          const hero = inject(TaskHeroService);
          const fromPath = from.firstChild?.routeConfig?.path ?? '';
          const toPath = to.firstChild?.routeConfig?.path ?? '';
          const isAuthRoute = (p: string) => p === 'login' || p === 'register';
          // Skip view transition when crossing between authenticated shell and auth pages
          // so the pinned topbar never overlaps the login/register layout.
          if (isAuthRoute(fromPath) !== isAuthRoute(toPath)) {
            transition.skipTransition();
            hero.clear();
            return;
          }
          void transition.finished.finally(() => hero.clear());
        },
      }),
    ),
    provideAppInitializer(() => {
      const i18n = inject(TranslationService);
      const auth = inject(AuthStore);
      return forkJoin([from(i18n.init()), auth.restoreSession()]);
    }),
  ],
};
