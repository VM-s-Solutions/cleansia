import { PaymentType } from '@cleansia/customer-services';
import type { CashEligibility } from '@cleansia/models';

/**
 * Frequency enum mirroring backend `RecurrenceFrequency`. Persisted as an int
 * over the wire — don't reorder. Names + values must match the Kotlin enum on
 * mobile and the C# enum on the backend.
 */
export enum RecurrenceFrequency {
  Weekly = 1,
  Biweekly = 2,
  Monthly = 3,
}

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

/** UI-side state for the create-recurring wizard. Mirrors the mobile shape. */
export interface RecurringWizardFormData {
  frequency: RecurrenceFrequency;
  /** .NET DayOfWeek (Sun=0..Sat=6). Default Thursday — mid-week, low conflict. */
  dayOfWeek: number;
  /** "HH:mm" 24h. */
  timeOfDay: string;
  rooms: number;
  bathrooms: number;
  savedAddressId: string | null;
  selectedServiceIds: string[];
  selectedPackageIds: string[];
  /** Null once a cash choice was taken away and the customer has not chosen again. */
  paymentType: PaymentType | null;
  /** Local Date for the picker; converted to ISO instant on submit. */
  startsOn: Date | null;
  /**
   * Not editable here, but carried from the schedule being edited: the update replaces every field
   * it is sent, so leaving either off clears it.
   */
  endsOn: Date | null;
  preferredEmployeeId: string | null;
}

export const RECURRING_WIZARD_INITIAL_DATA: RecurringWizardFormData = {
  frequency: RecurrenceFrequency.Weekly,
  dayOfWeek: 4, // Thursday — mid-week default (matches mobile)
  timeOfDay: '10:00',
  rooms: 2,
  bathrooms: 1,
  savedAddressId: null,
  selectedServiceIds: [],
  selectedPackageIds: [],
  paymentType: PaymentType.Card,
  startsOn: null,
  endsOn: null,
  preferredEmployeeId: null,
};

/**
 * Hour-grouped time slots — Morning / Afternoon / Evening. Same windows as the
 * mobile picker (08–11, 12–16, 17–19). Web renders these grouped under labels
 * with sun/sun/moon glyphs, matching mobile parity.
 */
export const TIME_PERIOD_GROUPS: ReadonlyArray<{
  labelKey: string;
  iconClass: string;
  slots: string[];
}> = [
  {
    labelKey: 'recurring_booking.time_period_morning',
    iconClass: 'pi pi-sun',
    slots: ['08:00', '09:00', '10:00', '11:00'],
  },
  {
    labelKey: 'recurring_booking.time_period_afternoon',
    iconClass: 'pi pi-sun',
    slots: ['12:00', '13:00', '14:00', '15:00', '16:00'],
  },
  {
    labelKey: 'recurring_booking.time_period_evening',
    iconClass: 'pi pi-moon',
    slots: ['17:00', '18:00', '19:00'],
  },
];

/**
 * Day-of-week chip definitions in display order (Mon → Sun). The numeric value
 * is .NET DayOfWeek (Sun=0..Sat=6); convert from JS Date.getDay() the same way.
 * `isWeekend` drives the visual gap + tint applied in the template.
 */
export const DAY_OF_WEEK_CHIPS: ReadonlyArray<{
  value: number;
  shortKey: string;
  fullKey: string;
  isWeekend: boolean;
}> = [
  { value: 1, shortKey: 'recurring_booking.day_short_mon', fullKey: 'recurring_booking.day_full_mon', isWeekend: false },
  { value: 2, shortKey: 'recurring_booking.day_short_tue', fullKey: 'recurring_booking.day_full_tue', isWeekend: false },
  { value: 3, shortKey: 'recurring_booking.day_short_wed', fullKey: 'recurring_booking.day_full_wed', isWeekend: false },
  { value: 4, shortKey: 'recurring_booking.day_short_thu', fullKey: 'recurring_booking.day_full_thu', isWeekend: false },
  { value: 5, shortKey: 'recurring_booking.day_short_fri', fullKey: 'recurring_booking.day_full_fri', isWeekend: false },
  { value: 6, shortKey: 'recurring_booking.day_short_sat', fullKey: 'recurring_booking.day_full_sat', isWeekend: true },
  { value: 0, shortKey: 'recurring_booking.day_short_sun', fullKey: 'recurring_booking.day_full_sun', isWeekend: true },
];

/**
 * Frequency option metadata for the wizard's first step. Subline copy
 * mirrors the mobile cadence hints; biweekly gets a "most popular" badge.
 */
export interface FrequencyOption {
  value: RecurrenceFrequency;
  labelKey: string;
  sublineKey: string;
  badgeKey: string | null;
}

