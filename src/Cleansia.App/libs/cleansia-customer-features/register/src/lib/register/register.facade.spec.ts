import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import {
  CustomerAuthService,
  CustomerClient,
  JwtTokenResponse,
  ValidateReferralQuery,
  ValidateReferralResponse,
} from '@cleansia/customer-services';
import { selectMarketCountryId } from '@cleansia/customer-stores';
import { SnackbarService, extractApiErrorCode } from '@cleansia/services';
import { MockStore, provideMockStore } from '@ngrx/store/testing';
import { TranslateService } from '@ngx-translate/core';
import { Subject, of, throwError } from 'rxjs';
import { RegisterFacade } from './register.facade';

describe('RegisterFacade — referral landing capture (/r/{code})', () => {
  let facade: RegisterFacade;
  let referralClient: { validate: jest.Mock };
  let authService: {
    register: jest.Mock;
    signUpWithGoogle: jest.Mock;
    signUpWithApple: jest.Mock;
    setSession: jest.Mock;
  };
  let snackbar: {
    showError: jest.Mock;
    showApiError: jest.Mock;
    showErrorTranslated: jest.Mock;
    showSuccessTranslated: jest.Mock;
  };

  const validResponse = ValidateReferralResponse.fromJS({
    isValid: true,
    referrerFirstName: 'Petra',
  });

  beforeEach(() => {
    referralClient = { validate: jest.fn() };
    authService = {
      register: jest.fn().mockReturnValue(of({})),
      signUpWithGoogle: jest.fn(),
      signUpWithApple: jest.fn(),
      setSession: jest.fn(),
    };
    snackbar = {
      showError: jest.fn(),
      showApiError: jest.fn(),
      showErrorTranslated: jest.fn(),
      showSuccessTranslated: jest.fn(),
    };

    TestBed.configureTestingModule({
      providers: [
        RegisterFacade,
        provideMockStore({
          selectors: [{ selector: selectMarketCountryId, value: null }],
        }),
        { provide: Router, useValue: { navigate: jest.fn() } },
        { provide: CustomerAuthService, useValue: authService },
        { provide: CustomerClient, useValue: { referralClient } },
        { provide: SnackbarService, useValue: snackbar },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
      ],
    });

    facade = TestBed.inject(RegisterFacade);
  });

  it('captures and normalizes the URL code into the signal and form control before validation resolves', () => {
    const pending$ = new Subject<ValidateReferralResponse>();
    referralClient.validate.mockReturnValue(pending$.asObservable());

    facade.applyReferralCodeFromUrl('  abc12 ');

    expect(facade.referralCode()).toBe('ABC12');
    expect(facade.formGroup.get('referralCode')?.value).toBe('ABC12');
    expect(facade.referralState()).toEqual({ kind: 'validating' });
  });

  it('validates exactly once and reaches the valid state with the referrer first name', async () => {
    referralClient.validate.mockReturnValue(of(validResponse));

    await facade.applyReferralCodeFromUrl('abc12');

    expect(referralClient.validate).toHaveBeenCalledTimes(1);
    // Every member of a generated query is optional, so a dropped assignment
    // type-checks — pin the serialized body instead (ADR-0031).
    const query = referralClient.validate.mock.calls[0][0];
    expect(query).toBeInstanceOf(ValidateReferralQuery);
    expect(query.toJSON()).toEqual({ code: 'ABC12' });
    expect(facade.referralState()).toEqual({
      kind: 'valid',
      referrerFirstName: 'Petra',
    });
    expect(facade.referralCode()).toBe('ABC12');
  });

  it('keeps the code applied on an invalid response (fail-soft, backend skips bad codes)', async () => {
    referralClient.validate.mockReturnValue(
      of(ValidateReferralResponse.fromJS({ isValid: false, errorCode: 'NotFound' }))
    );

    await facade.applyReferralCodeFromUrl('badcode');

    expect(facade.referralState()).toEqual({
      kind: 'invalid',
      error: 'NotFound',
    });
    expect(facade.formGroup.get('referralCode')?.value).toBe('BADCODE');
    expect(facade.formGroup.get('referralCode')?.valid).toBe(true);
  });

  it('fails soft on a network failure — state invalid, form still submittable', async () => {
    referralClient.validate.mockReturnValue(
      throwError(() => new Error('network'))
    );

    await facade.applyReferralCodeFromUrl('abc12');

    expect(facade.referralState()).toEqual({ kind: 'invalid', error: null });
    expect(facade.formGroup.get('referralCode')?.value).toBe('ABC12');
    expect(facade.formGroup.get('referralCode')?.valid).toBe(true);
  });

  it('does nothing for an empty or missing code', () => {
    facade.applyReferralCodeFromUrl(null);
    facade.applyReferralCodeFromUrl('   ');

    expect(referralClient.validate).not.toHaveBeenCalled();
    expect(facade.referralState()).toEqual({ kind: 'idle' });
  });

  it('sends the captured code through to authService.register at signup', async () => {
    referralClient.validate.mockReturnValue(of(validResponse));
    await facade.applyReferralCodeFromUrl('abc12');

    facade.formGroup.patchValue({
      firstName: 'Jan',
      lastName: 'Novák',
      email: 'jan@example.com',
      password: 'Heslo1234',
      confirmPassword: 'Heslo1234',
      terms: true,
    });

    facade.register();

    expect(authService.register).toHaveBeenCalledWith(
      'jan@example.com',
      'Heslo1234',
      'Jan',
      'Novák',
      true,
      'ABC12',
      null
    );
  });
});

