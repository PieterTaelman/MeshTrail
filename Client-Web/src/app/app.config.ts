import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { provideOptimus } from '@openng/optimus-ui/config';
import { routes } from './app.routes';
import { authInterceptor } from './core/auth/auth.interceptor';
import { MeshtrailPreset } from './core/theme/meshtrail-preset';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    // Route params arrive as component inputs (e.g. id = input<string>()).
    provideRouter(routes, withComponentInputBinding()),
    // Adds the access token to API calls and handles an expired session.
    provideHttpClient(withFetch(), withInterceptors([authInterceptor])),
    // Optimus UI is the only component library. The CSS layer keeps Tailwind utilities able to override it.
    provideOptimus({
      theme: {
        preset: MeshtrailPreset,
        options: {
          // ThemeService puts this class on <html>; dark is the default look.
          darkModeSelector: '.app-dark',
          cssLayer: { name: 'optimus', order: 'theme, base, optimus, components, utilities' },
        },
      },
    }),
  ],
};
