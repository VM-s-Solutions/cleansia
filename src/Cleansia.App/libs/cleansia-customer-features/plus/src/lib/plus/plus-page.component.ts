import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { FoamEdgeComponent } from '@cleansia-customer/home';
import { CustomerAuthService } from '@cleansia/customer-services';
import { EXPRESS_SURCHARGE_RATE } from '@cleansia/models';
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
  imports: [CommonModule, RouterLink, TranslatePipe, FoamEdgeComponent],
  templateUrl: './plus-page.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [PlusPageFacade],
})
export class PlusPageComponent implements OnInit {
  private readonly authService = inject(CustomerAuthService);
  readonly facade = inject(PlusPageFacade);

  private readonly isLoggedIn = this.authService.isLoggedIn;

  /**
   * Mirrors `BookingPolicy.ExpressSurchargeRate` via the shared booking-window
   * model the wizard and the home calculator already read. It is the "without
   * membership" side of the express row, so it has to be the same number the
   * scheduling step quotes.
   */
  readonly expressRatePercent = Math.round(EXPRESS_SURCHARGE_RATE * 100);

  /**
   * Where "start the trial" goes. Signing up is the first step for a visitor
   * with no account, and the subscribe screen for one who has already signed
   * in — including a member, whose subscribe screen is the right place to be
   * told they already have it.
   *
   * Bound as an attribute rather than branched with `@if`. `isLoggedIn()`
   * resolves differently on the server than at hydration, and a conditional
   * that changes the node count under it is an NG0500 — the navbar's
   * `ordersLink` is the same shape for the same reason.
   */
  readonly subscribeLink = computed(() =>
    this.isLoggedIn()
      ? `/${CleansiaCustomerRoute.MEMBERSHIP}/subscribe`
      : `/${CleansiaCustomerRoute.REGISTER}`,
  );

  readonly orderLink = `/${CleansiaCustomerRoute.ORDER}`;

  ngOnInit(): void {
    this.facade.load();
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
