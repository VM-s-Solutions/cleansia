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

/**
 * Late-acceptance referral input on the booking summary step. Mirrors the
 * promo state machine but tracks the inviter's first name (when the backend
 * is willing to share it) instead of a discount amount. Backend re-validates
 * at order-create time and a bad code is never a submit blocker.
 */
export type ReferralUiState =
  | { kind: 'idle' }
  | { kind: 'validating' }
  | { kind: 'valid'; referrerFirstName: string | null }
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
   * Optional referral code (late-acceptance path — covers the "forgot to enter
   * at signup" case). Backend treats this as best-effort: when the user has no
   * existing Referral row and the code validates, it creates one before order
   * persistence. Bad codes log a warning server-side and never fail the order.
   */
  referralCode: string;
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
  address: new AddressDto({ street: '', city: '', zipCode: '', countryId: '', state: '' }),
  addressLatitude: null,
  addressLongitude: null,
  cleaningDate: null,
  cleaningTime: '09:00',
  paymentType: PaymentType.Card,
  extras: {},
  specialInstructions: '',
  entryInstructions: '',
  promoCode: '',
  referralCode: '',
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
export const FIRST_WINDOW_HOUR = 8;
export const LAST_WINDOW_HOUR = 20;

/** Minimum hours between now and cleaning start for any booking to be accepted. */
export const EXPRESS_LEAD_TIME_HOURS = 2;

/** Minimum hours for a standard (non-surcharge) booking. Slots between 2–4h lead are "express". */
export const STANDARD_LEAD_TIME_HOURS = 4;

/** Surcharge applied to base price for express slots (2–4h lead time). */
export const EXPRESS_SURCHARGE_RATE = 0.20;

export type SlotAvailability = 'available' | 'express' | 'unavailable';

export interface TimeOption {
  /** Display label — start time only, e.g. "10:00". Matches mobile; hides the window. */
  label: string;
  /** Canonical value — start time as "HH:mm" (used for backend submission) */
  value: string;
  /** Whether the slot is bookable, requires express surcharge, or out of range. */
  availability?: SlotAvailability;
}

/**
 * Produce one option per 1-hour window from FIRST_WINDOW_HOUR to LAST_WINDOW_HOUR.
 * Availability is computed elsewhere based on the selected date + current time.
 */
export function generateTimeOptions(): TimeOption[] {
  const options: TimeOption[] = [];
  for (let h = FIRST_WINDOW_HOUR; h < LAST_WINDOW_HOUR; h++) {
    const start = `${h.toString().padStart(2, '0')}:00`;
    // Show only the arrival time (mobile parity). Orders can run longer than
    // one hour — displaying "10:00 – 11:00" misleads users into thinking the
    // cleaning ends at 11:00.
    options.push({
      label: start,
      value: start,
      availability: 'available',
    });
  }
  return options;
}

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
    if (hoursAhead < EXPRESS_LEAD_TIME_HOURS) availability = 'unavailable';
    else if (hoursAhead < STANDARD_LEAD_TIME_HOURS) availability = 'express';
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
