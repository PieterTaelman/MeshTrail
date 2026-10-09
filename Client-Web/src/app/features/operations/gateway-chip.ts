import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { gatewaySummaryState } from './mesh-format';
import { GatewaySummary } from './mesh.models';

/**
 * Top-bar chip "GATEWAYS online/total": green when all are online, amber when some are offline, red when none is.
 * Clicking it opens "My gateways".
 */
@Component({
  selector: 'app-gateway-chip',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'inline-flex' },
  template: `
    <button
      type="button"
      class="inline-flex items-center gap-2 rounded-full border px-3 py-1 font-mono text-xs hover:bg-content-hover"
      [class.border-green-500]="state() === 'ok'"
      [class.text-green-500]="state() === 'ok'"
      [class.border-amber-500]="state() === 'partial'"
      [class.text-amber-500]="state() === 'partial'"
      [class.border-red-500]="state() === 'down'"
      [class.text-red-500]="state() === 'down'"
      [class.border-content-border]="state() === 'none'"
      [class.text-muted-color]="state() === 'none'"
      [attr.title]="tooltip()"
      [attr.aria-label]="tooltip() + '. Open my gateways'"
      (click)="opened.emit()"
    >
      <span class="size-2 rounded-full bg-current" aria-hidden="true"></span>
      GATEWAYS {{ summary()?.online ?? 0 }}/{{ summary()?.total ?? 0 }}
    </button>
  `,
})
export class GatewayChip {
  readonly summary = input<GatewaySummary | undefined>();
  readonly opened = output<void>();

  protected readonly state = computed(() => gatewaySummaryState(this.summary()));
  protected readonly tooltip = computed(() => {
    const summary = this.summary();
    return !summary || summary.total === 0
      ? 'No gateways yet'
      : `${summary.online} of ${summary.total} gateways online`;
  });
}
