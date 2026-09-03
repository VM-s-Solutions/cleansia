import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
} from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CleansiaButtonComponent } from '@cleansia/components';
import { CleansiaCustomerRoute } from '@cleansia/services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { FoamEdgeComponent } from '@cleansia-customer/home';
import { GetMembershipPlansResponse, GetMyMembershipResponse } from '@cleansia/customer-services';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ConfirmationService } from 'primeng/api';
import { SkeletonModule } from 'primeng/skeleton';
import { MembershipFacade } from './membership.facade';

@Component({
  selector: 'cleansia-customer-membership-management',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    RouterLink,
    TranslatePipe,
    SkeletonModule,
    ConfirmDialogModule,
    CleansiaButtonComponent,
    FoamEdgeComponent,
  ],
  providers: [ConfirmationService, MembershipFacade],
  templateUrl: './membership-management.component.html',
})
export class MembershipManagementComponent implements OnInit {
  protected readonly facade = inject(MembershipFacade);
  private readonly translate = inject(TranslateService);
  private readonly confirmService = inject(ConfirmationService);
  private readonly router = inject(Router);

  // Re-expose facade signals so existing template bindings keep working.
  readonly loading = this.facade.loading;
  readonly cancelling = this.facade.cancelling;
  readonly switching = this.facade.switching;
  readonly membership = this.facade.membership;
  readonly plans = this.facade.plans;
  readonly expressUpgradesRemaining = this.facade.expressUpgradesRemaining;
  readonly expressWaiverAvailable = this.facade.expressWaiverAvailable;
  readonly expressWaiverExhausted = this.facade.expressWaiverExhausted;
  readonly expressWaiverPendingTrial = this.facade.expressWaiverPendingTrial;

  /** Yearly plan (if any) — drives the "Switch to annual" CTA visibility. */
  ngOnInit(): void {
    this.refresh();
    this.facade.loadPlans();
  }

  refresh(): void {
    this.facade.refresh();
  }

  /** Top-level CTA for non-subscribers — sends them to the marketing page. */
  goToSubscribe(): void {
    this.router.navigate([CleansiaCustomerRoute.MEMBERSHIP, 'subscribe']);
  }

  /** Cancel-at-period-end. The benefit window is unaffected until period end. */
  confirmCancel(): void {
    this.confirmService.confirm({
      message: this.translate.instant('pages.membership.cancel_dialog_message'),
      header: this.translate.instant('pages.membership.cancel_dialog_title'),
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: this.translate.instant('pages.membership.cancel_dialog_confirm'),
      rejectLabel: this.translate.instant('common.back'),
      accept: () => this.facade.cancel(),
    });
  }

  /**
   * Grouped, and in the reader's locale. It was `amount.toFixed(0) + ' Kč'`,
   * which prints 2030 where the board prints 2 030 — a four-figure price is
   * read wrong for a beat without the separator.
   */
  formatCzk(amount: number): string {
    return new Intl.NumberFormat(this.getLocale(), {
      style: 'currency',
      currency: 'CZK',
      maximumFractionDigits: amount % 1 === 0 ? 0 : 2,
    }).format(amount);
  }

  private getLocale(): string {
    const localeMap: Record<string, string> = {
      en: 'en-US',
      cs: 'cs-CZ',
      sk: 'sk-SK',
      uk: 'uk-UA',
      ru: 'ru-RU',
    };
    return localeMap[this.translate.currentLang] || 'en-US';
  }

  /**
   * Monthly or yearly, from the plan's billing interval in months. The board
   * names the cadence beside the plan, because "199 Kč" and "2 030 Kč" only
   * mean anything once you know which one they are per.
   */
  cadenceOf(plan: GetMembershipPlansResponse): string {
    return plan.billingInterval >= 12 ? 'yearly' : 'monthly';
  }

  cadenceKey(m: GetMyMembershipResponse): string | null {
    if (m.billingInterval == null) return null;
    return m.billingInterval >= 12
      ? 'pages.membership.cadence.yearly'
      : 'pages.membership.cadence.monthly';
  }

  /**
   * What the next charge will be. The membership response carries the monthly
   * figure and the interval; the plan list carries the actual charge, so it is
   * read from there when the codes match and falls back to the monthly one.
   */
  currentPrice(m: GetMyMembershipResponse): number {
    const plan = this.plans().find((p) => p.code === m.planCode);
    return plan?.price ?? m.monthlyPriceCzk ?? 0;
  }

  /** Any plan, not only the annual one — the board offers both directions. */
  switchTo(planCode: string): void {
    const plan = this.plans().find((p) => p.code === planCode);
    if (!plan) return;
    this.confirmService.confirm({
      message: this.translate.instant('pages.membership.switch_dialog_message', {
        price: this.formatCzk(plan.price),
      }),
      header: this.translate.instant('pages.membership.switch_dialog_title'),
      icon: 'pi pi-arrow-up-right',
      acceptLabel: this.translate.instant('pages.membership.switch_dialog_confirm'),
      rejectLabel: this.translate.instant('common.back'),
      accept: () => this.facade.swapPlan(planCode),
    });
  }

  formatDate(date: Date | undefined): string {
    if (!date) return '';
    return new Date(date).toLocaleDateString(this.getLocale(), {
      day: 'numeric',
      month: 'long',
      year: 'numeric',
    });
  }
}
