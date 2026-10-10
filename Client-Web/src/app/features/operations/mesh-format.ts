import {
  BoundingBox,
  GatewayState,
  GatewaySummary,
  MeshMessage,
  MeshNode,
  MessageStatus,
  PagedResult,
} from './mesh.models';

// Small pure helpers for showing mesh data. Kept out of components so they are easy to unit test.

/** A node counts as online when it was heard this recently (same rule as MeshNode.OnlineWindow in C#). */
export const ONLINE_WINDOW_MS = 15 * 60 * 1000;

/** Marker colours by how long ago a node was heard: fresh → green, older → yellow/orange, stale → grey. */
export const LAST_HEARD_COLORS = {
  online: '#22c55e',
  recent: '#eab308',
  today: '#f97316',
  stale: '#64748b',
} as const;

export function msSince(time: string | null, now: number): number | null {
  if (!time) {
    return null;
  }
  const at = Date.parse(time);
  return Number.isNaN(at) ? null : Math.max(0, now - at);
}

export function isOnline(lastHeardAt: string | null, now: number): boolean {
  const age = msSince(lastHeardAt, now);
  return age !== null && age <= ONLINE_WINDOW_MS;
}

export function lastHeardColor(lastHeardAt: string | null, now: number): string {
  const age = msSince(lastHeardAt, now);
  if (age === null) {
    return LAST_HEARD_COLORS.stale;
  }
  if (age <= ONLINE_WINDOW_MS) {
    return LAST_HEARD_COLORS.online;
  }
  if (age <= 2 * 60 * 60 * 1000) {
    return LAST_HEARD_COLORS.recent;
  }
  if (age <= 24 * 60 * 60 * 1000) {
    return LAST_HEARD_COLORS.today;
  }
  return LAST_HEARD_COLORS.stale;
}

/** "just now", "5 min ago", "3 h ago", "2 d ago", or "never". */
export function formatAge(time: string | null, now: number): string {
  const age = msSince(time, now);
  if (age === null) {
    return 'never';
  }
  const minutes = Math.floor(age / 60_000);
  if (minutes < 1) {
    return 'just now';
  }
  if (minutes < 60) {
    return `${minutes} min ago`;
  }
  const hours = Math.floor(minutes / 60);
  if (hours < 48) {
    return `${hours} h ago`;
  }
  return `${Math.floor(hours / 24)} d ago`;
}

/** Battery as text; the firmware reports 101 when the device runs on external power (USB). */
export function formatBattery(level: number | null, isExternalPower: boolean): string {
  if (isExternalPower) {
    return 'External power';
  }
  return level === null ? '—' : `${level} %`;
}

export function formatNumber(value: number | null, unit: string, digits = 1): string {
  return value === null ? '—' : `${value.toFixed(digits)} ${unit}`;
}

/** Replaces the node in the list (or adds it), keeping the most recently heard first. */
export function upsertNode(
  page: PagedResult<MeshNode> | undefined,
  node: MeshNode,
): PagedResult<MeshNode> | undefined {
  if (!page) {
    return page;
  }
  const exists = page.items.some((item) => item.nodeNum === node.nodeNum);
  const items = exists
    ? page.items.map((item) => (item.nodeNum === node.nodeNum ? node : item))
    : [...page.items, node];
  items.sort((a, b) => (b.lastHeardAt ?? '').localeCompare(a.lastHeardAt ?? ''));
  return { ...page, items, totalCount: exists ? page.totalCount : page.totalCount + 1 };
}

/** Bytes the text takes on the radio (UTF-8): emoji and accents count 2–4. */
export function utf8ByteCount(text: string): number {
  return new TextEncoder().encode(text).length;
}

/** A chat tab: one team's chat, or the direct-message conversation with one node. */
export type ChatTab = { kind: 'team'; teamId: string } | { kind: 'dm'; nodeNum: number };

export function chatTabKey(tab: ChatTab): string {
  return tab.kind === 'team' ? `team:${tab.teamId}` : `dm:${tab.nodeNum}`;
}

/**
 * The tab a message belongs in: its team, the conversation with the other node, or null for a channel message
 * outside a team (there is no worldwide channel tab).
 */
export function tabOf(message: MeshMessage): ChatTab | null {
  if (message.teamId) {
    return { kind: 'team', teamId: message.teamId };
  }
  return message.toNodeNum === null || message.peerNodeNum === null
    ? null
    : { kind: 'dm', nodeNum: message.peerNodeNum };
}

/**
 * Reads a node id as people type it: "!f115aaec" (as the Meshtastic app shows it), "f115aaec", or the decimal number.
 * Returns null for anything else.
 */
export function parseNodeId(text: string): number | null {
  const value = text.trim().toLowerCase();
  const hex = /^!?([0-9a-f]{8})$/.exec(value);
  if (hex) {
    return parseInt(hex[1], 16);
  }
  if (/^[0-9]{1,10}$/.test(value)) {
    const number = Number(value);
    return number > 0 && number < 0xffffffff ? number : null;
  }
  return null;
}

/** "!" + 8 hex digits, as people write node numbers. */
export function formatNodeId(nodeNum: number): string {
  return `!${nodeNum.toString(16).padStart(8, '0')}`;
}

/** Is the point inside the map view? Handles views that cross the 180° meridian. */
export function inBox(box: BoundingBox | null, latitude: number, longitude: number): boolean {
  if (!box) {
    return false;
  }
  const [west, south, east, north] = box;
  const inLatitude = latitude >= south && latitude <= north;
  return (
    inLatitude &&
    (west <= east ? longitude >= west && longitude <= east : longitude >= west || longitude <= east)
  );
}

/** Colour of a gateway by status (map ring and status dot). */
export function gatewayColor(status: GatewayState): string {
  switch (status) {
    case 'Online':
      return '#22c55e';
    case 'Pending':
      return '#eab308';
    case 'Offline':
      return '#ef4444';
    default:
      return '#64748b';
  }
}

/** Overall state of the gateways chip: all online, some offline, none online, or no gateways at all. */
export function gatewaySummaryState(
  summary: GatewaySummary | undefined,
): 'ok' | 'partial' | 'down' | 'none' {
  if (!summary || summary.total === 0) {
    return 'none';
  }
  if (summary.online === summary.total) {
    return 'ok';
  }
  return summary.online === 0 ? 'down' : 'partial';
}

/** Short status mark shown next to our own messages. */
export function statusMark(status: MessageStatus): string {
  switch (status) {
    case 'Queued':
      return '…';
    case 'Sent':
      return '✓';
    case 'Acked':
      return '✓✓';
    case 'Failed':
      return '!';
    default:
      return '';
  }
}

/** Replaces a message by id or adds it, keeping the list newest first (as the API returns it). */
export function upsertMessage(
  page: PagedResult<MeshMessage> | undefined,
  message: MeshMessage,
): PagedResult<MeshMessage> | undefined {
  if (!page) {
    return page;
  }
  const exists = page.items.some((item) => item.id === message.id);
  const items = exists
    ? page.items.map((item) => (item.id === message.id ? message : item))
    : [message, ...page.items];
  items.sort((a, b) => b.createdAt.localeCompare(a.createdAt));
  return { ...page, items, totalCount: exists ? page.totalCount : page.totalCount + 1 };
}
