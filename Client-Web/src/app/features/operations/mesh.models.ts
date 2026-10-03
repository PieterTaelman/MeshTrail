// TypeScript copies of Meshtrail.Core.Contracts/Mesh and /Map. Keep them in sync with the C# records.
// Names and texts come from the radio: always render them as text (interpolation), never as HTML.

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export type GatewayState = 'Connecting' | 'Online' | 'Offline';

export interface GatewayStatus {
  status: GatewayState;
  /** "Tcp" (real node) or "Simulated". */
  mode: string;
  statusChangedAt: string;
  lastConnectedAt: string | null;
  lastError: string | null;
  nodeNum: number | null;
  nodeId: string | null;
  firmwareVersion: string | null;
}

export interface Position {
  latitude: number;
  longitude: number;
  altitude: number | null;
  time: string;
  /** 32 = exact; lower = deliberately blurred by the sender. */
  precisionBits: number;
}

export interface MeshNode {
  nodeNum: number;
  nodeId: string;
  longName: string;
  shortName: string;
  hardwareModel: string | null;
  role: string | null;
  hasPublicKey: boolean;
  source: string;
  firstSeenAt: string;
  lastHeardAt: string | null;
  isOnline: boolean;
  snr: number | null;
  rssi: number | null;
  hopsAway: number | null;
  /** 0–100, or 101 = external power (see isExternalPower). */
  batteryLevel: number | null;
  isExternalPower: boolean;
  voltage: number | null;
  position: Position | null;
  isGateway: boolean;
  isRegistered: boolean;
}

export interface NodeListRequest {
  page: number;
  pageSize: number;
  search?: string;
  registered?: boolean;
  online?: boolean;
}

export interface RouteHop {
  nodeNum: number;
  nodeId: string;
  /** dB, null = unknown. */
  snr: number | null;
}

export type TracerouteState = 'Pending' | 'Completed' | 'TimedOut';

export interface NodeTraceroute {
  id: string;
  nodeNum: number;
  status: TracerouteState;
  requestedAt: string;
  requestedBy: string;
  completedAt: string | null;
  routeTowards: RouteHop[];
  routeBack: RouteHop[];
}

export interface NodeDetail {
  node: MeshNode;
  lastTraceroute: NodeTraceroute | null;
}

/** Properties the "nodes" map layer puts on each GeoJSON feature. */
export interface NodeFeatureProperties {
  layer: 'nodes';
  source: string;
  nodeNum: number;
  nodeId: string;
  longName: string;
  shortName: string;
  lastHeardAt: string | null;
  isOnline: boolean;
  isGateway: boolean;
  batteryLevel: number | null;
  isExternalPower: boolean;
  positionTime: string;
  precisionBits: number;
}

/** SignalR event names pushed by the API (see the Push…ToClientsHandler classes). */
export const MESH_EVENTS = {
  nodeUpdated: 'NodeUpdated',
  gatewayStatusChanged: 'GatewayStatusChanged',
  tracerouteCompleted: 'TracerouteCompleted',
} as const;

/** Map layer names (MapLayers in C#). */
export const MAP_LAYERS = { nodes: 'nodes' } as const;
