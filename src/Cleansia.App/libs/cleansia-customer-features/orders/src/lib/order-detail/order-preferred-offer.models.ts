import { OrderItem, PreferredOfferState } from '@cleansia/customer-services';

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

  return {
    state: offer.state,
    cleanerName: offer.cleanerName ?? '',
    respondByUtc: offer.respondByUtc ?? null,
    canChooseAnother: offer.canChooseAnother === true,
  };
}
