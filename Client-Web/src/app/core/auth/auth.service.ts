import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { API_BASE_URL, API_V1 } from '../api/api-config';
import { Profile, RegisterRequest, SignInResponse } from './account.models';

const STORAGE_KEY = 'meshtrail.session';

/** What we keep between page loads: the access token, when it expires and who it belongs to. */
interface Session {
  accessToken: string;
  expiresAt: string;
  profile: Profile;
}

/**
 * The signed-in user. The session (access token + profile) lives in localStorage until the token expires or the user
 * signs out. Account calls (register, confirm, reset) go through here too.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly url = `${inject(API_BASE_URL)}${API_V1}/account`;
  private readonly session = signal<Session | null>(loadSession());

  readonly profile = computed(() => this.session()?.profile ?? null);
  readonly isSignedIn = computed(() => this.session() !== null);

  /** The token for the Authorization header (null when signed out or expired). */
  accessToken(): string | null {
    const session = this.session();
    if (!session) {
      return null;
    }
    if (Date.parse(session.expiresAt) <= Date.now()) {
      this.signOut();
      return null;
    }
    return session.accessToken;
  }

  signIn(email: string, password: string): Observable<SignInResponse> {
    return this.http
      .post<SignInResponse>(`${this.url}/sign-in`, { email, password })
      .pipe(tap((response) => this.store(response)));
  }

  signOut(): void {
    this.session.set(null);
    try {
      localStorage.removeItem(STORAGE_KEY);
    } catch {
      // Storage blocked: nothing to remove.
    }
  }

  register(request: RegisterRequest): Observable<void> {
    return this.http.post<void>(`${this.url}/register`, request);
  }

  confirmEmail(userId: string, token: string): Observable<void> {
    return this.http.post<void>(`${this.url}/confirm-email`, { userId, token });
  }

  resendConfirmation(email: string): Observable<void> {
    return this.http.post<void>(`${this.url}/resend-confirmation`, { email });
  }

  forgotPassword(email: string): Observable<void> {
    return this.http.post<void>(`${this.url}/forgot-password`, { email });
  }

  resetPassword(userId: string, token: string, password: string): Observable<void> {
    return this.http.post<void>(`${this.url}/reset-password`, { userId, token, password });
  }

  updateProfile(firstName: string, lastName: string): Observable<Profile> {
    return this.http.put<Profile>(`${this.url}/me`, { firstName, lastName }).pipe(
      tap((profile) => {
        const session = this.session();
        if (session) {
          this.store({ ...session, profile });
        }
      }),
    );
  }

  private store(session: Session): void {
    this.session.set(session);
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(session));
    } catch {
      // Storage blocked (private window): the session lasts until the page reloads.
    }
  }
}

function loadSession(): Session | null {
  try {
    const stored = localStorage.getItem(STORAGE_KEY);
    const session = stored ? (JSON.parse(stored) as Session) : null;
    return session && Date.parse(session.expiresAt) > Date.now() ? session : null;
  } catch {
    return null;
  }
}
