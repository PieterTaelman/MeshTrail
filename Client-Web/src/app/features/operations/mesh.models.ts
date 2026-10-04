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

export type MessageStatus = 'Queued' | 'Sent' | 'Acked' | 'Failed' | 'Received';

/** A chat message. toNodeNum null = channel broadcast; peerNodeNum = the other node of a direct message. */
export interface MeshMessage {
  id: string;
  direction: 'Inbound' | 'Outbound';
  /** Verification messages never show their text (the code stays on the server). */
  kind: 'Text' | 'Verification';
  channelIndex: number;
  fromNodeNum: number | null;
  fromNodeId: string | null;
  toNodeNum: number | null;
  toNodeId: string | null;
  peerNodeNum: number | null;
  text: string;
  status: MessageStatus;
  failureReason: string | null;
  snr: number | null;
  rssi: number | null;
  hopsAway: number | null;
  createdAt: string;
  createdBy: string | null;
  sentAt: string | null;
  ackedAt: string | null;
}

export interface MessageListRequest {
  page: number;
  pageSize: number;
  channel?: number;
  node?: number;
}

export interface SendMessageRequest {
  channelIndex: number | null;
  toNodeNum: number | null;
  text: string;
}

export type RegistrationStatus = 'Claimed' | 'Verified' | 'Revoked';

export interface Registration {
  id: string;
  nodeNum: number;
  nodeId: string;
  longName: string;
  shortName: string;
  status: RegistrationStatus;
  userName: string;
  claimedAt: string;
  codeExpiresAt: string | null;
  attemptsLeft: number;
  verifiedAt: string | null;
  revokedReason: string | null;
  verificationMessageId: string | null;
  /** Whether the direct message with the code reached the node. */
  verificationMessageStatus: MessageStatus | null;
}

/** Max size of a message we send (MeshMessage.TextMaxBytes in C#). */
export const MESSAGE_MAX_BYTES = 200;

/** SignalR event names pushed by the API (see the Push…ToClientsHandler classes). */
export const MESH_EVENTS = {
  nodeUpdated: 'NodeUpdated',
  gatewayStatusChanged: 'GatewayStatusChanged',
  tracerouteCompleted: 'TracerouteCompleted',
  messageReceived: 'MessageReceived',
  messageStatusChanged: 'MessageStatusChanged',
} as const;

/** Map layer names (MapLayers in C#). */
export const MAP_LAYERS = { nodes: 'nodes' } as const;
