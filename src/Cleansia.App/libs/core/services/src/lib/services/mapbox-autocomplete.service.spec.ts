import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
  TestRequest,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { TranslateService } from '@ngx-translate/core';

import {
  MAPBOX_AUTOCOMPLETE_ENABLED,
  MAPBOX_COUNTRY_WHITELIST,
  MAPBOX_PROXY_PATH,
  MapboxAutocompleteService,
  MapboxAddressSuggestion,
} from './mapbox-autocomplete.service';

/**
 * The Mapbox access token must never travel in the request URL/query string. The frontend calls a
 * same-origin proxy path that injects the token server-side; the token is never present on the wire
 * from the browser.
 *
 * Mechanism decision (documented in the ticket): the Mapbox Geocoding REST
 * endpoints (v5 mapbox.places and v6 search/geocode) authenticate ONLY via the
 * `access_token` query parameter — they do not honor an `Authorization` header.
 * The conforming fix is therefore a thin same-origin proxy, not a header.
 */
describe('MapboxAutocompleteService (T-0159 token-out-of-URL)', () => {
  const PROXY_PATH = '/api/mapbox/geocode';

  function setup(options?: {
    enabled?: boolean;
    countries?: string[];
    lang?: string;
  }): { service: MapboxAutocompleteService; httpMock: HttpTestingController } {
    const translate = {
      currentLang: options?.lang ?? 'cs',
      getDefaultLang: () => 'cs',
    } as unknown as TranslateService;

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        MapboxAutocompleteService,
        { provide: TranslateService, useValue: translate },
        {
          provide: MAPBOX_AUTOCOMPLETE_ENABLED,
          useValue: options?.enabled ?? true,
        },
        { provide: MAPBOX_PROXY_PATH, useValue: PROXY_PATH },
        {
          provide: MAPBOX_COUNTRY_WHITELIST,
          useValue: options?.countries ?? ['cz', 'sk'],
        },
      ],
    });

    return {
      service: TestBed.inject(MapboxAutocompleteService),
      httpMock: TestBed.inject(HttpTestingController),
    };
  }

  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
  });

  function flushOneRequest(httpMock: HttpTestingController): TestRequest {
    const requests = httpMock.match(() => true);
    expect(requests.length).toBe(1);
    return requests[0];
  }

  it('AC1: does not put access_token anywhere in the request URL or query', () => {
    const { service, httpMock } = setup();

    service.search('Vinohradská 12').subscribe();

    const req = flushOneRequest(httpMock);
    expect(req.request.url).not.toContain('access_token');
    expect(req.request.urlWithParams).not.toContain('access_token');
    expect(req.request.params.has('access_token')).toBe(false);
    expect(req.request.urlWithParams.toLowerCase()).not.toContain(
      'pk.ey' // any Mapbox token literal prefix must not appear in the URL
    );

    req.flush({ features: [] });
  });

  it('AC1: never calls the third-party api.mapbox.com directly from the browser', () => {
    const { service, httpMock } = setup();

    service.search('Praha 1').subscribe();

    const req = flushOneRequest(httpMock);
    expect(req.request.url).not.toContain('api.mapbox.com');
    expect(req.request.url.startsWith(PROXY_PATH)).toBe(true);

    req.flush({ features: [] });
  });

  it('AC2: routes the call through the same-origin proxy path (no token in app code path)', () => {
    const { service, httpMock } = setup();

    service.search('Brno').subscribe();

    const req = flushOneRequest(httpMock);
    // The proxy is responsible for injecting the token server-side; the
    // browser request carries no credential at all.
    expect(req.request.url.startsWith(PROXY_PATH)).toBe(true);
    expect(req.request.params.has('access_token')).toBe(false);

    req.flush({ features: [] });
  });

  it('AC3: sends no Authorization header and no token-bearing header', () => {
    const { service, httpMock } = setup();

    service.search('Ostrava').subscribe();

    const req = flushOneRequest(httpMock);
    expect(req.request.headers.has('Authorization')).toBe(false);
    expect(req.request.headers.has('X-Mapbox-Token')).toBe(false);
    // Nothing token-shaped in any header value.
    for (const name of req.request.headers.keys()) {
      const value = req.request.headers.get(name) ?? '';
      expect(value.toLowerCase()).not.toContain('pk.ey');
    }

    req.flush({ features: [] });
  });

  it('AC3: the full loggable request surface (urlWithParams) is credential-free', () => {
    const { service, httpMock } = setup();

    service.search('Plzeň').subscribe();

    const req = flushOneRequest(httpMock);
    // urlWithParams is what an HTTP interceptor / Sentry span would record.
    expect(req.request.urlWithParams).not.toMatch(/access_token/i);
    expect(req.request.urlWithParams).not.toMatch(/pk\.ey/i);

    req.flush({ features: [] });
  });

  it('AC5: preserves country / language / types / autocomplete / limit params', () => {
    const { service, httpMock } = setup({ countries: ['cz', 'sk'], lang: 'sk' });

    service.search('Bratislava').subscribe();

    const req = flushOneRequest(httpMock);
    expect(req.request.params.get('country')).toBe('cz,sk');
    expect(req.request.params.get('language')).toBe('sk');
    expect(req.request.params.get('types')).toBe('address,postcode');
    expect(req.request.params.get('autocomplete')).toBe('true');
    expect(req.request.params.get('limit')).toBe('5');

    req.flush({ features: [] });
  });

  it('AC5: passes the (encoded) query through to the proxy', () => {
    const { service, httpMock } = setup();

    service.search('Vinohradská 12').subscribe();

    const req = flushOneRequest(httpMock);
    expect(req.request.params.get('q')).toBe('Vinohradská 12');

    req.flush({ features: [] });
  });

  it('AC5: parses Mapbox features into normalized suggestions unchanged', () => {
    const { service, httpMock } = setup();

    let result: MapboxAddressSuggestion[] = [];
    service.search('Vinohradská 12').subscribe((r) => (result = r));

    const req = flushOneRequest(httpMock);
    req.flush({
      features: [
        {
          place_name: 'Vinohradská 12, 120 00 Praha, Česko',
          text: 'Vinohradská',
          address: '12',
          center: [14.4378, 50.0755],
          context: [
            { id: 'postcode.1', text: '120 00' },
            { id: 'locality.1', text: 'Holešovice' },
            { id: 'place.1', text: 'Praha' },
          ],
        },
      ],
    });

    expect(result).toHaveLength(1);
    expect(result[0]).toEqual({
      placeName: 'Vinohradská 12, 120 00 Praha, Česko',
      street: 'Vinohradská 12',
      city: 'Praha',
      zipCode: '120 00',
      latitude: 50.0755,
      longitude: 14.4378,
    });
  });

  it('AC5: short-circuits with no request when below min query length', () => {
    const { service, httpMock } = setup();

    let result: MapboxAddressSuggestion[] | undefined;
    service.search('ab').subscribe((r) => (result = r));

    httpMock.expectNone(() => true);
    expect(result).toEqual([]);
  });

  it('AC5: isConfigured reflects the enabled flag and issues no request when disabled', () => {
    const { service, httpMock } = setup({ enabled: false });

    expect(service.isConfigured).toBe(false);

    let result: MapboxAddressSuggestion[] | undefined;
    service.search('Praha').subscribe((r) => (result = r));

    httpMock.expectNone(() => true);
    expect(result).toEqual([]);
  });

  it('AC5: isConfigured is true when enabled', () => {
    const { service } = setup({ enabled: true });
    expect(service.isConfigured).toBe(true);
    TestBed.inject(HttpTestingController).expectNone(() => true);
  });
});
