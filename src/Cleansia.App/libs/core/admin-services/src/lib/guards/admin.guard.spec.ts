import { Injector, runInInjectionContext } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, UrlTree } from '@angular/router';
import { CleansiaAdminRoute } from '@cleansia/services';
import { AdminAuthService } from '../services';
import { adminGuard } from './admin.guard';

/**
 * The admin audience never mints a token for an Employee, so an Employee role in the stored hint
 * is a stale session from another app on the same origin, not a signed-in cleaner with reduced
 * rights. The guard used to admit it.
 *
 * A refusal is a UrlTree, not a navigation started from inside the guard: the router lands it
 * itself, once, instead of racing a second navigation against the one it is still resolving.
 *
 * An administrator whose password someone else chose (another administrator's create, or a seed)
 * is held on the profile, whose only content is the password change, until the change succeeds.
 */
describe('adminGuard', () => {
  function run(
    auth: { isLoggedIn: boolean; isAdministrator: boolean; passwordChangeRequired?: boolean },
    path: string = CleansiaAdminRoute.ORDER_MANAGEMENT
  ) {
    TestBed.configureTestingModule({
      providers: [
        {
          provide: AdminAuthService,
          useValue: {
            isLoggedIn: () => auth.isLoggedIn,
            isAdministrator: () => auth.isAdministrator,
            passwordChangeRequired: () => auth.passwordChangeRequired ?? false,
          },
        },
        {
          provide: Router,
          useValue: {
            createUrlTree: jest.fn((commands: unknown[]) => ({ commands }) as unknown as UrlTree),
          },
        },
      ],
    });
    return runInInjectionContext(TestBed.inject(Injector), () =>
      adminGuard({ routeConfig: { path } } as unknown as ActivatedRouteSnapshot, null as never)
    );
  }

  it('admits a signed-in administrator', () => {
    expect(run({ isLoggedIn: true, isAdministrator: true })).toBe(true);
  });

  it('sends a signed-out visitor to sign in', () => {
    expect(run({ isLoggedIn: false, isAdministrator: false })).toEqual({ commands: ['/login'] });
  });

  it('sends a stale non-administrator session to /unauthorized', () => {
    expect(run({ isLoggedIn: true, isAdministrator: false })).toEqual({
      commands: ['/unauthorized'],
    });
  });

  it('holds an administrator who must change the password on the profile', () => {
    expect(
      run({ isLoggedIn: true, isAdministrator: true, passwordChangeRequired: true })
    ).toEqual({ commands: ['/profile'] });
  });

  it('admits the held administrator to the profile, where the password is changed', () => {
    expect(
      run(
        { isLoggedIn: true, isAdministrator: true, passwordChangeRequired: true },
        CleansiaAdminRoute.PROFILE
      )
    ).toBe(true);
  });

  it('holds no signed-out visitor on the profile: sign-in comes first', () => {
    expect(
      run(
        { isLoggedIn: false, isAdministrator: false, passwordChangeRequired: true },
        CleansiaAdminRoute.PROFILE
      )
    ).toEqual({ commands: ['/login'] });
  });
});
