import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { SavedAddressStore } from '@cleansia/customer-stores';
import { AUTH_COOKIE_KEYS } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { of } from 'rxjs';
import { CustomerClient } from '../client/customer-base-client';
import { CustomerAuthService } from './customer-auth.service';

describe('CustomerAuthService command payloads', () => {
  let service: CustomerAuthService;
  let authClient: Record<string, jest.Mock>;
  let userClient: Record<string, jest.Mock>;

  function sentBody(
    client: Record<string, jest.Mock>,
    method: string
  ): Record<string, unknown> {
    return client[method].mock.calls[0][0].toJSON();
  }

  function configure(currentLang: string): void {
    TestBed.configureTestingModule({
      providers: [
        CustomerAuthService,
        { provide: CustomerClient, useValue: { authClient, userClient } },
        { provide: Router, useValue: { navigate: jest.fn() } },
        {
          provide: TranslateService,
          useValue: { currentLang, getDefaultLang: () => 'en' },
        },
        {
          provide: SavedAddressStore,
          useValue: { refresh: jest.fn(), clear: jest.fn() },
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

  it('sends the google identity', () => {
    service
      .authenticateWithGoogle('tok', 'gid', 'a@b.cz', 'Jan', 'Novak')
      .subscribe();

    expect(sentBody(authClient, 'googleAuth')).toEqual({
      token: 'tok',
      googleId: 'gid',
      email: 'a@b.cz',
      firstName: 'Jan',
      lastName: 'Novak',
    });
  });

  it('sends the raw nonce alongside the apple identity token', () => {
    service.authenticateWithApple('idtok', 'raw-nonce', 'Jan').subscribe();

    expect(sentBody(authClient, 'appleAuth')).toEqual({
      identityToken: 'idtok',
      rawNonce: 'raw-nonce',
      firstName: 'Jan',
      lastName: undefined,
    });
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
