import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ButtonModule } from '@openng/optimus-ui/button';
import { InputTextModule } from '@openng/optimus-ui/inputtext';
import { AuthService } from '../../core/auth/auth.service';

/** Ask for a password-reset link. The answer is the same whether the address has an account or not. */
@Component({
  selector: 'app-forgot-password-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, ButtonModule, InputTextModule],
  template: `
    <section
      class="mx-auto mt-8 flex w-full max-w-md flex-col gap-4 rounded-xl border border-content-border bg-content p-6"
    >
      <h1 class="text-xl font-semibold">Forgot your password?</h1>
      @if (sent()) {
        <p class="text-sm">
          If <span class="font-mono">{{ email() }}</span> has an account, a link to choose a new
          password is on its way.
        </p>
      } @else {
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
          <p-button type="submit" label="Send me a link" [disabled]="!email() || busy()" />
        </form>
      }
      <a routerLink="/account/sign-in" class="text-sm text-primary">Back to sign in</a>
    </section>
  `,
})
export class ForgotPasswordPage {
  private readonly auth = inject(AuthService);

  protected readonly email = signal('');
  protected readonly busy = signal(false);
  protected readonly sent = signal(false);

  protected submit(): void {
    this.busy.set(true);
    this.auth.forgotPassword(this.email().trim()).subscribe({
      next: () => this.sent.set(true),
      error: () => this.busy.set(false),
    });
  }
}
