import { localeFor } from './money-formatters.utils';

export type DateFormatStyle = 'date' | 'dateTime' | 'utcDate';

const STYLE_OPTIONS: Record<DateFormatStyle, Intl.DateTimeFormatOptions> = {
  date: { dateStyle: 'medium' },
  dateTime: { dateStyle: 'medium', timeStyle: 'short' },
  utcDate: { dateStyle: 'medium', timeZone: 'UTC' },
};

/**
 * A date the way the session's language writes it ("21. 9. 2026", "Sep 21, 2026"), with the time
 * to the minute when the caller asks for a stamp. Seconds never print: nothing an admin reads in
 * a table is decided by them, and they are what pushed timestamps onto two lines. `utcDate` is
 * for a calendar day the wire carries as midnight UTC — a legal document's effective date — which
 * read in local time would slip to the day before west of Greenwich.
 */
export function formatDate(
  value: Date | string | null | undefined,
  lang: string | undefined,
  style: DateFormatStyle = 'date',
): string {
  if (value === null || value === undefined || value === '') return '';
  const date = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(date.getTime())) return '';
  return date.toLocaleString(localeFor(lang), STYLE_OPTIONS[style]);
}
