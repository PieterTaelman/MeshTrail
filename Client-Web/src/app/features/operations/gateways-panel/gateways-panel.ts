import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, input, output, signal } from '@angular/core';
import { rxResource, takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ButtonModule } from '@openng/optimus-ui/button';
import { TimesIcon } from '@openng/optimus-ui/icons/times';
import { MessageModule } from '@openng/optimus-ui/message';
import { debounceTime } from 'rxjs';
import { describeHttpError } from '../../../core/api/problem-details';
import { RealtimeService } from '../../../core/realtime/realtime.service';
import { MeshApi } from '../mesh.api';
import { formatAge, gatewayColor } from '../mesh-format';
import { Gateway, GatewayCredentials, MESH_EVENTS } from '../mesh.models';

/**
 * "My gateways": the user's gateways with their status, a button to add one (shows the MQTT settings for the node
 * once, with the password) and a two-step remove. Updates live when a gateway comes online or goes offline.
 */
@Component({
  selector: 'app-gateways-panel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, ButtonModule, MessageModule, TimesIcon],
  templateUrl: './gateways-panel.html',
  host: { class: 'block' },
})
export class GatewaysPanel {
  private readonly api = inject(MeshApi);

  /** Current time in ms, ticking in the parent, so ages stay fresh. */
  readonly now = input.required<number>();
  readonly closed = output<void>();
  /** The user clicked a gateway: show its node. */
  readonly showNode = output<number>();

  protected readonly gateways = rxResource({ stream: () => this.api.getGateways(true) });
  protected readonly credentials = signal<GatewayCredentials | null>(null);
  protected readonly confirmingId = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly copied = signal<string | null>(null);

  /** What the node can use when the server has no public address configured (fine for a test on the LAN). */
  protected readonly fallbackAddress = globalThis.location?.hostname ?? 'this server';

  protected readonly formatAge = formatAge;
  protected readonly gatewayColor = gatewayColor;

  constructor() {
    inject(RealtimeService)
      .on<Gateway>(MESH_EVENTS.gatewayStatusChanged)
      .pipe(debounceTime(500), takeUntilDestroyed())
      .subscribe(() => this.gateways.reload());
  }

  protected add(): void {
    this.busy.set(true);
    this.error.set(null);
    this.api.addGateway().subscribe({
      next: (credentials) => {
        this.busy.set(false);
        this.credentials.set(credentials);
        this.gateways.reload();
      },
      error: (error: unknown) => {
        this.busy.set(false);
        this.error.set(describeHttpError(error));
      },
    });
  }

  protected remove(gateway: Gateway): void {
    if (this.confirmingId() !== gateway.id) {
      this.confirmingId.set(gateway.id);
      return;
    }
    this.confirmingId.set(null);
    this.error.set(null);
    this.api.revokeGateway(gateway.id).subscribe({
      next: () => {
        if (this.credentials()?.gateway.id === gateway.id) {
          this.credentials.set(null);
        }
        this.gateways.reload();
      },
      error: (error: unknown) => this.error.set(describeHttpError(error)),
    });
  }

  protected copy(label: string, text: string): void {
    void navigator.clipboard?.writeText(text).then(() => this.copied.set(label));
  }
}
