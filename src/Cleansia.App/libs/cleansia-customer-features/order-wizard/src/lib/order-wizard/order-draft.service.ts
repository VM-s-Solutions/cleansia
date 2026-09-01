import { isPlatformBrowser } from '@angular/common';
import { inject, Injectable, PLATFORM_ID } from '@angular/core';
import { OrderWizardFormData } from './order-wizard.models';

const STORAGE_KEY = 'cleansia_order_draft';

/** How long a parked basket is worth restoring. Beyond a day it is a stale price. */
const MAX_AGE_MS = 24 * 60 * 60 * 1000;

interface ParkedDraft {
  savedAt: number;
  step: number;
  data: OrderWizardFormData;
}

/**
 * Keeps a half-finished booking across a trip to sign-in.
 *
 * Cleansia Plus is bought against an account, so the Plus step has to send an
 * anonymous customer to log in or register — and without this, that trip empties
 * their basket silently. The step says the order will still be here; this is the
 * part that makes that true.
 *
 * `sessionStorage`, not `localStorage`: the draft holds a street address, a phone
 * number and an email, and it only has to survive a navigation inside the same
 * tab. Closing the tab is the customer walking away, and it takes the data with
 * them. Same defensive shape as {@link SignupConsentService} — every access
 * try/caught and platform-guarded, because storage can refuse (private mode,
 * quota) and losing a basket must never be able to break the page holding it.
 *
 * It is NOT a resume-anywhere feature: one tab, one day, and the catalogue is
 * re-read on restore so a price cannot come back from the dead.
 */
@Injectable({ providedIn: 'root' })
export class OrderDraftService {
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  park(step: number, data: OrderWizardFormData): void {
    if (!this.isBrowser) return;
    try {
      const draft: ParkedDraft = { savedAt: Date.now(), step, data };
      sessionStorage.setItem(STORAGE_KEY, JSON.stringify(draft));
    } catch {
      // Storage refused. The trip to sign-in will cost the basket, which is bad;
      // throwing here would cost the page, which is worse.
    }
  }

  /**
   * The parked basket, or null. Reading CONSUMES it: a restore that is offered
   * twice is a basket that reappears after the customer deliberately started
   * over.
   */
  take(): { step: number; data: OrderWizardFormData } | null {
    if (!this.isBrowser) return null;
    try {
      const raw = sessionStorage.getItem(STORAGE_KEY);
      if (!raw) return null;
      sessionStorage.removeItem(STORAGE_KEY);

      const draft = JSON.parse(raw) as ParkedDraft;
      if (!draft?.data || typeof draft.savedAt !== 'number') return null;
      if (Date.now() - draft.savedAt > MAX_AGE_MS) return null;

      // A date survives JSON as a string, and the wizard's guards compare it as
      // a Date. Rehydrating it here keeps that knowledge in one place.
      const cleaningDate = draft.data.cleaningDate
        ? new Date(draft.data.cleaningDate)
        : null;
      const usable = cleaningDate && !Number.isNaN(cleaningDate.getTime());

      return {
        step: draft.step,
        data: { ...draft.data, cleaningDate: usable ? cleaningDate : null },
      };
    } catch {
      return null;
    }
  }

  clear(): void {
    if (!this.isBrowser) return;
    try {
      sessionStorage.removeItem(STORAGE_KEY);
    } catch {
      // Same contract as park().
    }
  }
}
