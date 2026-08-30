import { computed, Injectable, signal } from '@angular/core';

export type PromoRequestState = 'idle' | 'sending' | 'unavailable' | 'sent';

/**
 * The "leave your e-mail, get a code for your first clean" capture.
 *
 * There is no endpoint yet. `PromoCode` supports create and validate, but
 * nothing mints a code and mails it to an address that has no account, so this
 * facade reports `unavailable` rather than resolving.
 *
 * That is deliberate: the form this replaces called
 * `showSuccessTranslated('request_sent')` and reset itself, telling every
 * visitor their request had been sent when nothing left the browser. A page may
 * only claim what the system can do.
 *
 * When the backend ticket lands, `request()` calls it and the `unavailable`
 * branch goes; the component does not change.
 */
@Injectable()
export class PromoRequestFacade {
  private readonly _state = signal<PromoRequestState>('idle');
  readonly state = this._state.asReadonly();

  readonly isSending = computed(() => this._state() === 'sending');
  readonly isUnavailable = computed(() => this._state() === 'unavailable');
  readonly isSent = computed(() => this._state() === 'sent');

  request(email: string, consented: boolean): void {
    if (!email || !consented) {
      return;
    }
    this._state.set('sending');
    // No mint-and-send endpoint exists. Report it honestly.
    this._state.set('unavailable');
  }

  reset(): void {
    this._state.set('idle');
  }
}
