import { computed, inject, Injectable, signal } from '@angular/core';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import {
  CustomerClient,
  RequestPromoCodeCommand,
} from '@cleansia/customer-services';
import { extractApiErrorCode } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { catchError, of, takeUntil } from 'rxjs';

export type PromoRequestState = 'idle' | 'sending' | 'sent' | 'error';

/** Requests a first-clean promo and reports whether the API accepted it. */
@Injectable()
export class PromoRequestFacade extends UnsubscribeControlDirective {
  private readonly client = inject(CustomerClient);
  private readonly translate = inject(TranslateService);

  private readonly _state = signal<PromoRequestState>('idle');
  readonly state = this._state.asReadonly();
  private readonly _failureMessageKey = signal('pages.home.cta.promo_failed');
  readonly failureMessageKey = this._failureMessageKey.asReadonly();

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
    command.languageCode =
      this.translate.currentLang || this.translate.getDefaultLang();

    this._failureMessageKey.set('pages.home.cta.promo_failed');
    this._state.set('sending');

    this.client.promoCodeClient
      .request(command)
      .pipe(
        catchError((error: unknown) => {
          this._failureMessageKey.set(
            extractApiErrorCode(error) === 'promo.already_sent'
              ? 'api.promo.already_sent'
              : 'pages.home.cta.promo_failed'
          );
          this._state.set('error');
          return of(null);
        }),
        takeUntil(this.destroyed$)
      )
      .subscribe((result) => {
        if (result) {
          this._state.set(result.accepted ? 'sent' : 'error');
        }
      });
  }

  reset(): void {
    this._failureMessageKey.set('pages.home.cta.promo_failed');
    this._state.set('idle');
  }
}
