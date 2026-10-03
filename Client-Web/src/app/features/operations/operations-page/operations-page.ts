import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { rxResource, takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ButtonModule } from '@openng/optimus-ui/button';
import { IconFieldModule } from '@openng/optimus-ui/iconfield';
import { RefreshIcon } from '@openng/optimus-ui/icons/refresh';
import { SearchIcon } from '@openng/optimus-ui/icons/search';
import { InputIconModule } from '@openng/optimus-ui/inputicon';
import { InputTextModule } from '@openng/optimus-ui/inputtext';
import { MessageModule } from '@openng/optimus-ui/message';
import { debounceTime } from 'rxjs';
import { describeHttpError } from '../../../core/api/problem-details';
import { MapFeatures, MapView } from '../../../core/map/map-view';
import { RealtimeService } from '../../../core/realtime/realtime.service';
import { GatewayChip } from '../gateway-chip';
import { MeshApi } from '../mesh.api';
import { formatAge, isOnline, lastHeardColor, upsertNode } from '../mesh-format';
import {
  GatewayStatus,
  MAP_LAYERS,
  MESH_EVENTS,
  MeshNode,
  NodeFeatureProperties,
} from '../mesh.models';
import { NodeDetail } from '../node-detail/node-detail';

/** Largest node list we load in one go (the API allows up to 500). */
const NODE_PAGE_SIZE = 500;

/**
 * The operations map: gateway status, node list, topo map and node detail. Everything updates live via SignalR;
 * nothing polls.
 */
@Component({
  selector: 'app-operations-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ButtonModule,
    IconFieldModule,
    InputIconModule,
    InputTextModule,
    MessageModule,
    RefreshIcon,
    SearchIcon,
    GatewayChip,
    MapView,
    NodeDetail,
  ],
  templateUrl: './operations-page.html',
  host: { class: 'block h-full' },
})
export class OperationsPage {
  private readonly api = inject(MeshApi);
  private readonly map = viewChild(MapView);

  /** Ticks every 30 s so "last heard" texts and marker colours age without reloading. */
  protected readonly now = signal(Date.now());

  protected readonly gateway = rxResource({ stream: () => this.api.getGateway() });
  protected readonly nodes = rxResource({
    stream: () => this.api.getNodes({ page: 1, pageSize: NODE_PAGE_SIZE }),
  });
  private readonly nodeFeatures = rxResource({
    stream: () => this.api.getMapFeatures([MAP_LAYERS.nodes]),
  });

  protected readonly showNodes = signal(true);
  protected readonly searchText = signal('');
  protected readonly selectedNodeNum = signal<number | null>(null);
  protected readonly actionError = signal<string | null>(null);

  protected readonly allNodes = computed(() => this.nodes.value()?.items ?? []);
  protected readonly onlineCount = computed(
    () => this.allNodes().filter((node) => isOnline(node.lastHeardAt, this.now())).length,
  );
  protected readonly visibleNodes = computed(() => {
    const search = this.searchText().trim().toLowerCase();
    return search
      ? this.allNodes().filter((node) =>
          [node.longName, node.shortName, node.nodeId].some((text) =>
            text.toLowerCase().includes(search),
          ),
        )
      : this.allNodes();
  });

  /** Node features from the API, coloured by last heard (recomputed as time passes). */
  protected readonly mapFeatures = computed<MapFeatures>(() => {
    const now = this.now();
    const features = this.showNodes() ? (this.nodeFeatures.value()?.features ?? []) : [];
    return {
      type: 'FeatureCollection',
      features: features.map((feature) => {
        const properties = feature.properties as unknown as NodeFeatureProperties;
        return {
          ...feature,
          properties: {
            ...properties,
            color: lastHeardColor(properties.lastHeardAt, now),
            radius: properties.isGateway ? 9 : 7,
          },
        };
      }),
    };
  });

  protected readonly selectedFeatureId = computed(() => {
    const nodeNum = this.selectedNodeNum();
    return nodeNum === null ? null : `${MAP_LAYERS.nodes}:${nodeNum}`;
  });

  protected readonly formatAge = formatAge;
  protected readonly isOnline = isOnline;
  protected readonly lastHeardColor = lastHeardColor;

  constructor() {
    const timer = setInterval(() => this.now.set(Date.now()), 30_000);
    inject(DestroyRef).onDestroy(() => clearInterval(timer));

    const realtime = inject(RealtimeService);
    realtime
      .on<GatewayStatus>(MESH_EVENTS.gatewayStatusChanged)
      .pipe(takeUntilDestroyed())
      .subscribe((status) => this.gateway.set(status));

    realtime
      .on<MeshNode>(MESH_EVENTS.nodeUpdated)
      .pipe(takeUntilDestroyed())
      .subscribe((node) => this.nodes.update((page) => upsertNode(page, node)));

    // Positions change in bursts (a config dump sends every node); refresh the map layer once things settle.
    realtime
      .on<MeshNode>(MESH_EVENTS.nodeUpdated)
      .pipe(debounceTime(1500), takeUntilDestroyed())
      .subscribe(() => this.nodeFeatures.reload());
  }

  protected selectNode(node: MeshNode): void {
    this.selectedNodeNum.set(node.nodeNum);
    if (node.position) {
      this.map()?.flyTo(node.position.longitude, node.position.latitude);
    }
  }

  protected onFeatureClick(featureId: string): void {
    const [layer, nodeNum] = featureId.split(':');
    if (layer === MAP_LAYERS.nodes) {
      this.selectedNodeNum.set(Number(nodeNum));
    }
  }

  protected reconnect(): void {
    this.actionError.set(null);
    this.api.reconnectGateway().subscribe({
      error: (error: unknown) => this.actionError.set(describeHttpError(error)),
    });
  }
}
