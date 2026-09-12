import { TestBed } from '@angular/core/testing';
import { AdminClient, OrderItem } from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { OrderDetailFacade } from './order-detail.facade';

describe('OrderDetailFacade', () => {
  let facade: OrderDetailFacade;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        OrderDetailFacade,
        { provide: AdminClient, useValue: { adminOrderClient: {} } },
        { provide: SnackbarService, useValue: { showSuccess: jest.fn(), showError: jest.fn() } },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
      ],
    });
    facade = TestBed.inject(OrderDetailFacade);
  });

  it('labels a price with the symbol the order carries', () => {
    facade.order.set(OrderItem.fromJS({ currency: { symbol: '€', code: 'EUR' } }));

    expect(facade.formatPrice(45)).toBe('45.00 €');
  });

  // The order always names its currency; a bare number is honest when it does not, "Kc" is a guess.
  it('prints a bare number rather than a currency the order does not name', () => {
    facade.order.set(OrderItem.fromJS({}));

    expect(facade.formatPrice(45)).toBe('45.00');
    expect(facade.formatPrice(null)).toBe('-');
  });
});
