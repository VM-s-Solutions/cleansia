import { AddressDto, PackageListItem, PackageServiceSummary, PaymentType, ServiceListItem } from '@cleansia/customer-services';
import { TranslateService } from '@ngx-translate/core';

/**
 * Discriminated union representing the live state of promo-code validation in
 * the booking wizard. The facade owns this signal; the summary step renders
 * based on `kind`. Backend re-validates server-side at order-create time so
 * this is purely a UX optimization (instant green-check / red-X feedback).
 */
export type PromoCodeUiState =
  | { kind: 'idle' }
  | { kind: 'validating' }
  | { kind: 'valid'; discount: number }
  | { kind: 'invalid'; error: string | null };

export interface RebookParams {
  selectedServiceIds: string[];
  selectedPackageIds: string[];
  selectedServiceNames: string[];
  selectedPackageNames: string[];
  rooms: number;
  bathrooms: number;
  address?: { street: string; city: string; zipCode: string; countryId: string; state: string };
}

export interface OrderWizardFormData {
  selectedServiceIds: string[];
  selectedPackageIds: string[];
  rooms: number;
  bathrooms: number;
  customerFirstName: string;
  customerLastName: string;
  customerEmail: string;
  customerPhone: string;
  address: AddressDto;
  /**
   * Lat/lng captured from a Mapbox autocomplete pick (or null if the user
   * typed manually). Forwarded to the backend only when the customer also
   * chooses "Save this address" so cleaners get accurate routing.
   */
  addressLatitude: number | null;
  addressLongitude: number | null;
  /**
   * A house has no floor and no flat number, and asking for them reads as a form
   * that was not written for you. This drives which fields the address step
   * shows; it is not sent anywhere — the two values it gates are.
   */
  propertyType: 'flat' | 'house';
  /**
   * On the ORDER, not on Address: addresses dedupe across users, so a flat
   * number stored there would leak between neighbours at the same street
   * address. → src/Cleansia.Core.Domain/Orders/Order.cs
   */
  customerFloor: string;
  customerApartment: string;
  cleaningDate: Date | null;
  cleaningTime: string;
  paymentType: PaymentType;
  extras: Record<string, boolean>;
  specialInstructions: string;
  entryInstructions: string;
  /**
   * Optional promo code entered by the customer at the summary step. The actual
   * validation state lives on the facade as a signal — this string only carries
   * the raw user input through the form model so it can be persisted/echoed.
   * Backend re-validates and applies the discount inside CreateOrder.Handler.
   */
  promoCode: string;
  /**
   * The cleaner the customer asked for — a Cleansia Plus perk, gated server-side on an active
   * membership plus a previously completed order with that cleaner. It buys them the first offer,
   * not the job: whether the platform could withhold a seat is the server's answer and lives on the
   * order, not here.
   */
  preferredEmployeeId: string | null;
}

/**
 * Construct-then-assign (ADR-0031): an `AddressDto` object literal is
 * required-key checked, so every one of them breaks at the next NSwag regen.
 * Every wizard surface that builds an address goes through here, blanks and all.
 */
export function createAddressDto(
  fields: {
    street?: string;
    city?: string;
    zipCode?: string;
    countryId?: string;
    state?: string;
  } = {}
): AddressDto {
  const address = new AddressDto();
  address.street = fields.street ?? '';
  address.city = fields.city ?? '';
  address.zipCode = fields.zipCode ?? '';
  address.countryId = fields.countryId ?? '';
  address.state = fields.state ?? '';
  return address;
}

export const ORDER_WIZARD_INITIAL_DATA: OrderWizardFormData = {
  selectedServiceIds: [],
  selectedPackageIds: [],
  rooms: 1,
  bathrooms: 1,
  customerFirstName: '',
  customerLastName: '',
  customerEmail: '',
  customerPhone: '',
  address: createAddressDto(),
  addressLatitude: null,
  addressLongitude: null,
  cleaningDate: null,
  cleaningTime: '09:00',
  paymentType: PaymentType.Card,
  extras: {},
  specialInstructions: '',
  entryInstructions: '',
  propertyType: 'flat',
  customerFloor: '',
  customerApartment: '',
  promoCode: '',
  preferredEmployeeId: null,
};

