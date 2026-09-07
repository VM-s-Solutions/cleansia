import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  input,
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

/** Mirrors the backend's `BillingInterval` — an enum, not a month count. */
const BillingInterval = { Monthly: 1, Yearly: 2 } as const;

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
  /**
   * Suppresses this screen's own hero so it can sit INSIDE the Plus page,
   * which brings its own. `/plus` is now the single surface for the product —
   * benefits for everyone, this panel on top for a member — and two stacked
   * page titles would be the giveaway that it is two pages in a trenchcoat.
   *
   * A static input, so it renders identically on the server and at hydration.
   */
  readonly embedded = input(false);

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
    this.router.navigate([CleansiaCustomerRoute.PLUS]);
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
  /**
   * `BillingInterval` is an ENUM — `Monthly = 1, Yearly = 2` — not a count of
   * months. Both of these read `>= 12` and so called every plan monthly,
   * including the annual one: the switcher offered "MONTHLY · CZK 2,029 ·
   * every month". `MembershipPlanFactsService` has always split the two on
   * `=== 1` / `=== 2`, which is the reading this now shares.
   */
  cadenceOf(plan: GetMembershipPlansResponse): string {
    return plan.billingInterval === BillingInterval.Yearly ? 'yearly' : 'monthly';
  }

  cadenceKey(m: GetMyMembershipResponse): string | null {
    if (m.billingInterval == null) return null;
    return m.billingInterval === BillingInterval.Yearly
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
