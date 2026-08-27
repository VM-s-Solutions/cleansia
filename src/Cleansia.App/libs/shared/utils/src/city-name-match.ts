/**
 * Whether a customer's typed or geocoded city is the serviced city a `ServiceCity` row names.
 *
 * **A port of the server's `Cleansia.Core.Domain.ServiceAreas.CityNameMatch`, and it has to stay
 * one.** The server is the authority: `OrderAddressResolver` runs the rule through
 * `ServiceCityRepository.CityIsServicedAsync` and refuses the booking with `city.not_serviced`.
 * This copy exists only so the customer hears it at address-selection time instead of at payment.
 *
 * **The danger in a copy is being STRICTER than the server, not looser.** A client that refuses a
 * city the server would accept tells a paying customer we do not serve them when we do — which is
 * exactly what the exact string compare this replaces did to the repo's own seeded `Plzen` address
 * against its `Plzeň` row. Looser is survivable: the customer proceeds and the server refuses,
 * which is what happened before any client check existed.
 *
 * The same table is pinned four times — `city-name-match.spec.ts`,
 * `Cleansia.Tests/Domain/ServiceAreas/CityNameMatchTests.cs`,
 * `cleansia_android/core/.../CityNameMatchTest.kt` and
 * `cleansia_ios/CleansiaCore/Tests/.../CityNameMatchTests.swift`. Change a case in one, change it
 * in all four.
 */

/**
 * A trailing 1–2 digit district, optionally followed by a quarter after a dash — `Praha 8`,
 * `Praha 4 - Chodov`, `Praha 5 – Smíchov`.
 *
 * A dash with NO leading number is deliberately not matched: `Praha-západ` and `Brno-venkov` have
 * that exact shape and are *okresy* — the rural rings around those cities, not parts of them.
 *
 * A numbered group rather than the `(?<base>…)` the C# and Kotlin originals use: this lib compiles
 * its specs at `target: es2016`, and TypeScript rejects named groups below es2018. Same reason the
 * mark class below is an explicit range rather than `\p{Mn}`.
 */
const DISTRICT_SUFFIX = /^(\S.*?)\s+\d{1,2}(?:\s*[-–—]\s*\S.*)?$/;

/** Combining marks left behind by NFD — exact for every diacritic these five markets use. */
const COMBINING_MARKS = /[̀-ͯ]/g;

const WHITESPACE_RUN = /\s+/g;

/** Trim, strip diacritics, lowercase, and collapse internal whitespace runs to one space. */
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
    .replace(WHITESPACE_RUN, ' ');
}

/** Never returns empty — a bare `8` keeps its own shape and simply matches nothing. */
function stripDistrict(folded: string): string {
  return DISTRICT_SUFFIX.exec(folded)?.[1] ?? folded;
}

/**
 * The strip runs on the CUSTOMER's string only, never on the row's name. A row named `Praha 8`
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
  return row === city || row === stripDistrict(city);
}

/** True when ANY serviced city matches — the question a screen actually asks. */
export function isCityServiced(
  servicedCityNames: readonly (string | null | undefined)[],
  customerCity: string | null | undefined,
): boolean {
  return servicedCityNames.some((name) => cityNameMatches(name, customerCity));
}
