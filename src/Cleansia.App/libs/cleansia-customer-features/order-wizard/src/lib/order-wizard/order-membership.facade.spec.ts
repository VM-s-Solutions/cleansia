import { PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import {
  CustomerClient,
  GetMembershipPlansResponse,
  GetMyMembershipResponse,
} from '@cleansia/customer-services';
import { selectMarketCountryId } from '@cleansia/customer-stores';
import { MockStore, provideMockStore } from '@ngrx/store/testing';
import { of, throwError } from 'rxjs';
import { OrderMembershipFacade } from './order-membership.facade';

function buildMembership(fields: {
  hasMembership?: boolean;
  freeCancellationWindowHours?: number;
  expressUpgradesPerMonth?: number;
  expressUpgradesRemaining?: number;
  trialEndsAtUtc?: Date;
}): GetMyMembershipResponse {
  const response = new GetMyMembershipResponse();
  response.hasMembership = fields.hasMembership ?? true;
  response.freeCancellationWindowHours = fields.freeCancellationWindowHours;
  response.expressUpgradesPerMonth = fields.expressUpgradesPerMonth;
  response.expressUpgradesRemaining = fields.expressUpgradesRemaining;
  response.trialEndsAtUtc = fields.trialEndsAtUtc;
  return response;
}

describe('OrderMembershipFacade', () => {
  let facade: OrderMembershipFacade;
  let store: MockStore;
  let membershipClient: { getMine: jest.Mock; getPlans: jest.Mock };

  function build(platform: 'server' | 'browser'): void {
    membershipClient = {
      getMine: jest.fn().mockReturnValue(
        of(
          buildMembership({
            freeCancellationWindowHours: 4,
            expressUpgradesPerMonth: 2,
            expressUpgradesRemaining: 2,
          }),
        ),
      ),
      getPlans: jest.fn().mockReturnValue(of([])),
    };

    TestBed.configureTestingModule({
      providers: [
        OrderMembershipFacade,
        provideMockStore({ selectors: [{ selector: selectMarketCountryId, value: 'cze-id' }] }),
        { provide: PLATFORM_ID, useValue: platform },
        { provide: CustomerClient, useValue: { membershipClient } },
      ],
    });

    store = TestBed.inject(MockStore);
    facade = TestBed.inject(OrderMembershipFacade);
  }

  describe('load', () => {
    beforeEach(() => build('browser'));

    it('starts in the say-nothing state before anything is loaded', () => {
      expect(facade.loading()).toBe(false);
      expect(facade.loadFailed()).toBe(false);
      expect(facade.expressWaiverStatus()).toBe('none');
      expect(facade.expressUpgradesRemaining()).toBe(0);
    });

    it('skips the call for an anonymous customer', () => {
      facade.load(false);

      expect(membershipClient.getMine).not.toHaveBeenCalled();
      expect(facade.expressWaiverStatus()).toBe('none');
    });

    it('reads the membership once and exposes the server counts', () => {
      facade.load(true);

      expect(membershipClient.getMine).toHaveBeenCalledTimes(1);
      expect(facade.loading()).toBe(false);
      expect(facade.loadFailed()).toBe(false);
      expect(facade.expressUpgradesRemaining()).toBe(2);
      expect(facade.expressWaiverStatus()).toBe('available');
      expect(facade.expressWaiverAvailable()).toBe(true);
      expect(facade.expressWaiverExhausted()).toBe(false);
    });

    it('reports an exhausted member so the wizard can disclose the surcharge', () => {
      membershipClient.getMine.mockReturnValue(
        of(buildMembership({ expressUpgradesPerMonth: 2, expressUpgradesRemaining: 0 })),
      );

      facade.load(true);

      expect(facade.expressWaiverExhausted()).toBe(true);
      expect(facade.expressUpgradesRemaining()).toBe(0);
    });

    it('counts the waivers of a member inside the free trial', () => {
      membershipClient.getMine.mockReturnValue(
        of(
          buildMembership({
            expressUpgradesPerMonth: 2,
            expressUpgradesRemaining: 1,
            trialEndsAtUtc: new Date(Date.now() + 7 * 24 * 60 * 60 * 1000),
          }),
        ),
      );

      facade.load(true);

      expect(facade.expressWaiverAvailable()).toBe(true);
      expect(facade.expressUpgradesRemaining()).toBe(1);
    });

    it('says nothing about express for a member on a plan without the perk', () => {
      membershipClient.getMine.mockReturnValue(
        of(buildMembership({ freeCancellationWindowHours: 4, expressUpgradesPerMonth: 0 })),
      );

      facade.load(true);

      expect(facade.expressWaiverStatus()).toBe('none');
    });

    it('leaves a non-member with no express claim', () => {
      membershipClient.getMine.mockReturnValue(
        of(buildMembership({ hasMembership: false })),
      );

      facade.load(true);

      expect(facade.expressWaiverStatus()).toBe('none');
      expect(facade.loadFailed()).toBe(false);
    });

    it('degrades to the say-nothing state on a failed read', () => {
      membershipClient.getMine.mockReturnValue(throwError(() => new Error('boom')));

      facade.load(true);

      expect(facade.loadFailed()).toBe(true);
      expect(facade.loading()).toBe(false);
      expect(facade.expressWaiverStatus()).toBe('none');
    });
  });

  describe('SSR', () => {
    beforeEach(() => build('server'));

    it('does not fetch during the server render', () => {
      facade.load(true);

      expect(membershipClient.getMine).not.toHaveBeenCalled();
      expect(facade.expressWaiverStatus()).toBe('none');
    });
  });

  // One trial per account — the server's `trialEligible` — and each plan carries its own length.
  describe('the free trial the Plus step offers', () => {
    const plus = (trialPeriodDays: number) =>
      GetMembershipPlansResponse.fromJS({ code: 'PLUS_MONTHLY', trialPeriodDays });

    beforeEach(() => {
      build('browser');
      membershipClient.getPlans.mockReturnValue(of([plus(14)]));
      facade.loadPlans();
    });

    it("is the plan's own trial for a customer the server has not answered for", () => {
      expect(facade.trialDays()).toBe(14);
      expect(facade.trialDaysOf(plus(30))).toBe(30);
    });

    it('is none once the customer has had their trial', () => {
      membershipClient.getMine.mockReturnValue(
        of(GetMyMembershipResponse.fromJS({ hasMembership: false, trialEligible: false })),
      );

      facade.load(true);

      expect(facade.trialDays()).toBe(0);
      expect(facade.trialDaysOf(plus(30))).toBe(0);
    });
  });

  /**
   * THE GENERATED CLIENT CAN ANSWER WITH NULL. Its declared type is
   * `GetMembershipPlansResponse[]`, but `processGetPlans` falls to `result200 = null as any` for any
   * 200 whose body is not a JSON array — an empty body, a `{}`, a 204. Null is not an error, so
   * `catchError` never fires, and the declared type means TypeScript never complains either. Five
   * readers then index or measure this signal.
   */
  describe('plans', () => {
    beforeEach(() => build('browser'));

    it('holds an empty list when the client answers null instead of an array', () => {
      membershipClient.getPlans.mockReturnValue(of(null));

      facade.loadPlans();

      expect(facade.plans()).toEqual([]);
      expect(facade.plusUnavailable()).toBe(false);
    });

    // The Plus offer inside a booking follows the CHOSEN market, not the address (ADR-0058 D4).
    it('reads the plans for the chosen market and again when it changes', () => {
      const plan = new GetMembershipPlansResponse();
      plan.code = 'PLUS_MONTHLY';
      membershipClient.getPlans.mockReturnValue(of([plan]));

      facade.loadPlans();
      facade.loadPlans();

      expect(membershipClient.getPlans).toHaveBeenCalledTimes(1);
      expect(membershipClient.getPlans).toHaveBeenCalledWith('cze-id');
      expect(facade.plans()).toEqual([plan]);

      store.overrideSelector(selectMarketCountryId, 'svk-id');
      store.refreshState();

      expect(membershipClient.getPlans).toHaveBeenLastCalledWith('svk-id');
    });

    it('reports Plus unavailable when the market lists no plan, and not when the read failed', () => {
      membershipClient.getPlans.mockReturnValue(of([]));
      facade.loadPlans();
      expect(facade.plusUnavailable()).toBe(true);

      membershipClient.getPlans.mockReturnValue(throwError(() => new Error('offline')));
      store.overrideSelector(selectMarketCountryId, 'svk-id');
      store.refreshState();
      expect(facade.plans()).toEqual([]);
      expect(facade.plusUnavailable()).toBe(false);
    });

    it('reads nothing during a server render', () => {
      TestBed.resetTestingModule();
      build('server');

      facade.loadPlans();

      expect(membershipClient.getPlans).not.toHaveBeenCalled();
    });
  });

});
