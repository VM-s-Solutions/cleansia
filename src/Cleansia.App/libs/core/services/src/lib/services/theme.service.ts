import { isPlatformBrowser } from '@angular/common';
import { Injectable, PLATFORM_ID, inject, signal } from '@angular/core';

export type Theme = 'light' | 'dark';

const STORAGE_KEY = 'cleansia-theme';
const DARK_MODE_CLASS = 'dark-mode';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly platformId = inject(PLATFORM_ID);
  private readonly isBrowser = isPlatformBrowser(this.platformId);

  readonly currentTheme = signal<Theme>(this.loadTheme());

  constructor() {
    if (this.isBrowser) {
      this.applyTheme(this.currentTheme());
    }
  }

  toggleTheme(): void {
    const next: Theme = this.currentTheme() === 'light' ? 'dark' : 'light';
    this.currentTheme.set(next);
    if (this.isBrowser) {
      this.applyTheme(next);
      localStorage.setItem(STORAGE_KEY, next);
    }
  }

  private loadTheme(): Theme {
    if (this.isBrowser) {
      const stored = localStorage.getItem(STORAGE_KEY);
      return stored === 'dark' ? 'dark' : 'light';
    }
    return 'light';
  }

  private applyTheme(theme: Theme): void {
    const el = document.documentElement;
    if (theme === 'dark') {
      el.classList.add(DARK_MODE_CLASS);
      el.style.colorScheme = 'dark';
    } else {
      el.classList.remove(DARK_MODE_CLASS);
      el.style.colorScheme = 'light';
    }
  }
}
