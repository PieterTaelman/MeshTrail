import { chatTabKey, statusMark, tabOf, upsertMessage, utf8ByteCount } from './mesh-format';
import { MeshMessage, PagedResult } from './mesh.models';

const message = (overrides: Partial<MeshMessage>): MeshMessage =>
  ({
    id: 'a',
    direction: 'Inbound',
    kind: 'Text',
    channelIndex: 0,
    toNodeNum: null,
    peerNodeNum: null,
    status: 'Received',
    createdAt: '2026-10-04T10:00:00Z',
    ...overrides,
  }) as MeshMessage;

describe('chat helpers', () => {
  it('counts bytes, not characters (the radio limit is 200 bytes)', () => {
    expect(utf8ByteCount('hello')).toBe(5);
    expect(utf8ByteCount('é')).toBe(2);
    expect(utf8ByteCount('€')).toBe(3);
    expect(utf8ByteCount('🙂')).toBe(4);
  });

  it('puts broadcasts in their channel tab and direct messages in the peer tab', () => {
    expect(chatTabKey(tabOf(message({ channelIndex: 0 })))).toBe('ch:0');
    expect(chatTabKey(tabOf(message({ toNodeNum: 1, peerNodeNum: 42 })))).toBe('dm:42');
  });

  it('shows a mark per delivery status', () => {
    expect(statusMark('Queued')).toBe('…');
    expect(statusMark('Sent')).toBe('✓');
    expect(statusMark('Acked')).toBe('✓✓');
    expect(statusMark('Failed')).toBe('!');
  });

  it('updates a message in place and adds new ones on top', () => {
    const page: PagedResult<MeshMessage> = {
      items: [message({ id: 'a', status: 'Sent' })],
      totalCount: 1,
      page: 1,
      pageSize: 50,
    };

    const updated = upsertMessage(page, message({ id: 'a', status: 'Acked' }));
    const added = upsertMessage(updated, message({ id: 'b', createdAt: '2026-10-04T10:05:00Z' }));

    expect(updated?.items[0].status).toBe('Acked');
    expect(updated?.totalCount).toBe(1);
    expect(added?.items.map((item) => item.id)).toEqual(['b', 'a']);
  });
});
