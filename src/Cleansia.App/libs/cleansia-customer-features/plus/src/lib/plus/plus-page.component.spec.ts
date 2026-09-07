import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { CustomerAuthService, CustomerClient } from '@cleansia/customer-services';
import { of } from 'rxjs';
import { PlusPageComponent } from './plus-page.component';
import { PlusPageFacade } from './plus-page.facade';

/**
 * `/plus` is the product's ONE page now — the benefits for everyone, the
 * management panel on top for a member — so what its calls to action DO is the
 * thing worth pinning. They used to be links into `/membership/subscribe`, a
 * second sales page behind `customerAuthGuard`; that page is retired and this
 * one starts Stripe Checkout itself.
 *
 * Rendered without its template: every assertion here is about the component
 * class, and the markup is covered by the viewport and contrast checkers.
 */
describe('PlusPageComponent', () => {
  const isLoggedIn = signal(false);
  const isMember = signal(false);
  let navigate: jest.Mock;
  let startCheckout: jest.Mock;

  function build(plans: { code?: string; billingInterval?: number }[] = []): PlusPageComponent {
    navigate = jest.fn();
    startCheckout = jest.fn();
    isMember.set(false);

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        { provide: CustomerAuthService, useValue: { isLoggedIn } },
        { provide: Router, useValue: { navigate } },
        {
          provide: CustomerClient,
          useValue: { membershipClient: { getPlans: () => of([]), getMine: () => of(null) } },
        },
      ],
    });
    TestBed.overrideComponent(PlusPageComponent, {
      set: {
        template: '',
        providers: [
          {
            provide: PlusPageFacade,
            useValue: {
              load: () => undefined,
              startCheckout,
              isMember,
              submitting: signal(false),
              plans: signal(plans),
              monthlyPlan: signal(plans.find((p) => p.billingInterval === 1) ?? null),
              trialDays: signal(14),
              hasExpressPerk: signal(true),
              expressPerMonth: signal(1),
            },
          },
        ],
      },
    });
    return TestBed.createComponent(PlusPageComponent).componentInstance;
  }

  afterEach(() => TestBed.resetTestingModule());

  // Signing up is the first step for someone with no account. There is nothing
  // to check out yet, so the CTA has to be a route rather than a Stripe call.
  it('sends a signed-out visitor to register', () => {
    isLoggedIn.set(false);
    build().startTrial();

    expect(navigate).toHaveBeenCalledWith(['/register']);
    expect(startCheckout).not.toHaveBeenCalled();
  });

  it('starts checkout for the plan a signed-in visitor pressed', () => {
    isLoggedIn.set(true);
    build([{ code: 'PLUS_YEARLY', billingInterval: 2 }]).startTrial('PLUS_YEARLY');

    expect(startCheckout).toHaveBeenCalledWith('PLUS_YEARLY');
    expect(navigate).not.toHaveBeenCalled();
  });

  // The hero and the closing band have no plan of their own — they are the
  // page's general "start the trial", and monthly is what the retired subscribe
  // screen defaulted to.
  it('falls back to the monthly plan when no plan was named', () => {
    isLoggedIn.set(true);
    build([
      { code: 'PLUS_MONTHLY', billingInterval: 1 },
      { code: 'PLUS_YEARLY', billingInterval: 2 },
    ]).startTrial();

    expect(startCheckout).toHaveBeenCalledWith('PLUS_MONTHLY');
  });

  /**
   * The one that would cost real money: a member pressing a plan button is
   * switching plans, which the management panel does through Stripe's
   * subscription swap. Starting checkout would open a SECOND subscription
   * against the same customer.
   */
  it('never starts a second checkout for someone who already has Plus', () => {
    isLoggedIn.set(true);
    const component = build([{ code: 'PLUS_MONTHLY', billingInterval: 1 }]);
    isMember.set(true);

    component.startTrial('PLUS_MONTHLY');

    expect(startCheckout).not.toHaveBeenCalled();
    expect(navigate).toHaveBeenCalledWith([], { fragment: 'membership' });
  });

  it('asks for nothing when the plan catalogue is empty', () => {
    isLoggedIn.set(true);
    build().startTrial();

    expect(startCheckout).not.toHaveBeenCalled();
  });

  it('offers booking without a membership, which needs no account decision', () => {
    expect(build().orderLink).toBe('/order');
  });
});
