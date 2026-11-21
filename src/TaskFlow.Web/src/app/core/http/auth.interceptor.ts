import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthSessionBridge } from '../auth/auth-session';

const PUBLIC_AUTH_PATHS = ['/api/auth/login', '/api/auth/register'];

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const isPublic = PUBLIC_AUTH_PATHS.some((path) => req.url.endsWith(path));
  if (isPublic) {
    return next(req);
  }

  const token = inject(AuthSessionBridge).token();
  if (!token) {
    return next(req);
  }

  return next(
    req.clone({
      setHeaders: { Authorization: `Bearer ${token}` },
    }),
  );
};
