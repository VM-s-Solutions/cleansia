export const PREFERRED_MARKET_KEY = 'preferred_market';

/** The two facts the resolution needs; `MarketListItem` satisfies it structurally. */
export interface MarketCandidate {
  readonly isoCode: string | undefined;
  readonly isDefault: boolean;
}

/**
 * The market code a cookie header names, or null.
 *
 * The cookie is the only store (ADR-0058 D3): the server reads it off the request, the browser off
 * `document.cookie`, and one store read by both cannot disagree with itself. The value is
 * attacker-controlled text that is only ever compared against the market list, so anything that
 * cannot be an ISO-3166 alpha-3 code is dropped here.
 */
export function readPreferredMarket(cookieHeader: string | null | undefined): string | null {
  const match = (cookieHeader ?? '').match(/(?:^|;\s*)preferred_market=([^;]*)/);
  const value = match?.[1]?.trim() ?? '';
  return /^[A-Za-z]{3}$/.test(value) ? value.toUpperCase() : null;
}

/** Stored code if listed → the `isDefault` market → the first listed → null for an empty list. */
export function resolveMarket<T extends MarketCandidate>(
  markets: readonly T[],
  storedIsoCode: string | null,
): T | null {
  return (
    markets.find((market) => market.isoCode === storedIsoCode) ??
    markets.find((market) => market.isDefault) ??
    markets[0] ??
    null
  );
}

/** Browser only. The cookie is the whole store — no localStorage mirror. */
export function persistPreferredMarket(isoCode: string): void {
  document.cookie = `${PREFERRED_MARKET_KEY}=${isoCode}; path=/; max-age=31536000; SameSite=Lax`;
}

/** What one market has to carry to be offered as an address country; `MarketListItem` satisfies it. */
export interface MarketCountry {
  readonly countryId: string | undefined;
  readonly isoCode: string | undefined;
  readonly name: string | undefined;
  readonly translations?: { [language: string]: { name?: string | undefined } } | undefined;
}

export interface MarketCountryOption {
  readonly label: string;
  readonly value: string;
}

/**
 * The address country picker's options: the market directory, one per market's country
 * (ADR-0058 D1 — a market is a serviced country, so the directory is the served list).
 */
export function marketCountryOptions(
  markets: readonly MarketCountry[],
  language: string,
): MarketCountryOption[] {
  return markets
    .filter((market): market is MarketCountry & { countryId: string } => !!market.countryId)
    .map((market) => ({
      label: market.translations?.[language]?.name || market.name || market.isoCode || market.countryId,
      value: market.countryId,
    }));
}
