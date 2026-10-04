import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  model,
  output,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ButtonModule } from '@openng/optimus-ui/button';
import { DialogModule } from '@openng/optimus-ui/dialog';
import { InputTextModule } from '@openng/optimus-ui/inputtext';
import { MessageModule } from '@openng/optimus-ui/message';
import { Observable, filter } from 'rxjs';
import { describeHttpError } from '../../../core/api/problem-details';
import { RealtimeService } from '../../../core/realtime/realtime.service';
import { MeshApi } from '../mesh.api';
import { MESH_EVENTS, MeshMessage, MessageStatus, Registration } from '../mesh.models';

/** Contact links start like this (the text inside the Meshtastic app's "share contact" QR code). */
const CONTACT_PREFIX = 'https://meshtastic.org/v/#';

/**
 * Two steps: paste the node's contact link (we send it a 6-digit code by direct message), then type the code
 * shown on the node. QR scanning comes with the mobile app.
 */
@Component({
  selector: 'app-registration-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ButtonModule, DialogModule, InputTextModule, MessageModule],
  templateUrl: './registration-dialog.html',
})
export class RegistrationDialog {
  private readonly api = inject(MeshApi);

  readonly visible = model(false);
  /** Emits the registration once it is verified. */
  readonly registered = output<Registration>();

  protected readonly contactPrefix = CONTACT_PREFIX;
  protected readonly link = signal('');
  protected readonly code = signal('');
  protected readonly registration = signal<Registration | null>(null);
  protected readonly deliveryStatus = signal<MessageStatus | null>(null);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly step = computed(() => {
    const registration = this.registration();
    return !registration ? 'link' : registration.status === 'Verified' ? 'done' : 'code';
  });
  protected readonly linkLooksValid = computed(() => this.link().trim().startsWith(CONTACT_PREFIX));
  protected readonly codeLooksValid = computed(() => /^\d{6}$/.test(this.code().trim()));

  protected readonly deliveryText = computed(() => {
    switch (this.deliveryStatus()) {
      case 'Queued':
        return 'Waiting to send the code (the mesh allows one message every few seconds)…';
      case 'Sent':
        return 'Code sent. Waiting for the node to confirm…';
      case 'Acked':
        return 'The node received the code.';
      case 'Failed':
        return 'The code did not reach the node. Check that it is on and in range, then register again.';
      default:
        return '';
    }
  });

  constructor() {
    // The direct message with the code is tracked live, so the user knows when to look at the node.
    inject(RealtimeService)
      .on<MeshMessage>(MESH_EVENTS.messageStatusChanged)
      .pipe(
        filter((message) => message.id === this.registration()?.verificationMessageId),
        takeUntilDestroyed(),
      )
      .subscribe((message) => this.deliveryStatus.set(message.status));
  }

  protected submitLink(): void {
    this.run(this.api.registerFromContactUrl(this.link().trim()), (registration) => {
      this.registration.set(registration);
      this.deliveryStatus.set(registration.verificationMessageStatus);
    });
  }

  protected submitCode(): void {
    const registration = this.registration();
    if (!registration) {
      return;
    }
    this.run(this.api.verifyRegistration(registration.id, this.code().trim()), (verified) => {
      this.registration.set(verified);
      this.registered.emit(verified);
    });
  }

  protected startOver(): void {
    this.registration.set(null);
    this.deliveryStatus.set(null);
    this.code.set('');
    this.error.set(null);
  }

  /** Clears everything when the dialog closes, so the next registration starts fresh. */
  protected onHide(): void {
    this.startOver();
    this.link.set('');
  }

  private run<T>(request: Observable<T>, done: (result: T) => void): void {
    this.busy.set(true);
    this.error.set(null);
    request.subscribe({
      next: (result) => {
        this.busy.set(false);
        done(result);
      },
      error: (error: unknown) => {
        this.busy.set(false);
        this.error.set(describeHttpError(error));
      },
    });
  }
}
