import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { API_BASE_URL } from '../api/api-config';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';

@Component({ template: '' })
class Blank {}

describe('AuthService and authInterceptor', () => {
  let auth: AuthService;
  let http: HttpTestingController;
  let client: HttpClient;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: 'account/sign-in', component: Blank }]),
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: 'https://api.test' },
      ],
    });
    auth = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
    client = TestBed.inject(HttpClient);
  });

  afterEach(() => http.verify());

  function signIn(expiresInMs = 3_600_000): void {
    auth.signIn('ann@example.org', 'secret password').subscribe();
    http.expectOne('https://api.test/api/v1/account/sign-in').flush({
      accessToken: 'token-1',
      expiresAt: new Date(Date.now() + expiresInMs).toISOString(),
      profile: {
        id: 'u1',
        email: 'ann@example.org',
        firstName: 'Ann',
        lastName: 'Peak',
        displayName: 'Ann Peak',
        createdAt: '',
      },
    });
  }

  it('stores the session and adds the token to API calls', () => {
    signIn();
    client.get('https://api.test/api/v1/teams').subscribe();

    const request = http.expectOne('https://api.test/api/v1/teams');
    expect(request.request.headers.get('Authorization')).toBe('Bearer token-1');
    request.flush([]);
    expect(auth.profile()?.displayName).toBe('Ann Peak');
    expect(localStorage.getItem('meshtrail.session')).toContain('token-1');
  });

  it('does not send the token to other hosts', () => {
    signIn();
    client.get('https://tiles.example/1/2/3.png').subscribe();

    const request = http.expectOne('https://tiles.example/1/2/3.png');
    expect(request.request.headers.has('Authorization')).toBe(false);
    request.flush('');
  });

  it('forgets an expired session', () => {
    signIn(-1000);

    expect(auth.accessToken()).toBeNull();
    expect(auth.isSignedIn()).toBe(false);
  });

  it('signs out on a 401', () => {
    signIn();
    client.get('https://api.test/api/v1/account/me').subscribe({ error: () => undefined });

    http
      .expectOne('https://api.test/api/v1/account/me')
      .flush(null, { status: 401, statusText: 'Unauthorized' });
    expect(auth.isSignedIn()).toBe(false);
  });
});
