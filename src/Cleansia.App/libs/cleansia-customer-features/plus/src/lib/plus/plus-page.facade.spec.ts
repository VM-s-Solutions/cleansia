import { TestBed } from '@angular/core/testing';
import {
  CreateMembershipCheckoutSessionCommand,
  CustomerClient,
  GetMembershipPlansResponse,
} from '@cleansia/customer-services';
import { selectMarketCountryId } from '@cleansia/customer-stores';
import { SnackbarService } from '@cleansia/services';
import { MockStore, provideMockStore } from '@ngrx/store/testing';
import { TranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { PlusPageFacade } from './plus-page.facade';

/** `BillingInterval.Monthly` / `.Yearly` as they arrive on the wire. */
const MONTHLY = 1;
const YEARLY = 2;

function plan(fields: Partial<GetMembershipPlansResponse>): GetMembershipPlansResponse {
  const response = new GetMembershipPlansResponse();
  response.code = fields.code ?? 'PLUS_MONTHLY';
  response.name = fields.name ?? 'Cleansia Plus';
  response.price = fields.price ?? 199;
  response.monthlyEquivalentPrice = fields.monthlyEquivalentPrice ?? 199;
  response.billingInterval = fields.billingInterval ?? MONTHLY;
  response.discountPercentage = fields.discountPercentage ?? 5;
  response.freeCancellationWindowHours = fields.freeCancellationWindowHours ?? 4;
  response.allowsExpressUpgrade = fields.allowsExpressUpgrade ?? true;
  response.expressUpgradesPerMonth = fields.expressUpgradesPerMonth ?? 1;
  response.trialPeriodDays = fields.trialPeriodDays ?? 14;
  response.savingsPercentVsMonthly = fields.savingsPercentVsMonthly ?? 0;
  response.currencyCode = fields.currencyCode ?? 'CZK';
  return response;
}

/** What `Membership/GetPlans` actually returns against the seeded catalogue. */
const SEEDED = [
  plan({ code: 'PLUS_MONTHLY', billingInterval: MONTHLY, price: 199 }),
  plan({
    code: 'PLUS_YEARLY',
    billingInterval: YEARLY,
    price: 2030,
    monthlyEquivalentPrice: 169.17,
    savingsPercentVsMonthly: 15,
  }),
];

describe('PlusPageFacade', () => {
  let facade: PlusPageFacade;
  let store: MockStore;
  let getPlans: jest.Mock;
  let createCheckoutSession: jest.Mock;

  // Resets first: several tests re-mock `getPlans` and rebuild, and configuring
  // a TestBed that has already been instantiated throws rather than replacing
  // the provider.
  function build(): void {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        PlusPageFacade,
        provideMockStore({
          selectors: [{ selector: selectMarketCountryId, value: 'cze-id' }],
        }),
        {
          provide: CustomerClient,
          useValue: { membershipClient: { getPlans, createCheckoutSession } },
        },
        // Reached only by startCheckout's failure path, which these plan-facts
        // cases never take — the facade still needs them to construct.
        { provide: SnackbarService, useValue: { showError: jest.fn() } },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
      ],
    });
    store = TestBed.inject(MockStore);
    facade = TestBed.inject(PlusPageFacade);
  }

  beforeEach(() => {
    getPlans = jest.fn().mockReturnValue(of(SEEDED));
    createCheckoutSession = jest.fn().mockReturnValue(of({ checkoutUrl: '' }));
    build();
  });

  afterEach(() => TestBed.resetTestingModule());

  // The plans are priced per market (ADR-0059 D3): the read carries the chosen market's country
  // and the label is the response's own code — never a platform default.
  describe('the market a plan is priced for', () => {
    it('asks for the plans in the chosen market when the page loads', () => {
      facade.load();

      expect(getPlans).toHaveBeenCalledWith('cze-id');
    });

    it('labels the price with the currency the response carries', () => {
      getPlans.mockReturnValue(of([plan({ currencyCode: 'EUR' })]));
      build();

      facade.load();

      expect(facade.currencyCode()).toBe('EUR');
    });

    it('re-reads the plans when the customer switches market', () => {
      facade.load();
      store.overrideSelector(selectMarketCountryId, 'svk-id');
      store.refreshState();

      expect(getPlans).toHaveBeenLastCalledWith('svk-id');
      expect(getPlans).toHaveBeenCalledTimes(2);
    });

    it('sends no country when no market resolved', () => {
      store.overrideSelector(selectMarketCountryId, null);
      store.refreshState();

      facade.load();

      expect(getPlans).toHaveBeenCalledWith(undefined);
    });

    it('reports Plus unavailable when the market lists no plan', () => {
      getPlans.mockReturnValue(of([]));
      build();

      facade.load();

      expect(facade.plusUnavailable()).toBe(true);
      expect(facade.hasPlans()).toBe(false);
    });
  });

  describe('checkout', () => {
    it('subscribes in the chosen market', () => {
      facade.load();

      facade.startCheckout('PLUS_MONTHLY');

      const command = createCheckoutSession.mock.calls[0][0] as CreateMembershipCheckoutSessionCommand;
      expect(command.planCode).toBe('PLUS_MONTHLY');
      expect(command.countryId).toBe('cze-id');
    });

    it('sends no country when no market resolved', () => {
      store.overrideSelector(selectMarketCountryId, null);
      store.refreshState();

      facade.startCheckout('PLUS_MONTHLY');

      const command = createCheckoutSession.mock.calls[0][0] as CreateMembershipCheckoutSessionCommand;
      expect(command.countryId).toBeUndefined();
    });
  });

  it('splits the two plans by billing interval, not by code', () => {
    facade.load();
    expect(facade.monthlyPlan()?.code).toBe('PLUS_MONTHLY');
    expect(facade.yearlyPlan()?.code).toBe('PLUS_YEARLY');
  });

  // Every number the page states about the membership comes from here rather
  // than from copy. These four are the ones the hero, the perk cards and the
  // comparison table all interpolate.
  it('reads the benefit figures off the plan', () => {
    facade.load();
    expect(facade.discountPercent()).toBe(5);
    expect(facade.cancellationHours()).toBe(4);
    expect(facade.expressPerMonth()).toBe(1);
    expect(facade.trialDays()).toBe(14);
  });

  it('takes the savings percentage from the yearly plan, which is where the server puts it', () => {
    facade.load();
    // The monthly plan carries 0 — it is the baseline the yearly is measured
    // against — so reading the figure off the wrong plan silently renders "0 %".
    expect(SEEDED[0].savingsPercentVsMonthly).toBe(0);
    expect(facade.yearlySavingsPercent()).toBe(15);
  });

  it('answers the benefit figures from whatever loaded when no monthly plan exists', () => {
    getPlans.mockReturnValue(
      of([plan({ code: 'PLUS_YEARLY', billingInterval: YEARLY, discountPercentage: 7 })]),
    );
    build();
    facade.load();
    expect(facade.monthlyPlan()).toBeNull();
    expect(facade.discountPercent()).toBe(7);
  });

  describe('the express perk', () => {
    it('is advertised when the plan waives one', () => {
      facade.load();
      expect(facade.hasExpressPerk()).toBe(true);
    });

    // `AllowsExpressUpgrade` is a per-plan column an admin can turn off. The
    // perk card, the comparison row and the FAQ entry are all gated on this,
    // so a plan without the waiver must not be sold one.
    it('is withheld when the plan does not allow the upgrade', () => {
      getPlans.mockReturnValue(of([plan({ allowsExpressUpgrade: false })]));
      build();
      facade.load();
      expect(facade.hasExpressPerk()).toBe(false);
    });

    it('is withheld when the plan allows it but waives none per month', () => {
      getPlans.mockReturnValue(
        of([plan({ allowsExpressUpgrade: true, expressUpgradesPerMonth: 0 })]),
      );
      build();
      facade.load();
      expect(facade.hasExpressPerk()).toBe(false);
    });
  });

  describe('when the endpoint does not answer', () => {
    beforeEach(() => {
      getPlans.mockReturnValue(throwError(() => new Error('offline')));
      build();
      facade.load();
    });

    // The page keeps its perks, its comparison and its FAQ — all still true
    // without a price — rather than showing an error. Only the blocks that
    // would have to state a number are withheld.
    it('leaves the page without prices instead of failing', () => {
      expect(facade.hasPlans()).toBe(false);
      expect(facade.monthlyPlan()).toBeNull();
      expect(facade.yearlyPlan()).toBeNull();
    });

    // A failed read is not "Plus is not on sale here" — that claim needs an answer.
    it('does not claim Plus is unavailable', () => {
      expect(facade.plusUnavailable()).toBe(false);
    });

    it('stops loading, so the page is never stuck', () => {
      expect(facade.loading()).toBe(false);
    });

    // Zero is the one honest answer here: it makes every interpolated claim
    // read as "0 %" rather than crashing or rendering "undefined", and the
    // blocks that would show it are gated on hasPlans() anyway.
    it('reports no figures rather than undefined ones', () => {
      expect(facade.discountPercent()).toBe(0);
      expect(facade.cancellationHours()).toBe(0);
      expect(facade.expressPerMonth()).toBe(0);
      expect(facade.trialDays()).toBe(0);
      expect(facade.yearlySavingsPercent()).toBe(0);
      expect(facade.hasExpressPerk()).toBe(false);
    });
  });

  it('reads the plans once per market, however many surfaces ask', () => {
    facade.load();
    facade.load();
    expect(getPlans).toHaveBeenCalledTimes(1);
  });
});
