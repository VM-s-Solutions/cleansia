import { TestBed } from '@angular/core/testing';
import {
  CustomerClient,
  GetLoyaltyActivityActivityItem,
  GetLoyaltyTiersResponse,
  GetMyLoyaltyResponse,
  GetMyReferralResponse,
  LoyaltyTier,
} from '@cleansia/customer-services';
import { of, throwError } from 'rxjs';
import { RewardsFacade } from './rewards.facade';

describe('RewardsFacade', () => {
  let facade: RewardsFacade;
  let loyaltyClient: {
    getMy: jest.Mock;
    getTiers: jest.Mock;
    getActivity: jest.Mock;
  };
  let referralClient: {
    getMy: jest.Mock;
  };

  const account = GetMyLoyaltyResponse.fromJS({ tier: { value: 2 }, points: 100 });
  const tiers = GetLoyaltyTiersResponse.fromJS({
    tiers: [{ tier: { value: 1 } }, { tier: { value: 2 } }],
  });
  const activity = {
    data: [
      GetLoyaltyActivityActivityItem.fromJS({ points: 10 }),
      GetLoyaltyActivityActivityItem.fromJS({ points: 20 }),
    ],
    total: 2,
  };
  const referral = GetMyReferralResponse.fromJS({ code: 'ABC', acceptedCount: 3 });

  beforeEach(() => {
    loyaltyClient = {
      getMy: jest.fn().mockReturnValue(of(account)),
      getTiers: jest.fn().mockReturnValue(of(tiers)),
      getActivity: jest.fn().mockReturnValue(of(activity)),
    };
    referralClient = {
      getMy: jest.fn().mockReturnValue(of(referral)),
    };

    TestBed.configureTestingModule({
      providers: [
        RewardsFacade,
        { provide: CustomerClient, useValue: { loyaltyClient, referralClient } },
      ],
    });

    facade = TestBed.inject(RewardsFacade);
  });

  describe('loadAll — the three data states', () => {
    it('starts empty, not loading, no error', () => {
      expect(facade.account()).toBeNull();
      expect(facade.tiers()).toEqual([]);
      expect(facade.loading()).toBe(false);
      expect(facade.error()).toBeNull();
    });

    it('populates account, tiers and recent activity then clears loading', () => {
      facade.loadAll();

      expect(facade.account()).toBe(account);
      expect(facade.tiers().length).toBe(2);
      expect(facade.recentActivity().length).toBe(2);
      expect(facade.loading()).toBe(false);
      expect(facade.error()).toBeNull();
      expect(facade.hasLoaded()).toBe(true);
    });

    it('sets the error flag and clears loading when the snapshot call fails', () => {
      loyaltyClient.getMy.mockReturnValue(throwError(() => new Error('boom')));

      facade.loadAll();

      expect(facade.error()).toBe('load');
      expect(facade.loading()).toBe(false);
    });

    it('also pulls the referral snapshot in parallel', () => {
      facade.loadAll();

      expect(referralClient.getMy).toHaveBeenCalledTimes(1);
      expect(facade.referralAccount()).toBe(referral);
      expect(facade.referralAccountLoading()).toBe(false);
    });
  });

  describe('loadReferral', () => {
    it('caches the referral account on success', () => {
      facade.loadReferral();

      expect(facade.referralAccount()).toBe(referral);
      expect(facade.referralAccountLoading()).toBe(false);
    });

    it('resolves to null without surfacing an error when the call fails', () => {
      referralClient.getMy.mockReturnValue(throwError(() => new Error('boom')));

      facade.loadReferral();

      expect(facade.referralAccount()).toBeNull();
      expect(facade.referralAccountLoading()).toBe(false);
    });
  });

  describe('loadActivityPage', () => {
    it('replaces the list and sets totals when not appending', () => {
      facade.loadActivityPage(0, 10, false);

      expect(loyaltyClient.getActivity).toHaveBeenCalledWith(0, 10);
      expect(facade.activityList().length).toBe(2);
      expect(facade.totalActivity()).toBe(2);
      expect(facade.activityLoading()).toBe(false);
    });

    it('appends to the existing list when appending', () => {
      facade.loadActivityPage(0, 10, false);
      facade.loadActivityPage(10, 10, true);

      expect(facade.activityList().length).toBe(4);
      expect(facade.loadingMore()).toBe(false);
    });

    it('sets the error flag and clears the loaders on failure', () => {
      loyaltyClient.getActivity.mockReturnValue(throwError(() => new Error('boom')));

      facade.loadActivityPage(0, 10, false);

      expect(facade.error()).toBe('load');
      expect(facade.activityLoading()).toBe(false);
      expect(facade.loadingMore()).toBe(false);
    });
  });

  describe('tier mapping helpers', () => {
    it('maps each tier to its i18n suffix', () => {
      expect(facade.tierKey(LoyaltyTier.PlatinumSparkler)).toBe('platinum_sparkler');
      expect(facade.tierKey(LoyaltyTier.GoldPolisher)).toBe('gold_polisher');
      expect(facade.tierKey(LoyaltyTier.SilverMopper)).toBe('silver_mopper');
      expect(facade.tierKey(LoyaltyTier.BronzeCleaner)).toBe('bronze_cleaner');
      expect(facade.tierKey(null)).toBe('bronze_cleaner');
    });

    it('maps each tier to its SCSS accent', () => {
      expect(facade.tierAccent(LoyaltyTier.PlatinumSparkler)).toBe('platinum');
      expect(facade.tierAccent(LoyaltyTier.GoldPolisher)).toBe('gold');
      expect(facade.tierAccent(LoyaltyTier.SilverMopper)).toBe('silver');
      expect(facade.tierAccent(LoyaltyTier.BronzeCleaner)).toBe('bronze');
      expect(facade.tierAccent(undefined)).toBe('bronze');
    });
  });
});
