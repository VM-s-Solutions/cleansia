import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { CleansiaButtonComponent } from '@cleansia/components';
import {
  GetLoyaltyActivityActivityItem,
  LoyaltyEarnSource,
  LoyaltyTransactionType,
} from '@cleansia/customer-services';
import { FoamEdgeComponent } from '@cleansia-customer/home';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { SkeletonModule } from 'primeng/skeleton';
import { RewardsFacade } from './rewards.facade';

@Component({
  selector: 'cleansia-customer-rewards-activity',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    TranslatePipe,
    SkeletonModule,
    CleansiaButtonComponent,
    FoamEdgeComponent,
  ],
  templateUrl: './rewards-activity.component.html',
})
export class RewardsActivityComponent implements OnInit {
  protected readonly facade = inject(RewardsFacade);
  private readonly translate = inject(TranslateService);

  protected readonly LoyaltyTransactionType = LoyaltyTransactionType;
  protected readonly LoyaltyEarnSource = LoyaltyEarnSource;

  /**
   * Earned or deducted. Filtered in the page rather than on the server: the
   * whole point of the sign is to be read against its neighbours, and the
   * endpoint takes no type filter — asking for one would be inventing an API.
   */
  readonly filters = ['all', 'earned', 'deducted'] as const;
  readonly activeFilter = signal<(typeof this.filters)[number]>('all');

  rows = 20;

  readonly visibleActivity = computed(() => {
    const items = this.facade.activityList();
    switch (this.activeFilter()) {
      case 'earned':
        return items.filter((item) => item.points > 0);
      case 'deducted':
        return items.filter((item) => item.points < 0);
      default:
        return items;
    }
  });

  /**
   * By year, as the board draws it. A points account outlives a calendar, and a
   * bare run of dates stops telling you which December you are looking at. The
   * server returns newest first, so the groups come out in that order too.
   */
  readonly groupedActivity = computed(() => {
    const groups: { year: number; items: GetLoyaltyActivityActivityItem[] }[] = [];
    for (const item of this.visibleActivity()) {
      const year = new Date(item.occurredOn).getFullYear();
      const last = groups[groups.length - 1];
      if (last?.year === year) {
        last.items.push(item);
      } else {
        groups.push({ year, items: [item] });
      }
    }
    return groups;
  });

  ngOnInit(): void {
    // Always re-fetch on entry — counts and ordering may have shifted since
    // the user last looked. Cheap query, not worth caching.
    this.facade.loadActivityPage(0, this.rows, false);
    this.facade.loadAll();
  }

  selectFilter(filter: (typeof this.filters)[number]): void {
    this.activeFilter.set(filter);
  }

  /**
   * Older movements are APPENDED, not paged to. A ledger is read downwards, and
   * a paginator makes the reader hold which page they were on to answer "when
   * did that happen".
   */
  loadOlder(): void {
    this.facade.loadActivityPage(this.facade.activityList().length, this.rows, true);
  }

  /**
   * What the movement WAS, and nothing else.
   *
   * These keys used to carry the points and the order number into the sentence
   * — "+170 pts — Cleaning #CL-2026-0301" — beside a pill already showing +170
   * and a line already showing the order. Three copies of two facts. The row
   * says each thing once now: the pill is the amount, this is the event, the
   * line under it is the order.
   */
  txLabel(item: GetLoyaltyActivityActivityItem): { key: string; params: Record<string, unknown> } {
    if (item.source === LoyaltyEarnSource.Referral) {
      return { key: 'pages.rewards.tx.referral', params: {} };
    }
    if (item.source === LoyaltyEarnSource.ManualGrant) {
      return { key: 'pages.rewards.tx.manual', params: {} };
    }
    if (
      item.type === LoyaltyTransactionType.Revoke ||
      item.source === LoyaltyEarnSource.OrderCancelled
    ) {
      return { key: 'pages.rewards.tx.cancelled', params: {} };
    }
    if (item.source === LoyaltyEarnSource.OrderPartiallyRefunded) {
      return { key: 'pages.rewards.tx.refunded', params: {} };
    }
    return { key: 'pages.rewards.tx.completed', params: {} };
  }

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
    // The year is the group heading, so the row does not repeat it.
    return new Date(date).toLocaleDateString(locale, {
      day: 'numeric',
      month: 'numeric',
    });
  }

  signedPoints(value: number): string {
    if (value > 0) return `+${value}`;
    return `${value}`;
  }
}
