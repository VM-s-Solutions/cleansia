import { CommonModule, isPlatformBrowser } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, OnInit, PLATFORM_ID } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CleansiaButtonComponent } from '@cleansia/components';
import {
  GetLoyaltyActivityActivityItem,
  GetLoyaltyTiersTierInfo,
  LoyaltyEarnSource,
  LoyaltyTier,
  LoyaltyTransactionType,
} from '@cleansia/customer-services';
import { CleansiaCustomerRoute, SnackbarService } from '@cleansia/services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ButtonModule } from 'primeng/button';
import { ProgressBarModule } from 'primeng/progressbar';
import { SkeletonModule } from 'primeng/skeleton';
import { TagModule } from 'primeng/tag';
import { RewardsFacade } from './rewards.facade';

type TierStatus = 'unlocked' | 'current' | 'locked';

@Component({
  selector: 'cleansia-customer-rewards',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    RouterLink,
    TranslatePipe,
    TagModule,
    ProgressBarModule,
    SkeletonModule,
    ButtonModule,
    CleansiaButtonComponent,
  ],
  templateUrl: './rewards.component.html',
})
export class RewardsComponent implements OnInit {
  protected readonly facade = inject(RewardsFacade);
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);
  private readonly snackbar = inject(SnackbarService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  // Re-export enums so the template can reference them.
  protected readonly LoyaltyTransactionType = LoyaltyTransactionType;
  protected readonly LoyaltyEarnSource = LoyaltyEarnSource;
  protected readonly LoyaltyTier = LoyaltyTier;

  /**
   * Progress 0..100 to the next tier. Computed against the current tier's
   * threshold so the bar starts empty after each tier-up rather than always
   * filling proportionally to lifetime points.
   */
  readonly progressPercent = computed<number>(() => {
    const acc = this.facade.account();
    if (!acc || acc.pointsToNextTier == null) return 100;
    const currentThreshold = this.thresholdFor(acc.currentTier);
    const nextThreshold = this.thresholdFor(acc.nextTier);
    const span = Math.max(1, nextThreshold - currentThreshold);
    const earnedInBand = acc.lifetimePoints - currentThreshold;
    return Math.min(100, Math.max(0, (earnedInBand / span) * 100));
  });

  /**
   * Pre-resolved progress-bar caption so the template doesn't have to nest a
   * `translate` pipe inside an object literal (Angular's parser handles it
   * but it's noisy). Returns "" when there's no next tier.
   */
  readonly progressLabel = computed<string>(() => {
    const acc = this.facade.account();
    if (!acc || acc.pointsToNextTier == null) return '';
    const tierLabel = this.translate.instant('pages.rewards.tier.' + this.facade.tierKey(acc.nextTier));
    return this.translate.instant('pages.rewards.progress_to_next', {
      current: acc.lifetimePoints,
      target: acc.lifetimePoints + acc.pointsToNextTier,
      tier: tierLabel,
    });
  });

  ngOnInit(): void {
    if (!this.facade.hasLoaded()) {
      this.facade.loadAll();
    }
  }

  retry(): void {
    this.facade.loadAll();
  }

  goToActivity(): void {
    this.router.navigate(['/rewards/activity']);
  }

  /** Resolve the configured threshold for a tier from the loaded ladder. */
  private thresholdFor(tier: LoyaltyTier | undefined | null): number {
    if (tier == null) return 0;
    const info = this.facade.tiers().find((t) => t.tier === tier);
    return info?.lifetimePointsThreshold ?? 0;
  }

  tierStatus(tier: LoyaltyTier): TierStatus {
    const acc = this.facade.account();
    if (!acc) return 'locked';
    if (acc.currentTier === tier) return 'current';
    return acc.lifetimePoints >= this.thresholdFor(tier) ? 'unlocked' : 'locked';
  }

  tierStatusKey(tier: LoyaltyTier): string {
    return `pages.rewards.tier_status_${this.tierStatus(tier)}`;
  }

  tierStatusSeverity(tier: LoyaltyTier): 'success' | 'info' | 'secondary' {
    const status = this.tierStatus(tier);
    if (status === 'current') return 'info';
    if (status === 'unlocked') return 'success';
    return 'secondary';
  }

  /**
   * Pick a translation key + interpolation for a tier's discount line so
   * the template stays free of branching. Keys come from `pages.rewards.*`.
   */
  discountLabel(tier: GetLoyaltyTiersTierInfo): { key: string; params: Record<string, unknown> } {
    if (!tier.discountPercent) {
      return { key: 'pages.rewards.no_discount_yet', params: {} };
    }
    if (tier.minimumOrderAmountForDiscount && tier.minimumOrderAmountForDiscount > 0) {
      return {
        key: 'pages.rewards.discount_min_order',
        params: {
          percent: this.percentDisplay(tier.discountPercent),
          minAmount: tier.minimumOrderAmountForDiscount,
        },
      };
    }
    return {
      key: 'pages.rewards.discount_basic',
      params: { percent: this.percentDisplay(tier.discountPercent) },
    };
  }

  /**
   * Backend may emit either a fraction (0.05) or an integer percent (5).
   * Normalise to an integer for display so we don't show "0.05% off".
   */
  private percentDisplay(raw: number): number {
    return raw <= 1 ? Math.round(raw * 100) : Math.round(raw);
  }

  /**
   * Pick the right transaction-row translation key + params from the
   * ledger entry so templates stay declarative.
   */
  txLabel(item: GetLoyaltyActivityActivityItem): { key: string; params: Record<string, unknown> } {
    const points = item.points;
    const number = item.orderDisplayNumber ?? '';
    if (item.source === LoyaltyEarnSource.Referral) {
      return { key: 'pages.rewards.tx_referral', params: { points } };
    }
    if (item.source === LoyaltyEarnSource.ManualGrant) {
      return { key: 'pages.rewards.tx_manual', params: { points } };
    }
    if (item.type === LoyaltyTransactionType.Revoke || item.source === LoyaltyEarnSource.OrderCancelled) {
      return { key: 'pages.rewards.tx_revoke_order', params: { points, number } };
    }
    return { key: 'pages.rewards.tx_earn_order', params: { points, number } };
  }

  /**
   * Format the ledger timestamp using the active language. Falls back to en-US
   * when the runtime locale isn't in our explicit map.
   */
  formatDate(date: Date | undefined | null): string {
    if (!date) return '';
    const localeMap: Record<string, string> = {
      en: 'en-US',
      cs: 'cs-CZ',
      sk: 'sk-SK',
      uk: 'uk-UA',
      ru: 'ru-RU',
    };
    const locale = localeMap[this.translate.currentLang] || 'en-US';
    return new Date(date).toLocaleString(locale, {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  }

  /** "+N" or just "N" (already negative on Revoke). */
  signedPoints(value: number): string {
    if (value > 0) return `+${value}`;
    return `${value}`;
  }

  // ─── Referral helpers ───────────────────────────────────────
  //
  // The "Invite friends" card delegates copy + share to these handlers. We
  // wrap navigator.* calls with a browser guard so SSR doesn't blow up; share
  // also degrades to a clipboard copy when the Web Share API is unavailable
  // (most desktop browsers).

  /** Build the public landing-page URL the customer pastes into a chat. */
  private referralUrl(code: string): string {
    const path = `/${CleansiaCustomerRoute.REFERRAL_LANDING}/${code}`;
    if (!this.isBrowser) return path;
    return `${window.location.origin}${path}`;
  }

  copyReferralCode(code: string): void {
    if (!this.isBrowser || !navigator.clipboard) return;
    void navigator.clipboard.writeText(code).then(() => {
      this.snackbar.showSuccessTranslated('pages.rewards.referral.copied_toast');
    });
  }

  shareReferralCode(code: string): void {
    if (!this.isBrowser) return;
    const url = this.referralUrl(code);
    const text = this.translate.instant('pages.rewards.referral.share_text', { code, url });
    const nav = navigator as Navigator & { share?: (data: ShareData) => Promise<void> };

    if (typeof nav.share === 'function') {
      void nav.share({ text, url }).catch(() => {
        // User-cancel and no-permission both land here. Fall back to clipboard
        // so the share intent isn't a dead end.
        this.fallbackCopyShareText(code);
      });
      return;
    }

    this.fallbackCopyShareText(code);
  }

  private fallbackCopyShareText(code: string): void {
    if (!navigator.clipboard) return;
    void navigator.clipboard.writeText(code).then(() => {
      this.snackbar.showSuccessTranslated('pages.rewards.referral.share_failed');
    });
  }

  /**
   * Pick the right stats translation key based on (acceptedCount, qualifiedCount).
   * Buckets:
   *   - 0 accepted        → empty CTA
   *   - >=1, none qualified → waiting on first booking
   *   - >=1, some qualified → mixed-status line with both counts
   */
  readonly referralStatsLabel = computed<string>(() => {
    const ref = this.facade.referralAccount();
    if (!ref) return 'pages.rewards.referral.stats_empty';
    if (ref.acceptedCount === 0) return 'pages.rewards.referral.stats_empty';
    if (ref.qualifiedCount === 0) return 'pages.rewards.referral.stats_waiting';
    return 'pages.rewards.referral.stats_qualified';
  });

  readonly referralStatsParams = computed<Record<string, unknown>>(() => {
    const ref = this.facade.referralAccount();
    if (!ref) return {};
    return { count: ref.acceptedCount, qualified: ref.qualifiedCount };
  });
}
