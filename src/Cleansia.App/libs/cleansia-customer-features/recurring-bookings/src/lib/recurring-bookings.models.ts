import { DirtinessLevel, PaymentType } from '@cleansia/customer-services';
import { CashEligibility, EXPRESS_LEAD_TIME_HOURS } from '@cleansia/models';

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

export { RECURRING_PREFILL_STORAGE_KEY } from '@cleansia/customer-services';
export type { RecurringPrefillParams } from '@cleansia/customer-services';

/** UI-side state for the create-recurring wizard. Mirrors the mobile shape. */
export interface RecurringWizardFormData {
  frequency: RecurrenceFrequency;
  /** .NET DayOfWeek (Sun=0..Sat=6). Default Thursday — mid-week, low conflict. */
  dayOfWeek: number;
  /** "HH:mm" 24h. */
  timeOfDay: string;
  rooms: number;
  bathrooms: number;
  /** Null on a new schedule until the customer picks one: the level is an active choice. */
  dirtinessLevel: DirtinessLevel | null;
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
  /** One request covers every cleaning the schedule books; asked only when it is set up. */
  earlyPerformanceRequested: boolean;
  /** Read only while the facade asks for the tick. */
  termsAccepted: boolean;
}

export const RECURRING_WIZARD_INITIAL_DATA: RecurringWizardFormData = {
  frequency: RecurrenceFrequency.Weekly,
  dayOfWeek: 4, // Thursday — mid-week default (matches mobile)
  timeOfDay: '10:00',
  rooms: 2,
  bathrooms: 1,
  dirtinessLevel: null,
  savedAddressId: null,
  selectedServiceIds: [],
  selectedPackageIds: [],
  paymentType: PaymentType.Card,
  startsOn: null,
  endsOn: null,
  preferredEmployeeId: null,
  earlyPerformanceRequested: false,
  termsAccepted: false,
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
 * Computed the way the backend's `MaterializeRecurringBookingTemplate.ComputeOccurrences`
 * computes it, because the card states a date the customer will plan around. The
 * schedule's day and time are wall-clock time in the market's zone, which the
 * template names; the reader's zone stands in only when it does not.
 *
 * Every cadence is anchored on the first chosen weekday on or after `startsOn`, so
 * an edit — which clears `lastMaterializedFor` — keeps a fortnightly schedule on its
 * weeks and a monthly one on its nth weekday.
 */
export function nextOccurrenceUtc(
  template: {
    frequency: number;
    dayOfWeek: number;
    timeOfDay?: string;
    startsOn: Date | string;
    endsOn?: Date | string;
    lastMaterializedFor?: Date | string;
    timeZoneId?: string | null;
  },
  now: Date = new Date(),
  timeZone: string = template.timeZoneId || Intl.DateTimeFormat().resolvedOptions().timeZone,
): Date | null {
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
  const startDate = marketDate(startsOn, timeZone);
  const anchor = addDays(startDate, (template.dayOfWeek - startDate.getUTCDay() + 7) % 7);
  const today = marketDate(now, timeZone);
  let from = lastMaterializedFor ? addDays(marketDate(lastMaterializedFor, timeZone), 1) : anchor;
  if (from.getTime() < today.getTime()) from = today;

  const dates =
    template.frequency === RecurrenceFrequency.Monthly
      ? monthlyDates(anchor, from, template.dayOfWeek)
      : stepDates(anchor, from, template.frequency === RecurrenceFrequency.Biweekly ? 14 : 7);

  const [hours, minutes] = (template.timeOfDay ?? '00:00').split(':');
  const hour = Number(hours) || 0;
  const minute = Number(minutes) || 0;
  const earliest = now.getTime() + EXPRESS_LEAD_TIME_HOURS * 60 * 60 * 1000;

  // Bounded so a malformed template cannot spin.
  for (let i = 0; i < 64; i++) {
    const occurrence = marketTimeToUtc(dates.next().value, hour, minute, timeZone);
    if (endsOn && occurrence.getTime() > endsOn.getTime()) return null;
    if (occurrence.getTime() >= earliest && occurrence.getTime() >= startsOn.getTime()) return occurrence;
  }
  return null;
}

function* stepDates(anchor: Date, from: Date, stepDays: number): Generator<Date, never> {
  const days = Math.round((from.getTime() - anchor.getTime()) / (24 * 60 * 60 * 1000));
  let date = addDays(anchor, Math.max(0, Math.ceil(days / stepDays)) * stepDays);
  while (true) {
    yield date;
    date = addDays(date, stepDays);
  }
}

/** The nth weekday of each month, n read off the anchor; a 5th means the last, since most months have none. */
function* monthlyDates(anchor: Date, from: Date, dayOfWeek: number): Generator<Date, never> {
  const ordinal = Math.floor((anchor.getUTCDate() - 1) / 7) + 1;
  let month = from.getUTCMonth();
  const year = from.getUTCFullYear();
  while (true) {
    const firstOfMonth = new Date(Date.UTC(year, month, 1));
    const first = addDays(firstOfMonth, (dayOfWeek - firstOfMonth.getUTCDay() + 7) % 7);
    const fifth = addDays(first, 28);
    const date =
      ordinal < 5
        ? addDays(first, 7 * (ordinal - 1))
        : fifth.getUTCMonth() === firstOfMonth.getUTCMonth()
          ? fifth
          : addDays(first, 21);
    if (date.getTime() >= from.getTime()) yield date;
    month++;
  }
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
export type MissingField =
  | 'services'
  | 'dirtiness'
  | 'time'
  | 'address'
  | 'startsOn'
  | 'payment'
  | 'terms'
  | 'earlyPerformance';

/**
 * What is stopping this schedule from being saved.
 *
 * `canSubmit` answers yes/no, which is all a disabled button needs and nothing
 * a person does. The form presses a live button, gets this list back and says
 * which field to look at — the previous behaviour was a dead button and a page
 * that appeared to ignore the click.
 */
export function missingFields(
  data: RecurringWizardFormData,
  newSchedule = false,
  termsAsked = false,
): MissingField[] {
  const missing: MissingField[] = [];
  if (data.selectedServiceIds.length === 0 && data.selectedPackageIds.length === 0) {
    missing.push('services');
  }
  if (data.dirtinessLevel === null) missing.push('dirtiness');
  if (!data.timeOfDay) missing.push('time');
  if (!data.savedAddressId) missing.push('address');
  if (!data.startsOn) missing.push('startsOn');
  if (data.paymentType === null) missing.push('payment');
  if (termsAsked && !data.termsAccepted) missing.push('terms');
  if (newSchedule && !data.earlyPerformanceRequested) missing.push('earlyPerformance');
  return missing;
}

/** What a quote prices. The day, the time and the way to pay move no money. */
export interface PricedSelection {
  serviceIds: string[];
  packageIds: string[];
  rooms: number;
  bathrooms: number;
  dirtinessLevel: DirtinessLevel;
  countryId: string | null;
}

export function samePricedSelection(a: PricedSelection, b: PricedSelection): boolean {
  const sameIds = (x: string[], y: string[]) => x.length === y.length && x.every((id, i) => id === y[i]);
  return (
    sameIds(a.serviceIds, b.serviceIds) &&
    sameIds(a.packageIds, b.packageIds) &&
    a.rooms === b.rooms &&
    a.bathrooms === b.bathrooms &&
    a.dirtinessLevel === b.dirtinessLevel &&
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
