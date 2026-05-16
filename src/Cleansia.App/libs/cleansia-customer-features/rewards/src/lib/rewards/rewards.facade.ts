import { computed, inject, Injectable, signal } from '@angular/core';
import {
  CustomerClient,
  GetLoyaltyActivityActivityItem,
  GetLoyaltyTiersTierInfo,
  GetMyLoyaltyResponse,
  GetMyReferralResponse,
  LoyaltyTier,
} from '@cleansia/customer-services';
import { catchError, forkJoin, of } from 'rxjs';

/**
 * Rewards facade — single source of truth for the customer Rewards page.
 *
 * Owns the user's loyalty snapshot (`account`), the tier ladder (`tiers`),
 * and two slices of activity:
 *   - `recentActivity` — last 5 entries shown on the main Rewards page
 *   - `activityList`   — full paged list shown on /rewards/activity
 *
 * `loadAll()` is cheap to call repeatedly: the cached signals make second
 * visits feel instant while a fresh request runs in the background.
 */
@Injectable({ providedIn: 'root' })
export class RewardsFacade {
  private readonly customerClient = inject(CustomerClient);

  readonly account = signal<GetMyLoyaltyResponse | null>(null);
  readonly tiers = signal<GetLoyaltyTiersTierInfo[]>([]);
  readonly recentActivity = signal<GetLoyaltyActivityActivityItem[]>([]);
  readonly activityList = signal<GetLoyaltyActivityActivityItem[]>([]);
  readonly totalActivity = signal(0);

  // Referral snapshot — owner's lifetime code + qualified/accepted counters.
  // Loaded alongside `loadAll()`; failures are non-fatal so a missing referral
  // endpoint never breaks the rewards page itself.
  readonly referralAccount = signal<GetMyReferralResponse | null>(null);
  readonly referralAccountLoading = signal(false);

  readonly loading = signal(false);
  readonly loadingMore = signal(false);
  readonly activityLoading = signal(false);
  readonly error = signal<string | null>(null);

  readonly hasLoaded = computed(() => this.account() !== null);

  /**
   * Pull the loyalty snapshot, all tier configs, and the most recent 5
   * activity entries in parallel. Used by both the Profile entry-card and
   * the Rewards page itself, so it must be safe to call twice.
   */
  loadAll(): void {
    this.loading.set(true);
    this.error.set(null);

    forkJoin({
      account: this.customerClient.loyaltyClient.getMy(),
      tiers: this.customerClient.loyaltyClient.getTiers(),
      activity: this.customerClient.loyaltyClient.getActivity(0, 5),
    }).subscribe({
      next: ({ account, tiers, activity }) => {
        this.account.set(account);
        this.tiers.set(tiers.tiers ?? []);
        this.recentActivity.set(activity.data ?? []);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.error.set('load');
      },
    });

    // Pulled in parallel but tracked separately — referral fetch failures
    // shouldn't poison the main rewards screen. See loadReferral() for the
    // catchError-to-null fallback.
    this.loadReferral();
  }

  /**
   * Fetch the user's referral code + counters. Safe to call repeatedly; the
   * result is cached in the `referralAccount` signal until sign-out clears
   * the facade. Network failures resolve to `null` (no UI surface) instead
   * of bubbling to a snackbar — the "Invite friends" card just hides itself.
   */
  loadReferral(): void {
    this.referralAccountLoading.set(true);
    this.customerClient.referralClient
      .getMy()
      .pipe(catchError(() => of(null)))
      .subscribe((response) => {
        this.referralAccount.set(response);
        this.referralAccountLoading.set(false);
      });
  }

  /**
   * Paged activity loader. Used by RewardsActivityComponent. `append=true`
   * concatenates onto the existing list (infinite scroll / "load more"),
   * `append=false` replaces it (initial load or page change).
   */
  loadActivityPage(offset: number, limit: number, append = false): void {
    if (append) {
      this.loadingMore.set(true);
    } else {
      this.activityLoading.set(true);
    }

    this.customerClient.loyaltyClient.getActivity(offset, limit).subscribe({
      next: (paged) => {
        const items = paged.data ?? [];
        if (append) {
          this.activityList.set([...this.activityList(), ...items]);
        } else {
          this.activityList.set(items);
        }
        this.totalActivity.set(paged.total ?? 0);
        this.loadingMore.set(false);
        this.activityLoading.set(false);
      },
      error: () => {
        this.loadingMore.set(false);
        this.activityLoading.set(false);
        this.error.set('load');
      },
    });
  }

  /**
   * Map LoyaltyTier enum to a stable i18n suffix used under
   * `pages.rewards.tier.*`. Kept here so components/templates don't have to
   * each repeat the switch.
   */
  tierKey(tier: LoyaltyTier | undefined | null): string {
    switch (tier) {
      case LoyaltyTier.PlatinumSparkler:
        return 'platinum_sparkler';
      case LoyaltyTier.GoldPolisher:
        return 'gold_polisher';
      case LoyaltyTier.SilverMopper:
        return 'silver_mopper';
      case LoyaltyTier.BronzeCleaner:
      default:
        return 'bronze_cleaner';
    }
  }

  /**
   * CSS modifier suffix used by the rewards SCSS — a parallel of `tierKey`
   * but without the long name suffixes. Lets the template pick a colour
   * accent per tier (`rewards-hero--bronze`, `--silver`, etc).
   */
  tierAccent(tier: LoyaltyTier | undefined | null): 'bronze' | 'silver' | 'gold' | 'platinum' {
    switch (tier) {
      case LoyaltyTier.PlatinumSparkler:
        return 'platinum';
      case LoyaltyTier.GoldPolisher:
        return 'gold';
      case LoyaltyTier.SilverMopper:
        return 'silver';
      case LoyaltyTier.BronzeCleaner:
      default:
        return 'bronze';
    }
  }
}
