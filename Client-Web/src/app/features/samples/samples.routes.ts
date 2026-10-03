import { Routes } from '@angular/router';

// Lazy-loaded so the samples code is only downloaded when the user opens the page.
export const SAMPLES_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./sample-list/sample-list').then((m) => m.SampleList),
    title: 'Samples',
  },
  {
    path: 'new',
    loadComponent: () => import('./sample-detail/sample-detail').then((m) => m.SampleDetail),
    title: 'New sample',
  },
  {
    path: ':id',
    loadComponent: () => import('./sample-detail/sample-detail').then((m) => m.SampleDetail),
    title: 'Sample',
  },
];
