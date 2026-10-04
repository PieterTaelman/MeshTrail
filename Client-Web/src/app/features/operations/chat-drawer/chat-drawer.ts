import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { rxResource, takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ButtonModule } from '@openng/optimus-ui/button';
import { InputTextModule } from '@openng/optimus-ui/inputtext';
import { MessageModule } from '@openng/optimus-ui/message';
import { merge } from 'rxjs';
import { describeHttpError } from '../../../core/api/problem-details';
import { RealtimeService } from '../../../core/realtime/realtime.service';
import { MeshApi } from '../mesh.api';
import {
  ChatTab,
  chatTabKey,
  statusMark,
  tabOf,
  upsertMessage,
  utf8ByteCount,
} from '../mesh-format';
import { MESH_EVENTS, MESSAGE_MAX_BYTES, MeshMessage, MeshNode } from '../mesh.models';

const PAGE_SIZE = 50;
const CHANNEL_TAB: ChatTab = { kind: 'channel', channel: 0 };

/**
 * Bottom chat drawer: the primary channel plus one tab per direct-message conversation. New messages and status
 * changes arrive via SignalR; tabs that are not open count unread messages.
 */
@Component({
  selector: 'app-chat-drawer',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, ButtonModule, InputTextModule, MessageModule],
  templateUrl: './chat-drawer.html',
  host: { class: 'block' },
})
export class ChatDrawer {
  private readonly api = inject(MeshApi);

  /** Known nodes, to show names instead of ids. */
  readonly nodes = input<MeshNode[]>([]);

  protected readonly maxBytes = MESSAGE_MAX_BYTES;
  protected readonly expanded = signal(false);
  protected readonly tabs = signal<ChatTab[]>([CHANNEL_TAB]);
  protected readonly activeTab = signal<ChatTab>(CHANNEL_TAB);
  protected readonly unread = signal<Record<string, number>>({});
  protected readonly draft = signal('');
  protected readonly sending = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly draftBytes = computed(() => utf8ByteCount(this.draft().trim()));
  protected readonly tooLong = computed(() => this.draftBytes() > MESSAGE_MAX_BYTES);
  protected readonly totalUnread = computed(() =>
    Object.values(this.unread()).reduce((sum, count) => sum + count, 0),
  );

  protected readonly messages = rxResource({
    params: () => (this.expanded() ? this.activeTab() : undefined),
    stream: ({ params: tab }) =>
      this.api.getMessages(
        tab.kind === 'channel'
          ? { page: 1, pageSize: PAGE_SIZE, channel: tab.channel }
          : { page: 1, pageSize: PAGE_SIZE, node: tab.nodeNum },
      ),
  });

  /** Oldest first, as a chat reads. */
  protected readonly visibleMessages = computed(() =>
    [...(this.messages.value()?.items ?? [])].reverse(),
  );

  private readonly nodeNames = computed(
    () => new Map(this.nodes().map((node) => [node.nodeNum, node.longName])),
  );

  protected readonly chatTabKey = chatTabKey;
  protected readonly statusMark = statusMark;

  constructor() {
    const realtime = inject(RealtimeService);
    merge(
      realtime.on<MeshMessage>(MESH_EVENTS.messageReceived),
      realtime.on<MeshMessage>(MESH_EVENTS.messageStatusChanged),
    )
      .pipe(takeUntilDestroyed())
      .subscribe((message) => this.onMessage(message));
  }

  /** Opens (or creates) the direct-message tab for a node, e.g. from the node detail panel. */
  openConversation(nodeNum: number): void {
    const tab: ChatTab = { kind: 'dm', nodeNum };
    this.ensureTab(tab);
    this.select(tab);
    this.expanded.set(true);
  }

  protected select(tab: ChatTab): void {
    this.activeTab.set(tab);
    this.error.set(null);
    this.unread.update((counts) => {
      const rest = { ...counts };
      delete rest[chatTabKey(tab)];
      return rest;
    });
  }

  protected close(tab: ChatTab, event: Event): void {
    event.stopPropagation();
    const key = chatTabKey(tab);
    this.tabs.update((tabs) => tabs.filter((item) => chatTabKey(item) !== key));
    if (chatTabKey(this.activeTab()) === key) {
      this.select(CHANNEL_TAB);
    }
  }

  protected isActive(tab: ChatTab): boolean {
    return chatTabKey(tab) === chatTabKey(this.activeTab());
  }

  protected tabLabel(tab: ChatTab): string {
    return tab.kind === 'channel'
      ? `Channel ${tab.channel}`
      : (this.nodeNames().get(tab.nodeNum) ?? `!${tab.nodeNum.toString(16).padStart(8, '0')}`);
  }

  protected senderLabel(message: MeshMessage): string {
    if (message.direction === 'Outbound') {
      return message.createdBy ?? 'You';
    }
    return (
      (message.fromNodeNum !== null ? this.nodeNames().get(message.fromNodeNum) : undefined) ??
      message.fromNodeId ??
      'Unknown'
    );
  }

  protected statusTitle(message: MeshMessage): string {
    return message.status === 'Failed'
      ? `Failed: ${message.failureReason ?? 'unknown'}`
      : message.status;
  }

  protected send(): void {
    const text = this.draft().trim();
    if (!text || this.tooLong() || this.sending()) {
      return;
    }
    const tab = this.activeTab();
    this.sending.set(true);
    this.error.set(null);
    this.api
      .sendMessage({
        channelIndex: tab.kind === 'channel' ? tab.channel : null,
        toNodeNum: tab.kind === 'dm' ? tab.nodeNum : null,
        text,
      })
      .subscribe({
        next: (message) => {
          this.sending.set(false);
          this.draft.set('');
          this.messages.update((page) => upsertMessage(page, message));
        },
        error: (error: unknown) => {
          this.sending.set(false);
          this.error.set(describeHttpError(error));
        },
      });
  }

  protected onKeydown(event: KeyboardEvent): void {
    // Enter sends; Shift+Enter is not needed for one-line radio messages.
    if (event.key === 'Enter') {
      event.preventDefault();
      this.send();
    }
  }

  private onMessage(message: MeshMessage): void {
    if (message.kind === 'Verification') {
      return;
    }
    const tab = tabOf(message);
    if (tab.kind === 'channel' && tab.channel !== 0) {
      // Only the primary channel has a tab for now.
      return;
    }
    if (tab.kind === 'dm') {
      this.ensureTab(tab);
    }
    if (this.expanded() && this.isActive(tab)) {
      this.messages.update((page) => upsertMessage(page, message));
    } else if (message.direction === 'Inbound') {
      const key = chatTabKey(tab);
      this.unread.update((counts) => ({ ...counts, [key]: (counts[key] ?? 0) + 1 }));
    }
  }

  private ensureTab(tab: ChatTab): void {
    const key = chatTabKey(tab);
    this.tabs.update((tabs) =>
      tabs.some((item) => chatTabKey(item) === key) ? tabs : [...tabs, tab],
    );
  }
}
