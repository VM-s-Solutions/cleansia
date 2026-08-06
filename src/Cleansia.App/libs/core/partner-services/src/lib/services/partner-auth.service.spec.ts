import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { AUTH_COOKIE_KEYS } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { PartnerClient } from '../client/base-client';
import {
  ConsentType,
  GdprClient,
  GrantConsentCommand,
} from '../client/partner-client';
import { PartnerAuthService } from './partner-auth.service';
import { SignupConsentService } from './signup-consent.service';

function gdprClientMock(): Record<string, jest.Mock> {
  return {
    consentsGet: jest.fn().mockReturnValue(of([])),
    consentsPost: jest.fn().mockReturnValue(of(undefined)),
  };
}

describe('PartnerAuthService command payloads', () => {
  let service: PartnerAuthService;
  let authClient: Record<string, jest.Mock>;

  function sentBody(method: string): Record<string, unknown> {
    return authClient[method].mock.calls[0][0].toJSON();
  }

  beforeEach(() => {
    authClient = {
      login: jest.fn().mockReturnValue(of({})),
      register: jest.fn().mockReturnValue(of(true)),
      registerEmployee: jest.fn().mockReturnValue(of(true)),
      confirmUserEmail: jest.fn().mockReturnValue(of({})),
      resendConfirmationEmail: jest.fn().mockReturnValue(of(true)),
      googleAuth: jest.fn().mockReturnValue(of({})),
      logout: jest.fn().mockReturnValue(of(true)),
      refreshToken: jest.fn().mockReturnValue(of({})),
    };

    TestBed.configureTestingModule({
      providers: [
        PartnerAuthService,
        { provide: PartnerClient, useValue: { authClient } },
        { provide: GdprClient, useValue: gdprClientMock() },
        { provide: Router, useValue: { navigate: jest.fn() } },
        {
          provide: TranslateService,
          useValue: { currentLang: 'cs', getDefaultLang: () => 'en' },
        },
        {
          provide: AUTH_COOKIE_KEYS,
          useValue: {
            csrfToken: 'csrf',
            refreshTokenExp: 'exp',
            role: 'role',
          },
        },
      ],
    });

    service = TestBed.inject(PartnerAuthService);
  });

  it('sends the credentials on login', () => {
    service.login('cleaner@cleansia.cz', 'pw', true).subscribe();

    expect(sentBody('login')).toEqual({
      email: 'cleaner@cleansia.cz',
      password: 'pw',
      rememberMe: true,
    });
  });

  it('sends the profile plus the active language on register', () => {
    service.register('a@b.cz', 'pw', 'Jan', 'Novak', 'REF10').subscribe();

    expect(sentBody('register')).toEqual({
      email: 'a@b.cz',
      password: 'pw',
      firstName: 'Jan',
      lastName: 'Novak',
      language: 'cs',
      referralCode: 'REF10',
    });
  });

  it('omits the referral code when none was supplied', () => {
    service.register('a@b.cz', 'pw', 'Jan', 'Novak').subscribe();

    expect(sentBody('register')['referralCode']).toBeUndefined();
  });

  it('sends the profile plus the active language on employee register', () => {
    service.registerEmployee('a@b.cz', 'pw', 'Jan', 'Novak').subscribe();

    expect(sentBody('registerEmployee')).toEqual({
      email: 'a@b.cz',
      password: 'pw',
      firstName: 'Jan',
      lastName: 'Novak',
      language: 'cs',
    });
  });

  it('sends the code and email on confirm', () => {
    service.confirmUserEmail('123456', 'a@b.cz').subscribe();

    expect(sentBody('confirmUserEmail')).toEqual({
      code: '123456',
      email: 'a@b.cz',
    });
  });

  it('sends the email and language on resend', () => {
    service.resendEmailConfirmation('a@b.cz').subscribe();

    expect(sentBody('resendConfirmationEmail')).toEqual({
      email: 'a@b.cz',
      language: 'cs',
    });
  });

  it('sends the google identity', () => {
    service
      .authenticateWithGoogle('tok', 'gid', 'a@b.cz', 'Jan', 'Novak')
      .subscribe();

    expect(sentBody('googleAuth')).toEqual({
      token: 'tok',
      googleId: 'gid',
      email: 'a@b.cz',
      firstName: 'Jan',
      lastName: 'Novak',
    });
  });

  it('posts an empty token on logout — the refresh token is cookie-carried', () => {
    service.logout().subscribe();

    expect(sentBody('logout')).toEqual({ token: '' });
  });

  it('posts an empty token on refresh — the refresh token is cookie-carried', () => {
    service.refreshSession().subscribe();

    expect(sentBody('refreshToken')).toEqual({ token: '' });
  });

  it('falls back to the default language when none is active', () => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        PartnerAuthService,
        { provide: PartnerClient, useValue: { authClient } },
        { provide: GdprClient, useValue: gdprClientMock() },
        { provide: Router, useValue: { navigate: jest.fn() } },
        {
          provide: TranslateService,
          useValue: { currentLang: '', getDefaultLang: () => 'en' },
        },
        {
          provide: AUTH_COOKIE_KEYS,
          useValue: { csrfToken: 'csrf', refreshTokenExp: 'exp', role: 'role' },
        },
      ],
    });

    TestBed.inject(PartnerAuthService)
      .resendEmailConfirmation('a@b.cz')
      .subscribe();

    expect(sentBody('resendConfirmationEmail')['language']).toBe('en');
  });
});

describe('PartnerAuthService signup consent delivery', () => {
  let service: PartnerAuthService;
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
    gdprClient = gdprClientMock();

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        PartnerAuthService,
        { provide: PartnerClient, useValue: { authClient: {} } },
        { provide: GdprClient, useValue: gdprClient },
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

    service = TestBed.inject(PartnerAuthService);
    signupConsent = TestBed.inject(SignupConsentService);
  });

  it('grants the signup tick at the first session, keyed on the identity the server returned', () => {
    signupConsent.record('cleaner@example.com');

    startSession('cleaner@example.com');

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
    signupConsent.record('cleaner@example.com');

    expect(() => startSession('cleaner@example.com')).not.toThrow();

    expect(service.isLoggedIn()).toBe(true);
    expect(localStorage.getItem('csrf')).toBe('csrf-value');
  });
});
