import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { CustomerClient } from '@cleansia/customer-services';
import { of, throwError } from 'rxjs';
import { OrderPromoFacade } from './order-promo.facade';

describe('OrderPromoFacade', () => {
  let facade: OrderPromoFacade;
  let promoCodeClient: { validate: jest.Mock };
  let referralClient: { validate: jest.Mock };
  let displayedTotalPrice: ReturnType<typeof signal<number | null | undefined>>;
  let persistPromoCode: jest.Mock;
  let persistReferralCode: jest.Mock;

  function build(): void {
    promoCodeClient = {
      validate: jest.fn().mockReturnValue(of({ isValid: true, discountAmount: 100 })),
    };
    referralClient = {
      validate: jest.fn().mockReturnValue(of({ isValid: true, referrerFirstName: 'Pat' })),
    };
    displayedTotalPrice = signal<number | null | undefined>(1200);
    persistPromoCode = jest.fn();
    persistReferralCode = jest.fn();

    TestBed.configureTestingModule({
      providers: [
        OrderPromoFacade,
        { provide: CustomerClient, useValue: { promoCodeClient, referralClient } },
      ],
    });

    facade = TestBed.inject(OrderPromoFacade);
    facade.connect({ displayedTotalPrice, persistPromoCode, persistReferralCode });
  }

  beforeEach(build);

  describe('setPromoCode / setReferralCode', () => {
    it('mirrors the raw promo input and echoes it into the form model', () => {
      facade.setPromoCode('save10');

      expect(facade.promoCode()).toBe('save10');
      expect(persistPromoCode).toHaveBeenCalledWith('save10');
    });

    it('mirrors the raw referral input and echoes it into the form model', () => {
      facade.setReferralCode('friend');

      expect(facade.referralCode()).toBe('friend');
      expect(persistReferralCode).toHaveBeenCalledWith('friend');
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

  describe('validateReferralCodeNow', () => {
    it('returns idle and skips the client for an empty code', async () => {
      const state = await facade.validateReferralCodeNow('');

      expect(state).toEqual({ kind: 'idle' });
      expect(referralClient.validate).not.toHaveBeenCalled();
    });

    it('resolves to valid and tracks the referrer first name', async () => {
      const state = await facade.validateReferralCodeNow('friend');

      expect(state).toEqual({ kind: 'valid', referrerFirstName: 'Pat' });
      expect(facade.referralCode()).toBe('FRIEND');
    });

    it('resolves to invalid when the backend rejects the code', async () => {
      referralClient.validate.mockReturnValue(of({ isValid: false, errorCode: 'referral.self' }));

      const state = await facade.validateReferralCodeNow('bad');

      expect(state).toEqual({ kind: 'invalid', error: 'referral.self' });
    });

    it('resolves to invalid on a network error', async () => {
      referralClient.validate.mockReturnValue(throwError(() => new Error('boom')));

      const state = await facade.validateReferralCodeNow('x');

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

  describe('clearPromoCode / clearReferralCode', () => {
    it('clearPromoCode resets state and wipes the value', async () => {
      await facade.validatePromoCodeNow('save10');

      facade.clearPromoCode();

      expect(facade.promoCodeState()).toEqual({ kind: 'idle' });
      expect(facade.promoCode()).toBe('');
      expect(facade.effectivePromoDiscount()).toBe(0);
      expect(persistPromoCode).toHaveBeenLastCalledWith('');
    });

    it('clearReferralCode resets state and wipes the value', async () => {
      await facade.validateReferralCodeNow('friend');

      facade.clearReferralCode();

      expect(facade.referralState()).toEqual({ kind: 'idle' });
      expect(facade.referralCode()).toBe('');
      expect(persistReferralCode).toHaveBeenLastCalledWith('');
    });
  });
});
