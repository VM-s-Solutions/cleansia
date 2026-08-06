import { isPlatformBrowser } from '@angular/common';
import { inject, Injectable, PLATFORM_ID } from '@angular/core';
import { extractApiErrorCode } from '@cleansia/services';
import { catchError, of } from 'rxjs';
import { CustomerClient } from '../client/customer-base-client';
import { ConsentType, GrantConsentCommand } from '../client/customer-client';

const STORAGE_KEY = 'cleansia_signup_consent';
const CONSENT_ALREADY_GRANTED = 'gdpr.consent_already_granted';

/** The signup tick names both documents by title, so it is two consent records. */
export const SIGNUP_CONSENT_TYPES: readonly ConsentType[] = [
  ConsentType.TermsOfService,
  ConsentType.PrivacyPolicy,
];

interface PendingSignupConsent {
  email: string;
  types: ConsentType[];
}

function normalizeEmail(email: string): string {
  return email.trim().toLowerCase();
}

/**
 * Carries the consent ticked at signup to the first session that can record it:
 * the tick happens before any account is signed in and `GrantConsent` needs an
 * authenticated caller. A tick that cannot be delivered yet is kept and retried
 * at every later session start rather than dropped.
 */
@Injectable({ providedIn: 'root' })
export class SignupConsentService {
  private readonly customerClient = inject(CustomerClient);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  record(email: string): void {
    this.write({
      email: normalizeEmail(email),
      types: [...SIGNUP_CONSENT_TYPES],
    });
  }

  flush(email: string | null | undefined): void {
    try {
      const pending = this.read();
      if (!pending || !email || pending.email !== normalizeEmail(email)) return;

      this.customerClient.gdprClient
        .consentsGet()
        .pipe(catchError(() => of(null)))
        .subscribe((consents) => {
          if (!consents) return;
          const answered = new Set(consents.map((c) => c.consentType));
          for (const type of pending.types) {
            if (answered.has(type)) {
              this.settle(type);
            } else {
              this.grant(type);
            }
          }
        });
    } catch {
      // Sign-in must not be breakable by the bookkeeping call that rides it.
    }
  }

  private grant(type: ConsentType): void {
    const command = new GrantConsentCommand();
    command.consentType = type;

    this.customerClient.gdprClient.consentsPost(command).subscribe({
      next: () => this.settle(type),
      error: (error: unknown) => {
        // The endpoint refuses a consent already on file, and the record it
        // refuses to duplicate is the record we came to write.
        if (extractApiErrorCode(error) === CONSENT_ALREADY_GRANTED) {
          this.settle(type);
        }
      },
    });
  }

  private settle(type: ConsentType): void {
    const pending = this.read();
    if (!pending) return;

    const remaining = pending.types.filter((pendingType) => pendingType !== type);
    if (remaining.length) {
      this.write({ ...pending, types: remaining });
    } else {
      this.remove();
    }
  }

  private read(): PendingSignupConsent | null {
    if (!this.isBrowser) return null;
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      const pending = raw ? (JSON.parse(raw) as PendingSignupConsent) : null;
      return pending?.email && pending.types?.length ? pending : null;
    } catch {
      return null;
    }
  }

  private write(pending: PendingSignupConsent): void {
    if (!this.isBrowser) return;
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(pending));
    } catch {
      // Storage can refuse (private mode, quota). Losing the record loses the
      // consent, but failing the signup over it would be worse.
    }
  }

  private remove(): void {
    if (!this.isBrowser) return;
    try {
      localStorage.removeItem(STORAGE_KEY);
    } catch {
      // Same contract as write().
    }
  }
}
