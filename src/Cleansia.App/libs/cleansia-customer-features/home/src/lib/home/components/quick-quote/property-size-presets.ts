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
 * Deliberately an injection token rather than a literal in the component: the
 * list is per-country data, and T-0675 replaces this default with a
 * `PropertySizePreset` catalogue hung off `CountryConfiguration` — the same
 * place `TaxIdLabel` and `RegistrationNumberFormat` already live. When that
 * lands, only this provider changes.
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