describe('RegisterFacade — the chosen market', () => {
  let facade: RegisterFacade;
  let store: MockStore;
  let referralClient: { validate: jest.Mock };
  let authService: {
    register: jest.Mock;
    signUpWithGoogle: jest.Mock;
    signUpWithApple: jest.Mock;
    setSession: jest.Mock;
  };

  const EMAIL = 'jan@example.com';
  const CREDENTIAL = [
    'header',
    btoa(JSON.stringify({ sub: 'google-subject', email: EMAIL, given_name: 'Jan', family_name: 'Novák' })),
    'signature',
  ].join('.');

  function fillForm(): void {
    facade.formGroup.patchValue({
      firstName: 'Jan',
      lastName: 'Novák',
      email: EMAIL,
      password: 'Heslo1234',
      confirmPassword: 'Heslo1234',
      terms: true,
    });
  }

  beforeEach(() => {
    referralClient = {
      validate: jest.fn().mockReturnValue(of(ValidateReferralResponse.fromJS({ isValid: true }))),
    };
    authService = {
      register: jest.fn().mockReturnValue(of(true)),
      signUpWithGoogle: jest.fn().mockReturnValue(of({ email: EMAIL })),
      signUpWithApple: jest.fn().mockReturnValue(of({ email: EMAIL })),
      setSession: jest.fn(),
    };

    TestBed.configureTestingModule({
      providers: [
        RegisterFacade,
        provideMockStore({
          selectors: [{ selector: selectMarketCountryId, value: 'svk-id' }],
        }),
        { provide: Router, useValue: { navigate: jest.fn() } },
        { provide: CustomerAuthService, useValue: authService },
        { provide: CustomerClient, useValue: { referralClient } },
        {
          provide: SnackbarService,
          useValue: { showError: jest.fn(), showApiError: jest.fn(), showSuccessTranslated: jest.fn() },
        },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
      ],
    });

    facade = TestBed.inject(RegisterFacade);
    store = TestBed.inject(MockStore);
  });

  // The market the visitor is browsing is the operating company the account is
  // opened with (ADR-0061 D3); every anonymous call the form makes names it.
  it('registers with the persisted market', () => {
    fillForm();

    facade.register();

    expect(authService.register).toHaveBeenCalledWith(EMAIL, 'Heslo1234', 'Jan', 'Novák', true, undefined, 'svk-id');
  });

  it('signs up with Google in the persisted market', () => {
    fillForm();

    facade.googleRegister(CREDENTIAL);

    expect(authService.signUpWithGoogle).toHaveBeenCalledWith(
      CREDENTIAL,
      'google-subject',
      EMAIL,
      'Jan',
      'Novák',
      'svk-id'
    );
  });

  it('signs up with Apple in the persisted market', () => {
    fillForm();

    facade.appleRegister('id-token', 'raw-nonce', 'Jan', 'Novák');

    expect(authService.signUpWithApple).toHaveBeenCalledWith('id-token', 'raw-nonce', 'Jan', 'Novák', 'svk-id');
  });

  it('validates a referral code against the persisted market', async () => {
    await facade.validateReferralCodeNow('abc12');

    const query = referralClient.validate.mock.calls[0][0];
    expect(query).toBeInstanceOf(ValidateReferralQuery);
    expect(query.toJSON()).toEqual({ code: 'ABC12', countryId: 'svk-id' });
  });

  it('names no market when none resolved, so the server falls back to its default', async () => {
    store.overrideSelector(selectMarketCountryId, null);
    store.refreshState();
    fillForm();

    facade.register();
    await facade.validateReferralCodeNow('abc12');

    expect(authService.register).toHaveBeenCalledWith(EMAIL, 'Heslo1234', 'Jan', 'Novák', true, undefined, null);
    expect(referralClient.validate.mock.calls[0][0].toJSON()).toEqual({ code: 'ABC12' });
  });
});

