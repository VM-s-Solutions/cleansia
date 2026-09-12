import { TestBed } from '@angular/core/testing';
import {
  CustomerClient,
  GetMyMembershipResponse,
  SwapMembershipPlanCommand,
} from '@cleansia/customer-services';
import {
  loadCustomerCurrencies,
  selectCustomerDefaultCurrencyCode,
} from '@cleansia/customer-stores';
import { SnackbarService } from '@cleansia/services';
import { MockStore, provideMockStore } from '@ngrx/store/testing';
import { of, throwError } from 'rxjs';
import { MembershipFacade } from './membership.facade';

function buildMembership(fields: {
  hasMembership?: boolean;
  expressUpgradesPerMonth?: number;
  expressUpgradesRemaining?: number;
  trialEndsAtUtc?: Date;
}): GetMyMembershipResponse {
  const response = new GetMyMembershipResponse();
  response.hasMembership = fields.hasMembership ?? true;
  response.expressUpgradesPerMonth = fields.expressUpgradesPerMonth;
  response.expressUpgradesRemaining = fields.expressUpgradesRemaining;
  response.trialEndsAtUtc = fields.trialEndsAtUtc;
  return response;
}

describe('MembershipFacade — express waiver state', () => {
  let facade: MembershipFacade;
  let store: MockStore;
  let membershipClient: {
    getMine: jest.Mock;
    swapPlan: jest.Mock;
    getPlans: jest.Mock;
  };
  let snackbar: {
    showApiError: jest.Mock;
    showSuccessTranslated: jest.Mock;
    showErrorTranslated: jest.Mock;
  };

  beforeEach(() => {
    membershipClient = {
      getMine: jest.fn(),
      swapPlan: jest.fn(),
      getPlans: jest.fn().mockReturnValue(of([])),
    };
    snackbar = {
      showApiError: jest.fn(),
      showSuccessTranslated: jest.fn(),
      showErrorTranslated: jest.fn(),
    };

    TestBed.configureTestingModule({
      providers: [
        MembershipFacade,
        provideMockStore({
          selectors: [{ selector: selectCustomerDefaultCurrencyCode, value: null }],
        }),
        { provide: CustomerClient, useValue: { membershipClient } },
        { provide: SnackbarService, useValue: snackbar },
      ],
    });

    store = TestBed.inject(MockStore);
    facade = TestBed.inject(MembershipFacade);
  });

  // Neither `GetMyMembershipResponse` nor `GetMembershipPlansResponse` carries a currency —
  // `monthlyPriceCzk` is the wire name, not a label — so the amounts are labelled with the
  // platform default, read from the store rather than assumed.
  describe('the currency a plan is priced in', () => {
    it('asks the store for the platform currencies when the plans are loaded', () => {
      jest.spyOn(store, 'dispatch');

      facade.loadPlans();

      expect(store.dispatch).toHaveBeenCalledWith(loadCustomerCurrencies());
    });

    it('re-exposes the platform default', () => {
      store.overrideSelector(selectCustomerDefaultCurrencyCode, 'EUR');
      store.refreshState();

      expect(facade.defaultCurrencyCode()).toBe('EUR');
    });
  });

  it('advertises nothing before the membership is loaded', () => {
    expect(facade.expressWaiverAdvertised()).toBe(false);
    expect(facade.expressUpgradesRemaining()).toBe(0);
  });

  it('advertises the remaining count for a paid member with waivers left', () => {
    membershipClient.getMine.mockReturnValue(
      of(buildMembership({ expressUpgradesPerMonth: 2, expressUpgradesRemaining: 2 })),
    );

    facade.refresh();

    expect(facade.expressWaiverAdvertised()).toBe(true);
    expect(facade.expressWaiverAvailable()).toBe(true);
    expect(facade.expressUpgradesRemaining()).toBe(2);
    expect(facade.loading()).toBe(false);
  });

  it('advertises the perk as exhausted rather than absent when the quota is used up', () => {
    membershipClient.getMine.mockReturnValue(
      of(buildMembership({ expressUpgradesPerMonth: 2, expressUpgradesRemaining: 0 })),
    );

    facade.refresh();

    expect(facade.expressWaiverAdvertised()).toBe(true);
    expect(facade.expressWaiverExhausted()).toBe(true);
    expect(facade.expressWaiverAvailable()).toBe(false);
  });

  it('advertises the perk as pending — not exhausted — during the trial', () => {
    membershipClient.getMine.mockReturnValue(
      of(
        buildMembership({
          expressUpgradesPerMonth: 2,
          expressUpgradesRemaining: 0,
          trialEndsAtUtc: new Date(Date.now() + 7 * 24 * 60 * 60 * 1000),
        }),
      ),
    );

    facade.refresh();

    expect(facade.expressWaiverPendingTrial()).toBe(true);
    expect(facade.expressWaiverExhausted()).toBe(false);
  });

  it('advertises nothing on a plan that carries no express quota', () => {
    membershipClient.getMine.mockReturnValue(
      of(buildMembership({ expressUpgradesPerMonth: 0 })),
    );

    facade.refresh();

    expect(facade.expressWaiverAdvertised()).toBe(false);
  });

  it('advertises nothing for a customer with no active membership', () => {
    membershipClient.getMine.mockReturnValue(
      of(buildMembership({ hasMembership: false })),
    );

    facade.refresh();

    expect(facade.expressWaiverAdvertised()).toBe(false);
  });

  it('advertises nothing and surfaces the error when the read fails', () => {
    membershipClient.getMine.mockReturnValue(throwError(() => new Error('boom')));

    facade.refresh();

    expect(facade.expressWaiverAdvertised()).toBe(false);
    expect(facade.loading()).toBe(false);
    expect(snackbar.showApiError).toHaveBeenCalledTimes(1);
  });

  // Every member of a generated command is optional, so a dropped assignment type-checks.
  // These pin the serialized body instead (ADR-0031).
  describe('command bodies on the wire', () => {
    it('serializes a plan swap with the target plan code', () => {
      membershipClient.swapPlan.mockReturnValue(of(undefined));
      membershipClient.getMine.mockReturnValue(of(buildMembership({})));

      facade.swapPlan('plus-yearly');

      const command: SwapMembershipPlanCommand =
        membershipClient.swapPlan.mock.calls[0][0];
      expect(command).toBeInstanceOf(SwapMembershipPlanCommand);
      expect(command.toJSON()).toEqual({ newPlanCode: 'plus-yearly' });
    });
  });

  /**
   * THE GENERATED CLIENT CAN ANSWER WITH NULL. Its declared type is
   * `GetMembershipPlansResponse[]`, but `processGetPlans` falls to `result200 = null as any` for any
   * 200 whose body is not a JSON array — an empty body, a `{}`, a 204. Null is not an error, so
   * `catchError` never fires, and the declared type means TypeScript never complains either. Five
   * readers then index or measure this signal.
   */
  it('holds an empty list, and hands the callback one, when the client answers null', () => {
    membershipClient.getPlans.mockReturnValue(of(null));
    const onLoaded = jest.fn();

    facade.loadPlans(onLoaded);

    expect(facade.plans()).toEqual([]);
    // The callback's argument is indexed by its callers just as the signal is, so both halves are
    // pinned — which is why the facade coalesces once and passes the same list to both.
    expect(onLoaded).toHaveBeenCalledWith([]);
  });

});
