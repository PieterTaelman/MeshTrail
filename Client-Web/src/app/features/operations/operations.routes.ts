import { Routes } from '@angular/router';

// Lazy-loaded: MapLibre is large, so it is only downloaded when the operations map opens.
export const OPERATIONS_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./operations-page/operations-page').then((m) => m.OperationsPage),
    title: 'Operations',
  },
];
