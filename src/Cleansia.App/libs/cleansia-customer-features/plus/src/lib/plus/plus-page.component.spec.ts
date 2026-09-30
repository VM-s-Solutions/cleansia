import { Component, input, output, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideRouter, Router } from '@angular/router';
import { MembershipManagementComponent } from '@cleansia-customer/profile';
import {
  CustomerAuthService,
  CustomerClient,
  GetMyMembershipResponse,
  MembershipStatus,
} from '@cleansia/customer-services';
import { selectMarketCountryId } from '@cleansia/customer-stores';
import { SnackbarService } from '@cleansia/services';
import { provideMockStore } from '@ngrx/store/testing';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { NEVER, of } from 'rxjs';
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
  let translate: { currentLang: string };

  function build(
    plans: { code?: string; billingInterval?: number }[] = [],
    currencyCode: string | null = null,
  ): PlusPageComponent {
    navigate = jest.fn();
    startCheckout = jest.fn();
    translate = { currentLang: 'en' };
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
        { provide: TranslateService, useValue: translate },
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
              currencyCode: signal<string | null>(currencyCode),
              plusUnavailable: signal(false),
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

  // The locale was a `'cs-CZ'` literal, so an English reader got Czech digit grouping and symbol
  // placement on the Plus page while every order screen formatted per language. The code is the
  // plan response's own (ADR-0059 D3), never a platform default.
  it("labels the price with the plans' currency, grouped the way the reader's language does", () => {
    const component = build([], 'CZK');

    translate.currentLang = 'en';
    expect(component.formatPrice(1200)).toMatch(/CZK\s?1,200/);

    translate.currentLang = 'cs';
    expect(component.formatPrice(1200)).toMatch(/1\s200\sKč/);
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

@Component({ selector: 'cleansia-customer-membership-management', standalone: true, template: '' })
class MembershipPanelStub {
  readonly embedded = input(false);
  readonly cancelled = output<void>();
}

function membership(hasMembership: boolean, status: MembershipStatus): GetMyMembershipResponse {
  const response = new GetMyMembershipResponse();
  response.hasMembership = hasMembership;
  response.status = status;
  return response;
}

/**
 * Cancelling a failed renewal ends the membership now, and with no card update on the web, buying
 * Plus again is the only way back. Rendered with the page's own template, so the panel's binding
 * is what is under test; the panel itself is a stand-in.
 */
describe('PlusPageComponent — after a failed renewal is cancelled in the panel', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('re-reads the membership, so a plan button starts checkout again', async () => {
    const getMine = jest
      .fn()
      .mockReturnValueOnce(of(membership(true, MembershipStatus.PastDue)))
      .mockReturnValue(of(membership(false, MembershipStatus.Cancelled)));
    const createCheckoutSession = jest.fn().mockReturnValue(NEVER);

    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [PlusPageComponent, TranslateModule.forRoot()],
      providers: [
        provideRouter([]),
        provideMockStore({ selectors: [{ selector: selectMarketCountryId, value: 'cze-id' }] }),
        { provide: CustomerAuthService, useValue: { isLoggedIn: signal(true) } },
        {
          provide: CustomerClient,
          useValue: {
            membershipClient: { getPlans: () => of([]), getMine, createCheckoutSession },
          },
        },
        { provide: SnackbarService, useValue: { showError: jest.fn() } },
      ],
    })
      .overrideComponent(PlusPageComponent, {
        remove: { imports: [MembershipManagementComponent] },
        add: { imports: [MembershipPanelStub] },
      })
      .compileComponents();

    const fixture = TestBed.createComponent(PlusPageComponent);
    fixture.detectChanges();
    const panel = () => fixture.debugElement.query(By.directive(MembershipPanelStub));

    fixture.componentInstance.startTrial('PLUS_MONTHLY');
    expect(createCheckoutSession).not.toHaveBeenCalled();

    (panel().componentInstance as MembershipPanelStub).cancelled.emit();
    fixture.detectChanges();

    expect(getMine).toHaveBeenCalledTimes(2);
    expect(panel()).toBeNull();

    fixture.componentInstance.startTrial('PLUS_MONTHLY');
    expect(createCheckoutSession).toHaveBeenCalledWith(
      expect.objectContaining({ planCode: 'PLUS_MONTHLY' }),
    );
  });
});
