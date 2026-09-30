import { SavedCardDto } from '@cleansia/customer-services';

/** The query parameter and outcomes Stripe's card-setup Checkout returns to the profile with. */
export const CARD_SETUP_QUERY_PARAM = 'cardSetup';
export const CARD_SETUP_SUCCESS = 'success';
export const CARD_SETUP_CANCEL = 'cancel';

export interface SavedCardRow {
  id: string;
  brand: string;
  last4: string;
  expiry: string;
  currencyCode: string;
}

export function toSavedCardRow(card: SavedCardDto): SavedCardRow {
  const brand = card.brand ?? '';
  return {
    id: card.id ?? '',
    brand: brand.charAt(0).toUpperCase() + brand.slice(1),
    last4: card.last4 ?? '',
    expiry: `${String(card.expMonth).padStart(2, '0')}/${card.expYear}`,
    currencyCode: card.currencyCode ?? '',
  };
}
