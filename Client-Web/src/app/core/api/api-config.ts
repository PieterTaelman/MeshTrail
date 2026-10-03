import { InjectionToken } from '@angular/core';
import { environment } from '../../../environments/environment';

/** Base URL of the Meshtrail API. A token (not a constant) so tests can swap it. */
export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL', {
  providedIn: 'root',
  factory: () => environment.apiBaseUrl,
});

/** All API routes are versioned: api/v1/... */
export const API_V1 = '/api/v1';
