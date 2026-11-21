import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthStore } from './auth.store';

export const guestGuard: CanActivateFn = () => {
  const auth = inject(AuthStore);
  if (!auth.isAuthenticated()) {
    return true;
  }
  return inject(Router).createUrlTree(['/dashboard']);
};
