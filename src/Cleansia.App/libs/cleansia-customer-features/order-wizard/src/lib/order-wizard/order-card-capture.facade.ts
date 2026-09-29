import { isPlatformBrowser } from '@angular/common';
import { inject, Injectable, PLATFORM_ID, signal } from '@angular/core';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { CreateSavedCardCheckoutSessionCommand, CustomerClient } from '@cleansia/customer-services';
import { catchError, of, takeUntil } from 'rxjs';
import { OrderDraftService } from './order-draft.service';
import { OrderWizardFormData } from './order-wizard.models';

/** Dependencies the card-capture step reads from the orchestrating wizard facade. */
interface CardCaptureConnection {
  countryId: () => string | null;
  snapshot: () => { step: number; data: OrderWizardFormData };
}

/**
 * The card a signed-in customer saves as the guarantee for a cash booking, captured through a Stripe
 * Checkout Session in setup mode. Stripe returns the customer to the profile, which sends them back to
 * the booking parked here.
 */
@Injectable()
export class OrderCardCaptureFacade extends UnsubscribeControlDirective {
  private readonly customerClient = inject(CustomerClient);
  private readonly draft = inject(OrderDraftService);
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
        const { step, data } = deps.snapshot();
        this.draft.park(step, data);
        if (this.isBrowser) window.location.href = response.checkoutUrl;
      });
  }
}
