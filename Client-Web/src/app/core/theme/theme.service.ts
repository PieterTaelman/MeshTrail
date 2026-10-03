import { DOCUMENT, Injectable, inject, signal } from '@angular/core';

export type ColorScheme = 'dark' | 'light';

/** Class on <html> that switches Optimus (darkModeSelector) and our own tokens to the dark scheme. */
export const DARK_MODE_CLASS = 'app-dark';
const STORAGE_KEY = 'meshtrail.colorScheme';

/** Dark by default; remembers the user's choice in localStorage. */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly root = inject(DOCUMENT).documentElement;

  readonly scheme = signal<ColorScheme>(readStoredScheme());

  constructor() {
    this.apply(this.scheme());
  }

  toggle(): void {
    const next: ColorScheme = this.scheme() === 'dark' ? 'light' : 'dark';
    this.scheme.set(next);
    this.apply(next);
    try {
      localStorage.setItem(STORAGE_KEY, next);
    } catch {
      // Storage can be blocked (private mode); the choice then simply lasts for this page load.
    }
  }

  private apply(scheme: ColorScheme): void {
    this.root.classList.toggle(DARK_MODE_CLASS, scheme === 'dark');
  }
}

function readStoredScheme(): ColorScheme {
  try {
    return localStorage.getItem(STORAGE_KEY) === 'light' ? 'light' : 'dark';
  } catch {
    return 'dark';
  }
}
