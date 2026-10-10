import { DecimalPipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { rxResource, takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { ButtonModule } from '@openng/optimus-ui/button';
import { TimesIcon } from '@openng/optimus-ui/icons/times';
import { MessageModule } from '@openng/optimus-ui/message';
import { Observable, filter } from 'rxjs';
import { describeHttpError } from '../../../core/api/problem-details';
import { RealtimeService } from '../../../core/realtime/realtime.service';
import { MeshApi } from '../mesh.api';
import { formatAge, formatBattery, formatNumber, isOnline } from '../mesh-format';
import { MESH_EVENTS, MeshNode, NodeTraceroute } from '../mesh.models';

/**
 * Right-hand panel: everything we know about one node, the gateways that heard it, and actions that send something
 * to it (through the best of those gateways).
 */
@Component({
  selector: 'app-node-detail',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, RouterLink, ButtonModule, MessageModule, TimesIcon],
  templateUrl: './node-detail.html',
  host: { class: 'block' },
})
export class NodeDetail {
  private readonly api = inject(MeshApi);

  readonly nodeNum = input.required<number>();
  /** Current time in ms, ticking in the parent, so ages stay fresh. */
  readonly now = input.required<number>();
  readonly closed = output<void>();
  /** The user wants to chat with this node (opens a direct-message tab). */
  readonly messageNode = output<number>();
  /** The user wants to register this node to themselves. */
  readonly registerNode = output<void>();
  /** The user clicked another node (a gateway in "Heard by"). */
  readonly showNode = output<number>();
  /** Actions (message, position, traceroute, register) need an account. */
  readonly signedIn = input(true);

  protected readonly detail = rxResource({
    params: () => this.nodeNum(),
    stream: ({ params }) => this.api.getNode(params),
  });

  protected readonly node = computed(() => this.detail.value()?.node);
  protected readonly traceroute = computed(() => this.detail.value()?.lastTraceroute ?? null);
  /** Gateways that heard the node; the first one is the gateway messages go out through. */
  protected readonly heardBy = computed(() => this.detail.value()?.heardBy ?? []);
  protected readonly online = computed(() =>
    isOnline(this.node()?.lastHeardAt ?? null, this.now()),
  );

  protected readonly busy = signal(false);
  protected readonly notice = signal<string | null>(null);
  protected readonly error = signal<string | null>(null);

  protected readonly formatAge = formatAge;
  protected readonly formatBattery = formatBattery;
  protected readonly formatNumber = formatNumber;

  constructor() {
    // Reload when this node changes or its traceroute answer arrives (no polling).
    const realtime = inject(RealtimeService);
    realtime
      .on<MeshNode>(MESH_EVENTS.nodeUpdated)
      .pipe(
        filter((node) => node.nodeNum === this.nodeNum()),
        takeUntilDestroyed(),
      )
      .subscribe((node) => this.detail.update((detail) => (detail ? { ...detail, node } : detail)));
    realtime
      .on<NodeTraceroute>(MESH_EVENTS.tracerouteCompleted)
      .pipe(
        filter((route) => route.nodeNum === this.nodeNum()),
        takeUntilDestroyed(),
      )
      .subscribe((lastTraceroute) =>
        this.detail.update((detail) => (detail ? { ...detail, lastTraceroute } : detail)),
      );
  }

  protected requestPosition(): void {
    this.run(this.api.requestPosition(this.nodeNum()), () =>
      this.notice.set('Position requested. The map updates when the node answers.'),
    );
  }

  protected requestTraceroute(): void {
    this.run(this.api.requestTraceroute(this.nodeNum()), (pending) => {
      this.notice.set('Traceroute sent. The route appears here when the answer arrives.');
      this.detail.update((detail) => (detail ? { ...detail, lastTraceroute: pending } : detail));
    });
  }

  protected hopLabel(nodeId: string, snr: number | null): string {
    return snr === null ? nodeId : `${nodeId} (${snr.toFixed(1)} dB)`;
  }

  private run<T>(request: Observable<T>, done: (result: T) => void): void {
    this.busy.set(true);
    this.notice.set(null);
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
