import { Injector, runInInjectionContext } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, UrlTree } from '@angular/router';
import { PartnerAuthService } from '../services';
import { authGuard } from './auth.guard';

describe('partner authGuard', () => {
  function run(isLoggedIn: boolean) {
    TestBed.configureTestingModule({
      providers: [
        { provide: PartnerAuthService, useValue: { isLoggedIn: () => isLoggedIn } },
        {
          provide: Router,
          useValue: {
            createUrlTree: jest.fn((commands: unknown[]) => ({ commands }) as unknown as UrlTree),
          },
        },
      ],
    });
    return runInInjectionContext(TestBed.inject(Injector), () =>
      authGuard(null as never, null as never)
    );
  }

  it('admits a signed-in cleaner', () => {
    expect(run(true)).toBe(true);
  });

  it('sends a signed-out visitor to sign in', () => {
    expect(run(false)).toEqual({ commands: ['/login'] });
  });
});
