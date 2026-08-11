import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { AUTH_COOKIE_KEYS } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { CustomerClient } from '../client/customer-base-client';
import { ConsentType, GrantConsentCommand } from '../client/customer-client';
import { CustomerAuthService } from './customer-auth.service';
import { SESSION_LIFECYCLE_LISTENERS } from './session-lifecycle';
import { SignupConsentService } from './signup-consent.service';

describe('CustomerAuthService command payloads', () => {
  let service: CustomerAuthService;
  let authClient: Record<string, jest.Mock>;
  let userClient: Record<string, jest.Mock>;
  let gdprClient: Record<string, jest.Mock>;

  function bodyAt(
    client: Record<string, jest.Mock>,
    method: string,
    index: number
  ): Record<string, unknown> {
    return client[method].mock.calls[index][0].toJSON();
  }

  function sentBody(
    client: Record<string, jest.Mock>,
    method: string
  ): Record<string, unknown> {
    return bodyAt(client, method, 0);
  }

  function configure(currentLang: string): void {
    TestBed.configureTestingModule({
      providers: [
        CustomerAuthService,
        { provide: CustomerClient, useValue: { authClient, userClient, gdprClient } },
        { provide: Router, useValue: { navigate: jest.fn() } },
        {
          provide: TranslateService,
          useValue: { currentLang, getDefaultLang: () => 'en' },
        },
        {
          provide: SESSION_LIFECYCLE_LISTENERS,
          useValue: { onSessionStarted: jest.fn(), onSessionEnded: jest.fn() },
          multi: true,
        },
        {
          provide: AUTH_COOKIE_KEYS,
          useValue: { csrfToken: 'csrf', refreshTokenExp: 'exp', role: 'role' },
        },
      ],
    });
  }

  beforeEach(() => {
    authClient = {
      login: jest.fn().mockReturnValue(of({})),
      register: jest.fn().mockReturnValue(of(true)),
      confirmUserEmail: jest.fn().mockReturnValue(of({})),
      resendConfirmationEmail: jest.fn().mockReturnValue(of(true)),
      googleAuth: jest.fn().mockReturnValue(of({})),
      appleAuth: jest.fn().mockReturnValue(of({})),
      logout: jest.fn().mockReturnValue(of(true)),
      refreshToken: jest.fn().mockReturnValue(of({})),
    };
    userClient = {
      requestPasswordChange: jest.fn().mockReturnValue(of(true)),
    };
    gdprClient = {
      consentsGet: jest.fn().mockReturnValue(of([])),
      consentsPost: jest.fn().mockReturnValue(of(undefined)),
    };
    localStorage.clear();

    configure('cs');
    service = TestBed.inject(CustomerAuthService);
  });

  it('sends the credentials on login', () => {
    service.login('a@b.cz', 'pw', true).subscribe();

    expect(sentBody(authClient, 'login')).toEqual({
      email: 'a@b.cz',
      password: 'pw',
      rememberMe: true,
    });
  });

  it('uppercases and trims the referral code on register', () => {
    service.register('a@b.cz', 'pw', 'Jan', 'Novak', '  ref10 ').subscribe();

    expect(sentBody(authClient, 'register')).toEqual({
      email: 'a@b.cz',
      password: 'pw',
      firstName: 'Jan',
      lastName: 'Novak',
      language: 'cs',
      referralCode: 'REF10',
    });
  });

  it('omits a whitespace-only referral code', () => {
    service.register('a@b.cz', 'pw', 'Jan', 'Novak', '   ').subscribe();

    expect(sentBody(authClient, 'register')['referralCode']).toBeUndefined();
  });

  it('sends the code and email on confirm', () => {
    service.confirmUserEmail('123456', 'a@b.cz').subscribe();

    expect(sentBody(authClient, 'confirmUserEmail')).toEqual({
      code: '123456',
      email: 'a@b.cz',
    });
  });

  it('sends the email and language on resend', () => {
    service.resendEmailConfirmation('a@b.cz').subscribe();

    expect(sentBody(authClient, 'resendConfirmationEmail')).toEqual({
      email: 'a@b.cz',
      language: 'cs',
    });
  });

  // Provisioning is gated on the tick: a google signup that fails to assert it
  // is refused with `auth.social_account_not_found` instead of creating the
  // account, so the flag belongs in the pinned body rather than beside it.
  it('sends the google identity and the asserted tick on a signup', () => {
    service
      .signUpWithGoogle('tok', 'gid', 'a@b.cz', 'Jan', 'Novak')
      .subscribe();

    expect(sentBody(authClient, 'googleAuth')).toEqual({
      token: 'tok',
      googleId: 'gid',
      email: 'a@b.cz',
      firstName: 'Jan',
      lastName: 'Novak',
      termsAccepted: true,
    });
  });

  it('sends the raw nonce and the asserted tick on an apple signup', () => {
    service.signUpWithApple('idtok', 'raw-nonce', 'Jan').subscribe();

    expect(sentBody(authClient, 'appleAuth')).toEqual({
      identityToken: 'idtok',
      rawNonce: 'raw-nonce',
      firstName: 'Jan',
      lastName: undefined,
      termsAccepted: true,
    });
  });

  // Sign-in asserts nothing on purpose (Q-CONSENT-02): an identity it does not
  // recognize must be turned away, not signed up behind the visitor's back.
  it.each([
    ['google', 'googleAuth', () => service.signInWithGoogle('tok', 'gid', 'a@b.cz', 'Jan', 'Novak')],
    ['apple', 'appleAuth', () => service.signInWithApple('idtok', 'raw-nonce')],
  ])('asserts no tick on a %s sign-in', (_, method, call) => {
    call().subscribe();

    expect(sentBody(authClient, method)['termsAccepted']).toBe(false);
  });

  // One root-provided service serves the signup and the sign-in screen, so the
  // flag has to come from the method called and from nothing that survives it.
  it.each([
    [
      'google',
      'googleAuth',
      () => service.signUpWithGoogle('tok', 'gid', 'a@b.cz', 'Jan', 'Novak'),
      () => service.signInWithGoogle('tok', 'gid', 'a@b.cz', 'Jan', 'Novak'),
    ],
    [
      'apple',
      'appleAuth',
      () => service.signUpWithApple('idtok', 'raw-nonce', 'Jan'),
      () => service.signInWithApple('idtok', 'raw-nonce'),
    ],
  ])('does not carry a preceding %s signup tick into a later sign-in', (_, method, signUp, signIn) => {
    signUp().subscribe();

    signIn().subscribe();

    expect(bodyAt(authClient, method, 1)['termsAccepted']).toBe(false);
  });

  // Unlike email registration, both social branches settle the session inside the
  // response pipeline — so anything the caller records in its own next handler is
  // already too late for the flush that rides setSession.
  it.each([
    ['google', () => service.signUpWithGoogle('tok', 'gid', 'a@b.cz', 'Jan', 'Novak')],
    ['apple', () => service.signUpWithApple('idtok', 'raw-nonce')],
  ])('has already started the session when the %s response reaches the caller', (_, call) => {
    let loggedInWhenResumed: boolean | null = null;

    call().subscribe(() => (loggedInWhenResumed = service.isLoggedIn()));

    expect(loggedInWhenResumed).toBe(true);
  });

  it('sends the email and language on forgot password', () => {
    service.forgotPassword('a@b.cz').subscribe();

    expect(sentBody(userClient, 'requestPasswordChange')).toEqual({
      email: 'a@b.cz',
      language: 'cs',
    });
  });

  it('posts an empty token on logout — the refresh token is cookie-carried', () => {
    service.logout().subscribe();

    expect(sentBody(authClient, 'logout')).toEqual({ token: '' });
  });

  it('posts an empty token on refresh — the refresh token is cookie-carried', () => {
    service.refreshSession().subscribe();

    expect(sentBody(authClient, 'refreshToken')).toEqual({ token: '' });
  });

  it('falls back to the default language when none is active', () => {
    TestBed.resetTestingModule();
    configure('');

    TestBed.inject(CustomerAuthService)
      .resendEmailConfirmation('a@b.cz')
      .subscribe();

    expect(sentBody(authClient, 'resendConfirmationEmail')['language']).toBe(
      'en'
    );
  });
});

