import { Routes } from '@angular/router';

/** Public pages around the account: register, confirm the email address, sign in, reset the password. */
export const ACCOUNT_ROUTES: Routes = [
  {
    path: 'register',
    loadComponent: () => import('./register-page').then((m) => m.RegisterPage),
    title: 'Register',
  },
  {
    path: 'check-mail',
    loadComponent: () => import('./check-mail-page').then((m) => m.CheckMailPage),
    title: 'Check your mail',
  },
  {
    path: 'confirm',
    loadComponent: () => import('./confirm-page').then((m) => m.ConfirmPage),
    title: 'Confirm your email',
  },
  {
    path: 'sign-in',
    loadComponent: () => import('./sign-in-page').then((m) => m.SignInPage),
    title: 'Sign in',
  },
  {
    path: 'forgot-password',
    loadComponent: () => import('./forgot-password-page').then((m) => m.ForgotPasswordPage),
    title: 'Forgot password',
  },
  {
    path: 'reset',
    loadComponent: () => import('./reset-password-page').then((m) => m.ResetPasswordPage),
    title: 'New password',
  },
];
