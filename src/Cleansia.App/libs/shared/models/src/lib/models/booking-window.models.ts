/**
 * The daily booking window, the arrival times inside it, and how far ahead they run.
 *
 * Lives in a shared model rather than in the order wizard because two places
 * offer the same choice: the wizard's scheduling step, and the home page's price
 * calculator. The wizard is lazy-loaded, so the calculator cannot import from it
 * without pulling the whole wizard into the landing bundle — and a second copy
 * of the daily arrival times is a rule that drifts the first time one of them
 * changes.
 */
export const FIRST_WINDOW_HOUR = 8;
export const LAST_WINDOW_HOUR = 20;
/** Mirrors `BookingPolicy.SlotGridMinutes`. */
export const SLOT_GRID_MINUTES = 15;
/** Mirrors `BookingPolicy.MaxBookingHorizonDays`. */
export const MAX_BOOKING_HORIZON_DAYS = 60;

/** Minimum hours between now and cleaning start for any booking to be accepted. */
export const EXPRESS_LEAD_TIME_HOURS = 2;

/** Minimum hours for a standard (non-surcharge) booking. Slots between 2-4h lead are "express". */
export const STANDARD_LEAD_TIME_HOURS = 4;

/**
 * What an express slot adds, as a fraction. Mirrors `BookingPolicy.ExpressSurchargeRate`.
 *
 * Displayed only — the surcharge itself is always the server's arithmetic, and it
 * arrives on the quote as `expressSurchargeAmount`. This constant exists so the
 * step that offers an express slot can SAY what it costs, next to the lead-time
 * rule it already states.
 */
export const EXPRESS_SURCHARGE_RATE = 0.2;

/**
 * What the increased and heavy dirtiness levels add, as fractions. Mirror
 * `BookingPolicy.IncreasedDirtinessSurchargeRate` and `HeavyDirtinessSurchargeRate`, and are
 * displayed only, like the express rate: the surcharge itself arrives on the quote as
 * `dirtinessSurchargeAmount`.
 */
export const INCREASED_DIRTINESS_SURCHARGE_RATE = 0.15;
export const HEAVY_DIRTINESS_SURCHARGE_RATE = 0.3;

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
 * Quarter-hour starts from FIRST_WINDOW_HOUR inclusive to LAST_WINDOW_HOUR exclusive.
 * Availability is computed by the caller from the selected date and the clock.
 */
export function generateTimeOptions(): TimeOption[] {
  const options: TimeOption[] = [];
  for (
    let minute = FIRST_WINDOW_HOUR * 60;
    minute < LAST_WINDOW_HOUR * 60;
    minute += SLOT_GRID_MINUTES
  ) {
    const hour = Math.floor(minute / 60);
    const start = `${hour.toString().padStart(2, '0')}:${(minute % 60)
      .toString()
      .padStart(2, '0')}`;
    // Show only the arrival time (mobile parity). Orders can run longer than one
    // hour — "10:00 – 11:00" reads as a promise that the clean ends at 11.
    options.push({ label: start, value: start, availability: 'available' });
  }
  return options;
}

/**
 * Midnight of the last day a picker offers: the day before the one the server's horizon
 * (`BookingPolicy.IsBeyondBookingHorizon`) falls on, so every slot of every offered day is inside it.
 */
export function lastBookableDay(now: Date): Date {
  const horizon = new Date(now.getTime() + MAX_BOOKING_HORIZON_DAYS * 24 * 60 * 60 * 1000);
  return new Date(horizon.getFullYear(), horizon.getMonth(), horizon.getDate() - 1);
}
