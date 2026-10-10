import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, output, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { ButtonModule } from '@openng/optimus-ui/button';
import { MessageModule } from '@openng/optimus-ui/message';
import { describeHttpError } from '../../../core/api/problem-details';
import { MeshApi } from '../mesh.api';
import { Registration } from '../mesh.models';
import { RegistrationDialog } from '../registration-dialog/registration-dialog';

/**
 * "My nodes": the nodes registered to you (and claims still waiting for their code), register another one, or remove
 * one (two-step). A node can be registered once: remove it first to register it again.
 */
@Component({
  selector: 'app-my-nodes-panel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, ButtonModule, MessageModule, RegistrationDialog],
  template: `
    <section class="flex flex-col gap-3" aria-label="My nodes">
      <div class="flex items-center justify-between gap-2">
        <p class="text-xs text-muted-color">
          Nodes you proved are yours: with the code we sent to the node, or because they became your
          gateway.
        </p>
        <p-button label="Register a node" size="small" (onClick)="dialogOpen.set(true)" />
      </div>
      @if (error(); as message) {
        <p-message severity="error" [text]="message" />
      }
      <ul class="flex flex-col gap-2">
        @for (registration of registrations.value() ?? []; track registration.id) {
          <li class="flex items-center gap-3 rounded-lg border border-content-border p-2 text-sm">
            <span class="flex min-w-0 flex-1 flex-col">
              <span class="truncate">{{ registration.longName }}</span>
              <span class="font-mono text-xs text-muted-color">
                {{ registration.nodeId }} ·
                @if (registration.status === 'Verified') {
                  registered {{ registration.verifiedAt | date: 'd MMM yyyy' }}
                } @else {
                  waiting for the code ({{ registration.attemptsLeft }} attempts left)
                }
              </span>
            </span>
            <span
              class="font-mono text-xs"
              [class.text-primary]="registration.status === 'Verified'"
              [class.text-amber-500]="registration.status !== 'Verified'"
              >{{ registration.status.toUpperCase() }}</span
            >
            <p-button
              [label]="confirmingId() === registration.id ? 'Really remove?' : 'Remove'"
              size="small"
              [text]="true"
              [severity]="confirmingId() === registration.id ? 'danger' : 'secondary'"
              (onClick)="remove(registration)"
            />
          </li>
        } @empty {
          <li class="text-sm text-muted-color">
            {{ registrations.isLoading() ? 'Loading…' : 'No nodes registered to you yet.' }}
          </li>
        }
      </ul>
    </section>
    <app-registration-dialog [(visible)]="dialogOpen" (registered)="registrations.reload()" />
  `,
})
export class MyNodesPanel {
  private readonly api = inject(MeshApi);

  /** Something changed (e.g. a node removed): the parent may refresh what depends on it. */
  readonly changed = output<void>();

  protected readonly registrations = rxResource({ stream: () => this.api.getMyRegistrations() });
  protected readonly dialogOpen = signal(false);
  protected readonly confirmingId = signal<string | null>(null);
  protected readonly error = signal<string | null>(null);

  protected remove(registration: Registration): void {
    if (this.confirmingId() !== registration.id) {
      this.confirmingId.set(registration.id);
      return;
    }
    this.confirmingId.set(null);
    this.error.set(null);
    this.api.revokeRegistration(registration.id).subscribe({
      next: () => {
        this.registrations.reload();
        this.changed.emit();
      },
      error: (error: unknown) => this.error.set(describeHttpError(error)),
    });
  }
}
