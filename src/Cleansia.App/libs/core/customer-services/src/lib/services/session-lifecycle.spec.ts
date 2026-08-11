import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { AUTH_COOKIE_KEYS } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { of } from 'rxjs';
import { CustomerClient } from '../client/customer-base-client';
import { CustomerAuthService } from './customer-auth.service';
import {
  SESSION_LIFECYCLE_LISTENERS,
  SessionLifecycleListener,
} from './session-lifecycle';

describe('CustomerAuthService session lifecycle notifications', () => {
  let listener: jest.Mocked<SessionLifecycleListener>;

  function configure(listeners: SessionLifecycleListener[]): void {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        CustomerAuthService,
        {
          provide: CustomerClient,
          useValue: {
            authClient: { logout: jest.fn().mockReturnValue(of(true)) },
          },
        },
        { provide: Router, useValue: { navigate: jest.fn() } },
        {
          provide: TranslateService,
          useValue: { currentLang: 'cs', getDefaultLang: () => 'en' },
        },
        { provide: AUTH_COOKIE_KEYS, useValue: { csrfToken: 'csrf', refreshTokenExp: 'exp', role: 'role' } },
        ...listeners.map((value) => ({
          provide: SESSION_LIFECYCLE_LISTENERS,
          useValue: value,
          multi: true,
        })),
      ],
    });
  }

  beforeEach(() => {
    localStorage.clear();
    listener = { onSessionStarted: jest.fn(), onSessionEnded: jest.fn() };
    configure([listener]);
  });

  it('notifies every listener when a session starts', () => {
    TestBed.inject(CustomerAuthService).setSession({
      csrfToken: 'c',
      refreshTokenExpiresAt: new Date('2030-01-01T00:00:00Z'),
    } as never);

    expect(listener.onSessionStarted).toHaveBeenCalledTimes(1);
    expect(listener.onSessionEnded).not.toHaveBeenCalled();
  });

  it('notifies every listener when the session is removed', () => {
    TestBed.inject(CustomerAuthService).removeSession();

    expect(listener.onSessionEnded).toHaveBeenCalledTimes(1);
  });

  it('notifies on logout, which is what blanks another user\'s cached data', () => {
    TestBed.inject(CustomerAuthService).logout().subscribe();

    expect(listener.onSessionEnded).toHaveBeenCalledTimes(1);
  });

  it('works with no listener registered at all', () => {
    configure([]);

    expect(() => TestBed.inject(CustomerAuthService).removeSession()).not.toThrow();
  });
});