describe('RegisterFacade — Sign in with Apple', () => {
  let facade: RegisterFacade;
  let authService: { signUpWithApple: jest.Mock; setSession: jest.Mock };
  let snackbar: {
    showApiError: jest.Mock;
    showErrorTranslated: jest.Mock;
    showSuccessTranslated: jest.Mock;
  };
  let router: { navigate: jest.Mock };

  beforeEach(() => {
    authService = { signUpWithApple: jest.fn(), setSession: jest.fn() };
    snackbar = {
      showApiError: jest.fn(),
      showErrorTranslated: jest.fn(),
      showSuccessTranslated: jest.fn(),
    };
    router = { navigate: jest.fn() };

    TestBed.configureTestingModule({
      providers: [
        RegisterFacade,
        provideMockStore({
          selectors: [{ selector: selectMarketCountryId, value: null }],
        }),
        { provide: Router, useValue: router },
        { provide: CustomerAuthService, useValue: authService },
        { provide: CustomerClient, useValue: { referralClient: { validate: jest.fn() } } },
        { provide: SnackbarService, useValue: snackbar },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
      ],
    });

    facade = TestBed.inject(RegisterFacade);
    // The gate refuses every social branch while the box is unticked; these
    // pin what happens past it.
    facade.formGroup.patchValue({ terms: true });
  });

  it('forwards the RAW nonce and the first-authorization name untouched', () => {
    authService.signUpWithApple.mockReturnValue(of({}));

    facade.appleRegister('id-token', 'raw-nonce', 'Jan', 'Novák');

    // The server hashes this value itself — sending the hash instead fails
    // every sign-up with a generic error, so pin the argument order.
    expect(authService.signUpWithApple).toHaveBeenCalledWith(
      'id-token',
      'raw-nonce',
      'Jan',
      'Novák',
      null
    );
    expect(authService.setSession).toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalled();
  });

  it('surfaces an Apple sign-up error under the register fallback key', () => {
    // Bare ProblemDetails — the shape NSwag actually throws. Pinning the
    // resolved key as well as the fallback keeps this from passing while the
    // user is shown the generic message instead of the real reason.
    authService.signUpWithApple.mockReturnValue(
      throwError(() => ({
        detail: 'auth.invalid_apple_token',
        errors: { IdentityToken: 'auth.invalid_apple_token' },
      }))
    );

    facade.appleRegister('id-token', 'raw-nonce');

    expect(snackbar.showApiError).toHaveBeenCalledWith(expect.anything(), 'auth.register.error');
    const [reported] = snackbar.showApiError.mock.calls[0];
    expect(extractApiErrorCode(reported)).toBe('auth.invalid_apple_token');
    expect(authService.setSession).not.toHaveBeenCalled();
  });

  it('reports a popup failure that never reached the API', () => {
    facade.appleSignInFailed();

    expect(snackbar.showErrorTranslated).toHaveBeenCalledWith('api.common.error_occurred');
  });
});

