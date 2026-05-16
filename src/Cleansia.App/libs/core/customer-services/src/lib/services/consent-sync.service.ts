import { inject, Injectable } from '@angular/core';
import {
  ConsentType,
  GrantConsentCommand,
  WithdrawConsentCommand,
} from '../client/customer-client';
import { CustomerClient } from '../client/customer-base-client';
import { CustomerAuthService } from './customer-auth.service';
import { CookieConsentStatus, CookiePreferences } from '@cleansia/components';

/**
 * Maps frontend cookie preference categories to backend ConsentType values
 * and syncs consent choices to the GDPR API.
 *
 * Only syncs when the user is authenticated — anonymous consent stays in localStorage only.
 */
@Injectable({ providedIn: 'root' })
export class ConsentSyncService {
  // Always route through the CustomerClient wrapper so requests honour the
  // configured CUSTOMER_API_BASE_URL. Direct sub-client injection uses
  // NSwag's empty-string default baseUrl, which falls through to the SPA's
  // own origin and returns the index.html / 404 page.
  private readonly customerClient = inject(CustomerClient);
  private readonly authService = inject(CustomerAuthService);

  /**
   * Returns a sync function to pass to the cookie consent component's [syncToBackend] input.
   */
  getSyncFn(): (preferences: CookiePreferences, status: CookieConsentStatus) => void {
    return (preferences: CookiePreferences, status: CookieConsentStatus) => {
      this.syncConsent(preferences, status);
    };
  }

  private syncConsent(preferences: CookiePreferences, status: CookieConsentStatus): void {
    if (!this.authService.isLoggedIn()) return;

    const mappings: { key: keyof CookiePreferences; consentType: ConsentType }[] = [
      { key: 'analytics', consentType: ConsentType.DataProcessing },
      { key: 'marketing', consentType: ConsentType.MarketingEmails },
    ];

    for (const { key, consentType } of mappings) {
      const granted = status === 'accepted' || (status === 'custom' && preferences[key]);

      if (granted) {
        this.grantConsent(consentType);
      } else {
        this.withdrawConsent(consentType);
      }
    }
  }

  private grantConsent(consentType: ConsentType): void {
    // IP + user-agent are populated server-side from the request now
    // (legal-audit integrity) — client no longer sends them.
    const command = new GrantConsentCommand({ consentType });

    this.customerClient.gdprClient.consentsPost(command).subscribe({
      error: () => { /* silently ignore — consent is saved locally regardless */ },
    });
  }

  private withdrawConsent(consentType: ConsentType): void {
    const command = new WithdrawConsentCommand({
      consentType,
    });

    this.customerClient.consentsClient.withdraw(command).subscribe({
      error: () => { /* silently ignore */ },
    });
  }
}
