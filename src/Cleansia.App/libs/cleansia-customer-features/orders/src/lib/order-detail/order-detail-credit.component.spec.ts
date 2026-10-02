import { Component, input, PLATFORM_ID } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, provideRouter } from '@angular/router';
import {
  CustomerAuthService,
  CustomerClient,
  OrderItem,
  OrderStatus,
  PaymentStatus,
  PaymentType,
} from '@cleansia/customer-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateLoader, TranslateModule, TranslateService } from '@ngx-translate/core';
import { of, Subject } from 'rxjs';
import { OrderPreferredOfferComponent } from './components/order-preferred-offer.component';
import { OrderDetailComponent } from './order-detail.component';
import { OrderMarketFacade } from '../order-market.facade';
import { OrderPreferredOfferFacade } from './order-preferred-offer.facade';

const ORDER_ID = 'ord-1';

const COPY = {
  pages: {
    order: { summary: { due_on_card: 'To pay by card' } },
    order_detail: { paid_with_credit: 'Paid with credit', paid_by_card: 'Paid by card' },
  },
};

@Component({ selector: 'cleansia-customer-order-preferred-offer', standalone: true, template: '' })
class PreferredOfferStub {
  facade = input<unknown>();
  locale = input<string>();
}

function order(paymentStatus: PaymentStatus, creditAppliedAmount: number): OrderItem {
  return OrderItem.fromJS({
    id: ORDER_ID,
    displayOrderNumber: 'ORD-1',
    orderStatus: { value: OrderStatus.Confirmed, name: 'Confirmed' },
    paymentType: { value: PaymentType.Card, name: 'Card' },
    paymentStatus: { value: paymentStatus, name: PaymentStatus[paymentStatus] },
    cleaningDateTime: '2026-09-25T08:00:00Z',
    originalSubtotal: 2000,
    totalPrice: 2000,
    creditAppliedAmount,
    amountDueOnCard: 2000 - creditAppliedAmount,
    currency: { code: 'CZK' },
    selectedServices: [{ id: 's-1', name: 'Standard cleaning' }],
    selectedPackages: [],
  });
}

/**
 * How a booking that spent credit is paid. Credit is a tender, not a discount, so the total stays
 * the size of the sale and two lines under it split it. The card line may say "Paid by card" only once
 * the card was charged; a pending or failed payment still owes it, as both apps say.
 */
describe('OrderDetailComponent — the credit split', () => {
  let fixture: ComponentFixture<OrderDetailComponent>;

  async function setup(booked: OrderItem): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [
        OrderDetailComponent,
        TranslateModule.forRoot({
          loader: { provide: TranslateLoader, useValue: { getTranslation: () => of(COPY) } },
        }),
      ],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: PLATFORM_ID, useValue: 'browser' },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => ORDER_ID } } } },
        {
          provide: CustomerClient,
          useValue: {
            orderClient: { getById: jest.fn().mockReturnValue(of(booked)) },
            membershipClient: { getMine: () => of(null) },
            receivableClient: { getMine: () => of([]) },
          },
        },
        { provide: CustomerAuthService, useValue: { isLoggedIn: () => true } },
        {
          provide: SnackbarService,
          useValue: { showSuccess: jest.fn(), showError: jest.fn(), showApiError: jest.fn() },
        },
      ],
    })
      .overrideComponent(OrderDetailComponent, {
        remove: { imports: [OrderPreferredOfferComponent] },
        add: {
          imports: [PreferredOfferStub],
          providers: [
            { provide: OrderMarketFacade, useValue: { label: () => 'Czechia', destroyed$: new Subject<void>() } },
            { provide: OrderPreferredOfferFacade, useValue: { connect: jest.fn(), destroyed$: new Subject<void>() } },
          ],
        },
      })
      .compileComponents();

    TestBed.inject(TranslateService).use('en');
    fixture = TestBed.createComponent(OrderDetailComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  afterEach(() => TestBed.resetTestingModule());

  /** [label, amount] of one tender line, or [] when it is not on the page. */
  const tender = (kind: 'credit' | 'card'): string[] =>
    Array.from(
      (fixture.nativeElement as HTMLElement).querySelector(`[data-spec-tender="${kind}"]`)?.children ?? [],
    ).map((cell) => (cell.textContent ?? '').replace(/\s+/g, ' ').trim());

  it('splits a charged card order into credit and card', async () => {
    await setup(order(PaymentStatus.Paid, 500));

    expect(tender('credit')[0]).toBe('Paid with credit');
    expect(tender('credit')[1]).toMatch(/^−.*500/);
    expect(tender('card')[0]).toBe('Paid by card');
    expect(tender('card')[1]).toMatch(/1\D?500/);
  });

  it.each([PaymentStatus.Refunded, PaymentStatus.PartiallyRefunded, PaymentStatus.Disputed])(
    'keeps "Paid by card" once the card was charged, even after it moved on (status %p)',
    async (status) => {
      await setup(order(status, 500));

      expect(tender('card')[0]).toBe('Paid by card');
    },
  );

  it.each([PaymentStatus.Pending, PaymentStatus.Failed])(
    'says what the card is still to pay while the payment has not gone through (status %p)',
    async (status) => {
      await setup(order(status, 500));

      expect(tender('credit')[0]).toBe('Paid with credit');
      expect(tender('card')[0]).toBe('To pay by card');
      expect(tender('card')[1]).toMatch(/1\D?500/);
    },
  );

  it('shows no split on an order that spent no credit', async () => {
    await setup(order(PaymentStatus.Paid, 0));

    expect(tender('credit')).toEqual([]);
    expect(tender('card')).toEqual([]);
  });
});
