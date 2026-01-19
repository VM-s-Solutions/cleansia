import { CommonModule } from '@angular/common';
import {
  Component,
  computed,
  input,
  OnInit,
  output,
  signal,
} from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';

export interface CookieConsentConfig {
  /** Storage key for remembering consent */
  storageKey?: string;
  /** Whether to show the decline button */
  showDeclineButton?: boolean;
  /** URL for the cookie policy page */
  policyUrl?: string;
  /** Position of the banner: 'bottom' or 'top' */
  position?: 'bottom' | 'top';
}

export type CookieConsentStatus = 'pending' | 'accepted' | 'declined';

/**
 * Cleansia Cookie Consent Component
 *
 * A GDPR-compliant cookie consent banner that can be used across all apps.
 * Stores user consent in localStorage and emits events when consent changes.
 *
 * @example
 * <cleansia-cookie-consent
 *   [storageKey]="'app-cookie-consent'"
 *   [showDeclineButton]="true"
 *   [policyUrl]="'/cookie-policy'"
 *   (consentChange)="onConsentChange($event)"
 * />
 */
@Component({
  selector: 'cleansia-cookie-consent',
  standalone: true,
  imports: [CommonModule, TranslateModule],
  templateUrl: './cleansia-cookie-consent.component.html',
})
export class CleansiaCookieConsentComponent implements OnInit {
  /** Storage key for remembering consent (default: 'cleansia-cookie-consent') */
  storageKey = input<string>('cleansia-cookie-consent');

  /** Whether to show the decline button (default: true) */
  showDeclineButton = input<boolean>(true);

  /** URL for the cookie policy page */
  policyUrl = input<string>('');

  /** Position of the banner: 'bottom' or 'top' (default: 'bottom') */
  position = input<'bottom' | 'top'>('bottom');

  /** Emits when consent status changes */
  consentChange = output<CookieConsentStatus>();

  /** Internal state */
  private consentStatus = signal<CookieConsentStatus>('pending');
  isVisible = signal<boolean>(false);

  /** Whether consent has been given */
  hasConsented = computed(() => this.consentStatus() === 'accepted');

  /** Whether consent has been declined */
  hasDeclined = computed(() => this.consentStatus() === 'declined');

  ngOnInit(): void {
    this.checkExistingConsent();
  }

  private checkExistingConsent(): void {
    const storedConsent = localStorage.getItem(this.storageKey());

    if (storedConsent === 'accepted') {
      this.consentStatus.set('accepted');
      this.isVisible.set(false);
    } else if (storedConsent === 'declined') {
      this.consentStatus.set('declined');
      this.isVisible.set(false);
    } else {
      this.consentStatus.set('pending');
      this.isVisible.set(true);
    }
  }

  acceptCookies(): void {
    this.setConsent('accepted');
  }

  declineCookies(): void {
    this.setConsent('declined');
  }

  private setConsent(status: CookieConsentStatus): void {
    this.consentStatus.set(status);
    localStorage.setItem(this.storageKey(), status);
    this.isVisible.set(false);
    this.consentChange.emit(status);
  }

  /** Reset consent (useful for testing or settings page) */
  resetConsent(): void {
    localStorage.removeItem(this.storageKey());
    this.consentStatus.set('pending');
    this.isVisible.set(true);
    this.consentChange.emit('pending');
  }

  /** Static method to check consent status from outside the component */
  static getConsentStatus(storageKey = 'cleansia-cookie-consent'): CookieConsentStatus {
    const storedConsent = localStorage.getItem(storageKey);
    if (storedConsent === 'accepted') return 'accepted';
    if (storedConsent === 'declined') return 'declined';
    return 'pending';
  }

  /** Static method to check if cookies are accepted */
  static hasAcceptedCookies(storageKey = 'cleansia-cookie-consent'): boolean {
    return localStorage.getItem(storageKey) === 'accepted';
  }
}
