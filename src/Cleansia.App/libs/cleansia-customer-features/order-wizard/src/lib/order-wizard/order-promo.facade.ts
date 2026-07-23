import { computed, inject, Injectable, Signal, signal } from '@angular/core';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import {
  CustomerClient,
  ValidatePromoCodeCommand,
} from '@cleansia/customer-services';
import { catchError, of, takeUntil } from 'rxjs';
import { PromoCodeUiState } from './order-wizard.models';

/** Dependencies the promo engine reads from the orchestrating wizard facade. */
interface PromoConnection {
  /** Post-surcharge subtotal the user is actually charged — promo validates against this. */
  displayedTotalPrice: Signal<number | null | undefined>;
  /** Echoes the raw promo input into the wizard form model. */
  persistPromoCode: (value: string) => void;
}

/**
 * Promo-code validation for the booking wizard.
 *
 * Wolt-style: the summary step shows a tappable row that opens a modal which
 * calls `validatePromoCodeNow` exactly once on Apply (no debounced
 * auto-validation). Backend re-validates server-side at order-create time, so
 * this state machine is purely a UX optimization (instant green-check / red-X
 * feedback). The orchestrating facade owns the form model and connects the echo
 * callback in via [connect].
 */
@Injectable()
export class OrderPromoFacade extends UnsubscribeControlDirective {
  private readonly customerClient = inject(CustomerClient);

  private deps: PromoConnection | null = null;

  promoCode = signal('');
  promoCodeState = signal<PromoCodeUiState>({ kind: 'idle' });

  /** Promo discount the user just applied via the dialog (client-side validation). */
  readonly effectivePromoDiscount = computed(() => {
    const state = this.promoCodeState();
    return state.kind === 'valid' ? state.discount : 0;
  });

  connect(deps: PromoConnection): void {
    this.deps = deps;
  }

  setPromoCode(value: string): void {
    this.promoCode.set(value);
    this.deps?.persistPromoCode(value);
  }

  /**
   * Apply-button handler from the promo dialog. Single backend call, no
   * debounce. Empty input resets to idle without touching the network.
   * Returns the resolved state so the dialog can react to it.
   */
  validatePromoCodeNow(code: string): Promise<PromoCodeUiState> {
    const normalized = code.trim().toUpperCase();
    if (!normalized) {
      this.promoCodeState.set({ kind: 'idle' });
      this.setPromoCode('');
      return Promise.resolve({ kind: 'idle' });
    }
    this.promoCodeState.set({ kind: 'validating' });
    // Validate against the price the user is actually charged — backend's
    // CreateOrder.Handler resolves promo discounts against `finalTotalPrice`
    // (post-express-surcharge), so a bare-subtotal validation could fail a
    // min-order threshold that would otherwise pass on the real charge.
    const subtotal = this.deps?.displayedTotalPrice() ?? 0;
    return new Promise<PromoCodeUiState>((resolve) => {
      this.customerClient.promoCodeClient
        .validate(
          new ValidatePromoCodeCommand({
            code: normalized,
            orderSubtotal: subtotal,
          }),
        )
        .pipe(
          takeUntil(this.destroyed$),
          catchError(() => of(null)),
        )
        .subscribe((resp) => {
          const newState: PromoCodeUiState =
            resp && resp.isValid && resp.discountAmount != null
              ? { kind: 'valid', discount: resp.discountAmount }
              : { kind: 'invalid', error: resp?.errorCode ?? null };
          this.promoCodeState.set(newState);
          if (newState.kind === 'valid') {
            this.setPromoCode(normalized);
          }
          resolve(newState);
        });
    });
  }

  /** Wipes the applied promo state — used by the row's clear-X button. */
  clearPromoCode(): void {
    this.setPromoCode('');
    this.promoCodeState.set({ kind: 'idle' });
  }
}
