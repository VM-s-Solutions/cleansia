import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  OnInit,
  output,
  PLATFORM_ID,
  signal,
} from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { isLocalStorageAvailable } from '@cleansia/utils';
import { TranslateModule } from '@ngx-translate/core';
import { ToggleSwitchModule } from 'primeng/toggleswitch';

export interface CookieConsentConfig {
  storageKey?: string;
  showDeclineButton?: boolean;
  policyUrl?: string;
  position?: 'bottom' | 'top';
}

export interface CookiePreferences {
  necessary: boolean;
  analytics: boolean;
  marketing: boolean;
  preferences: boolean;
}

export type CookieConsentStatus = 'pending' | 'accepted' | 'declined' | 'custom';

/**
 * Callback for syncing consent preferences to the backend.
 * Called with the full preferences map whenever consent changes.
 * Implementations should map cookie categories to backend ConsentType values.
 */
export type ConsentSyncFn = (preferences: CookiePreferences, status: CookieConsentStatus) => void;

@Component({
  selector: 'cleansia-cookie-consent',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslateModule, ToggleSwitchModule],
  templateUrl: './cleansia-cookie-consent.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CleansiaCookieConsentComponent implements OnInit {
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  storageKey = input<string>('cleansia-cookie-consent');
  showDeclineButton = input<boolean>(true);
  policyUrl = input<string>('');
  position = input<'bottom' | 'top'>('bottom');

  /**
   * Optional function to sync consent to the backend API.
   * When provided, the component will call this after saving to localStorage.
   */
  syncToBackend = input<ConsentSyncFn | null>(null);

  consentChange = output<CookieConsentStatus>();

  private consentStatus = signal<CookieConsentStatus>('pending');
  isVisible = signal<boolean>(false);
  showSettings = signal<boolean>(false);

  preferences = signal<CookiePreferences>({
    necessary: true,
    analytics: false,
    marketing: false,
    preferences: false,
  });

  hasConsented = computed(() => this.consentStatus() === 'accepted' || this.consentStatus() === 'custom');
  hasDeclined = computed(() => this.consentStatus() === 'declined');

  readonly categories = [
    {
      key: 'necessary' as const,
      icon: 'pi pi-lock',
      locked: true,
    },
    {
      key: 'analytics' as const,
      icon: 'pi pi-chart-bar',
      locked: false,
    },
    {
      key: 'marketing' as const,
      icon: 'pi pi-megaphone',
      locked: false,
    },
    {
      key: 'preferences' as const,
      icon: 'pi pi-sliders-h',
      locked: false,
    },
  ];

  ngOnInit(): void {
    this.checkExistingConsent();
  }

  private checkExistingConsent(): void {
    if (!this.isBrowser) {
      this.consentStatus.set('pending');
      this.isVisible.set(false);
      return;
    }

    const storedConsent = localStorage.getItem(this.storageKey());
    const storedPrefs = localStorage.getItem(`${this.storageKey()}-preferences`);

    if (storedConsent === 'accepted' || storedConsent === 'custom') {
      this.consentStatus.set(storedConsent as CookieConsentStatus);
      this.isVisible.set(false);
      if (storedPrefs) {
        try {
          this.preferences.set({ ...this.preferences(), ...JSON.parse(storedPrefs), necessary: true });
        } catch { /* use defaults */ }
      } else if (storedConsent === 'accepted') {
        this.preferences.set({ necessary: true, analytics: true, marketing: true, preferences: true });
      }
    } else if (storedConsent === 'declined') {
      this.consentStatus.set('declined');
      this.isVisible.set(false);
    } else {
      this.consentStatus.set('pending');
      this.isVisible.set(true);
    }
  }

  acceptAll(): void {
    this.preferences.set({ necessary: true, analytics: true, marketing: true, preferences: true });
    this.setConsent('accepted');
  }

  declineAll(): void {
    this.preferences.set({ necessary: true, analytics: false, marketing: false, preferences: false });
    this.setConsent('declined');
  }

  savePreferences(): void {
    this.setConsent('custom');
  }

  toggleSettings(): void {
    this.showSettings.update(v => !v);
  }

  updatePreference(key: keyof CookiePreferences, value: boolean): void {
    if (key === 'necessary') return;
    this.preferences.update(p => ({ ...p, [key]: value }));
  }

  private setConsent(status: CookieConsentStatus): void {
    this.consentStatus.set(status);
    if (this.isBrowser) {
      localStorage.setItem(this.storageKey(), status);
      localStorage.setItem(`${this.storageKey()}-preferences`, JSON.stringify(this.preferences()));
    }
    this.isVisible.set(false);
    this.showSettings.set(false);
    this.consentChange.emit(status);

    // Sync to backend if callback provided
    const syncFn = this.syncToBackend();
    if (syncFn) {
      syncFn(this.preferences(), status);
    }
  }

  resetConsent(): void {
    if (this.isBrowser) {
      localStorage.removeItem(this.storageKey());
      localStorage.removeItem(`${this.storageKey()}-preferences`);
    }
    this.consentStatus.set('pending');
    this.isVisible.set(true);
    this.showSettings.set(false);
    this.consentChange.emit('pending');
  }

  static getConsentStatus(storageKey = 'cleansia-cookie-consent'): CookieConsentStatus {
    if (!isLocalStorageAvailable()) return 'pending';
    const stored = localStorage.getItem(storageKey);
    if (stored === 'accepted' || stored === 'custom') return stored as CookieConsentStatus;
    if (stored === 'declined') return 'declined';
    return 'pending';
  }

  static hasAcceptedCookies(storageKey = 'cleansia-cookie-consent'): boolean {
    if (!isLocalStorageAvailable()) return false;
    const stored = localStorage.getItem(storageKey);
    return stored === 'accepted' || stored === 'custom';
  }

  static getPreferences(storageKey = 'cleansia-cookie-consent'): CookiePreferences {
    const defaults: CookiePreferences = { necessary: true, analytics: false, marketing: false, preferences: false };
    if (!isLocalStorageAvailable()) return defaults;
    try {
      const stored = localStorage.getItem(`${storageKey}-preferences`);
      return stored ? { ...defaults, ...JSON.parse(stored), necessary: true } : defaults;
    } catch {
      return defaults;
    }
  }
}
