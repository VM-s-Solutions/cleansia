import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterModule } from '@angular/router';
import { CustomerAuthService } from '@cleansia/customer-services';
import { TranslatePipe } from '@ngx-translate/core';

/**
 * Cleansia Plus, stated with the figures the seed actually pins:
 * DiscountPercentage 5.00, FreeCancellationWindowHours 4,
 * ExpressUpgradesPerMonth 1, TrialPeriodDays 14, 199 CZK monthly.
 *
 * The price is copy rather than a live read of `GetMembershipPlans` — worth
 * revisiting if plans ever vary by tenant or country. The public `/plus` page
 * does read the plan, so the two can disagree if a price changes; that band is
 * the one to convert first.
 */
@Component({
  selector: 'cleansia-plus',
  templateUrl: './plus.component.html',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, RouterModule],
})
export class PlusComponent {
  private readonly authService = inject(CustomerAuthService);

  /**
   * This CTA pointed at `/membership/subscribe` unconditionally, which is
   * behind `customerAuthGuard` — so the home page's argument for a paid
   * membership ended, for every visitor without an account, at a login form.
   * They get the public page instead; a signed-in visitor still goes straight
   * to checkout.
   *
   * Bound as an attribute rather than branched: `isLoggedIn()` resolves
   * differently on the server than at hydration, and changing the node count
   * here is an NG0500.
   */
  readonly ctaLink = computed(() =>
    this.authService.isLoggedIn() ? '/membership/subscribe' : '/plus',
  );
}
