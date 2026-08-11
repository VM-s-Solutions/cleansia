import {
  OrderItem,
  OrderStatus,
  PaymentStatus,
  PaymentType,
  PreferredOfferState,
} from '@cleansia/customer-services';
import { resolvePreferredOfferView } from './order-preferred-offer.models';

/** Read off the enums so a member added by a later regen joins the sweep without an edit here. */
const ALL_ORDER_STATUSES = Object.values(OrderStatus).filter(
  (value): value is OrderStatus => typeof value === 'number'
);
const ALL_OFFER_STATES = Object.values(PreferredOfferState).filter(
  (value): value is PreferredOfferState => typeof value === 'number'
);

function buildOrder(fields: {
  orderStatus?: OrderStatus;
  paymentType?: PaymentType;
  paymentStatus?: PaymentStatus;
  preferredOffer?: {
    state: PreferredOfferState;
    cleanerName?: string;
    respondByUtc?: string;
    canChooseAnother: boolean;
  } | null;
}): OrderItem {
  return OrderItem.fromJS({
    id: 'ord-1',
    orderStatus: { value: fields.orderStatus ?? OrderStatus.New, name: 'New' },
    paymentType: { value: fields.paymentType ?? PaymentType.Cash, name: 'Cash' },
    paymentStatus: {
      value: fields.paymentStatus ?? PaymentStatus.Paid,
      name: 'Paid',
    },
    preferredOffer: fields.preferredOffer ?? undefined,
  });
}

describe('resolvePreferredOfferView', () => {
  it('reads no offer at all off an absent order', () => {
    const view = resolvePreferredOfferView(null);

    expect(view.state).toBe(PreferredOfferState.None);
    expect(view.canChooseAnother).toBe(false);
  });

  // A block the server did not send must render as "no offer" — never as a dropped order row.
  it('reads no offer off an order whose block the server did not send', () => {
    const view = resolvePreferredOfferView(buildOrder({ preferredOffer: null }));

    expect(view.state).toBe(PreferredOfferState.None);
    expect(view.cleanerName).toBe('');
    expect(view.respondByUtc).toBeNull();
    expect(view.canChooseAnother).toBe(false);
  });

  it('carries the running reservation through unchanged', () => {
    const view = resolvePreferredOfferView(
      buildOrder({
        preferredOffer: {
          state: PreferredOfferState.AwaitingConfirmation,
          cleanerName: 'Anna Nováková',
          respondByUtc: '2026-09-01T18:00:00Z',
          canChooseAnother: false,
        },
      })
    );

    expect(view.state).toBe(PreferredOfferState.AwaitingConfirmation);
    expect(view.cleanerName).toBe('Anna Nováková');
    expect(view.respondByUtc).toEqual(new Date('2026-09-01T18:00:00Z'));
    expect(view.canChooseAnother).toBe(false);
  });

  it('offers the exit exactly where the server says it is open', () => {
    const view = resolvePreferredOfferView(
      buildOrder({
        preferredOffer: {
          state: PreferredOfferState.Closed,
          cleanerName: 'Anna Nováková',
          canChooseAnother: true,
        },
      })
    );

    expect(view.canChooseAnother).toBe(true);
  });

  it('never opens the exit the server closed', () => {
    const view = resolvePreferredOfferView(
      buildOrder({
        preferredOffer: {
          state: PreferredOfferState.AwaitingConfirmation,
          canChooseAnother: false,
        },
      })
    );

    expect(view.canChooseAnother).toBe(false);
  });

  it('reads an absent flag as closed rather than as open', () => {
    const view = resolvePreferredOfferView(
      OrderItem.fromJS({
        id: 'ord-1',
        orderStatus: { value: OrderStatus.New, name: 'New' },
        preferredOffer: { state: PreferredOfferState.Closed },
      })
    );

    expect(view.canChooseAnother).toBe(false);
  });

  /**
   * `PreferredOfferExit.IsOpen` conjoins `OrderAvailability.IsOfferable`, so these arrive already
   * closed. Each is asserted through the flag, never through a status this file reads: a rule
   * restated here would be a second, weaker implementation of the server's gate.
   */
  describe('the closures the server now sends', () => {
    it('is closed on a cancelled order because the server closed it', () => {
      const view = resolvePreferredOfferView(
        buildOrder({
          orderStatus: OrderStatus.Cancelled,
          preferredOffer: {
            state: PreferredOfferState.Closed,
            cleanerName: 'Anna Nováková',
            canChooseAnother: false,
          },
        })
      );

      expect(view.canChooseAnother).toBe(false);
    });

    it('is closed on a completed order because the server closed it', () => {
      const view = resolvePreferredOfferView(
        buildOrder({
          orderStatus: OrderStatus.Completed,
          preferredOffer: {
            state: PreferredOfferState.Accepted,
            cleanerName: 'Anna Nováková',
            canChooseAnother: false,
          },
        })
      );

      expect(view.canChooseAnother).toBe(false);
    });

    // The money axis, which no status set can express: a card booking whose payment has not landed
    // is refused by IsOfferable while its fulfilment state is plain `New`.
    it('is closed on an unpaid card order that is in no closed fulfilment state', () => {
      const order = buildOrder({
        orderStatus: OrderStatus.New,
        paymentType: PaymentType.Card,
        paymentStatus: PaymentStatus.Pending,
        preferredOffer: {
          state: PreferredOfferState.Closed,
          cleanerName: 'Anna Nováková',
          canChooseAnother: false,
        },
      });

      expect(order.orderStatus.value).not.toBe(OrderStatus.Cancelled);
      expect(order.orderStatus.value).not.toBe(OrderStatus.Completed);
      expect(resolvePreferredOfferView(order).canChooseAnother).toBe(false);
    });
  });

  // A sweep read off an enum passes vacuously if the read ever returns nothing.
  it('sweeps every fulfilment state and every offer state, both ends included', () => {
    expect(ALL_ORDER_STATUSES.length).toBeGreaterThanOrEqual(7);
    expect(ALL_ORDER_STATUSES).toEqual(
      expect.arrayContaining([OrderStatus.Completed, OrderStatus.Cancelled])
    );
    expect(ALL_OFFER_STATES.length).toBeGreaterThanOrEqual(4);
    expect(ALL_OFFER_STATES).toEqual(
      expect.arrayContaining([PreferredOfferState.Closed, PreferredOfferState.Accepted])
    );
  });

  /**
   * The mutation guard for the deleted narrowing: the BLOCK is the whole answer on EVERY fulfilment
   * state, on `state` as well as on the flag. Re-introducing a status veto over either field reddens
   * the Cancelled and Completed rows. `Completed` × `Accepted` is here because it is the one true
   * sentence the server's own limb (a) withholds — a client that reproduces that judgement is the
   * second copy ADR-0049 refuses.
   */
  it.each(
    ALL_ORDER_STATUSES.flatMap((status) =>
      ALL_OFFER_STATES.map((state) => [status, state] as const)
    )
  )('re-derives no rule of its own on status %i in state %i', (status, state) => {
    const view = resolvePreferredOfferView(
      buildOrder({
        orderStatus: status,
        preferredOffer: { state, canChooseAnother: true },
      })
    );

    expect(view.state).toBe(state);
    expect(view.canChooseAnother).toBe(true);
  });
});
