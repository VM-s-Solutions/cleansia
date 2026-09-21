import { Injector, runInInjectionContext } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, UrlTree } from '@angular/router';
import { AdminAuthService } from '../services';
import { adminGuard } from './admin.guard';

/**
 * The admin audience never mints a token for an Employee, so an Employee role in the stored hint
 * is a stale session from another app on the same origin, not a signed-in cleaner with reduced
 * rights. The guard used to admit it.
 *
 * A refusal is a UrlTree, not a navigation started from inside the guard: the router lands it
 * itself, once, instead of racing a second navigation against the one it is still resolving.
 */
describe('adminGuard', () => {
  function run(auth: { isLoggedIn: boolean; isAdministrator: boolean }) {
    TestBed.configureTestingModule({
      providers: [
        {
          provide: AdminAuthService,
          useValue: {
            isLoggedIn: () => auth.isLoggedIn,
            isAdministrator: () => auth.isAdministrator,
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
      adminGuard(null as never, null as never)
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
});