describe('RegisterFacade — the consent ticked at signup', () => {
  let facade: RegisterFacade;
  let authService: { register: jest.Mock };
  let router: { navigate: jest.Mock };
  let snackbar: { showError: jest.Mock; showApiError: jest.Mock; showSuccessTranslated: jest.Mock };

  const EMAIL = 'jan@example.com';

  function fillForm(terms: boolean): void {
    facade.formGroup.patchValue({
      firstName: 'Jan',
      lastName: 'Novák',
      email: EMAIL,
      password: 'Heslo1234',
      confirmPassword: 'Heslo1234',
      terms,
    });
  }

  function assertedTick(): unknown {
    return authService.register.mock.calls[0][4];
  }

  beforeEach(() => {
    authService = { register: jest.fn().mockReturnValue(of(true)) };
    router = { navigate: jest.fn() };
    snackbar = {
      showError: jest.fn(),
      showApiError: jest.fn(),
      showSuccessTranslated: jest.fn(),
    };

    TestBed.configureTestingModule({
      providers: [
        RegisterFacade,
        provideMockStore({
          selectors: [{ selector: selectMarketCountryId, value: null }],
        }),
        { provide: Router, useValue: router },
        { provide: CustomerAuthService, useValue: authService },
        { provide: CustomerClient, useValue: { referralClient: { validate: jest.fn() } } },
        { provide: SnackbarService, useValue: snackbar },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
      ],
    });

    facade = TestBed.inject(RegisterFacade);
  });

  // The server grants the two consents from the tick and records the assertion (ADR-0062 D4), so
  // the tick is a property of the register request — nothing is parked client-side any more.
  it('asserts the tick on the register request', () => {
    fillForm(true);

    facade.register();

    expect(assertedTick()).toBe(true);
    expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith('auth.register.success');
    expect(router.navigate).toHaveBeenCalled();
  });

  it('refuses to register at all while the box is unticked', () => {
    fillForm(false);

    facade.register();

    expect(authService.register).not.toHaveBeenCalled();
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

    expect(authService.register).toHaveBeenCalled();
    expect(assertedTick()).toBe(false);
  });
});

