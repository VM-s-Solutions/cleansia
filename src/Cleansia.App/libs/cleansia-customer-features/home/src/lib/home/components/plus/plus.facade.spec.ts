import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import {
  CustomerAuthService,
  CustomerClient,
  GetMembershipPlansResponse,
  GetMyMembershipResponse,
} from '@cleansia/customer-services';
import { selectMarketCountryId } from '@cleansia/customer-stores';
import { MockStore, provideMockStore } from '@ngrx/store/testing';
import { of, throwError } from 'rxjs';
import { PlusFacade } from './plus.facade';

/** `BillingInterval.Monthly` / `.Yearly` as they arrive on the wire. */
const MONTHLY = 1;
const YEARLY = 2;

function plan(billingInterval: number, trialPeriodDays: number): GetMembershipPlansResponse {
  return GetMembershipPlansResponse.fromJS({
    code: billingInterval === MONTHLY ? 'PLUS_MONTHLY' : 'PLUS_YEARLY',
    billingInterval,
    trialPeriodDays,
    price: 199,
    currencyCode: 'CZK',
  });
}

function membership(hasMembership: boolean, trialEligible: boolean): GetMyMembershipResponse {
  return GetMyMembershipResponse.fromJS({ hasMembership, trialEligible });
}

describe('PlusFacade', () => {
  let facade: PlusFacade;
  let store: MockStore;
  let getPlans: jest.Mock;
  let getMine: jest.Mock;

  function build(signedIn: boolean): void {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        PlusFacade,
        provideMockStore({
          selectors: [{ selector: selectMarketCountryId, value: 'cze-id' }],
        }),
        { provide: CustomerAuthService, useValue: { isLoggedIn: signal(signedIn) } },
        { provide: CustomerClient, useValue: { membershipClient: { getPlans, getMine } } },
      ],
    });
    store = TestBed.inject(MockStore);
    facade = TestBed.inject(PlusFacade);
  }

  beforeEach(() => {
    getPlans = jest.fn().mockReturnValue(of([plan(MONTHLY, 14), plan(YEARLY, 30)]));
    getMine = jest.fn().mockReturnValue(of(null));
  });

  afterEach(() => TestBed.resetTestingModule());

  it('reads the plans for the chosen market and again when it changes', () => {
    build(false);

    facade.load();
    store.overrideSelector(selectMarketCountryId, 'svk-id');
    store.refreshState();

    expect(getPlans.mock.calls).toEqual([['cze-id'], ['svk-id']]);
  });

  // One trial per account — the server's `trialEligible` — so the plan's own trial column alone
  // would promise free days to a customer who already had theirs.
  describe('the free trial the band offers', () => {
    it("offers a signed-out visitor the monthly plan's trial, without asking who they are", () => {
      build(false);

      facade.load();

      expect(getMine).not.toHaveBeenCalled();
      expect(facade.trialDays()).toBe(14);
    });

    it('offers it to a signed-in customer who has never had one', () => {
      getMine.mockReturnValue(of(membership(false, true)));
      build(true);

      facade.load();

      expect(facade.trialDays()).toBe(14);
    });

    it('offers none to a customer who has had their trial', () => {
      getMine.mockReturnValue(of(membership(false, false)));
      build(true);

      facade.load();

      expect(facade.trialDays()).toBe(0);
    });

    it('offers none to a member', () => {
      getMine.mockReturnValue(of(membership(true, true)));
      build(true);

      facade.load();

      expect(facade.trialDays()).toBe(0);
    });

    it('answers a failed membership read as the Plus page does, like a visitor', () => {
      getMine.mockReturnValue(throwError(() => new Error('offline')));
      build(true);

      facade.load();

      expect(facade.trialDays()).toBe(14);
    });

    it('offers none on a plan without a trial', () => {
      getPlans.mockReturnValue(of([plan(MONTHLY, 0)]));
      build(false);

      facade.load();

      expect(facade.trialDays()).toBe(0);
    });
  });
});
