import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { AUTH_COOKIE_KEYS } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { of } from 'rxjs';
import { PartnerClient } from '../client/base-client';
import { PartnerAuthService } from './partner-auth.service';

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
