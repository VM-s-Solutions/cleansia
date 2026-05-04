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
import { Router } from '@angular/router';
import { CleansiaButtonComponent } from '@cleansia/components';
import {
  CreateMembershipCheckoutSessionCommand,
  CustomerClient,
  GetMembershipPlansResponse,
} from '@cleansia/customer-services';
import { CleansiaCustomerRoute, SnackbarService } from '@cleansia/services';
import { TranslatePipe } from '@ngx-translate/core';
import { catchError, of } from 'rxjs';

/**
 * Marketing + start-checkout page for Cleansia Plus on the web. Lists the
 * perks, lets the user pick monthly or annual, then redirects to a
 * Stripe-hosted Checkout Session in subscription mode. The local
 * UserMembership row is provisioned by the customer.subscription.created
 * webhook on success — no client-side polling needed.
 */
@Component({
  selector: 'cleansia-customer-membership-subscribe',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, TranslatePipe, CleansiaButtonComponent],
  templateUrl: './membership-subscribe.component.html',
})
export class MembershipSubscribeComponent implements OnInit {
  // Always go through CustomerClient — direct injection of MembershipClient
  // hits NSwag's empty-string default baseUrl and bypasses CUSTOMER_API_BASE_URL.
  private readonly customerClient = inject(CustomerClient);
  private readonly client = this.customerClient.membershipClient;
  private readonly snackbar = inject(SnackbarService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly router = inject(Router);

  submitting = signal(false);
  plans = signal<GetMembershipPlansResponse[]>([]);
  selectedCode = signal<string>('PLUS_MONTHLY');

  /** Currently selected plan; null while plans are loading. */
  readonly selectedPlan = computed(() =>
    this.plans().find((p) => p.code === this.selectedCode()) ?? null,
  );

  ngOnInit(): void {
    this.loadPlans();
  }

  private loadPlans(): void {
    this.client
      .getPlans()
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        catchError(() => of<GetMembershipPlansResponse[]>([])),
      )
      .subscribe((plans) => {
        this.plans.set(plans);
        // Default to the first Monthly plan; falls back to the first
        // available plan if monthly isn't seeded.
        const monthly = plans.find((p) => p.billingInterval === 1);
        if (monthly && monthly.code) this.selectedCode.set(monthly.code);
        else if (plans.length > 0 && plans[0].code) this.selectedCode.set(plans[0].code);
      });
  }

  selectPlan(code: string | undefined): void {
    // Generated DTO types code as `string | undefined`. Defensive: ignore
    // empty selections rather than corrupting selectedCode with empty string.
    if (code) this.selectedCode.set(code);
  }

  startCheckout(): void {
    if (this.submitting()) return;
    const planCode = this.selectedCode();
    if (!planCode) return;
    this.submitting.set(true);

    const origin = typeof window !== 'undefined' ? window.location.origin : '';
    // Land on the celebration page after Stripe confirms — replaces the
    // silent /membership?subscribed=1 path that left users with no
    // affirmation. The welcome page itself routes onward to "Set up
    // recurring" (headline perk) or back to management.
    const successUrl = `${origin}/${CleansiaCustomerRoute.MEMBERSHIP}/welcome`;
    const cancelUrl = `${origin}/${CleansiaCustomerRoute.MEMBERSHIP}/subscribe`;

    const command = new CreateMembershipCheckoutSessionCommand({
      planCode,
      successUrl,
      cancelUrl,
      // UserId is enriched server-side from the JWT; the wire field is
      // required by the generated DTO so we send an empty string here.
      userId: '',
    });

    this.client
      .createCheckoutSession(command)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (response) => {
          if (response?.checkoutUrl && typeof window !== 'undefined') {
            window.location.href = response.checkoutUrl;
          } else {
            this.submitting.set(false);
            this.snackbar.showErrorTranslated('membership.stripe_customer_required');
          }
        },
        error: (err) => {
          this.submitting.set(false);
          this.snackbar.showApiError(err, 'membership.stripe_customer_required');
        },
      });
  }

  goBack(): void {
    this.router.navigate([CleansiaCustomerRoute.MEMBERSHIP]);
  }

  formatCzk(amount: number): string {
    const rounded = amount % 1 === 0 ? amount.toFixed(0) : amount.toFixed(2);
    return `${rounded} Kč`;
  }
}
