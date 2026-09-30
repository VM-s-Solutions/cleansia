import { isPlatformBrowser } from '@angular/common';
import { inject, Injectable, PLATFORM_ID, signal } from '@angular/core';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { catchError, of, takeUntil } from 'rxjs';
import { CustomerClient } from '../client/customer-base-client';
import { CreateSavedCardCheckoutSessionCommand } from '../client/customer-client';

const CARD_SETUP_RETURN_STORAGE_KEY = 'cleansia_card_setup_return';

/** What the card-capture step reads from the screen whose cash booking it guarantees. */
export interface CardCaptureConnection {
  countryId: () => string | null;
  /** Keeps what the customer was doing, so the page they come back to can pick it up. */
  park: () => void;
  /** The page Stripe's return to the profile sends the customer on to. */
  returnUrl: () => string;
}

/**
 * The card a signed-in customer saves as the guarantee for a cash booking, captured through a Stripe
 * Checkout Session in setup mode. Stripe returns every capture to the profile, which sends the customer
 * on to the page remembered here.
 */
@Injectable()
export class CardCaptureFacade extends UnsubscribeControlDirective {
  private readonly customerClient = inject(CustomerClient);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  private deps: CardCaptureConnection | null = null;

  readonly visible = signal(false);
  readonly consentAccepted = signal(false);
  readonly starting = signal(false);

  connect(deps: CardCaptureConnection): void {
    this.deps = deps;
  }

  open(): void {
    this.consentAccepted.set(false);
    this.visible.set(true);
  }

  close(): void {
    this.visible.set(false);
  }

  setConsent(accepted: boolean): void {
    this.consentAccepted.set(accepted);
  }

  start(): void {
    const deps = this.deps;
    if (!deps || !this.consentAccepted() || this.starting()) return;
    const command = new CreateSavedCardCheckoutSessionCommand();
    command.consentAccepted = true;
    command.countryId = deps.countryId() ?? undefined;
    this.starting.set(true);
    this.customerClient.savedCardClient
      .createCheckoutSession(command)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
      )
      .subscribe((response) => {
        if (!response?.checkoutUrl) {
          this.starting.set(false);
          return;
        }
        deps.park();
        if (!this.isBrowser) return;
        rememberCardSetupReturnUrl(deps.returnUrl());
        window.location.href = response.checkoutUrl;
      });
  }
}

/** Where the profile sends the customer on when Stripe returns them from a card capture. */
export function rememberCardSetupReturnUrl(url: string): void {
  try {
    sessionStorage.setItem(CARD_SETUP_RETURN_STORAGE_KEY, url);
  } catch {
    // Storage refused: the return stays on the profile, which still lists the card.
  }
}

/** The page the last card capture started from, read once; null when none was remembered. */
export function takeCardSetupReturnUrl(): string | null {
  try {
    const url = sessionStorage.getItem(CARD_SETUP_RETURN_STORAGE_KEY);
    sessionStorage.removeItem(CARD_SETUP_RETURN_STORAGE_KEY);
    return url;
  } catch {
    return null;
  }
}
