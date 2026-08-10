import { OrderItem, OrderStatus, PreferredOfferState } from '@cleansia/customer-services';

/**
 * What the customer's order detail says about the cleaner they asked for. Every field is the
 * server's own answer; nothing here re-derives a policy the platform owns.
 */
export interface PreferredOfferView {
  state: PreferredOfferState;
  cleanerName: string;
  respondByUtc: Date | null;
  canChooseAnother: boolean;
}

/**
 * Fulfilment states in which asking for another cleaner can be accepted and still do nothing.
 * `PreferredOfferExit.IsOpen` gates on the reservation, the round count, the assignment and the
 * lead time — it carries no offerability term, so a cancelled order with a future cleaning time and
 * no cleaner on it answers `true`. Taking that request would reserve a seat on work nobody will do
 * and push a named cleaner about it.
 */
const CLOSED_TO_A_NEW_REQUEST: ReadonlySet<OrderStatus> = new Set([
  OrderStatus.Cancelled,
  OrderStatus.Completed,
]);

export function resolvePreferredOfferView(order: OrderItem | null): PreferredOfferView {
  const offer = order?.preferredOffer;
  if (!offer) {
    return {
      state: PreferredOfferState.None,
      cleanerName: '',
      respondByUtc: null,
      canChooseAnother: false,
    };
  }

  const orderStatus = order?.orderStatus?.value as OrderStatus | undefined;

  return {
    state: offer.state,
    cleanerName: offer.cleanerName ?? '',
    respondByUtc: offer.respondByUtc ?? null,
    canChooseAnother:
      offer.canChooseAnother === true &&
      (orderStatus === undefined || !CLOSED_TO_A_NEW_REQUEST.has(orderStatus)),
  };
}
