import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
} from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { FoamEdgeComponent } from '@cleansia-customer/home';
import { MembershipManagementComponent } from '@cleansia-customer/profile';
import { CustomerAuthService } from '@cleansia/customer-services';
import { EXPRESS_SURCHARGE_RATE } from '@cleansia/models';
import { clearOnBackForwardRestore } from '@cleansia/utils';
import { CleansiaCustomerRoute } from '@cleansia/services';
import { TranslatePipe } from '@ngx-translate/core';
import { PlusPageFacade } from './plus-page.facade';

/**
 * Cleansia Plus, for someone who has not bought it — and, until now, could not
 * read about it: the nav bar's "Cleansia Plus" link and the home page's Plus
 * CTA both pointed into `/membership`, which is behind `customerAuthGuard`, so
 * the one surface arguing for a paid upgrade bounced every anonymous visitor
 * to a login form with no explanation. This page is what those links now open.
 *
 * It states five perks, not the four the design board carried: requesting a
 * preferred cleaner is gated on membership too
 * (`PreferredEmployeeMembershipRequired`), and the authenticated subscribe page
 * has always advertised it. A visitor who counts four here and finds five after
 * paying is a happy accident, but it is still the page under-selling the thing
 * it exists to sell.
 */
@Component({
  selector: 'cleansia-customer-plus-page',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    TranslatePipe,
    FoamEdgeComponent,
    MembershipManagementComponent,
  ],
  templateUrl: './plus-page.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [PlusPageFacade],
})
export class PlusPageComponent implements OnInit {
  private readonly authService = inject(CustomerAuthService);
  private readonly router = inject(Router);
  readonly facade = inject(PlusPageFacade);

  private readonly isLoggedIn = this.authService.isLoggedIn;

  constructor() {
    // Leaving for Stripe keeps `submitting` set on purpose — see startCheckout.
    // Pressing Back restores this page with that flag still true, and the
    // trial buttons come back permanently dead.
    clearOnBackForwardRestore(this.facade.submitting);
  }

  /**
   * Mirrors `BookingPolicy.ExpressSurchargeRate` via the shared booking-window
   * model the wizard and the home calculator already read. It is the "without
   * membership" side of the express row, so it has to be the same number the
   * scheduling step quotes.
   */
  readonly expressRatePercent = Math.round(EXPRESS_SURCHARGE_RATE * 100);

  /**
   * What "start the trial" does, now that this page IS the subscribe page.
   *
   * Always a <button>, never a link that becomes a button. `isLoggedIn()`
   * resolves differently on the server than at hydration, and a conditional
   * that changes the node count under it is an NG0500 — which is exactly what
   * the attribute-bound `subscribeLink` this replaces existed to avoid. One
   * element in every state, and the BEHAVIOUR branches inside the handler
   * where hydration cannot see it.
   */
  startTrial(planCode?: string): void {
    if (!this.isLoggedIn()) {
      this.router.navigate([`/${CleansiaCustomerRoute.REGISTER}`]);
      return;
    }
    // A member pressing a plan button is switching plans, which is the
    // management panel's job, not a second subscription.
    if (this.facade.isMember()) {
      this.router.navigate([], { fragment: 'membership' });
      return;
    }
    const code = planCode ?? this.facade.monthlyPlan()?.code ?? this.facade.plans()[0]?.code;
    if (code) this.facade.startCheckout(code);
  }

  readonly orderLink = `/${CleansiaCustomerRoute.ORDER}`;

  ngOnInit(): void {
    this.facade.load();
    // Only for someone who could have one. This page's majority traffic is
    // anonymous and must never wait on an authenticated call to read the
    // marketing below.
    if (this.isLoggedIn()) {
      this.facade.refreshMembership();
    }
  }

  /**
   * Whole korunas. Both seeded plans are whole numbers and a "199,00 Kč" on a
   * price card reads as a form field rather than a price; the fractional case
   * is kept because an admin can price a plan to the halér.
   */
  formatCzk(amount: number): string {
    const fractionDigits = amount % 1 === 0 ? 0 : 2;
    return new Intl.NumberFormat('cs-CZ', {
      style: 'currency',
      currency: 'CZK',
      minimumFractionDigits: fractionDigits,
      maximumFractionDigits: fractionDigits,
    }).format(amount);
  }
}
