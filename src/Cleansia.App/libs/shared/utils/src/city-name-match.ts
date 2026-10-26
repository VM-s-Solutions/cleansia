/**
 * Whether a customer's address is in a serviced city: a port of the server's
 * `Cleansia.Core.Domain.ServiceAreas.CityNameMatch`, which refuses the booking with `city.not_serviced`.
 * This copy only tells the customer earlier. A copy STRICTER than the server tells a customer we do not
 * serve them when we do, so the same case table is pinned in `city-name-match.spec.ts`,
 * `CityNameMatchTests.cs`, `CityNameMatchTest.kt` and `CityNameMatchTests.swift`.
 */

/** Combining marks left behind by NFD — exact for every diacritic these five markets use. */
const COMBINING_MARKS = /[̀-ͯ]/g;

const DASHES = /[\u2010-\u2015\u2212]/g;

const WHITESPACE_RUN = /\s+/g;

const WHITESPACE = /\s/g;

/** A trailing district: 1–2 digits (`Praha 8`) or a Roman numeral (`Pardubice II`). */
const TRAILING_DISTRICT = /^(\S.*?)\s+(?:\d{1,2}|[ivx]{1,6})$/;

const POSTCODE = /^\d{5}$/;

/** The okresy around these cities are written like a part of the city (`Praha-západ`) and are not in it. */
const OKRES_RINGS: ReadonlySet<string> = new Set([
  'praha-zapad',
  'praha-vychod',
  'brno-venkov',
  'plzen-sever',
  'plzen-jih',
]);

/** Trim, strip diacritics, lowercase, one kind of dash, and collapse whitespace runs to one space. */
function fold(value: string | null | undefined): string {
  const trimmed = (value ?? '').trim();
  if (trimmed.length === 0) {
    return '';
  }
  return trimmed
    .normalize('NFD')
    .replace(COMBINING_MARKS, '')
    .normalize('NFC')
    .toLowerCase()
    .replace(DASHES, '-')
    .replace(WHITESPACE_RUN, ' ');
}

/** The city, the city without its part after the first dash, and each of those without a trailing district. */
function candidates(city: string): string[] {
  const names = [city];
  const dash = city.indexOf('-');
  if (dash > 0) {
    const head = city.slice(0, dash).trim();
    const part = city.slice(dash + 1).trim();
    if (head.length > 0 && part.length > 0 && !OKRES_RINGS.has(`${head}-${part.split(' ')[0]}`)) {
      names.push(head);
    }
  }
  for (const name of [...names]) {
    const district = TRAILING_DISTRICT.exec(name);
    if (district) {
      names.push(district[1]);
    }
  }
  return names;
}

/**
 * The parts are removed from the CUSTOMER's city only, never from the row's name. A row named `Praha 8`
 * therefore keeps serving only Praha 8 — an operator who seeded one district meant one district.
 */
export function cityNameMatches(
  servicedCityName: string | null | undefined,
  customerCity: string | null | undefined,
): boolean {
  const row = fold(servicedCityName);
  const city = fold(customerCity);
  if (row.length === 0 || city.length === 0) {
    return false;
  }
  return candidates(city).includes(row);
}

/**
 * True when ANY serviced city takes the address. A row with a ZIP prefix decides an address that has a
 * five-digit postcode by that postcode alone, whatever the city is called; an address without one, and
 * every row without a prefix, is matched by name.
 */
export function isCityServiced(
  servicedCities: readonly { name?: string | null; zipPrefix?: string | null }[],
  customerCity: string | null | undefined,
  customerPostcode: string | null | undefined,
): boolean {
  const compact = (customerPostcode ?? '').replace(WHITESPACE, '');
  const postcode = POSTCODE.test(compact) ? compact : null;
  return servicedCities.some((row) => {
    const prefix = (row.zipPrefix ?? '').trim();
    return prefix.length > 0 && postcode !== null
      ? postcode.startsWith(prefix)
      : cityNameMatches(row.name, customerCity);
  });
}
