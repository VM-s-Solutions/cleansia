import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { CustomerClient } from '@cleansia/customer-services';
import { of, throwError } from 'rxjs';
import { OrderPromoFacade } from './order-promo.facade';

describe('OrderPromoFacade', () => {
  let facade: OrderPromoFacade;
  let promoCodeClient: { validate: jest.Mock };
  let displayedTotalPrice: ReturnType<typeof signal<number | null | undefined>>;
  let persistPromoCode: jest.Mock;

  function build(): void {
    promoCodeClient = {
      validate: jest.fn().mockReturnValue(of({ isValid: true, discountAmount: 100 })),
    };
    displayedTotalPrice = signal<number | null | undefined>(1200);
    persistPromoCode = jest.fn();

    TestBed.configureTestingModule({
      providers: [
        OrderPromoFacade,
        { provide: CustomerClient, useValue: { promoCodeClient } },
      ],
    });

    facade = TestBed.inject(OrderPromoFacade);
    facade.connect({ displayedTotalPrice, persistPromoCode });
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

    it('validates against the displayed (post-surcharge) total', async () => {
      await facade.validatePromoCodeNow('save10');

      const command = promoCodeClient.validate.mock.calls[0][0];
      expect(command.orderSubtotal).toBe(1200);
    });

    it('falls back to 0 subtotal when the displayed total is null', async () => {
      displayedTotalPrice.set(null);

      await facade.validatePromoCodeNow('save10');

      const command = promoCodeClient.validate.mock.calls[0][0];
      expect(command.orderSubtotal).toBe(0);
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
