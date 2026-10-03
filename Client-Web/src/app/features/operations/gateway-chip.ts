import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { GatewayStatus } from './mesh.models';

/** Coloured chip with the gateway connection state; the last error shows as tooltip text when offline. */
@Component({
  selector: 'app-gateway-chip',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'inline-flex' },
  template: `
    <span
      class="inline-flex items-center gap-2 rounded-full border px-3 py-1 font-mono text-xs"
      [class.border-green-500]="state() === 'Online'"
      [class.text-green-500]="state() === 'Online'"
      [class.border-amber-500]="state() === 'Connecting'"
      [class.text-amber-500]="state() === 'Connecting'"
      [class.border-red-500]="state() === 'Offline'"
      [class.text-red-500]="state() === 'Offline'"
      [attr.title]="tooltip()"
      role="status"
      [attr.aria-label]="'Gateway ' + label()"
    >
      <span
        class="size-2 rounded-full bg-current"
        [class.animate-pulse]="state() === 'Connecting'"
        aria-hidden="true"
      ></span>
      GATEWAY {{ label() }}
      @if (gateway()?.nodeId; as nodeId) {
        <span class="text-muted-color">{{ nodeId }}</span>
      }
      @if (gateway()?.mode === 'Simulated') {
        <span class="text-muted-color">SIM</span>
      }
    </span>
  `,
})
export class GatewayChip {
  readonly gateway = input<GatewayStatus | undefined>();

  protected readonly state = computed(() => this.gateway()?.status ?? 'Offline');
  protected readonly label = computed(() => this.state().toUpperCase());
  protected readonly tooltip = computed(() => {
    const gateway = this.gateway();
    if (!gateway) {
      return 'Gateway status unknown';
    }
    const firmware = gateway.firmwareVersion ? `Firmware ${gateway.firmwareVersion}. ` : '';
    return gateway.lastError ? `${firmware}${gateway.lastError}` : firmware || gateway.status;
  });
}
