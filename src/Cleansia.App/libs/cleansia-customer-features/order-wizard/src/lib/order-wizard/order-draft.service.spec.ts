import { TestBed } from '@angular/core/testing';
import { OrderDraftService } from './order-draft.service';
import { ORDER_WIZARD_INITIAL_DATA, OrderWizardFormData } from './order-wizard.models';

/**
 * The half of "your unfinished order will be kept" that is code.
 *
 * The Cleansia Plus step has to send an anonymous customer to sign in, because a
 * membership is bought against an account. Without this the trip empties their
 * basket silently — which is worse than not offering Plus at that point at all.
 */
describe('OrderDraftService', () => {
  let service: OrderDraftService;

  function draftWith(overrides: Partial<OrderWizardFormData> = {}): OrderWizardFormData {
    return { ...ORDER_WIZARD_INITIAL_DATA, ...overrides };
  }

  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({ providers: [OrderDraftService] });
    service = TestBed.inject(OrderDraftService);
  });

  afterEach(() => sessionStorage.clear());

  it('gives back what was parked', () => {
    service.park(4, draftWith({ selectedServiceIds: ['s-1'], rooms: 3, customerEmail: 'a@b.test' }));

    const restored = service.take();

    expect(restored?.step).toBe(4);
    expect(restored?.data.selectedServiceIds).toEqual(['s-1']);
    expect(restored?.data.rooms).toBe(3);
    expect(restored?.data.customerEmail).toBe('a@b.test');
  });

  it('gives it back ONCE', () => {
    // A restore offered twice is a basket that reappears after the customer
    // deliberately started over.
    service.park(2, draftWith({ selectedServiceIds: ['s-1'] }));

    expect(service.take()).not.toBeNull();
    expect(service.take()).toBeNull();
  });

  it('rehydrates the cleaning date as a Date', () => {
    // JSON turns it into a string, and everything that guards the wizard's
    // scheduling step compares it as a Date.
    const cleaningDate = new Date(2026, 8, 2, 9, 0, 0, 0);
    service.park(3, draftWith({ cleaningDate }));

    const restored = service.take();

    expect(restored?.data.cleaningDate).toBeInstanceOf(Date);
    expect(restored?.data.cleaningDate?.getTime()).toBe(cleaningDate.getTime());
  });

  it('drops a date it cannot parse rather than restoring an Invalid Date', () => {
    sessionStorage.setItem(
      'cleansia_order_draft',
      JSON.stringify({ savedAt: Date.now(), step: 3, data: { ...ORDER_WIZARD_INITIAL_DATA, cleaningDate: 'not-a-date' } })
    );

    expect(service.take()?.data.cleaningDate).toBeNull();
  });

  it('refuses a draft older than a day', () => {
    // Beyond that it is a stale price, and restoring it silently would show a
    // total the checkout will not honour.
    const twoDaysAgo = Date.now() - 2 * 24 * 60 * 60 * 1000;
    sessionStorage.setItem(
      'cleansia_order_draft',
      JSON.stringify({ savedAt: twoDaysAgo, step: 1, data: ORDER_WIZARD_INITIAL_DATA })
    );

    expect(service.take()).toBeNull();
  });

  it('returns null rather than throwing on a corrupt entry', () => {
    sessionStorage.setItem('cleansia_order_draft', '{ not json');

    expect(() => service.take()).not.toThrow();
    expect(service.take()).toBeNull();
  });

  it('clear() removes a parked draft', () => {
    service.park(1, draftWith());
    service.clear();

    expect(service.take()).toBeNull();
  });

  it('survives storage refusing a write', () => {
    // Private mode and quota both throw on setItem. Losing the basket is bad;
    // taking the page down with it is worse.
    const setItem = jest.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('QuotaExceededError');
    });

    expect(() => service.park(1, draftWith())).not.toThrow();

    setItem.mockRestore();
  });
});
