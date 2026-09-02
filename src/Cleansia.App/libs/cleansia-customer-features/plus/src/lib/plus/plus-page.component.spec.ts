import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { CustomerAuthService, CustomerClient } from '@cleansia/customer-services';
import { of } from 'rxjs';
import { PlusPageComponent } from './plus-page.component';

/**
 * The page's whole reason for existing is that `/membership` is guarded, so
 * where its calls to action point is the thing worth pinning. Rendered without
 * its template — every assertion here is about the component class, and the
 * markup is covered by the viewport and contrast checkers instead.
 */
describe('PlusPageComponent', () => {
  const isLoggedIn = signal(false);

  function build(): PlusPageComponent {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        { provide: CustomerAuthService, useValue: { isLoggedIn } },
        {
          provide: CustomerClient,
          useValue: { membershipClient: { getPlans: () => of([]) } },
        },
      ],
    });
    TestBed.overrideComponent(PlusPageComponent, { set: { template: '' } });
    return TestBed.createComponent(PlusPageComponent).componentInstance;
  }

  afterEach(() => TestBed.resetTestingModule());

  // Signing up is the first step for someone with no account. Sending them to
  // `/membership/subscribe` is what the home page's CTA used to do, and it is
  // behind `customerAuthGuard` — they arrived at a login form instead.
  it('sends a signed-out visitor to register', () => {
    isLoggedIn.set(false);
    expect(build().subscribeLink()).toBe('/register');
  });

  it('sends a signed-in visitor straight to the subscribe screen', () => {
    isLoggedIn.set(true);
    expect(build().subscribeLink()).toBe('/membership/subscribe');
  });

  // A computed, not a value read once in the constructor: the session can end
  // or begin while the page is open, and the link has to follow it.
  it('follows the session without being rebuilt', () => {
    isLoggedIn.set(false);
    const component = build();
    expect(component.subscribeLink()).toBe('/register');

    isLoggedIn.set(true);
    expect(component.subscribeLink()).toBe('/membership/subscribe');
  });

  it('offers booking without a membership, which needs no account decision', () => {
    expect(build().orderLink).toBe('/order');
  });

  // Mirrors `BookingPolicy.ExpressSurchargeRate` through the shared model the
  // wizard's scheduling step reads, so the two quote the same number.
  it('states the express surcharge as a whole percentage', () => {
    expect(build().expressRatePercent).toBe(20);
  });

  describe('price formatting', () => {
    // Intl separates the amount from the currency with a NO-BREAK space, not
    // a plain one, so the expectations normalise it rather than carrying a
    // character that is invisible in a diff and trips no-irregular-whitespace.
    const plain = (value: string): string => value.replace(/\u00a0/g, ' ');

    it('drops the decimals on a whole-koruna plan', () => {
      // Both seeded plans are whole numbers; "199,00 Kč" on a price card reads
      // as a form field rather than as a price.
      expect(plain(build().formatCzk(199))).toBe('199 Kč');
    });

    it('keeps them when a plan is priced to the halér', () => {
      expect(plain(build().formatCzk(169.17))).toBe('169,17 Kč');
    });
  });
});
