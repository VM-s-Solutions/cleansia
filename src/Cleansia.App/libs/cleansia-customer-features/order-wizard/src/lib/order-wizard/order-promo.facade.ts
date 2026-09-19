import { computed, inject, Injectable, Injector, Signal, signal } from '@angular/core';
import { toObservable } from '@angular/core/rxjs-interop';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import {
  CustomerClient,
  ValidatePromoCodeCommand,
} from '@cleansia/customer-services';
import { catchError, distinctUntilChanged, of, takeUntil } from 'rxjs';
import { PromoCodeUiState } from './order-wizard.models';

/** Dependencies the promo engine reads from the orchestrating wizard facade. */
interface PromoConnection {
  /**
   * Pre-surcharge subtotal from the live quote — the base `CreateOrder.Handler` previews the promo
   * against, and the only base whose verdict the submit reproduces.
   */
  preSurchargeSubtotal: Signal<number>;
  /**
   * The currency the live quote is priced in — the service address's country's, which is the one
   * `CreateOrder` previews the promo in. Null before a quote exists, which the server reads as the
   * platform default.
   */
  currencyId: Signal<string | null>;
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
  private readonly injector = inject(Injector);

  private deps: PromoConnection | null = null;

  promoCode = signal('');
  promoCodeState = signal<PromoCodeUiState>({ kind: 'idle' });
  /** The currency the applied code was previewed in — the only one its verdict is known for. */
  private validatedInCurrencyId: string | null = null;

  /** Promo discount the user just applied via the dialog (client-side validation). */
  readonly effectivePromoDiscount = computed(() => {
    const state = this.promoCodeState();
    return state.kind === 'valid' ? state.discount : 0;
  });

  connect(deps: PromoConnection): void {
    this.deps = deps;
    // A code bound to one currency is refused in any other, so an applied code is only known to
    // hold for the currency it was previewed in. When the address moves the booking to another,
    // the same code is asked again — and a refusal clears the discount the summary was showing.
    toObservable(deps.currencyId, { injector: this.injector })
      .pipe(distinctUntilChanged(), takeUntil(this.destroyed$))
      .subscribe((currencyId) => {
        if (this.promoCodeState().kind === 'valid' && currencyId !== this.validatedInCurrencyId) {
          void this.validatePromoCodeNow(this.promoCode());
        }
      });
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
    // Validate against the RAW pre-surcharge subtotal. `CreateOrder.Handler` previews the promo
    // against `calc.TotalPrice - calc.ExpressSurchargeAmount`, so a surcharge-inclusive base returns
    // a percentage discount a fifth larger than the submit applies, and clears a minimum-order floor
    // the submit then fails. It is also the only base that does not move once a code is applied —
    // feeding the discounted total back in compounded the discount on every re-apply.
    const subtotal = this.deps?.preSurchargeSubtotal() ?? 0;
    const currencyId = this.deps?.currencyId() ?? null;
    const command = new ValidatePromoCodeCommand();
    command.code = normalized;
    command.orderSubtotal = subtotal;
    command.currencyId = currencyId ?? undefined;

    return new Promise<PromoCodeUiState>((resolve) => {
      this.customerClient.promoCodeClient
        .validate(command)
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
            this.validatedInCurrencyId = currencyId;
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
