import { DatePipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  inject,
  input,
  signal,
  viewChild,
} from '@angular/core';
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
  formatNodeId,
  statusMark,
  tabOf,
  upsertMessage,
  utf8ByteCount,
} from '../mesh-format';
import { MESH_EVENTS, MESSAGE_MAX_BYTES, MeshMessage, MeshNode, Team } from '../mesh.models';

const PAGE_SIZE = 50;

/**
 * Bottom chat drawer: one tab per team (its own Meshtastic channel, sent through every gateway that carries it) and
 * one per direct-message conversation (sent through the gateway that heard the node best). There is no worldwide
 * channel. New messages and status changes arrive via SignalR (only for the people involved); tabs that are not open
 * count unread messages.
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
  /** My teams: each always has a tab. */
  readonly teams = input<Team[]>([]);

  private readonly draftInput = viewChild<ElementRef<HTMLInputElement>>('draftInput');

  protected readonly maxBytes = MESSAGE_MAX_BYTES;
  protected readonly expanded = signal(false);
  /** Direct-message conversations that are open. */
  private readonly conversations = signal<number[]>([]);
  private readonly selected = signal<ChatTab | null>(null);
  protected readonly unread = signal<Record<string, number>>({});
  /** Names seen when a conversation was opened, so a tab keeps its name when the node leaves the map view. */
  private readonly knownNames = signal<Record<number, string>>({});
  protected readonly draft = signal('');
  protected readonly sending = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly tabs = computed<ChatTab[]>(() => [
    ...this.teams().map((team): ChatTab => ({ kind: 'team', teamId: team.id })),
    ...this.conversations().map((nodeNum): ChatTab => ({ kind: 'dm', nodeNum })),
  ]);

  /** The selected tab, or the first one when nothing (valid) is selected. */
  protected readonly activeTab = computed<ChatTab | null>(() => {
    const selected = this.selected();
    const tabs = this.tabs();
    return (
      tabs.find((tab) => selected !== null && chatTabKey(tab) === chatTabKey(selected)) ??
      tabs[0] ??
      null
    );
  });

  protected readonly draftBytes = computed(() => utf8ByteCount(this.draft().trim()));
  protected readonly tooLong = computed(() => this.draftBytes() > MESSAGE_MAX_BYTES);
  protected readonly totalUnread = computed(() =>
    Object.values(this.unread()).reduce((sum, count) => sum + count, 0),
  );

  protected readonly messages = rxResource({
    params: () => {
      const tab = this.activeTab();
      return this.expanded() && tab ? tab : undefined;
    },
    stream: ({ params: tab }) =>
      this.api.getMessages(
        tab.kind === 'team'
          ? { page: 1, pageSize: PAGE_SIZE, team: tab.teamId }
          : { page: 1, pageSize: PAGE_SIZE, node: tab.nodeNum },
      ),
  });

  /** Oldest first, as a chat reads. */
  protected readonly visibleMessages = computed(() =>
    [...(this.messages.value()?.items ?? [])].reverse(),
  );

  private readonly nodeNames = computed<Record<number, string>>(() => ({
    ...this.knownNames(),
    ...Object.fromEntries(this.nodes().map((node) => [node.nodeNum, node.longName])),
  }));

  /** The active team, to warn when no gateway carries its channel. */
  protected readonly activeTeam = computed(() => {
    const tab = this.activeTab();
    return tab?.kind === 'team' ? this.teams().find((team) => team.id === tab.teamId) : undefined;
  });

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
    const name = this.nodes().find((node) => node.nodeNum === nodeNum)?.longName;
    if (name) {
      this.knownNames.update((names) => ({ ...names, [nodeNum]: name }));
    }
    this.ensureConversation(nodeNum);
    this.select({ kind: 'dm', nodeNum });
    this.expanded.set(true);
  }

  /** Opens a team's tab, e.g. from the teams panel. */
  openTeam(teamId: string): void {
    this.select({ kind: 'team', teamId });
    this.expanded.set(true);
  }

  protected select(tab: ChatTab): void {
    this.selected.set(tab);
    this.error.set(null);
    this.unread.update((counts) => {
      const rest = { ...counts };
      delete rest[chatTabKey(tab)];
      return rest;
    });
  }

  protected close(tab: ChatTab, event: Event): void {
    event.stopPropagation();
    if (tab.kind === 'dm') {
      this.conversations.update((nodes) => nodes.filter((nodeNum) => nodeNum !== tab.nodeNum));
    }
  }

  protected isActive(tab: ChatTab): boolean {
    const active = this.activeTab();
    return active !== null && chatTabKey(tab) === chatTabKey(active);
  }

  protected tabLabel(tab: ChatTab): string {
    if (tab.kind === 'team') {
      return this.teams().find((team) => team.id === tab.teamId)?.name ?? 'Team';
    }
    return this.nodeNames()[tab.nodeNum] ?? formatNodeId(tab.nodeNum);
  }

  protected senderLabel(message: MeshMessage): string {
    if (message.direction === 'Outbound') {
      return message.createdBy ?? 'You';
    }
    return (
      (message.fromNodeNum !== null ? this.nodeNames()[message.fromNodeNum] : undefined) ??
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
    const tab = this.activeTab();
    if (!text || !tab || this.tooLong() || this.sending()) {
      return;
    }
    this.sending.set(true);
    this.error.set(null);
    this.api
      .sendMessage({
        toNodeNum: tab.kind === 'dm' ? tab.nodeNum : null,
        teamId: tab.kind === 'team' ? tab.teamId : null,
        text,
      })
      .subscribe({
        next: (message) => {
          this.sending.set(false);
          this.draft.set('');
          // Clear the box itself too: the [value] binding skips '' when it never saw the typed text.
          const box = this.draftInput()?.nativeElement;
          if (box) {
            box.value = '';
          }
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
    const tab = tabOf(message);
    if (message.kind === 'Verification' || tab === null) {
      return;
    }
    if (tab.kind === 'dm') {
      this.ensureConversation(tab.nodeNum);
    }
    if (this.expanded() && this.isActive(tab)) {
      this.messages.update((page) => upsertMessage(page, message));
    } else if (message.direction === 'Inbound') {
      const key = chatTabKey(tab);
      this.unread.update((counts) => ({ ...counts, [key]: (counts[key] ?? 0) + 1 }));
    }
  }

  private ensureConversation(nodeNum: number): void {
    this.conversations.update((nodes) => (nodes.includes(nodeNum) ? nodes : [...nodes, nodeNum]));
  }
}
