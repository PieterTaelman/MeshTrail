import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

/** Pages that need an account: signed-out visitors go to sign-in and come back afterwards. */
export const authGuard: CanActivateFn = (_route, state) =>
  inject(AuthService).isSignedIn() ||
  inject(Router).createUrlTree(['/account/sign-in'], { queryParams: { returnUrl: state.url } });
