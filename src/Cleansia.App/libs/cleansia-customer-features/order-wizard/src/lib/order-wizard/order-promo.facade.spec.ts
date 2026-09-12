import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { CustomerClient } from '@cleansia/customer-services';
import { of, throwError } from 'rxjs';
import { OrderPromoFacade } from './order-promo.facade';

describe('OrderPromoFacade', () => {
  let facade: OrderPromoFacade;
  let promoCodeClient: { validate: jest.Mock };
  let preSurchargeSubtotal: ReturnType<typeof signal<number>>;
  let currencyId: ReturnType<typeof signal<string | null>>;
  let persistPromoCode: jest.Mock;

  /** `toObservable` delivers through an effect, and the validation settles a tick later. */
  async function settle(): Promise<void> {
    TestBed.flushEffects();
    await Promise.resolve();
    await Promise.resolve();
  }

  function build(): void {
    promoCodeClient = {
      validate: jest.fn().mockReturnValue(of({ isValid: true, discountAmount: 100 })),
    };
    preSurchargeSubtotal = signal(1000);
    currencyId = signal<string | null>('czk');
    persistPromoCode = jest.fn();

    TestBed.configureTestingModule({
      providers: [
        OrderPromoFacade,
        { provide: CustomerClient, useValue: { promoCodeClient } },
      ],
    });

    facade = TestBed.inject(OrderPromoFacade);
    facade.connect({ preSurchargeSubtotal, currencyId, persistPromoCode });
  }

  beforeEach(build);

  describe('setPromoCode', () => {
    it('mirrors the raw promo input and echoes it into the form model', () => {
      facade.setPromoCode('save10');

      expect(facade.promoCode()).toBe('save10');
      expect(persistPromoCode).toHaveBeenCalledWith('save10');
    });
  });

  describe('validatePromoCodeNow', () => {
    it('returns idle and skips the client for an empty code', async () => {
      const state = await facade.validatePromoCodeNow('   ');

      expect(state).toEqual({ kind: 'idle' });
      expect(promoCodeClient.validate).not.toHaveBeenCalled();
    });

    it('resolves to valid, normalizes the code and stores it uppercased', async () => {
      const state = await facade.validatePromoCodeNow('save10');

      expect(promoCodeClient.validate).toHaveBeenCalledTimes(1);
      expect(state).toEqual({ kind: 'valid', discount: 100 });
      expect(facade.promoCodeState()).toEqual({ kind: 'valid', discount: 100 });
      expect(facade.promoCode()).toBe('SAVE10');
      expect(persistPromoCode).toHaveBeenLastCalledWith('SAVE10');
    });

    // CreateOrder.Handler previews the promo against `calc.TotalPrice - calc.ExpressSurchargeAmount`.
    // A surcharge-inclusive base would preview a percentage discount 20% larger than the one the
    // submit applies, and clear a minimum-order floor the submit would fail.
    it('validates against the pre-surcharge subtotal, the base the submit will use', async () => {
      await facade.validatePromoCodeNow('save10');

      expect(promoCodeClient.validate.mock.calls[0][0].orderSubtotal).toBe(1000);
    });

    it('does not compound — a second code is validated against the same untouched base', async () => {
      await facade.validatePromoCodeNow('save10');

      await facade.validatePromoCodeNow('save20');

      expect(promoCodeClient.validate.mock.calls[1][0].orderSubtotal).toBe(1000);
    });

    it('falls back to 0 subtotal before any quote has arrived', async () => {
      preSurchargeSubtotal.set(0);

      await facade.validatePromoCodeNow('save10');

      expect(promoCodeClient.validate.mock.calls[0][0].orderSubtotal).toBe(0);
    });

    it('resolves to invalid when the backend rejects the code', async () => {
      promoCodeClient.validate.mockReturnValue(of({ isValid: false, errorCode: 'promo.expired' }));

      const state = await facade.validatePromoCodeNow('bad');

      expect(state).toEqual({ kind: 'invalid', error: 'promo.expired' });
    });

    it('resolves to invalid on a network error', async () => {
      promoCodeClient.validate.mockReturnValue(throwError(() => new Error('boom')));

      const state = await facade.validatePromoCodeNow('bad');

      expect(state).toEqual({ kind: 'invalid', error: null });
    });
  });

  // A code with a minimum is bound to one currency, and the server answers `CurrencyMismatch` on
  // any other. The preview has to ask in the currency the booking is priced in, which is the
  // quote's — the address's country decides it, and the customer picks nothing.
  describe('the currency the preview asks in', () => {
    it("sends the quote's currency with the code", async () => {
      currencyId.set('eur');

      await facade.validatePromoCodeNow('save10');

      expect(promoCodeClient.validate.mock.calls[0][0].currencyId).toBe('eur');
    });

    it('sends no currency before a quote exists, which the server reads as the default', async () => {
      currencyId.set(null);

      await facade.validatePromoCodeNow('save10');

      expect(promoCodeClient.validate.mock.calls[0][0].currencyId).toBeUndefined();
    });

    it('re-validates an applied code when the quote moves to another currency', async () => {
      await facade.validatePromoCodeNow('save10');
      promoCodeClient.validate.mockReturnValue(
        of({ isValid: false, errorCode: 'CurrencyMismatch' }),
      );

      currencyId.set('eur');
      await settle();

      expect(promoCodeClient.validate).toHaveBeenCalledTimes(2);
      expect(promoCodeClient.validate.mock.calls[1][0]).toMatchObject({
        code: 'SAVE10',
        currencyId: 'eur',
      });
      expect(facade.promoCodeState()).toEqual({ kind: 'invalid', error: 'CurrencyMismatch' });
      expect(facade.effectivePromoDiscount()).toBe(0);
    });

    it('leaves a code that was never applied alone when the currency moves', async () => {
      currencyId.set('eur');
      await settle();

      expect(promoCodeClient.validate).not.toHaveBeenCalled();
      expect(facade.promoCodeState()).toEqual({ kind: 'idle' });
    });

    it('does not re-validate a code the server already refused', async () => {
      promoCodeClient.validate.mockReturnValue(of({ isValid: false, errorCode: 'Expired' }));
      await facade.validatePromoCodeNow('old');

      currencyId.set('eur');
      await settle();

      expect(promoCodeClient.validate).toHaveBeenCalledTimes(1);
    });
  });

  describe('effectivePromoDiscount', () => {
    it('is 0 while idle', () => {
      expect(facade.effectivePromoDiscount()).toBe(0);
    });

    it('reflects the applied valid discount', async () => {
      await facade.validatePromoCodeNow('save10');

      expect(facade.effectivePromoDiscount()).toBe(100);
    });
  });

  describe('clearPromoCode', () => {
    it('resets state and wipes the value', async () => {
      await facade.validatePromoCodeNow('save10');

      facade.clearPromoCode();

      expect(facade.promoCodeState()).toEqual({ kind: 'idle' });
      expect(facade.promoCode()).toBe('');
      expect(facade.effectivePromoDiscount()).toBe(0);
      expect(persistPromoCode).toHaveBeenLastCalledWith('');
    });
  });
});
