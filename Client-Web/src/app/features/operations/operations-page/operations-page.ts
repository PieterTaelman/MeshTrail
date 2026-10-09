import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { rxResource, takeUntilDestroyed, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { ButtonModule } from '@openng/optimus-ui/button';
import { IconFieldModule } from '@openng/optimus-ui/iconfield';
import { SearchIcon } from '@openng/optimus-ui/icons/search';
import { InputIconModule } from '@openng/optimus-ui/inputicon';
import { InputTextModule } from '@openng/optimus-ui/inputtext';
import { MessageModule } from '@openng/optimus-ui/message';
import { debounceTime, distinctUntilChanged, map, of } from 'rxjs';
import { MapBounds, MapFeatures, MapView } from '../../../core/map/map-view';
import { RealtimeService } from '../../../core/realtime/realtime.service';
import { ChatDrawer } from '../chat-drawer/chat-drawer';
import { GatewayChip } from '../gateway-chip';
import { GatewaysPanel } from '../gateways-panel/gateways-panel';
import { MeshApi } from '../mesh.api';
import {
  formatAge,
  gatewayColor,
  inBox,
  isOnline,
  lastHeardColor,
  upsertNode,
} from '../mesh-format';
import {
  BoundingBox,
  Gateway,
  GatewayFeatureProperties,
  MAP_LAYERS,
  MESH_EVENTS,
  MeshNode,
  NodeFeatureProperties,
  PagedResult,
} from '../mesh.models';
import { NodeDetail } from '../node-detail/node-detail';
import { RegistrationDialog } from '../registration-dialog/registration-dialog';
import { TeamsPanel } from '../teams-panel/teams-panel';

/** Nodes listed at once (in view or found by search). Zoom in or search to see others. */
const NODE_PAGE_SIZE = 200;

const NO_NODES: PagedResult<MeshNode> = {
  items: [],
  totalCount: 0,
  page: 1,
  pageSize: NODE_PAGE_SIZE,
};

/**
 * The operations map, for nodes anywhere in the world. The map and the node list load what is in view (or what a
 * search finds, anywhere); the server pushes changes for the area in view only. Gateways show as rings coloured by
 * status, and the GATEWAYS chip opens "My gateways". Nothing polls.
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
    SearchIcon,
    ChatDrawer,
    GatewayChip,
    GatewaysPanel,
    MapView,
    NodeDetail,
    RegistrationDialog,
    TeamsPanel,
  ],
  templateUrl: './operations-page.html',
  host: { class: 'block h-full' },
})
export class OperationsPage {
  private readonly api = inject(MeshApi);
  private readonly realtime = inject(RealtimeService);
  private readonly map = viewChild(MapView);
  private readonly chat = viewChild(ChatDrawer);

  /** Ticks every 30 s so "last heard" texts and marker colours age without reloading. */
  protected readonly now = signal(Date.now());

  /** The visible map area; null until the map is ready. */
  protected readonly view = signal<BoundingBox | null>(null);
  protected readonly searchText = signal('');
  protected readonly onlyMine = signal(false);
  protected readonly showNodes = signal(true);
  protected readonly showGateways = signal(true);
  protected readonly selectedNodeNum = signal<number | null>(null);
  /** What the right-hand panel shows when no node is selected. */
  protected readonly sidePanel = signal<'gateways' | 'teams' | null>(null);
  protected readonly registrationOpen = signal(false);

  /** The search, once the user stops typing. */
  private readonly search = toSignal(
    toObservable(this.searchText).pipe(
      debounceTime(300),
      map((text) => text.trim()),
      distinctUntilChanged(),
    ),
    { initialValue: '' },
  );

  /** A search or "only my nodes" looks worldwide; otherwise the list shows the nodes in view. */
  protected readonly searching = computed(() => this.search() !== '' || this.onlyMine());

  protected readonly nodes = rxResource({
    params: () => ({ view: this.view(), search: this.search(), mine: this.onlyMine() }),
    stream: ({ params }) => {
      if (params.search || params.mine) {
        return this.api.getNodes({
          page: 1,
          pageSize: NODE_PAGE_SIZE,
          search: params.search || undefined,
          mine: params.mine || undefined,
        });
      }
      return params.view
        ? this.api.getNodes({ page: 1, pageSize: NODE_PAGE_SIZE, bbox: params.view })
        : of(NO_NODES);
    },
  });

  private readonly features = rxResource({
    params: () => this.view() ?? undefined,
    stream: ({ params }) =>
      this.api.getMapFeatures([MAP_LAYERS.nodes, MAP_LAYERS.gateways], params),
  });

  protected readonly gatewaySummary = rxResource({ stream: () => this.api.getGatewaySummary() });
  protected readonly teams = rxResource({ stream: () => this.api.getTeams() });
  protected readonly myTeams = computed(() => this.teams.value() ?? []);

  protected readonly listedNodes = computed(() => this.nodes.value()?.items ?? []);
  protected readonly listedTotal = computed(() => this.nodes.value()?.totalCount ?? 0);
  protected readonly onlineCount = computed(
    () => this.listedNodes().filter((node) => isOnline(node.lastHeardAt, this.now())).length,
  );

  /** Node features coloured by last heard (recomputed as time passes). Clustered by the map when zoomed out. */
  protected readonly nodeFeatures = computed<MapFeatures>(() => {
    const now = this.now();
    const features = this.showNodes() ? (this.features.value()?.features ?? []) : [];
    return {
      type: 'FeatureCollection',
      features: features
        .filter((feature) => feature.properties?.['layer'] === MAP_LAYERS.nodes)
        .map((feature) => {
          const properties = feature.properties as unknown as NodeFeatureProperties;
          return {
            ...feature,
            properties: {
              ...properties,
              color: lastHeardColor(properties.lastHeardAt, now),
              radius: 7,
            },
          };
        }),
    };
  });

  /** Gateways as rings in their status colour, drawn on top of the nodes. */
  protected readonly gatewayMarkers = computed<MapFeatures>(() => {
    const features = this.showGateways() ? (this.features.value()?.features ?? []) : [];
    return {
      type: 'FeatureCollection',
      features: features
        .filter((feature) => feature.properties?.['layer'] === MAP_LAYERS.gateways)
        .map((feature) => {
          const properties = feature.properties as unknown as GatewayFeatureProperties;
          return {
            ...feature,
            properties: { ...properties, color: gatewayColor(properties.status) },
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

    // Only nodes in view are pushed (see watchArea); keep the list in sync without reloading.
    this.realtime
      .on<MeshNode>(MESH_EVENTS.nodeUpdated)
      .pipe(takeUntilDestroyed())
      .subscribe((node) => this.onNodeUpdated(node));

    // Positions change in bursts; refresh the map layers once things settle.
    this.realtime
      .on<MeshNode>(MESH_EVENTS.nodeUpdated)
      .pipe(debounceTime(1500), takeUntilDestroyed())
      .subscribe(() => this.features.reload());

    this.realtime
      .on<Gateway>(MESH_EVENTS.gatewayStatusChanged)
      .pipe(debounceTime(1000), takeUntilDestroyed())
      .subscribe(() => {
        this.gatewaySummary.reload();
        this.features.reload();
        // A gateway may have started or stopped carrying a team's channel.
        this.teams.reload();
      });
  }

  protected onBoundsChange(bounds: MapBounds): void {
    this.view.set(bounds);
    this.realtime.watchArea(bounds);
  }

  protected selectNode(node: MeshNode): void {
    this.showNodeDetail(node.nodeNum);
    if (node.position) {
      this.map()?.flyTo(node.position.longitude, node.position.latitude);
    }
  }

  protected showNodeDetail(nodeNum: number): void {
    this.sidePanel.set(null);
    this.selectedNodeNum.set(nodeNum);
  }

  protected openPanel(panel: 'gateways' | 'teams'): void {
    this.selectedNodeNum.set(null);
    this.sidePanel.set(panel);
  }

  protected openTeamChat(teamId: string): void {
    this.chat()?.openTeam(teamId);
  }

  protected onFeatureClick(featureId: string): void {
    const [layer, id] = featureId.split(':');
    if (layer === MAP_LAYERS.nodes) {
      this.showNodeDetail(Number(id));
      return;
    }

    // A gateway ring: show the gateway's node.
    const gateway = this.gatewayMarkers().features.find(
      (feature) => String(feature.id) === featureId,
    );
    const nodeNum = (gateway?.properties as GatewayFeatureProperties | undefined)?.nodeNum;
    if (nodeNum !== undefined) {
      this.showNodeDetail(nodeNum);
    }
  }

  protected openConversation(nodeNum: number): void {
    this.chat()?.openConversation(nodeNum);
  }

  private onNodeUpdated(node: MeshNode): void {
    const listed = this.listedNodes().some((item) => item.nodeNum === node.nodeNum);
    const inView =
      !this.searching() &&
      node.position !== null &&
      inBox(this.view(), node.position.latitude, node.position.longitude);
    if (listed || inView) {
      this.nodes.update((page) => upsertNode(page, node));
    }
  }
}
