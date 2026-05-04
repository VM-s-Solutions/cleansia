import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { CleansiaButtonComponent } from '@cleansia/components';
import {
  CustomerClient,
  GetMembershipPlansResponse,
  GetMyMembershipResponse,
  SwapMembershipPlanCommand,
} from '@cleansia/customer-services';
import { CleansiaCustomerRoute, SnackbarService } from '@cleansia/services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ConfirmationService } from 'primeng/api';
import { SkeletonModule } from 'primeng/skeleton';
import { catchError, of } from 'rxjs';

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
  providers: [ConfirmationService],
  templateUrl: './membership-management.component.html',
})
export class MembershipManagementComponent implements OnInit {
  // Always go through CustomerClient — direct injection of MembershipClient
  // hits NSwag's empty-string default baseUrl and bypasses CUSTOMER_API_BASE_URL.
  private readonly customerClient = inject(CustomerClient);
  private readonly client = this.customerClient.membershipClient;
  private readonly snackbar = inject(SnackbarService);
  private readonly translate = inject(TranslateService);
  private readonly confirmService = inject(ConfirmationService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly router = inject(Router);

  loading = signal(true);
  cancelling = signal(false);
  switching = signal(false);
  membership = signal<GetMyMembershipResponse | null>(null);
  plans = signal<GetMembershipPlansResponse[]>([]);

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
    this.loadPlans();
  }

  refresh(): void {
    this.loading.set(true);
    this.client
      .getMine()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (response) => {
          this.membership.set(response);
          this.loading.set(false);
        },
        error: (err) => {
          this.snackbar.showApiError(err, 'membership.not_found');
          this.loading.set(false);
        },
      });
  }

  private loadPlans(): void {
    // Anonymous-friendly endpoint — no auth required, but interceptor adds
    // bearer if present. Failing silently is fine; the switch CTA just won't show.
    this.client
      .getPlans()
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        catchError(() => of<GetMembershipPlansResponse[]>([])),
      )
      .subscribe((plans) => this.plans.set(plans));
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
      accept: () => this.cancel(),
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
      accept: () => this.swapToAnnual(yearly.code!),
    });
  }

  private swapToAnnual(planCode: string): void {
    this.switching.set(true);
    const command = new SwapMembershipPlanCommand({
      newPlanCode: planCode,
      // UserId is enriched server-side from JWT; field is required on the
      // generated DTO so we send empty.
      userId: '',
    });
    this.client
      .swapPlan(command)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.switching.set(false);
          this.snackbar.showSuccessTranslated('pages.membership.switch_success');
          this.refresh();
        },
        error: (err) => {
          this.switching.set(false);
          this.snackbar.showApiError(err, 'membership.swap_same_plan');
        },
      });
  }

  private cancel(): void {
    this.cancelling.set(true);
    this.client
      .cancel()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.cancelling.set(false);
          this.snackbar.showSuccessTranslated('pages.membership.cancel_success');
          this.refresh();
        },
        error: (err) => {
          this.cancelling.set(false);
          this.snackbar.showApiError(err, 'membership.not_found');
        },
      });
  }

  formatCzk(amount: number): string {
    const rounded = amount % 1 === 0 ? amount.toFixed(0) : amount.toFixed(2);
    return `${rounded} Kč`;
  }
}
