import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ButtonModule } from '@openng/optimus-ui/button';
import { InputTextModule } from '@openng/optimus-ui/inputtext';
import { MessageModule } from '@openng/optimus-ui/message';
import { describeHttpError } from '../../core/api/problem-details';
import { PASSWORD_MIN_LENGTH } from '../../core/auth/account.models';
import { AuthService } from '../../core/auth/auth.service';

/** The page the reset mail links to (?user=…&token=…): choose a new password. */
@Component({
  selector: 'app-reset-password-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, ButtonModule, InputTextModule, MessageModule],
  template: `
    <section
      class="mx-auto mt-8 flex w-full max-w-md flex-col gap-4 rounded-xl border border-content-border bg-content p-6"
    >
      <h1 class="text-xl font-semibold">Choose a new password</h1>
      @if (done()) {
        <p-message severity="success" text="Your password is changed. You can sign in now." />
        <a routerLink="/account/sign-in" class="text-sm text-primary">Sign in</a>
      } @else {
        <form class="flex flex-col gap-3" (submit)="$event.preventDefault(); submit()">
          <label class="flex flex-col gap-1 text-sm">
            New password (at least {{ minLength }} characters)
            <input
              pInputText
              type="password"
              autocomplete="new-password"
              [value]="password()"
              (input)="password.set($any($event.target).value)"
            />
          </label>
          <label class="flex flex-col gap-1 text-sm">
            Repeat
            <input
              pInputText
              type="password"
              autocomplete="new-password"
              [value]="repeat()"
              (input)="repeat.set($any($event.target).value)"
            />
          </label>
          @if (error(); as message) {
            <p-message severity="error" [text]="message" />
          }
          <p-button type="submit" label="Save" [disabled]="!valid() || busy()" />
        </form>
      }
    </section>
  `,
})
export class ResetPasswordPage {
  private readonly auth = inject(AuthService);

  readonly user = input('');
  readonly token = input('');

  protected readonly minLength = PASSWORD_MIN_LENGTH;
  protected readonly password = signal('');
  protected readonly repeat = signal('');
  protected readonly busy = signal(false);
  protected readonly done = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly valid = computed(
    () => this.password().length >= PASSWORD_MIN_LENGTH && this.password() === this.repeat(),
  );

  protected submit(): void {
    this.busy.set(true);
    this.error.set(null);
    this.auth.resetPassword(this.user(), this.token(), this.password()).subscribe({
      next: () => this.done.set(true),
      error: (error: unknown) => {
        this.busy.set(false);
        this.error.set(describeHttpError(error));
      },
    });
  }
}