// ── Validation ──────────────────────────────────────────────

const EMAIL_REGEX = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const PHONE_REGEX = /^[+]?[\d\s()-]{6,20}$/;
const ZIP_REGEX = /^[\d\s-]{3,20}$/;

export function getFieldError(
  field: string,
  data: OrderWizardFormData,
  translate: TranslateService
): string | null {
  switch (field) {
    case 'customerFirstName':
      if (!data.customerFirstName) return translate.instant('global.validation.required');
      if (data.customerFirstName.length < 2) return translate.instant('global.validation.minlength', { min: 2 });
      if (data.customerFirstName.length > 50) return translate.instant('global.validation.maxlength', { max: 50 });
      return null;
    case 'customerLastName':
      if (!data.customerLastName) return translate.instant('global.validation.required');
      if (data.customerLastName.length < 2) return translate.instant('global.validation.minlength', { min: 2 });
      if (data.customerLastName.length > 50) return translate.instant('global.validation.maxlength', { max: 50 });
      return null;
    case 'customerEmail':
      if (!data.customerEmail) return translate.instant('global.validation.required');
      if (!EMAIL_REGEX.test(data.customerEmail)) return translate.instant('global.validation.email');
      if (data.customerEmail.length > 50) return translate.instant('global.validation.maxlength', { max: 50 });
      return null;
    case 'customerPhone':
      if (!data.customerPhone) return translate.instant('global.validation.required');
      if (!PHONE_REGEX.test(data.customerPhone.replace(/\s/g, ''))) return translate.instant('global.validation.phone');
      return null;
    case 'street':
      if (!data.address.street) return translate.instant('global.validation.required');
      if (data.address.street.length < 5) return translate.instant('global.validation.minlength', { min: 5 });
      if (data.address.street.length > 255) return translate.instant('global.validation.maxlength', { max: 255 });
      return null;
    case 'city':
      if (!data.address.city) return translate.instant('global.validation.required');
      if (data.address.city.length < 2) return translate.instant('global.validation.minlength', { min: 2 });
      if (data.address.city.length > 100) return translate.instant('global.validation.maxlength', { max: 100 });
      return null;
    case 'zipCode':
      if (!data.address.zipCode) return translate.instant('global.validation.required');
      if (!ZIP_REGEX.test(data.address.zipCode)) return translate.instant('global.validation.zip');
      return null;
    default:
      return null;
  }
}

// ── Time helpers ────────────────────────────────────────────
//
// Time slots are 1-hour arrival windows (e.g., 10:00–11:00). Customer picks a
// window; internally we still schedule on the 30-min grid (target start is the
// window's start). See Cleansia.Core.AppServices.Features.Orders.BookingPolicy
// on the backend for the authoritative numbers — keep these in sync.

/** Window duration shown to the customer. Keep in sync with backend BookingPolicy. */
export const WINDOW_DURATION_MINUTES = 60;

/** Earliest and latest starting hours for bookable windows (inclusive start, exclusive end). */
import {
  EXPRESS_LEAD_TIME_HOURS as SHARED_EXPRESS_LEAD_TIME_HOURS,
  FIRST_WINDOW_HOUR as SHARED_FIRST_WINDOW_HOUR,
  LAST_WINDOW_HOUR as SHARED_LAST_WINDOW_HOUR,
  STANDARD_LEAD_TIME_HOURS as SHARED_STANDARD_LEAD_TIME_HOURS,
} from '@cleansia/models';
import type { SlotAvailability, TimeOption } from '@cleansia/models';

