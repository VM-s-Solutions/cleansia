import { inject, Injectable, InjectionToken } from '@angular/core';
import {
  catchError,
  debounceTime,
  distinctUntilChanged,
  Observable,
  of,
  Subject,
  switchMap,
} from 'rxjs';

/**
 * Mapbox public access token. Provide it from each app's environment.ts.
 * If empty, the autocomplete short-circuits and returns no suggestions —
 * the user can still type the address manually.
 *
 * @deprecated the browser must NEVER hold or send the Mapbox
 * token — a token in a request URL leaks into history/referrer/CDN/APM logs.
 * The token now lives server-side and is injected by a same-origin proxy
 * (see {@link MAPBOX_PROXY_PATH}). Whether autocomplete is available is
 * advertised by the token-free boolean {@link MAPBOX_AUTOCOMPLETE_ENABLED}.
 * This token is retained only for backward compatibility and is unused by the
 * service; remove it from app providers once the proxy is wired everywhere.
 */
export const MAPBOX_ACCESS_TOKEN = new InjectionToken<string>(
  'MAPBOX_ACCESS_TOKEN'
);

/**
 * How this library reaches address search. It is a port, not a URL, because the
 * lookup is a backend endpoint like every other one — and the typed client that
 * calls it is generated PER APP. A shared library cannot inject the customer
 * app's client without dragging it into the partner app, so each app provides
 * the implementation and this library stays app-agnostic.
 *
 * The default returns nothing: an app that has not provided one has no address
 * search, which is exactly what it had before.
 */
export interface AddressSearchPort {
  search(
    query: string,
    countries: string,
    limit: number
  ): Observable<MapboxAddressSuggestion[]>;
}

export const ADDRESS_SEARCH_PORT = new InjectionToken<AddressSearchPort>(
  'ADDRESS_SEARCH_PORT',
  { factory: (): AddressSearchPort => ({ search: () => of([]) }) }
);

/**
 * @deprecated The lookup moved off a same-origin SSR route and onto the
 * platform API, behind {@link ADDRESS_SEARCH_PORT}. The SSR route only ever
 * existed on the customer app, was never proxied by its own dev server, and had
 * no equivalent on the partner API at all. Retained so an app still providing it
 * compiles; it is read by nothing.
 */
export const MAPBOX_PROXY_PATH = new InjectionToken<string>(
  'MAPBOX_PROXY_PATH',
  { factory: () => '' }
);

/**
 * Token-free signal that the autocomplete UI may be shown. Provide it from each
 * app as `!!environment.mapboxToken` (i.e. "is a token configured for the
 * server proxy"), so the no-token-hides-UI behavior is preserved without ever
 * shipping the token to the browser. Defaults to disabled.
 */
export const MAPBOX_AUTOCOMPLETE_ENABLED = new InjectionToken<boolean>(
  'MAPBOX_AUTOCOMPLETE_ENABLED',
  { factory: () => false }
);

/**
 * ISO 3166-1 alpha-2 country codes (lowercase) used to limit Mapbox
 * geocoding results. Apps can override per-deployment via
 * `provide(MAPBOX_COUNTRY_WHITELIST, { useValue: [...] })`. The factory
 * default `['cz', 'sk']` matches the platform's current launch markets.
 */
export const MAPBOX_COUNTRY_WHITELIST = new InjectionToken<string[]>(
  'MAPBOX_COUNTRY_WHITELIST',
  { factory: () => ['cz', 'sk'] }
);

/** A normalised suggestion shape consumed by the address autocomplete UI. */
export interface MapboxAddressSuggestion {
  /** Full formatted line, e.g. "Vinohradská 12, 120 00 Praha, Česko". */
  placeName: string;
  /** Street + house number (or street base if no number). */
  street: string;
  /** City / municipality. */
  city: string;
  /** Postal code. */
  zipCode: string;
  /** Latitude in degrees (WGS84). */
  latitude: number;
  /** Longitude in degrees (WGS84). */
  longitude: number;
}

/**
 * Address autocomplete for the booking and profile address fields.
 *
 * This class no longer speaks to Mapbox. It debounces, enforces the minimum
 * query length and picks the request language; the lookup itself goes through
 * {@link ADDRESS_SEARCH_PORT} to the platform API, which owns the credential,
 * the provider's response shape and the rate limit.
 *
 * The provider's parameters — country whitelist, `types=address,postcode`,
 * `autocomplete=true`, `limit=5` — moved to the server with the call. Mobile
 * additionally allows `place,locality,neighborhood`; the web picker is focused
 * on real address-with-house-number selection, so it does not.
 */
@Injectable({ providedIn: 'root' })
export class MapboxAutocompleteService {
  private readonly port = inject(ADDRESS_SEARCH_PORT);
  private readonly enabled = inject(MAPBOX_AUTOCOMPLETE_ENABLED);
  private readonly countryWhitelist = inject(MAPBOX_COUNTRY_WHITELIST);

  private static readonly DEBOUNCE_MS = 300;
  private static readonly MIN_QUERY_LENGTH = 3;
  private static readonly MAX_QUERY_LENGTH = 120;
  private static readonly RESULT_LIMIT = 5;

  /**
   * True when geocoding is available; consumers can hide the suggestions UI.
   * Driven by a token-free boolean so the no-token-hides-UI behavior is
   * preserved without ever exposing the token to the browser.
   */
  get isConfigured(): boolean {
    return this.enabled === true;
  }

  /**
   * One-shot search. Use this when you want to control debouncing yourself
   * (e.g., from PrimeNG's `completeMethod` event which already throttles
   * internally via the input's keystrokes).
   */
  search(query: string): Observable<MapboxAddressSuggestion[]> {
    const trimmed = (query ?? '').trim();
    if (!this.isConfigured) return of([]);
    if (trimmed.length < MapboxAutocompleteService.MIN_QUERY_LENGTH) return of([]);

    const q = trimmed.slice(0, MapboxAutocompleteService.MAX_QUERY_LENGTH);

    // NOT localised. Asking the provider for the visitor's language translates
    // the place names, so a Prague address comes back with its city as "Прага"
    // — which the serviced-city list, holding "Praha", then rejects. An address
    // is written the way the local post office reads it.
    return this.port.search(
      q,
      this.countryWhitelist.join(','),
      MapboxAutocompleteService.RESULT_LIMIT
    );
  }

  /**
   * Convenience: pipe a raw input stream through debounce + dedupe + search.
   * Components that drive their own input observable can subscribe to this.
   */
  autocomplete$(input$: Subject<string>): Observable<MapboxAddressSuggestion[]> {
    return input$.pipe(
      debounceTime(MapboxAutocompleteService.DEBOUNCE_MS),
      distinctUntilChanged(),
      switchMap((q) => this.search(q).pipe(catchError(() => of([]))))
    );
  }



}
