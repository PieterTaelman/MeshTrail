import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { TagModule } from '@openng/optimus-ui/tag';
import { RealtimeService } from './core/realtime/realtime.service';

/** App shell: header with navigation and live-connection indicator, page content below. */
@Component({
  selector: 'app-root',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, TagModule],
  templateUrl: './app.html',
})
export class App {
  protected readonly realtimeStatus = inject(RealtimeService).status;
}
