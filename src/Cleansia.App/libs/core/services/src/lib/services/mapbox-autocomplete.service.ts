import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable, InjectionToken } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';
import {
  catchError,
  debounceTime,
  distinctUntilChanged,
  Observable,
  of,
  Subject,
  switchMap,
  throwError,
} from 'rxjs';

/**
 * Mapbox public access token. Provide it from each app's environment.ts.
 * If empty, the autocomplete short-circuits and returns no suggestions —
 * the user can still type the address manually.
 */
export const MAPBOX_ACCESS_TOKEN = new InjectionToken<string>(
  'MAPBOX_ACCESS_TOKEN'
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

/** Raw subset of the Mapbox feature shape we care about. */
interface MapboxContextItem {
  id?: string;
  text?: string;
}

interface MapboxFeature {
  place_name?: string;
  text?: string;
  address?: string;
  center?: [number, number];
  context?: MapboxContextItem[];
}

interface MapboxResponse {
  features?: MapboxFeature[];
}

/**
 * Forward geocoding via Mapbox Geocoding v5 (text-only, no map widget).
 *
 * Parity with the mobile `ReverseGeocodingService.forwardGeocode`:
 *   - country=<MAPBOX_COUNTRY_WHITELIST> (defaults to `cz,sk`)
 *   - types=address,postcode
 *   - autocomplete=true
 *   - limit=5
 * Mobile additionally allows `place,locality,neighborhood`; the web picker is
 * focused on real address-with-house-number selection so we drop those.
 *
 * Docs: https://docs.mapbox.com/api/search/geocoding-v5/
 */
@Injectable({ providedIn: 'root' })
export class MapboxAutocompleteService {
  private readonly http = inject(HttpClient);
  private readonly translate = inject(TranslateService);
  private readonly accessToken = inject(MAPBOX_ACCESS_TOKEN, { optional: true }) ?? '';
  private readonly countryWhitelist = inject(MAPBOX_COUNTRY_WHITELIST);

  private static readonly ENDPOINT =
    'https://api.mapbox.com/geocoding/v5/mapbox.places';
  private static readonly DEBOUNCE_MS = 300;
  private static readonly MIN_QUERY_LENGTH = 3;
  private static readonly MAX_QUERY_LENGTH = 120;

  /** True when no token is configured; consumers can hide the suggestions UI. */
  get isConfigured(): boolean {
    return this.accessToken.trim().length > 0;
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

    const encoded = encodeURIComponent(
      trimmed.slice(0, MapboxAutocompleteService.MAX_QUERY_LENGTH)
    );
    const url = `${MapboxAutocompleteService.ENDPOINT}/${encoded}.json`;

    const params = new HttpParams()
      .set('access_token', this.accessToken)
      .set('autocomplete', 'true')
      .set('country', this.countryWhitelist.join(','))
      .set('types', 'address,postcode')
      .set('limit', '5')
      .set('language', this.languageForRequest());

    return this.http.get<MapboxResponse>(url, { params }).pipe(
      switchMap((res) => of(this.parse(res))),
      catchError((err) => throwError(() => err))
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

  private parse(res: MapboxResponse): MapboxAddressSuggestion[] {
    const features = res?.features ?? [];
    const out: MapboxAddressSuggestion[] = [];
    for (const f of features) {
      const mapped = this.featureToSuggestion(f);
      if (mapped) out.push(mapped);
    }
    return out;
  }

  private featureToSuggestion(f: MapboxFeature): MapboxAddressSuggestion | null {
    const center = f.center;
    if (!center || center.length < 2) return null;
    const [lng, lat] = center;
    if (typeof lat !== 'number' || typeof lng !== 'number') return null;

    const baseStreet = f.text ?? '';
    const houseNumber = f.address ?? '';
    const placeName = f.place_name ?? '';

    let street = '';
    if (baseStreet && houseNumber) street = `${baseStreet} ${houseNumber}`;
    else if (baseStreet) street = baseStreet;
    else street = placeName.split(',')[0]?.trim() ?? '';

    let city = '';
    let zip = '';
    for (const ctx of f.context ?? []) {
      const id = ctx.id ?? '';
      const text = ctx.text ?? '';
      if (id.startsWith('postcode')) zip = text;
      else if (id.startsWith('place') && !city) city = text;
      else if (id.startsWith('locality') && !city) city = text;
    }

    return {
      placeName,
      street,
      city,
      zipCode: zip,
      latitude: lat,
      longitude: lng,
    };
  }

  /** Mapbox supports cs/sk/uk/ru/en — pass through the active app language. */
  private languageForRequest(): string {
    const lang = (this.translate.currentLang || this.translate.getDefaultLang() || 'cs').toLowerCase();
    return ['cs', 'sk', 'uk', 'ru', 'en'].includes(lang) ? lang : 'cs';
  }
}
