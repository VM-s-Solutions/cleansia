import { localeFor } from '@cleansia/utils';

const UNIT_STEP = 1024;
const UNITS: readonly Intl.NumberFormatOptions['unit'][] = ['byte', 'kilobyte', 'megabyte', 'gigabyte'];

/**
 * A file size in the session's language. `Intl` carries the unit names and the byte plural for
 * every locale the app ships, so "1 bajt", "2 bajty", "512 bajtů" and "1,5 kB" need no keys.
 */
export function formatFileSize(bytes: number | null | undefined, lang: string | undefined): string {
  if (bytes === null || bytes === undefined || !Number.isFinite(bytes) || bytes < 0) return '';
  let value = bytes;
  let unitIndex = 0;
  while (value >= UNIT_STEP && unitIndex < UNITS.length - 1) {
    value /= UNIT_STEP;
    unitIndex++;
  }
  return new Intl.NumberFormat(localeFor(lang), {
    style: 'unit',
    unit: UNITS[unitIndex],
    unitDisplay: unitIndex === 0 ? 'long' : 'short',
    maximumFractionDigits: 1,
  }).format(value);
}

const EXTENSION = /\.([A-Za-z0-9]{1,5})$/;

/** The upper-cased extension of a file name, or nothing when the name has none. */
export function fileExtensionOf(fileName: string | null | undefined): string {
  const match = fileName?.match(EXTENSION);
  if (!match || match.index === 0) return '';
  return match[1].toUpperCase();
}
