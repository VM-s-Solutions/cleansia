import { fakeAsync, flush, TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import {
  MarketListItem,
  PartnerAuthService,
  PartnerClient,
  Translation,
} from '@cleansia/partner-services';
import { CleansiaPartnerRoute, SnackbarService } from '@cleansia/services';
import { LangChangeEvent, TranslateService } from '@ngx-translate/core';
import { of, Subject, throwError } from 'rxjs';
import { RegisterFacade } from './register.facade';

function translation(name: string): Translation {
  return new Translation({ name, description: undefined, tagline: undefined });
}

function market(overrides: Partial<MarketListItem>): MarketListItem {
  return new MarketListItem({
    countryId: 'cz-id',
    isoCode: 'CZE',
    isoAlpha2: 'CZ',
    name: 'Czech Republic',
    currencyCode: 'CZK',
    isDefault: false,
    translations: { cs: translation('Česko') },
    ...overrides,
  } as MarketListItem);
}

const CZ = market({ countryId: 'cz-id', isDefault: true });
const SVK = market({
  countryId: 'svk-id',
  isoCode: 'SVK',
  isoAlpha2: 'SK',
  name: 'Slovakia',
  currencyCode: 'EUR',
  translations: { cs: translation('Slovensko') },
});

function translateStub(langChange: Subject<LangChangeEvent>) {
  return {
    instant: (k: string) => k,
    currentLang: 'cs',
    getDefaultLang: () => 'en',
    onLangChange: langChange,
  };
}

describe('RegisterFacade — the consent ticked at signup', () => {
  let facade: RegisterFacade;
  let authService: { registerEmployee: jest.Mock };
  let router: { navigate: jest.Mock };
  let snackbar: { showError: jest.Mock; showErrorTranslated: jest.Mock };
  let partnerClient: { marketClient: { getOverview: jest.Mock } };

  const EMAIL = 'cleaner@example.com';

  function fillForm(terms: boolean): void {
    facade.formGroup.patchValue({
      firstName: 'Petr',
      lastName: 'Dvořák',
      email: EMAIL,
      password: 'Heslo1234',
      confirmPassword: 'Heslo1234',
      terms,
    });
  }

  function assertedTick(): unknown {
    expect(authService.registerEmployee).toHaveBeenCalledTimes(1);
    return authService.registerEmployee.mock.calls[0][4];
  }

  beforeEach(() => {
    authService = { registerEmployee: jest.fn().mockReturnValue(of(true)) };
    router = { navigate: jest.fn() };
    snackbar = { showError: jest.fn(), showErrorTranslated: jest.fn() };
    partnerClient = {
      marketClient: { getOverview: jest.fn().mockReturnValue(of([CZ])) },
    };

    TestBed.configureTestingModule({
      providers: [
        RegisterFacade,
        { provide: Router, useValue: router },
        { provide: PartnerAuthService, useValue: authService },
        { provide: PartnerClient, useValue: partnerClient },
        { provide: SnackbarService, useValue: snackbar },
        {
          provide: TranslateService,
          useValue: translateStub(new Subject<LangChangeEvent>()),
        },
      ],
    });

    facade = TestBed.inject(RegisterFacade);
  });

  // The server grants both employee consents from the tick on RegisterEmployee itself, so the tick
  // is a property of the request — nothing is parked client-side for a later session any more.
  it('asserts the tick on the RegisterEmployee request', () => {
    fillForm(true);

    facade.register();

    expect(assertedTick()).toBe(true);
    expect(router.navigate).toHaveBeenCalledWith(
      [CleansiaPartnerRoute.CONFIRM_EMAIL],
      { queryParams: { email: EMAIL } }
    );
  });

  it('refuses to register at all while the box is unticked', () => {
    fillForm(false);

    facade.register();

    expect(authService.registerEmployee).not.toHaveBeenCalled();
    expect(snackbar.showErrorTranslated).toHaveBeenCalledTimes(1);
  });

  // Unreachable in the shipped form, which the test above pins: `terms` is
  // `requiredTrue`, so an unticked submit never reaches the request. This pins
  // the assertion, not its reachability — an untick must never be sent as a tick
  // if the tick ever stops being required.
  it('asserts no tick for an absent one when the form does not require one', () => {
    const terms = facade.formGroup.get('terms');
    terms?.clearValidators();
    terms?.updateValueAndValidity();
    fillForm(false);

    facade.register();

    expect(assertedTick()).toBe(false);
  });
});

describe('RegisterFacade — the market picker', () => {
  let authService: { registerEmployee: jest.Mock };
  let getOverview: jest.Mock;
  let langChange: Subject<LangChangeEvent>;

  function createFacade(): RegisterFacade {
    TestBed.configureTestingModule({
      providers: [
        RegisterFacade,
        { provide: Router, useValue: { navigate: jest.fn() } },
        { provide: PartnerAuthService, useValue: authService },
        { provide: PartnerClient, useValue: { marketClient: { getOverview } } },
        {
          provide: SnackbarService,
          useValue: {
            showError: jest.fn(),
            showErrorTranslated: jest.fn(),
          },
        },
        { provide: TranslateService, useValue: translateStub(langChange) },
      ],
    });
    return TestBed.inject(RegisterFacade);
  }

  function fillForm(facade: RegisterFacade): void {
    facade.formGroup.patchValue({
      firstName: 'Petr',
      lastName: 'Dvořák',
      email: 'cleaner@example.com',
      password: 'Heslo1234',
      confirmPassword: 'Heslo1234',
      terms: true,
    });
  }

  function sentCountryId(): unknown {
    expect(authService.registerEmployee).toHaveBeenCalledTimes(1);
    return authService.registerEmployee.mock.calls[0][5];
  }

  beforeEach(() => {
    authService = { registerEmployee: jest.fn().mockReturnValue(of(true)) };
    getOverview = jest.fn().mockReturnValue(of([SVK, CZ]));
    langChange = new Subject<LangChangeEvent>();
  });

  it('lists exactly what the directory returns, one translated row per market, and preselects the flagged default over the first', () => {
    const facade = createFacade();

    expect(getOverview).toHaveBeenCalledTimes(1);
    expect(facade.marketOptions()).toEqual([
      { value: 'svk-id', label: 'Slovensko · EUR' },
      { value: 'cz-id', label: 'Česko · CZK' },
    ]);
    expect(facade.hasMarketChoice()).toBe(true);
    expect(facade.formGroup.get('countryId')?.value).toBe('cz-id');
  });

  it('preselects the first market when none is flagged as default', () => {
    getOverview.mockReturnValue(of([SVK, market({ countryId: 'cz-id' })]));

    const facade = createFacade();

    expect(facade.formGroup.get('countryId')?.value).toBe('svk-id');
  });

  it('relabels the rows when the language changes', () => {
    const facade = createFacade();

    langChange.next({ lang: 'en', translations: {} });

    expect(facade.marketOptions().map((option) => option.label)).toEqual([
      'Slovakia · EUR',
      'Czech Republic · CZK',
    ]);
  });

  it('sends the picked market on RegisterEmployee', () => {
    const facade = createFacade();
    fillForm(facade);
    facade.formGroup.patchValue({ countryId: 'svk-id' });

    facade.register();

    expect(sentCountryId()).toBe('svk-id');
  });

  it('still sends the market when the directory lists exactly one, with no choice to draw', () => {
    getOverview.mockReturnValue(of([SVK]));
    const facade = createFacade();
    fillForm(facade);

    facade.register();

    expect(facade.hasMarketChoice()).toBe(false);
    expect(sentCountryId()).toBe('svk-id');
  });

  it('leaves the form usable with no market sent when the directory read fails', fakeAsync(() => {
    getOverview.mockReturnValue(throwError(() => new Error('down')));
    const facade = createFacade();
    fillForm(facade);

    facade.register();
    flush();

    expect(facade.marketOptions()).toEqual([]);
    expect(facade.hasMarketChoice()).toBe(false);
    expect(facade.formGroup.valid).toBe(true);
    expect(sentCountryId()).toBeNull();
  }));
});
