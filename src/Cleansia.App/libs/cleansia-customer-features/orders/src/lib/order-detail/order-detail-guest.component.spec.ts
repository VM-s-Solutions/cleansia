import { Component, input, PLATFORM_ID } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, provideRouter } from '@angular/router';
import {
  CancellationFeeTier,
  CustomerAuthService,
  CustomerClient,
  GetCancellationFeePreviewResponse,
  OrderItem,
  OrderStatus,
  PaymentStatus,
  PaymentType,
} from '@cleansia/customer-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { of, Subject } from 'rxjs';
import { OrderPreferredOfferComponent } from './components/order-preferred-offer.component';
import { OrderDetailComponent } from './order-detail.component';
import { OrderMarketFacade } from '../order-market.facade';
import { OrderPreferredOfferFacade } from './order-preferred-offer.facade';

const ORDER_ID = 'ord-guest';

@Component({ selector: 'cleansia-customer-order-preferred-offer', standalone: true, template: '' })
class PreferredOfferStub {
  facade = input<unknown>();
  locale = input<string>();
}

interface Booking {
  status: OrderStatus;
  isGuestBooking: boolean;
  startsAt?: string;
  crew?: boolean;
  receiptNumber?: string;
}

function order({ status, isGuestBooking, startsAt, crew, receiptNumber }: Booking): OrderItem {
  return OrderItem.fromJS({
    id: ORDER_ID,
    displayOrderNumber: 'CLS-2026-0901',
    orderStatus: { value: status, name: OrderStatus[status] },
    paymentType: { value: PaymentType.Card, name: 'Card' },
    paymentStatus: { value: PaymentStatus.Paid, name: 'Paid' },
    cleaningDateTime: startsAt ?? new Date(Date.now() + 3 * 24 * 3600_000).toISOString(),
    totalPrice: 1200,
    currency: { code: 'CZK' },
    assignedEmployees: crew ? [{ employeeId: 'emp-1', fullName: 'Petra S.' }] : [],
    receiptNumber,
    isGuestBooking,
  });
}

const preview = GetCancellationFeePreviewResponse.fromJS({
  orderId: ORDER_ID,
  tier: CancellationFeeTier.FreeNotAccepted,
  feeRate: 0,
  feeAmount: 0,
  refundAmount: 1200,
  totalPrice: 1200,
  currencyCode: 'CZK',
  expressWaiverForfeitedOnCancel: false,
});

/**
 * A booking made without an account under the session's e-mail opens from My orders with what its
 * e-mails already carried, and can be cancelled on its own guest terms; it cannot be rated, invoiced
 * or disputed from the account, so those actions are not offered on it. The server refuses each of
 * them for such a booking either way.
 */
describe('OrderDetailComponent — a guest booking opened from the account', () => {
  let fixture: ComponentFixture<OrderDetailComponent>;
  let orderClient: { getById: jest.Mock; cancellationPreview: jest.Mock; cancel: jest.Mock };

  async function setup(booking: Booking): Promise<HTMLElement> {
    orderClient = {
      getById: jest.fn().mockReturnValue(of(order(booking))),
      cancellationPreview: jest.fn().mockReturnValue(of(preview)),
      cancel: jest.fn(),
    };
    await TestBed.configureTestingModule({
      imports: [OrderDetailComponent, TranslateModule.forRoot()],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: PLATFORM_ID, useValue: 'browser' },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => ORDER_ID } } } },
        {
          provide: CustomerClient,
          useValue: {
            orderClient,
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
    return fixture.nativeElement as HTMLElement;
  }

  const text = (page: HTMLElement): string => page.textContent ?? '';
  const cancelButton = (page: HTMLElement): HTMLButtonElement | null =>
    page.querySelector('.order-detail__action--danger');
  const past = new Date(Date.now() - 3600_000).toISOString();

  describe('before its start', () => {
    it('is labelled and says what the account can do with it', async () => {
      const page = await setup({ status: OrderStatus.Confirmed, isGuestBooking: true });

      expect(page.querySelector('.order-detail__hero .order-detail__guest')?.textContent?.trim()).toBe(
        'pages.orders.guest_booking',
      );
      expect(text(page)).toContain('pages.order_detail.guest_booking_note');
    });

    it('offers Cancel, which previews the fee on the order as for any booking', async () => {
      const page = await setup({ status: OrderStatus.Confirmed, isGuestBooking: true });

      cancelButton(page)?.click();
      fixture.detectChanges();

      expect(orderClient.cancellationPreview).toHaveBeenCalledWith(ORDER_ID);
    });

    it('offers no way to report a problem with it', async () => {
      const page = await setup({ status: OrderStatus.Confirmed, isGuestBooking: true });

      expect(text(page)).not.toContain('pages.order_detail.action_report');
    });
  });

  it('offers neither the rating nor the invoice once it is completed', async () => {
    const page = await setup({ status: OrderStatus.Completed, isGuestBooking: true, receiptNumber: 'R-1' });

    expect(page.querySelector('.order-detail__review')).toBeNull();
    expect(text(page)).not.toContain('pages.order_detail.action_invoice');
    expect(text(page)).toContain('pages.order_detail.action_recurring');
  });

  it('offers no no-show report once it is past its start with a cleaner on it, nor Cancel', async () => {
    const page = await setup({ status: OrderStatus.Confirmed, isGuestBooking: true, startsAt: past, crew: true });

    expect(text(page)).not.toContain('pages.order_detail.cleaner_no_show.action');
    expect(text(page)).not.toContain('pages.order_detail.cleaner_no_show.note');
    expect(cancelButton(page)).toBeNull();
  });

  describe('the account’s own booking, for contrast', () => {
    it('is not labelled and offers the report', async () => {
      const page = await setup({ status: OrderStatus.Confirmed, isGuestBooking: false });

      expect(page.querySelector('.order-detail__guest')).toBeNull();
      expect(text(page)).not.toContain('pages.order_detail.guest_booking_note');
      expect(text(page)).toContain('pages.order_detail.action_report');
      expect(cancelButton(page)).not.toBeNull();
    });

    it('offers the rating and the invoice once it is completed', async () => {
      const page = await setup({ status: OrderStatus.Completed, isGuestBooking: false, receiptNumber: 'R-1' });

      expect(page.querySelector('.order-detail__review')).not.toBeNull();
      expect(text(page)).toContain('pages.order_detail.action_invoice');
    });

    it('offers the no-show report once it is past its start with a cleaner on it', async () => {
      const page = await setup({ status: OrderStatus.Confirmed, isGuestBooking: false, startsAt: past, crew: true });

      expect(text(page)).toContain('pages.order_detail.cleaner_no_show.action');
      expect(text(page)).toContain('pages.order_detail.cleaner_no_show.note');
    });
  });
});
