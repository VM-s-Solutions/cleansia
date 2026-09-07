import { computed, inject, Injectable, signal } from '@angular/core';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { CustomerClient, RequestPromoCodeCommand } from '@cleansia/customer-services';
import { TranslateService } from '@ngx-translate/core';
import { catchError, of, takeUntil } from 'rxjs';

export type PromoRequestState = 'idle' | 'sending' | 'sent' | 'error';

/**
 * The "leave your e-mail, get a code for your first clean" capture.
 *
 * The form this replaces called `showSuccessTranslated('request_sent')` and
 * reset itself, telling every visitor their request had been sent when nothing
 * left the browser. A page may only claim what the system can do — so every
 * state here corresponds to something the server actually reported.
 *
 * `sent` deliberately does not mean "a code was minted". The endpoint answers
 * the same way for an address it has seen before, because answering differently
 * would turn the footer of a public website into an account-existence probe.
 * What we can honestly say is that the request was accepted.
 */
@Injectable()
export class PromoRequestFacade extends UnsubscribeControlDirective {
  private readonly client = inject(CustomerClient);
  private readonly translate = inject(TranslateService);

  private readonly _state = signal<PromoRequestState>('idle');
  readonly state = this._state.asReadonly();

  readonly isSending = computed(() => this._state() === 'sending');
  readonly isSent = computed(() => this._state() === 'sent');
  readonly hasFailed = computed(() => this._state() === 'error');

  request(email: string, consented: boolean): void {
    if (!email || !consented || this.isSending()) {
      return;
    }

    const command = new RequestPromoCodeCommand();
    command.email = email;
    // The e-mail is rendered in five locales; send the one the visitor is
    // reading the site in rather than letting the backend default to English.
    command.languageCode = this.translate.currentLang || this.translate.getDefaultLang();

    this._state.set('sending');

    this.client.promoCodeClient
      .request(command)
      .pipe(
        catchError(() => {
          this._state.set('error');
          return of(null);
        }),
        takeUntil(this.destroyed$),
      )
      .subscribe((result) => {
        if (result) {
          this._state.set(result.accepted ? 'sent' : 'error');
        }
      });
  }

  reset(): void {
    this._state.set('idle');
  }
}
