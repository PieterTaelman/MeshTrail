import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { API_BASE_URL } from '../api/api-config';
import { AuthService } from './auth.service';

/**
 * Adds the access token to calls to our API. A 401 means the session is gone (expired or revoked): sign out and go
 * to the sign-in page, then come back.
 */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const apiBase = inject(API_BASE_URL);
  const token = request.url.startsWith(apiBase) ? auth.accessToken() : null;
  const outgoing = token
    ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
    : request;

  return next(outgoing).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && error.status === 401 && token) {
        auth.signOut();
        void router.navigate(['/account/sign-in'], { queryParams: { returnUrl: router.url } });
      }
      return throwError(() => error);
    }),
  );
};
