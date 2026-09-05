import { PLATFORM_ID, runInInjectionContext, Injector } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, UrlTree } from '@angular/router';
import { of, throwError } from 'rxjs';
import { CustomerClient } from '../client/customer-base-client';
import { CustomerAuthService } from '../services';
import { customerMembershipGuard } from './membership.guard';

/**
 * The Plus gate on a route.
 *
 * Before this, a signed-in non-member could open /membership/recurring/create, fill the entire
 * wizard, and be refused by the server at submit with `recurring_booking.membership_required`.
 *
 * The two "let it through" cases below are the ones worth reading: SSR and a failed lookup both
 * pass DELIBERATELY. A guard that redirects during pre-render ships a member the sales page, and
 * one that redirects on a network blip tells a paying member to buy what they own. The server is
 * the control; this is UX.
 */
describe('customerMembershipGuard', () => {
  let injector: Injector;

  function setup(options: {
    browser?: boolean;
    loggedIn?: boolean;
    hasMembership?: boolean;
    lookupFails?: boolean;
  }) {
    const getMine = options.lookupFails
      ? jest.fn(() => throwError(() => new Error('offline')))
      : jest.fn(() => of({ hasMembership: options.hasMembership ?? false }));

    TestBed.configureTestingModule({
      providers: [
        { provide: PLATFORM_ID, useValue: options.browser === false ? 'server' : 'browser' },
        { provide: CustomerAuthService, useValue: { isLoggedIn: () => options.loggedIn ?? true } },
        { provide: CustomerClient, useValue: { membershipClient: { getMine } } },
        {
          provide: Router,
          useValue: {
            createUrlTree: jest.fn((commands: unknown[]) => ({ commands }) as unknown as UrlTree),
          },
        },
      ],
    });
    injector = TestBed.inject(Injector);
    return { getMine };
  }

  const run = () =>
    runInInjectionContext(injector, () =>
      customerMembershipGuard(
        null as never,
        null as never,
      ),
    );

  it('lets a member through', (done) => {
    setup({ hasMembership: true });

    (run() as { subscribe: (o: (v: unknown) => void) => void }).subscribe((result) => {
      expect(result).toBe(true);
      done();
    });
  });

  it('sends a signed-in non-member to the page that sells Plus', (done) => {
    setup({ hasMembership: false });

    (run() as { subscribe: (o: (v: unknown) => void) => void }).subscribe((result) => {
      expect(result).toEqual({ commands: ['/plus'] });
      done();
    });
  });

  it('sends a signed-out visitor to sign in, not to the sales page', () => {
    setup({ loggedIn: false });

    // A membership answer would be meaningless without a session — and telling someone to buy
    // Plus when the real problem is that they are logged out is the wrong sentence.
    expect(run()).toEqual({ commands: ['login'] });
  });

  it('never asks for a membership on the server', () => {
    const { getMine } = setup({ browser: false, hasMembership: false });

    expect(run()).toBe(true);
    expect(getMine).not.toHaveBeenCalled();
  });

  it('fails OPEN when the membership lookup errors', (done) => {
    setup({ lookupFails: true });

    // Deliberate: the server still refuses a non-member, so the only thing a redirect here can
    // achieve is telling a paying member to buy what they already have.
    (run() as { subscribe: (o: (v: unknown) => void) => void }).subscribe((result) => {
      expect(result).toBe(true);
      done();
    });
  });
});
