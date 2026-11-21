import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { APP_CONFIG } from '../config/app-config.token';

export const apiBaseInterceptor: HttpInterceptorFn = (req, next) => {
  const { apiUrl } = inject(APP_CONFIG);
  if (!apiUrl || !req.url.startsWith('/api')) {
    return next(req);
  }
  const base = apiUrl.endsWith('/') ? apiUrl.slice(0, -1) : apiUrl;
  return next(req.clone({ url: `${base}${req.url}` }));
};
