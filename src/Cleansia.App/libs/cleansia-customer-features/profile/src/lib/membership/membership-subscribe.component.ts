import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { Router } from '@angular/router';
import { CleansiaButtonComponent } from '@cleansia/components';
import { CleansiaCustomerRoute } from '@cleansia/services';
import { TranslatePipe } from '@ngx-translate/core';
import { MembershipFacade } from './membership.facade';

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
  providers: [MembershipFacade],
  templateUrl: './membership-subscribe.component.html',
})
export class MembershipSubscribeComponent implements OnInit {
  private readonly facade = inject(MembershipFacade);
  private readonly router = inject(Router);

  // Re-expose facade signals so existing template bindings keep working.
  readonly submitting = this.facade.submitting;
  readonly plans = this.facade.plans;

  selectedCode = signal<string>('PLUS_MONTHLY');

  /** Currently selected plan; null while plans are loading. */
  readonly selectedPlan = computed(() =>
    this.plans().find((p) => p.code === this.selectedCode()) ?? null,
  );

  ngOnInit(): void {
    this.facade.loadPlans((plans) => {
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

    const origin = typeof window !== 'undefined' ? window.location.origin : '';
    // Land on the celebration page after Stripe confirms — replaces the
    // silent /membership?subscribed=1 path that left users with no
    // affirmation. The welcome page itself routes onward to "Set up
    // recurring" (headline perk) or back to management.
    const successUrl = `${origin}/${CleansiaCustomerRoute.MEMBERSHIP}/welcome`;
    const cancelUrl = `${origin}/${CleansiaCustomerRoute.MEMBERSHIP}/subscribe`;

    this.facade.createCheckoutSession(planCode, successUrl, cancelUrl);
  }

  goBack(): void {
    this.router.navigate([CleansiaCustomerRoute.MEMBERSHIP]);
  }

  formatCzk(amount: number): string {
    const rounded = amount % 1 === 0 ? amount.toFixed(0) : amount.toFixed(2);
    return `${rounded} Kč`;
  }
}