describe('RegisterFacade — the consent ticked at a social signup', () => {
  let facade: RegisterFacade;
  // Both pairs are stubbed so wiring a signup screen to the sign-in entry point
  // shows up as a called mock instead of a crash on an absent method.
  let authService: {
    signUpWithGoogle: jest.Mock;
    signUpWithApple: jest.Mock;
    signInWithGoogle: jest.Mock;
    signInWithApple: jest.Mock;
    setSession: jest.Mock;
  };
  let router: { navigate: jest.Mock };
  let snackbar: {
    showApiError: jest.Mock;
    showErrorTranslated: jest.Mock;
    showSuccessTranslated: jest.Mock;
  };

  const EMAIL = 'jan@example.com';
  const SESSION = { email: EMAIL } as JwtTokenResponse;

  /** Google hands the callback an ID token; the facade reads its payload segment. */
  const CREDENTIAL = [
    'header',
    btoa(
      JSON.stringify({
        sub: 'google-subject',
        email: EMAIL,
        given_name: 'Jan',
        family_name: 'Novak',
      })
    ),
    'signature',
  ].join('.');

  function tick(accepted: boolean): void {
    facade.formGroup.patchValue({ terms: accepted });
  }

  beforeEach(() => {
    authService = {
      signUpWithGoogle: jest.fn().mockReturnValue(of(SESSION)),
      signUpWithApple: jest.fn().mockReturnValue(of(SESSION)),
      signInWithGoogle: jest.fn().mockReturnValue(of(SESSION)),
      signInWithApple: jest.fn().mockReturnValue(of(SESSION)),
      setSession: jest.fn(),
    };
    router = { navigate: jest.fn() };
    snackbar = {
      showApiError: jest.fn(),
      showErrorTranslated: jest.fn(),
      showSuccessTranslated: jest.fn(),
    };

    TestBed.configureTestingModule({
      providers: [
        RegisterFacade,
        provideMockStore({
          selectors: [{ selector: selectMarketCountryId, value: null }],
        }),
        { provide: Router, useValue: router },
        { provide: CustomerAuthService, useValue: authService },
        { provide: CustomerClient, useValue: { referralClient: { validate: jest.fn() } } },
        { provide: SnackbarService, useValue: snackbar },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
      ],
    });

    facade = TestBed.inject(RegisterFacade);
  });

  // The signup screen must reach the entry point that asserts the tick. A screen
  // wired to the wrong one still passes every behavioural test below — the gate
  // blocks both and both mint a session — so read the call itself.
  it.each([
    ['Google', (f: RegisterFacade) => f.googleRegister(CREDENTIAL), 'signUpWithGoogle', 'signInWithGoogle'],
    ['Apple', (f: RegisterFacade) => f.appleRegister('id-token', 'raw-nonce'), 'signUpWithApple', 'signInWithApple'],
  ])('takes the %s SIGNUP entry point, the one that asserts the tick', (_, run, signUp, signIn) => {
    tick(true);

    run(facade);

    expect(authService[signUp as keyof typeof authService]).toHaveBeenCalled();
    expect(authService[signIn as keyof typeof authService]).not.toHaveBeenCalled();
  });

  it.each([
    ['Google', (f: RegisterFacade) => f.googleRegister(CREDENTIAL), 'signUpWithGoogle', 'signInWithGoogle'],
    ['Apple', (f: RegisterFacade) => f.appleRegister('id-token', 'raw-nonce'), 'signUpWithApple', 'signInWithApple'],
  ])('creates no account at all on %s while the box is unticked', (_, run, signUp, signIn) => {
    tick(false);

    run(facade);

    expect(authService[signUp as keyof typeof authService]).not.toHaveBeenCalled();
    expect(authService[signIn as keyof typeof authService]).not.toHaveBeenCalled();
    expect(authService.setSession).not.toHaveBeenCalled();
    expect(router.navigate).not.toHaveBeenCalled();
    expect(snackbar.showErrorTranslated).toHaveBeenCalledWith(
      'auth.register.social_terms_required'
    );
  });

  it.each([
    ['Google', (f: RegisterFacade) => f.googleRegister(CREDENTIAL)],
    ['Apple', (f: RegisterFacade) => f.appleRegister('id-token', 'raw-nonce', 'Jan', 'Novak')],
  ])('signs the user in and lands on the orders after a %s signup', (_, run) => {
    tick(true);

    run(facade);

    expect(authService.setSession).toHaveBeenCalledWith(SESSION);
    expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith('auth.login.success');
    expect(router.navigate).toHaveBeenCalled();
  });

  it.each([
    ['Google', (f: RegisterFacade) => f.googleRegister(CREDENTIAL), 'signUpWithGoogle'],
    ['Apple', (f: RegisterFacade) => f.appleRegister('id-token', 'raw-nonce'), 'signUpWithApple'],
  ])('mints no session when the %s sign-up itself failed', (_, run, clientCall) => {
    (authService[clientCall as keyof typeof authService] as jest.Mock).mockReturnValue(
      throwError(() => ({ errors: { Token: 'auth.invalid_google_token' } }))
    );
    tick(true);

    run(facade);

    expect(authService.setSession).not.toHaveBeenCalled();
    expect(snackbar.showApiError).toHaveBeenCalled();
  });

  it('tracks the tick so the buttons can reflect it', () => {
    expect(facade.termsAccepted()).toBe(false);

    tick(true);
    expect(facade.termsAccepted()).toBe(true);

    tick(false);
    expect(facade.termsAccepted()).toBe(false);
  });
});
