import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  input,
  signal,
} from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { ButtonModule } from '@openng/optimus-ui/button';
import { InputTextModule } from '@openng/optimus-ui/inputtext';
import { MessageModule } from '@openng/optimus-ui/message';
import { describeHttpError } from '../../core/api/problem-details';
import { AuthService } from '../../core/auth/auth.service';
import { GatewaysPanel } from '../operations/gateways-panel/gateways-panel';
import { MeshApi } from '../operations/mesh.api';
import { MyNodesPanel } from '../operations/my-nodes-panel/my-nodes-panel';
import { TeamsPanel } from '../operations/teams-panel/teams-panel';

type ProfileTab = 'details' | 'gateways' | 'nodes' | 'teams';

const TABS: { id: ProfileTab; label: string }[] = [
  { id: 'gateways', label: 'My gateways' },
  { id: 'nodes', label: 'My nodes' },
  { id: 'teams', label: 'My teams' },
  { id: 'details', label: 'Personal details' },
];

/** Your profile: your gateways, your nodes, your teams and your name. The tab is in the URL (?tab=…). */
@Component({
  selector: 'app-profile-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ButtonModule, InputTextModule, MessageModule, GatewaysPanel, MyNodesPanel, TeamsPanel],
  template: `
    <section class="mx-auto flex w-full max-w-3xl flex-col gap-4">
      <header class="flex flex-wrap items-end justify-between gap-2">
        <div>
          <span class="app-eyebrow">Profile</span>
          <h1 class="text-2xl font-semibold">{{ auth.profile()?.displayName }}</h1>
          <p class="font-mono text-xs text-muted-color">{{ auth.profile()?.email }}</p>
        </div>
        <p-button
          label="Sign out"
          size="small"
          severity="secondary"
          [outlined]="true"
          (onClick)="signOut()"
        />
      </header>

      <div
        class="flex flex-wrap gap-1 border-b border-content-border"
        role="tablist"
        aria-label="Profile sections"
      >
        @for (item of tabs; track item.id) {
          <button
            type="button"
            role="tab"
            class="-mb-px border-b-2 px-3 py-2 text-sm"
            [class.border-primary]="activeTab() === item.id"
            [class.text-primary]="activeTab() === item.id"
            [class.border-transparent]="activeTab() !== item.id"
            [attr.aria-selected]="activeTab() === item.id"
            (click)="open(item.id)"
          >
            {{ item.label }}
          </button>
        }
      </div>

      @switch (activeTab()) {
        @case ('gateways') {
          <app-gateways-panel [now]="now()" [closable]="false" (showNode)="showNode($event)" />
        }
        @case ('nodes') {
          <app-my-nodes-panel />
        }
        @case ('teams') {
          <app-teams-panel
            [teams]="teams.value() ?? []"
            [closable]="false"
            (changed)="teams.reload()"
            (openChat)="openTeamChat($event)"
          />
        }
        @case ('details') {
          <form
            class="flex max-w-md flex-col gap-3 rounded-xl border border-content-border bg-content p-4"
            (submit)="$event.preventDefault(); save()"
          >
            <label class="flex flex-col gap-1 text-sm">
              Email
              <input pInputText [value]="auth.profile()?.email ?? ''" disabled />
            </label>
            <label class="flex flex-col gap-1 text-sm">
              First name
              <input
                pInputText
                maxlength="100"
                [value]="firstName()"
                (input)="firstName.set($any($event.target).value)"
              />
            </label>
            <label class="flex flex-col gap-1 text-sm">
              Last name
              <input
                pInputText
                maxlength="100"
                [value]="lastName()"
                (input)="lastName.set($any($event.target).value)"
              />
            </label>
            @if (message(); as text) {
              <p-message [severity]="saved() ? 'success' : 'error'" [text]="text" />
            }
            <p-button
              type="submit"
              label="Save"
              [disabled]="!firstName().trim() || !lastName().trim()"
            />
          </form>
        }
      }
    </section>
  `,
})
export class ProfilePage {
  private readonly api = inject(MeshApi);
  private readonly router = inject(Router);
  protected readonly auth = inject(AuthService);

  /** From the URL: ?tab=gateways|nodes|teams|details. */
  readonly tab = input<string>('');

  protected readonly tabs = TABS;
  protected readonly activeTab = computed<ProfileTab>(() =>
    TABS.some((item) => item.id === this.tab()) ? (this.tab() as ProfileTab) : 'gateways',
  );
  protected readonly teams = rxResource({ stream: () => this.api.getTeams() });

  protected readonly now = signal(Date.now());
  protected readonly firstName = signal(this.auth.profile()?.firstName ?? '');
  protected readonly lastName = signal(this.auth.profile()?.lastName ?? '');
  protected readonly message = signal<string | null>(null);
  protected readonly saved = signal(false);

  constructor() {
    const timer = setInterval(() => this.now.set(Date.now()), 30_000);
    inject(DestroyRef).onDestroy(() => clearInterval(timer));
  }

  protected open(tab: ProfileTab): void {
    void this.router.navigate([], { queryParams: { tab }, replaceUrl: true });
  }

  protected showNode(nodeNum: number): void {
    void this.router.navigate(['/operations'], { queryParams: { node: nodeNum } });
  }

  protected openTeamChat(teamId: string): void {
    void this.router.navigate(['/operations'], { queryParams: { team: teamId } });
  }

  protected save(): void {
    this.message.set(null);
    this.auth.updateProfile(this.firstName().trim(), this.lastName().trim()).subscribe({
      next: () => {
        this.saved.set(true);
        this.message.set('Saved.');
      },
      error: (error: unknown) => {
        this.saved.set(false);
        this.message.set(describeHttpError(error));
      },
    });
  }

  protected signOut(): void {
    this.auth.signOut();
    void this.router.navigate(['/operations']);
  }
}
