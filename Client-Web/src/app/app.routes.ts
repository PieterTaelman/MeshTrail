import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'operations' },
  {
    path: 'operations',
    loadChildren: () =>
      import('./features/operations/operations.routes').then((m) => m.OPERATIONS_ROUTES),
  },
  {
    path: 'samples',
    loadChildren: () => import('./features/samples/samples.routes').then((m) => m.SAMPLES_ROUTES),
  },
  { path: '**', redirectTo: 'operations' },
];
