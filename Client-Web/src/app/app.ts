import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { ButtonModule } from '@openng/optimus-ui/button';
import { ThLargeIcon } from '@openng/optimus-ui/icons/thlarge';
import { RealtimeService } from './core/realtime/realtime.service';
import { ThemeService } from './core/theme/theme.service';

/** One entry in the sidebar. */
interface NavItem {
  label: string;
  link: string;
}

/** App shell: sidebar navigation, a status bar on top and the page content on a dotted canvas. */
@Component({
  selector: 'app-root',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, RouterOutlet, RouterLink, RouterLinkActive, ButtonModule, ThLargeIcon],
  templateUrl: './app.html',
})
export class App {
  protected readonly realtimeStatus = inject(RealtimeService).status;
  protected readonly theme = inject(ThemeService);

  protected readonly navigation: NavItem[] = [
    { label: 'Operations', link: '/operations' },
    { label: 'Samples', link: '/samples' },
  ];

  /** UTC clock in the status bar, ticking every second. */
  protected readonly now = signal(new Date());

  constructor() {
    const timer = setInterval(() => this.now.set(new Date()), 1000);
    inject(DestroyRef).onDestroy(() => clearInterval(timer));
  }
}
