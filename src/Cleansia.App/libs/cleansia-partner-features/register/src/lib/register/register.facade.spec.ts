import { fakeAsync, flush, TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import {
  ConsentType,
  GdprClient,
  GrantConsentCommand,
  MarketListItem,
  PartnerAuthService,
  PartnerClient,
  SignupConsentService,
  Translation,
} from '@cleansia/partner-services';
import { SnackbarService } from '@cleansia/services';
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
  let signupConsent: SignupConsentService;
  let gdprClient: Record<string, jest.Mock>;
  let authService: { registerEmployee: jest.Mock };
  let router: { navigate: jest.Mock };
  let snackbar: { showError: jest.Mock };
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

  function grantedBodies(): Record<string, unknown>[] {
    return gdprClient['consentsPost'].mock.calls.map(([command]) => {
      expect(command).toBeInstanceOf(GrantConsentCommand);
      return (command as GrantConsentCommand).toJSON();
    });
  }

  beforeEach(() => {
    localStorage.clear();
    authService = { registerEmployee: jest.fn().mockReturnValue(of(true)) };
    router = { navigate: jest.fn() };
    snackbar = { showError: jest.fn() };
    gdprClient = {
      consentsGet: jest.fn().mockReturnValue(of([])),
      consentsPost: jest.fn().mockReturnValue(of(undefined)),
    };
    partnerClient = {
      marketClient: { getOverview: jest.fn().mockReturnValue(of([CZ])) },
    };

    TestBed.configureTestingModule({
      providers: [
        RegisterFacade,
        { provide: Router, useValue: router },
        { provide: PartnerAuthService, useValue: authService },
        { provide: PartnerClient, useValue: partnerClient },
        { provide: GdprClient, useValue: gdprClient },
        { provide: SnackbarService, useValue: snackbar },
        {
          provide: TranslateService,
          useValue: translateStub(new Subject<LangChangeEvent>()),
        },
      ],
    });

    facade = TestBed.inject(RegisterFacade);
    signupConsent = TestBed.inject(SignupConsentService);
  });

  it('grants the ticked documents at the session that follows the signup', () => {
    fillForm(true);

    facade.register();
    signupConsent.flush(EMAIL);

    expect(grantedBodies()).toEqual([
      { consentType: ConsentType.TermsOfService },
      { consentType: ConsentType.PrivacyPolicy },
    ]);
  });

  it('grants nothing when the registration itself failed', () => {
    authService.registerEmployee.mockReturnValue(
      throwError(() => new Error('taken'))
    );
    fillForm(true);

    facade.register();
    signupConsent.flush(EMAIL);

    expect(gdprClient['consentsPost']).not.toHaveBeenCalled();
  });

  it('refuses to register at all while the box is unticked', () => {
    fillForm(false);

    facade.register();

    expect(authService.registerEmployee).not.toHaveBeenCalled();
  });

  // Unreachable in the shipped form, which the test above pins: `terms` is
  // `requiredTrue`, so an unticked submit never reaches the grant. This pins the
  // guard, not its reachability — it is what keeps an untick from becoming a
  // manufactured record if the tick ever stops being required.
  it('grants nothing for an absent tick when the form does not require one', () => {
    const terms = facade.formGroup.get('terms');
    terms?.clearValidators();
    terms?.updateValueAndValidity();
    fillForm(false);

    facade.register();
    signupConsent.flush(EMAIL);

    expect(authService.registerEmployee).toHaveBeenCalled();
    expect(gdprClient['consentsPost']).not.toHaveBeenCalled();
  });

  it('completes the signup even when the tick cannot be parked for delivery', () => {
    const setItem = jest
      .spyOn(Storage.prototype, 'setItem')
      .mockImplementation(() => {
        throw new Error('quota');
      });
    fillForm(true);

    expect(() => facade.register()).not.toThrow();

    setItem.mockRestore();
    expect(router.navigate).toHaveBeenCalled();
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
        { provide: GdprClient, useValue: { consentsGet: jest.fn().mockReturnValue(of([])) } },
        { provide: SnackbarService, useValue: { showError: jest.fn() } },
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
    return authService.registerEmployee.mock.calls[0][4];
  }

  beforeEach(() => {
    localStorage.clear();
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
