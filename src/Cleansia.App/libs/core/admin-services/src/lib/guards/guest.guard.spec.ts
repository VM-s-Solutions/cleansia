import { Injector, runInInjectionContext } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, UrlTree } from '@angular/router';
import { AdminAuthService } from '../services';
import { guestGuard } from './guest.guard';

describe('admin guestGuard', () => {
  function run(isLoggedIn: boolean) {
    TestBed.configureTestingModule({
      providers: [
        { provide: AdminAuthService, useValue: { isLoggedIn: () => isLoggedIn } },
        {
          provide: Router,
          useValue: {
            createUrlTree: jest.fn((commands: unknown[]) => ({ commands }) as unknown as UrlTree),
          },
        },
      ],
    });
    return runInInjectionContext(TestBed.inject(Injector), () =>
      guestGuard(null as never, null as never)
    );
  }

  it('admits a visitor to the sign-in screen', () => {
    expect(run(false)).toBe(true);
  });

  it('lands a signed-in administrator on the role-resolved home instead', () => {
    expect(run(true)).toEqual({ commands: ['/'] });
  });
});
