import {
  LAST_HEARD_COLORS,
  formatAge,
  formatBattery,
  gatewaySummaryState,
  inBox,
  isOnline,
  lastHeardColor,
  upsertNode,
} from './mesh-format';
import { MeshNode, PagedResult } from './mesh.models';

const NOW = Date.parse('2026-10-03T20:00:00Z');
const minutesAgo = (minutes: number) => new Date(NOW - minutes * 60_000).toISOString();

describe('mesh-format', () => {
  it('treats a node heard within 15 minutes as online', () => {
    expect(isOnline(minutesAgo(14), NOW)).toBe(true);
    expect(isOnline(minutesAgo(16), NOW)).toBe(false);
    expect(isOnline(null, NOW)).toBe(false);
  });

  it('colours markers by last heard', () => {
    expect(lastHeardColor(minutesAgo(5), NOW)).toBe(LAST_HEARD_COLORS.online);
    expect(lastHeardColor(minutesAgo(90), NOW)).toBe(LAST_HEARD_COLORS.recent);
    expect(lastHeardColor(minutesAgo(600), NOW)).toBe(LAST_HEARD_COLORS.today);
    expect(lastHeardColor(minutesAgo(3000), NOW)).toBe(LAST_HEARD_COLORS.stale);
    expect(lastHeardColor(null, NOW)).toBe(LAST_HEARD_COLORS.stale);
  });

  it('formats ages for people', () => {
    expect(formatAge(minutesAgo(0), NOW)).toBe('just now');
    expect(formatAge(minutesAgo(5), NOW)).toBe('5 min ago');
    expect(formatAge(minutesAgo(180), NOW)).toBe('3 h ago');
    expect(formatAge(minutesAgo(60 * 72), NOW)).toBe('3 d ago');
    expect(formatAge(null, NOW)).toBe('never');
  });

  it('shows external power instead of 101 %', () => {
    expect(formatBattery(101, true)).toBe('External power');
    expect(formatBattery(80, false)).toBe('80 %');
    expect(formatBattery(null, false)).toBe('—');
  });

  it('knows whether a point is in the map view, also across the 180° meridian', () => {
    expect(inBox([2, 49, 7, 52], 50.8, 4.3)).toBe(true);
    expect(inBox([2, 49, 7, 52], 48, 4.3)).toBe(false);
    expect(inBox([170, -10, -170, 10], 0, 175)).toBe(true);
    expect(inBox([170, -10, -170, 10], 0, 0)).toBe(false);
    expect(inBox(null, 0, 0)).toBe(false);
  });

  it('summarises the gateways for the top-bar chip', () => {
    expect(gatewaySummaryState(undefined)).toBe('none');
    expect(gatewaySummaryState({ online: 0, total: 0 })).toBe('none');
    expect(gatewaySummaryState({ online: 3, total: 3 })).toBe('ok');
    expect(gatewaySummaryState({ online: 1, total: 3 })).toBe('partial');
    expect(gatewaySummaryState({ online: 0, total: 2 })).toBe('down');
  });

  it('updates a known node in place and adds new ones, most recently heard first', () => {
    const node = (nodeNum: number, minutes: number) =>
      ({ nodeNum, lastHeardAt: minutesAgo(minutes) }) as MeshNode;
    const page: PagedResult<MeshNode> = {
      items: [node(1, 5), node(2, 10)],
      totalCount: 2,
      page: 1,
      pageSize: 500,
    };

    const updated = upsertNode(page, node(2, 1));
    const added = upsertNode(updated, node(3, 30));

    expect(updated?.items.map((item) => item.nodeNum)).toEqual([2, 1]);
    expect(updated?.totalCount).toBe(2);
    expect(added?.items.map((item) => item.nodeNum)).toEqual([2, 1, 3]);
    expect(added?.totalCount).toBe(3);
  });
});
