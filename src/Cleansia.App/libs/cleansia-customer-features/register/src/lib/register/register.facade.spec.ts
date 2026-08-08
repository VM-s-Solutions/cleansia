import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import {
  ConsentType,
  CustomerAuthService,
  CustomerClient,
  GrantConsentCommand,
  JwtTokenResponse,
  SignupConsentService,
  ValidateReferralQuery,
  ValidateReferralResponse,
} from '@cleansia/customer-services';
import { SnackbarService, extractApiErrorCode } from '@cleansia/services';
import { provideMockStore } from '@ngrx/store/testing';
import { TranslateService } from '@ngx-translate/core';
import { Subject, of, throwError } from 'rxjs';
import { RegisterFacade } from './register.facade';

describe('RegisterFacade — referral landing capture (/r/{code})', () => {
  let facade: RegisterFacade;
  let referralClient: { validate: jest.Mock };
  let authService: {
    register: jest.Mock;
    authenticateWithGoogle: jest.Mock;
    authenticateWithApple: jest.Mock;
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
      authenticateWithGoogle: jest.fn(),
      authenticateWithApple: jest.fn(),
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
        provideMockStore(),
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
      'ABC12'
    );
  });
});

describe('RegisterFacade — Sign in with Apple', () => {
  let facade: RegisterFacade;
  let authService: { authenticateWithApple: jest.Mock; setSession: jest.Mock };
  let snackbar: {
    showApiError: jest.Mock;
    showErrorTranslated: jest.Mock;
    showSuccessTranslated: jest.Mock;
  };
  let router: { navigate: jest.Mock };

  beforeEach(() => {
    authService = { authenticateWithApple: jest.fn(), setSession: jest.fn() };
    snackbar = {
      showApiError: jest.fn(),
      showErrorTranslated: jest.fn(),
      showSuccessTranslated: jest.fn(),
    };
    router = { navigate: jest.fn() };

    TestBed.configureTestingModule({
      providers: [
        RegisterFacade,
        provideMockStore(),
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
    authService.authenticateWithApple.mockReturnValue(of({}));

    facade.appleRegister('id-token', 'raw-nonce', 'Jan', 'Novák');

    // The server hashes this value itself — sending the hash instead fails
    // every sign-up with a generic error, so pin the argument order.
    expect(authService.authenticateWithApple).toHaveBeenCalledWith(
      'id-token',
      'raw-nonce',
      'Jan',
      'Novák'
    );
    expect(authService.setSession).toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalled();
  });

  it('surfaces an Apple sign-up error under the register fallback key', () => {
    // Bare ProblemDetails — the shape NSwag actually throws. Pinning the
    // resolved key as well as the fallback keeps this from passing while the
    // user is shown the generic message instead of the real reason.
    authService.authenticateWithApple.mockReturnValue(
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
  let signupConsent: SignupConsentService;
  let gdprClient: Record<string, jest.Mock>;
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

  function grantedTypes(): unknown[] {
    return gdprClient['consentsPost'].mock.calls.map(([command]) => {
      expect(command).toBeInstanceOf(GrantConsentCommand);
      return (command as GrantConsentCommand).toJSON();
    });
  }

  beforeEach(() => {
    localStorage.clear();
    authService = { register: jest.fn().mockReturnValue(of(true)) };
    router = { navigate: jest.fn() };
    snackbar = {
      showError: jest.fn(),
      showApiError: jest.fn(),
      showSuccessTranslated: jest.fn(),
    };
    gdprClient = {
      consentsGet: jest.fn().mockReturnValue(of([])),
      consentsPost: jest.fn().mockReturnValue(of(undefined)),
    };

    TestBed.configureTestingModule({
      providers: [
        RegisterFacade,
        provideMockStore(),
        { provide: Router, useValue: router },
        { provide: CustomerAuthService, useValue: authService },
        {
          provide: CustomerClient,
          useValue: { referralClient: { validate: jest.fn() }, gdprClient },
        },
        { provide: SnackbarService, useValue: snackbar },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
      ],
    });

    facade = TestBed.inject(RegisterFacade);
    signupConsent = TestBed.inject(SignupConsentService);
  });

  it('grants the ticked documents at the session that follows the signup', () => {
    fillForm(true);

    facade.register();
    signupConsent.flush(EMAIL);

    expect(grantedTypes()).toEqual([
      { consentType: ConsentType.TermsOfService },
      { consentType: ConsentType.PrivacyPolicy },
    ]);
  });

  it('grants nothing when the registration itself failed', () => {
    authService.register.mockReturnValue(throwError(() => new Error('taken')));
    fillForm(true);

    facade.register();
    signupConsent.flush(EMAIL);

    expect(gdprClient['consentsPost']).not.toHaveBeenCalled();
  });

  it('refuses to register at all while the box is unticked', () => {
    fillForm(false);

    facade.register();

    expect(authService.register).not.toHaveBeenCalled();
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

    expect(authService.register).toHaveBeenCalled();
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
    expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith('auth.register.success');
    expect(router.navigate).toHaveBeenCalled();
  });
});

describe('RegisterFacade — the consent ticked at a social signup', () => {
  let facade: RegisterFacade;
  let gdprClient: Record<string, jest.Mock>;
  let authService: {
    authenticateWithGoogle: jest.Mock;
    authenticateWithApple: jest.Mock;
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

  function grantedTypes(): unknown[] {
    return gdprClient['consentsPost'].mock.calls.map(([command]) => {
      expect(command).toBeInstanceOf(GrantConsentCommand);
      return (command as GrantConsentCommand).toJSON();
    });
  }

  beforeEach(() => {
    localStorage.clear();
    authService = {
      authenticateWithGoogle: jest.fn().mockReturnValue(of(SESSION)),
      authenticateWithApple: jest.fn().mockReturnValue(of(SESSION)),
      setSession: jest.fn(),
    };
    router = { navigate: jest.fn() };
    snackbar = {
      showApiError: jest.fn(),
      showErrorTranslated: jest.fn(),
      showSuccessTranslated: jest.fn(),
    };
    gdprClient = {
      consentsGet: jest.fn().mockReturnValue(of([])),
      consentsPost: jest.fn().mockReturnValue(of(undefined)),
    };

    TestBed.configureTestingModule({
      providers: [
        RegisterFacade,
        provideMockStore(),
        { provide: Router, useValue: router },
        { provide: CustomerAuthService, useValue: authService },
        {
          provide: CustomerClient,
          useValue: { referralClient: { validate: jest.fn() }, gdprClient },
        },
        { provide: SnackbarService, useValue: snackbar },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
      ],
    });

    facade = TestBed.inject(RegisterFacade);
  });

  it('grants exactly the two ticked documents on a Google signup', () => {
    tick(true);

    facade.googleRegister(CREDENTIAL);

    expect(grantedTypes()).toEqual([
      { consentType: ConsentType.TermsOfService },
      { consentType: ConsentType.PrivacyPolicy },
    ]);
    expect(grantedTypes().map((body) => (body as Record<string, unknown>)['consentType'])).not.toContain(
      ConsentType.MarketingEmails
    );
  });

  it('grants exactly the two ticked documents on an Apple signup', () => {
    tick(true);

    facade.appleRegister('id-token', 'raw-nonce', 'Jan', 'Novak');

    expect(grantedTypes()).toEqual([
      { consentType: ConsentType.TermsOfService },
      { consentType: ConsentType.PrivacyPolicy },
    ]);
  });

  // The address in the credential is a client-supplied claim the backend ignores;
  // parking a failed delivery under it strands the retry, because every later
  // flush is keyed on the identity the token response carried.
  it('parks a failed delivery under the identity the server returned, not the one Google claimed', () => {
    gdprClient['consentsGet'].mockReturnValueOnce(
      throwError(() => new Error('offline'))
    );
    const claimedByGoogle = [
      'header',
      btoa(JSON.stringify({ sub: 'google-subject', email: 'someone-else@example.com' })),
      'signature',
    ].join('.');
    tick(true);

    facade.googleRegister(claimedByGoogle);

    expect(gdprClient['consentsPost']).not.toHaveBeenCalled();

    TestBed.inject(SignupConsentService).flush(EMAIL);

    expect(grantedTypes()).toEqual([
      { consentType: ConsentType.TermsOfService },
      { consentType: ConsentType.PrivacyPolicy },
    ]);
  });

  it.each([
    ['Google', (f: RegisterFacade) => f.googleRegister(CREDENTIAL), 'authenticateWithGoogle'],
    ['Apple', (f: RegisterFacade) => f.appleRegister('id-token', 'raw-nonce'), 'authenticateWithApple'],
  ])('creates no account at all on %s while the box is unticked', (_, run, clientCall) => {
    tick(false);

    run(facade);

    expect(authService[clientCall as keyof typeof authService]).not.toHaveBeenCalled();
    expect(gdprClient['consentsPost']).not.toHaveBeenCalled();
    expect(authService.setSession).not.toHaveBeenCalled();
    expect(router.navigate).not.toHaveBeenCalled();
    expect(snackbar.showErrorTranslated).toHaveBeenCalledWith(
      'auth.register.social_terms_required'
    );
  });

  // The provider popup runs in its own window and the box stays clickable behind
  // it, so the tick that opened the flow can be gone by the time it returns. The
  // blocker cannot see that; this is the guard that does.
  it('grants nothing when the box is unticked while the provider popup is open', () => {
    const pending$ = new Subject<JwtTokenResponse>();
    authService.authenticateWithGoogle.mockReturnValue(pending$.asObservable());
    tick(true);

    facade.googleRegister(CREDENTIAL);
    tick(false);
    pending$.next(SESSION);

    expect(authService.authenticateWithGoogle).toHaveBeenCalled();
    expect(authService.setSession).toHaveBeenCalled();
    expect(gdprClient['consentsPost']).not.toHaveBeenCalled();
  });

  it.each([
    ['Google', (f: RegisterFacade) => f.googleRegister(CREDENTIAL), 'authenticateWithGoogle'],
    ['Apple', (f: RegisterFacade) => f.appleRegister('id-token', 'raw-nonce'), 'authenticateWithApple'],
  ])('grants nothing when the %s sign-up itself failed', (_, run, clientCall) => {
    (authService[clientCall as keyof typeof authService] as jest.Mock).mockReturnValue(
      throwError(() => ({ errors: { Token: 'auth.invalid_google_token' } }))
    );
    tick(true);

    run(facade);

    expect(gdprClient['consentsPost']).not.toHaveBeenCalled();
    expect(authService.setSession).not.toHaveBeenCalled();
  });

  it('signs the user in even when the grant is refused', () => {
    gdprClient['consentsPost'].mockReturnValue(
      throwError(() => ({ errors: { '': 'common.error_occurred' } }))
    );
    tick(true);

    expect(() => facade.googleRegister(CREDENTIAL)).not.toThrow();

    expect(authService.setSession).toHaveBeenCalledWith(SESSION);
    expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith('auth.login.success');
    expect(router.navigate).toHaveBeenCalled();
  });

  it('tracks the tick so the buttons can reflect it', () => {
    expect(facade.termsAccepted()).toBe(false);

    tick(true);
    expect(facade.termsAccepted()).toBe(true);

    tick(false);
    expect(facade.termsAccepted()).toBe(false);
  });
});
