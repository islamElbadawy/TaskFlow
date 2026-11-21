import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { UserRole } from '../api/models';
import { AuthStore } from './auth.store';

export function roleGuard(...allowed: readonly UserRole[]): CanActivateFn {
  return () => {
    const user = inject(AuthStore).user();
    if (user && allowed.includes(user.role)) {
      return true;
    }
    return inject(Router).createUrlTree(['/dashboard']);
  };
}
