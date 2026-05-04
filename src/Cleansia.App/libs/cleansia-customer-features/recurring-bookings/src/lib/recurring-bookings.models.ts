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
 * Path B prefill payload — stashed in sessionStorage by the order-detail
 * "Make this recurring" CTA, consumed by the wizard on init.
 *
 * Only carries fields that survive an Order → recurring template translation:
 * services/packages, room counts, payment type, and the source order's
 * cleaning time of day (for a sensible time slot default). The user still
 * picks frequency, day-of-week, address, and start date in the wizard.
 *
 * `selectedServiceNames` / `selectedPackageNames` are passed through so the
 * wizard can show a "no longer available" warning if the catalog dropped
 * any IDs since the source order — same pattern as the order-wizard rebook.
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
  /** 1 = Cash, 2 = Card. */
  paymentType: number;
  /** Local Date for the picker; converted to ISO instant on submit. */
  startsOn: Date | null;
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
  paymentType: 1,
  startsOn: null,
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
  return (
    !!data.timeOfDay &&
    !!data.savedAddressId &&
    !!data.startsOn &&
    (data.selectedServiceIds.length > 0 || data.selectedPackageIds.length > 0)
  );
}
