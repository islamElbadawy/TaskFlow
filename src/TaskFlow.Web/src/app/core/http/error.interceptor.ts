import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { AuthSessionBridge } from '../auth/auth-session';
import { toApiError } from './problem-details';

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const session = inject(AuthSessionBridge);

  return next(req).pipe(
    catchError((err: unknown) => {
      const apiError = toApiError(err);
      const isLoginCall = req.url.endsWith('/api/auth/login');
      if (apiError.status === 401 && !isLoginCall) {
        session.notifyUnauthorized();
      }
      return throwError(() => apiError);
    }),
  );
};
