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
  CustomerOrderClient,
  OrderItem,
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
  let getById: jest.SpyInstance;

  /**
   * `guestOrders` is where the access tokens live — one is written when a booking is opened from
   * its e-mail link. Empty by default so these tests exercise the page WITHOUT an order, which is
   * the branch that must keep working: the confirmation still has to render when there is nothing
   * to prove the booking with.
   */
  async function render(
    options: {
      type?: string;
      orderId?: string;
      loggedIn?: boolean;
      guestOrders?: unknown[];
      lookup?: unknown;
      signedInRead?: unknown;
    } = {},
  ): Promise<void> {
    // The batch query is the one keyed on access tokens; the single lookup takes exactly one.
    lookupBatch = jest.fn().mockReturnValue(
      options.lookup === 'fail'
        ? throwError(() => new Error('not found'))
        : of({ orders: options.lookup === 'none' ? [] : [ORDER] }),
    );
    // The signed-in read is the app's own authenticated order detail, which answers the same
    // members this page reads off the guest lookup.
    getById = jest.spyOn(CustomerOrderClient.prototype, 'getById').mockReturnValue(
      options.signedInRead === 'fail'
        ? throwError(() => new Error('not yours'))
        : of(OrderItem.fromJS(ORDER)),
    );
    queryParamMap = new BehaviorSubject<ParamMap>(
      convertToParamMap({
        ...(options.type === undefined ? {} : { type: options.type }),
        ...(options.orderId === undefined ? {} : { orderId: options.orderId }),
      })
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

  afterEach(() => {
    jest.restoreAllMocks();
    TestBed.resetTestingModule();
  });

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
   * Both paths name the booking in the URL — the wizard puts `?orderId=` on the cash navigation and
   * Stripe returns it on the card one. A guest proves it with the access token this browser holds;
   * a signed-in customer's session proves it, and the server decides whose order it is.
   */
  describe('the order it is confirming', () => {
    const GUEST = [
      { orderId: '01ORDER', accessToken: 'tok-01ORDER', createdAt: '2026-09-02T09:14:00Z' },
    ];

    it.each(['card', 'cash'])(
      'asks for the booking the %s path named, with the token this browser holds',
      async (type) => {
        await render({ type, orderId: '01ORDER', guestOrders: GUEST });
        expect(lookupBatch).toHaveBeenCalledWith(['tok-01ORDER']);
        expect(fixture.componentInstance.hasOrder()).toBe(true);
      },
    );

    // The whole point of the fallback: a confirmation that cannot prove what it
    // is confirming still has to confirm, rather than showing an error to
    // someone who has just paid.
    it('renders without figures when the lookup fails', async () => {
      await render({ orderId: '01ORDER', guestOrders: GUEST, lookup: 'fail' });
      expect(fixture.componentInstance.hasOrder()).toBe(false);
    });

    it('asks for nothing when this browser holds no token for that booking', async () => {
      await render({ orderId: '01ORDER', guestOrders: [] });
      expect(lookupBatch).not.toHaveBeenCalled();
      expect(fixture.componentInstance.hasOrder()).toBe(false);
    });

    it('asks for nothing when the URL names no booking', async () => {
      await render({ type: 'cash', guestOrders: GUEST });
      expect(lookupBatch).not.toHaveBeenCalled();
      expect(fixture.componentInstance.hasOrder()).toBe(false);
    });

    it('asks for nothing when the URL names a booking this browser cannot prove', async () => {
      await render({ orderId: '01OTHER', guestOrders: GUEST });
      expect(lookupBatch).not.toHaveBeenCalled();
      expect(fixture.componentInstance.hasOrder()).toBe(false);
    });

    it('never reads a guest booking through the signed-out path with the session', async () => {
      await render({ orderId: '01ORDER', guestOrders: GUEST });
      expect(getById).not.toHaveBeenCalled();
    });
  });

  /**
   * A signed-in customer holds no access token — one is minted for guest bookings only — so the
   * booking comes back through the account's own order detail, keyed on the id in the URL. Whether
   * that booking is theirs is the server's answer, not this page's: a refusal reads the same here as
   * a booking that does not exist.
   */
  describe('the order it is confirming, signed in', () => {
    it.each(['card', 'cash'])(
      'reads the booking the %s path named through the account order detail',
      async (type) => {
        await render({ type, orderId: '01ORDER', loggedIn: true });

        expect(getById).toHaveBeenCalledWith('01ORDER');
        expect(lookupBatch).not.toHaveBeenCalled();
        expect(fixture.componentInstance.hasOrder()).toBe(true);
      },
    );

    it('states no figures when the booking is not theirs, and says nothing else about it', async () => {
      await render({ orderId: '01OTHER', loggedIn: true, signedInRead: 'fail' });

      expect(getById).toHaveBeenCalledWith('01OTHER');
      expect(fixture.componentInstance.hasOrder()).toBe(false);
    });

    it('asks for nothing when the URL names no booking', async () => {
      await render({ type: 'cash', loggedIn: true });

      expect(getById).not.toHaveBeenCalled();
      expect(lookupBatch).not.toHaveBeenCalled();
      expect(fixture.componentInstance.hasOrder()).toBe(false);
    });
  });
});
