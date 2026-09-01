import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { TranslateService } from '@ngx-translate/core';
import { of } from 'rxjs';

import {
  ADDRESS_SEARCH_PORT,
  AddressSearchPort,
  MAPBOX_AUTOCOMPLETE_ENABLED,
  MAPBOX_COUNTRY_WHITELIST,
  MapboxAddressSuggestion,
  MapboxAutocompleteService,
} from './mapbox-autocomplete.service';

/**
 * The geocoding credential must never reach the browser (T-0159).
 *
 * The mechanism changed and the guarantee got stronger. It used to be a
 * same-origin SSR route that injected the token, and these tests checked that
 * the browser's request to it carried nothing token-shaped. The lookup is now a
 * platform API endpoint reached through {@link ADDRESS_SEARCH_PORT}, so this
 * service issues NO http request of its own at all — which is what the first
 * group asserts, and it is a stricter statement than "the request was clean".
 *
 * Parsing the provider's feature shape moved to the API with the call. Its test
 * moved too: `src/Cleansia.Tests/Features/Addresses/MapboxSuggestionParsingTests.cs`.
 */
describe('MapboxAutocompleteService (T-0159 credential-out-of-browser)', () => {
  type PortCall = {
    query: string;
    countries: string;
    limit: number;
  };

  function setup(options?: {
    enabled?: boolean;
    countries?: string[];
    lang?: string;
    result?: MapboxAddressSuggestion[];
  }): {
    service: MapboxAutocompleteService;
    calls: PortCall[];
    httpMock: HttpTestingController;
  } {
    const translate = {
      currentLang: options?.lang ?? 'cs',
      getDefaultLang: () => 'cs',
    } as unknown as TranslateService;

    const calls: PortCall[] = [];
    const port: AddressSearchPort = {
      search: (query, countries, limit) => {
        calls.push({ query, countries, limit });
        return of(options?.result ?? []);
      },
    };

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
        { provide: ADDRESS_SEARCH_PORT, useValue: port },
        {
          provide: MAPBOX_COUNTRY_WHITELIST,
          useValue: options?.countries ?? ['cz', 'sk'],
        },
      ],
    });

    return {
      service: TestBed.inject(MapboxAutocompleteService),
      calls,
      httpMock: TestBed.inject(HttpTestingController),
    };
  }

  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
  });

  it('AC1: issues no http request of its own, so no request of its can carry a credential', () => {
    const { service, httpMock } = setup();

    service.search('Vinohradská 12').subscribe();

    // Stronger than checking a request for a token: there is no request. The
    // lookup is the API client's, behind the port.
    httpMock.expectNone(() => true);
  });

  it('AC1: never names the third-party host anywhere in what it sends', () => {
    const { service, calls } = setup();

    service.search('Praha 1').subscribe();

    expect(calls).toHaveLength(1);
    const sent = JSON.stringify(calls[0]);
    expect(sent).not.toContain('api.mapbox.com');
    expect(sent).not.toContain('access_token');
  });

  it('AC2: routes the lookup through the address-search port', () => {
    const { service, calls } = setup();

    service.search('Brno').subscribe();

    expect(calls).toHaveLength(1);
    expect(calls[0].query).toBe('Brno');
  });

  it('AC3: hands the port nothing token-shaped', () => {
    const { service, calls } = setup();

    service.search('Ostrava').subscribe();

    for (const value of Object.values(calls[0])) {
      expect(String(value).toLowerCase()).not.toContain('pk.ey');
    }
  });

  it('AC5: passes the country whitelist and the result limit', () => {
    const { service, calls } = setup({ countries: ['cz', 'sk'], lang: 'sk' });

    service.search('Bratislava').subscribe();

    expect(calls[0].countries).toBe('cz,sk');
    expect(calls[0].limit).toBe(5);
  });

  it('AC5: does NOT localise the lookup, whatever the app language is', () => {
    // Asking the provider for the visitor's language translates the place names
    // with everything else: a Prague address comes back with its city as
    // "Прага", and the serviced-city list holds "Praha", so the service-area
    // check rejected it and uk/ru customers could not book in Prague at all.
    const { service, calls } = setup({ lang: 'uk' });

    service.search('Zenklova').subscribe();

    expect(JSON.stringify(calls[0])).not.toContain('uk');
    expect(Object.keys(calls[0])).toEqual(['query', 'countries', 'limit']);
  });

  it('AC5: passes the query through unchanged', () => {
    const { service, calls } = setup();

    service.search('Vinohradská 12').subscribe();

    expect(calls[0].query).toBe('Vinohradská 12');
  });

  it('AC5: returns the API suggestions unchanged — the shape is the API contract now', () => {
    const suggestion: MapboxAddressSuggestion = {
      placeName: 'Vinohradská 12, 120 00 Praha, Česko',
      street: 'Vinohradská 12',
      city: 'Praha',
      zipCode: '120 00',
      latitude: 50.0755,
      longitude: 14.4378,
    };
    const { service } = setup({ result: [suggestion] });

    let result: MapboxAddressSuggestion[] = [];
    service.search('Vinohradská 12').subscribe((r) => (result = r));

    expect(result).toEqual([suggestion]);
  });

  it('AC5: short-circuits without calling the port below the minimum query length', () => {
    const { service, calls } = setup();

    let result: MapboxAddressSuggestion[] | undefined;
    service.search('ab').subscribe((r) => (result = r));

    expect(calls).toHaveLength(0);
    expect(result).toEqual([]);
  });

  it('AC5: isConfigured reflects the enabled flag and calls nothing when disabled', () => {
    const { service, calls } = setup({ enabled: false });

    expect(service.isConfigured).toBe(false);

    let result: MapboxAddressSuggestion[] | undefined;
    service.search('Praha').subscribe((r) => (result = r));

    expect(calls).toHaveLength(0);
    expect(result).toEqual([]);
  });

  it('AC5: isConfigured is true when enabled', () => {
    const { service } = setup({ enabled: true });
    expect(service.isConfigured).toBe(true);
  });

  it('the port default is empty, so an app that provides none simply has no suggestions', () => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        MapboxAutocompleteService,
        {
          provide: TranslateService,
          useValue: {
            currentLang: 'cs',
            getDefaultLang: () => 'cs',
          } as unknown as TranslateService,
        },
        { provide: MAPBOX_AUTOCOMPLETE_ENABLED, useValue: true },
      ],
    });

    let result: MapboxAddressSuggestion[] | undefined;
    TestBed.inject(MapboxAutocompleteService)
      .search('Praha')
      .subscribe((r) => (result = r));

    expect(result).toEqual([]);
  });
});
