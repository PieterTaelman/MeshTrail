import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'operations' },
  {
    path: 'operations',
    loadChildren: () =>
      import('./features/operations/operations.routes').then((m) => m.OPERATIONS_ROUTES),
  },
  {
    path: 'account',
    loadChildren: () => import('./features/account/account.routes').then((m) => m.ACCOUNT_ROUTES),
  },
  {
    path: 'profile',
    canActivate: [authGuard],
    loadComponent: () => import('./features/profile/profile-page').then((m) => m.ProfilePage),
    title: 'Profile',
  },
  {
    path: 'samples',
    loadChildren: () => import('./features/samples/samples.routes').then((m) => m.SAMPLES_ROUTES),
  },
  { path: '**', redirectTo: 'operations' },
];
