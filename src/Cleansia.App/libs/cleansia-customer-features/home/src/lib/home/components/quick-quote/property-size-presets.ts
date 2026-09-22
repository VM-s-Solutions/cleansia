/**
 * One selectable property size on the home-page calculator.
 *
 * `rooms` and `bathrooms` are what the platform actually stores and prices on:
 * `Order.Rooms` / `Order.Bathrooms` are plain integers and
 * `OrderPricingCalculator` computes `BasePrice + PerRoomPrice * (rooms +
 * bathrooms)`. Nothing anywhere persists "3+kk" — that string is a Czech
 * presentation label and nothing more.
 *
 * The set is per market and served by `GET /api/Country/GetPropertySizes`
 * with the label already resolved for the requested language, so a market
 * whose housing stock is described differently (German "3-Zimmer", British
 * "2-bed", a studio, a detached house) is a different set of rows over the
 * same two integers. → /decisions/adr-0056
 */
export interface PropertySizePreset {
  /** Stable, never shown to a user. */
  readonly code: string;
  /** Already resolved for the current language. */
  readonly label: string;
  readonly rooms: number;
  readonly bathrooms: number;
}

/**
 * What the calculator prices when no market resolved and there is no set to
 * choose from — a form default, not a market's preset. The size row is not
 * drawn in that state.
 */
export const DEFAULT_PROPERTY_SIZE: PropertySizePreset = {
  code: 'DEFAULT',
  label: '',
  rooms: 3,
  bathrooms: 1,
};
