// TypeScript copies of Meshtrail.Core.Contracts/Mesh and /Map. Keep them in sync with the C# records.
// Names and texts come from the radio: always render them as text (interpolation), never as HTML.

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

/** Pending = credentials handed out, waiting for the first uplink (which tells us which node it is). */
export type GatewayState = 'Pending' | 'Online' | 'Offline' | 'Revoked';

/** A node that connects the mesh around it to Meshtrail. lastError and mqttUserName only for your own (isMine). */
export interface Gateway {
  id: string;
  nodeNum: number | null;
  nodeId: string | null;
  /** From the radio: render as text. */
  name: string;
  transport: 'Mqtt' | 'Tcp' | 'Simulated';
  status: GatewayState;
  statusChangedAt: string;
  lastUplinkAt: string | null;
  lastError: string | null;
  firmwareVersion: string | null;
  broker: string | null;
  channels: string[];
  isMine: boolean;
  mqttUserName: string | null;
  createdAt: string;
  position: Position | null;
}

export interface GatewaySummary {
  online: number;
  total: number;
}

/** What goes in the node's MQTT settings. serverAddress null = not configured on the server. */
export interface MqttSetup {
  serverAddress: string | null;
  port: number;
  useTls: boolean;
  root: string;
}

/** Answer of "Add gateway": the password is shown this one time only. */
export interface GatewayCredentials {
  gateway: Gateway;
  userName: string;
  password: string;
  setup: MqttSetup;
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
  /** Only nodes in this map view. */
  bbox?: BoundingBox;
  /** Only nodes registered to me. */
  mine?: boolean;
}

/** A map view: [west, south, east, north] in degrees. */
export type BoundingBox = [number, number, number, number];

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

/** A gateway that heard the node. The first one is the gateway we would send through. */
export interface HeardBy {
  gatewayNodeNum: number;
  gatewayNodeId: string;
  gatewayName: string;
  gatewayOnline: boolean;
  lastHeardAt: string;
  snr: number | null;
  rssi: number | null;
  hopsAway: number | null;
}

export interface NodeDetail {
  node: MeshNode;
  lastTraceroute: NodeTraceroute | null;
  heardBy: HeardBy[];
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

/** Properties the "gateways" map layer puts on each GeoJSON feature. */
export interface GatewayFeatureProperties {
  layer: 'gateways';
  gatewayId: string;
  nodeNum: number;
  nodeId: string;
  longName: string;
  transport: string;
  status: GatewayState;
  lastUplinkAt: string | null;
}

export type MessageStatus = 'Queued' | 'Sent' | 'Acked' | 'Failed' | 'Received';

/** A chat message. toNodeNum null = channel broadcast; peerNodeNum = the other node of a direct message. */
export interface MeshMessage {
  id: string;
  direction: 'Inbound' | 'Outbound';
  /** Verification messages never show their text (the code stays on the server). */
  kind: 'Text' | 'Verification';
  channelIndex: number;
  channelName: string | null;
  /** The gateway it went out through (or first arrived through; null for team messages we sent). */
  gatewayNodeNum: number | null;
  /** Team chat: the team whose channel it is on. */
  teamId: string | null;
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

/** One direct-message conversation (node) or one team chat (team). There is no worldwide channel. */
export interface MessageListRequest {
  page: number;
  pageSize: number;
  node?: number;
  team?: string;
}

/** Exactly one of toNodeNum (direct message) or teamId (team chat). */
export interface SendMessageRequest {
  toNodeNum: number | null;
  teamId: string | null;
  text: string;
}

export interface TeamMember {
  userName: string;
  role: 'Owner' | 'Member';
  joinedAt: string;
}

/** A team you are in. gatewaysOnline = online gateways carrying its channel (0 = team messages cannot go out). */
export interface Team {
  id: string;
  name: string;
  /** The Meshtastic channel the team chats on (configure the same name and key on the nodes). */
  channelName: string;
  joinCode: string;
  myRole: 'Owner' | 'Member';
  members: TeamMember[];
  gatewaysOnline: number;
  createdAt: string;
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
export const MAP_LAYERS = { nodes: 'nodes', gateways: 'gateways' } as const;
