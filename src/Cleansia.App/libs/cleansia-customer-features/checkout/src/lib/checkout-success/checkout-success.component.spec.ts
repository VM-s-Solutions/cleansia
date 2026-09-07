import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Title } from '@angular/platform-browser';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { convertToParamMap, ParamMap } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { GuestOrderService, TrackOrderFacade } from '@cleansia-customer/orders';
import {
  CUSTOMER_API_BASE_URL,
  CustomerAuthService,
} from '@cleansia/customer-services';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { BehaviorSubject, of, throwError } from 'rxjs';
import { CheckoutSuccessComponent } from './checkout-success.component';

/** Shaped as `LookupOrder.Response` returns it. */
const ORDER = {
  id: '01ORDER',
  displayOrderNumber: 'CLS-2026-0412',
  cleaningDateTime: '2026-09-03T10:00:00',
  paymentType: { type: 'PaymentType', name: 'Card', value: 2 },
  paymentStatus: { type: 'PaymentStatus', name: 'Paid', value: 2 },
  totalPrice: 1700,
  estimatedTime: 210,
  orderStatus: { type: 'OrderStatus', name: 'Confirmed', value: 2 },
  currency: { code: 'CZK' },
  selectedServices: [{ id: 's1', name: 'A' }, { id: 's2', name: 'B' }],
  selectedPackages: [{ id: 'p1', name: 'C' }],
  statusHistory: [],
  createdOn: '2026-09-02T09:14:00',
};

describe('CheckoutSuccessComponent', () => {
  let fixture: ComponentFixture<CheckoutSuccessComponent>;
  let queryParamMap: BehaviorSubject<ParamMap>;
  let title: Title;
  let lookupBatch: jest.Mock;

  /**
   * `guestOrders` is what the page uses to find the order it is confirming —
   * the wizard writes it there immediately before leaving for Stripe. Empty by
   * default so these tests exercise the page WITHOUT an order, which is the
   * branch that must keep working: the confirmation still has to render when
   * the lookup finds nothing.
   */
  async function render(
    options: {
      type?: string;
      loggedIn?: boolean;
      guestOrders?: unknown[];
      lookup?: unknown;
    } = {},
  ): Promise<void> {
    // The page looks the order up by its ULID, which only the BATCH query
    // matches — the single lookup is keyed on the display order number.
    lookupBatch = jest.fn().mockReturnValue(
      options.lookup === 'fail'
        ? throwError(() => new Error('not found'))
        : of({ orders: options.lookup === 'none' ? [] : [ORDER] }),
    );
    queryParamMap = new BehaviorSubject<ParamMap>(
      convertToParamMap(options.type === undefined ? {} : { type: options.type })
    );

    await TestBed.configureTestingModule({
      imports: [CheckoutSuccessComponent, TranslateModule.forRoot()],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { queryParamMap } },
        { provide: CustomerAuthService, useValue: { isLoggedIn: () => options.loggedIn ?? false } },
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: CUSTOMER_API_BASE_URL, useValue: 'http://localhost:5003' },
        {
          provide: GuestOrderService,
          useValue: { getAll: () => options.guestOrders ?? [] },
        },
      ],
    })
      // The page provides the facade itself, so the double replaces the
      // COMPONENT's provider rather than a module-level one.
      .overrideComponent(CheckoutSuccessComponent, {
        set: { providers: [{ provide: TrackOrderFacade, useValue: { lookupBatch } }] },
      })
      .compileComponents();

    TestBed.inject(TranslateService).setTranslation(
      'en',
      {
        pages: {
          checkout: {
            success: {
              cash: { title: 'Booking confirmed' },
              card: { title: 'Payment received' },
            },
          },
        },
      },
      true
    );
    TestBed.inject(TranslateService).use('en');
    title = TestBed.inject(Title);

    fixture = TestBed.createComponent(CheckoutSuccessComponent);
    fixture.detectChanges();
  }

  afterEach(() => TestBed.resetTestingModule());

  describe('payment type', () => {
    it('reads a cash return from the type query param', async () => {
      await render({ type: 'cash' });

      expect(fixture.componentInstance.isCash()).toBe(true);
    });

    it('treats an absent type param as a card return', async () => {
      await render();

      expect(fixture.componentInstance.isCash()).toBe(false);
    });

    it('treats an unrecognised type as a card return rather than cash', async () => {
      await render({ type: 'bank-transfer' });

      expect(fixture.componentInstance.isCash()).toBe(false);
    });

    it('tracks a later change of the query param', async () => {
      await render({ type: 'card' });

      queryParamMap.next(convertToParamMap({ type: 'cash' }));

      expect(fixture.componentInstance.isCash()).toBe(true);
    });
  });

  describe('document title', () => {
    it('announces the cash outcome on a cash return', async () => {
      await render({ type: 'cash' });

      expect(title.getTitle()).toBe('Booking confirmed | Cleansia');
    });

    it('announces the card outcome on a card return', async () => {
      await render({ type: 'card' });

      expect(title.getTitle()).toBe('Payment received | Cleansia');
    });
  });

  describe('orders link', () => {
    it('sends a signed-in customer to their own order list', async () => {
      await render({ loggedIn: true });

      expect(fixture.componentInstance.ordersRoute).toBe('/orders');
    });

    it('sends a guest to the public order tracker instead', async () => {
      await render({ loggedIn: false });

      expect(fixture.componentInstance.ordersRoute).toBe('/track-order');
    });
  });

  /**
   * Neither return path carries the order in the URL — the cash path navigates
   * here with only `?type=cash`, the card path comes back from a URL Stripe
   * builds — so the page finds it through the entry the wizard wrote before
   * either departure.
   */
  describe('the order it is confirming', () => {
    const GUEST = [
      { orderId: '01ORDER', email: 'jan@example.com', createdAt: '2026-09-02T09:14:00Z' },
    ];

    it('asks for the newest remembered order', async () => {
      await render({ guestOrders: GUEST });
      expect(lookupBatch).toHaveBeenCalledWith([
        { orderId: '01ORDER', email: 'jan@example.com' },
      ]);
      expect(fixture.componentInstance.hasOrder()).toBe(true);
    });

    // The whole point of the fallback: a confirmation that cannot prove what it
    // is confirming still has to confirm, rather than showing an error to
    // someone who has just paid.
    it('renders without figures when the lookup fails', async () => {
      await render({ guestOrders: GUEST, lookup: 'fail' });
      expect(fixture.componentInstance.hasOrder()).toBe(false);
    });

    it('asks for nothing when this browser remembers no order', async () => {
      await render({ guestOrders: [] });
      expect(lookupBatch).not.toHaveBeenCalled();
      expect(fixture.componentInstance.hasOrder()).toBe(false);
    });
  });
});
