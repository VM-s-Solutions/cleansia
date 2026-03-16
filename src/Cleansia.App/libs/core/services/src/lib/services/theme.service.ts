import { Injectable, signal } from '@angular/core';

export type Theme = 'light' | 'dark';

const STORAGE_KEY = 'cleansia-theme';
const DARK_MODE_CLASS = 'dark-mode';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  readonly currentTheme = signal<Theme>(this.loadTheme());

  constructor() {
    this.applyTheme(this.currentTheme());
  }

  toggleTheme(): void {
    const next: Theme = this.currentTheme() === 'light' ? 'dark' : 'light';
    this.currentTheme.set(next);
    this.applyTheme(next);
    localStorage.setItem(STORAGE_KEY, next);
  }

  private loadTheme(): Theme {
    const stored = localStorage.getItem(STORAGE_KEY);
    return stored === 'dark' ? 'dark' : 'light';
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
