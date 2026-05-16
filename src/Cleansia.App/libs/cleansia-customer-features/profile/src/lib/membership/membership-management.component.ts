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
  ],
  providers: [ConfirmationService, MembershipFacade],
  templateUrl: './membership-management.component.html',
})
export class MembershipManagementComponent implements OnInit {
  private readonly facade = inject(MembershipFacade);
  private readonly translate = inject(TranslateService);
  private readonly confirmService = inject(ConfirmationService);
  private readonly router = inject(Router);

  // Re-expose facade signals so existing template bindings keep working.
  readonly loading = this.facade.loading;
  readonly cancelling = this.facade.cancelling;
  readonly switching = this.facade.switching;
  readonly membership = this.facade.membership;
  readonly plans = this.facade.plans;

  /** Yearly plan (if any) — drives the "Switch to annual" CTA visibility. */
  readonly yearlyPlan = computed(() =>
    this.plans().find((p) => p.billingInterval === 2),
  );

  /**
   * Show the upgrade CTA only when the user is on a Monthly plan and a
   * Yearly plan exists in the catalog and they haven't requested cancel.
   */
  readonly showSwitchCta = computed(() => {
    const m = this.membership();
    return (
      m?.hasMembership === true &&
      m?.billingInterval === 1 &&
      m?.cancelRequested === false &&
      this.yearlyPlan() !== undefined
    );
  });

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
   * Open the prorated upgrade confirm dialog. Backend handles the actual
   * Stripe swap + invoice — we just show the price the user will be charged
   * (full annual amount) so they don't get a surprise.
   */
  confirmSwitchToAnnual(): void {
    const yearly = this.yearlyPlan();
    if (!yearly) return;
    this.confirmService.confirm({
      message: this.translate.instant('pages.membership.switch_dialog_message', {
        price: this.formatCzk(yearly.price),
      }),
      header: this.translate.instant('pages.membership.switch_dialog_title'),
      icon: 'pi pi-arrow-up-right',
      acceptLabel: this.translate.instant('pages.membership.switch_dialog_confirm'),
      rejectLabel: this.translate.instant('common.back'),
      accept: () => this.facade.swapPlan(yearly.code!),
    });
  }

  formatCzk(amount: number): string {
    const rounded = amount % 1 === 0 ? amount.toFixed(0) : amount.toFixed(2);
    return `${rounded} Kč`;
  }
}
