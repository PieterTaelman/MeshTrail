import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { ButtonModule } from '@openng/optimus-ui/button';
import { InputTextModule } from '@openng/optimus-ui/inputtext';
import { MessageModule } from '@openng/optimus-ui/message';
import { describeHttpError } from '../../core/api/problem-details';
import { AuthService } from '../../core/auth/auth.service';

/** Email + password. Not confirmed yet → offer to resend the link. Back to where the user came from afterwards. */
@Component({
  selector: 'app-sign-in-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, ButtonModule, InputTextModule, MessageModule],
  template: `
    <section
      class="mx-auto mt-8 flex w-full max-w-md flex-col gap-4 rounded-xl border border-content-border bg-content p-6"
    >
      <h1 class="text-xl font-semibold">Sign in</h1>
      <form class="flex flex-col gap-3" (submit)="$event.preventDefault(); submit()">
        <label class="flex flex-col gap-1 text-sm">
          Email
          <input
            pInputText
            type="email"
            autocomplete="email"
            required
            [value]="email()"
            (input)="email.set($any($event.target).value)"
          />
        </label>
        <label class="flex flex-col gap-1 text-sm">
          Password
          <input
            pInputText
            type="password"
            autocomplete="current-password"
            required
            [value]="password()"
            (input)="password.set($any($event.target).value)"
          />
        </label>
        @if (error(); as message) {
          <p-message severity="error" [text]="message" />
        }
        @if (notConfirmed()) {
          <p-button
            label="Send the confirmation link again"
            size="small"
            severity="secondary"
            [outlined]="true"
            (onClick)="resend()"
          />
        }
        <p-button type="submit" label="Sign in" [disabled]="!email() || !password() || busy()" />
      </form>
      <div class="flex justify-between text-sm">
        <a routerLink="/account/forgot-password" class="text-primary">Forgot password?</a>
        <a routerLink="/account/register" class="text-primary">Create an account</a>
      </div>
    </section>
  `,
})
export class SignInPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  /** Where to go after signing in (set by the guard). */
  readonly returnUrl = input<string>('');

  protected readonly email = signal('');
  protected readonly password = signal('');
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly notConfirmed = signal(false);

  protected submit(): void {
    this.busy.set(true);
    this.error.set(null);
    this.notConfirmed.set(false);
    this.auth.signIn(this.email().trim(), this.password()).subscribe({
      next: () => void this.router.navigateByUrl(this.safeReturnUrl()),
      error: (error: unknown) => {
        this.busy.set(false);
        this.error.set(describeHttpError(error));
        this.notConfirmed.set(error instanceof HttpErrorResponse && error.status === 403);
      },
    });
  }

  protected resend(): void {
    this.auth.resendConfirmation(this.email().trim()).subscribe(() => {
      this.notConfirmed.set(false);
      this.error.set('A new confirmation link is on its way.');
    });
  }

  /** Only paths inside this app (no "https://evil.example"). */
  private safeReturnUrl(): string {
    const url = this.returnUrl();
    return url.startsWith('/') && !url.startsWith('//') && !url.startsWith('/account')
      ? url
      : '/operations';
  }
}