describe('CustomerAuthService signup consent delivery', () => {
  let service: CustomerAuthService;
  let signupConsent: SignupConsentService;
  let gdprClient: Record<string, jest.Mock>;

  function startSession(email: string): void {
    service.setSession({
      email,
      csrfToken: 'csrf-value',
      refreshTokenExpiresAt: new Date('2030-01-01T00:00:00Z'),
    } as never);
  }

  beforeEach(() => {
    localStorage.clear();
    gdprClient = {
      consentsGet: jest.fn().mockReturnValue(of([])),
      consentsPost: jest.fn().mockReturnValue(of(undefined)),
    };

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        CustomerAuthService,
        { provide: CustomerClient, useValue: { gdprClient } },
        { provide: Router, useValue: { navigate: jest.fn() } },
        {
          provide: TranslateService,
          useValue: { currentLang: 'cs', getDefaultLang: () => 'en' },
        },
        {
          provide: AUTH_COOKIE_KEYS,
          useValue: { csrfToken: 'csrf', refreshTokenExp: 'exp', role: 'role' },
        },
      ],
    });

    service = TestBed.inject(CustomerAuthService);
    signupConsent = TestBed.inject(SignupConsentService);
  });

  it('grants the signup tick at the first session, keyed on the identity the server returned', () => {
    signupConsent.record('jan@example.com');

    startSession('jan@example.com');

    const bodies = gdprClient['consentsPost'].mock.calls.map(([command]) => {
      expect(command).toBeInstanceOf(GrantConsentCommand);
      return (command as GrantConsentCommand).toJSON();
    });
    expect(bodies).toEqual([
      { consentType: ConsentType.TermsOfService },
      { consentType: ConsentType.PrivacyPolicy },
    ]);
  });

  it('signs the user in even when the grant is refused', () => {
    gdprClient['consentsPost'].mockReturnValue(
      throwError(() => ({ errors: { '': 'common.error_occurred' } }))
    );
    signupConsent.record('jan@example.com');

    expect(() => startSession('jan@example.com')).not.toThrow();

    expect(service.isLoggedIn()).toBe(true);
    expect(localStorage.getItem('csrf')).toBe('csrf-value');
  });
});
