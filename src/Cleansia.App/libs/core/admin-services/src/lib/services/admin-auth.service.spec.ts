import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { AUTH_COOKIE_KEYS, Role } from '@cleansia/services';
import { of } from 'rxjs';
import { AdminClient } from '../client/admin-base-client';
import { JwtTokenResponse } from '../client/admin-client';
import { AdminAuthService } from './admin-auth.service';

const KEYS = {
  token: 't',
  refreshToken: 'rt',
  refreshTokenExp: 'exp',
  role: 'role',
  csrfToken: 'csrf',
  adminRole: 'admin_role',
  userId: 'user_id',
};

function tokenResponse(fields: Partial<JwtTokenResponse>): JwtTokenResponse {
  return JwtTokenResponse.fromJS({
    csrfToken: 'csrf-1',
    refreshTokenExpiresAt: new Date(Date.now() + 60_000).toISOString(),
    role: Role.ADMINISTRATOR,
    ...fields,
  });
}

/**
 * The session is the web's only view of the token: the JWT is HttpOnly, so the role and the
 * administrator's role ride the response and live in localStorage as hints. The refresh path
 * re-runs setSession, which is what lets a role changed on the server reach the hint without a
 * new sign-in.
 */
describe('AdminAuthService session', () => {
  let service: AdminAuthService;
  let refreshToken: jest.Mock;

  beforeEach(() => {
    localStorage.clear();
    refreshToken = jest.fn();

    TestBed.configureTestingModule({
      providers: [
        AdminAuthService,
        { provide: AdminClient, useValue: { adminAuthClient: { refreshToken } } },
        { provide: Router, useValue: { navigate: jest.fn() } },
        { provide: AUTH_COOKIE_KEYS, useValue: KEYS },
      ],
    });

    service = TestBed.inject(AdminAuthService);
  });

  it('stores the administrator role and the account id beside the profile on sign-in', () => {
    service.setSession(tokenResponse({ adminRole: 'Support', userId: 'usr-1' }));

    expect(localStorage.getItem(KEYS.role)).toBe(Role.ADMINISTRATOR);
    expect(localStorage.getItem(KEYS.adminRole)).toBe('Support');
    expect(service.getUserId()).toBe('usr-1');
  });

  it('replaces the stored administrator role from a refresh response without a new sign-in', () => {
    service.setSession(tokenResponse({ adminRole: 'Support' }));
    refreshToken.mockReturnValue(of(tokenResponse({ adminRole: 'Manager' })));

    service.refreshSession().subscribe();

    expect(localStorage.getItem(KEYS.adminRole)).toBe('Manager');
  });

  it('clears a stored administrator role when the response carries none', () => {
    service.setSession(tokenResponse({ adminRole: 'Support' }));

    service.setSession(tokenResponse({ adminRole: undefined }));

    expect(localStorage.getItem(KEYS.adminRole)).toBeNull();
  });

  it('removes the administrator role with the rest of the session', () => {
    service.setSession(tokenResponse({ adminRole: 'Accountant', userId: 'usr-1' }));

    service.removeSession();

    expect(localStorage.getItem(KEYS.adminRole)).toBeNull();
    expect(service.getUserId()).toBeNull();
    expect(localStorage.getItem(KEYS.role)).toBeNull();
    expect(localStorage.getItem(KEYS.csrfToken)).toBeNull();
  });

  it('is an administrator only for a live session whose profile is Administrator', () => {
    service.setSession(tokenResponse({ adminRole: 'Support' }));
    expect(service.isAdministrator()).toBe(true);

    localStorage.setItem(KEYS.role, Role.EMPLOYEE);
    expect(service.isAdministrator()).toBe(false);

    service.removeSession();
    expect(service.isAdministrator()).toBe(false);
  });
});
