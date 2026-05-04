import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { CleansiaButtonComponent } from '@cleansia/components';
import {
  GetLoyaltyActivityActivityItem,
  LoyaltyEarnSource,
  LoyaltyTransactionType,
} from '@cleansia/customer-services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { PaginatorModule, PaginatorState } from 'primeng/paginator';
import { SkeletonModule } from 'primeng/skeleton';
import { RewardsFacade } from './rewards.facade';

@Component({
  selector: 'cleansia-customer-rewards-activity',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    TranslatePipe,
    PaginatorModule,
    SkeletonModule,
    CleansiaButtonComponent,
  ],
  templateUrl: './rewards-activity.component.html',
})
export class RewardsActivityComponent implements OnInit {
  protected readonly facade = inject(RewardsFacade);
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);

  protected readonly LoyaltyTransactionType = LoyaltyTransactionType;
  protected readonly LoyaltyEarnSource = LoyaltyEarnSource;

  rows = 20;
  first = 0;

  ngOnInit(): void {
    // Always re-fetch on entry — counts and ordering may have shifted since
    // the user last looked. Cheap query, not worth caching.
    this.facade.loadActivityPage(0, this.rows, false);
  }

  onPageChange(event: PaginatorState): void {
    this.first = event.first ?? 0;
    this.rows = event.rows ?? 20;
    this.facade.loadActivityPage(this.first, this.rows, false);
  }

  back(): void {
    this.router.navigate(['/rewards']);
  }

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

  signedPoints(value: number): string {
    if (value > 0) return `+${value}`;
    return `${value}`;
  }
}
