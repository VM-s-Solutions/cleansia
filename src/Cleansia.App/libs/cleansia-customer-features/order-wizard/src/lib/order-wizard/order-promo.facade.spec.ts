import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { CustomerClient } from '@cleansia/customer-services';
import { of, throwError } from 'rxjs';
import { OrderPromoFacade } from './order-promo.facade';

describe('OrderPromoFacade', () => {
  let facade: OrderPromoFacade;
  let promoCodeClient: { validate: jest.Mock };
  let preSurchargeSubtotal: ReturnType<typeof signal<number>>;
  let persistPromoCode: jest.Mock;

  function build(): void {
    promoCodeClient = {
      validate: jest.fn().mockReturnValue(of({ isValid: true, discountAmount: 100 })),
    };
    preSurchargeSubtotal = signal(1000);
    persistPromoCode = jest.fn();

    TestBed.configureTestingModule({
      providers: [
        OrderPromoFacade,
        { provide: CustomerClient, useValue: { promoCodeClient } },
      ],
    });

    facade = TestBed.inject(OrderPromoFacade);
    facade.connect({ preSurchargeSubtotal, persistPromoCode });
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
