import { Injector, PLATFORM_ID, runInInjectionContext } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, RouterStateSnapshot, UrlTree } from '@angular/router';
import { CustomerAuthService } from '../services';
import { customerAuthGuard } from './auth.guard';

/**
 * An order e-mail links an account booking to `/orders/:id`. Signed out, the
 * guard used to send the reader to login and forget the order, so the e-mail's
 * button landed on the order board instead of the booking it named.
 */
describe('customerAuthGuard', () => {
  let injector: Injector;
  let createUrlTree: jest.Mock;

  function setup(loggedIn: boolean, browser = true) {
    createUrlTree = jest.fn(
      (commands: unknown[], extras: unknown) => ({ commands, extras }) as unknown as UrlTree,
    );
    TestBed.configureTestingModule({
      providers: [
        { provide: PLATFORM_ID, useValue: browser ? 'browser' : 'server' },
        { provide: CustomerAuthService, useValue: { isLoggedIn: () => loggedIn } },
        { provide: Router, useValue: { createUrlTree } },
      ],
    });
    injector = TestBed.inject(Injector);
  }

  const run = (url: string) =>
    runInInjectionContext(injector, () =>
      customerAuthGuard(null as never, { url } as RouterStateSnapshot),
    );

  it('lets a signed-in customer through', () => {
    setup(true);

    expect(run('/orders/01ORDER')).toBe(true);
  });

  it('sends a signed-out reader to login carrying the page they asked for', () => {
    setup(false);

    expect(run('/orders/01ORDER')).toEqual({
      commands: ['/login'],
      extras: { queryParams: { returnUrl: '/orders/01ORDER' } },
    });
  });

  it('lets the server render through — it has no session to read', () => {
    setup(false, false);

    expect(run('/orders/01ORDER')).toBe(true);
  });
});
