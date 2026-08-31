import { Injectable, signal } from '@angular/core';

/**
 * Lets anything on the page reopen the cookie banner's settings panel.
 *
 * The banner hides itself once a choice is stored, which is correct — but the
 * footer's "Cookie settings" row then had nothing to open, so it was rendered
 * as dead text. A visitor must be able to change a consent decision after
 * making it, and a link that says "settings" has to lead somewhere.
 *
 * A counter rather than a boolean: reopening twice in a row is a real request
 * both times, and a boolean would swallow the second.
 */
@Injectable({ providedIn: 'root' })
export class CookieConsentService {
  private readonly _openRequests = signal(0);

  /** Increments each time something asks for the settings panel. */
  readonly openRequests = this._openRequests.asReadonly();

  openSettings(): void {
    this._openRequests.update((n) => n + 1);
  }
}
