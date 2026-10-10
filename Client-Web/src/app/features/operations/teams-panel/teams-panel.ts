import { ChangeDetectionStrategy, Component, inject, input, output, signal } from '@angular/core';
import { ButtonModule } from '@openng/optimus-ui/button';
import { TimesIcon } from '@openng/optimus-ui/icons/times';
import { InputTextModule } from '@openng/optimus-ui/inputtext';
import { MessageModule } from '@openng/optimus-ui/message';
import { Observable } from 'rxjs';
import { describeHttpError } from '../../../core/api/problem-details';
import { MeshApi } from '../mesh.api';
import { Team } from '../mesh.models';

/** The firmware allows channel names of at most 11 characters. */
const CHANNEL_MAX_LENGTH = 11;

/**
 * "Teams": the teams you are in (channel, join code, members, whether a gateway carries the channel), and forms to
 * create a team on its own channel or join one with a code. Team chat itself lives in the chat drawer.
 */
@Component({
  selector: 'app-teams-panel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ButtonModule, InputTextModule, MessageModule, TimesIcon],
  templateUrl: './teams-panel.html',
  host: { class: 'block' },
})
export class TeamsPanel {
  private readonly api = inject(MeshApi);

  readonly teams = input<Team[]>([]);
  /** False when the panel is a page section (profile) instead of a side panel. */
  readonly closable = input(true);
  readonly closed = output<void>();
  /** Something changed (created, joined, left, new code): the parent reloads the teams. */
  readonly changed = output<void>();
  /** The user wants to chat with this team. */
  readonly openChat = output<string>();

  protected readonly channelMaxLength = CHANNEL_MAX_LENGTH;
  protected readonly name = signal('');
  protected readonly channel = signal('');
  protected readonly code = signal('');
  protected readonly confirmingLeave = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly copied = signal<string | null>(null);

  protected create(): void {
    this.run(this.api.createTeam(this.name().trim(), this.channel().trim()), () => {
      this.name.set('');
      this.channel.set('');
    });
  }

  protected join(): void {
    this.run(this.api.joinTeam(this.code().trim()), () => this.code.set(''));
  }

  protected renewCode(team: Team): void {
    this.run(this.api.renewJoinCode(team.id), () => undefined);
  }

  protected leave(team: Team): void {
    if (this.confirmingLeave() !== team.id) {
      this.confirmingLeave.set(team.id);
      return;
    }
    this.confirmingLeave.set(null);
    this.run(this.api.leaveTeam(team.id), () => undefined);
  }

  protected copy(team: Team): void {
    void navigator.clipboard?.writeText(team.joinCode).then(() => this.copied.set(team.id));
  }

  private run<T>(request: Observable<T>, done: () => void): void {
    this.busy.set(true);
    this.error.set(null);
    request.subscribe({
      next: () => {
        this.busy.set(false);
        done();
        this.changed.emit();
      },
      error: (error: unknown) => {
        this.busy.set(false);
        this.error.set(describeHttpError(error));
      },
    });
  }
}
