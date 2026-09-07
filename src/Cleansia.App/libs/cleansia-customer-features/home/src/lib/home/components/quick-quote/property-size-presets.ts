import { InjectionToken } from '@angular/core';

/**
 * One selectable property size on the home-page calculator.
 *
 * `rooms` and `bathrooms` are what the platform actually stores and prices on:
 * `Order.Rooms` / `Order.Bathrooms` are plain integers and
 * `OrderPricingCalculator` computes `BasePrice + PerRoomPrice * (rooms +
 * bathrooms)`. Nothing anywhere persists "3+kk" — that string is a Czech
 * presentation label and nothing more.
 *
 * `labelKey` therefore carries the localised label and `code` is the stable
 * identifier, so a market whose housing stock is described differently (German
 * "3-Zimmer", British "2-bed", a studio, a detached house) is a different set of
 * presets over the same two integers.
 */
export interface PropertySizePreset {
  /** Stable, never shown to a user. */
  readonly code: string;
  /** Translation key for the visible label. */
  readonly labelKey: string;
  readonly rooms: number;
  readonly bathrooms: number;
}

/**
 * Where the calculator gets its size options.
 *
 * Deliberately an injection token rather than a literal in the component, so the
 * source can change without the component knowing.
 *
 * **The backend half of T-0675 has shipped.** `PropertySizePreset` is a real
 * catalogue keyed on country, CZ and SK are seeded
 * (`sql-scripts/seed/insert_property_size_presets.sql`), and the list is served
 * anonymously from `GET /api/Country/GetPropertySizes?isoCode=&languageCode=`
 * with the label already resolved for the requested language.
 *
 * **What is still hardcoded is this factory, and only because the generated
 * client has no method for that route yet** — NSwag regeneration is owner-run
 * (see MS-14). When it lands, this factory calls
 * `customerClient.countryClient.getPropertySizes(...)` and maps `label` straight
 * onto the chip; `labelKey` and `CZ_PROPERTY_SIZE_PRESETS` below both go, because
 * the server sends text rather than a translation key. Nothing else changes: the
 * component and facade already read the token.
 */
export const PROPERTY_SIZE_PRESETS = new InjectionToken<readonly PropertySizePreset[]>(
  'PROPERTY_SIZE_PRESETS',
  {
    providedIn: 'root',
    factory: () => CZ_PROPERTY_SIZE_PRESETS,
  },
);

/** The Czech and Slovak set — the only market live today. */
export const CZ_PROPERTY_SIZE_PRESETS: readonly PropertySizePreset[] = [
  { code: 'CZ_1KK', labelKey: 'pages.home.quote.size_1kk', rooms: 1, bathrooms: 1 },
  { code: 'CZ_2KK', labelKey: 'pages.home.quote.size_2kk', rooms: 2, bathrooms: 1 },
  { code: 'CZ_3KK', labelKey: 'pages.home.quote.size_3kk', rooms: 3, bathrooms: 1 },
  { code: 'CZ_4KK', labelKey: 'pages.home.quote.size_4kk', rooms: 4, bathrooms: 2 },
  { code: 'CZ_HOUSE', labelKey: 'pages.home.quote.size_house', rooms: 5, bathrooms: 2 },
];
