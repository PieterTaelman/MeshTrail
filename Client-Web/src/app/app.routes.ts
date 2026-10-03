import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'samples' },
  {
    path: 'samples',
    loadChildren: () => import('./features/samples/samples.routes').then((m) => m.SAMPLES_ROUTES),
  },
  { path: '**', redirectTo: 'samples' },
];
