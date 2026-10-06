import { Component, input, PLATFORM_ID } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, provideRouter } from '@angular/router';
import {
  CustomerAuthService,
  CustomerClient,
  MyReceivableDto,
  OrderItem,
  OrderStatus,
  PaymentType,
} from '@cleansia/customer-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateLoader, TranslateModule, TranslateService } from '@ngx-translate/core';
import { of, Subject } from 'rxjs';
import { OrderPreferredOfferComponent } from './components/order-preferred-offer.component';
import { OrderDetailComponent } from './order-detail.component';
import { OrderMarketFacade } from '../order-market.facade';
import { OrderDetailFacade } from './order-detail.facade';
import { OrderPreferredOfferFacade } from './order-preferred-offer.facade';

const ORDER_ID = 'ord-1';

const COPY = {
  pages: {
    order_detail: { order: 'Order' },
    amount_due: {
      title: 'Amount due',
      pay: 'Pay now',
      after_payment: 'Just paid?',
      kind: { cash_cancellation_fee: 'Late-cancellation fee', lockout: 'Lockout fee' },
    },
  },
};

@Component({ selector: 'cleansia-customer-order-preferred-offer', standalone: true, template: '' })
class PreferredOfferStub {
  facade = input<unknown>();
  locale = input<string>();
}

const cancelledCashOrder = OrderItem.fromJS({
  id: ORDER_ID,
  displayOrderNumber: 'CL-1001',
  orderStatus: { value: OrderStatus.Cancelled, name: 'Cancelled' },
  paymentType: { value: PaymentType.Cash, name: 'Cash' },
  cleaningDateTime: '2026-09-25T08:00:00Z',
  totalPrice: 1200,
  currency: { code: 'CZK' },
  selectedServices: [],
  selectedPackages: [],
});

function receivable(id: string, orderId: string, kind: number, amount: number): MyReceivableDto {
  return MyReceivableDto.fromJS({
    id,
    orderId,
    displayOrderNumber: orderId === ORDER_ID ? 'CL-1001' : 'CL-2002',
    kind: { value: kind },
    amount,
    currencyCode: 'CZK',
    createdOn: '2026-09-28T10:00:00Z',
  });
}

describe('OrderDetailComponent — an amount due on the order', () => {
  let fixture: ComponentFixture<OrderDetailComponent>;

  async function setup(owed: MyReceivableDto[]): Promise<void> {
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
            orderClient: { getById: jest.fn().mockReturnValue(of(cancelledCashOrder)) },
            membershipClient: { getMine: () => of(null) },
            receivableClient: { getMine: () => of(owed), createPayLink: jest.fn() },
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

  const text = (el: Element | null): string => (el?.textContent ?? '').replace(/\s+/g, ' ').trim();
  const card = (): HTMLElement | null =>
    (fixture.nativeElement as HTMLElement).querySelector('.customer-amount-due');
  const rows = (): string[][] =>
    Array.from(card()?.querySelectorAll('.customer-amount-due__row') ?? []).map((row) =>
      ['.customer-amount-due__kind', '.customer-amount-due__order', '.customer-amount-due__amount', 'cleansia-button'].map(
        (part) => text(row.querySelector(part)),
      ),
    );

  it('offers to pay what this order owes, and not what another order owes', async () => {
    await setup([receivable('rcv-1', ORDER_ID, 1, 450), receivable('rcv-2', 'ord-2', 2, 1200)]);

    expect(text(card()?.querySelector('.order-detail__card-title') ?? null)).toBe('Amount due');
    expect(rows()).toEqual([['Late-cancellation fee', 'Order CL-1001', 'CZK 450', 'Pay now']]);
    expect(text(card())).toContain('Just paid?');
  });

  it('shows no amount-due card when the order owes nothing', async () => {
    await setup([receivable('rcv-2', 'ord-2', 2, 1200)]);

    expect(card()).toBeNull();
  });

  it('offers to pay everything owed, and says why, once a confirmation was refused for it', async () => {
    await setup([receivable('rcv-1', ORDER_ID, 1, 450), receivable('rcv-2', 'ord-2', 2, 1200)]);

    fixture.debugElement.injector.get(OrderDetailFacade).confirmRefusedForDebt.set(true);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(rows().map((row) => row[1])).toEqual(['Order CL-1001', 'Order CL-2002']);
    expect(
      (fixture.nativeElement as HTMLElement).querySelector('[data-spec-recurring-owed]'),
    ).not.toBeNull();
  });
});
