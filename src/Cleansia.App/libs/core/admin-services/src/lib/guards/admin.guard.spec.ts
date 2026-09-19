import { Injector, runInInjectionContext } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { AdminAuthService } from '../services';
import { adminGuard } from './admin.guard';

/**
 * The admin audience never mints a token for an Employee, so an Employee role in the stored hint
 * is a stale session from another app on the same origin, not a signed-in cleaner with reduced
 * rights. The guard used to admit it.
 */
describe('adminGuard', () => {
  let navigate: jest.Mock;

  function run(auth: { isLoggedIn: boolean; isAdministrator: boolean }) {
    navigate = jest.fn().mockResolvedValue(true);
    TestBed.configureTestingModule({
      providers: [
        {
          provide: AdminAuthService,
          useValue: {
            isLoggedIn: () => auth.isLoggedIn,
            isAdministrator: () => auth.isAdministrator,
          },
        },
        { provide: Router, useValue: { navigate } },
      ],
    });
    return runInInjectionContext(TestBed.inject(Injector), () =>
      adminGuard(null as never, null as never)
    );
  }

  it('admits a signed-in administrator', () => {
    expect(run({ isLoggedIn: true, isAdministrator: true })).toBe(true);
    expect(navigate).not.toHaveBeenCalled();
  });

  it('sends a signed-out visitor to sign in', () => {
    run({ isLoggedIn: false, isAdministrator: false });

    expect(navigate).toHaveBeenCalledWith(['login']);
  });

  it('sends a stale non-administrator session to /unauthorized', () => {
    run({ isLoggedIn: true, isAdministrator: false });

    expect(navigate).toHaveBeenCalledWith(['unauthorized']);
  });
});
