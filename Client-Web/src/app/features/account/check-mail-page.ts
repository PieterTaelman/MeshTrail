import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ButtonModule } from '@openng/optimus-ui/button';
import { AuthService } from '../../core/auth/auth.service';

/** After registering: tell the user to click the link in the mail, and offer to send it again. */
@Component({
  selector: 'app-check-mail-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, ButtonModule],
  template: `
    <section
      class="mx-auto mt-8 flex w-full max-w-md flex-col gap-4 rounded-xl border border-content-border bg-content p-6"
    >
      <h1 class="text-xl font-semibold">Check your mail</h1>
      <p class="text-sm">
        We sent a confirmation link to <span class="font-mono">{{ email() }}</span
        >. Click it to finish your registration, then sign in.
      </p>
      <p class="text-xs text-muted-color">
        No mail after a few minutes? Look in your spam folder, or send it again.
      </p>
      <p-button
        label="Send the link again"
        severity="secondary"
        [outlined]="true"
        [disabled]="busy() || sent()"
        (onClick)="resend()"
      />
      @if (sent()) {
        <p class="text-xs text-green-500">Sent again.</p>
      }
      <a routerLink="/account/sign-in" class="text-sm text-primary">Go to sign in</a>
    </section>
  `,
})
export class CheckMailPage {
  private readonly auth = inject(AuthService);

  /** From the query string (?email=…). */
  readonly email = input('');

  protected readonly busy = signal(false);
  protected readonly sent = signal(false);

  protected resend(): void {
    this.busy.set(true);
    this.auth.resendConfirmation(this.email()).subscribe({
      next: () => {
        this.busy.set(false);
        this.sent.set(true);
      },
      error: () => this.busy.set(false),
    });
  }
}