export const FREQUENCY_OPTIONS: ReadonlyArray<FrequencyOption> = [
  {
    value: RecurrenceFrequency.Weekly,
    labelKey: 'recurring_booking.freq_weekly_label',
    sublineKey: 'recurring_booking.freq_weekly_subline',
    badgeKey: null,
  },
  {
    value: RecurrenceFrequency.Biweekly,
    labelKey: 'recurring_booking.freq_biweekly_label',
    sublineKey: 'recurring_booking.freq_biweekly_subline',
    badgeKey: 'recurring_booking.freq_most_popular_badge',
  },
  {
    value: RecurrenceFrequency.Monthly,
    labelKey: 'recurring_booking.freq_monthly_label',
    sublineKey: 'recurring_booking.freq_monthly_subline',
    badgeKey: null,
  },
];

/**
 * Step-wise validation. Returns true when the user has filled the minimum
 * fields required to advance from this step. Final-step submit re-checks
 * everything together via [canSubmit].
 */
export function canAdvance(step: number, data: RecurringWizardFormData): boolean {
  switch (step) {
    case 1:
      return !!data.timeOfDay; // frequency + day always have defaults
    case 2:
      return data.selectedServiceIds.length > 0 || data.selectedPackageIds.length > 0;
    case 3:
      return !!data.savedAddressId && !!data.startsOn;
    default:
      return false;
  }
}

export function canSubmit(data: RecurringWizardFormData): boolean {
  return missingFields(data).length === 0;
}

/**
 * The next instant this template will be materialized for, or `null` when the
 * schedule has run out.
 *
 * This is a LINE-BY-LINE mirror of the backend's own derivation —
 * `MaterializeRecurringBookingTemplate.ComputeOccurrences` — and it has to
 * stay one: the card states a date the customer will plan around, and a second
 * opinion about the cadence is worse than no date at all. The schedule's day
 * and time are wall-clock time where the home is, so the walk runs over dates
 * in that zone and each date is converted to an instant on its own.
 *
 * The template carries no zone, so the reader's stands in for the market's —
 * the same zone the wizard's "HH:mm" was typed in.
 *
 * `lastMaterializedFor` only moves the START of the search forward — it is not
 * a duplicate guard here any more than it is there.
 */
export function nextOccurrenceUtc(
  template: {
    frequency: number;
    dayOfWeek: number;
    timeOfDay?: string;
    startsOn: Date | string;
    endsOn?: Date | string;
    lastMaterializedFor?: Date | string;
  },
  now: Date = new Date(),
  timeZone: string = Intl.DateTimeFormat().resolvedOptions().timeZone,
): Date | null {
  const stepDays =
    template.frequency === RecurrenceFrequency.Biweekly
      ? 14
      : template.frequency === RecurrenceFrequency.Monthly
        ? 30 // the backend's own approximation — mirrored, not corrected
        : 7;

  const asDate = (v: Date | string | undefined): Date | null => {
    if (!v) return null;
    const d = v instanceof Date ? v : new Date(v);
    return Number.isNaN(d.getTime()) ? null : d;
  };

  const startsOn = asDate(template.startsOn);
  if (!startsOn) return null;
  const endsOn = asDate(template.endsOn);
  const lastMaterializedFor = asDate(template.lastMaterializedFor);

  // Market dates are held as midnight-UTC stand-ins, so day arithmetic is exact.
  let searchDate = lastMaterializedFor
    ? addDays(marketDate(lastMaterializedFor, timeZone), stepDays)
    : marketDate(startsOn, timeZone);
  const today = marketDate(now, timeZone);
  if (searchDate.getTime() < today.getTime()) searchDate = today;

  while (searchDate.getUTCDay() !== template.dayOfWeek) {
    searchDate = addDays(searchDate, 1);
  }

  const [hours, minutes] = (template.timeOfDay ?? '00:00').split(':');
  const hour = Number(hours) || 0;
  const minute = Number(minutes) || 0;

  // The backend yields only occurrences inside [startsOn, endsOn]; anything
  // earlier steps forward. Bounded so a malformed template cannot spin.
  for (let i = 0; i < 64; i++) {
    const occurrence = marketTimeToUtc(searchDate, hour, minute, timeZone);
    if (endsOn && occurrence.getTime() > endsOn.getTime()) return null;
    if (occurrence.getTime() >= startsOn.getTime()) return occurrence;
    searchDate = addDays(searchDate, stepDays);
  }
  return null;
}

function addDays(date: Date, days: number): Date {
  return new Date(Date.UTC(date.getUTCFullYear(), date.getUTCMonth(), date.getUTCDate() + days));
}

