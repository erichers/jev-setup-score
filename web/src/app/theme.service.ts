import { Injectable, effect, signal } from '@angular/core';

const KEY = 'jev-theme';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  readonly theme = signal<'light' | 'dark'>(this.read());
  private persist = false;

  constructor() {
    effect(() => {
      const theme = this.theme();
      document.documentElement.dataset['theme'] = theme;
      document.documentElement.style.colorScheme = theme;
      document.querySelector('meta[name="theme-color"]')?.setAttribute('content', theme === 'dark' ? '#0b1220' : '#f6f8fa');
      if (this.persist) {
        localStorage.setItem(KEY, theme);
      }
    });

    const media = window.matchMedia('(prefers-color-scheme: dark)');
    media.addEventListener('change', () => {
      if (!localStorage.getItem(KEY)) {
        this.theme.set(media.matches ? 'dark' : 'light');
      }
    });
  }

  toggle(): void {
    this.persist = true;
    this.theme.update((theme) => (theme === 'dark' ? 'light' : 'dark'));
  }

  private read(): 'light' | 'dark' {
    const stored = localStorage.getItem(KEY);
    if (stored === 'light' || stored === 'dark') {
      return stored;
    }
    return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
  }
}
