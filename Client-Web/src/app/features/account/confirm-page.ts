import { ChangeDetectionStrategy, Component, OnInit, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MessageModule } from '@openng/optimus-ui/message';
import { describeHttpError } from '../../core/api/problem-details';
import { AuthService } from '../../core/auth/auth.service';

/** The page the confirmation mail links to (?user=…&token=…): confirms the email address right away. */
@Component({
  selector: 'app-confirm-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, MessageModule],
  template: `
    <section
      class="mx-auto mt-8 flex w-full max-w-md flex-col gap-4 rounded-xl border border-content-border bg-content p-6"
    >
      <h1 class="text-xl font-semibold">Confirm your email</h1>
      @switch (state()) {
        @case ('busy') {
          <p class="text-sm text-muted-color">Confirming…</p>
        }
        @case ('done') {
          <p-message
            severity="success"
            text="Your email address is confirmed. You can sign in now."
          />
          <a routerLink="/account/sign-in" class="text-sm text-primary">Sign in</a>
        }
        @case ('failed') {
          <p-message severity="error" [text]="error() ?? 'This link does not work.'" />
          <a routerLink="/account/sign-in" class="text-sm text-primary"
            >Sign in or ask for a new link</a
          >
        }
      }
    </section>
  `,
})
export class ConfirmPage implements OnInit {
  private readonly auth = inject(AuthService);

  readonly user = input('');
  readonly token = input('');

  protected readonly state = signal<'busy' | 'done' | 'failed'>('busy');
  protected readonly error = signal<string | null>(null);

  ngOnInit(): void {
    this.auth.confirmEmail(this.user(), this.token()).subscribe({
      next: () => this.state.set('done'),
      error: (error: unknown) => {
        this.error.set(describeHttpError(error));
        this.state.set('failed');
      },
    });
  }
}