// The window, its option type and the generator moved to @cleansia/models: the
// home-page calculator offers the same choice and cannot import from this lib,
// which is lazy-loaded. Re-exported so every existing import here still resolves.
export { generateTimeOptions } from '@cleansia/models';
export type { SlotAvailability, TimeOption } from '@cleansia/models';
export const FIRST_WINDOW_HOUR = SHARED_FIRST_WINDOW_HOUR;
export const LAST_WINDOW_HOUR = SHARED_LAST_WINDOW_HOUR;
export const EXPRESS_LEAD_TIME_HOURS = SHARED_EXPRESS_LEAD_TIME_HOURS;
export const STANDARD_LEAD_TIME_HOURS = SHARED_STANDARD_LEAD_TIME_HOURS;

/**
 * Annotate time options with availability based on the selected date and lead-time rules.
 *  - Slots starting less than EXPRESS_LEAD_TIME_HOURS away → "unavailable"
 *  - Slots 2–4h away → "express" (bookable with surcharge)
 *  - All other future slots → "available"
 * For future dates (not today), all slots are "available".
 */
export function filterTimeOptionsForToday(
  allOptions: TimeOption[],
  selectedDate: Date | null
): TimeOption[] {
  if (!selectedDate) return allOptions;

  const now = new Date();
  const isToday =
    selectedDate.getFullYear() === now.getFullYear() &&
    selectedDate.getMonth() === now.getMonth() &&
    selectedDate.getDate() === now.getDate();

  if (!isToday) {
    // Future date — nothing is within lead time.
    return allOptions.map((opt) => ({ ...opt, availability: 'available' as const }));
  }

  const nowMs = now.getTime();
  return allOptions.map((opt) => {
    const [h, m] = opt.value.split(':').map(Number);
    const slotDate = new Date(selectedDate);
    slotDate.setHours(h, m, 0, 0);
    const hoursAhead = (slotDate.getTime() - nowMs) / (1000 * 60 * 60);

    let availability: SlotAvailability = 'available';
    if (hoursAhead < SHARED_EXPRESS_LEAD_TIME_HOURS) availability = 'unavailable';
    else if (hoursAhead < SHARED_STANDARD_LEAD_TIME_HOURS) availability = 'express';
    return { ...opt, availability };
  });
}

// ── Price formatting ────────────────────────────────────────

const CZK_FORMATTER = new Intl.NumberFormat('cs-CZ', {
  style: 'currency',
  currency: 'CZK',
  minimumFractionDigits: 0,
});

export function formatPrice(price: number): string {
  return CZK_FORMATTER.format(price);
}

/**
 * Final charge for a discount `/Order/Quote` could not price for us.
 *
 * `QuoteOrderResponse.finalPriceAfterDiscount` is the charged figure for the tier/membership pair and
 * is read verbatim. `QuoteOrderCommand` carries no promo code, so a promo that beats that pair has no
 * quoted price and only this reproduces how the server composes one: `BookingPolicy` discounts the RAW
 * pre-surcharge subtotal and grosses the remainder up — `(raw - d) * 1.2`, never `raw * 1.2 - d`.
 *
 * The gross-up factor is read back out of the quote's own two totals so no copy of the express rate
 * lives on the client; with no surcharge `grossSubtotal === rawSubtotal` and the factor is 1. Multiply
 * before dividing, and round to cents, so the ratio cannot surface as binary dust in the price.
 */
export function composeFinalPriceForUnquotedDiscount(
  rawSubtotal: number,
  grossSubtotal: number,
  discount: number,
): number {
  if (rawSubtotal <= 0) return 0;
  const discountedSubtotal = Math.max(0, rawSubtotal - discount);
  return Math.round((discountedSubtotal * grossSubtotal * 100) / rawSubtotal) / 100;
}

// ── Translation helpers ─────────────────────────────────────

export function getItemTranslation(
  item: ServiceListItem | PackageListItem | PackageServiceSummary,
  field: string,
  translate: TranslateService
): string {
  const lang = translate.currentLang || translate.getDefaultLang();
  const translations = item.translations;
  if (translations && translations[lang]) {
    const translated = (translations[lang] as unknown as Record<string, string>)[field];
    if (translated) return translated;
  }
  return (item as unknown as Record<string, string>)[field] || '';
}
