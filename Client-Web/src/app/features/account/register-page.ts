import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { ButtonModule } from '@openng/optimus-ui/button';
import { InputTextModule } from '@openng/optimus-ui/inputtext';
import { MessageModule } from '@openng/optimus-ui/message';
import { describeHttpError } from '../../core/api/problem-details';
import { PASSWORD_MIN_LENGTH } from '../../core/auth/account.models';
import { AuthService } from '../../core/auth/auth.service';

/** Create an account: email, first and last name, password. A confirmation link is mailed afterwards. */
@Component({
  selector: 'app-register-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, ButtonModule, InputTextModule, MessageModule],
  template: `
    <section
      class="mx-auto mt-8 flex w-full max-w-md flex-col gap-4 rounded-xl border border-content-border bg-content p-6"
    >
      <h1 class="text-xl font-semibold">Create your Meshtrail account</h1>
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
        <div class="flex gap-3">
          <label class="flex flex-1 flex-col gap-1 text-sm">
            First name
            <input
              pInputText
              autocomplete="given-name"
              required
              maxlength="100"
              [value]="firstName()"
              (input)="firstName.set($any($event.target).value)"
            />
          </label>
          <label class="flex flex-1 flex-col gap-1 text-sm">
            Last name
            <input
              pInputText
              autocomplete="family-name"
              required
              maxlength="100"
              [value]="lastName()"
              (input)="lastName.set($any($event.target).value)"
            />
          </label>
        </div>
        <label class="flex flex-col gap-1 text-sm">
          Password (at least {{ minLength }} characters)
          <input
            pInputText
            type="password"
            autocomplete="new-password"
            required
            [value]="password()"
            (input)="password.set($any($event.target).value)"
          />
        </label>
        <label class="flex flex-col gap-1 text-sm">
          Repeat password
          <input
            pInputText
            type="password"
            autocomplete="new-password"
            required
            [value]="repeat()"
            (input)="repeat.set($any($event.target).value)"
          />
        </label>
        @if (repeat() && repeat() !== password()) {
          <p class="text-xs text-red-500">The passwords are not the same.</p>
        }
        @if (error(); as message) {
          <p-message severity="error" [text]="message" />
        }
        <p-button type="submit" label="Register" [disabled]="!valid() || busy()" />
      </form>
      <p class="text-sm text-muted-color">
        Already registered? <a routerLink="/account/sign-in" class="text-primary">Sign in</a>
      </p>
    </section>
  `,
})
export class RegisterPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly minLength = PASSWORD_MIN_LENGTH;
  protected readonly email = signal('');
  protected readonly firstName = signal('');
  protected readonly lastName = signal('');
  protected readonly password = signal('');
  protected readonly repeat = signal('');
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly valid = computed(
    () =>
      this.email().includes('@') &&
      this.firstName().trim() !== '' &&
      this.lastName().trim() !== '' &&
      this.password().length >= PASSWORD_MIN_LENGTH &&
      this.password() === this.repeat(),
  );

  protected submit(): void {
    if (!this.valid() || this.busy()) {
      return;
    }
    this.busy.set(true);
    this.error.set(null);
    const email = this.email().trim();
    this.auth
      .register({
        email,
        firstName: this.firstName().trim(),
        lastName: this.lastName().trim(),
        password: this.password(),
      })
      .subscribe({
        next: () => void this.router.navigate(['/account/check-mail'], { queryParams: { email } }),
        error: (error: unknown) => {
          this.busy.set(false);
          this.error.set(describeHttpError(error));
        },
      });
  }
}