/** The wall clock in `timeZone` at `instant`, as if it were UTC. */
function wallClock(instant: Date, timeZone: string): Date {
  const parts = new Intl.DateTimeFormat('en-US', {
    timeZone,
    hourCycle: 'h23',
    year: 'numeric',
    month: 'numeric',
    day: 'numeric',
    hour: 'numeric',
    minute: 'numeric',
  }).formatToParts(instant);
  const part = (type: Intl.DateTimeFormatPartTypes) =>
    Number(parts.find((p) => p.type === type)?.value ?? 0);
  return new Date(
    Date.UTC(part('year'), part('month') - 1, part('day'), part('hour'), part('minute')),
  );
}

function marketDate(instant: Date, timeZone: string): Date {
  const local = wallClock(instant, timeZone);
  return new Date(Date.UTC(local.getUTCFullYear(), local.getUTCMonth(), local.getUTCDate()));
}

/**
 * The instant at which `timeZone` reads `hour:minute` on `date`, the way .NET's
 * `ConvertTimeToUtc` reads it: a time the autumn change repeats is the later,
 * standard-time one, and a time the spring change skips reads with the offset
 * in force before the gap, which moves it forward by the gap.
 */
function marketTimeToUtc(date: Date, hour: number, minute: number, timeZone: string): Date {
  const day = 24 * 60 * 60 * 1000;
  const asUtc = Date.UTC(date.getUTCFullYear(), date.getUTCMonth(), date.getUTCDate(), hour, minute);
  const offsetAt = (ms: number) =>
    wallClock(new Date(ms), timeZone).getTime() - Math.floor(ms / 60000) * 60000;
  const candidates = [asUtc - offsetAt(asUtc - day), asUtc - offsetAt(asUtc + day)];
  const exact = candidates.filter((ms) => wallClock(new Date(ms), timeZone).getTime() === asUtc);
  return new Date(exact.length > 0 ? Math.max(...exact) : candidates[0]);
}

/** A required field the form is still missing, in the order the form asks. */
export type MissingField = 'services' | 'time' | 'address' | 'startsOn' | 'payment';

/**
 * What is stopping this schedule from being saved.
 *
 * `canSubmit` answers yes/no, which is all a disabled button needs and nothing
 * a person does. The form presses a live button, gets this list back and says
 * which field to look at — the previous behaviour was a dead button and a page
 * that appeared to ignore the click.
 */
export function missingFields(data: RecurringWizardFormData): MissingField[] {
  const missing: MissingField[] = [];
  if (data.selectedServiceIds.length === 0 && data.selectedPackageIds.length === 0) {
    missing.push('services');
  }
  if (!data.timeOfDay) missing.push('time');
  if (!data.savedAddressId) missing.push('address');
  if (!data.startsOn) missing.push('startsOn');
  if (data.paymentType === null) missing.push('payment');
  return missing;
}

/** What a quote prices. The day, the time and the way to pay move no money. */
export interface PricedSelection {
  serviceIds: string[];
  packageIds: string[];
  rooms: number;
  bathrooms: number;
  countryId: string | null;
}

export function samePricedSelection(a: PricedSelection, b: PricedSelection): boolean {
  const sameIds = (x: string[], y: string[]) => x.length === y.length && x.every((id, i) => id === y[i]);
  return (
    sameIds(a.serviceIds, b.serviceIds) &&
    sameIds(a.packageIds, b.packageIds) &&
    a.rooms === b.rooms &&
    a.bathrooms === b.bathrooms &&
    a.countryId === b.countryId
  );
}

/** Why a schedule cannot be paid in cash right now, or null when it can. */
export function scheduleCashReason(
  eligibility: CashEligibility,
): { key: string; params: Record<string, number> } | null {
  switch (eligibility.kind) {
    case 'needs_card':
      return { key: 'recurring_booking.cash_needs_card', params: { count: eligibility.requiredCleaners } };
    case 'pending':
      return { key: 'recurring_booking.cash_pending', params: {} };
    default:
      return null;
  }
}

type ScheduleState = { isActive: boolean; requiresPaymentMethodChange?: boolean };

/** A paused schedule books nothing, and neither does a cash one the server now skips until it is changed. */
export function scheduleBooksCleanings(template: ScheduleState): boolean {
  return template.isActive && !template.requiresPaymentMethodChange;
}

export function scheduleStatusKey(template: ScheduleState): string {
  if (!template.isActive) return 'recurring_booking.paused_badge';
  return template.requiresPaymentMethodChange
    ? 'recurring_booking.status_needs_change'
    : 'recurring_booking.status_active';
}
