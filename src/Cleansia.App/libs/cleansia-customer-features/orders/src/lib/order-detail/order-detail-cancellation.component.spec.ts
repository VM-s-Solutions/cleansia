import { Component, input, PLATFORM_ID } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { CleansiaTextareaComponent } from '@cleansia/components';
import {
  CancelOrderResponse,
  CancellationFeeTier,
  CustomerAuthService,
  CustomerClient,
  GetCancellationFeePreviewResponse,
  OrderItem,
  OrderStatus,
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
    order_detail: {
      cancellation: {
        success: 'Your booking is cancelled.',
        refund_success: 'A refund of {{amount}} was issued.',
        grace_note: 'Free within {{minutes}} minutes of booking.',
      },
    },
  },
};

@Component({ selector: 'cleansia-customer-order-preferred-offer', standalone: true, template: '' })
class PreferredOfferStub {
  facade = input<unknown>();
  locale = input<string>();
}

function order(status: OrderStatus): OrderItem {
  return OrderItem.fromJS({
    id: ORDER_ID,
    displayOrderNumber: 'ORD-1',
    orderStatus: { value: status, name: OrderStatus[status] },
    cleaningDateTime: '2026-09-25T08:00:00Z',
    totalPrice: 1200,
    currency: { code: 'CZK' },
    services: [],
    packages: [],
  });
}

const preview = GetCancellationFeePreviewResponse.fromJS({
  orderId: ORDER_ID,
  tier: CancellationFeeTier.Partial,
  feeRate: 0.25,
  feeAmount: 300,
  refundAmount: 900,
  totalPrice: 1200,
  currencyCode: 'CZK',
  expressWaiverForfeitedOnCancel: false,
});

/**
 * The customer sees three things the server decides: whether cancelling is on offer at all, how much
 * of a reason it will accept, and what it actually refunded — which is not the preview's ceiling.
 */
describe('OrderDetailComponent — cancelling a booking', () => {
  let fixture: ComponentFixture<OrderDetailComponent>;
  let orderClient: { getById: jest.Mock; cancellationPreview: jest.Mock; cancel: jest.Mock };

  async function setup(status: OrderStatus): Promise<void> {
    orderClient = {
      getById: jest.fn().mockReturnValue(of(order(status))),
      cancellationPreview: jest.fn().mockReturnValue(of(preview)),
      cancel: jest.fn(),
    };
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
          useValue: { orderClient, membershipClient: { getMine: () => of(null) } },
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
          // The real facades' destroy hooks run against the doubles on teardown.
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

  const cancelButton = (): HTMLButtonElement | null =>
    (fixture.nativeElement as HTMLElement).querySelector('.order-detail__action--danger');

  it.each([OrderStatus.New, OrderStatus.Confirmed, OrderStatus.OnTheWay])(
    'offers the cancel action while the order is in status %s',
    async (status) => {
      await setup(status);
      expect(cancelButton()).not.toBeNull();
    },
  );

  it.each([OrderStatus.InProgress, OrderStatus.Completed, OrderStatus.Cancelled])(
    'withholds the cancel action once the order is in status %s',
    async (status) => {
      await setup(status);
      expect(cancelButton()).toBeNull();
    },
  );

  // Owner ruling 2026-09-24: 15 minutes, 60 for an entitled Plus member. Which one THIS customer has
  // is the server's answer on the preview; the sheet never works it out from the membership.
  it.each([15, 60])("states the customer's own %i-minute grace from the preview", async (minutes) => {
    await setup(OrderStatus.Confirmed);
    orderClient.cancellationPreview.mockReturnValue(
      of(GetCancellationFeePreviewResponse.fromJS({ ...preview.toJSON(), oopsWindowMinutes: minutes })),
    );

    fixture.componentInstance.openCancellation();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain(
      `Free within ${minutes} minutes of booking.`,
    );
  });

  it('caps the reason at the length the server accepts', async () => {
    await setup(OrderStatus.OnTheWay);

    cancelButton()?.click();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const reason = fixture.debugElement.query(By.directive(CleansiaTextareaComponent));
    expect(reason).toBeTruthy();
    expect((reason.componentInstance as CleansiaTextareaComponent).maxLength()).toBe(500);
    const textarea = (reason.nativeElement as HTMLElement).querySelector('textarea');
    expect(textarea?.getAttribute('maxlength')).toBe('500');
  });

  it('confirms with the figure the server actually refunded, not the preview estimate', async () => {
    await setup(OrderStatus.Confirmed);
    orderClient.cancel.mockReturnValue(
      of(
        CancelOrderResponse.fromJS({
          orderId: ORDER_ID,
          feeRate: 0.25,
          refundAmount: 900,
          totalPrice: 1200,
          refundInitiated: true,
          actualRefundAmount: 850,
        }),
      ),
    );
    orderClient.getById.mockReturnValue(of(order(OrderStatus.Cancelled)));

    fixture.componentInstance.openCancellation();
    fixture.componentInstance.confirmCancellation();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const result = (fixture.nativeElement as HTMLElement).querySelector('.order-detail__cancellation-result');
    expect(result?.textContent).toContain('Your booking is cancelled.');
    expect(result?.textContent).toContain('A refund of');
    expect(result?.textContent).toContain('850');
    expect(result?.textContent).not.toContain('900');
    expect(cancelButton()).toBeNull();
  });

  it('names no refund when the server issued none', async () => {
    await setup(OrderStatus.New);
    orderClient.cancel.mockReturnValue(
      of(
        CancelOrderResponse.fromJS({
          orderId: ORDER_ID,
          feeRate: 0,
          refundAmount: 1200,
          totalPrice: 1200,
          refundInitiated: false,
        }),
      ),
    );
    orderClient.getById.mockReturnValue(of(order(OrderStatus.Cancelled)));

    fixture.componentInstance.openCancellation();
    fixture.componentInstance.confirmCancellation();
    fixture.detectChanges();

    const result = (fixture.nativeElement as HTMLElement).querySelector('.order-detail__cancellation-result');
    expect(result?.textContent).toContain('Your booking is cancelled.');
    expect(result?.textContent).not.toContain('refund');
    expect(result?.textContent).not.toMatch(/\d/);
  });
});
