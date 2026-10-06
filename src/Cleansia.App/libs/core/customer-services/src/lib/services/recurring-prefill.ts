/**
 * Prefill payload stashed by the order-detail "make this recurring" action and consumed by the wizard.
 *
 * **Only carries what survives an order-to-template translation** — selections, room counts, payment
 * type and time of day. The user still picks frequency, day, address and start date.
 * → /flows/booking-and-pricing#recurring-bookings
 */
export interface RecurringPrefillParams {
  selectedServiceIds: string[];
  selectedPackageIds: string[];
  selectedServiceNames: string[];
  selectedPackageNames: string[];
  rooms: number;
  bathrooms: number;
  paymentType: number;
  /** "HH:mm" — derived from the source order's cleaningDateTime in local TZ. */
  timeOfDay: string | null;
}

/** sessionStorage key for the Path B prefill payload. */
export const RECURRING_PREFILL_STORAGE_KEY = 'cleansia_recurring_prefill_data';
