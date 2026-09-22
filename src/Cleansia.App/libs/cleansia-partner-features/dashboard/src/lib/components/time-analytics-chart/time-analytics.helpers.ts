import { localeFor } from '@cleansia/utils';

/** The worked-time axis is in minutes; one tick per hour keeps every label a distinct whole hour. */
export const HOUR_TICK_STEP_MINUTES = 60;

/** Minutes as hours, with the hour unit the session's language abbreviates ("2 h", "2 hr", "2 год"). */
export function formatHours(
  minutes: number | null | undefined,
  lang: string | undefined,
  fractionDigits = 1,
): string {
  const hours = Number.isFinite(minutes) ? (minutes as number) / 60 : 0;
  return new Intl.NumberFormat(localeFor(lang), {
    style: 'unit',
    unit: 'hour',
    unitDisplay: 'short',
    maximumFractionDigits: fractionDigits,
  }).format(hours);
}
