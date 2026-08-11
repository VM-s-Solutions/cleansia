import { PLATFORM_ID, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import {
  ChoosePreferredCleanerCommand,
  CustomerClient,
  GetMyServingCleanersResponse,
  OrderItem,
  OrderStatus,
  PaymentStatus,
  PaymentType,
  PreferredOfferState,
} from '@cleansia/customer-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { NEVER, of, throwError } from 'rxjs';
import { OrderPreferredOfferFacade } from './order-preferred-offer.facade';

const ORDER_ID = 'ord-1';

/** Read off the enums so a member added by a later regen joins the sweep without an edit here. */
const ALL_ORDER_STATUSES = Object.values(OrderStatus).filter(
  (value): value is OrderStatus => typeof value === 'number'
);
/** The states that carry a sentence; `None` says nothing on its own and is pinned separately. */
const SENTENCE_STATES = [
  PreferredOfferState.AwaitingConfirmation,
  PreferredOfferState.Accepted,
  PreferredOfferState.Closed,
];

function cleaner(
  employeeId: string,
  fullName: string,
  isAvailableForRequestedSlot?: boolean
): GetMyServingCleanersResponse {
  const row = new GetMyServingCleanersResponse();
  row.employeeId = employeeId;
  row.fullName = fullName;
  row.isAvailableForRequestedSlot = isAvailableForRequestedSlot;
  return row;
}

function buildOrder(fields: {
  orderStatus?: OrderStatus;
  paymentType?: PaymentType;
  paymentStatus?: PaymentStatus;
  state?: PreferredOfferState;
  canChooseAnother?: boolean;
  cleaningDateTime?: string;
  serviceIds?: string[];
  packageIds?: string[];
}): OrderItem {
  return OrderItem.fromJS({
    id: ORDER_ID,
    orderStatus: { value: fields.orderStatus ?? OrderStatus.New, name: 'New' },
    paymentType: { value: fields.paymentType ?? PaymentType.Cash, name: 'Cash' },
    paymentStatus: {
      value: fields.paymentStatus ?? PaymentStatus.Paid,
      name: 'Paid',
    },
    cleaningDateTime: fields.cleaningDateTime ?? '2026-09-01T08:00:00Z',
    selectedServices: (fields.serviceIds ?? ['svc-1']).map((id) => ({ id })),
    selectedPackages: (fields.packageIds ?? []).map((id) => ({ id })),
    preferredOffer: {
      state: fields.state ?? PreferredOfferState.Closed,
      cleanerName: 'Anna Nováková',
      canChooseAnother: fields.canChooseAnother ?? true,
    },
  });
}

/** What the server now sends once the block's sentence has stopped being true (ADR-0049 D4). */
function buildOrderWithheldOffer(orderStatus: OrderStatus): OrderItem {
  return OrderItem.fromJS({
    id: ORDER_ID,
    orderStatus: { value: orderStatus },
    paymentType: { value: PaymentType.Cash, name: 'Cash' },
    paymentStatus: { value: PaymentStatus.Paid, name: 'Paid' },
    cleaningDateTime: '2026-09-01T08:00:00Z',
    selectedServices: [{ id: 'svc-1' }],
    selectedPackages: [],
  });
}

describe('OrderPreferredOfferFacade', () => {
  let facade: OrderPreferredOfferFacade;
  let orderClient: {
    myServingCleaners: jest.Mock;
    choosePreferredCleaner: jest.Mock;
  };
  let snackbar: { showSuccess: jest.Mock; showError: jest.Mock };
  let order: ReturnType<typeof signal<OrderItem | null>>;
  let onChosen: jest.Mock;

  function build(platform: 'server' | 'browser' = 'browser'): void {
    orderClient = {
      myServingCleaners: jest
        .fn()
        .mockReturnValue(of([cleaner('emp-1', 'Anna Nováková', true)])),
      choosePreferredCleaner: jest
        .fn()
        .mockReturnValue(of({ orderId: ORDER_ID, round: 2 })),
    };
    snackbar = { showSuccess: jest.fn(), showError: jest.fn() };
    order = signal<OrderItem | null>(null);
    onChosen = jest.fn();

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        OrderPreferredOfferFacade,
        { provide: PLATFORM_ID, useValue: platform },
        { provide: CustomerClient, useValue: { orderClient } },
        { provide: SnackbarService, useValue: snackbar },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
      ],
    });

    facade = TestBed.inject(OrderPreferredOfferFacade);
    facade.connect({ order, onChosen });
  }

  beforeEach(() => build());

  describe('the state block', () => {
    it('says nothing about an order that has not loaded', () => {
      expect(facade.offer().state).toBe(PreferredOfferState.None);
      expect(facade.canChooseAnother()).toBe(false);
      expect(facade.visible()).toBe(false);
    });

    it('renders nothing for an order that never carried a reservation and cannot take one', () => {
      order.set(
        buildOrder({ state: PreferredOfferState.None, canChooseAnother: false })
      );

      expect(facade.visible()).toBe(false);
    });

    it('renders while a reservation is running', () => {
      order.set(
        buildOrder({
          state: PreferredOfferState.AwaitingConfirmation,
          canChooseAnother: false,
        })
      );

      expect(facade.visible()).toBe(true);
      expect(facade.offer().cleanerName).toBe('Anna Nováková');
    });

    it('renders once a reservation has closed', () => {
      order.set(buildOrder({ state: PreferredOfferState.Closed }));

      expect(facade.visible()).toBe(true);
      expect(facade.canChooseAnother()).toBe(true);
    });
  });

  /**
   * ADR-0049 D4/D6. `visible()` answers one question — did the block arrive — and the server is the
   * only judge of whether its sentence is still true. These two sweeps are the mutation guard for
   * that split, and they are the pair `order-preferred-offer.models.spec.ts` cannot provide: it
   * exercises the resolver, while the veto a lane reaches for goes here, in `visible()`.
   */
  describe('renders off the arrival of the block and nothing else', () => {
    // A sweep read off an enum passes vacuously if the read ever returns nothing.
    it('sweeps every fulfilment state, both concluded ends included', () => {
      expect(ALL_ORDER_STATUSES.length).toBeGreaterThanOrEqual(7);
      expect(ALL_ORDER_STATUSES).toEqual(
        expect.arrayContaining([OrderStatus.Completed, OrderStatus.Cancelled])
      );
    });

    it.each(
      ALL_ORDER_STATUSES.flatMap((status) =>
        SENTENCE_STATES.map((state) => [status, state] as const)
      )
    )('renders a block the server sent on status %i in state %i', (status, state) => {
      order.set(buildOrder({ orderStatus: status, state, canChooseAnother: false }));

      expect(facade.visible()).toBe(true);
      expect(facade.offer().state).toBe(state);
    });

    // `None` plus an open exit is how the feature is first offered — no reservation yet, and the
    // invitation to make one. Nothing else here fails if `visible()` loses its second disjunct.
    it('renders the invitation on an order with no reservation that can still take one', () => {
      order.set(
        buildOrder({ state: PreferredOfferState.None, canChooseAnother: true })
      );

      expect(facade.visible()).toBe(true);
    });

    // The arrival pin: the server may now send no block at all, and the absence IS the answer.
    it.each(ALL_ORDER_STATUSES)(
      'renders nothing at all when the server withheld the block on status %i',
      (status) => {
        order.set(buildOrderWithheldOffer(status));

        expect(facade.visible()).toBe(false);
        expect(facade.offer().state).toBe(PreferredOfferState.None);
        expect(facade.offer().cleanerName).toBe('');
        expect(facade.offer().respondByUtc).toBeNull();
        expect(facade.canChooseAnother()).toBe(false);
      }
    );

    // The withheld block takes the exit with it, and the picker must not reopen it (ADR-0049 D5).
    it('opens no picker for an order whose block the server withheld', () => {
      order.set(buildOrderWithheldOffer(OrderStatus.Completed));

      facade.openPicker();

      expect(facade.picking()).toBe(false);
      expect(orderClient.myServingCleaners).not.toHaveBeenCalled();
    });
  });

  describe('the exit', () => {
    it('is closed while a reservation is still running', () => {
      order.set(
        buildOrder({
          state: PreferredOfferState.AwaitingConfirmation,
          canChooseAnother: false,
        })
      );

      expect(facade.canChooseAnother()).toBe(false);
    });

    it('is closed on a cancelled order because the server closed it', () => {
      order.set(
        buildOrder({
          orderStatus: OrderStatus.Cancelled,
          canChooseAnother: false,
        })
      );

      expect(facade.canChooseAnother()).toBe(false);
    });

    // The money axis: no fulfilment state names this order, so no client-side status set could
    // have withheld the button. `OrderAvailability.IsOfferable` does, inside the server's flag.
    it('is closed on an unpaid card order in no closed fulfilment state', () => {
      order.set(
        buildOrder({
          orderStatus: OrderStatus.New,
          paymentType: PaymentType.Card,
          paymentStatus: PaymentStatus.Pending,
          canChooseAnother: false,
        })
      );

      expect(facade.canChooseAnother()).toBe(false);
      expect(facade.visible()).toBe(true);
    });

    // `PreferredOfferExit.IsOpen` opens with the caller's entitlement, so a non-member's order
    // arrives already closed and this facade asks nothing further.
    it('is closed for a customer without Plus because the server closed it', () => {
      order.set(buildOrder({ canChooseAnother: false }));

      expect(facade.canChooseAnother()).toBe(false);
      expect(facade.visible()).toBe(true);
    });

    it('renders nothing at all for an order that never carried a reservation and cannot take one', () => {
      order.set(
        buildOrder({ state: PreferredOfferState.None, canChooseAnother: false })
      );

      expect(facade.visible()).toBe(false);
    });

    /**
     * The mutation guard for the deleted membership conjunct. This facade is connected to the
     * order alone; a term about the caller re-introduced here reads a signal that no longer
     * exists and reddens this row.
     */
    it('conjoins nothing of its own onto the flag the server sent', () => {
      order.set(buildOrder({ canChooseAnother: true }));

      expect(facade.canChooseAnother()).toBe(true);
    });

    it('does not open the picker where it is closed', () => {
      order.set(buildOrder({ canChooseAnother: false }));

      facade.openPicker();

      expect(facade.picking()).toBe(false);
      expect(orderClient.myServingCleaners).not.toHaveBeenCalled();
    });
  });

  describe('the roster', () => {
    beforeEach(() => order.set(buildOrder({})));

    it('asks the slot question about this order, not about nothing', () => {
      facade.openPicker();

      const [cleaningDateTimeUtc, serviceIds, packageIds] =
        orderClient.myServingCleaners.mock.calls[0];
      expect(cleaningDateTimeUtc).toEqual(new Date('2026-09-01T08:00:00Z'));
      expect(serviceIds).toEqual(['svc-1']);
      expect(packageIds).toEqual([]);
    });

    it('exposes the roster as options once it lands', () => {
      facade.openPicker();

      expect(facade.picking()).toBe(true);
      expect(facade.listLoading()).toBe(false);
      expect(facade.listFailed()).toBe(false);
      expect(facade.options()).toEqual([
        { label: 'Anna Nováková', value: 'emp-1', disabled: false },
      ]);
    });

    it('reports an empty roster as an empty state, not as a failure', () => {
      orderClient.myServingCleaners.mockReturnValue(of([]));

      facade.openPicker();

      expect(facade.options()).toEqual([]);
      expect(facade.listFailed()).toBe(false);
    });

    it('reports a failed roster read so the customer can retry', () => {
      orderClient.myServingCleaners.mockReturnValue(throwError(() => new Error('boom')));

      facade.openPicker();

      expect(facade.listFailed()).toBe(true);
      expect(facade.listLoading()).toBe(false);
      expect(facade.options()).toEqual([]);
    });

    it('keeps a cleaner this slot cannot take visible and unselectable', () => {
      orderClient.myServingCleaners.mockReturnValue(
        of([cleaner('emp-1', 'Anna Nováková', false)])
      );

      facade.openPicker();

      expect(facade.options()[0].disabled).toBe(true);
    });

    it('does not read the roster during the server render', () => {
      build('server');
      order.set(buildOrder({}));

      facade.openPicker();

      expect(orderClient.myServingCleaners).not.toHaveBeenCalled();
    });
  });

  describe('submitting the choice', () => {
    beforeEach(() => {
      order.set(buildOrder({}));
      facade.openPicker();
    });

    it('sends the order id and the cleaner id, and nothing else', () => {
      facade.select('emp-1');
      facade.submit();

      const command: ChoosePreferredCleanerCommand =
        orderClient.choosePreferredCleaner.mock.calls[0][0];
      expect(command).toBeInstanceOf(ChoosePreferredCleanerCommand);
      expect(JSON.parse(JSON.stringify(command))).toEqual({
        orderId: ORDER_ID,
        employeeId: 'emp-1',
      });
    });

    it('sends nothing until a cleaner is chosen', () => {
      facade.submit();

      expect(orderClient.choosePreferredCleaner).not.toHaveBeenCalled();
    });

    it('sends one request even when the button is tapped twice', () => {
      orderClient.choosePreferredCleaner.mockReturnValue(NEVER);
      facade.select('emp-1');

      facade.submit();
      facade.submit();

      expect(orderClient.choosePreferredCleaner).toHaveBeenCalledTimes(1);
    });

    it('closes the picker, tells the customer who was asked and reloads the order', () => {
      facade.select('emp-1');

      facade.submit();

      expect(snackbar.showSuccess).toHaveBeenCalledWith(
        'preferred_cleaner.choose_success'
      );
      expect(facade.picking()).toBe(false);
      expect(facade.submitting()).toBe(false);
      expect(onChosen).toHaveBeenCalledTimes(1);
    });

    // The shared interceptor already renders the refusal from its own key; a second snackbar here
    // would replace a translated sentence with whatever this facade guessed.
    it('leaves a refusal to the interceptor and never reloads the order', () => {
      orderClient.choosePreferredCleaner.mockReturnValue(
        throwError(() => new Error('order.preferred_offer_closed'))
      );
      facade.select('emp-1');

      facade.submit();

      expect(snackbar.showError).not.toHaveBeenCalled();
      expect(snackbar.showSuccess).not.toHaveBeenCalled();
      expect(facade.submitting()).toBe(false);
      expect(onChosen).not.toHaveBeenCalled();
    });
  });
});
